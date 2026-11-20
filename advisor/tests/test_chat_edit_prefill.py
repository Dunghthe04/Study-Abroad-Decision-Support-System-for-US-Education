from app.schemas.chat import ChatState
from app.services.skills import profile_handler, skill_ho_so
from tests.test_chat_skills_flow import field

# Sửa hồ sơ trong chat: câu hỏi lại kèm giá trị cũ ("Hiện tại") và điền sẵn ô chọn, vẫn có nút "Giữ như cũ"


def editing(**profile) -> ChatState:
    return ChatState(current_step="B2", question_id="X1", profile=profile, edit_queue=[])


def keep_shown(out) -> bool:
    return skill_ho_so.KEEP_REPLY.label in [q.label for q in out.quick_replies]


def test_select_question_gets_current_and_initial():
    state = editing(current_level=field("high_school"), current_grade=field("Lớp 11"))
    out = profile_handler.ask_edit_question(state, "B1.1")

    assert keep_shown(out)
    assert out.widget.data["current"]["current_level"] == {"value": "high_school", "status": "confirmed"}
    assert out.widget.data["initial"] == {"level": "Trung học phổ thông", "grade": "Lớp 11"}


def test_intake_gpa_and_plan_prefill():
    state = editing(
        intake=field("Fall 2027"),
        gpa=field({"value": 8.5, "scale": 10}),
        english_plan=field({"planned": True, "test_type": "ielts", "target_score": 7.0, "date": "2026-09"}),
    )

    assert profile_handler.ask_edit_question(state, "B1.4").widget.data["initial"] == {"term": "Fall", "year": "2027"}
    assert profile_handler.ask_edit_question(state, "D2").widget.data["initial"] == {"gpa": "8.5", "scale": "10"}
    assert profile_handler.ask_edit_question(state, "E2").widget.data["initial"] == {
        "type": "IELTS Academic", "score": "7", "year": "2026", "month": "9"
    }


def test_scholarship_card_marked():
    out = profile_handler.ask_edit_question(editing(scholarship=field("preferred")), "F2")
    assert out.widget.data["initial"] == {"need": "preferred"}


def test_list_question_only_shows_current():
    out = profile_handler.ask_edit_question(editing(activities=field([{"name": "CLB Robotics"}])), "D4")
    assert "current" in out.widget.data
    assert "initial" not in out.widget.data


def test_button_question_writes_old_value_in_text():
    out = profile_handler.ask_edit_question(editing(target_level=field("master")), "B1.2")
    assert out.answer.endswith("Hiện tại: Thạc sĩ.")
    assert keep_shown(out)


def test_not_editing_has_no_current():
    state = ChatState(current_step="B1", question_id="B1.1", profile={"current_level": field("high_school")})
    out = profile_handler.ask_edit_question(state, "B1.1")
    assert "current" not in out.widget.data
    assert not keep_shown(out)
