"""Tạo vector cho kho kiến thức app.knowledge_documents.

Dùng chung cho hai nơi: script scripts/ingest_knowledge.py (chạy tay) và API POST /api/v1/knowledge/embed-missing
(backend gọi sau khi moderator import file Excel). Dòng cần tạo vector: chưa có vector, hoặc embedding_hash khác
mã băm sha256 của question hiện tại (câu hỏi đã đổi thì backend xóa vector, hàm này tạo lại).
"""

import asyncio
import hashlib
import logging
from datetime import UTC, datetime
from typing import Any

from pgvector import Vector

from app.services import usage_meter
from app.services.ollama import OllamaError

logger = logging.getLogger("advisor.knowledge_embedding")

BATCH = 16  # số câu hỏi gửi dịch vụ embedding mỗi lần
RETRIES = 3
RETRY_WAIT_SECONDS = 20

SELECT_SQL = """
SELECT id, question, embedding_hash, (embedding IS NOT NULL) AS has_vector
FROM app.knowledge_documents ORDER BY entry_no
"""

# AND question = ...: nếu moderator vừa sửa câu hỏi trong lúc đang tạo vector thì không ghi vector của câu cũ
UPDATE_SQL = """
UPDATE app.knowledge_documents
SET embedding = %(embedding)s, embedding_hash = %(hash)s, ingested_at = %(now)s
WHERE id = %(id)s AND question = %(question)s
"""


def question_hash(question: str) -> str:
    return hashlib.sha256(question.strip().encode("utf-8")).hexdigest()


def needs_embedding(question: str, embedding_hash: str | None, has_vector: bool) -> bool:
    return not has_vector or embedding_hash != question_hash(question)


# Các dòng cần tạo vector: (id, question)
def pending(conn: Any, limit: int | None = None) -> list[tuple[Any, str]]:
    rows = [(r[0], r[1]) for r in conn.execute(SELECT_SQL).fetchall() if needs_embedding(r[1], r[2], r[3])]
    return rows[:limit]


# Chạy tạo vector ở nền cho API: mỗi lúc chỉ một lần chạy, trạng thái để trang admin hỏi
class KnowledgeEmbedJob:
    def __init__(
        self,
        pool: Any,
        embedder: Any,
        batch: int = BATCH,
        retries: int = RETRIES,
        retry_wait: float = RETRY_WAIT_SECONDS,
    ) -> None:
        self._pool = pool
        self._embedder = embedder  # có hàm async embed(list[str]) -> list[list[float]]
        self._batch = batch
        self._retries = retries
        self._retry_wait = retry_wait
        self._task: asyncio.Task[None] | None = None
        self._sweeper: asyncio.Task[None] | None = None
        self.done = 0  # số dòng đã tạo vector trong lần chạy gần nhất
        self.error: str | None = (
            None  # lần chạy gần nhất lỗi thì có chữ, không kèm chi tiết (chi tiết nằm trong log)
        )

    @property
    def running(self) -> bool:
        return self._task is not None and not self._task.done()

    def _load_pending(self) -> list[tuple[Any, str]]:
        with self._pool.connection() as conn:
            return pending(conn)

    async def count_pending(self) -> int:
        return len(await asyncio.to_thread(self._load_pending))

    # Bắt đầu chạy nền; trả False nếu đang có một lần chạy (không chạy chồng). Phải gọi trong vòng lặp asyncio
    def start(self) -> bool:
        if self.running:
            return False
        self.done = 0
        self.error = None
        self._task = asyncio.create_task(self._run())
        return True

    # Quét định kỳ: dòng thiếu vector mà không ai nhờ (backend gọi hụt, advisor tắt lúc nhập, sửa thẳng trong DB) vẫn được tạo bù.
    # Phải gọi trong vòng lặp asyncio; gọi lần hai khi đang quét thì không làm gì
    def start_sweeper(self, interval: float) -> None:
        if self._sweeper is None or self._sweeper.done():
            self._sweeper = asyncio.create_task(self._sweep_forever(interval))

    async def _sweep_forever(self, interval: float) -> None:
        while True:
            try:
                if not self.running and await self.count_pending() > 0:
                    logger.info("Quét định kỳ: có dòng thiếu vector, bắt đầu tạo")
                    self.start()
            except asyncio.CancelledError:
                raise
            except Exception:  # DB tạm lỗi: vòng quét không được chết, lần sau quét lại
                logger.exception("Quét dòng thiếu vector thất bại")
            await asyncio.sleep(interval)

    # Dừng khi tắt dịch vụ
    async def stop(self) -> None:
        tasks = [t for t in (self._sweeper, self._task) if t is not None and not t.done()]
        for task in tasks:
            task.cancel()
        await asyncio.gather(*tasks, return_exceptions=True)

    async def _run(self) -> None:
        try:
            rows = await asyncio.to_thread(self._load_pending)
            for start in range(0, len(rows), self._batch):
                batch = rows[start : start + self._batch]
                vectors = await self._embed_with_retry([question for _, question in batch])
                await asyncio.to_thread(self._save, batch, vectors)
                self.done += len(batch)
        except asyncio.CancelledError:
            raise
        except Exception:
            logger.exception("Tạo vector cho kho kiến thức thất bại sau %s dòng", self.done)
            self.error = "Không tạo được vector, xem log của advisor."

    # Dịch vụ embedding hay giới hạn số lần gọi (429): đợi rồi thử lại
    async def _embed_with_retry(self, texts: list[str]) -> list[list[float]]:
        for attempt in range(self._retries):
            try:
                with usage_meter.purpose(
                    "embedding"
                ):  # nhãn cho cả dịch vụ embedding kiểu OpenAI (không tự gắn nhãn)
                    return await self._embedder.embed(texts)
            except OllamaError:
                if attempt == self._retries - 1:
                    raise
                await asyncio.sleep(self._retry_wait)
        raise AssertionError

    def _save(self, batch: list[tuple[Any, str]], vectors: list[list[float]]) -> None:
        now = datetime.now(UTC)
        with (
            self._pool.connection() as conn
        ):  # thoát khối with thì lưu cả lô, lỗi giữa chừng thì hoàn tác cả lô
            for (row_id, question), vector in zip(batch, vectors, strict=True):
                conn.execute(
                    UPDATE_SQL,
                    {
                        "embedding": Vector(vector),
                        "hash": question_hash(question),
                        "now": now,
                        "id": row_id,
                        "question": question,
                    },
                )
