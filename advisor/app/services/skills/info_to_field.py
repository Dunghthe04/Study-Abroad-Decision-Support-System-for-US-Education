"""Đổi khóa thông tin Qwen trả về thành khóa chuẩn của hồ sơ chat (HUONG_DAN mục 6.2).

skill_nhu_cau trả các khóa kiểu "gpa_text", "budget_text". skill_rules.missing_fields chỉ biết khóa chuẩn
("gpa", "budget"...), nên phải đổi ở đây, nếu không chat sẽ hỏi lại mãi. Chỉ đổi tên khóa, không đọc giá trị.
"""

from typing import Any

# Khóa "*_text" của skill_nhu_cau → khóa chuẩn trong state.profile
INFO_TO_FIELD = {
    "english_text": "english",
    "gpa_text": "gpa",
    "budget_text": "budget",
    "major_text": "major",
}

# Khóa chuẩn của hồ sơ chat, giữ đồng bộ với skill_rules (REQUIRED_B4) và bảng 6.2
PROFILE_KEYS = (
    "current_level",
    "current_grade",
    "target_level",
    "major",
    "intake",
    "education_system",
    "gpa",
    "sat",
    "act",
    "english",
    "english_plan",
    "budget",
    "scholarship",
    "funding_source",
    "activities",
    "awards",
    "relatives_state",
    "preferred_states",
    "school_type",
    "visa_refused",
)


# Đổi một dict thông tin sang khóa chuẩn. Khóa lạ và giá trị rỗng bị bỏ, để Qwen không ghi bừa vào hồ sơ.
# Nếu Qwen trả cả "gpa" lẫn "gpa_text" thì giữ "gpa" (khóa chuẩn thắng)
def to_fields(info: dict[str, Any]) -> dict[str, Any]:
    fields: dict[str, Any] = {}
    for key, value in info.items():
        if value is None or value == "":
            continue
        if key in PROFILE_KEYS:
            fields[key] = value
        elif key in INFO_TO_FIELD:
            fields.setdefault(INFO_TO_FIELD[key], value)
    return fields
