"""C4. Làm phẳng hồ sơ chat thành ho_so của SkillRequest (khóa → chữ) để SLM sinh skill.

Lấy từ state.profile (nháp .NET gửi mỗi lượt, đã gộp với chat). Chữ viết giống dữ liệu train
(data/slm/du_lieu_slm.jsonl): "8 thang 10", "IELTS 6.5", "trưởng / chủ nhiệm CLB Tiếng Anh cấp tỉnh / thành phố từ
09/2024 đến nay".
skill_request_of dựng cả SkillRequest ho_so: hồ sơ làm phẳng (ho_so) và tách riêng các dòng code tính sẵn (tinh_san,
từ profile_calc: mức GPA, điểm TB từng năm, mốc thời gian...) để skill chỉ đọc rồi nhận xét, không tự tính. Không gửi
cả bảng điểm học bạ: TB từng năm đã nằm trong grade_by_year.
"""

import datetime
from typing import Any

from app.schemas.chat import ChatState, ProfileField
from app.schemas.slm import PROFILE_KEYS, SkillRequest
from app.services.profile_calc import calc_profile
from app.services.skills.buttons import (
    BUDGET_LABELS,
    BUTTONS,
    CURRENT_LEVEL_LABELS,
    EDUCATION_SYSTEM_LABELS,
    FUNDING_SOURCE_LABELS,
    IMPACT_LABELS,
    ROLE_LABELS,
    SCHOLARSHIP_LABELS,
    SCHOOL_TYPE_LABELS,
)
from app.services.skills.skill_danh_gia import gpa_table_of
from app.services.skills.validators import BUDGET_RANGES, CERT_NAMES

# Trạng thái có giá trị thật; declined (không muốn nói) thì bỏ khóa
VALUE_STATUSES = ("confirmed", "said_in_B0")
NONE_TEXT = "không có"
UNKNOWN_TEXT = "chưa rõ"

# Bậc muốn học: lấy chữ từ nút B1.2, không chép tay
TARGET_LEVEL_LABELS = {button.values["target_level"].value: button.label for button in BUTTONS["B1.2"]}
# Khóa có mã → nhãn form
CODE_LABELS = {
    "current_level": CURRENT_LEVEL_LABELS,
    "target_level": TARGET_LEVEL_LABELS,
    "education_system": EDUCATION_SYSTEM_LABELS,
    "funding_source": FUNDING_SOURCE_LABELS,
    "scholarship": SCHOLARSHIP_LABELS,
    "school_type": SCHOOL_TYPE_LABELS,
}


# Viết thường chữ đầu để ghép vào câu ("Nên có" → "nên có"), giữ nguyên phần sau ("Việt Nam")
def lower_first(text: str) -> str:
    return text[:1].lower() + text[1:]


# 7.0 → "7", 6.5 → "6.5", 1450 → "1450"
def number(value: Any) -> str:
    return f"{float(value):g}" if isinstance(value, int | float) else str(value)


# 35000 → "35.000"
def usd(amount: int) -> str:
    return f"{amount:,}".replace(",", ".")


# "2027-05" → "05/2027" như học sinh nói
def month_text(date: str) -> str:
    year, _, month = date.partition("-")
    return f"{month}/{year}" if month else date


def gpa_text(value: dict) -> str:
    return f"{number(value['value'])} thang {value['scale']}"


def english_text(value: dict) -> str:
    return f"{CERT_NAMES.get(value['type'], value['type'].upper())} {number(value['value'])}"


# Mức ngân sách → khoảng tiền, sàn và trần lấy từ BUDGET_RANGES
def budget_text(tier: str) -> str:
    if tier not in BUDGET_RANGES:
        return BUDGET_LABELS.get(tier, tier)
    floor, ceiling = BUDGET_RANGES[tier]
    if ceiling is None:
        return f"trên {usd(floor)} USD mỗi năm"
    if floor == 0:
        return f"dưới {usd(ceiling)} USD mỗi năm"
    return f"khoảng {usd(floor)} đến {usd(ceiling)} USD mỗi năm"


# {"planned": True, "date": "2027-05", "test_type": "ielts", "target_score": 7} → "dự định thi IELTS tháng 05/2027, mục tiêu 7"
def plan_text(value: dict) -> str:
    if not value.get("planned"):
        return "chưa có kế hoạch thi"
    parts = ["dự định thi"]
    if value.get("test_type"):
        parts.append(CERT_NAMES.get(value["test_type"], value["test_type"].upper()))
    if value.get("date"):
        parts.append(f"tháng {month_text(value['date'])}")
    text = " ".join(parts)
    if value.get("target_score") is not None:
        text += f", mục tiêu {number(value['target_score'])}"
    return text


def activity_text(item: dict) -> str:
    role = ROLE_LABELS.get(item.get("role") or "", item.get("role") or "").lower()
    impact = IMPACT_LABELS.get(item.get("impactLevel") or "", item.get("impactLevel") or "").lower()
    end = "nay" if item.get("isOngoing") else item.get("endDate")
    return " ".join(
        part
        for part in (
            role,
            item.get("name"),
            f"cấp {impact}" if impact else "",
            f"từ {item.get('startDate')} đến {end}",
        )
        if part
    )


def award_text(item: dict) -> str:
    impact = IMPACT_LABELS.get(item.get("impactLevel") or "", item.get("impactLevel") or "").lower()
    parts = [item.get("name") or "", f"cấp {impact}" if impact else ""]
    if item.get("year"):
        parts.append(f"năm {item['year']}")
    return " ".join(part for part in parts if part)


# Một giá trị có thật → chữ. Dạng lạ thì str() để không mất thông tin
def value_text(key: str, value: Any) -> str:
    if key in CODE_LABELS and isinstance(value, str):
        return lower_first(CODE_LABELS[key].get(value, value))
    if key == "gpa" and isinstance(value, dict):
        return gpa_text(value)
    if key == "english" and isinstance(value, dict):
        return english_text(value)
    if key == "budget" and isinstance(value, str):
        return budget_text(value)
    if key in ("english_plan", "standardized_plan") and isinstance(value, dict):
        return plan_text(value)
    if key == "visa_refused" and isinstance(value, bool):
        return "đã từng bị từ chối visa" if value else "chưa từng bị từ chối visa"
    if key == "activities" and isinstance(value, list):
        return "; ".join(activity_text(item) if isinstance(item, dict) else str(item) for item in value)
    if key == "awards" and isinstance(value, list):
        return "; ".join(award_text(item) if isinstance(item, dict) else str(item) for item in value)
    if key == "preferred_states" and isinstance(value, list):
        return ", ".join(map(str, value))
    if isinstance(value, list):
        return "; ".join(map(str, value))
    return number(value) if isinstance(value, int | float) and not isinstance(value, bool) else str(value)


# Hồ sơ chat → ho_so của SkillRequest. Chỉ giữ khóa trong PROFILE_KEYS (bỏ *_pending, certifications...), theo thứ tự
# PROFILE_KEYS. Học sinh nói "không có" thì ghi "không có" để skill biết là thiếu thật chứ không phải chưa hỏi
def profile_to_skill_input(profile: dict[str, ProfileField]) -> dict[str, str]:
    result = {}
    for key in PROFILE_KEYS:
        field = profile.get(key)
        if field is None:
            continue
        if field.status == "none":
            result[key] = NONE_TEXT
        elif field.status == "unknown":
            result[key] = UNKNOWN_TEXT
        elif field.status in VALUE_STATUSES and field.value not in (None, "", []):
            text = value_text(key, field.value).strip()
            if text:
                result[key] = text
    return result


# Đầu vào sinh skill ho_so: hồ sơ làm phẳng, và tách riêng các dòng code tính sẵn từ hồ sơ, học bạ (state.transcript)
# và mốc trường (state.benchmarks). Hạn nộp của trường chưa có đường gửi sang nên timeline tạm chỉ có tháng nhập học
def skill_request_of(state: ChatState, cau_hoi: str, today: datetime.date | None = None) -> SkillRequest:
    tinh_san = calc_profile(
        state.profile,
        state.transcript,
        today or datetime.date.today(),
        benchmarks=state.benchmarks,
        gpa_table=gpa_table_of(state.grade_scale),
    )
    return SkillRequest(
        loai="ho_so", cau_hoi=cau_hoi, ho_so=profile_to_skill_input(state.profile), tinh_san=tinh_san
    )
