"""Chuyển sheet 2 (kho tri thức của Đức) thành JSONL để nạp vào bảng app.knowledge_documents.

Mỗi dòng JSONL là một cặp hỏi đáp, khớp các cột của bảng:
  code          mã cố định của mẩu kiến thức (KB-001...), khóa để cập nhật khi nạp lại
  question      câu hỏi mẫu (chữ được embedding để tìm kiếm)
  content       câu trả lời chuẩn (đã bỏ phần "(Nguồn kiểm chứng: ...)" ở cuối)
  quote         nguồn kiểm chứng gốc, tách từ cuối câu trả lời
  title, topic, study_level, scope, doc_type, status, source_type, source_note, retrieved_at, review_due_at

Script đọc cột theo TÊN (không theo vị trí). Ba cột tùy chọn Đức có thể thêm vào sheet:
  "Mã kiến thức"          KB-089, KB-090... mỗi dòng một mã không trùng (không có cột này thì tự sinh KB-<STT>)
  "Phạm vi"               system_advisory | center_info (không có cột này thì dùng danh sách cũ theo STT)
  "Trạng thái kiểm chứng" draft | verified | rejected, hoặc "Đã kiểm chứng", "Chưa kiểm chứng" (mặc định draft)

Dữ liệu sai thì script báo từng dòng và KHÔNG ghi file.

Chạy từ thư mục advisor/:
  python -m scripts.build_knowledge_jsonl "D:/DoAn/USAS_Du_Lieu_Tu_Van (1).xlsx"
Ra: data/processed/knowledge_faq.jsonl
"""

import json
import re
import sys
from datetime import date, datetime, timedelta
from pathlib import Path
from typing import Any

SHEET = "2_Kho_Tri_Thuc_74_Cau"
OUT_PATH = Path(__file__).resolve().parents[1] / "data" / "processed" / "knowledge_faq.jsonl"

# Từ khóa nhận ra cột theo tên (không phân biệt hoa thường); cột bắt buộc
REQUIRED_COLUMNS = {
    "stt": "stt",
    "label": "nhãn phân loại",
    "level": "bậc áp dụng",
    "question": "câu hỏi tư vấn",
    "result": "kết quả",
    "source": "nguồn tư vấn",
}
# Cột Đức có thể thêm; thiếu thì dùng giá trị mặc định
OPTIONAL_COLUMNS = {
    "code": "mã kiến thức",
    "scope": "phạm vi",
    "status": "trạng thái kiểm chứng",
}

# Bậc trong sheet -> bậc của hệ thống (StudyLevels). Dòng áp dụng nhiều bậc thì để "general"
LEVELS = {
    "Chung": "general",
    "THCS; THPT": "secondary",
    "Đại học": "undergraduate",
    "Thạc sĩ": "master",
    "Tiến sĩ": "phd",
    "Đại học; Thạc sĩ": "general",
}
SCOPES = {"system_advisory", "center_info"}
STATUSES = {"draft", "verified", "rejected"}
# Cách Đức có thể viết trạng thái bằng tiếng Việt
STATUS_ALIASES = {"đã kiểm chứng": "verified", "chưa kiểm chứng": "draft", "loại": "rejected"}

# Khi sheet chưa có cột "Phạm vi": dòng chứa giá dịch vụ cụ thể của một trung tâm (dành cho B6)
LEGACY_CENTER_INFO_STT = {46, 62}

# Thông tin có số tiền cụ thể thì dễ hết hạn: nhắc kiểm tra lại sau chừng này ngày
REVIEW_AFTER_DAYS = 180
TIME_SENSITIVE = re.compile(r"\$|USD")

CODE_RE = re.compile(r"^KB-\d{3,}$")
# Cuối câu có thể còn dấu chấm và dấu nháy kép thừa: "... (Nguồn kiểm chứng: ...).""
_QUOTE_RE = re.compile(r"\s*\(Nguồn kiểm chứng:\s*(?P<quote>.+?)\)\.?\"?\s*$", re.DOTALL)
_SOURCE_RE = re.compile(
    r"^(?P<name>.+?)\s*\n\s*Mã:\s*(?P<code>\S+)\s*\n\s*Ngày thu:\s*(?P<date>\d{2}/\d{2}/\d{4})", re.DOTALL
)


def _norm(text: Any) -> str:
    return " ".join(str(text or "").split()).lower()


# Tìm vị trí từng cột theo tên; thiếu cột bắt buộc thì báo rõ thiếu cột nào
def find_columns(header: tuple[Any, ...]) -> dict[str, int]:
    names = [_norm(cell) for cell in header]
    columns: dict[str, int] = {}
    missing = []
    for key, keyword in {**REQUIRED_COLUMNS, **OPTIONAL_COLUMNS}.items():
        index = next((i for i, name in enumerate(names) if keyword in name), None)
        if index is not None:
            columns[key] = index
        elif key in REQUIRED_COLUMNS:
            missing.append(keyword)
    if missing:
        raise ValueError(f"Sheet thiếu cột: {', '.join(missing)}")
    return columns


# Tách câu trả lời thành (nội dung, nguồn kiểm chứng); không có phần nguồn thì nguồn là None
def split_quote(result: str) -> tuple[str, str | None]:
    text = result.strip()
    match = _QUOTE_RE.search(text)
    if not match:
        return text, None
    return text[: match.start()].rstrip(), match.group("quote").strip()


# "Edu4life\nMã: RAW-ON-003\nNgày thu: 27/09/2026" -> ("Edu4life, RAW-ON-003", date(2026, 9, 27))
def parse_source(text: str) -> tuple[str, date]:
    match = _SOURCE_RE.match(text.strip())
    if not match:
        raise ValueError(f"Không đọc được nguồn: {text!r}")
    collected = datetime.strptime(match.group("date"), "%d/%m/%Y").date()
    return f"{match.group('name').strip()}, {match.group('code')}", collected


def map_level(label: str) -> str:
    try:
        return LEVELS[label.strip()]
    except KeyError:
        raise ValueError(f"Bậc lạ trong sheet: {label!r}") from None


def map_status(value: str) -> str:
    text = _norm(value)
    if not text:
        return "draft"
    status = STATUS_ALIASES.get(text, text)
    if status not in STATUSES:
        raise ValueError(f"Trạng thái lạ: {value!r} (dùng draft, verified hoặc rejected)")
    return status


def map_scope(value: str, stt: int, has_column: bool) -> str:
    if not has_column:
        return "center_info" if stt in LEGACY_CENTER_INFO_STT else "system_advisory"
    text = _norm(value) or "system_advisory"
    if text not in SCOPES:
        raise ValueError(f"Phạm vi lạ: {value!r} (dùng system_advisory hoặc center_info)")
    return text


# Nguồn kiểm chứng của chính hệ thống ("USAS ...") không phải nguồn chính thức bên ngoài
def source_type_of(quote: str | None) -> str:
    if quote is None:
        return "internal"
    return "internal" if quote.startswith("USAS") else "official"


# Một dòng của sheet -> một bản ghi cho bảng; sai dữ liệu thì ném ValueError
def to_record(row: tuple[Any, ...], columns: dict[str, int]) -> dict[str, Any]:
    def cell(key: str) -> str:
        index = columns.get(key)
        return "" if index is None or index >= len(row) or row[index] is None else str(row[index]).strip()

    stt = int(cell("stt"))
    question = " ".join(cell("question").split())
    if not question:
        raise ValueError("thiếu câu hỏi")
    content, quote = split_quote(cell("result"))
    if not content:
        raise ValueError("thiếu câu trả lời")
    if quote is None:
        raise ValueError('thiếu nguồn kiểm chứng ở cuối câu trả lời: "(Nguồn kiểm chứng: ...)"')
    source_note, collected = parse_source(cell("source"))

    if "code" in columns:
        code = cell("code")
        if not code:
            raise ValueError('thiếu "Mã kiến thức" (cột này có trong sheet nên mọi dòng đều phải có mã)')
    else:
        code = f"KB-{stt:03d}"
    if not CODE_RE.match(code):
        raise ValueError(f"mã không đúng dạng KB-001: {code!r}")

    review_due = None
    if TIME_SENSITIVE.search(content):
        review_due = (collected + timedelta(days=REVIEW_AFTER_DAYS)).isoformat()
    return {
        "code": code,
        "stt": stt,
        "title": question[:300],
        "question": question,
        "content": content,
        "quote": quote,
        "topic": cell("label"),
        "study_level": map_level(cell("level")),
        "scope": map_scope(cell("scope"), stt, "scope" in columns),
        "doc_type": "faq",
        "status": map_status(cell("status")),
        "source_type": source_type_of(quote),
        "source_note": source_note,
        "retrieved_at": collected.isoformat(),
        "review_due_at": review_due,
    }


# Chuyển mọi dòng; trả về (các bản ghi, danh sách lỗi theo dòng). Có lỗi thì không nên ghi file
def build_records(
    header: tuple[Any, ...], rows: list[tuple[Any, ...]]
) -> tuple[list[dict[str, Any]], list[str]]:
    columns = find_columns(header)
    records: list[dict[str, Any]] = []
    errors: list[str] = []
    for row in rows:
        try:
            records.append(to_record(row, columns))
        except ValueError as exc:
            errors.append(f"STT {row[columns['stt']]}: {exc}")
    errors += _duplicates(records, "code", "mã")
    errors += _duplicates(records, "question", "câu hỏi")
    return records, errors


def _duplicates(records: list[dict[str, Any]], field: str, name: str) -> list[str]:
    seen: dict[str, int] = {}
    errors = []
    for record in records:
        value = record[field]
        if value in seen:
            errors.append(f"STT {record['stt']}: {name} trùng với STT {seen[value]}: {value!r}")
        else:
            seen[value] = record["stt"]
    return errors


def read_sheet(xlsx_path: str) -> tuple[tuple[Any, ...], list[tuple[Any, ...]]]:
    import openpyxl  # chỉ cần khi chạy script này, không cần khi test

    all_rows = list(openpyxl.load_workbook(xlsx_path, read_only=True)[SHEET].iter_rows(values_only=True))
    header_index = next(i for i, r in enumerate(all_rows) if r and _norm(r[0]) == "stt")
    rows = [r for r in all_rows[header_index + 1 :] if r and r[0]]
    return all_rows[header_index], rows


def main() -> None:
    sys.stdout.reconfigure(encoding="utf-8")
    if len(sys.argv) < 2:
        raise SystemExit("Cách dùng: python -m scripts.build_knowledge_jsonl <file xlsx>")
    header, rows = read_sheet(sys.argv[1])
    records, errors = build_records(header, rows)
    if errors:
        print(f"Có {len(errors)} lỗi, chưa ghi file:")
        for error in errors:
            print(f"  - {error}")
        raise SystemExit(1)
    OUT_PATH.parent.mkdir(parents=True, exist_ok=True)
    with OUT_PATH.open("w", encoding="utf-8", newline="\n") as f:
        for record in records:
            f.write(json.dumps(record, ensure_ascii=False) + "\n")
    centers = sum(r["scope"] == "center_info" for r in records)
    print(f"Đã ghi {len(records)} dòng ({centers} dòng center_info) -> {OUT_PATH}")


if __name__ == "__main__":
    main()
