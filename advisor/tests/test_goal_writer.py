"""Lời cho goal-seeking: Gemini diễn đạt lại kết quả code đã tính; số lạ, tên trường lạ, lời hứa và quên tên trường bị chặn."""

import copy

import pytest
from fastapi.testclient import TestClient

from app.api.deps import get_goal_writer
from app.main import create_app
from app.schemas.goal_note import GoalNoteRequest
from app.services.goal_writer import GoalWriter, allowed_numbers, build_user
from app.services.ollama import OllamaError
from tests.fakes import FakeLLM

# JSON backend gửi (camelCase)
REQUEST = {
    "school": "Boston University",
    "target": "Vừa sức",
    "outcome": "reachable",
    "facts": [
        "Tài chính: Chi phí khoảng 38.000 USD mỗi năm, nằm trong ngân sách của bạn.",
        "Học thuật: để lên nhóm Vừa sức, mức tối thiểu khi chỉ đổi một yếu tố:",
        "SAT 1290 → 1430 (khó)",
        "Nên đặt mục tiêu (có đệm): SAT 1290 → 1460 (khó)",
    ],
    "alternatives": ["Ohio Wesleyan University"],
    "others": ["Harvard University", "Stanford University", "Baylor University"],
}

GOOD = {
    "intro": "Để lên nhóm Vừa sức ở Boston University, bạn cần nâng SAT và mức này khá khó.",
    "points": [
        "Tài chính ổn: chi phí khoảng 38.000 USD mỗi năm, nằm trong ngân sách của bạn.",
        "Nếu chỉ đổi SAT thì cần từ 1290 lên 1430, nên đặt mục tiêu 1460 cho chắc.",
        "Bạn cũng có thể xem Ohio Wesleyan University, một trường tương tự đang ở nhóm Vừa sức.",
    ],
    "closing": "Bạn xem bảng chi tiết ở trang Gợi ý trường nhé.",
}


def req(**changes) -> GoalNoteRequest:
    return GoalNoteRequest.model_validate({**copy.deepcopy(REQUEST), **changes})


def writer(llm: FakeLLM) -> GoalWriter:
    return GoalWriter(llm, "gemini-test")


def changed(**changes) -> dict:
    return {**copy.deepcopy(GOOD), **changes}


# ---- dữ liệu gửi Gemini
def test_the_prompt_carries_the_facts_but_not_the_other_schools():
    user = build_user(req())

    assert user["school"] == "Boston University"
    assert user["target"] == "Vừa sức"
    assert user["facts"][2] == "SAT 1290 → 1430 (khó)"
    assert user["alternatives"] == ["Ohio Wesleyan University"]
    assert "Harvard" not in str(user)  # others chỉ dùng để kiểm tra, không gửi cho Gemini


def test_allowed_numbers_come_from_the_facts_only():
    allowed = allowed_numbers(req())

    assert {38000, 1290, 1430, 1460}.issubset(allowed)
    assert 1500 not in allowed


# ---- lời hợp lệ
async def test_a_reply_that_matches_the_facts_becomes_one_message():
    llm = FakeLLM(GOOD)

    res = await writer(llm).write(req())

    assert res.status == "OK"
    lines = res.answer.splitlines()
    assert lines[0] == GOOD["intro"]
    assert "- Nếu chỉ đổi SAT thì cần từ 1290 lên 1430" in res.answer
    assert res.answer.endswith(GOOD["closing"])
    assert len(llm.calls) == 1 and llm.calls[0]["temperature"] == 0.0


async def test_a_reply_without_points_or_closing_is_fine():
    ok = changed(points=[], closing="")

    res = await writer(FakeLLM(ok)).write(req())

    assert res.status == "OK" and res.answer == GOOD["intro"]


async def test_the_number_check_ignores_thousand_and_decimal_formatting():
    ok = changed(points=["Chi phí khoảng 38000 USD mỗi năm, SAT cần 1.430."])

    assert (await writer(FakeLLM(ok)).write(req())).status == "OK"


# ---- chặn lời sai
BAD = {
    "forgot-the-school": changed(intro="Để lên nhóm Vừa sức, bạn cần nâng SAT và mức này khá khó."),
    "invented-number": changed(points=["SAT cần lên 1500 mới đủ."]),
    "stranger-school": changed(closing="Bạn cũng có thể nhắm tới Harvard University nhé."),
    "promise": changed(closing="Làm theo vậy thì bạn chắc chắn đậu."),
}


@pytest.mark.parametrize("bad", BAD.values(), ids=BAD.keys())
async def test_a_reply_that_breaks_the_rules_is_rejected_then_retried_once(bad):
    llm = FakeLLM(bad, GOOD)

    res = await writer(llm).write(req())

    assert res.status == "OK" and res.answer.splitlines()[0] == GOOD["intro"]
    assert len(llm.calls) == 2


async def test_two_bad_replies_fail_so_backend_uses_its_own_sentence():
    bad = BAD["invented-number"]

    res = await writer(FakeLLM(bad, bad)).write(req())

    assert res.status == "FAILED" and res.answer == ""


async def test_the_target_school_and_its_alternatives_are_never_strangers():
    # Tên ngắn nằm trong tên dài hợp lệ không bị coi là trường lạ
    data = copy.deepcopy(REQUEST)
    data["school"] = "Washington University in St. Louis"
    data["others"] = ["Washington University"]
    ok = changed(intro="Để lên nhóm Vừa sức ở Washington University in St. Louis, bạn cần nâng SAT.")

    res = await writer(FakeLLM(ok)).write(GoalNoteRequest.model_validate(data))

    assert res.status == "OK"


async def test_a_gemini_error_is_retried_then_reported_as_failed():
    llm = FakeLLM(OllamaError("hết hạn mức"), OllamaError("hết hạn mức"))

    assert (await writer(llm).write(req())).status == "FAILED"


# ---- khuôn yêu cầu và route
def test_facts_are_required():
    with pytest.raises(ValueError):
        req(facts=[])


def test_the_route_returns_the_message_with_the_skill_version():
    app = create_app()
    app.dependency_overrides[get_goal_writer] = lambda: writer(FakeLLM(GOOD))

    res = TestClient(app).post("/api/v1/recommend/goal-seek", json=REQUEST)

    assert res.status_code == 200
    body = res.json()
    assert body["status"] == "OK" and body["answer"].startswith("Để lên nhóm Vừa sức ở Boston University")
    assert body["skill_version"] == "1.0" and body["model"] == "gemini-test"
