"""SLM tách được collect_profile với nhiệm vụ status ("hồ sơ em đang lưu gì") và restart ("làm lại từ đầu").
Trước đây không nơi nào xử lý nên cả hai nhận câu "Hồ sơ của bạn đã đầy đủ rồi"."""

from app.schemas.chat import ChatMessage, ChatRequest, ChatState, ProfileField
from app.services.chat_flow import QUESTIONS
from app.services.intake import INTAKE_ID, intake_ask
from app.services.replies import PROFILE_DONE, PROFILE_NONE_YET, PROFILE_RESTART_INTRO, PROFILE_STATUS
from app.services.skill_rules import REQUIRED_B3
from app.services.skills import build_handlers
from tests.fakes import FakeLLM, flow_with, tin
from tests.test_chat_no_unsolicited_profile import state

STATUS = tin(("collect_profile", {"tasks": ["status"]}))
RESTART = tin(("collect_profile", {"tasks": ["restart"]}))


async def reply(chat_state: ChatState, slm, message: str = "x", llm: FakeLLM | None = None):
    flow = flow_with(build_handlers(llm or FakeLLM(), "m"), slm)
    return await flow.reply(
        ChatRequest(messages=[ChatMessage(role="user", content=message)], state=chat_state)
    )


async def test_asking_what_is_saved_shows_the_profile_without_changing_it():
    before = state()
    res = await reply(before, STATUS, "hồ sơ em đang lưu những gì")

    assert res.answer == PROFILE_STATUS
    assert res.widget.type == "review"
    assert res.widget.data["profile"]["gpa"]["value"] == {"value": 8.7, "scale": 10}
    assert res.state.profile == before.profile
    assert res.state.question_id == "X1"  # nút "Cần sửa" của bảng dùng được ngay


async def test_asking_what_is_saved_with_an_empty_profile_says_so_and_offers_to_fill_it():
    res = await reply(ChatState(), STATUS)

    assert res.answer.startswith(PROFILE_NONE_YET)
    assert res.state.question_id == INTAKE_ID and res.quick_replies


async def test_restart_asks_the_questions_again_and_keeps_the_saved_values():
    before = state()
    res = await reply(before, RESTART, "em muốn làm lại hồ sơ từ đầu")

    assert res.answer.startswith(PROFILE_RESTART_INTRO) and PROFILE_DONE not in res.answer
    assert res.state.edit_queue is not None
    assert res.state.question_id not in (None, "X1")
    assert res.state.profile == before.profile  # chưa trả lời gì thì không mất dữ liệu


async def test_restart_with_an_empty_profile_just_starts_filling_it():
    res = await reply(ChatState(), RESTART)

    assert res.state.question_id == INTAKE_ID


async def test_status_in_the_middle_of_a_question_does_not_drop_that_question():
    pending = {
        "current_step": "B2",
        "question_id": "F1",
        "last_question": QUESTIONS["F1"],
        "intake_mode": "chat",
    }
    profile = {"current_level": ProfileField(value="high_school", status="confirmed")}

    # Đang hỏi dở thì đi luồng hỏi như cũ: Claude giả đọc tin không thấy giá trị nào
    nothing = {"values": {}, "none_fields": []}
    res = await reply(ChatState(profile=profile, **pending), STATUS, llm=FakeLLM(nothing, nothing))

    assert res.state.question_id == "F1"


async def test_the_forgot_the_exam_date_button_is_handled_as_a_button_not_sent_to_the_classifier():
    # Lỗi thật 27/11/2026: bấm "Không nhớ ngày thi" của chính hệ thống thì nhận "Mình chưa hiểu rõ ý bạn"
    # vì chữ của nút bị gửi sang SLM
    pending = ProfileField(
        value={"type": "sat", "score": 1350.0, "category": "standardized", "cert_name": "SAT"},
        status="unknown",
    )
    start = ChatState(
        current_step="B2",
        question_id="D3",
        last_question=QUESTIONS["D3"],
        intake_mode="chat",
        profile={"cert_pending": pending},
    )
    # SLM giả trả ngoài phạm vi: nếu tin bị gửi sang SLM thì test hỏng
    res = await reply(start, tin(("out_of_scope", {"subtype": "ambiguous"})), "Không nhớ")

    assert "chưa hiểu" not in res.answer
    assert res.state.profile["sat"].value == 1350
    assert "cert_pending" not in res.state.profile


def test_values_the_student_just_told_are_not_counted_as_missing():
    # Lỗi thật 27/11/2026: học sinh vừa nói GPA và IELTS mà vẫn được báo "còn thiếu 5 mục", trong khi lúc hỏi tiếp
    # hai mục đó không bị hỏi lại
    told = {
        name: ProfileField(value=value, status="said_in_B0")
        for name, value in {
            "gpa": {"value": 3.2, "scale": 4},
            "english": {"type": "ielts", "value": 7.0},
        }.items()
    }

    assert "còn thiếu 3 mục" in intake_ask(told, REQUIRED_B3)
    assert "còn thiếu 5 mục" in intake_ask(
        {"major": ProfileField(value="x", status="confirmed")}, REQUIRED_B3
    )
