"""Hỏi giải thích một trường trong chat, bước 2: skill_explain xác định trường nào rồi báo .NET (action explain)."""

import pytest

from app.schemas.chat import ChatState, ResultSchool
from app.services.ollama import OllamaError
from app.services.skills import build_handlers
from app.services.skills.skill_explain import (
    ASK_SCHOOL,
    NO_LIST,
    ExplainHandler,
    check_pick,
    match_by_name,
)
from tests.fakes import FakeLLM

IOWA = ResultSchool(code="iowa", name="University of Iowa", group="R")
ASU = ResultSchool(code="asu", name="Arizona State University", group="M")
SCHOOLS = [IOWA, ASU]


def state(schools=SCHOOLS):
    return ChatState(current_step="B4", has_results=True, schools=schools)


def handler(llm):
    return ExplainHandler(llm, "qwen3:8b")


# ---------- kiểm tra mã AI chọn ----------


def test_check_pick_accepts_a_code_in_the_list_in_any_letter_case():
    assert check_pick({"code": "iowa"}, SCHOOLS) == "iowa"
    assert check_pick({"code": "ASU"}, SCHOOLS) == "asu"


def test_check_pick_treats_null_as_not_sure():
    assert check_pick({"code": None}, SCHOOLS) is None


def test_check_pick_rejects_a_code_the_ai_made_up():
    with pytest.raises(ValueError):
        check_pick({"code": "harvard"}, SCHOOLS)


# ---------- khớp tên ----------


def test_match_by_name_finds_exactly_one_school_written_in_full():
    assert match_by_name("vì sao University of Iowa là nhóm khó vào", SCHOOLS) == IOWA


def test_match_by_name_gives_up_when_none_or_several_names_appear():
    assert match_by_name("vì sao em bị xếp nhóm này", SCHOOLS) is None
    assert match_by_name("so University of Iowa với Arizona State University", SCHOOLS) is None


# ---------- handler ----------


async def test_a_full_school_name_skips_the_ai_and_asks_net_to_explain():
    llm = FakeLLM()

    out = await handler(llm).run(state(), "vì sao University of Iowa là nhóm khó vào")

    assert out.action == "explain" and out.action_data == {"code": "iowa"}
    assert "University of Iowa" in out.answer
    assert llm.calls == []


async def test_the_ai_picks_the_school_when_the_name_is_not_written_in_full():
    llm = FakeLLM({"code": "asu"})

    out = await handler(llm).run(state(), "giải thích giúp em trường thứ hai")

    assert out.action == "explain" and out.action_data == {"code": "asu"}
    # AI thấy tên tiếng Việt của nhóm, không thấy mã kỹ thuật R/M
    sent = llm.calls[0]["messages"][1]["content"]
    assert "Khó vào" in sent and '"group":"R"' not in sent


async def test_the_ai_not_sure_makes_the_handler_ask_with_one_button_per_school():
    out = await handler(FakeLLM({"code": None})).run(state(), "vì sao em bị xếp nhóm này")

    assert out.action is None and out.answer == ASK_SCHOOL
    assert [b.label for b in out.quick_replies] == ["University of Iowa", "Arizona State University"]
    # bấm nút là gửi câu có đúng tên trường, lượt sau khớp tên mà không cần AI
    assert match_by_name(out.quick_replies[0].value, SCHOOLS) == IOWA


async def test_a_made_up_code_twice_falls_back_to_asking():
    llm = FakeLLM({"code": "harvard"}, {"code": "harvard"})

    out = await handler(llm).run(state(), "giải thích giúp em trường thứ hai")

    assert out.action is None and out.answer == ASK_SCHOOL
    assert len(llm.calls) == 2


async def test_the_ai_being_down_falls_back_to_asking():
    llm = FakeLLM(OllamaError("tắt"), OllamaError("tắt"))

    out = await handler(llm).run(state(), "giải thích giúp em trường thứ hai")

    assert out.action is None and out.answer == ASK_SCHOOL


async def test_no_school_list_asks_for_a_recommendation_first_without_calling_the_ai():
    llm = FakeLLM()

    out = await handler(llm).run(state([]), "vì sao Iowa khó vào")

    assert out.answer == NO_LIST and out.action is None
    assert llm.calls == []


async def test_only_the_first_buttons_are_shown_for_a_long_list():
    many = [ResultSchool(code=f"s{i}", name=f"School {i}") for i in range(20)]

    out = await handler(FakeLLM({"code": None})).run(state(many), "vì sao em bị xếp nhóm này")

    assert len(out.quick_replies) == 6


def test_the_handler_is_registered_for_skill_explain():
    assert isinstance(build_handlers(llm=object(), model="m")["skill_explain"], ExplainHandler)
