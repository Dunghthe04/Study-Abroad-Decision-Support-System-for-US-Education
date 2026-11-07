"""Ưu tiên nói trong chat: nhận ra yếu tố và mức, không nhầm với thông tin hồ sơ hay câu hỏi kiến thức."""

import pytest

from app.schemas.chat import ChatState
from app.schemas.slm import SlmTinResult, SlmY
from app.services.chat_flow import with_priority_intent
from app.services.priority import detect_priorities


@pytest.mark.parametrize(
    ("message", "expected"),
    [
        ("em ưu tiên học bổng hơn", {"aid": 3}),
        ("Ưu tiên chi phí thấp nhất", {"cost": 4}),
        ("em rất quan tâm danh tiếng của trường", {"prestige": 4}),
        ("không quan tâm danh tiếng", {"prestige": 0}),
        ("em ít quan tâm chi phí", {"cost": 1}),
        ("em uu tien hoc bong", {"aid": 3}),  # gõ không dấu
        ("ưu tiên học bổng nhất, chi phí thì ít quan tâm", {"aid": 4, "cost": 1}),
        ("quan tâm học bổng và danh tiếng", {"aid": 3, "prestige": 3}),
        ("tôi đã thi SAT 1400", {}),  # "tôi đã" không phải mức tối đa
    ],
)
def test_detects_the_factor_and_level(message, expected):
    assert detect_priorities(message) == expected


@pytest.mark.parametrize(
    "message",
    [
        "học bổng là gì",
        "em cần học bổng 50%",  # thông tin hồ sơ, không có từ chỉ mức quan tâm
        "trường nào chi phí 30000 USD",
        "ưu tiên bang California",  # có mức nhưng không có yếu tố ưu tiên
    ],
)
def test_ignores_profile_values_and_knowledge_questions(message):
    assert detect_priorities(message) == {}


def mac_dinh(message: str) -> SlmTinResult:
    return SlmTinResult(cac_y=[SlmY(type="mac_dinh", doan=message)])


def test_priority_message_becomes_a_what_if_when_results_exist():
    state = ChatState(has_results=True)
    slm = with_priority_intent(state, "em ưu tiên học bổng", mac_dinh("em ưu tiên học bổng"))

    assert slm.types() == ["kich_ban"]
    assert slm.cac_y[0].thuc_the["che_do"] == "what_if"


def test_priority_is_left_alone_while_a_profile_question_waits():
    message = "em ưu tiên học bổng"
    waiting = ChatState(has_results=True, question_id="B3.4")
    assert with_priority_intent(waiting, message, mac_dinh(message)).types() == ["mac_dinh"]


def test_the_opening_greeting_question_does_not_block_it():
    # Lỗi thật: chat mới mở còn đang ở câu chào B0.1, "Em ưu tiên học bổng nhất" bị SLM xếp mac_dinh và trả "hồ sơ đã đủ"
    message = "Em ưu tiên học bổng nhất"
    greeting = ChatState(question_id="B0.1")
    assert with_priority_intent(greeting, message, mac_dinh(message)).types() == ["kich_ban"]


def test_without_results_the_scenario_branch_asks_for_recommendations_first():
    message = "em ưu tiên học bổng"
    assert with_priority_intent(ChatState(), message, mac_dinh(message)).types() == ["kich_ban"]


def test_keeps_other_intents_when_the_message_also_has_numbers():
    state = ChatState(has_results=True)
    message = "SAT 1500 và ưu tiên học bổng"
    slm = with_priority_intent(state, message, mac_dinh(message))

    assert slm.types() == ["kich_ban", "mac_dinh"]
