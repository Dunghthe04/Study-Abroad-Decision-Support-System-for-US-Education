"""Đổi file Excel dữ liệu của SLM thành JSONL để huấn luyện và đo. Excel là nguồn, JSONL sinh ra từ Excel, không sửa tay.

Mỗi loại một sheet (collect_profile, profile_review, scenario, knowledge, center_referral, out_of_scope): sheet là loại chính của dòng,
dòng nhiều ý đặt ở sheet của một trong các ý. Mỗi dòng: đầu vào của SLM (question, profile, pending_question, context) và đầu ra
mong muốn là cột intents, một danh sách ý [{"type", "span", "entities"}] theo thứ tự học sinh nói. Dòng sai chuẩn thì báo sheet và số dòng.

Tên sheet, cột, khóa và mã trong Excel là tiếng Anh, đúng từng chữ với JSONL, prompt (app/skills/slm_intents.md) và code
(app/schemas/slm.py): không đổi tên ở giữa. Hợp đồng và luật kiểm nằm ở app/schemas/slm.py.

Cột context (tùy chọn, ở mọi sheet): điều hệ thống đang nói tới, đưa vào đầu vào của SLM giống lúc chạy thật.
Dòng không có ngữ cảnh thì bỏ trống.

Chạy trong thư mục advisor:
  python -m scripts.build_slm_jsonl
  python -m scripts.build_slm_jsonl --excel data/slm/du_lieu_slm.xlsx --out data/slm/du_lieu_slm.jsonl
"""

import argparse
import json
import re
import sys
from collections import Counter
from pathlib import Path
from typing import Any

from openpyxl import load_workbook

from app.schemas.slm import INTENTS as TYPES
from app.schemas.slm import MAX_RAW_Y, PROFILE_KEYS, SlmTinResult, SlmY
from app.services.chat_flow import QUESTIONS

DATA_DIR = Path(__file__).resolve().parent.parent / "data" / "slm"
DEFAULT_EXCEL = DATA_DIR / "du_lieu_slm.xlsx"
DEFAULT_OUT = DATA_DIR / "du_lieu_slm.jsonl"
REVIEW_COLUMN = "review"  # tùy chọn, ở mọi sheet: đánh dấu dòng mới chờ duyệt (v9, v10…); người duyệt ghi ok / sửa / xóa. Không đi vào dữ liệu
CONTEXT_COLUMN = "context"  # tùy chọn, ở mọi sheet: điều hệ thống đang nói tới (JSON)
# Cột bắt buộc, giống nhau ở mọi sheet. Loại chính lấy từ tên sheet, id sinh theo sheet và thứ tự dòng
COLUMNS = ("question", "profile", "pending_question", "intents", "split")
SPLITS = ("train", "val", "test")
_WORD = re.compile(r"[^\W_]+(?:[-.'/][^\W_]+)*")


def as_text(value: Any) -> str:
    return "" if value is None else str(value).strip()


def as_json(value: Any, where: str, column: str) -> Any:
    text = as_text(value)
    if not text:
        return {}
    try:
        return json.loads(text)
    except ValueError as ex:
        raise ValueError(f"{where}: cột {column} không phải JSON hợp lệ") from ex


# Hồ sơ thành khóa → chữ. Giá trị là danh sách (hoạt động, giải thưởng) thì nối bằng "; " cho đọc như học sinh nói
def profile_text(profile: dict[str, Any]) -> dict[str, str]:
    return {
        key: "; ".join(map(str, value)) if isinstance(value, list) else str(value)
        for key, value in profile.items()
    }


# Các từ của một đoạn chữ, viết thường ("I-20", "TP.HCM", "8.5" giữ nguyên một từ)
def words(text: str) -> set[str]:
    return set(_WORD.findall(text.lower()))


# Kiểm cột intents: từng ý hợp lệ, span chỉ dùng chữ có trong câu hỏi, giá trị hồ sơ nguyên văn, và không ý nào bị
# gộp hay bỏ khi chuẩn hóa (dữ liệu phải ghi sẵn đúng chuẩn, nếu không model học sai)
def check_intents(
    intent: str, question: str, pending: str, items: Any, where: str, context: dict[str, Any] | None = None
) -> list[dict[str, Any]]:
    if not isinstance(items, list) or not items:
        raise ValueError(f"{where}: intents phải là danh sách ý, ít nhất một ý")
    if len(items) > MAX_RAW_Y:
        raise ValueError(f"{where}: intents có {len(items)} ý, nhiều nhất {MAX_RAW_Y}")
    try:
        ys = [SlmY.model_validate(item) for item in items]
        result = SlmTinResult.from_raw({"intents": items})
    except ValueError as ex:
        raise ValueError(f"{where}: {ex}") from ex
    if len(result.intents) != len(ys):
        raise ValueError(
            f"{where}: intents chưa đúng chuẩn, có ý bị gộp hoặc bỏ (ý cùng loại hồ sơ phải gộp, bỏ ý chào)"
        )
    if [y.type for y in ys] != [y.type for y in result.intents]:
        raise ValueError(
            f"{where}: collect_profile phải đứng đầu intents, các ý còn lại theo thứ tự học sinh nói"
        )
    if intent not in {y.type for y in ys}:
        raise ValueError(f"{where}: sheet {intent} nhưng intents không có ý nào loại {intent}")
    spoken = words(question)
    for y in ys:
        extra = words(y.span) - spoken
        if extra:
            raise ValueError(f"{where}: span có chữ không có trong câu hỏi: {sorted(extra)}")
        if y.type != "collect_profile" and set(y.entities) & set(PROFILE_KEYS):
            raise ValueError(f"{where}: giá trị hồ sơ chỉ ghi ở ý collect_profile, không ghi vào ý {y.type}")
        for key, value in y.entities.items():
            if (key in PROFILE_KEYS or key == "city") and value not in question:
                # Câu nối tiếp ("thế còn luyện phỏng vấn") lấy thành phố từ ngữ cảnh: phải đúng thành phố trong ngữ cảnh
                from_context = (
                    key == "city"
                    and y.entities.get("follow_up") is True
                    and value == (context or {}).get("city")
                )
                if not from_context:
                    raise ValueError(f"{where}: {key} '{value}' phải là đoạn chữ có trong question")
        # collect_profile rỗng chỉ hợp lệ khi đang có câu chờ (tin mơ hồ, hỏi lại). Không có câu chờ thì phải có giá trị hồ sơ,
        # không thì là out_of_scope/ambiguous
        if y.type == "collect_profile" and not pending and not y.entities:
            raise ValueError(f"{where}: collect_profile không có câu chờ thì entities phải có giá trị hồ sơ")
    return items


# Một dòng trong sheet thành một bản ghi. Sai chuẩn thì ném ValueError có tên sheet và số dòng
def to_record(intent: str, raw: dict[str, Any], where: str, record_id: str) -> dict[str, Any]:
    text = {key: as_text(raw.get(key)) for key in COLUMNS}  # context đọc riêng bên dưới
    if not text["question"]:
        raise ValueError(f"{where}: thiếu question")
    if text["split"] not in SPLITS:
        raise ValueError(f"{where}: split phải là một trong {SPLITS}, đang là '{text['split']}'")
    pending = text["pending_question"]
    if pending and pending not in QUESTIONS.values():
        raise ValueError(f"{where}: pending_question không khớp câu nào trong chat_flow.QUESTIONS")
    profile = as_json(raw.get("profile"), where, "profile")
    if not isinstance(profile, dict):
        raise ValueError(f"{where}: cột profile phải là một object JSON")
    if set(profile) - set(PROFILE_KEYS):
        raise ValueError(f"{where}: profile có khóa lạ: {sorted(set(profile) - set(PROFILE_KEYS))}")
    context = as_json(raw.get(CONTEXT_COLUMN), where, CONTEXT_COLUMN)
    if context and not isinstance(context, dict):
        raise ValueError(f"{where}: cột context phải là một object JSON")
    intents = check_intents(
        intent,
        text["question"],
        pending,
        as_json(raw.get("intents"), where, "intents") or None,
        where,
        context,
    )
    record = {
        "id": record_id,
        "type": intent,
        "split": text["split"],
        "input": {"question": text["question"], "profile": profile or None, "pending_question": pending},
        "output": {"intents": intents},
    }
    # Ngữ cảnh là đầu vào của SLM (giống lúc chạy: slm_input); chỉ ghi khi có, nên dòng không ngữ cảnh giữ nguyên khuôn cũ
    if context:
        record["input"]["context"] = context
    return record


def read_records(path: Path) -> list[dict[str, Any]]:
    """Đọc Excel thành danh sách bản ghi (đúng dạng một dòng của JSONL). Dòng sai chuẩn thì ném ValueError có tên sheet và số dòng."""
    workbook = load_workbook(path, read_only=True, data_only=True)
    records: list[dict[str, Any]] = []
    for sheet in workbook.worksheets:
        if sheet.title in ("README", "summary"):
            continue  # sheet giải thích cho người đọc, không có dữ liệu
        if sheet.title not in TYPES:
            raise ValueError(f"Sheet '{sheet.title}' không phải một loại trong {TYPES} (mỗi loại một sheet)")
        rows = sheet.iter_rows(values_only=True)
        full_header = tuple(as_text(cell) for cell in next(rows, ()) if as_text(cell))
        # Cột context và review tùy chọn, không tính vào bộ cột bắt buộc
        header = tuple(name for name in full_header if name not in (CONTEXT_COLUMN, REVIEW_COLUMN))
        if header != COLUMNS:
            raise ValueError(f"Sheet '{sheet.title}' phải có đúng các cột {COLUMNS}, đang là {header}")
        count = 0
        for number, values in enumerate(rows, start=2):
            raw = dict(zip(full_header, values, strict=False))
            if not any(as_text(v) for v in raw.values()):
                continue  # dòng trống
            count += 1
            where = f"{sheet.title}, dòng {number}"
            records.append(to_record(sheet.title, raw, where, f"{sheet.title}_{count:03d}"))
    return records


def write_jsonl(records: list[dict[str, Any]], out: Path) -> None:
    out.parent.mkdir(parents=True, exist_ok=True)
    with open(out, "w", encoding="utf-8", newline="\n") as f:
        for record in records:
            f.write(json.dumps(record, ensure_ascii=False) + "\n")


def main() -> None:
    sys.stdout.reconfigure(encoding="utf-8")
    parser = argparse.ArgumentParser(
        description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter
    )
    parser.add_argument("--excel", type=Path, default=DEFAULT_EXCEL)
    parser.add_argument("--out", type=Path, default=DEFAULT_OUT)
    args = parser.parse_args()
    records = read_records(args.excel)
    write_jsonl(records, args.out)
    print(f"Đã ghi {len(records)} dòng vào {args.out}")
    for intent in TYPES:
        rows = [r for r in records if r["type"] == intent]
        by_split = Counter(r["split"] for r in rows)
        print(f"  {intent:16} {len(rows):4}  " + "  ".join(f"{s}={by_split[s]}" for s in SPLITS))
    by_count = Counter(len(r["output"]["intents"]) for r in records)
    print("  số ý mỗi dòng: " + "  ".join(f"{n} ý={by_count[n]}" for n in sorted(by_count)))


if __name__ == "__main__":
    main()
