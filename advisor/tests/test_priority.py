"""Ưu tiên nói trong chat: nhận ra yếu tố và mức, không nhầm với thông tin hồ sơ hay câu hỏi kiến thức."""

import pytest

from app.schemas.chat import ChatState
from app.schemas.slm import SlmTinResult, SlmY
from app.services.chat_flow import with_comparison_intent, with_info_intent
from app.services.slm_classifier import FALLBACK


def collect_profile(message: str) -> SlmTinResult:
    return SlmTinResult(intents=[SlmY(type="collect_profile", span=message)])


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


def test_a_message_the_slm_could_not_classify_is_not_guessed_into_a_knowledge_question():
    # Lỗi thật 27/11/2026: SLM trả sai khuôn cho câu xin gợi ý trường ở Texas, luật này đổi thành câu kiến thức vì có chữ IELTS,
    # rồi hệ thống trả số liệu một trường ở Chicago. Sai khuôn thì giữ nguyên để chat nói chưa hiểu và hỏi lại
    message = "Tài chính nhà mình khoảng 800 triệu/năm, IELTS 6.5. So sánh giúp mình 3 trường ở bang Texas"
    fallback = FALLBACK.model_copy(
        deep=True, update={"intents": [FALLBACK.intents[0].model_copy(update={"span": message})]}
    )

    slm = with_info_intent(message, fallback)

    assert slm.types() == ["out_of_scope"]
    assert slm.intents[0].entities["subtype"] == "ambiguous"


def test_an_ambiguous_label_the_slm_did_return_is_still_rescued():
    # Model trả đúng khuôn mà gán ambiguous cho câu hỏi số liệu trường: vẫn đổi thành câu kiến thức như trước
    assert with_info_intent("học phí của MIT là bao nhiêu", out_of_scope("ambiguous")).types() == [
        "knowledge"
    ]


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


# ---- không còn quy tắc từ khóa: câu chữ không tự thành ưu tiên, chỉ điều model trả (priority_*) mới tính
async def test_a_scholarship_need_the_model_read_is_not_also_counted_as_a_priority():
    # Lỗi thật 16/11: "nêu không cần học bổng" model trả đúng mức cần học bổng, nhưng quy tắc từ khóa thêm "Ưu tiên mới: học bổng nhiều 0/4"
    from app.services.branches import build_branches
    from tests.fakes import tin

    slm = tin(("scenario", {"mode": "what_if", "changes": [{"factor": "scholarship", "value": "optional"}]}))
    out = await build_branches({})["scenario"].run(
        ChatState(has_results=True), "nêu không cần học bổng thì sao", slm
    )

    assert out.action == "simulate"
    assert "priorities" not in out.action_data
    assert "Ưu tiên" not in out.answer
