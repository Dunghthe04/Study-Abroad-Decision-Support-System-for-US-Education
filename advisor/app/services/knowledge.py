"""Tìm kiếm kho kiến thức (RAG) trong Postgres bằng pgvector.

So vector của câu hỏi học sinh với vector câu hỏi mẫu của từng dòng trong app.knowledge_documents.
Mọi dòng đã có vector đều dùng được (kho không còn trạng thái duyệt).
"""

import asyncio
from dataclasses import dataclass
from datetime import date
from typing import Any

from pgvector import Vector

SEARCH_SQL = """
SELECT entry_no, question, answer, source, source_url, doc_type, label, topic_code, retrieved_at, version,
       1 - (embedding <=> %(vector)s) AS score
FROM app.knowledge_documents
WHERE embedding IS NOT NULL AND deleted_at IS NULL
ORDER BY embedding <=> %(vector)s
LIMIT %(top_k)s
"""

# Các phần của một thủ tục (là gì, chuẩn bị, các bước, lỗi hay gặp...): cùng topic_code
BY_TOPIC_SQL = """
SELECT entry_no, question, doc_type
FROM app.knowledge_documents
WHERE topic_code = %(code)s AND embedding IS NOT NULL AND deleted_at IS NULL
ORDER BY entry_no
"""


# Mô tả 1 kết quả tìm kiếm trong kho
@dataclass(frozen=True)
class KnowledgeHit:
    entry_no: int
    question: str  # câu hỏi mẫu trong kho
    answer: str  # câu trả lời chuẩn
    source: str | None  # tên nguồn
    source_url: str | None
    doc_type: str
    label: str
    topic_code: str | None
    retrieved_at: date | None
    version: int
    score: float  # độ tương đồng cosine (0 đến 1) giữa câu hỏi của học sinh và câu hỏi mẫu


# Một phần của thủ tục (để làm nút "Các bước", "Lỗi hay gặp"...)
@dataclass(frozen=True)
class TopicPart:
    entry_no: int
    question: str
    doc_type: str


class KnowledgeRetriever:
    def __init__(self, pool: Any, top_k: int = 4) -> None:
        self._pool = pool
        self._top_k = top_k

    # Bản đồng bộ: chạy trong luồng riêng (xem search) vì hồ chứa kết nối là bản sync
    def search_sync(self, embedding: list[float]) -> list[KnowledgeHit]:
        params = {"vector": Vector(embedding), "top_k": self._top_k}
        with self._pool.connection() as conn:
            rows = conn.execute(SEARCH_SQL, params).fetchall()
        return [KnowledgeHit(*row[:-1], score=float(row[-1])) for row in rows]

    # Các phần khác của cùng một thủ tục (cùng topic_code)
    def by_topic_sync(self, topic_code: str) -> list[TopicPart]:
        with self._pool.connection() as conn:
            rows = conn.execute(BY_TOPIC_SQL, {"code": topic_code}).fetchall()
        return [TopicPart(*row) for row in rows]

    async def by_topic(self, topic_code: str) -> list[TopicPart]:
        return await asyncio.to_thread(self.by_topic_sync, topic_code)

    # Trả về tối đa top_k dòng gần nghĩa nhất, xếp theo độ tương đồng giảm dần
    async def search(self, embedding: list[float]) -> list[KnowledgeHit]:
        return await asyncio.to_thread(self.search_sync, embedding)
