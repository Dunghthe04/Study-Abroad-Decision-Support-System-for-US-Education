"""Câu hỏi nối tiếp phải dùng trường được nói gần nhất, không dùng trường còn nằm trong kịch bản cũ.

Lỗi thật 27/11/2026: hỏi "muốn vào được Purdue cần gì", rồi hỏi học phí Drexel, rồi "còn tỷ lệ nhận thì sao". SLM xếp câu
cuối là tìm mức cần đạt nối tiếp (không nêu trường), code lấy trường từ kịch bản cũ nên trả lời về Purdue."""

from app.schemas.chat import ChatMessage, ChatRequest, ChatState
from app.services.chat_flow import SkillOutput
from tests.fakes import flow_with, tin


class Answer:
    async def run(self, state, message, slm=None):
        return SkillOutput("trả lời kiến thức")


GOAL_PURDUE = tin(("scenario", {"mode": "goal_seeking", "school": "Purdue University"}))
# Đúng như model đã trả: nối tiếp, tìm mức cần đạt, không nêu trường
FOLLOW_UP = tin(("scenario", {"mode": "goal_seeking", "factor": ["acceptance_rate"], "follow_up": True}))


def cost_of(school: str):
    return tin(("knowledge", {"tasks": ["school_info"], "schools": [school], "fields": ["cost"]}))


async def talk(*turns):
    state, response = ChatState(), None
    for slm in turns:
        flow = flow_with({"skill_tra_loi": Answer()}, slm)
        response = await flow.reply(
            ChatRequest(messages=[ChatMessage(role="user", content="câu hỏi")], state=state)
        )
        state = response.state
    return response


async def test_a_follow_up_uses_the_school_talked_about_after_the_scenario():
    res = await talk(GOAL_PURDUE, cost_of("Drexel University"), FOLLOW_UP)

    assert res.action == "goal_seek"
    assert res.action_data["school"] == "Drexel University"


async def test_a_follow_up_right_after_the_scenario_keeps_its_school():
    res = await talk(GOAL_PURDUE, FOLLOW_UP)

    assert res.action_data["school"] == "Purdue University"


async def test_after_two_schools_the_follow_up_asks_which_one():
    compare = tin(
        (
            "knowledge",
            {"tasks": ["compare_facts"], "schools": ["Drexel University", "Arizona State University"]},
        )
    )

    res = await talk(GOAL_PURDUE, compare, FOLLOW_UP)

    assert res.action is None
    assert res.widget is not None and res.widget.type == "school"


async def test_a_school_search_leaves_no_current_school_so_the_follow_up_asks():
    search = tin(("knowledge", {"tasks": ["search"], "filters": {"school_type": "public"}}))

    res = await talk(GOAL_PURDUE, search, FOLLOW_UP)

    assert res.action is None
    assert res.widget is not None and res.widget.type == "school"
