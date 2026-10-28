"""Việc 2.3: bắt đầu hỏi hồ sơ. Các hàm thuần nên không cần AI hay DB."""

from app.schemas.chat import ProfileField
from app.services.chat_flow import QUESTIONS
from app.services.intake import (
    CHAT_BUTTON,
    FIELD_QUESTION,
    FORM_BUTTON,
    has_profile,
    intake_choice,
    next_question,
)
from app.services.skill_rules import REQUIRED_B4


def profile(*keys: str) -> dict[str, ProfileField]:
    return {k: ProfileField(value="x", status="confirmed") for k in keys}


def test_buttons_send_text_that_intake_choice_recognises():
    assert intake_choice(CHAT_BUTTON.value) == "chat"
    assert intake_choice(FORM_BUTTON.value) == "form"


def test_short_free_text_with_form_or_chat_also_counts():
    assert intake_choice("em điền form") == "form"
    assert intake_choice("Em muốn trả lời trong chat") == "chat"
    assert intake_choice("  FORM  ") == "form"


def test_anything_else_is_not_a_choice():
    assert intake_choice("I-20 là gì ạ?") is None
    assert intake_choice("") is None
    long_text = (
        "em muốn hỏi về học phí và việc làm thêm khi học ở Mỹ, đồng thời xem chat với tư vấn viên " * 2
    )
    assert intake_choice(long_text) is None  # câu dài không coi là chọn nút


def test_only_keys_the_form_manages_count_as_a_profile():
    assert not has_profile({})
    assert not has_profile(profile("funding_source", "education_system"))  # chỉ chat nhớ, form không có
    assert has_profile(profile("gpa"))


def test_next_question_asks_what_b3_needs_first_in_asking_order():
    assert next_question({}) == ("B1", "B1.2")  # bậc học trước
    assert next_question(profile("target_level")) == ("B2", "D2")
    assert next_question(profile("target_level", "gpa")) == ("B2", "E1")
    assert next_question(profile("target_level", "gpa", "english")) == ("B2", "F1")


def test_next_question_moves_to_b4_fields_once_b3_is_complete():
    b3 = ("target_level", "gpa", "english", "budget")

    assert next_question(profile(*b3)) == ("B1", "B1.3")  # ngành
    assert next_question(profile(*b3, "major")) == ("B2", "F2")  # học bổng
    assert next_question(profile(*b3, "major", "scholarship")) is None


def test_every_required_field_has_a_question_that_exists():
    assert set(FIELD_QUESTION) == set(REQUIRED_B4)
    for step, question_id in FIELD_QUESTION.values():
        assert question_id in QUESTIONS
        assert step in {"B1", "B2"}
