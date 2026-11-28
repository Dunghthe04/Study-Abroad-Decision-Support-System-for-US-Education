"""Hiểu "trường đó", "trường đầu tiên", "trường thứ 2" sau khi gợi ý hoặc giải thích trường (UC-39, UC-153).

Lỗi thật 27/11/2026: sau khi có danh sách, hỏi tiếp về "trường đó" thì chat không biết là trường nào, vì giải thích và
gợi ý không ghi gì vào ngữ cảnh. Chỉ ghi nhớ thôi thì chưa đủ: đã nhớ một trường, model chép luôn trường đó cho cả
"trường cuối cùng", nên code tạm đọc thứ tự trong câu (ngoại lệ chốt 28/11/2026, gỡ sau khi train).
"""

import pytest

from app.schemas.chat import ChatContext, ChatState, ResultSchool
from app.schemas.slm import SlmTinResult, SlmY
from app.services.chat_flow import SkillOutput
from app.services.conversation_context import contextualize, update_context

NAMES = ["Purdue University", "Boston University", "Ohio State University"]


def listed(**context) -> ChatState:
    schools = [ResultSchool(code=str(i), name=name, group="M") for i, name in enumerate(NAMES)]
    return ChatState(has_results=True, schools=schools, context=ChatContext(**context))


def ask(state: ChatState, span: str, kind: str, **entities) -> dict:
    slm = SlmTinResult(intents=[SlmY(type=kind, span=span, entities=entities)])
    return contextualize(state, span, slm).intents[0].entities


def info(state: ChatState, span: str, **extra) -> dict:
    return ask(state, span, "knowledge", tasks=["school_info"], **extra)


# ---- ghi nhớ sau mỗi câu trả lời
def test_explaining_a_school_remembers_it():
    state = listed()

    update_context(state, SkillOutput("x", action="explain", action_data={"code": "1"}))

    assert state.context.schools == ["Boston University"]


def test_a_new_school_list_forgets_the_remembered_school():
    state = listed(schools=["Harvard"])

    update_context(state, SkillOutput("x", action="recommend", action_data={"note": True}))

    assert state.context.schools == []


# ---- thứ tự trong danh sách
@pytest.mark.parametrize(
    ("span", "name"),
    [
        ("trường đầu tiên học phí bao nhiêu", "Purdue University"),
        ("Trường thứ 2 cần IELTS mấy", "Boston University"),
        ("truong thu hai hoc phi bao nhieu", "Boston University"),
        ("trường cuối cùng xếp hạng bao nhiêu", "Ohio State University"),
        ("trường cuối danh sách có học bổng không", "Ohio State University"),
        ("trường số 3 trong danh sách ở đâu", "Ohio State University"),
    ],
)
@pytest.mark.parametrize(
    "said",
    [
        {},
        {"follow_up": True},
        {"schools": ["Purdue University"], "follow_up": True},
        {"schools": ["trường đó"]},
    ],
    ids=["khong_co_truong", "chi_noi_tiep", "model_chep_truong_dang_nho", "model_tra_cum_chu"],
)
def test_a_position_in_the_list_picks_that_school(span, name, said):
    assert info(listed(schools=["Purdue University"]), span, **said)["schools"] == [name]


def test_the_explanation_and_goal_seeking_use_the_position_too():
    state = listed(schools=["Purdue University"])

    explain = ask(
        state,
        "giải thích trường cuối cùng",
        "profile_review",
        tasks=["explain_school"],
        schools=["Purdue University"],
    )
    goal = ask(
        state,
        "muốn vào trường thứ 2 em cần gì",
        "scenario",
        mode="goal_seeking",
        school="Purdue University",
    )

    assert explain["schools"] == ["Ohio State University"]
    assert goal["school"] == "Boston University"


# Vị trí không có trong danh sách, hoặc chưa có danh sách: bỏ trường model chép từ ngữ cảnh để hệ thống hỏi lại
@pytest.mark.parametrize(
    ("state", "span"),
    [
        (listed(schools=["Purdue University"]), "trường thứ 9 học phí bao nhiêu"),
        (ChatState(context=ChatContext(schools=["Harvard"])), "trường đầu tiên học phí bao nhiêu"),
    ],
    ids=["ngoai_danh_sach", "chua_co_danh_sach"],
)
def test_an_unknown_position_never_falls_back_to_another_school(state, span):
    out = info(state, span, schools=state.context.schools, follow_up=True)

    assert "schools" not in out and "follow_up" not in out


# Chữ chỉ thứ tự mà KHÔNG chỉ vị trí trong danh sách: giữ nguyên nhãn
@pytest.mark.parametrize(
    "span",
    [
        "thứ 2 trung tâm có mở cửa không",
        "bước đầu tiên để xin visa là gì",
        "trường số 1 nước Mỹ là trường nào",
        "trường đại học đầu tiên của Mỹ là trường nào",
        "học bổng năm thứ 2 có giữ được không",
        "so sánh trường thứ 1 với trường thứ 2",
    ],
)
def test_other_ordinal_words_change_nothing(span):
    assert info(listed(), span) == {"tasks": ["school_info"]}


# Học sinh nói rõ tên trường thì tên đó thắng
def test_a_school_named_in_the_sentence_is_kept():
    out = info(listed(), "Harvard có phải trường đầu tiên không", schools=["Harvard"])

    assert out["schools"] == ["Harvard"]


# Chỉ áp cho ý model đã nói là hỏi về một trường: câu kiến thức chung không bị gắn trường
def test_a_general_knowledge_question_is_left_alone():
    assert ask(listed(), "trường đầu tiên thành lập ở Mỹ", "knowledge") == {}
