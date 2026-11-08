"""Bước 1.1: khung API /api/v1/chat. Chưa gọi AI, chỉ kiểm khuôn vào/ra và câu chào B0.1."""

import json

from fastapi.testclient import TestClient

from app.api.deps import get_chat_flow
from app.main import create_app
from app.services.chat_flow import QUESTIONS, ChatFlow, SkillOutput
from app.services.slm_contract import FixedSlm
from tests.fakes import tin


class SilentBranch:
    """Nhánh giả không trả lời gì: API chỉ kiểm khuôn vào/ra và câu chào B0.1."""

    async def run(self, state, message, slm):
        return SkillOutput("")


def make_client() -> TestClient:
    # Không dùng "with" nên không chạy lifespan, không cần Ollama, Claude
    app = create_app()
    flow = ChatFlow(FixedSlm(tin("kien_thuc")), {"kien_thuc": SilentBranch()})
    app.dependency_overrides[get_chat_flow] = lambda: flow
    return TestClient(app)


def test_new_chat_starts_with_b0_question():
    # Đúng JSON .NET gửi: camelCase, chưa có state
    res = make_client().post(
        "/api/v1/chat",
        json={"messages": [{"role": "user", "content": "chào"}], "sessionId": None, "studyLevel": None},
    )

    assert res.status_code == 200
    body = res.json()
    assert body["answer"] == QUESTIONS["B0.1"]
    assert body["citations"] == []  # .NET đọc các trường answer, citations, disclaimer
    assert body["state"]["currentStep"] == "B0"  # trả camelCase
    assert body["state"]["lastQuestion"] == QUESTIONS["B0.1"]
    assert body["state"]["asked"] == {"B0.1": 1}
    # trường mới của hợp đồng: có mặt, rỗng, camelCase để .NET đọc được
    assert body["quickReplies"] == []
    assert body["widget"] is None
    assert body["profileChanged"] == []


def test_existing_state_is_kept():
    state = {"currentStep": "B2", "lastQuestion": "Bạn đã thi SAT chưa?", "asked": {"D3": 1}}
    res = make_client().post(
        "/api/v1/chat",
        json={"messages": [{"role": "user", "content": "em được 1420"}], "state": state},
    )

    assert res.status_code == 200
    body = res.json()
    assert body["state"]["currentStep"] == "B2"  # nhánh không đổi bước nên giữ nguyên
    assert body["state"]["asked"] == {"D3": 1}


def test_rejects_empty_messages():
    res = make_client().post("/api/v1/chat", json={"messages": []})

    assert res.status_code == 422


def test_rejects_unknown_step():
    res = make_client().post(
        "/api/v1/chat",
        json={"messages": [{"role": "user", "content": "hi"}], "state": {"currentStep": "B9"}},
    )

    assert res.status_code == 422


# ---- /api/v1/chat/stream: mỗi dòng một JSON ----
def stream_client(flow: ChatFlow) -> TestClient:
    app = create_app()
    app.dependency_overrides[get_chat_flow] = lambda: flow
    return TestClient(app)


def read_lines(res) -> list[dict]:
    return [json.loads(line) for line in res.text.splitlines() if line.strip()]


class Echo:
    async def run(self, state, message, slm):
        return SkillOutput(f"đáp {message}")


class Broken:
    async def run(self, state, message, slm):
        raise RuntimeError("hỏng")


def test_the_stream_sends_progress_each_answer_then_the_same_json_as_the_plain_endpoint():
    slm = tin(("kien_thuc", {}, "I-20 là gì"), ("kien_thuc", {}, "SAT là gì"))
    client = stream_client(ChatFlow(FixedSlm(slm), {"kien_thuc": Echo()}))
    body = {
        "messages": [{"role": "user", "content": "I-20 là gì, SAT là gì"}],
        "state": {"currentStep": "B3"},
    }

    res = client.post("/api/v1/chat/stream", json=body)
    plain = client.post("/api/v1/chat", json=body).json()

    assert res.status_code == 200 and res.headers["content-type"].startswith("application/x-ndjson")
    events = read_lines(res)
    assert [e["type"] for e in events] == ["progress", "partial", "partial", "final"]
    assert events[0]["answers"] == 2
    assert [e["text"] for e in events[1:3]] == ["đáp I-20 là gì", "đáp SAT là gì"]
    assert events[3]["response"] == plain  # dòng cuối đúng JSON camelCase như khi không stream


def test_an_error_while_answering_ends_the_stream_with_an_error_line():
    client = stream_client(ChatFlow(FixedSlm(tin("kien_thuc")), {"kien_thuc": Broken()}))

    res = client.post("/api/v1/chat/stream", json={"messages": [{"role": "user", "content": "x"}]})

    assert res.status_code == 200
    assert [e["type"] for e in read_lines(res)] == ["progress", "error"]


def test_the_stream_rejects_empty_messages_like_the_plain_endpoint():
    res = make_client().post("/api/v1/chat/stream", json={"messages": []})

    assert res.status_code == 422
