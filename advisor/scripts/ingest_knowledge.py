"""Nạp kho kiến thức (data/processed/knowledge_faq.jsonl) vào bảng app.knowledge_documents, kèm vector của câu hỏi.

Chạy từ thư mục advisor/ (cần Postgres và Ollama đang bật, DATABASE_URL trong .env):
  python -m scripts.ingest_knowledge --dry-run   # chỉ in ra sẽ làm gì, không ghi gì
  python -m scripts.ingest_knowledge             # nạp thật
  python -m scripts.ingest_knowledge --prune     # dòng có mã nhưng không còn trong file thì đánh dấu rejected

Quy tắc (chạy lại nhiều lần an toàn):
  1. Khóa để biết "dòng đã có" là code (KB-001...).
  2. Dòng đã có mà không đổi: giữ nguyên, không đụng tới status và người kiểm chứng.
  3. Nội dung đổi (question, content, quote): cập nhật, tăng version, dòng verified quay về draft.
  4. Chỉ đổi thông tin phụ (chủ đề, nguồn, ngày...): cập nhật, giữ nguyên status.
  5. Chỉ tạo lại vector khi chưa có vector hoặc question đổi (so mã băm sha256 của question).
  6. Dòng trong file ghi verified mà trong DB còn draft thì được nâng lên verified; không bao giờ tự hạ verified xuống.
  7. Dòng không còn trong file không bị xóa; chỉ khi chạy --prune mới đánh dấu rejected.
  8. Sai dữ liệu hoặc lỗi giữa chừng thì không ghi gì (một giao dịch duy nhất).
"""

import argparse
import asyncio
import hashlib
import json
import sys
from collections.abc import Callable
from dataclasses import dataclass
from datetime import UTC, date, datetime
from pathlib import Path
from typing import Any

from pgvector import Vector

IN_PATH = Path(__file__).resolve().parents[1] / "data" / "processed" / "knowledge_faq.jsonl"
BATCH = 16  # số câu hỏi gửi Ollama mỗi lần

REQUIRED = (
    "code",
    "question",
    "content",
    "study_level",
    "scope",
    "status",
    "source_type",
    "doc_type",
    "title",
)
# Thay đổi ở các trường này nghĩa là câu trả lời đổi: phải kiểm chứng lại
CONTENT_FIELDS = ("question", "content", "quote")
# Các trường phụ: đổi thì cập nhật nhưng không ảnh hưởng trạng thái kiểm chứng
META_FIELDS = (
    "title", "topic", "study_level", "scope", "doc_type", "source_type", "source_note", "source_url",
    "retrieved_at", "review_due_at",
)  # fmt: skip
STATUSES = {"draft", "verified", "rejected"}

EXISTING_SQL = """
SELECT code, status, question, content, quote, title, topic, study_level, scope, doc_type, source_type,
       source_note, source_url, retrieved_at, review_due_at, embedding_hash, (embedding IS NOT NULL) AS has_vector
FROM app.knowledge_documents WHERE code <> ''
"""

INSERT_SQL = """
INSERT INTO app.knowledge_documents
    (id, code, title, question, content, quote, topic, study_level, scope, doc_type, status, source_type,
     source_note, source_url, retrieved_at, review_due_at, version, embedding_hash, embedding, ingested_at,
     verified_at, created_at)
VALUES (gen_random_uuid(), %(code)s, %(title)s, %(question)s, %(content)s, %(quote)s, %(topic)s, %(study_level)s,
        %(scope)s, %(doc_type)s, %(status)s, %(source_type)s, %(source_note)s, %(source_url)s, %(retrieved_at)s,
        %(review_due_at)s, 1, %(embedding_hash)s, %(embedding)s, %(ingested_at)s, %(verified_at)s, now())
"""


@dataclass
class Summary:
    inserted: int = 0
    content_changed: int = 0  # nội dung đổi, tăng version
    reset_to_draft: int = 0  # trong số đó, dòng verified quay về draft
    meta_changed: int = 0
    promoted: int = 0  # draft nâng lên verified
    unchanged: int = 0
    embedded: int = 0  # số vector đã tạo
    pruned: int = 0  # đánh dấu rejected vì không còn trong file

    def report(self) -> str:
        return (
            f"thêm mới {self.inserted}, nội dung đổi {self.content_changed} (về draft {self.reset_to_draft}), "
            f"thông tin phụ đổi {self.meta_changed}, nâng lên verified {self.promoted}, không đổi {self.unchanged}, "
            f"tạo vector {self.embedded}, đánh dấu rejected {self.pruned}"
        )


def question_hash(question: str) -> str:
    return hashlib.sha256(question.strip().encode("utf-8")).hexdigest()


# Đọc và kiểm tra file JSONL; sai thì ném ValueError kèm danh sách lỗi theo dòng
def load_records(path: Path = IN_PATH) -> list[dict[str, Any]]:
    records: list[dict[str, Any]] = []
    errors: list[str] = []
    seen: dict[str, int] = {}
    with path.open(encoding="utf-8") as f:
        for number, line in enumerate(f, start=1):
            if not line.strip():
                continue
            try:
                record = json.loads(line)
            except json.JSONDecodeError as exc:
                errors.append(f"dòng {number}: không phải JSON ({exc})")
                continue
            missing = [k for k in REQUIRED if not str(record.get(k) or "").strip()]
            if missing:
                errors.append(f"dòng {number}: thiếu {', '.join(missing)}")
                continue
            if record["status"] not in STATUSES:
                errors.append(f"dòng {number}: status lạ {record['status']!r}")
            if record["code"] in seen:
                errors.append(f"dòng {number}: mã {record['code']} trùng với dòng {seen[record['code']]}")
            seen.setdefault(record["code"], number)
            records.append(record)
    if errors:
        raise ValueError("\n".join(errors))
    return records


def _differs(record: dict[str, Any], row: dict[str, Any], name: str) -> bool:
    new, old = record.get(name), row.get(name)
    if name == "retrieved_at":  # trong DB là kiểu date, trong file là chữ ISO
        new, old = new or None, (old.isoformat() if isinstance(old, date) else old)
    elif name == "review_due_at":  # trong DB là thời điểm, so theo ngày
        new, old = new or None, (old.astimezone(UTC).date().isoformat() if isinstance(old, datetime) else old)
    else:
        new, old = new or None, old or None
    return new != old


# Quyết định làm gì với từng dòng (hàm thuần, không đụng DB): trả về (hành động, cần tạo vector)
def decide(record: dict[str, Any], row: dict[str, Any] | None) -> tuple[str, bool]:
    if row is None:
        return "insert", True
    needs_vector = not row["has_vector"] or row["embedding_hash"] != question_hash(record["question"])
    if any(_differs(record, row, name) for name in CONTENT_FIELDS):
        return "content", needs_vector
    if any(_differs(record, row, name) for name in META_FIELDS):
        action = "meta"
    else:
        action = "unchanged"
    if record["status"] == "verified" and row["status"] == "draft":
        action = "promote" if action == "unchanged" else "meta+promote"
    return action, needs_vector


def _row_values(record: dict[str, Any]) -> dict[str, Any]:
    review_due = record.get("review_due_at")
    return {
        "code": record["code"],
        "title": record["title"][:300],
        "question": record["question"],
        "content": record["content"],
        "quote": record.get("quote"),
        "topic": record.get("topic") or None,
        "study_level": record["study_level"],
        "scope": record["scope"],
        "doc_type": record["doc_type"],
        "source_type": record["source_type"],
        "source_note": record.get("source_note") or None,
        "source_url": record.get("source_url") or None,
        "retrieved_at": date.fromisoformat(record["retrieved_at"]) if record.get("retrieved_at") else None,
        "review_due_at": datetime.fromisoformat(review_due).replace(tzinfo=UTC) if review_due else None,
    }


# Nạp các dòng vào DB trong một giao dịch. embed: hàm biến danh sách câu hỏi thành danh sách vector
def ingest(
    conn: Any,
    records: list[dict[str, Any]],
    embed: Callable[[list[str]], list[list[float]]],
    *,
    prune: bool = False,
    dry_run: bool = False,
) -> Summary:
    summary = Summary()
    existing = {r["code"]: r for r in _fetch_existing(conn)}

    plans = [(record, *decide(record, existing.get(record["code"]))) for record in records]
    to_embed = [record for record, _, needs in plans if needs]
    vectors = {}
    if to_embed and not dry_run:  # tạo hết vector trước khi ghi, lỗi Ollama thì chưa ghi gì
        questions = [r["question"] for r in to_embed]
        result: list[list[float]] = []
        for start in range(0, len(questions), BATCH):
            result += embed(questions[start : start + BATCH])
        vectors = {r["code"]: v for r, v in zip(to_embed, result, strict=True)}
    summary.embedded = len(to_embed)

    now = datetime.now(UTC)
    with conn.transaction():
        for record, action, needs in plans:
            if dry_run:
                _count(summary, action, existing.get(record["code"]))
                continue
            _apply(
                conn,
                summary,
                record,
                action,
                existing.get(record["code"]),
                vectors.get(record["code"]),
                needs,
                now,
            )
        if prune:
            summary.pruned = _prune(conn, {r["code"] for r in records}, existing, dry_run)
    return summary


def _fetch_existing(conn: Any) -> list[dict[str, Any]]:
    with conn.cursor() as cur:
        cur.execute(EXISTING_SQL)
        names = [c.name for c in cur.description]
        return [dict(zip(names, row, strict=True)) for row in cur.fetchall()]


def _count(summary: Summary, action: str, row: dict[str, Any] | None) -> None:
    if action == "insert":
        summary.inserted += 1
    elif action == "content":
        summary.content_changed += 1
        summary.reset_to_draft += 1 if row and row["status"] == "verified" else 0
    elif action == "unchanged":
        summary.unchanged += 1
    else:
        summary.meta_changed += 0 if action == "promote" else 1
        summary.promoted += 1 if "promote" in action else 0


def _apply(
    conn: Any,
    summary: Summary,
    record: dict[str, Any],
    action: str,
    row: dict[str, Any] | None,
    vector: list[float] | None,
    needs_vector: bool,
    now: datetime,
) -> None:
    values = _row_values(record)
    if action == "insert":
        verified = record["status"] == "verified"
        conn.execute(
            INSERT_SQL,
            {
                **values,
                "status": record["status"],
                "embedding_hash": question_hash(record["question"]),
                "embedding": Vector(vector) if vector is not None else None,
                "ingested_at": now,
                "verified_at": now if verified else None,
            },
        )
    else:
        sets, params = _update_parts(record, values, action, row, vector, needs_vector, now)
        if sets:
            conn.execute(
                f"UPDATE app.knowledge_documents SET {', '.join(sets)}, updated_at = now() WHERE code = %(code)s",
                {**params, "code": record["code"]},
            )
    _count(summary, action, row)


def _update_parts(
    record: dict[str, Any],
    values: dict[str, Any],
    action: str,
    row: dict[str, Any] | None,
    vector: list[float] | None,
    needs_vector: bool,
    now: datetime,
) -> tuple[list[str], dict[str, Any]]:
    if action == "content":
        fields = [k for k in values if k != "code"]
    elif action in ("meta", "meta+promote"):  # chỉ thông tin phụ
        fields = list(META_FIELDS)
    else:  # unchanged, promote: không đụng tới trường nào
        fields = []
    sets = [f"{name} = %({name})s" for name in fields]
    params = {name: values[name] for name in fields}
    if action == "content":
        sets.append("version = version + 1")
        if row and row["status"] == "verified":  # nội dung đã đổi thì phải kiểm chứng lại
            sets += ["status = 'draft'", "verified_by_user_id = NULL", "verified_at = NULL"]
    if "promote" in action:
        sets += ["status = 'verified'", "verified_at = %(verified_at)s"]
        params["verified_at"] = now
    if needs_vector and vector is not None:
        sets += [
            "embedding = %(embedding)s",
            "embedding_hash = %(embedding_hash)s",
            "ingested_at = %(ingested_at)s",
        ]
        params |= {
            "embedding": Vector(vector),
            "embedding_hash": question_hash(record["question"]),
            "ingested_at": now,
        }
    return sets, params


def _prune(conn: Any, codes_in_file: set[str], existing: dict[str, dict[str, Any]], dry_run: bool) -> int:
    gone = [c for c, r in existing.items() if c not in codes_in_file and r["status"] != "rejected"]
    if gone and not dry_run:
        conn.execute(
            "UPDATE app.knowledge_documents SET status = 'rejected', updated_at = now() WHERE code = ANY(%(codes)s)",
            {"codes": gone},
        )
    return len(gone)


# Gọi Ollama (bge-m3) thật để tạo vector: bọc hàm async của OllamaClient thành hàm thường
def make_ollama_embedder() -> Callable[[list[str]], list[list[float]]]:
    import httpx

    from app.core.config import get_settings
    from app.services.ollama import OllamaClient

    settings = get_settings()

    async def run(texts: list[str]) -> list[list[float]]:
        async with httpx.AsyncClient(
            base_url=settings.ollama_base_url, timeout=settings.llm_timeout_seconds
        ) as http:
            client = OllamaClient(http, settings.llm_model, embedding_model=settings.embedding_model)
            return await client.embed(texts)

    return lambda texts: asyncio.run(run(texts))


def main() -> None:
    sys.stdout.reconfigure(encoding="utf-8")
    parser = argparse.ArgumentParser(description="Nạp kho kiến thức vào pgvector")
    parser.add_argument(
        "--file", type=Path, default=IN_PATH, help="file JSONL (mặc định knowledge_faq.jsonl)"
    )
    parser.add_argument("--dry-run", action="store_true", help="chỉ in ra sẽ làm gì, không ghi gì")
    parser.add_argument(
        "--prune", action="store_true", help="dòng không còn trong file thì đánh dấu rejected"
    )
    args = parser.parse_args()

    import psycopg
    from pgvector.psycopg import register_vector

    from app.core.config import get_settings

    settings = get_settings()
    if not settings.database_url:
        raise SystemExit("Chưa cấu hình DATABASE_URL trong advisor/.env")
    try:
        records = load_records(args.file)
    except ValueError as exc:
        print("File dữ liệu có lỗi, chưa nạp gì:")
        print(exc)
        raise SystemExit(1) from exc

    with psycopg.connect(settings.database_url) as conn:
        register_vector(conn)
        summary = ingest(conn, records, make_ollama_embedder(), prune=args.prune, dry_run=args.dry_run)
    print(("[dry-run, chưa ghi gì] " if args.dry_run else "") + summary.report())


if __name__ == "__main__":
    main()
