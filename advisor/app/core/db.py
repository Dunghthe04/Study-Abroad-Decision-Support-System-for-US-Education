"""Hồ chứa kết nối Postgres cho kho kiến thức (RAG).

Dùng bản đồng bộ (sync) của psycopg, gọi qua asyncio.to_thread: bản async của psycopg không chạy được trên Windows
với vòng lặp mặc định (ProactorEventLoop), mà máy phát triển của nhóm là Windows.
Không cấu hình DATABASE_URL hoặc không kết nối được thì trả None: chat vẫn chạy, chỉ tắt tính năng trả lời kiến thức.
"""

import logging

from pgvector.psycopg import register_vector
from psycopg import Connection
from psycopg_pool import ConnectionPool

logger = logging.getLogger("advisor.db")


# Mỗi kết nối mới được dạy hiểu kiểu vector của pgvector (thiếu extension vector thì bước này lỗi)
def _configure(conn: Connection) -> None:
    register_vector(conn)


# Mở hồ chứa kết nối; không có URL hoặc DB không phản hồi trong timeout giây thì trả None
def open_pool(database_url: str, timeout: float = 5) -> ConnectionPool | None:
    if not database_url:
        return None
    pool = ConnectionPool(database_url, min_size=1, max_size=5, configure=_configure, open=False)
    try:
        pool.open(wait=True, timeout=timeout)
    except Exception:
        logger.exception("Không kết nối được database, tắt tính năng trả lời kiến thức")
        pool.close()
        return None
    return pool
