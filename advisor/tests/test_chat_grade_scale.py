"""Bảng quy đổi điểm grade_scale.10 (moderator sửa): .NET gửi mỗi lượt, chat cất vào state.grade_scale,
skill tra GPA tổng thang 10 sang thang 4 theo bảng này; chưa có bảng thì dùng bảng WES."""

from datetime import date

from app.schemas.chat import ChatMessage, ChatRequest, ChatState, GradeScaleRule, ProfileField
from app.services.profile_calc import calc_profile
from app.services.skill_input import skill_request_of
from app.services.skills.skill_danh_gia import gpa10_to_4, gpa_table_of, read_facts
from app.services.skills.skill_lo_trinh import choose_route
from tests.fakes import flow_with, tin

TODAY = date(2026, 11, 10)
ASK = tin("profile_review")
# Bảng moderator sửa: khác WES ở mốc 8.0 (WES 3.5) để thấy rõ bảng nào được dùng. Cố ý để thứ tự lộn xộn
RULES = [
    GradeScaleRule(min_score=8.0, max_score=8.99, gpa4=3.3, letter_grade="B+"),
    GradeScaleRule(min_score=9.0, max_score=10, gpa4=4.0, letter_grade="A"),
    GradeScaleRule(min_score=0, max_score=7.99, gpa4=2.0, letter_grade="C"),
]
TABLE = gpa_table_of(RULES)


def profile(gpa: dict) -> dict[str, ProfileField]:
    return {"gpa": ProfileField(value=gpa, status="confirmed")}


def request(state: ChatState, grade_scale: list[GradeScaleRule] | None) -> ChatRequest:
    return ChatRequest(
        messages=[ChatMessage(role="user", content="hồ sơ em thế nào")], state=state, grade_scale=grade_scale
    )


def test_request_reads_grade_scale_in_camel_case():
    req = ChatRequest.model_validate(
        {
            "messages": [{"role": "user", "content": "hi"}],
            "gradeScale": [{"minScore": 8.0, "maxScore": 8.99, "gpa4": 3.3, "letterGrade": "B+"}],
        }
    )

    assert req.grade_scale == [GradeScaleRule(min_score=8.0, max_score=8.99, gpa4=3.3, letter_grade="B+")]


async def test_grade_scale_is_kept_in_state():
    res = await flow_with({}, ASK).reply(request(ChatState(current_step="B4"), RULES))

    assert res.state.grade_scale == RULES
    assert "grade_scale" not in res.state.profile


async def test_turn_without_grade_scale_keeps_the_old_one():
    res = await flow_with({}, ASK).reply(request(ChatState(current_step="B4", grade_scale=RULES), None))

    assert res.state.grade_scale == RULES


def test_table_is_sorted_high_to_low_and_empty_means_default():
    assert TABLE == [(9.0, 4.0), (8.0, 3.3), (0.0, 2.0)]
    assert gpa_table_of(None) is None
    assert gpa_table_of([]) is None


def test_lookup_uses_lower_bound_of_given_table():
    assert gpa10_to_4(8.99, TABLE) == 3.3
    assert gpa10_to_4(9.0, TABLE) == 4.0
    assert gpa10_to_4(7.5, TABLE) == 2.0
    assert gpa10_to_4(8.0) == 3.5  # không có bảng: WES


def test_only_overall_gpa_scale_10_is_converted():
    assert read_facts(profile({"value": 8.0, "scale": 10}), TODAY, TABLE).gpa4 == 3.3
    assert read_facts(profile({"value": 3.62, "scale": 4}), TODAY, TABLE).gpa4 == 3.62  # thang 4 giữ nguyên
    assert read_facts(profile({"value": 8.0, "scale": 10}), TODAY).gpa4 == 3.5


def test_profile_lines_and_route_use_the_table():
    assert (
        calc_profile(profile({"value": 8.0, "scale": 10}), None, TODAY, gpa_table=TABLE)["gpa_level"]
        == "3.3 thang 4"
    )
    _, facts, _ = choose_route(profile({"value": 8.0, "scale": 10}), TODAY, TABLE)
    assert facts.gpa4 == 3.3


def test_skill_request_reads_table_from_state():
    state = ChatState(profile=profile({"value": 8.0, "scale": 10}), grade_scale=RULES)

    req = skill_request_of(state, "Hồ sơ em thế nào?", TODAY)

    assert req.tinh_san["gpa_level"] == "3.3 thang 4"
