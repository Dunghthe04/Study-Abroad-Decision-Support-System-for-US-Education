"""Ưu tiên nói trong chat: nhận ra yếu tố và mức, không nhầm với thông tin hồ sơ hay câu hỏi kiến thức."""

import pytest

from app.schemas.chat import ChatState
from app.schemas.slm import SlmTinResult, SlmY
from app.services.chat_flow import with_info_intent, with_priority_intent
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


def goal_seeking(message: str, school: str | None = None) -> SlmTinResult:
    thuc_the = {"che_do": "goal_seeking", "muc_tieu": "Chi phí thấp"}
    if school:
        thuc_the["truong"] = school
    return SlmTinResult(cac_y=[SlmY(type="kich_ban", doan=message, thuc_the=thuc_the)])


def test_a_priority_the_slm_took_for_goal_seeking_without_a_school_becomes_a_what_if():
    # Lỗi thật khi thử với usas-slm-v2: "ưu tiên chi phí thấp" bị xếp goal_seeking, hệ thống hỏi "bạn muốn vào trường nào"
    message = "ưu tiên chi phí thấp"
    slm = with_priority_intent(ChatState(has_results=True), message, goal_seeking(message))

    assert slm.types() == ["kich_ban"]
    assert slm.cac_y[0].thuc_the["che_do"] == "what_if"


def test_goal_seeking_with_a_real_school_is_left_alone():
    message = "em ưu tiên học bổng, cần gì để vào Harvard"
    slm = with_priority_intent(ChatState(has_results=True), message, goal_seeking(message, "Harvard"))

    assert slm.cac_y[0].thuc_the["che_do"] == "goal_seeking"


def out_of_scope(kind: str) -> SlmTinResult:
    return SlmTinResult(cac_y=[SlmY(type="ngoai_pham_vi", doan="x", thuc_the={"loai_con": kind})])


@pytest.mark.parametrize(
    "message",
    [
        "GPA của The University of Texas at Austin là bao nhiêu",  # lỗi thật khi thử với usas-slm-v2
        "học phí của MIT là bao nhiêu",
        "Harvard xếp hạng bao nhiêu",
    ],
)
def test_a_school_fact_question_the_slm_took_for_sensitive_becomes_a_knowledge_question(message):
    slm = with_info_intent(message, out_of_scope("nhay_cam"))

    assert slm.types() == ["kien_thuc"]
    assert slm.cac_y[0].doan == message


@pytest.mark.parametrize(
    "message",
    [
        "em muốn sửa GPA cao hơn để nộp hồ sơ được không",
        "làm giả bảng điểm có được không",
        "khai gian GPA có sao không",
        "mua bằng IELTS ở đâu",
    ],
)
def test_truly_sensitive_questions_stay_blocked_even_when_they_mention_a_field(message):
    assert with_info_intent(message, out_of_scope("nhay_cam")).types() == ["ngoai_pham_vi"]


def test_other_out_of_scope_kinds_and_other_intents_are_left_alone():
    assert with_info_intent("học phí là gì", out_of_scope("tam_biet")).types() == ["ngoai_pham_vi"]
    assert with_info_intent("học phí MIT", mac_dinh("học phí MIT")).types() == ["mac_dinh"]


# ---- ưu tiên do model trả (priority_*) trong thay_doi
def test_priorities_the_model_returned_are_read_and_bad_values_are_dropped():
    from app.services.priority import from_model

    changes = [
        {"yeu_to": "priority_aid", "gia_tri": "4"},
        {"yeu_to": "priority_cost", "gia_tri": "9"},  # ngoài 0..4
        {"yeu_to": "priority_prestige", "gia_tri": "cao"},  # không phải số
        {"yeu_to": "priority_unknown", "gia_tri": "2"},  # yếu tố lạ
        {"yeu_to": "sat", "gia_tri": "1500"},  # không phải ưu tiên
    ]

    assert from_model(changes) == {"aid": 4}


async def test_a_what_if_with_only_model_priorities_goes_to_dotnet_with_them():
    from app.services.branches import build_branches
    from tests.fakes import tin
    from tests.test_branches import skills

    entities = {
        "che_do": "what_if",
        "thay_doi": [{"yeu_to": "priority_cost", "gia_tri": "0"}, {"yeu_to": "priority_aid", "gia_tri": "4"}],
    }

    out = await build_branches(skills([]))["kich_ban"].run(
        ChatState(has_results=True), "x", tin(("kich_ban", entities, "đổi ưu tiên"))
    )

    assert out.action == "simulate"
    assert out.action_data["priorities"] == {"cost": 0, "aid": 4}
    assert out.action_data["unsupported"] == []  # ưu tiên không bị báo là "chưa thử được"
