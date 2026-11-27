"""Học sinh đã có hồ sơ đủ, báo một giá trị mới: hệ thống phải cho xác nhận, không mời điền hồ sơ như người mới.

Lỗi thật 27/11/2026: giá trị vừa kể (chờ xác nhận) bị tính là "còn thiếu" nên hiện câu mời chọn chat hay form, gõ
"Đúng rồi" thì hệ thống không hiểu."""

from app.schemas.chat import ChatMessage, ChatRequest, ChatState, ProfileField
from app.services.intake import INTAKE_ID
from app.services.skill_rules import REQUIRED_B3, missing_fields, unanswered_fields
from app.services.skills import build_handlers
from app.services.skills.skill_ho_so import SAVED
from tests.fakes import FakeLLM, flow_with, tin

PROFILE = {
    "current_level": "high_school",
    "current_grade": "Lớp 11",
    "major": "Khoa học máy tính",
    "gpa": {"value": 8.7, "scale": 10},
    "sat": 1350,
    "english": {"type": "ielts", "value": 6.5},
    "budget": "tier_35k_50k",
    "scholarship": "preferred",
}


def saved_state(**changed) -> ChatState:
    profile = {k: ProfileField(value=v, status="confirmed") for k, v in PROFILE.items()}
    for name, value in changed.items():
        profile[name] = ProfileField(value=value, status="said_in_B0")
    return ChatState(profile=profile, has_committed_profile=True)


NEW_ENGLISH = {"type": "ielts", "value": 7.5}
UPDATE = tin(("collect_profile", {"tasks": ["edit"], "english": "7.5"}))
UPDATE_AND_ADVICE = tin(
    ("collect_profile", {"tasks": ["edit"], "english": "7.5"}),
    ("profile_review", {"tasks": ["roadmap"]}),
)


async def send(state: ChatState, slm, message: str, llm: FakeLLM | None = None):
    flow = flow_with(build_handlers(llm or FakeLLM(), "m"), slm)
    return await flow.reply(
        ChatRequest(
            messages=[ChatMessage(role="user", content=message)],
            state=state,
            has_committed_profile=True,
        )
    )


def test_a_value_waiting_for_confirmation_is_not_unanswered():
    state = saved_state(english=NEW_ENGLISH)

    assert missing_fields(state.profile, REQUIRED_B3) == ["english"]  # vẫn chưa được coi là đã xác nhận
    assert unanswered_fields(state.profile, REQUIRED_B3) == []


async def test_advice_after_an_update_shows_the_review_board_and_keeps_the_question():
    res = await send(
        saved_state(english=NEW_ENGLISH), tin(("profile_review", {"tasks": ["roadmap"]})), "lộ trình"
    )

    assert res.state.question_id == "X1"
    assert res.widget is not None and res.widget.type == "review"
    assert res.state.pending_advice is not None


async def test_confirming_the_update_saves_it_and_answers_the_held_question():
    first = await send(
        saved_state(english=NEW_ENGLISH), tin(("profile_review", {"tasks": ["roadmap"]})), "lộ trình"
    )

    res = await send(first.state, tin("collect_profile"), "Đúng rồi")

    assert res.state.profile["english"].status == "confirmed"
    assert res.state.pending_advice is None
    assert SAVED in res.answer and "lời tư vấn" in res.answer


async def test_a_student_without_a_saved_profile_is_still_invited_to_fill_it():
    state = ChatState(
        profile={"english": ProfileField(value=NEW_ENGLISH, status="said_in_B0")},
        has_committed_profile=False,
    )
    flow = flow_with(build_handlers(FakeLLM(), "m"), tin(("profile_review", {"tasks": ["roadmap"]})))

    res = await flow.reply(
        ChatRequest(
            messages=[ChatMessage(role="user", content="lộ trình")], state=state, has_committed_profile=False
        )
    )

    assert res.state.question_id == INTAKE_ID


async def test_an_update_and_a_question_in_one_message_ask_for_confirmation_only_once():
    # Cả phần cập nhật hồ sơ lẫn phần tư vấn đều cần xác nhận: câu xác nhận chỉ hiện một lần
    read = {"values": {"english": {"type": "ielts", "score": 7.5, "date": "2026-10"}}, "none_fields": []}
    llm = FakeLLM(read, read, read)

    res = await send(saved_state(), UPDATE_AND_ADVICE, "em mới thi IELTS 7.5, lộ trình đổi sao", llm)

    assert res.state.question_id == "X1"
    assert res.answer.count(res.state.last_question) == 1
    assert res.state.pending_advice is not None
