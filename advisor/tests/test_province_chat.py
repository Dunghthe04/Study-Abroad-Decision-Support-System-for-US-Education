"""C4. Chat hỏi tỉnh / thành sau câu trả lời lộ trình (roadmap.provinceMissing), lượt sau trả action set_province
để .NET kiểm tra và lưu; Bỏ qua thì không hỏi lại trong phiên."""

import pytest

from app.schemas.chat import ChatMessage, ChatRequest, ChatState, ProfileField
from app.services.chat_flow import SkillOutput, is_province_reply
from app.services.replies import BACK_TO_QUESTION
from app.services.skills import build_handlers
from app.services.skills.skill_tinh import (
    PROVINCE_ID,
    PROVINCE_QUESTION,
    ROADMAP_BUTTON,
    SET_PROVINCE,
    SKIPPED,
    answer_province,
    ask_province,
    province_text,
    with_province_question,
)
from tests.fakes import flow_with, tin
from tests.test_province_reply import catalog  # noqa: F401 - backend giả: danh sách và tên gọi khác của tỉnh

pytestmark = pytest.mark.usefixtures("catalog")

MISSING = {"mode": "average", "schools": [], "tasks": [], "provinceMissing": True}


def state_with(roadmap=MISSING, **kwargs) -> ChatState:
    profile = {
        "gpa": ProfileField(value={"value": 8.5, "scale": 10}, status="confirmed"),
        "budget": ProfileField(value="tier_above_70k", status="confirmed"),
    }
    return ChatState(profile=profile, roadmap=roadmap, **kwargs)


def labels(output: SkillOutput) -> list[str]:
    return [r.label for r in output.quick_replies]


# ---- đọc chữ học sinh gõ ----


def test_province_text_strips_place_words():
    assert province_text("em ở Hà Nội") == "Hà Nội"
    assert province_text("mình sống tại TP. Hồ Chí Minh.") == "TP. Hồ Chí Minh"
    assert province_text("Đà Nẵng") == "Đà Nẵng"


def test_province_text_rejects_questions_and_long_sentences():
    assert province_text("trung tâm nào dạy IELTS tốt?") is None
    assert province_text("cho em hỏi học phí các trường bên California thì khoảng bao nhiêu") is None
    assert province_text("   ") is None


# ---- hỏi sau câu trả lời lộ trình ----


def test_asks_once_when_province_missing():
    state = state_with()
    out = with_province_question(
        state, SkillOutput("lời tư vấn", quick_replies=[ROADMAP_BUTTON.model_copy()])
    )
    assert out.answer == f"lời tư vấn\n\n{PROVINCE_QUESTION}"
    assert labels(out) == ["Xem lộ trình", "Bỏ qua"]
    assert state.question_id == PROVINCE_ID

    state.question_id = None  # học sinh đã trả lời / bỏ qua
    again = with_province_question(state, SkillOutput("lời tư vấn"))
    assert again.answer == "lời tư vấn"


def test_does_not_ask_when_province_known_or_other_question_pending():
    known = state_with({**MISSING, "provinceMissing": False})
    assert with_province_question(known, SkillOutput("a")).answer == "a"
    busy = state_with(question_id="D2", last_question="GPA?")
    assert with_province_question(busy, SkillOutput("a")).answer == "a"
    assert busy.question_id == "D2"


# ---- lượt sau: trả lời câu tỉnh ----


async def test_answer_gives_set_province_action():
    state = state_with()
    ask_province(state)
    out = await answer_province(state, "em ở Hà Nội")
    assert out.action == SET_PROVINCE
    assert out.action_data == {"province": "Hà Nội"}
    assert labels(out) == ["Xem lộ trình"]
    assert state.question_id is None


async def test_skip_clears_question_without_action():
    state = state_with()
    ask_province(state)
    out = await answer_province(state, "Bỏ qua")
    assert out.answer == SKIPPED and out.action is None
    assert state.question_id is None
    assert with_province_question(state, SkillOutput("a")).answer == "a"  # không hỏi lại trong phiên


async def test_province_answer_skips_slm():
    waiting = state_with(question_id=PROVINCE_ID)
    assert await is_province_reply(waiting, "Hà Nội")
    assert await is_province_reply(waiting, "Bỏ qua")
    assert not await is_province_reply(waiting, "trường nào ở Mỹ có học bổng ngành khoa học máy tính?")
    assert not await is_province_reply(state_with(question_id="D2"), "Hà Nội")


# ---- cả lượt chat ----


class Knowledge:
    async def run(self, state, message, entities=None):
        return SkillOutput("câu trả lời kiến thức")


def handlers() -> dict:
    return {**build_handlers(llm=object(), model="m"), "skill_tra_loi": Knowledge()}


def request(state: ChatState, text: str) -> ChatRequest:
    return ChatRequest(messages=[ChatMessage(role="user", content=text)], state=state)


async def test_turn_with_province_name_returns_action():
    state = state_with()
    ask_province(state)
    flow = flow_with(handlers(), tin("knowledge"))  # SLM không được gọi: tên tỉnh đi thẳng vào nhánh hồ sơ

    res = await flow.reply(request(state, "Hà Nội"))

    assert res.action == SET_PROVINCE
    assert res.action_data == {"province": "Hà Nội"}
    assert res.state.question_id is None


async def test_off_topic_question_reminds_province_with_skip():
    state = state_with()
    ask_province(state)
    flow = flow_with(handlers(), tin("knowledge"))

    res = await flow.reply(request(state, "visa F1 cần giấy tờ gì vậy ạ?"))

    assert res.answer == "câu trả lời kiến thức\n\n" + BACK_TO_QUESTION + PROVINCE_QUESTION
    assert [r.label for r in res.quick_replies] == ["Bỏ qua"]
    assert res.state.question_id == PROVINCE_ID
    assert res.state.asked[PROVINCE_ID] == 1  # nhắc lại không tính là hỏi thêm
