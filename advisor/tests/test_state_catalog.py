"""Đổi tên bang học sinh nói sang mã bang giống form, và chat hỏi lại khi chỉ gần giống hoặc không khớp."""

import pytest

from app.services.skills import state_catalog
from app.services.skills.state_catalog import UsState, match, to_codes
from app.services.skills.validators import AskAgain
from tests.fakes import FakeLLM
from tests.test_chat_skills_flow import PAGE1_ASKED, ho_so, turn

CA = UsState("CA", "California", "west")
TX = UsState("TX", "Texas", "south")
NC = UsState("NC", "North Carolina", "south")
SC = UsState("SC", "South Carolina", "south")
STATES = [CA, NC, SC, TX]


@pytest.fixture
def catalog():
    state_catalog.set_states(STATES)
    yield
    state_catalog.set_states(None)


def test_exact_match_by_code_or_name():
    assert match("ca", STATES).exact == CA
    assert match("  TEXAS ", STATES).exact == TX
    assert match("bang Texas", STATES).exact == TX


def test_typo_and_partial_name_give_suggestions():
    assert match("Califonia", STATES).near == [CA]
    assert match("Carolina", STATES).near == [NC, SC]
    assert match("vũ trụ", STATES).near == []


def test_names_become_codes_without_duplicates():
    assert to_codes(["California", "TX", "california"], STATES) == ["CA", "TX"]


def test_without_catalog_names_are_kept():
    assert to_codes(["California"], []) == ["California"]


def test_unknown_state_asks_again():
    with pytest.raises(AskAgain, match="gần với bang California"):
        to_codes(["Califonia"], STATES)
    with pytest.raises(AskAgain, match='chưa tìm thấy bang "Atlantis"'):
        to_codes(["Atlantis"], STATES)


async def test_chat_saves_state_codes_like_form(catalog):
    llm = FakeLLM(ho_so({"preferred_states": ["California", "Texas"]}))
    res = await turn(
        llm, "Em muốn học ở California, Texas", current_step="B2", question_id="N2", asked=PAGE1_ASKED
    )

    assert res.state.profile["preferred_states"].value == ["CA", "TX"]


async def test_chat_asks_again_for_misspelled_state(catalog):
    llm = FakeLLM(ho_so({"preferred_states": ["Califonia"]}))
    res = await turn(llm, "em muốn học ở Califonia", current_step="B2", question_id="N2", asked={"N2": 1})

    assert "preferred_states" not in res.state.profile
    assert res.answer.startswith('Mình thấy "Califonia" gần với bang California')
