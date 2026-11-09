"""Đổi file Excel dữ liệu của SLM thành JSONL để huấn luyện và đo. Excel là nguồn, JSONL sinh ra từ Excel, không sửa tay.

Mỗi loại một sheet (mac_dinh, ho_so, kich_ban, kien_thuc, trung_tam, ngoai_pham_vi): sheet là loại chính của dòng,
dòng nhiều ý đặt ở sheet của một trong các ý. Mỗi dòng: đầu vào của SLM (câu hỏi, hồ sơ, câu hệ thống đang hỏi) và đầu ra
mong muốn là cột cac_y, một danh sách ý [{"type", "doan", "thuc_the"}] theo thứ tự học sinh nói (skill md với hồ sơ và kịch bản
khi có hồ sơ). Dòng sai chuẩn thì báo sheet và số dòng.

File Excel ghi bằng tiếng Anh cho dễ trình bày (tên sheet, cột, khóa trong cột intents, mã loại và mã dịch vụ); script đổi
về tên nội bộ (tiếng Việt) trước khi kiểm, nên SLM, prompt và code không đổi. Excel cũ ghi tên nội bộ vẫn đọc được.
Bảng đối chiếu nằm ở EN_* bên dưới, là nơi duy nhất ghi cặp tên.

Cột context (tùy chọn, ở mọi sheet): điều hệ thống đang nói tới, ghi tiếng Anh, script đổi về tên nội bộ rồi đưa vào đầu vào của SLM
(khóa dang_noi_ve) giống lúc chạy thật. Dòng không có ngữ cảnh thì bỏ trống, bản ghi giữ nguyên khuôn cũ.

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

from app.schemas.slm import CALC_KEYS, MAX_RAW_Y, PROFILE_KEYS, SkillRequest, SlmTinResult, SlmY, check_skill
from app.schemas.slm import INTENTS as TYPES
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
# Dòng code tính sẵn (JSON khóa CALC_KEYS → chữ), đứng sau cột hồ sơ, chỉ ở sheet của SKILL_TYPES. Không bắt buộc (file cũ
# chưa có). Chỉ đưa vào đầu vào sinh skill, không vào đầu vào tách ý vì lúc chạy SLM lần 1 không thấy các dòng này
CALC_COLUMN = "ho_so_tinh_san"
CONTEXT_COLUMN = "context"  # tùy chọn, ở mọi sheet: điều hệ thống đang nói tới (JSON, ghi tiếng Anh)
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
    "profile_calc": CALC_COLUMN,
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
        "about_system": "ve_he_thong",
        "human_handoff": "gap_nguoi",
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

# ---- Khuôn v2: nhiệm vụ, danh sách trường / trung tâm, mục, bộ lọc, cách xếp, nối tiếp. Các cặp tên này đổi cả tên khóa lẫn mã
EN_PROFILE_TASKS = {
    "answer": "tra_loi",
    "edit": "sua",
    "status": "xem_ho_so",
    "restart": "lam_lai",
}  # collect_profile
EN_REVIEW_TASKS = {  # profile_review, ghi thành danh sách cac_nhiem_vu
    "assessment": "danh_gia",
    "school_list": "chon_truong",
    "roadmap": "lo_trinh",
    "compare": "so_sanh",
    "explain_school": "giai_thich_truong",
}
EN_KNOWLEDGE_TASKS = {
    "school_info": "tra_cuu",
    "compare_facts": "so_sanh",
    "search": "tim_kiem",
}  # general = không ghi nhiem_vu
EN_CENTER_TASKS = {"find": "tim", "booking": "dat_lich", "compare": "so_sanh"}  # center_referral
EN_FIELDS = {
    "cost": "chi_phi",
    "aid": "hoc_bong",
    "english_requirement": "tieng_anh",
    "test_policy": "diem_thi",
    "gpa": "gpa",
    "acceptance_rate": "ty_le_nhan",
    "deadline": "han_nop",
    "graduation_rate": "tot_nghiep",
    "majors": "nganh",
    "ranking": "xep_hang",
    "location": "vi_tri",
    "website": "website",
    "other": "khac",
}
EN_FILTER_KEYS = {
    "state": "bang",
    "city": "thanh_pho",
    "major": "nganh",
    "school_type": "loai_truong",
    "aid": "hoc_bong",
    "test_policy": "chinh_sach_thi",
    "max_cost": "chi_phi_toi_da",
    "max_ielts": "ielts_toi_da",
    "max_toefl": "toefl_toi_da",
    "max_duolingo": "duolingo_toi_da",
    "min_acceptance_rate": "ty_le_nhan_toi_thieu",
    "max_acceptance_rate": "ty_le_nhan_toi_da",
    "max_rank": "xep_hang_toi_da",
    "min_graduation_rate": "ty_le_tot_nghiep_toi_thieu",
    "ivy": "ivy",
}
EN_FILTER_VALUES = {  # theo khóa nội bộ; khóa không có ở đây giữ nguyên chữ học sinh gõ
    "loai_truong": {"public": "cong_lap", "private": "tu_thuc"},
    "hoc_bong": {"yes": "co", "need_based": "theo_nhu_cau", "merit_based": "theo_thanh_tich"},
    "chinh_sach_thi": {"optional": "khong_bat_buoc", "required": "bat_buoc"},
    "ivy": {"yes": "co"},
}
EN_SORTS = {
    "ranking": "xep_hang",
    "lowest_cost": "chi_phi_thap",
    "easiest_admission": "de_vao",
    "most_selective": "chon_loc",
}
# Khóa của cột context (điều hệ thống đang nói tới): tên Anh -> tên nội bộ
EN_CONTEXT_KEYS = {
    "schools": "truong",
    "fields": "muc",
    "filters": "bo_loc",
    "scenario": "kich_ban",
    "last_field": "muc_vua_nhap",
    "last_task": "viec_vua_lam",
    "service": "dich_vu",
    "city": "thanh_pho",
    "centers": "cac_trung_tam",
}


def _filters_to_internal(filters: dict[str, Any]) -> dict[str, Any]:
    out: dict[str, Any] = {}
    for key, value in filters.items():
        name = EN_FILTER_KEYS.get(key, key)
        if name == "bang" and isinstance(value, str):
            value = [value]
        if isinstance(value, str):
            value = EN_FILTER_VALUES.get(name, {}).get(value, value)
        out[name] = value
    return out


# Khóa và mã của khuôn v2 trong thuc_the của một ý -> tên nội bộ. kind là loại ghi trong Excel (tên Anh);
# loại ghi tên nội bộ (file cũ) giữ nguyên
def v2_entities_to_internal(kind: str, entities: dict[str, Any]) -> dict[str, Any]:
    if kind not in EN_SHEETS:
        return entities
    tasks = entities.get("tasks")
    first = tasks[0] if isinstance(tasks, list) and tasks else None
    out: dict[str, Any] = {}
    for key, value in entities.items():
        if key == "tasks":
            if kind == "collect_profile" and first:
                out["nhiem_vu"] = EN_PROFILE_TASKS.get(first, first)
            elif kind == "profile_review":
                out["cac_nhiem_vu"] = [EN_REVIEW_TASKS.get(t, t) for t in value]
            elif kind == "knowledge" and first and first != "general":
                out["nhiem_vu"] = EN_KNOWLEDGE_TASKS.get(first, first)
            elif kind == "center_referral" and first:
                out["nhiem_vu"] = EN_CENTER_TASKS.get(first, first)
            elif kind not in ("collect_profile", "knowledge", "center_referral"):
                out["tasks"] = (
                    value  # loại không có nhiệm vụ (kịch bản, ngoài phạm vi): giữ nguyên để bộ kiểm báo lỗi
                )
        elif key == "schools":
            comparing = any(
                t in ("compare_facts", "compare") for t in (tasks if isinstance(tasks, list) else [])
            )
            if len(value) >= 2 or comparing:
                out["cac_truong"] = list(value)
            elif len(value) == 1:
                out["truong"] = value[0]
        elif key == "centers":
            out["cac_trung_tam"] = list(value)
        elif key == "fields":
            out["muc"] = [EN_FIELDS.get(f, f) for f in value]
        elif key == "field" and isinstance(value, str):  # khuôn v2 cũ ghi một mục
            out["muc"] = [EN_FIELDS.get(value, value)]
        elif key == "filters":
            out["bo_loc"] = _filters_to_internal(value)
        elif key == "sort":
            out["sap_xep"] = EN_SORTS.get(value, value)
        elif key == "follow_up":
            out["tiep_noi"] = value
        else:
            out[key] = value
    return out


# Cột context -> khóa nội bộ của dang_noi_ve. Rỗng thì None (dòng không có ngữ cảnh)
def context_to_internal(context: Any) -> dict[str, Any] | None:
    if not context:
        return None
    if not isinstance(context, dict):
        raise ValueError("cột context phải là một object JSON")
    out: dict[str, Any] = {}
    for key, value in context.items():
        name = EN_CONTEXT_KEYS.get(key, key)
        if name == "muc":
            value = [EN_FIELDS.get(f, f) for f in value]
        elif name == "bo_loc":
            value = _filters_to_internal(value)
        elif name == "kich_ban":
            value = _entities_to_internal(v2_entities_to_internal("scenario", value))
        elif name == "viec_vua_lam":
            value = EN_REVIEW_TASKS.get(value, value)
        elif name == "dich_vu":
            value = EN_VALUES["dich_vu"].get(value, value)
        out[name] = value
    return out


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
        if isinstance(
            converted.get("thuc_the"), dict
        ):  # khuôn v2: cần biết loại ghi bằng tên Anh nên làm trước khi đổi loại
            converted["thuc_the"] = v2_entities_to_internal(str(item.get("type")), converted["thuc_the"])
        if isinstance(converted.get("type"), str):
            converted["type"] = EN_TYPES.get(converted["type"], converted["type"])
        if "thuc_the" in converted:
            converted["thuc_the"] = _entities_to_internal(converted["thuc_the"])
        result.append(converted)
    return result


def columns_of(sheet: str, with_calc: bool = False) -> tuple[str, ...]:
    if sheet not in SKILL_TYPES:
        return COLUMNS
    columns = COLUMNS[:-1] + (SKILL_COLUMN, "tap")
    return columns[:2] + (CALC_COLUMN,) + columns[2:] if with_calc else columns


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
def check_cac_y(
    intent: str, question: str, pending: str, items: Any, where: str, context: dict[str, Any] | None = None
) -> list[dict[str, Any]]:
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
                # Câu nối tiếp ("thế còn luyện phỏng vấn") lấy thành phố từ ngữ cảnh: phải đúng thành phố trong ngữ cảnh
                from_context = (
                    key == "thanh_pho"
                    and y.thuc_the.get("tiep_noi") is True
                    and value == (context or {}).get("thanh_pho")
                )
                if not from_context:
                    raise ValueError(f"{where}: {key} '{value}' phải là đoạn chữ có trong cau_hoi")
        # mac_dinh rỗng chỉ hợp lệ khi đang có câu chờ (tin mơ hồ, hỏi lại). Không có câu chờ thì phải có giá trị hồ sơ,
        # không thì là ngoai_pham_vi/mo_ho
        if y.type == "mac_dinh" and not pending and not y.thuc_the:
            raise ValueError(f"{where}: mac_dinh không có câu chờ thì thuc_the phải có giá trị hồ sơ")
    return [y.model_dump() for y in ys]


# Một dòng trong sheet thành một bản ghi. Sai chuẩn thì ném ValueError có tên sheet và số dòng
def to_record(intent: str, raw: dict[str, Any], where: str, record_id: str) -> dict[str, Any]:
    text = {key: as_text(raw.get(key)) for key in (*COLUMNS, SKILL_COLUMN)}  # context đọc riêng bên dưới
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
    calc = as_json(raw.get(CALC_COLUMN), where, CALC_COLUMN)
    if not isinstance(calc, dict):
        raise ValueError(f"{where}: cột {CALC_COLUMN} phải là một object JSON")
    if set(calc) - set(CALC_KEYS):
        raise ValueError(f"{where}: {CALC_COLUMN} có khóa lạ: {sorted(set(calc) - set(CALC_KEYS))}")
    context = context_to_internal(as_json(raw.get("context"), where, "context"))
    cac_y = check_cac_y(
        intent,
        text["cau_hoi"],
        pending,
        intents_to_internal(as_json(raw.get("cac_y"), where, "cac_y")) or None,
        where,
        context,
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
            tinh_san=profile_text(calc),
            thay_doi=[{k: str(v) for k, v in change.items()} for change in main.get("thay_doi", [])],
            muc_tieu=main.get("muc_tieu") or (None if main.get("thay_doi") else text["cau_hoi"]),
        )
        try:
            check_skill(skill, request)
        except ValueError as ex:
            raise ValueError(f"{where}: skill_md sai: {ex}") from ex
    record = {
        "id": record_id,
        "type": intent,
        "split": text["tap"],
        "input": {"cau_hoi": text["cau_hoi"], "ho_so": profile or None, "cau_he_thong_dang_hoi": pending},
        "output": {"cac_y": cac_y},
        "skill_md": skill or None,
    }
    # Ngữ cảnh là đầu vào của SLM (giống lúc chạy: slm_input); chỉ ghi khi có, nên dòng không ngữ cảnh giữ nguyên khuôn cũ
    if context:
        record["input"]["dang_noi_ve"] = context
    # Ngoài input (input là đầu vào tách ý); chỉ ghi khi có, nên dòng cũ giữ nguyên
    if calc:
        record["tinh_san"] = profile_text(calc)
    return record


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
        full_header = tuple(
            EN_COLUMNS.get(as_text(cell), as_text(cell)) for cell in next(rows, ()) if as_text(cell)
        )
        header = tuple(
            name for name in full_header if name != CONTEXT_COLUMN
        )  # cột context tùy chọn, không tính vào bộ cột bắt buộc
        # Sheet kịch bản không có skill, nhưng file cũ còn cột skill_md (để trống) thì vẫn đọc được
        allowed = {columns_of(intent), columns_of("ho_so")} if intent == "kich_ban" else {columns_of(intent)}
        if intent in SKILL_TYPES:
            allowed.add(columns_of(intent, with_calc=True))
        if header not in allowed:
            raise ValueError(
                f"Sheet '{sheet.title}' phải có đúng các cột {columns_of(intent)}, đang là {header}"
            )
        count = 0
        for number, values in enumerate(rows, start=2):
            raw = dict(zip(full_header, values, strict=False))
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
