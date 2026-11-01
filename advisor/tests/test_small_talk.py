"""Chào, cảm ơn, tạm biệt: model xếp nhầm vào ngoài phạm vi thì vẫn được đáp đúng chỗ (OffTopicHandler)."""

from dataclasses import dataclass

import pytest

from app.schemas.chat import ChatMessage, ChatRequest, ChatState
from app.services.chat_flow import ASK_MORE, ASK_MORE_WITH_RESULTS, GOODBYE, GREETING, THANKS, ChatFlow
from app.services.small_talk import classify


@pytest.mark.parametrize(
    ("message", "kind"),
    [
        ("chào bạn", "greeting"),
        ("Xin chào!", "greeting"),
        ("hello", "greeting"),
        ("chao ban", "greeting"),
        ("cảm ơn bạn", "thanks"),
        ("em cảm ơn ạ", "thanks"),
        ("thanks nhé", "thanks"),
        ("cảm ơn bạn, tạm biệt", "goodbye"),
        ("tạm biệt", "goodbye"),
        ("bye", "goodbye"),
        ("hẹn gặp lại", "goodbye"),
    ],
)
def test_short_social_messages_are_recognised(message, kind):
    assert classify(message) == kind


@pytest.mark.parametrize(
    "message",
    [
        "chào bạn, IELTS là gì",  # có câu hỏi đi kèm
        "cảm ơn, vậy còn học bổng thì sao",
        "bạn tên gì",
        "hôm nay trời đẹp nhỉ",
        "ok",
        "???",
        "",
        "tạm biệt bạn nhưng trước đó cho em hỏi visa F-1 là gì",  # dài và có nội dung
    ],
)
def test_messages_with_content_or_off_topic_are_not_small_talk(message):
    assert classify(message) is None


@dataclass
class FakePrediction:
    skills: list[str]
    probs: dict[str, float]


class PickerOf:
    def __init__(self, *skills: str) -> None:
        self._skills = list(skills)

    async def predict(self, step, last_question, message, missing):
        return FakePrediction(self._skills, {s: 0.9 for s in self._skills})


async def reply(message: str, state: ChatState):
    # Model luôn đoán ngoai_le, như lúc nó xếp nhầm "chào bạn"
    req = ChatRequest(messages=[ChatMessage(role="user", content=message)], state=state)
    return await ChatFlow(PickerOf("ngoai_le"), {}).reply(req)


async def test_a_greeting_mid_chat_is_answered_and_the_pending_question_is_repeated():
    state = ChatState(current_step="B2", question_id="D2", last_question="Điểm GPA của bạn là bao nhiêu?")

    res = await reply("chào bạn", state)

    assert res.answer == f"{GREETING} Điểm GPA của bạn là bao nhiêu?"
    assert res.state.current_step == "B2" and res.state.question_id == "D2"


async def test_thanks_after_results_invites_the_next_question():
    res = await reply("cảm ơn bạn", ChatState(current_step="B5", has_results=True))

    assert res.answer == f"{THANKS} {ASK_MORE_WITH_RESULTS}"


async def test_a_greeting_with_nothing_pending_asks_what_the_student_needs():
    res = await reply("chào bạn", ChatState(current_step="B5"))

    assert res.answer == f"{GREETING} {ASK_MORE}"


async def test_a_goodbye_the_model_missed_still_ends_the_conversation():
    res = await reply("cảm ơn bạn, tạm biệt", ChatState(current_step="B6"))

    assert res.answer == GOODBYE and res.state.current_step == "END"


async def test_real_off_topic_still_gets_the_refusal():
    res = await reply("hôm nay thời tiết ở Mỹ thế nào", ChatState(current_step="B5"))

    assert res.answer.startswith("Mình chỉ hỗ trợ tư vấn du học Mỹ thôi nhé.")
