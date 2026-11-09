"""Mốc GPA / tiếng Anh từ dữ liệu trường: .NET tính và gửi mỗi lượt, chat cất vào state.benchmarks."""

from app.schemas.chat import ChatMessage, ChatRequest, ChatState
from tests.fakes import flow_with, tin

MARKS = {"gpa4": 3.6, "english": {"ielts": 6.5, "toefl": 80}}
ASK = tin(("ho_so", {}))


def request(state: ChatState, benchmarks: dict | None) -> ChatRequest:
    return ChatRequest(
        messages=[ChatMessage(role="user", content="hồ sơ em thế nào")], state=state, benchmarks=benchmarks
    )


def test_request_reads_benchmarks():
    req = ChatRequest.model_validate({"messages": [{"role": "user", "content": "hi"}], "benchmarks": MARKS})

    assert req.benchmarks == MARKS


async def test_benchmarks_are_kept_in_state():
    res = await flow_with({}, ASK).reply(request(ChatState(current_step="B4"), MARKS))

    assert res.state.benchmarks == MARKS
    assert "benchmarks" not in res.state.profile


async def test_turn_without_benchmarks_keeps_the_old_ones():
    res = await flow_with({}, ASK).reply(request(ChatState(current_step="B4", benchmarks=MARKS), None))

    assert res.state.benchmarks == MARKS
