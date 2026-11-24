"""Script tạo vector cho kho kiến thức: phần quyết định là hàm thuần; phần ghi DB thử trên Postgres local rồi hoàn tác."""

import hashlib

import psycopg
import pytest
from pgvector.psycopg import register_vector

from scripts.ingest_knowledge import BATCH, embed_pending, needs_embedding, question_hash

LOCAL_DB = "postgresql://studyabroad:studyabroad@localhost:5432/studyabroad"


def fake_embed_factory():
    """Dịch vụ embedding giả: vector 1024 số tạo từ chữ, và ghi lại những câu đã được yêu cầu."""
    asked: list[str] = []

    def embed(texts: list[str]) -> list[list[float]]:
        asked.extend(texts)
        return [[b / 255 for b in hashlib.sha256(t.encode()).digest()] * 32 for t in texts]

    return embed, asked


# --- hàm thuần ---


def test_a_row_without_a_vector_needs_one():
    assert needs_embedding("Câu hỏi?", None, False)
    assert needs_embedding("Câu hỏi?", question_hash("Câu hỏi?"), False)


def test_a_row_with_a_matching_hash_is_left_alone():
    assert not needs_embedding("Câu hỏi?", question_hash("Câu hỏi?"), True)
    assert not needs_embedding("  Câu hỏi?  ", question_hash("Câu hỏi?"), True)


def test_a_row_whose_question_changed_or_has_no_hash_needs_a_new_vector():
    assert needs_embedding("Câu hỏi khác?", question_hash("Câu hỏi?"), True)
    assert needs_embedding("Câu hỏi?", None, True)


def test_question_hash_is_64_chars():
    assert len(question_hash("x")) == 64


# --- ghi vào DB thật (Postgres local), mọi thứ hoàn tác ở cuối ---


def db_ready() -> bool:
    try:
        with psycopg.connect(LOCAL_DB, connect_timeout=2) as conn:
            return (
                conn.execute(
                    "SELECT count(*) FROM information_schema.columns WHERE table_schema='app' "
                    "AND table_name='knowledge_documents' AND column_name IN ('entry_no', 'embedding_hash')"
                ).fetchone()[0]
                == 2
            )
    except psycopg.Error:
        return False


needs_db = pytest.mark.skipif(
    not db_ready(), reason="Không có Postgres local với bảng knowledge_documents đã migrate"
)

INSERT_SQL = """
INSERT INTO app.knowledge_documents (id, entry_no, doc_type, label, question, answer, retrieved_at, created_at, updated_at)
VALUES (gen_random_uuid(), %s, 'faq', 'LABEL_X', %s, 'Câu trả lời.', '2026-09-27', now(), now())
"""


@pytest.fixture
def conn():
    connection = psycopg.connect(LOCAL_DB)
    register_vector(connection)
    connection.execute(
        "DELETE FROM app.knowledge_documents"
    )  # hoàn tác ở cuối, để mỗi test bắt đầu từ kho trống
    yield connection
    connection.rollback()  # không để lại gì trong DB thật
    connection.close()


def add(conn, n: int, question: str | None = None) -> None:
    conn.execute(INSERT_SQL, (n, question or f"Câu hỏi {n}?"))


def fetch(conn, n: int) -> tuple:
    return conn.execute(
        "SELECT (embedding IS NOT NULL), embedding_hash, ingested_at, question "
        "FROM app.knowledge_documents WHERE entry_no = %s",
        (n,),
    ).fetchone()


@needs_db
def test_rows_without_vectors_get_vector_hash_and_ingest_time(conn):
    embed, asked = fake_embed_factory()
    add(conn, 1)
    add(conn, 2)

    assert embed_pending(conn, embed) == 2

    assert asked == ["Câu hỏi 1?", "Câu hỏi 2?"]
    has_vector, embedding_hash, ingested_at, _ = fetch(conn, 1)
    assert has_vector and embedding_hash == question_hash("Câu hỏi 1?") and ingested_at is not None


@needs_db
def test_running_again_asks_for_nothing(conn):
    embed, asked = fake_embed_factory()
    add(conn, 1)
    embed_pending(conn, embed)
    asked.clear()

    assert embed_pending(conn, embed) == 0
    assert asked == []


@needs_db
def test_a_changed_question_with_cleared_vector_is_embedded_again(conn):
    embed, asked = fake_embed_factory()
    add(conn, 1)
    embed_pending(conn, embed)
    asked.clear()
    conn.execute(
        "UPDATE app.knowledge_documents SET question = 'Câu hỏi đã đổi?', embedding = NULL, "
        "embedding_hash = NULL, ingested_at = NULL WHERE entry_no = 1"
    )

    assert embed_pending(conn, embed) == 1

    assert asked == ["Câu hỏi đã đổi?"]
    assert fetch(conn, 1)[1] == question_hash("Câu hỏi đã đổi?")


@needs_db
def test_a_stale_hash_with_a_vector_is_embedded_again(conn):
    embed, asked = fake_embed_factory()
    add(conn, 1)
    embed_pending(conn, embed)
    asked.clear()
    conn.execute("UPDATE app.knowledge_documents SET question = 'Câu hỏi đã đổi?' WHERE entry_no = 1")

    assert embed_pending(conn, embed) == 1
    assert asked == ["Câu hỏi đã đổi?"]


@needs_db
def test_dry_run_counts_but_calls_nothing_and_writes_nothing(conn):
    embed, asked = fake_embed_factory()
    add(conn, 1)
    add(conn, 2)

    assert embed_pending(conn, embed, dry_run=True) == 2

    assert asked == []
    assert fetch(conn, 1)[0] is False


@needs_db
def test_limit_caps_the_number_of_rows(conn):
    embed, asked = fake_embed_factory()
    for n in (1, 2, 3):
        add(conn, n)

    assert embed_pending(conn, embed, limit=2) == 2

    assert asked == ["Câu hỏi 1?", "Câu hỏi 2?"]
    assert fetch(conn, 3)[0] is False


@needs_db
def test_a_service_failure_writes_nothing(conn):
    def broken(texts):
        raise RuntimeError("Dịch vụ embedding tắt")

    add(conn, 1)

    with pytest.raises(RuntimeError):
        embed_pending(conn, broken)

    assert fetch(conn, 1)[0] is False


@needs_db
def test_rows_are_batched_when_asking_the_service(conn):
    embed, _ = fake_embed_factory()
    batches = []

    def spying_embed(texts):
        batches.append(len(texts))
        return embed(texts)

    for n in range(1, BATCH + 5):
        add(conn, n)

    embed_pending(conn, spying_embed)

    assert batches == [BATCH, 4]
