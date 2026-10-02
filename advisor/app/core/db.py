from pathlib import Path

from pgvector.psycopg import register_vector_async
from psycopg import AsyncConnection
from psycopg_pool import AsyncConnectionPool

from app.core.config import get_settings

SQL_DIR = Path(__file__).resolve().parents[2] / "sql"

_pool: AsyncConnectionPool | None = None


async def _configure(conn: AsyncConnection) -> None:
    await register_vector_async(conn)


async def open_pool() -> AsyncConnectionPool:
    global _pool
    if _pool is None:
        _pool = AsyncConnectionPool(get_settings().database_url, configure=_configure, open=False)
        await _pool.open()
    return _pool


async def close_pool() -> None:
    global _pool
    if _pool is not None:
        await _pool.close()
        _pool = None


def get_pool() -> AsyncConnectionPool:
    if _pool is None:
        raise RuntimeError("Database pool is not open")
    return _pool


async def run_schema_sql() -> None:
    """Apply idempotent schema files in sql/ (advisor schema, documents table, indexes)."""
    async with await AsyncConnection.connect(get_settings().database_url, autocommit=True) as conn:
        for file in sorted(SQL_DIR.glob("*.sql")):
            await conn.execute(file.read_text(encoding="utf-8"))
