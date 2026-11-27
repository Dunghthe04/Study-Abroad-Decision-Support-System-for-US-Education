"""Xin danh sách trường hay không là do NHÃN của SLM, code không đoán lại bằng chữ trong câu.

Lỗi thật 27/11/2026: "Tại sao đề xuất trường này" được SLM gán đúng là xin giải thích, nhưng câu có chữ
"đề xuất trường" nên code coi là xin danh sách và in lại y danh sách vừa gợi ý.
"""

import pytest

from app.schemas.chat import ChatState, ResultSchool
from app.schemas.slm import SlmY
from app.services.branches import ASK_WHICH_SCHOOLS, RECOMMENDING
from app.services.intake import form_goals
from app.services.skill_rules import REQUIRED_B4
from tests.fakes import tin
from tests.test_branches import FakeWriter, filled, new_advice


def with_results() -> ChatState:
    return ChatState(
        profile=filled(REQUIRED_B4),
        has_results=True,
        schools=[ResultSchool(code="1", name="Purdue University", group="match")],
    )


async def run(question: str, tasks: list[str], writer: FakeWriter | None = None):
    return await new_advice([], writer=writer or FakeWriter()).run(
        with_results(), question, tin(("profile_review", {"tasks": tasks}, question))
    )


@pytest.mark.parametrize(
    ("question", "tasks"),
    [
        ("Tại sao đề xuất trường này", ["explain_fit"]),
        ("sao lại gợi ý trường đó cho em vậy", ["explain_fit"]),
        ("em có nên nộp trường này không", ["explain_school"]),
        ("vì sao trường nào cũng bị xếp nhóm khó vào", ["explain_fit"]),
    ],
)
async def test_a_question_about_why_is_explained_not_listed_again(question, tasks):
    out = await run(question, tasks)

    assert out.action != "recommend" and out.answer != RECOMMENDING
    assert out.answer == ASK_WHICH_SCHOOLS  # chưa biết "trường này" là trường nào: hỏi lại, không đoán
    assert out.widget is not None and out.widget.data["kind"] == "explain"


async def test_an_assessment_question_does_not_also_run_a_school_list():
    writer = FakeWriter()

    out = await run("vậy em thuộc nhóm trường nào", ["assessment"], writer)

    assert out.answer == "lời tư vấn" and out.action is None


# Nhãn xin danh sách thì chạy gợi ý, dù câu không có chữ nào quen
async def test_the_school_list_label_runs_the_recommendation():
    out = await run("kiếm giúp em vài chỗ để theo học", ["school_list"])

    assert out.action == "recommend" and out.answer == RECOMMENDING


# Form tích sẵn việc cũng theo nhãn
def test_the_form_goals_follow_the_label():
    pending = SlmY(type="profile_review", span="em thuộc nhóm trường nào", entities={"tasks": ["assessment"]})

    assert form_goals(pending) == ["danh_gia"]
