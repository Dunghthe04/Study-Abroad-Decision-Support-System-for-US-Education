"""Mở lại phiên chat cũ: dựng lại nút và công cụ của câu hồ sơ đang chờ từ state (tin nhắn lưu chỉ có chữ).

Hàm thuần, không gọi AI: chỉ chạy lại phần code hỏi câu hồ sơ trên bản sao của state.
"""

import pytest
from fastapi.testclient import TestClient

from app.main import create_app
from app.schemas.chat import ChatState, PendingQuestionRequest, ProfileField
from app.services.chat_flow import QUESTIONS, apply_turn_inputs, pending_question


def state_waiting_for(question_id: str | None, gpa: str = "3.0") -> ChatState:
    return ChatState(
        current_step="B2",
        question_id=question_id,
        last_question=QUESTIONS.get(question_id, "") if question_id else "",
        profile={"gpa": ProfileField(value=gpa, status="confirmed")},
    )


def ask(state: ChatState, **inputs):
    return pending_question(state, PendingQuestionRequest(state=state, **inputs))


@pytest.mark.parametrize(
    ("question_id", "widget"),
    [
        ("F1", "budget"),
        ("F2", "scholarship"),
        ("B1.3", "major"),
        ("B1.1", "select"),
        ("D3", "select"),
        ("X1", "review"),
    ],
)
def test_a_waiting_profile_question_gets_its_tool_back(question_id, widget):
    result = ask(state_waiting_for(question_id))

    assert result.pending is True
    assert result.question_id == question_id
    assert result.question == QUESTIONS[question_id]
    assert result.widget is not None and result.widget.type == widget


def test_the_review_table_also_gets_its_confirm_buttons():
    result = ask(state_waiting_for("X1"))

    assert [reply.label for reply in result.quick_replies] == ["Đúng rồi", "Cần sửa"]


@pytest.mark.parametrize("question_id", ["B0.1", "B0.2", "X2", "X3", None])
def test_opening_and_suggestion_questions_are_not_rebuilt(question_id):
    # Nút của các câu này chỉ là gợi ý: học sinh gõ tay là đủ
    result = ask(state_waiting_for(question_id))

    assert result.pending is False
    assert result.widget is None
    assert result.quick_replies == []


def test_the_review_table_shows_the_current_profile_not_the_one_saved_in_the_old_state():
    # .NET gửi hồ sơ mới đọc từ server: học sinh sửa hồ sơ sau khi rời phiên thì bảng phải là hồ sơ mới
    old = state_waiting_for("X1", gpa="3.0")

    result = ask(old, profile={"gpa": ProfileField(value="3.8", status="confirmed")})

    rows = str(result.widget.data)
    assert "3.8" in rows
    assert "3.0" not in rows


def test_rebuilding_never_changes_the_sessions_state():
    old = state_waiting_for("X1", gpa="3.0")
    before = old.model_dump()

    ask(old, profile={"gpa": ProfileField(value="3.8", status="confirmed")})

    assert old.model_dump() == before


def test_the_chat_turn_and_the_rebuild_merge_what_dotnet_sends_the_same_way():
    # Cùng một hàm ghép cho cả hai nơi: lượt chat và dựng lại câu đang chờ thấy cùng một state
    state = state_waiting_for("D3")
    request = PendingQuestionRequest(
        state=state,
        profile={"gpa": ProfileField(value="3.6", status="confirmed")},
        exact_budget=True,
        has_committed_profile=True,
    )
    merged = state.model_copy(deep=True)

    apply_turn_inputs(merged, request)

    assert (merged.profile["gpa"].value, merged.exact_budget, merged.has_committed_profile) == (
        "3.6",
        True,
        True,
    )


# ---- API


def test_the_api_takes_camel_case_and_answers_camel_case_without_returning_the_state():
    client = TestClient(create_app())  # không chạy lifespan: không cần Ollama, Claude

    res = client.post(
        "/api/v1/chat/pending-question",
        json={
            "state": {"currentStep": "B2", "questionId": "F1", "lastQuestion": QUESTIONS["F1"]},
            "profile": {"gpa": {"value": "3.5", "status": "confirmed"}},
        },
    )

    assert res.status_code == 200
    body = res.json()
    assert (body["pending"], body["questionId"], body["widget"]["type"]) == (True, "F1", "budget")
    assert body["quickReplies"]
    assert "state" not in body


def test_the_api_requires_a_state():
    res = TestClient(create_app()).post("/api/v1/chat/pending-question", json={"profile": None})

    assert res.status_code == 422
