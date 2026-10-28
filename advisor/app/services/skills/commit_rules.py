"""Bộ luật "đủ điều kiện xác nhận" của chat, chép từ ValidateForCommit (ProfileFlowService.cs).

B3 (skill_ho_so.run) chỉ mở X1 (rà soát) khi commit_blockers trả rỗng, nên học sinh bấm "Đúng" thì .NET không từ chối.
Hồ sơ có thể đến từ nháp form (form tự lưu cả khi đang điền dở), nên kiểm tra lại cả những giá trị đã có.
Luật chat chặt hơn hoặc bằng .NET, không bao giờ lỏng hơn.
"""

from collections.abc import Callable
from datetime import date
from typing import Any

from app.schemas.chat import ProfileField
from app.services.skills.skill_bac_hoc import (
    CURRENT_LEVELS,
    MAX_MAJOR_LEN,
    TARGET_LEVELS,
    UNKNOWN,
    check_grade,
)
from app.services.skills.validators import (
    ALL_TIERS,
    SCHOLARSHIP_LEVELS,
    AskAgain,
    check_activity,
    check_award,
    check_english,
    check_gpa,
    check_states,
)

# Bậc muốn học hợp lệ khi lưu: "chưa rõ" không lưu được (StudyLevels.IsValid)
STUDY_LEVELS = {code for code in TARGET_LEVELS.values() if code != UNKNOWN}


def ok_current_level(value: Any, profile: dict, today: date) -> bool:
    return value in CURRENT_LEVELS


# Lớp không bắt buộc, có thì phải thuộc đúng cấp học
def ok_current_grade(value: Any, profile: dict, today: date) -> bool:
    check_grade(profile.get("current_level"), value)
    return True


def ok_target_level(value: Any, profile: dict, today: date) -> bool:
    return value in STUDY_LEVELS


# Ngành không bắt buộc khi lưu (B4 mới đòi), có thì tối đa 128 ký tự
def ok_major(value: Any, profile: dict, today: date) -> bool:
    return value is None or len(value) <= MAX_MAJOR_LEN


# GPA bắt buộc khi lưu (kế hoạch đồng bộ 3.1, .NET thêm ở S6)
def ok_gpa(value: Any, profile: dict, today: date) -> bool:
    check_gpa(value["value"], value["scale"])
    return True


# Không có chứng chỉ thì vẫn lưu được; có thì điểm đúng thang và ngày thi không ở tương lai
def ok_english(value: Any, profile: dict, today: date) -> bool:
    if value is None:
        return True
    check_english(value["type"], value["value"], value.get("date"), today=today)
    return True


def ok_budget(value: Any, profile: dict, today: date) -> bool:
    return value in ALL_TIERS


# Bỏ luật này khi C6 xong (.NET cho để trống học bổng)
def ok_scholarship(value: Any, profile: dict, today: date) -> bool:
    return value in SCHOLARSHIP_LEVELS


# Mỗi hoạt động đủ vai trò, phạm vi, tháng bắt đầu, tháng kết thúc hoặc "đến nay"
def ok_activities(value: Any, profile: dict, today: date) -> bool:
    for item in value or []:
        check_activity(
            item.get("name"),
            item.get("role"),
            item.get("impactLevel"),
            item.get("startDate"),
            item.get("endDate"),
            bool(item.get("isOngoing")),
            today=today,
        )
    return True


# Mỗi giải thưởng có phạm vi và năm đạt giải
def ok_awards(value: Any, profile: dict, today: date) -> bool:
    for item in value or []:
        check_award(item.get("name"), item.get("impactLevel"), item.get("year"), today=today)
    return True


def ok_states(value: Any, profile: dict, today: date) -> bool:
    check_states(value or [])
    return True


# Khóa hồ sơ → luật, theo thứ tự hỏi lại. Khóa không có trong hồ sơ thì giá trị là None
RULES: dict[str, Callable[[Any, dict, date], bool]] = {
    "current_level": ok_current_level,
    "current_grade": ok_current_grade,
    "target_level": ok_target_level,
    "major": ok_major,
    "gpa": ok_gpa,
    "english": ok_english,
    "budget": ok_budget,
    "scholarship": ok_scholarship,
    "activities": ok_activities,
    "awards": ok_awards,
    "preferred_states": ok_states,
}


# Giá trị của từng khóa; "không có", "chưa rõ", "không trả lời" đều coi như không có giá trị
def profile_values(profile: dict[str, ProfileField]) -> dict[str, Any]:
    return {
        key: field.value if field.status in ("confirmed", "said_in_B0") else None
        for key, field in profile.items()
    }


# Các khóa .NET sẽ từ chối khi lưu, theo thứ tự hỏi lại. Rỗng thì được mở X1
def commit_blockers(profile: dict[str, ProfileField], today: date | None = None) -> list[str]:
    today = today or date.today()
    values = profile_values(profile)
    blockers = []
    for key, rule in RULES.items():
        try:
            ok = rule(values.get(key), values, today)
        except (AskAgain, ValueError, KeyError, TypeError):
            ok = False
        if not ok:
            blockers.append(key)
    return blockers
