"""Tạo vector cho kho kiến thức app.knowledge_documents (dữ liệu nạp qua trang admin, import Excel).

Chạy từ thư mục advisor/ (cần DATABASE_URL và dịch vụ embedding trong .env):
  python -m scripts.ingest_knowledge --dry-run     # chỉ đếm số dòng cần tạo vector, không gọi embedding, không ghi gì
  python -m scripts.ingest_knowledge               # tạo vector thật
  python -m scripts.ingest_knowledge --limit 50    # chỉ làm tối đa 50 dòng

Dòng cần tạo vector: chưa có vector, hoặc embedding_hash khác mã băm sha256 của question hiện tại.
Mỗi lô ghi trong một giao dịch riêng nên chạy lại tiếp tục từ chỗ dở.
"""

import argparse
import asyncio
import hashlib
import sys
import time
from collections.abc import Callable
from datetime import UTC, datetime
from typing import Any

from pgvector import Vector

BATCH = 16  # số câu hỏi gửi dịch vụ embedding mỗi lần
RETRIES = 8
RETRY_WAIT_SECONDS = 20

SELECT_SQL = """
SELECT id, question, embedding_hash, (embedding IS NOT NULL) AS has_vector
FROM app.knowledge_documents ORDER BY entry_no
"""

UPDATE_SQL = """
UPDATE app.knowledge_documents
SET embedding = %(embedding)s, embedding_hash = %(hash)s, ingested_at = %(now)s
WHERE id = %(id)s AND question = %(question)s
"""


def question_hash(question: str) -> str:
    return hashlib.sha256(question.strip().encode("utf-8")).hexdigest()


def needs_embedding(question: str, embedding_hash: str | None, has_vector: bool) -> bool:
    return not has_vector or embedding_hash != question_hash(question)


def pending(conn: Any, limit: int | None = None) -> list[tuple[Any, str]]:
    rows = [(r[0], r[1]) for r in conn.execute(SELECT_SQL).fetchall() if needs_embedding(r[1], r[2], r[3])]
    return rows[:limit]


# Tạo vector cho các dòng còn thiếu; trả về số dòng đã (hoặc, khi dry_run, sẽ) được tạo vector
def embed_pending(
    conn: Any,
    embed: Callable[[list[str]], list[list[float]]],
    *,
    dry_run: bool = False,
    limit: int | None = None,
) -> int:
    rows = pending(conn, limit)
    if dry_run:
        return len(rows)
    for start in range(0, len(rows), BATCH):
        batch = rows[start : start + BATCH]
        vectors = embed([question for _, question in batch])
        now = datetime.now(UTC)
        with conn.transaction():
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
    return len(rows)


# Embedding như app/main.py: dịch vụ tương thích OpenAI nếu có EMBEDDING_BASE_URL, không thì Ollama
def make_embedder() -> Callable[[list[str]], list[list[float]]]:
    import httpx

    from app.core.config import get_settings
    from app.services.embedding import OpenAiEmbeddingClient
    from app.services.ollama import OllamaClient, OllamaError

    settings = get_settings()

    async def run(texts: list[str]) -> list[list[float]]:
        async with httpx.AsyncClient(
            base_url=settings.embedding_base_url or settings.ollama_base_url,
            timeout=settings.llm_timeout_seconds,
        ) as http:
            if settings.embedding_base_url:
                return await OpenAiEmbeddingClient(
                    http, settings.embedding_model, settings.embedding_api_key
                ).embed(texts)
            client = OllamaClient(http, settings.llm_model, embedding_model=settings.embedding_model)
            return await client.embed(texts)

    def with_retry(texts: list[str]) -> list[list[float]]:
        for attempt in range(RETRIES):
            try:
                return asyncio.run(run(texts))
            except OllamaError:  # thường là 429: dịch vụ giới hạn số lần gọi, đợi rồi thử lại
                if attempt == RETRIES - 1:
                    raise
                time.sleep(RETRY_WAIT_SECONDS)
        raise AssertionError

    return with_retry


def main() -> None:
    sys.stdout.reconfigure(encoding="utf-8")
    parser = argparse.ArgumentParser(description="Tạo vector cho kho kiến thức")
    parser.add_argument("--dry-run", action="store_true", help="chỉ đếm, không gọi embedding, không ghi gì")
    parser.add_argument("--limit", type=int, help="tối đa bao nhiêu dòng")
    args = parser.parse_args()

    import psycopg
    from pgvector.psycopg import register_vector

    from app.core.config import get_settings

    settings = get_settings()
    if not settings.database_url:
        raise SystemExit("Chưa cấu hình DATABASE_URL trong advisor/.env")

    with psycopg.connect(settings.database_url, autocommit=True) as conn:
        register_vector(conn)
        count = embed_pending(conn, make_embedder(), dry_run=args.dry_run, limit=args.limit)
    print(f"{'Cần tạo vector (dry-run, chưa ghi gì)' if args.dry_run else 'Đã tạo vector'}: {count} dòng")


if __name__ == "__main__":
    main()
