"""Ưu tiên nói trong chat: nhận ra yếu tố và mức, không nhầm với thông tin hồ sơ hay câu hỏi kiến thức."""

import pytest

from app.schemas.chat import ChatState
from app.schemas.slm import SlmTinResult, SlmY
from app.services.chat_flow import with_comparison_intent, with_info_intent, with_priority_intent
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


def collect_profile(message: str) -> SlmTinResult:
    return SlmTinResult(intents=[SlmY(type="collect_profile", span=message)])


def test_priority_message_becomes_a_what_if_when_results_exist():
    state = ChatState(has_results=True)
    slm = with_priority_intent(state, "em ưu tiên học bổng", collect_profile("em ưu tiên học bổng"))

    assert slm.types() == ["scenario"]
    assert slm.intents[0].entities["mode"] == "what_if"


def test_priority_is_left_alone_while_a_profile_question_waits():
    message = "em ưu tiên học bổng"
    waiting = ChatState(has_results=True, question_id="B3.4")
    assert with_priority_intent(waiting, message, collect_profile(message)).types() == ["collect_profile"]


def test_the_opening_greeting_question_does_not_block_it():
    # Lỗi thật: chat mới mở còn đang ở câu chào B0.1, "Em ưu tiên học bổng nhất" bị SLM xếp collect_profile và trả "hồ sơ đã đủ"
    message = "Em ưu tiên học bổng nhất"
    greeting = ChatState(question_id="B0.1")
    assert with_priority_intent(greeting, message, collect_profile(message)).types() == ["scenario"]


def test_without_results_the_scenario_branch_asks_for_recommendations_first():
    message = "em ưu tiên học bổng"
    assert with_priority_intent(ChatState(), message, collect_profile(message)).types() == ["scenario"]


def test_keeps_other_intents_when_the_message_also_has_numbers():
    state = ChatState(has_results=True)
    message = "SAT 1500 và ưu tiên học bổng"
    slm = with_priority_intent(state, message, collect_profile(message))

    assert slm.types() == ["scenario", "collect_profile"]


def goal_seeking(message: str, school: str | None = None) -> SlmTinResult:
    entities = {"mode": "goal_seeking"}
    if school:
        entities["school"] = school
    return SlmTinResult(intents=[SlmY(type="scenario", span=message, entities=entities)])


def test_a_priority_the_slm_took_for_goal_seeking_without_a_school_becomes_a_what_if():
    # Lỗi thật khi thử với usas-slm-v2: "ưu tiên chi phí thấp" bị xếp goal_seeking, hệ thống hỏi "bạn muốn vào trường nào"
    message = "ưu tiên chi phí thấp"
    slm = with_priority_intent(ChatState(has_results=True), message, goal_seeking(message))

    assert slm.types() == ["scenario"]
    assert slm.intents[0].entities["mode"] == "what_if"


def test_goal_seeking_with_a_real_school_is_left_alone():
    message = "em ưu tiên học bổng, cần gì để vào Harvard"
    slm = with_priority_intent(ChatState(has_results=True), message, goal_seeking(message, "Harvard"))

    assert slm.intents[0].entities["mode"] == "goal_seeking"


def out_of_scope(kind: str) -> SlmTinResult:
    return SlmTinResult(intents=[SlmY(type="out_of_scope", span="x", entities={"subtype": kind})])


@pytest.mark.parametrize(
    "message",
    [
        "GPA của The University of Texas at Austin là bao nhiêu",  # lỗi thật khi thử với usas-slm-v2
        "học phí của MIT là bao nhiêu",
        "Harvard xếp hạng bao nhiêu",
    ],
)
def test_a_school_fact_question_the_slm_took_for_sensitive_becomes_a_knowledge_question(message):
    slm = with_info_intent(message, out_of_scope("sensitive"))

    assert slm.types() == ["knowledge"]
    assert slm.intents[0].span == message


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
    assert with_info_intent(message, out_of_scope("sensitive")).types() == ["out_of_scope"]


def test_other_out_of_scope_kinds_and_other_intents_are_left_alone():
    assert with_info_intent("học phí là gì", out_of_scope("farewell")).types() == ["out_of_scope"]
    assert with_info_intent("học phí MIT", collect_profile("học phí MIT")).types() == ["collect_profile"]


# ---- ưu tiên do model trả (priority_*) trong changes
def test_priorities_the_model_returned_are_read_and_bad_values_are_dropped():
    from app.services.priority import from_model

    changes = [
        {"factor": "priority_aid", "value": "4"},
        {"factor": "priority_cost", "value": "9"},  # ngoài 0..4
        {"factor": "priority_prestige", "value": "cao"},  # không phải số
        {"factor": "priority_unknown", "value": "2"},  # yếu tố lạ
        {"factor": "sat", "value": "1500"},  # không phải ưu tiên
    ]

    assert from_model(changes) == {"aid": 4}


async def test_a_what_if_with_only_model_priorities_goes_to_dotnet_with_them():
    from app.services.branches import build_branches
    from tests.fakes import tin
    from tests.test_branches import skills

    entities = {
        "mode": "what_if",
        "changes": [{"factor": "priority_cost", "value": "0"}, {"factor": "priority_aid", "value": "4"}],
    }

    out = await build_branches(skills([]))["scenario"].run(
        ChatState(has_results=True), "x", tin(("scenario", entities, "đổi ưu tiên"))
    )

    assert out.action == "simulate"
    assert out.action_data["priorities"] == {"cost": 0, "aid": 4}
    assert out.action_data["unsupported"] == []  # ưu tiên không bị báo là "chưa thử được"


# ---- "So sánh 2 trường A và B" SLM xếp nhầm vào nhánh hồ sơ (lỗi thật khi thử với usas-slm-v3)
def profile_compare(*schools: str) -> SlmTinResult:
    entities = {"tasks": ["compare"], "schools": list(schools)}
    return SlmTinResult(intents=[SlmY(type="profile_review", span="x", entities=entities)])


def test_comparing_schools_without_mentioning_the_profile_becomes_a_fact_comparison():
    message = "So sánh 2 trường Florida State University và The University of Texas at Austin"

    slm = with_comparison_intent(
        message, profile_compare("Florida State University", "The University of Texas at Austin")
    )

    y = slm.intents[0]
    assert (y.type, y.entities["tasks"]) == ("knowledge", ["compare_facts"])
    assert y.entities["schools"] == ["Florida State University", "The University of Texas at Austin"]


@pytest.mark.parametrize(
    "message",
    [
        "So sánh Purdue và Ohio State giúp em, với hồ sơ của em thì chọn trường nào?",
        "so sánh USC NYU xem cái nào hợp với em",
        "so sánh Stanford và MIT, em có khả năng vào trường nào",
    ],
)
def test_a_comparison_about_the_students_own_profile_stays_in_the_profile_branch(message):
    slm = with_comparison_intent(message, profile_compare("Purdue", "Ohio State"))

    assert slm.types() == ["profile_review"]


def test_other_profile_tasks_are_left_alone():
    other_task = SlmTinResult(
        intents=[SlmY(type="profile_review", span="x", entities={"tasks": ["assessment"]})]
    )

    assert with_comparison_intent("đánh giá hồ sơ em", other_task).types() == ["profile_review"]
