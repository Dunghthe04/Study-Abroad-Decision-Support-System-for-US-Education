"""Học sinh muốn thử kịch bản nhưng chưa nói đổi gì: hợp đồng nhận what_if không có changes, hệ thống hỏi muốn đổi gì.

Trước 27/11/2026 hợp đồng bắt buộc what_if phải có changes, nên đầu ra đúng ý của SLM bị loại và học sinh nhận "chưa hiểu"."""

import pytest

from app.schemas.chat import ChatContext, ChatMessage, ChatRequest, ChatState
from app.schemas.slm import SlmTinResult
from app.services.replies import TRY_WHAT
from tests.fakes import flow_with, tin

PREVIOUS = {"mode": "what_if", "changes": [{"factor": "budget", "value": "25000", "unit": "USD"}]}


def raw(entities: dict) -> dict:
    return {"intents": [{"type": "scenario", "span": "thử xíu coi sao", "entities": entities}]}


@pytest.mark.parametrize("entities", [{"mode": "what_if"}, {"mode": "what_if", "changes": []}])
def test_the_contract_accepts_a_what_if_with_no_change_named(entities):
    assert SlmTinResult.from_raw(raw(entities)).intents[0].entities["mode"] == "what_if"


@pytest.mark.parametrize("changes", [[{"factor": "sat"}], [{"value": "1400"}]])
def test_the_contract_still_rejects_a_half_written_change(changes):
    with pytest.raises(ValueError):
        SlmTinResult.from_raw(raw({"mode": "what_if", "changes": changes}))


def test_the_contract_still_requires_a_mode():
    with pytest.raises(ValueError):
        SlmTinResult.from_raw(raw({}))


async def send(state: ChatState, entities: dict):
    flow = flow_with({}, tin(("scenario", entities)))
    return await flow.reply(ChatRequest(messages=[ChatMessage(role="user", content="x")], state=state))


@pytest.mark.parametrize("follow_up", [None, True])
async def test_it_asks_what_to_change_and_does_not_rerun_the_previous_scenario(follow_up):
    state = ChatState(has_results=True, context=ChatContext(scenario=PREVIOUS))
    entities = {"mode": "what_if"} | ({"follow_up": True} if follow_up else {})

    res = await send(state, entities)

    assert res.action is None
    assert res.answer.startswith(TRY_WHAT.split("{")[0])
    assert res.quick_replies
    assert res.state.context.scenario == PREVIOUS  # câu sau còn nói tiếp kịch bản cũ được


async def test_without_a_school_list_it_asks_for_the_list_first():
    res = await send(ChatState(), {"mode": "what_if"})

    assert res.action is None and "danh sách" in res.answer
