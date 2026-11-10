"""B1. Skill ho_so dựng bằng code từ khuôn 9 mục (skill_ho_so_khuon.md): đúng mục, đúng việc, khóa thiếu không vỡ,
không có số nào ngoài dữ liệu."""

import re
from datetime import date

import pytest

from app.schemas.chat import ChatState, GradeScaleRule, ProfileField
from app.schemas.slm import PROFILE_KEYS
from app.services.skill_input import skill_request_of
from app.services.skill_template import build_skill, fill_du_lieu, fill_noi_dung, tasks_of
from app.services.skills.skill_danh_gia import NUMBER, allowed_numbers, gpa_table_of, read_facts, readings
from app.services.skills.skill_lo_trinh import choose_route, timeline_numbers

TODAY = date(2026, 11, 5)
SECTIONS = (
    "## 0. VAI TRÒ",
    "## 1. NHIỆM VỤ",
    "## 2. NGUYÊN TẮC DỮ LIỆU",
    "## 3. DỮ LIỆU BẠN NHẬN",
    "## 4. CÁCH HIỂU DỮ LIỆU",
    "## 5. NỘI DUNG CẦN NÓI",
    "## 6. GIỚI HẠN VÀ ĐIỀU CẤM",
    "## 7. ĐỊNH DẠNG ĐẦU RA",
    "## 8. VÍ DỤ",
)
PLACEHOLDER = re.compile(r"\{(nhiem_vu|du_lieu|noi_dung)\}")
ASSESS = '### Phần "Đánh giá hồ sơ"'
ROADMAP = '### Phần "Lộ trình chuẩn bị"'

# Hồ sơ đủ 21 khóa, giống docs/MAU_SKILL_HO_SO_DAY_DU.md
FULL = {
    "current_level": "high_school",
    "current_grade": "Lớp 11",
    "target_level": "undergraduate",
    "major": "Khoa học máy tính",
    "intake": "Fall 2028",
    "education_system": "specialized",
    "gpa": {"value": 8.7, "scale": 10},
    "sat": 1350,
    "act": 29,
    "english": {"type": "ielts", "value": 6.5},
    "english_plan": {"planned": True, "date": "2027-06", "test_type": "ielts", "target_score": 7.5},
    "standardized_plan": {"planned": True, "date": "2027-10", "test_type": "sat", "target_score": 1450},
    "budget": "tier_35k_50k",
    "scholarship": "preferred",
    "funding_source": "family_support",
    "activities": [
        {
            "name": "CLB Lập trình",
            "role": "founder",
            "impactLevel": "school",
            "startDate": "09/2024",
            "isOngoing": True,
        },
        {
            "name": "Dự án dạy tin học cho trẻ vùng cao",
            "role": "leader",
            "impactLevel": "province",
            "startDate": "06/2025",
            "isOngoing": True,
        },
    ],
    "awards": [{"name": "Huy chương Đồng Olympic Tin học", "impactLevel": "national", "year": 2026}],
    "relatives_state": "California",
    "preferred_states": ["California", "Texas"],
    "school_type": "public",
    "visa_refused": False,
}
TRANSCRIPT = {"years": [{"grade": "Lớp 10", "average": 8.5}, {"grade": "Lớp 11", "average": 8.9}]}
BENCHMARKS = {"gpa4": 3.73, "english": {"ielts": 6.5}}


def fields(values: dict) -> dict[str, ProfileField]:
    return {key: ProfileField(value=value, status="confirmed") for key, value in values.items()}


def state_of(values: dict, benchmarks: dict | None = BENCHMARKS, **extra) -> ChatState:
    return ChatState(profile=fields(values), transcript=TRANSCRIPT, benchmarks=benchmarks, **extra)


def skill_of(state: ChatState, tasks: list[str] | None = None) -> str:
    req = skill_request_of(state, "Đánh giá hồ sơ của em và cho em lộ trình", TODAY)
    return build_skill(req, tasks, state.profile, TODAY, gpa_table_of(state.grade_scale))


def test_nine_sections_in_order_and_no_placeholder_left():
    skill = skill_of(state_of(FULL))

    positions = [skill.index(title) for title in SECTIONS]
    assert positions == sorted(positions)
    assert skill.startswith("# SKILL TƯ VẤN HỒ SƠ (skill_ho_so_khuon) - skill_version 1.0")
    assert not PLACEHOLDER.search(skill)
    assert "None" not in skill


def test_assessment_only():
    skill = skill_of(state_of(FULL), ["danh_gia"])

    assert ASSESS in skill
    assert ROADMAP not in skill
    assert "Không viết phần lộ trình chuẩn bị." in skill


def test_roadmap_only():
    skill = skill_of(state_of(FULL), ["lo_trinh"])

    assert ROADMAP in skill
    assert ASSESS not in skill
    assert "Lộ trình phù hợp với bạn: Đại học thẳng." in skill


@pytest.mark.parametrize("tasks", [None, [], ["chon_truong"], ["lo_trinh", "danh_gia", "lo_trinh"]])
def test_both_tasks(tasks):
    skill = skill_of(state_of(FULL), tasks)

    assert ASSESS in skill and ROADMAP in skill
    assert tasks_of(tasks) in (["danh_gia", "lo_trinh"], ["lo_trinh", "danh_gia"])


def test_tasks_follow_student_order():
    skill = skill_of(state_of(FULL), ["lo_trinh", "danh_gia"])

    assert skill.index(ROADMAP) < skill.index(ASSESS)


@pytest.mark.parametrize("key", PROFILE_KEYS)
def test_each_key_can_be_missing(key):
    values = {k: v for k, v in FULL.items() if k != key}

    skill = skill_of(state_of(values))

    assert not PLACEHOLDER.search(skill)
    assert "None" not in skill
    if key == "major":
        assert "| Ngành | Chưa chọn |  |" in skill
        assert "bạn chưa chọn ngành" in skill


def test_none_and_unknown_values():
    profile = fields(FULL)
    profile["english"] = ProfileField(status="none")
    profile["act"] = ProfileField(status="unknown")
    state = ChatState(profile=profile, transcript=TRANSCRIPT, benchmarks=BENCHMARKS)

    skill = skill_of(state)

    assert "| Tiếng Anh | Không có |  |" in skill
    assert "| ACT | Chưa rõ |  |" in skill
    assert "Chưa có trong hồ sơ: Tiếng Anh." in skill


def test_without_benchmarks_says_no_mark():
    skill = skill_of(state_of(FULL, benchmarks=None))

    assert "Chưa có dữ liệu trường để so: GPA 3.7 thang 4; Tiếng Anh IELTS 6.5; ACT 29." in skill


def test_low_gpa_and_refused_visa_are_attention():
    values = FULL | {"gpa": {"value": 7.2, "scale": 10}, "visa_refused": True}

    skill = skill_of(state_of(values))

    assert "Cần lưu ý nhất: GPA 3 thang 4, thấp (TB các trường 3.73); Visa: đã từng bị từ chối visa." in skill
    assert "- Visa: chuẩn bị giải thích rõ lần bị từ chối trước." in skill


def test_middle_school():
    values = FULL | {"current_level": "middle_school", "current_grade": "Lớp 8"}

    skill = skill_of(state_of(values))

    assert "Bạn đang học THCS: GPA là mục tiêu" in skill
    assert "Bạn đang học THCS: lộ trình trên là định hướng dài hạn" in skill


def test_budget_above_70k_is_floor_only():
    skill = skill_of(state_of(FULL | {"budget": "tier_above_70k"}))

    assert "| Ngân sách | Trên 70.000 USD mỗi năm | Trên 70.000 USD mỗi năm, mức đủ |" in skill


def test_gpa_uses_grade_scale_table():
    values = FULL | {"gpa": {"value": 8.0, "scale": 10}}
    rules = [GradeScaleRule(min_score=8.0, max_score=10, gpa4=3.3, letter_grade="B+")]

    assert "3.5 thang 4" in skill_of(state_of(values))
    assert "3.3 thang 4" in skill_of(state_of(values, grade_scale=rules))


# Mọi số code điền vào (mục 3 và 5) phải có trong hồ sơ, dòng tính sẵn, mốc lộ trình hoặc số được phép của hồ sơ
@pytest.mark.parametrize(
    "values",
    [FULL, FULL | {"gpa": {"value": 6.0, "scale": 10}, "budget": "tier_under_20k"}, {"intake": "Fall 2027"}],
)
def test_no_number_outside_data(values):
    state = state_of(values)
    req = skill_request_of(state, "x", TODAY)
    route, facts, intake = choose_route(state.profile, TODAY)
    allowed = allowed_numbers(read_facts(state.profile, TODAY)) | timeline_numbers(intake, route)
    for text in (*req.ho_so.values(), *req.tinh_san.values()):
        for token in NUMBER.findall(text):
            allowed |= readings(token)

    filled = fill_du_lieu(req) + fill_noi_dung(req, ["danh_gia", "lo_trinh"], state.profile, TODAY)

    for token in NUMBER.findall(filled):
        assert any(abs(r - a) < 0.005 for r in readings(token) for a in allowed), token


def test_full_profile_keeps_every_number():
    state = state_of(FULL)
    req = skill_request_of(state, "x", TODAY)

    skill = skill_of(state)

    for text in (*req.ho_so.values(), *req.tinh_san.values()):
        for token in NUMBER.findall(text):
            assert token in skill, token
