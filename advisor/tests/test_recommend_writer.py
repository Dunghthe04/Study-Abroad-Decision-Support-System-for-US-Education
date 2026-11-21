"""Lời cho danh sách gợi ý trường: Claude diễn đạt lại danh sách code đã xếp nhóm; bỏ sót trường, số lạ, tên trường lạ,
lời hứa và xếp hạng trường đều bị chặn, hai lần sai thì backend dùng câu liệt kê ghép bằng code."""

import copy
import json
import re

import pytest
from fastapi.testclient import TestClient

from app.api.deps import get_recommend_writer
from app.main import create_app
from app.schemas.recommend_note import RecommendNoteRequest
from app.services.ollama import OllamaError
from app.services.recommend_writer import (
    SKILL_PATH,
    RecommendWriter,
    allowed_numbers,
    build_user,
    check_answer,
)
from app.services.skill_call import load_skill
from tests.fakes import FakeLLM

# JSON backend gửi (camelCase)
REQUEST = {
    "schools": [
        "Oklahoma State University-Main Campus",
        "University of Vermont",
        "California State University-Long Beach",
        "University of South Dakota",
    ],
    "facts": [
        "Tổng cộng 7 trường phù hợp với hồ sơ của bạn.",
        "Khó vào: Oklahoma State University-Main Campus, University of Vermont",
        "Vừa sức: California State University-Long Beach",
        "An toàn hơn: University of South Dakota và 3 trường khác",
    ],
    "others": ["Harvard University", "Stanford University", "Baylor University"],
}

GOOD = {
    "intro": "Mình đã tìm được 7 trường phù hợp với hồ sơ và ngành của bạn.",
    "points": [
        "Khó vào là các mục tiêu thử sức: Oklahoma State University-Main Campus và University of Vermont.",
        "Vừa sức là nhóm hồ sơ của bạn nằm trong khoảng sinh viên đã được nhận: California State University-Long Beach.",
        "An toàn hơn là nhóm cơ hội tốt hơn: University of South Dakota, và còn 3 trường khác.",
    ],
    "closing": "Bạn bấm Vì sao nhóm này? ở từng trường để xem lý do nhé.",
}


def req(**changes) -> RecommendNoteRequest:
    return RecommendNoteRequest.model_validate({**copy.deepcopy(REQUEST), **changes})


def writer(llm: FakeLLM) -> RecommendWriter:
    return RecommendWriter(llm, "claude-test")


def changed(**changes) -> dict:
    return {**copy.deepcopy(GOOD), **changes}


# ---- dữ liệu gửi Claude
def test_the_prompt_carries_schools_and_facts_but_not_the_other_schools():
    user = build_user(req())

    assert user["schools"][1] == "University of Vermont"
    assert user["facts"][0] == "Tổng cộng 7 trường phù hợp với hồ sơ của bạn."
    assert "Harvard" not in str(user)  # others chỉ dùng để kiểm tra, không gửi cho Claude


def test_allowed_numbers_come_from_the_facts_only():
    allowed = allowed_numbers(req())

    assert {7, 3}.issubset(allowed)
    assert 9 not in allowed


# ---- lời hợp lệ
async def test_a_reply_that_matches_the_list_becomes_one_message():
    llm = FakeLLM(GOOD)

    res = await writer(llm).write(req())

    assert res.status == "OK"
    lines = res.answer.splitlines()
    assert lines[0] == GOOD["intro"]
    assert "- Khó vào là các mục tiêu thử sức" in res.answer
    assert res.answer.endswith(GOOD["closing"])
    assert len(llm.calls) == 1 and llm.calls[0]["temperature"] == 0.0


async def test_the_number_check_ignores_thousand_and_decimal_formatting():
    ok = changed(intro="Mình đã tìm được 7,0 trường phù hợp với hồ sơ và ngành của bạn.")

    assert (await writer(FakeLLM(ok)).write(req())).status == "OK"


# ---- chặn lời sai
BAD = {
    "dropped-a-school": changed(
        points=[
            "Khó vào là các mục tiêu thử sức: Oklahoma State University-Main Campus.",
            "Vừa sức: California State University-Long Beach.",
            "An toàn hơn: University of South Dakota.",
        ]
    ),
    "invented-number": changed(intro="Mình đã tìm được 12 trường phù hợp với hồ sơ và ngành của bạn."),
    "stranger-school": changed(closing="Bạn cũng có thể xem thêm Harvard University nhé."),
    "promise": changed(closing="Nộp theo danh sách này thì bạn chắc chắn đậu."),
    "ranking": changed(closing="University of South Dakota là trường tốt nhất cho bạn."),
}


@pytest.mark.parametrize("bad", BAD.values(), ids=BAD.keys())
async def test_a_reply_that_breaks_the_rules_is_rejected_then_retried_once(bad):
    llm = FakeLLM(bad, GOOD)

    res = await writer(llm).write(req())

    assert res.status == "OK" and res.answer.splitlines()[0] == GOOD["intro"]
    assert len(llm.calls) == 2


async def test_two_bad_replies_fail_so_backend_uses_its_own_list():
    bad = BAD["dropped-a-school"]

    res = await writer(FakeLLM(bad, bad)).write(req())

    assert res.status == "FAILED" and res.answer == ""


async def test_a_shorter_school_name_inside_a_listed_one_is_not_a_stranger():
    # "University of Vermont" nằm trong tên dài hợp lệ thì không bị coi là trường lạ
    data = copy.deepcopy(REQUEST)
    data["schools"] = ["University of Vermont and State Agricultural College"]
    data["facts"] = [
        "Tổng cộng 1 trường phù hợp với hồ sơ của bạn.",
        "Vừa sức: University of Vermont and State Agricultural College",
    ]
    data["others"] = ["University of Vermont"]
    ok = {
        "intro": "Mình đã tìm được 1 trường phù hợp với hồ sơ: University of Vermont and State Agricultural College.",
        "points": [],
        "closing": "",
    }

    res = await writer(FakeLLM(ok)).write(RecommendNoteRequest.model_validate(data))

    assert res.status == "OK"


async def test_a_claude_error_is_retried_then_reported_as_failed():
    llm = FakeLLM(OllamaError("hết hạn mức"), OllamaError("hết hạn mức"))

    assert (await writer(llm).write(req())).status == "FAILED"


# ---- khuôn yêu cầu và route
def test_schools_and_facts_are_required():
    with pytest.raises(ValueError):
        req(schools=[])
    with pytest.raises(ValueError):
        req(facts=[])


def test_the_route_returns_the_message_with_the_skill_version():
    app = create_app()
    app.dependency_overrides[get_recommend_writer] = lambda: writer(FakeLLM(GOOD))

    res = TestClient(app).post("/api/v1/recommend/school-list", json=REQUEST)

    assert res.status_code == 200
    body = res.json()
    assert body["status"] == "OK" and body["answer"].startswith("Mình đã tìm được 7 trường")
    assert body["skill_version"] == load_skill(SKILL_PATH)[1] and body["model"] == "claude-test"


# ---- ví dụ trong file skill phải qua đúng bộ kiểm của code (ví dụ sai thì Claude học theo cái sai)
def skill_examples() -> list[tuple[dict, dict]]:
    text = SKILL_PATH.read_text(encoding="utf-8")
    pairs = re.findall(r"Đầu vào:\n(\{.*?\})\nĐầu ra:\n(\{.*?\})\n", text + "\n", flags=re.S)
    return [(json.loads(a), json.loads(b)) for a, b in pairs]


def test_the_skill_has_examples_and_the_version_line():
    assert len(skill_examples()) >= 2
    assert load_skill(SKILL_PATH)[1] != "unknown"


@pytest.mark.parametrize("example", skill_examples())
def test_every_example_in_the_skill_passes_the_checks(example):
    given, answer = example

    check_answer(answer, RecommendNoteRequest.model_validate(given))
