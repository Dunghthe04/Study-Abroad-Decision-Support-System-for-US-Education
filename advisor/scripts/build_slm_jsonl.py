"""Đổi file Excel dữ liệu của SLM thành JSONL để huấn luyện và đo. Excel là nguồn, JSONL sinh ra từ Excel, không sửa tay.

Mỗi loại một sheet (mac_dinh, ho_so, kich_ban, kien_thuc, trung_tam, ngoai_pham_vi): sheet là loại chính của dòng,
dòng nhiều ý đặt ở sheet của một trong các ý. Mỗi dòng: đầu vào của SLM (câu hỏi, hồ sơ, câu hệ thống đang hỏi) và đầu ra
mong muốn là cột cac_y, một danh sách ý [{"type", "doan", "thuc_the"}] theo thứ tự học sinh nói (skill md với hồ sơ và kịch bản
khi có hồ sơ). Dòng sai chuẩn thì báo sheet và số dòng.

File Excel ghi bằng tiếng Anh cho dễ trình bày (tên sheet, cột, khóa trong cột intents, mã loại và mã dịch vụ); script đổi
về tên nội bộ (tiếng Việt) trước khi kiểm, nên SLM, prompt và code không đổi. Excel cũ ghi tên nội bộ vẫn đọc được.
Bảng đối chiếu nằm ở EN_* bên dưới, là nơi duy nhất ghi cặp tên.

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
from app.schemas.slm import MAX_RAW_Y, PROFILE_KEYS, SkillRequest, SlmTinResult, SlmY, check_skill
from app.services.chat_flow import QUESTIONS

DATA_DIR = Path(__file__).resolve().parent.parent / "data" / "slm"
DEFAULT_EXCEL = DATA_DIR / "du_lieu_slm.xlsx"
DEFAULT_OUT = DATA_DIR / "du_lieu_slm.jsonl"

# Loại có skill riêng của học sinh (chỉ khi đã có hồ sơ). Các loại khác dùng skill chung cố định.
# Kịch bản do code tính, SLM chỉ tách thông tin (cột intents), nên không có skill
SKILL_TYPES = ("ho_so",)
# Đầu vào (giống nhau ở mọi loại) rồi đầu ra. Loại chính lấy từ tên sheet, id sinh theo sheet và thứ tự dòng
COLUMNS = ("cau_hoi", "ho_so", "cau_he_thong_dang_hoi", "cac_y", "tap")
SKILL_COLUMN = "skill_md"  # chỉ có ở sheet của SKILL_TYPES
SPLITS = ("train", "val", "test")
_WORD = re.compile(r"[^\W_]+(?:[-.'/][^\W_]+)*")


# Tên trong Excel (tiếng Anh) -> tên nội bộ. Tên chưa có trong bảng giữ nguyên, nên tên nội bộ cũng đọc được
EN_SHEETS = {
    "collect_profile": "mac_dinh",
    "profile_review": "ho_so",
    "scenario": "kich_ban",
    "knowledge": "kien_thuc",
    "center_referral": "trung_tam",
    "out_of_scope": "ngoai_pham_vi",
}
EN_COLUMNS = {
    "question": "cau_hoi",
    "profile": "ho_so",
    "pending_question": "cau_he_thong_dang_hoi",
    "intents": "cac_y",
    "split": "tap",
}
EN_ITEM_KEYS = {"span": "doan", "entities": "thuc_the"}
EN_ENTITY_KEYS = {
    "school": "truong",
    "city": "thanh_pho",
    "service": "dich_vu",
    "subtype": "loai_con",
    "mode": "che_do",
    "changes": "thay_doi",
    "factor": "yeu_to",
    "value": "gia_tri",
    "unit": "don_vi",
    "target_group": "muc_tieu",
    "action": "hanh_dong",
}
# Giá trị có mã: chỉ đổi giá trị của khóa tương ứng (khóa ghi theo tên nội bộ, sau khi đã đổi khóa)
EN_VALUES = {
    "loai_con": {
        "exception": "ngoai_le",
        "sensitive": "nhay_cam",
        "farewell": "tam_biet",
        "ambiguous": "mo_ho",
    },
    "dich_vu": {
        "SCHOOL_SELECTION": "CHON_TRUONG",
        "ESSAY_EDITING": "SUA_BAI_LUAN",
        "APPLICATION_SUBMISSION": "NOP_HO_SO",
        "VISA_INTERVIEW_PREP": "LUYEN_PHONG_VAN",
        "TEST_PREP": "LUYEN_THI",
        "SCHOLARSHIP_SEARCH": "SAN_HOC_BONG",
    },
    "don_vi": {"scale_4": "thang_4", "scale_10": "thang_10"},
    "hanh_dong": {"skip": "bo_qua"},
}
EN_TYPES = EN_SHEETS  # loại trong cột intents trùng tên sheet


def _entities_to_internal(value: Any) -> Any:
    if isinstance(value, list):
        return [_entities_to_internal(v) for v in value]
    if not isinstance(value, dict):
        return value
    out: dict[str, Any] = {}
    for key, item in value.items():
        name = EN_ENTITY_KEYS.get(key, key)
        item = _entities_to_internal(item)
        if isinstance(item, str):
            item = EN_VALUES.get(name, {}).get(item, item)
        out[name] = item
    return out


# Cột intents ghi bằng tiếng Anh thì đổi về khóa và mã nội bộ trước khi kiểm
def intents_to_internal(items: Any) -> Any:
    if not isinstance(items, list):
        return items
    result = []
    for item in items:
        if not isinstance(item, dict):
            result.append(item)
            continue
        converted = {EN_ITEM_KEYS.get(key, key): value for key, value in item.items()}
        if isinstance(converted.get("type"), str):
            converted["type"] = EN_TYPES.get(converted["type"], converted["type"])
        if "thuc_the" in converted:
            converted["thuc_the"] = _entities_to_internal(converted["thuc_the"])
        result.append(converted)
    return result


def columns_of(sheet: str) -> tuple[str, ...]:
    return COLUMNS[:-1] + (SKILL_COLUMN, "tap") if sheet in SKILL_TYPES else COLUMNS


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


# Kiểm cột cac_y: từng ý hợp lệ, doan chỉ dùng chữ có trong câu hỏi, giá trị hồ sơ nguyên văn, và không ý nào bị
# gộp hay bỏ khi chuẩn hóa (dữ liệu phải ghi sẵn đúng chuẩn, nếu không model học sai)
def check_cac_y(intent: str, question: str, pending: str, items: Any, where: str) -> list[dict[str, Any]]:
    if not isinstance(items, list) or not items:
        raise ValueError(f"{where}: cac_y phải là danh sách ý, ít nhất một ý")
    if len(items) > MAX_RAW_Y:
        raise ValueError(f"{where}: cac_y có {len(items)} ý, nhiều nhất {MAX_RAW_Y}")
    try:
        ys = [SlmY.model_validate(item) for item in items]
        result = SlmTinResult.from_raw({"cac_y": items})
    except ValueError as ex:
        raise ValueError(f"{where}: {ex}") from ex
    if len(result.cac_y) != len(ys):
        raise ValueError(
            f"{where}: cac_y chưa đúng chuẩn, có ý bị gộp hoặc bỏ (ý cùng loại hồ sơ phải gộp, bỏ ý chào)"
        )
    if [y.type for y in ys] != [y.type for y in result.cac_y]:
        raise ValueError(f"{where}: mac_dinh phải đứng đầu cac_y, các ý còn lại theo thứ tự học sinh nói")
    if intent not in {y.type for y in ys}:
        raise ValueError(f"{where}: sheet {intent} nhưng cac_y không có ý nào loại {intent}")
    spoken = words(question)
    for y in ys:
        extra = words(y.doan) - spoken
        if extra:
            raise ValueError(f"{where}: doan có chữ không có trong câu hỏi: {sorted(extra)}")
        if y.type != "mac_dinh" and set(y.thuc_the) & set(PROFILE_KEYS):
            raise ValueError(f"{where}: giá trị hồ sơ chỉ ghi ở ý mac_dinh, không ghi vào ý {y.type}")
        for key, value in y.thuc_the.items():
            if (key in PROFILE_KEYS or key == "thanh_pho") and value not in question:
                raise ValueError(f"{where}: {key} '{value}' phải là đoạn chữ có trong cau_hoi")
        # mac_dinh rỗng chỉ hợp lệ khi đang có câu chờ (tin mơ hồ, hỏi lại). Không có câu chờ thì phải có giá trị hồ sơ,
        # không thì là ngoai_pham_vi/mo_ho
        if y.type == "mac_dinh" and not pending and not y.thuc_the:
            raise ValueError(f"{where}: mac_dinh không có câu chờ thì thuc_the phải có giá trị hồ sơ")
    return [y.model_dump() for y in ys]


# Một dòng trong sheet thành một bản ghi. Sai chuẩn thì ném ValueError có tên sheet và số dòng
def to_record(intent: str, raw: dict[str, Any], where: str, record_id: str) -> dict[str, Any]:
    text = {key: as_text(raw.get(key)) for key in (*COLUMNS, SKILL_COLUMN)}
    if not text["cau_hoi"]:
        raise ValueError(f"{where}: thiếu cau_hoi")
    if text["tap"] not in SPLITS:
        raise ValueError(f"{where}: tap phải là một trong {SPLITS}, đang là '{text['tap']}'")
    pending = text["cau_he_thong_dang_hoi"]
    if pending and pending not in QUESTIONS.values():
        raise ValueError(f"{where}: cau_he_thong_dang_hoi không khớp câu nào trong chat_flow.QUESTIONS")
    profile = as_json(raw.get("ho_so"), where, "ho_so")
    if not isinstance(profile, dict):
        raise ValueError(f"{where}: cột ho_so phải là một object JSON")
    if set(profile) - set(PROFILE_KEYS):
        raise ValueError(f"{where}: ho_so có khóa lạ: {sorted(set(profile) - set(PROFILE_KEYS))}")
    cac_y = check_cac_y(
        intent,
        text["cau_hoi"],
        pending,
        intents_to_internal(as_json(raw.get("cac_y"), where, "cac_y")) or None,
        where,
    )
    skill = text[SKILL_COLUMN]
    if skill and intent not in SKILL_TYPES:
        raise ValueError(f"{where}: loại {intent} không có skill riêng")
    if skill and not profile:
        raise ValueError(f"{where}: có skill_md thì phải có hồ sơ (skill sinh theo hồ sơ)")
    if skill:
        main = next(y for y in cac_y if y["type"] == intent)["thuc_the"]
        request = SkillRequest(
            loai=intent,
            cau_hoi=text["cau_hoi"],
            ho_so=profile_text(profile),
            thay_doi=[{k: str(v) for k, v in change.items()} for change in main.get("thay_doi", [])],
            muc_tieu=main.get("muc_tieu") or (None if main.get("thay_doi") else text["cau_hoi"]),
        )
        try:
            check_skill(skill, request)
        except ValueError as ex:
            raise ValueError(f"{where}: skill_md sai: {ex}") from ex
    return {
        "id": record_id,
        "type": intent,
        "split": text["tap"],
        "input": {"cau_hoi": text["cau_hoi"], "ho_so": profile or None, "cau_he_thong_dang_hoi": pending},
        "output": {"cac_y": cac_y},
        "skill_md": skill or None,
    }


def read_records(path: Path) -> list[dict[str, Any]]:
    workbook = load_workbook(path, read_only=True, data_only=True)
    records: list[dict[str, Any]] = []
    for sheet in workbook.worksheets:
        if sheet.title in ("README", "summary"):
            continue  # sheet giải thích cho người đọc, không có dữ liệu
        intent = EN_SHEETS.get(sheet.title, sheet.title)
        if intent not in TYPES:
            raise ValueError(f"Sheet '{sheet.title}' không phải một loại trong {TYPES} (mỗi loại một sheet)")
        rows = sheet.iter_rows(values_only=True)
        header = tuple(
            EN_COLUMNS.get(as_text(cell), as_text(cell)) for cell in next(rows, ()) if as_text(cell)
        )
        # Sheet kịch bản không có skill, nhưng file cũ còn cột skill_md (để trống) thì vẫn đọc được
        allowed = {columns_of(intent), columns_of("ho_so")} if intent == "kich_ban" else {columns_of(intent)}
        if header not in allowed:
            raise ValueError(
                f"Sheet '{sheet.title}' phải có đúng các cột {columns_of(intent)}, đang là {header}"
            )
        count = 0
        for number, values in enumerate(rows, start=2):
            raw = dict(zip(header, values, strict=False))
            if not any(as_text(v) for v in raw.values()):
                continue  # dòng trống
            count += 1
            where = f"{sheet.title}, dòng {number}"
            records.append(to_record(intent, raw, where, f"{intent}_{count:03d}"))
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
        skills = sum(1 for r in rows if r["skill_md"])
        print(
            f"  {intent:14} {len(rows):4}  "
            + "  ".join(f"{s}={by_split[s]}" for s in SPLITS)
            + (f"  có skill={skills}" if intent in SKILL_TYPES else "")
        )
    by_count = Counter(len(r["output"]["cac_y"]) for r in records)
    print("  số ý mỗi dòng: " + "  ".join(f"{n} ý={by_count[n]}" for n in sorted(by_count)))


if __name__ == "__main__":
    main()
