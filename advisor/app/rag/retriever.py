from dataclasses import dataclass

import numpy as np
from psycopg.rows import dict_row
from psycopg_pool import AsyncConnectionPool

from app.core.study_levels import GENERAL

# Hybrid search: vector similarity (semantic) + full-text (exact terms like F-1, I-20, SEVIS),
# merged with Reciprocal Rank Fusion (k = 60).
# Level filter: if a level is given, match that level plus "general" documents; otherwise search everything.
HYBRID_SQL = """
WITH vec AS (
    SELECT id, ROW_NUMBER() OVER (ORDER BY embedding <=> %(emb)s) AS rnk
    FROM advisor.documents
    WHERE %(level)s::text IS NULL OR study_level IN (%(level)s, %(general)s)
    ORDER BY embedding <=> %(emb)s
    LIMIT %(k)s
),
kw AS (
    SELECT id, ROW_NUMBER() OVER (ORDER BY ts_rank(content_tsv, q) DESC) AS rnk
    FROM advisor.documents, plainto_tsquery('simple', %(query)s) q
    WHERE content_tsv @@ q
      AND (%(level)s::text IS NULL OR study_level IN (%(level)s, %(general)s))
    ORDER BY ts_rank(content_tsv, q) DESC
    LIMIT %(k)s
),
fused AS (
    SELECT id, SUM(1.0 / (60 + rnk)) AS score
    FROM (SELECT * FROM vec UNION ALL SELECT * FROM kw) s
    GROUP BY id
)
SELECT d.id, d.title, d.source_url, d.content, d.doc_type, d.study_level, f.score::float AS score
FROM fused f JOIN advisor.documents d ON d.id = f.id
ORDER BY f.score DESC
LIMIT %(top)s
"""


@dataclass
class RetrievedChunk:
    id: int
    title: str
    source_url: str | None
    content: str
    doc_type: str
    study_level: str
    score: float


class Retriever:
    def __init__(self, pool: AsyncConnectionPool, candidate_k: int, top_k: int):
        self._pool = pool
        self._candidate_k = candidate_k
        self._top_k = top_k

    async def search(
        self, query: str, embedding: list[float], study_level: str | None = None
    ) -> list[RetrievedChunk]:
        async with self._pool.connection() as conn, conn.cursor(row_factory=dict_row) as cur:
            await cur.execute(
                HYBRID_SQL,
                {
                    "emb": np.array(embedding, dtype=np.float32),
                    "query": query,
                    "level": study_level,
                    "general": GENERAL,
                    "k": self._candidate_k,
                    "top": self._top_k,
                },
            )
            rows = await cur.fetchall()
        return [RetrievedChunk(**row) for row in rows]
