"""Nhận xét so sánh trường: Claude diễn đạt lại số liệu code đã tra; số lạ, tên trường lạ, lời hứa, phán trường nào tốt hơn và quên tên trường bị chặn."""

import copy

import pytest
from fastapi.testclient import TestClient

from app.api.deps import get_compare_writer
from app.main import create_app
from app.schemas.compare_note import CompareNoteRequest
from app.services.compare_writer import SKILL_PATH, CompareWriter, allowed_numbers, build_user
from app.services.ollama import OllamaError
from app.services.skill_call import load_skill
from tests.fakes import FakeLLM

REQUEST = {
    "schools": ["Boston University", "New York University"],
    "facts": [
        "Chi phí mỗi năm: Boston University 86.000 USD · New York University 75.000 USD",
        "New York University rẻ hơn Boston University khoảng 11.000 USD mỗi năm.",
        "Tỷ lệ nhận: Boston University 11% · New York University 8%",
    ],
    "others": ["Harvard University", "Stanford University"],
}

GOOD = {
    "intro": "Boston University và New York University khác nhau rõ nhất ở chi phí mỗi năm.",
    "points": [
        "New York University tốn khoảng 75.000 USD mỗi năm, thấp hơn Boston University 86.000 USD.",
        "Tỷ lệ nhận của New York University là 8%, thấp hơn Boston University 11%, nghĩa là chọn lọc hơn.",
    ],
    "closing": "Nếu bạn ưu tiên chi phí thì New York University thấp hơn.",
}


def req(**changes) -> CompareNoteRequest:
    return CompareNoteRequest.model_validate({**copy.deepcopy(REQUEST), **changes})


def writer(llm: FakeLLM) -> CompareWriter:
    return CompareWriter(llm, "claude-test")


def changed(**changes) -> dict:
    return {**copy.deepcopy(GOOD), **changes}


def test_the_prompt_carries_the_schools_and_facts_but_not_the_other_schools():
    user = build_user(req())

    assert user["schools"] == ["Boston University", "New York University"]
    assert len(user["facts"]) == 3
    assert "Harvard" not in str(user)


def test_allowed_numbers_come_from_the_facts_only():
    allowed = allowed_numbers(req())

    assert {86000, 75000, 11000, 11, 8}.issubset(allowed)
    assert 90000 not in allowed


async def test_a_reply_that_matches_the_facts_becomes_one_message():
    llm = FakeLLM(GOOD)

    res = await writer(llm).write(req())

    assert res.status == "OK"
    assert res.answer.splitlines()[0] == GOOD["intro"]
    assert "- Tỷ lệ nhận của New York University là 8%" in res.answer
    assert res.answer.endswith(GOOD["closing"])
    assert len(llm.calls) == 1 and llm.calls[0]["temperature"] == 0.0


async def test_the_number_check_ignores_thousand_formatting():
    ok = changed(points=["New York University khoảng 75000 USD mỗi năm, Boston University 86.000 USD."])

    assert (await writer(FakeLLM(ok)).write(req())).status == "OK"


BAD = {
    "forgot-a-school": changed(
        intro="Hai trường này khác nhau rõ nhất ở chi phí mỗi năm của Boston University.",
        points=["Boston University tốn khoảng 86.000 USD mỗi năm."],
        closing="Bạn xem thêm nhé.",
    ),
    "invented-number": changed(points=["New York University chỉ tốn 60.000 USD mỗi năm."]),
    "stranger-school": changed(closing="Bạn cũng có thể xem Harvard University nhé."),
    "promise": changed(closing="Chọn New York University thì bạn chắc chắn đậu."),
    "verdict-better": changed(closing="New York University tốt hơn Boston University về chi phí."),
    "verdict-should-choose": changed(closing="Bạn nên chọn New York University."),
}


@pytest.mark.parametrize("bad", BAD.values(), ids=BAD.keys())
async def test_a_reply_that_breaks_the_rules_is_rejected_then_retried_once(bad):
    llm = FakeLLM(bad, GOOD)

    res = await writer(llm).write(req())

    assert res.status == "OK" and res.answer.splitlines()[0] == GOOD["intro"]
    assert len(llm.calls) == 2


async def test_two_bad_replies_fail_so_backend_shows_only_the_table():
    bad = BAD["invented-number"]

    res = await writer(FakeLLM(bad, bad)).write(req())

    assert res.status == "FAILED" and res.answer == ""


async def test_saying_a_school_is_cheaper_is_fine_when_it_is_a_fact():
    # "rẻ hơn" và "thấp hơn" là sự thật trong số liệu; chỉ phán xét chung (tốt hơn, nên chọn) bị chặn
    ok = changed(closing="Nếu bạn ưu tiên chi phí thì New York University rẻ hơn Boston University.")

    assert (await writer(FakeLLM(ok)).write(req())).status == "OK"


async def test_a_claude_error_is_retried_then_reported_as_failed():
    llm = FakeLLM(OllamaError("hết hạn mức"), OllamaError("hết hạn mức"))

    assert (await writer(llm).write(req())).status == "FAILED"


def test_at_least_two_schools_and_some_facts_are_required():
    with pytest.raises(ValueError):
        req(schools=["Boston University"])
    with pytest.raises(ValueError):
        req(facts=[])
    with pytest.raises(ValueError):
        req(schools=["A", "B", "C", "D", "E"])


def test_the_route_returns_the_message_with_the_skill_version():
    app = create_app()
    app.dependency_overrides[get_compare_writer] = lambda: writer(FakeLLM(GOOD))

    res = TestClient(app).post("/api/v1/recommend/compare", json=REQUEST)

    assert res.status_code == 200
    body = res.json()
    assert body["status"] == "OK" and body["answer"].startswith("Boston University và New York University")
    assert body["skill_version"] == load_skill(SKILL_PATH)[1] and body["model"] == "claude-test"
