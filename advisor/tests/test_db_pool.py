"""Hồ chứa kết nối database cho kho kiến thức: không có DB thì trả None, chat vẫn chạy."""

import logging

import psycopg
import pytest

from app.core.config import Settings
from app.core.db import open_pool

LOCAL_DB = "postgresql://studyabroad:studyabroad@localhost:5432/studyabroad"


def local_db_available() -> bool:
    try:
        psycopg.connect(LOCAL_DB, connect_timeout=2).close()
        return True
    except psycopg.Error:
        return False


def test_no_url_means_no_pool():
    assert open_pool("") is None


def test_unreachable_database_returns_none_and_logs_instead_of_crashing(caplog):
    with caplog.at_level(logging.ERROR, logger="advisor.db"):
        pool = open_pool("postgresql://u:p@127.0.0.1:1/none", timeout=1)

    assert pool is None
    assert any("tắt tính năng" in r.getMessage() for r in caplog.records)


def test_settings_default_has_no_database_and_uses_bge_m3():
    settings = Settings(_env_file=None, database_url="", embedding_model="bge-m3")

    assert settings.database_url == ""
    assert settings.embedding_model == "bge-m3"


@pytest.mark.skipif(not local_db_available(), reason="Không có Postgres local (docker compose up db)")
def test_real_database_gives_a_pool_that_understands_vectors():
    pool = open_pool(LOCAL_DB)

    assert pool is not None
    try:
        with pool.connection() as conn:
            row = conn.execute("SELECT '[1,2,3]'::vector <=> '[1,2,3]'::vector").fetchone()
        assert row[0] == 0  # khoảng cách cosine của một vector với chính nó
    finally:
        pool.close()
