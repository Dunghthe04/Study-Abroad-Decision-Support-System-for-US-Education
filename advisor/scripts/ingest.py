"""Offline ingestion: data/processed/*.jsonl -> chunks -> bge-m3 embeddings -> advisor.documents.

Each JSONL line is one cleaned source document:
{"title": "...", "source_url": "https://...", "doc_type": "visa", "study_level": "general",
 "retrieved_at": "2026-10-01", "content": "..."}

study_level: secondary | community_college | undergraduate | master | phd | general (applies to all levels)

Run from the advisor/ folder:  python -m scripts.ingest [--reset]
"""

import argparse
import asyncio
import json
from pathlib import Path

import numpy as np
from pgvector.psycopg import register_vector_async
from psycopg import AsyncConnection
from psycopg.types.json import Jsonb

from app.core.config import get_settings
from app.core.db import run_schema_sql
from app.core.study_levels import GENERAL
from app.services.ollama import OllamaClient

PROCESSED_DIR = Path(__file__).resolve().parents[1] / "data" / "processed"
CHUNK_CHARS = 2000  # ~500 tokens
OVERLAP_CHARS = 300
MIN_CHUNK_CHARS = 120
BATCH_SIZE = 16

UPSERT_SQL = """
INSERT INTO advisor.documents
    (source_url, title, doc_type, study_level, chunk_index, content, embedding, metadata, retrieved_at)
VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s)
ON CONFLICT (source_url, chunk_index) DO UPDATE SET
    title = EXCLUDED.title, doc_type = EXCLUDED.doc_type, study_level = EXCLUDED.study_level,
    content = EXCLUDED.content, embedding = EXCLUDED.embedding,
    metadata = EXCLUDED.metadata, retrieved_at = EXCLUDED.retrieved_at
"""


def chunk_text(text: str) -> list[str]:
    """Pack paragraphs into ~CHUNK_CHARS chunks, carrying OVERLAP_CHARS of context forward."""
    paragraphs = [p.strip() for p in text.split("\n\n") if p.strip()]
    chunks: list[str] = []
    current = ""
    for para in paragraphs:
        if current and len(current) + len(para) + 2 > CHUNK_CHARS:
            chunks.append(current)
            current = current[-OVERLAP_CHARS:]
        current = f"{current}\n\n{para}" if current else para
        while len(current) > CHUNK_CHARS:
            chunks.append(current[:CHUNK_CHARS])
            current = current[CHUNK_CHARS - OVERLAP_CHARS :]
    if current:
        chunks.append(current)
    return [c for c in chunks if len(c) >= MIN_CHUNK_CHARS]


def load_documents() -> list[dict]:
    docs = []
    for file in sorted(PROCESSED_DIR.glob("*.jsonl")):
        with file.open(encoding="utf-8") as f:
            docs.extend(json.loads(line) for line in f if line.strip())
    return docs


async def main(reset: bool) -> None:
    settings = get_settings()
    await run_schema_sql()
    llm = OllamaClient(settings)

    rows = []
    for doc in load_documents():
        for i, chunk in enumerate(chunk_text(doc["content"])):
            rows.append((doc, i, chunk))
    print(f"{len(rows)} chunks from {PROCESSED_DIR}")

    async with await AsyncConnection.connect(settings.database_url) as conn:
        await register_vector_async(conn)
        if reset:
            await conn.execute("TRUNCATE advisor.documents")
        for start in range(0, len(rows), BATCH_SIZE):
            batch = rows[start : start + BATCH_SIZE]
            embeddings = await llm.embed([chunk for _, _, chunk in batch])
            async with conn.cursor() as cur:
                await cur.executemany(
                    UPSERT_SQL,
                    [
                        (
                            doc.get("source_url"),
                            doc["title"],
                            doc.get("doc_type", "general"),
                            doc.get("study_level", GENERAL),
                            i,
                            chunk,
                            np.array(emb, dtype=np.float32),
                            Jsonb(doc.get("metadata", {})),
                            doc.get("retrieved_at"),
                        )
                        for (doc, i, chunk), emb in zip(batch, embeddings, strict=True)
                    ],
                )
            await conn.commit()
            print(f"  ingested {min(start + BATCH_SIZE, len(rows))}/{len(rows)}")

    await llm.aclose()


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--reset", action="store_true", help="Truncate the knowledge base first")
    asyncio.run(main(parser.parse_args().reset))
