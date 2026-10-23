"""Bước 1.1: khung API /api/v1/chat. Chưa gọi AI, chỉ kiểm khuôn vào/ra và câu chào B0.1."""

from fastapi.testclient import TestClient

from app.api.deps import get_chat_flow
from app.main import create_app
from app.services.chat_flow import QUESTIONS, ChatFlow


def make_client() -> TestClient:
    # Không dùng "with" nên không chạy lifespan, không cần Ollama, Gemini
    app = create_app()
    app.dependency_overrides[get_chat_flow] = ChatFlow
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


def test_existing_state_is_kept():
    state = {"currentStep": "B2", "lastQuestion": "Bạn đã thi SAT chưa?", "asked": {"D3": 1}}
    res = make_client().post(
        "/api/v1/chat",
        json={"messages": [{"role": "user", "content": "em được 1420"}], "state": state},
    )

    assert res.status_code == 200
    body = res.json()
    assert body["state"]["currentStep"] == "B2"  # bước 1.1 chưa có skill nên không đổi bước
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
