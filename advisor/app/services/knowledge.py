"""Tìm kiếm kho kiến thức (RAG) trong Postgres bằng pgvector.

So vector của câu hỏi học sinh với vector câu hỏi mẫu của từng dòng trong app.knowledge_documents.
Chỉ lấy dòng trong phạm vi tư vấn của hệ thống, có trạng thái được phép và đúng bậc học (hoặc bậc "general").
"""

import asyncio
from dataclasses import dataclass
from datetime import date
from typing import Any

from pgvector import Vector

SEARCH_SQL = """
SELECT code, question, content, quote, source_url, source_note, retrieved_at,
       1 - (embedding <=> %(vector)s) AS score
FROM app.knowledge_documents
WHERE status = ANY(%(statuses)s)
  AND scope = 'system_advisory'
  AND embedding IS NOT NULL
  AND (%(level)s::text IS NULL OR study_level IN (%(level)s, 'general'))
ORDER BY embedding <=> %(vector)s
LIMIT %(top_k)s
"""


# Mô tả 1 kết quả tìm kiếm trong kho
# Khiến các trường của đối tượng không thể bị gán lại sau khi đối tượng được tạo.chỉ sử dụng k edit
@dataclass(frozen=True)
class KnowledgeHit:
    code: str
    question: str  # câu hỏi mẫu trong kho
    content: str  # câu trả lời chuẩn
    quote: str | None  # nguồn kiểm chứng gốc
    source_url: str | None
    source_note: str | None
    retrieved_at: date | None
    score: float  # độ tương đồng cosine (0 đến 1) giữa câu hỏi của học sinh và câu hỏi mẫu


class KnowledgeRetriever:
    def __init__(self, pool: Any, statuses: list[str], top_k: int = 4) -> None:
        self._pool = pool
        self._statuses = statuses
        self._top_k = top_k

    # Bản đồng bộ: chạy trong luồng riêng (xem search) vì hồ chứa kết nối là bản sync
    def search_sync(self, embedding: list[float], study_level: str | None = None) -> list[KnowledgeHit]:
        params = {
            "vector": Vector(embedding),
            "statuses": self._statuses,
            "level": study_level,
            "top_k": self._top_k,
        }
        with self._pool.connection() as conn:
            rows = conn.execute(SEARCH_SQL, params).fetchall()
        hits = []

        for row in rows:
            hit = KnowledgeHit(
                code=row[0],
                question=row[1],
                content=row[2],
                quote=row[3],
                source_url=row[4],
                source_note=row[5],
                retrieved_at=row[6],
                score=float(row[7]),
            )
            hits.append(hit)

        return hits

    # Trả về tối đa top_k dòng gần nghĩa nhất, xếp theo độ tương đồng giảm dần
    async def search(self, embedding: list[float], study_level: str | None = None) -> list[KnowledgeHit]:
        # asyncio.to_thread() là cách nhờ một thread khác chạy hàm đang có, để luồng xử lý chính không phải đứng chờ trực tiếp hàm đó chạy xong.
        return await asyncio.to_thread(self.search_sync, embedding, study_level)
