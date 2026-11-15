"""Học bạ theo năm: .NET gửi thành mục riêng (transcript) cạnh hồ sơ, chat cất vào state.transcript.

Không nằm trong state.profile nên SLM chọn skill và Qwen đọc tin không thấy; chat chỉ đọc, học bạ chỉ nhập ở form.
"""

from app.schemas.chat import ChatMessage, ChatRequest, ChatState
from tests.fakes import flow_with, tin

TRANSCRIPT = {
    "years": [{"grade": "Lớp 10", "average": 8.67, "subjects": [{"name": "Toán", "average": 8.67}]}]
}
ASK = tin("profile_review")


def request(state: ChatState, profile: dict | None, transcript: dict | None) -> ChatRequest:
    return ChatRequest(
        messages=[ChatMessage(role="user", content="hồ sơ em thế nào")],
        state=state,
        profile=profile,
        transcript=transcript,
    )


def test_request_reads_transcript_in_camel_case():
    req = ChatRequest.model_validate(
        {"messages": [{"role": "user", "content": "hi"}], "transcript": TRANSCRIPT}
    )

    assert req.transcript == TRANSCRIPT


async def test_transcript_goes_to_its_own_place_not_into_the_profile():
    flow = flow_with({}, ASK)

    res = await flow.reply(request(ChatState(current_step="B4"), {}, TRANSCRIPT))

    assert res.state.transcript == TRANSCRIPT
    assert "transcript" not in res.state.profile


async def test_switching_to_overall_gpa_clears_the_old_transcript():
    flow = flow_with({}, ASK)

    res = await flow.reply(request(ChatState(current_step="B4", transcript=TRANSCRIPT), {}, None))

    assert res.state.transcript is None


async def test_turn_without_profile_from_net_keeps_the_transcript():
    flow = flow_with({}, ASK)

    res = await flow.reply(request(ChatState(current_step="B4", transcript=TRANSCRIPT), None, None))

    assert res.state.transcript == TRANSCRIPT
