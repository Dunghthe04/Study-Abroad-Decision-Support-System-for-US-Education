"""Luật hồ sơ: mục nào còn thiếu thì chưa đủ để tư vấn. Hàm thuần nên không cần AI, DB hay Ollama."""

from app.schemas.chat import ChatState, ProfileField
from app.services.skill_rules import REQUIRED_B3, missing_fields


def state(step: str = "B2", **fields: str) -> ChatState:
    """Dựng trạng thái: fields là {tên thông tin: trạng thái}, ví dụ gpa="confirmed"."""
    return ChatState(
        current_step=step, profile={k: ProfileField(value="x", status=v) for k, v in fields.items()}
    )


B3_PROFILE = dict(
    target_level="confirmed",
    gpa="confirmed",
    english="confirmed",
    sat="confirmed",
    budget="confirmed",
    scholarship="confirmed",
)
FULL_PROFILE = dict(B3_PROFILE, major="confirmed")  # đủ cho cả B4


def test_assessment_needs_the_b3_items_in_asking_order_without_the_major():
    profile = state("B3", gpa="confirmed").profile

    assert missing_fields(profile, REQUIRED_B3) == [
        "target_level",
        "english",
        "sat",
        "budget",
        "scholarship",
    ]  # đúng thứ tự hỏi, chưa đòi ngành


def test_assessment_profile_is_complete_without_the_major():
    assert missing_fields(state("B3", **B3_PROFILE).profile, REQUIRED_B3) == []


# Đánh giá hồ sơ cũng cần học bổng và câu SAT/ACT
def test_assessment_is_missing_scholarship_and_sat_when_they_are_not_answered():
    profile = {k: v for k, v in B3_PROFILE.items() if k not in ("sat", "scholarship")}

    assert missing_fields(state("B3", **profile).profile, REQUIRED_B3) == ["sat", "scholarship"]


# Câu SAT/ACT: có ACT, bấm "Em chưa thi" (none) hoặc có chứng chỉ khác không phải tiếng Anh đều tính là đã trả lời
def test_sat_question_is_answered_by_act_none_or_other_standardized_cert():
    base = {k: v for k, v in FULL_PROFILE.items() if k != "sat"}
    assert missing_fields(state(**base).profile) == ["sat"]
    assert missing_fields(state(**base, act="confirmed").profile) == []
    assert missing_fields(state(**base, sat="none").profile) == []

    with_ap = state(**base).profile
    with_ap["certifications"] = ProfileField(
        value=[{"category": "standardized", "type": "ap"}], status="confirmed"
    )
    assert missing_fields(with_ap) == []

    english_only = state(**base).profile
    english_only["certifications"] = ProfileField(
        value=[{"category": "english", "type": "ielts"}], status="confirmed"
    )
    assert missing_fields(english_only) == ["sat"]


def test_unknown_major_does_not_block_the_assessment_but_blocks_the_school_list():
    profile = dict(B3_PROFILE, major="unknown")  # chưa rõ ngành vẫn đánh giá được hồ sơ

    assert missing_fields(state("B3", **profile).profile, REQUIRED_B3) == []
    assert missing_fields(state("B4", **profile).profile) == ["major"]  # gợi ý trường cần ngành thật


def test_roadmap_asks_profile_items_before_the_major():
    profile = dict(FULL_PROFILE, gpa="unknown", major="unknown")  # GPA đứng trước ngành trong thứ tự hỏi

    assert missing_fields(state("B4", **profile).profile) == ["gpa", "major"]


def test_none_and_unknown_count_as_answered_but_not_for_must_have_fields():
    profile = dict(FULL_PROFILE, english="none", scholarship="unknown")
    assert missing_fields(state(**profile).profile) == []

    profile = dict(FULL_PROFILE, gpa="unknown")  # GPA không được bỏ qua
    assert missing_fields(state(**profile).profile) == ["gpa"]

    profile = dict(FULL_PROFILE, budget="said_in_B0")  # tự nói chưa xác nhận
    assert missing_fields(state(**profile).profile) == ["budget"]

    profile = dict(FULL_PROFILE, target_level="unknown")  # bậc học không được bỏ qua
    assert missing_fields(state(**profile).profile) == ["target_level"]

    profile = dict(FULL_PROFILE, major="unknown")  # "chưa rõ" ngành: B4 vẫn thiếu
    assert missing_fields(state(**profile).profile) == ["major"]


def test_missing_fields_can_be_counted_for_the_b3_milestone_only():
    profile = dict(B3_PROFILE, major="unknown")

    assert missing_fields(state(**profile).profile, REQUIRED_B3) == []
    assert missing_fields(state(**profile).profile) == ["major"]  # mặc định tính theo B4
