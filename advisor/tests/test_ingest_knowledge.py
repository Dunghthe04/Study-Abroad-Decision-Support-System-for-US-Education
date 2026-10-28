"""Việc B3: script nạp kho kiến thức. Phần quyết định là hàm thuần; phần ghi DB thử trên Postgres local rồi hoàn tác."""

import hashlib
import json
from datetime import UTC, datetime

import psycopg
import pytest
from pgvector.psycopg import register_vector

from scripts.ingest_knowledge import (
    IN_PATH,
    decide,
    ingest,
    load_records,
    question_hash,
)

LOCAL_DB = "postgresql://studyabroad:studyabroad@localhost:5432/studyabroad"


def make_record(n: int, **changes) -> dict:
    record = {
        "code": f"KB-9{n:03d}",  # dải 9xxx để không đụng 88 dòng thật
        "title": f"Câu hỏi {n}?",
        "question": f"Câu hỏi {n}?",
        "content": f"Câu trả lời {n}.",
        "quote": "EducationUSA",
        "topic": "LABEL_X",
        "study_level": "general",
        "scope": "system_advisory",
        "doc_type": "faq",
        "status": "draft",
        "source_type": "official",
        "source_note": "Edu4life, RAW-ON-003",
        "retrieved_at": "2026-09-27",
        "review_due_at": None,
    }
    return record | changes


def fake_embed_factory():
    """Ollama giả: vector 1024 số tạo từ chữ, và ghi lại những câu đã được yêu cầu."""
    asked: list[str] = []

    def embed(texts: list[str]) -> list[list[float]]:
        asked.extend(texts)
        return [[b / 255 for b in hashlib.sha256(t.encode()).digest()] * 32 for t in texts]

    return embed, asked


def db_row(code: str, **want) -> dict:
    return want  # chỗ này chỉ để đọc cho rõ ở bên dưới


def existing_row(**changes) -> dict:
    row = {
        "code": "KB-9001", "status": "draft", "question": "Câu hỏi 1?", "content": "Câu trả lời 1.",
        "quote": "EducationUSA", "title": "Câu hỏi 1?", "topic": "LABEL_X", "study_level": "general",
        "scope": "system_advisory", "doc_type": "faq", "source_type": "official",
        "source_note": "Edu4life, RAW-ON-003", "source_url": None,
        "retrieved_at": datetime(2026, 9, 27).date(), "review_due_at": None,
        "embedding_hash": question_hash("Câu hỏi 1?"), "has_vector": True,
    }  # fmt: skip
    return row | changes


# --- hàm thuần: quyết định làm gì với từng dòng ---


def test_a_new_code_is_inserted_with_a_vector():
    assert decide(make_record(1), None) == ("insert", True)


def test_an_identical_row_is_left_alone_and_needs_no_vector():
    assert decide(make_record(1), existing_row()) == ("unchanged", False)


def test_changed_answer_is_a_content_change_but_does_not_need_a_new_vector():
    action, needs_vector = decide(make_record(1, content="Câu trả lời mới."), existing_row())

    assert action == "content"
    assert needs_vector is False  # vector chỉ phụ thuộc vào câu hỏi


def test_changed_question_needs_a_new_vector():
    assert decide(make_record(1, question="Câu hỏi khác?"), existing_row()) == ("content", True)


def test_changed_source_only_is_a_meta_change():
    assert decide(make_record(1, source_note="Nguồn khác"), existing_row()) == ("meta", False)


def test_missing_vector_is_filled_even_when_nothing_else_changed():
    assert decide(make_record(1), existing_row(has_vector=False)) == ("unchanged", True)
    assert decide(make_record(1), existing_row(embedding_hash=None)) == ("unchanged", True)


def test_draft_in_db_and_verified_in_file_is_promoted_but_never_the_other_way():
    assert decide(make_record(1, status="verified"), existing_row()) == ("promote", False)
    assert decide(make_record(1, status="draft"), existing_row(status="verified")) == ("unchanged", False)


def test_review_date_is_compared_by_day_in_utc():
    row = existing_row(review_due_at=datetime(2027, 3, 26, tzinfo=UTC))

    assert decide(make_record(1, review_due_at="2027-03-26"), row) == ("unchanged", False)
    assert decide(make_record(1, review_due_at="2027-03-27"), row) == ("meta", False)


def test_question_hash_ignores_surrounding_spaces_and_is_64_chars():
    assert question_hash("  Câu hỏi?  ") == question_hash("Câu hỏi?")
    assert len(question_hash("x")) == 64


# --- đọc file JSONL ---


def write_jsonl(path, records) -> None:
    path.write_text("\n".join(json.dumps(r, ensure_ascii=False) for r in records) + "\n", encoding="utf-8")


def test_the_real_data_file_loads_cleanly():
    records = load_records(IN_PATH)

    assert len(records) == 88
    assert records[0]["code"] == "KB-001"


def test_load_records_reports_every_bad_line_and_duplicate_codes(tmp_path):
    path = tmp_path / "bad.jsonl"
    good = make_record(1)
    bad = {k: v for k, v in make_record(2).items() if k != "content"}
    path.write_text(
        "\n".join([json.dumps(good), json.dumps(bad), "không phải json", json.dumps(good)]) + "\n",
        encoding="utf-8",
    )

    with pytest.raises(ValueError) as excinfo:
        load_records(path)

    message = str(excinfo.value)
    assert "dòng 2: thiếu content" in message
    assert "dòng 3: không phải JSON" in message
    assert "dòng 4: mã KB-9001 trùng với dòng 1" in message


def test_load_records_rejects_an_unknown_status(tmp_path):
    path = tmp_path / "status.jsonl"
    write_jsonl(path, [make_record(1, status="đang xem")])

    with pytest.raises(ValueError, match="status lạ"):
        load_records(path)


# --- ghi vào DB thật (Postgres local), mọi thứ hoàn tác ở cuối ---


def db_ready() -> bool:
    try:
        with psycopg.connect(LOCAL_DB, connect_timeout=2) as conn:
            return (
                conn.execute(
                    "SELECT count(*) FROM information_schema.columns "
                    "WHERE table_schema='app' AND table_name='knowledge_documents' AND column_name='embedding_hash'"
                ).fetchone()[0]
                == 1
            )
    except psycopg.Error:
        return False


needs_db = pytest.mark.skipif(
    not db_ready(), reason="Không có Postgres local với bảng knowledge_documents đã migrate"
)


@pytest.fixture
def conn():
    connection = psycopg.connect(LOCAL_DB)
    register_vector(connection)
    yield connection
    connection.rollback()  # không để lại gì trong DB thật
    connection.close()


def fetch(conn, code: str) -> dict:
    cur = conn.execute(
        "SELECT status, version, topic, source_note, embedding_hash, (embedding IS NOT NULL), verified_at, "
        "verified_by_user_id, ingested_at, content, question, study_level, doc_type, title "
        "FROM app.knowledge_documents WHERE code = %s",
        (code,),
    )
    row = cur.fetchone()
    names = ["status", "version", "topic", "source_note", "hash", "has_vector", "verified_at", "verified_by",
             "ingested_at", "content", "question", "study_level", "doc_type", "title"]  # fmt: skip
    return dict(zip(names, row, strict=True)) if row else {}


@needs_db
def test_first_run_inserts_rows_with_vectors_and_hashes(conn):
    embed, asked = fake_embed_factory()
    records = [make_record(1), make_record(2, status="verified")]

    summary = ingest(conn, records, embed)

    assert summary.inserted == 2 and summary.embedded == 2
    assert asked == ["Câu hỏi 1?", "Câu hỏi 2?"]
    first, second = fetch(conn, "KB-9001"), fetch(conn, "KB-9002")
    assert first["status"] == "draft" and first["version"] == 1 and first["has_vector"]
    assert first["hash"] == question_hash("Câu hỏi 1?") and first["ingested_at"] is not None
    assert first["verified_at"] is None
    assert second["status"] == "verified" and second["verified_at"] is not None


@needs_db
def test_running_again_changes_nothing_and_does_not_call_ollama(conn):
    embed, asked = fake_embed_factory()
    ingest(conn, [make_record(1), make_record(2)], embed)
    asked.clear()
    before = fetch(conn, "KB-9001")

    summary = ingest(conn, [make_record(1), make_record(2)], embed)

    assert summary.unchanged == 2 and summary.embedded == 0 and asked == []
    assert fetch(conn, "KB-9001") == before


@needs_db
def test_changing_the_answer_bumps_the_version_and_sends_a_verified_row_back_to_draft(conn):
    embed, asked = fake_embed_factory()
    ingest(conn, [make_record(1, status="verified")], embed)
    asked.clear()

    summary = ingest(conn, [make_record(1, status="verified", content="Câu trả lời đã sửa.")], embed)

    row = fetch(conn, "KB-9001")
    assert summary.content_changed == 1 and summary.reset_to_draft == 1
    assert row["content"] == "Câu trả lời đã sửa." and row["version"] == 2
    assert row["status"] == "draft" and row["verified_at"] is None and row["verified_by"] is None
    assert asked == []  # chỉ sửa câu trả lời nên không tạo lại vector


@needs_db
def test_changing_the_question_creates_a_new_vector_and_hash(conn):
    embed, asked = fake_embed_factory()
    ingest(conn, [make_record(1)], embed)
    old_hash = fetch(conn, "KB-9001")["hash"]
    asked.clear()

    ingest(conn, [make_record(1, question="Câu hỏi đã đổi?")], embed)

    row = fetch(conn, "KB-9001")
    assert asked == ["Câu hỏi đã đổi?"]
    assert row["hash"] == question_hash("Câu hỏi đã đổi?") != old_hash and row["version"] == 2


@needs_db
def test_changing_only_the_source_keeps_the_verified_status_and_version(conn):
    embed, asked = fake_embed_factory()
    ingest(conn, [make_record(1, status="verified")], embed)
    asked.clear()

    summary = ingest(
        conn, [make_record(1, status="verified", topic="LABEL_Y", source_note="Nguồn mới")], embed
    )

    row = fetch(conn, "KB-9001")
    assert summary.meta_changed == 1 and asked == []
    assert (row["topic"], row["source_note"]) == ("LABEL_Y", "Nguồn mới")
    assert row["status"] == "verified" and row["version"] == 1


@needs_db
def test_a_draft_is_promoted_by_the_file_but_a_verified_row_is_never_demoted(conn):
    embed, _ = fake_embed_factory()
    ingest(conn, [make_record(1), make_record(2, status="verified")], embed)

    summary = ingest(conn, [make_record(1, status="verified"), make_record(2, status="draft")], embed)

    assert summary.promoted == 1
    one, two = fetch(conn, "KB-9001"), fetch(conn, "KB-9002")
    assert one["status"] == "verified" and one["verified_at"] is not None
    assert two["status"] == "verified"  # file ghi draft nhưng DB đã verified thì giữ nguyên


@needs_db
def test_a_row_missing_its_vector_gets_one_on_the_next_run(conn):
    embed, asked = fake_embed_factory()
    ingest(conn, [make_record(1)], embed)
    conn.execute("UPDATE app.knowledge_documents SET embedding = NULL WHERE code = 'KB-9001'")
    asked.clear()

    summary = ingest(conn, [make_record(1)], embed)

    assert summary.embedded == 1 and asked == ["Câu hỏi 1?"]
    assert fetch(conn, "KB-9001")["has_vector"]


@needs_db
def test_rows_missing_from_the_file_are_only_rejected_with_prune(conn):
    embed, _ = fake_embed_factory()
    ingest(conn, [make_record(1), make_record(2)], embed)

    ingest(conn, [make_record(1)], embed)  # không có --prune
    assert fetch(conn, "KB-9002")["status"] == "draft"

    summary = ingest(conn, [make_record(1)], embed, prune=True)

    assert summary.pruned >= 1  # có thể nhiều hơn một vì DB thật có thể đã có 88 dòng, mọi thứ sẽ hoàn tác
    assert fetch(conn, "KB-9002")["status"] == "rejected"
    assert fetch(conn, "KB-9001")["status"] == "draft"


@needs_db
def test_dry_run_writes_nothing(conn):
    embed, asked = fake_embed_factory()

    summary = ingest(conn, [make_record(1), make_record(2)], embed, dry_run=True)

    assert summary.inserted == 2 and summary.embedded == 2
    assert asked == []  # dry-run không gọi Ollama
    assert fetch(conn, "KB-9001") == {} and fetch(conn, "KB-9002") == {}


@needs_db
def test_an_ollama_failure_writes_nothing(conn):
    def broken(texts):
        raise RuntimeError("Ollama tắt")

    with pytest.raises(RuntimeError):
        ingest(conn, [make_record(1), make_record(2)], broken)

    assert fetch(conn, "KB-9001") == {} and fetch(conn, "KB-9002") == {}


@needs_db
def test_rows_are_batched_when_asking_ollama(conn):
    embed, asked = fake_embed_factory()
    batches = []

    def spying_embed(texts):
        batches.append(len(texts))
        return embed(texts)

    ingest(conn, [make_record(n) for n in range(1, 21)], spying_embed)

    assert batches == [16, 4]  # 20 câu hỏi gửi thành hai lần, mỗi lần tối đa 16
