"""Lời cho kịch bản what-if: Gemini viết lời, code chặn tên trường, nhóm và số không có trong kết quả."""

import copy

import pytest
from fastapi.testclient import TestClient

from app.api.deps import get_scenario_writer
from app.main import create_app
from app.schemas.scenario_note import ScenarioNoteRequest
from app.services.ollama import OllamaError
from app.services.scenario_writer import (
    FACTOR_EFFECT,
    REASON_TEXT,
    ScenarioWriter,
    allowed_numbers,
    build_user,
)
from tests.fakes import FakeLLM

# JSON backend gửi (camelCase): SAT 1500, ba nhóm; Boston và Northeastern vừa đổi nhóm
REQUEST = {
    "label": "SAT 1500 (hiện tại 1290)",
    "factors": ["sat"],
    "unsupported": [],
    "direction": "better",
    "student": {"budgetUsd": 40000, "scholarship": "nên có"},
    "groups": [
        {
            "key": "reach",
            "title": "Khó vào",
            "schools": [{"name": "Cornell University", "reasons": []}, {"name": "Duke University"}],
            "more": 0,
        },
        {
            "key": "match",
            "title": "Vừa sức",
            "schools": [
                {
                    "name": "Boston University",
                    "movedFrom": "Khó vào",
                    "reasons": ["SAT_ABOVE"],
                    "needAid": True,
                },
                {"name": "Northeastern University", "movedFrom": "Khó vào", "reasons": []},
            ],
            "more": 3,
        },
        {"key": "safety", "title": "An toàn hơn", "schools": [{"name": "Rutgers University"}], "more": 0},
    ],
    "others": ["Harvard University", "Stanford University"],
}

NOTE = "Boston University lên từ Khó vào vì SAT cao hơn mốc 75% của trường, nhưng vượt ngân sách 40.000 USD."
GOOD = {
    "intro": "Với SAT 1500 (hiện tại 1290), danh sách của bạn khá hơn rõ rệt.",
    "groups": [
        {"key": "reach", "schools": ["Cornell University", "Duke University"], "note": ""},
        {"key": "match", "schools": ["Boston University", "Northeastern University"], "note": NOTE},
        {"key": "safety", "schools": ["Rutgers University"], "note": ""},
    ],
    "closing": "Bạn muốn biết cần gì để vào một trường cụ thể thì cứ hỏi mình nhé.",
}

NONE_REQUEST = {
    "label": "IELTS 7.5 (hiện tại 6.5)",
    "factors": ["ielts"],
    "unsupported": ["ngành"],
    "direction": "none",
    "groups": [],
    "others": ["Harvard University"],
}
NONE_GOOD = {
    "intro": "Mình đã thử IELTS 7.5 (hiện tại 6.5): danh sách giữ nguyên vì IELTS không làm đổi nhóm.",
    "groups": [],
    "closing": "Mình chưa thử được: ngành.",
}


def req(base: dict = REQUEST) -> ScenarioNoteRequest:
    return ScenarioNoteRequest.model_validate(copy.deepcopy(base))


def writer(llm: FakeLLM) -> ScenarioWriter:
    return ScenarioWriter(llm, "gemini-test")


def changed(good: dict, **changes) -> dict:
    return {**copy.deepcopy(good), **changes}


def group(key: str, schools: list[str], note: str = "") -> dict:
    return {"key": key, "schools": schools, "note": note}


# ---- dữ liệu gửi Gemini ----
def test_the_prompt_carries_names_reasons_and_effects_but_not_the_other_schools():
    user = build_user(req())

    assert user["label"] == "SAT 1500 (hiện tại 1290)"
    assert user["factorEffects"] == [FACTOR_EFFECT["sat"]]
    match = user["groups"][1]
    assert match["title"] == "Vừa sức" and match["more"] == 3
    assert match["schools"][0] == {
        "name": "Boston University",
        "movedFrom": "Khó vào",
        "why": [REASON_TEXT["SAT_ABOVE"]],
        "needAid": True,
    }
    assert user["student"] == {"budgetUsd": 40000, "scholarship": "nên có"}
    assert "Harvard" not in str(user)  # others chỉ dùng để kiểm tra, không gửi cho Gemini


def test_unknown_reason_codes_are_dropped():
    data = copy.deepcopy(REQUEST)
    data["groups"][1]["schools"][1]["reasons"] = ["MA_LA"]

    user = build_user(ScenarioNoteRequest.model_validate(data))

    assert "why" not in user["groups"][1]["schools"][1]


def test_allowed_numbers_are_the_label_budget_reasons_and_counts():
    allowed = allowed_numbers(req())

    assert {1500, 1290, 40000, 75, 3}.issubset(allowed)
    assert 1400 not in allowed


# ---- lời hợp lệ ----
async def test_a_reply_that_matches_the_data_becomes_one_message():
    llm = FakeLLM(GOOD)

    res = await writer(llm).write(req())

    assert res.status == "OK"
    assert res.answer.splitlines()[0] == GOOD["intro"]
    assert "- Khó vào: Cornell University, Duke University." in res.answer
    assert (
        "- Vừa sức: Boston University, Northeastern University. Boston University lên từ Khó vào"
        in res.answer
    )
    assert "- An toàn hơn: Rutgers University." in res.answer
    assert res.answer.endswith(GOOD["closing"])
    assert len(llm.calls) == 1 and llm.calls[0]["temperature"] == 0.0


async def test_the_order_of_schools_inside_a_group_does_not_matter():
    shuffled = copy.deepcopy(GOOD)
    shuffled["groups"][0]["schools"] = ["Duke University", "Cornell University"]

    assert (await writer(FakeLLM(shuffled)).write(req())).status == "OK"


async def test_no_change_gives_a_short_reply_without_lists():
    res = await writer(FakeLLM(NONE_GOOD)).write(req(NONE_REQUEST))

    assert res.status == "OK"
    assert "\n- " not in res.answer  # không có dòng danh sách
    assert "IELTS không làm đổi nhóm" in res.answer


# ---- chặn lời sai ----
BAD = {
    "wrong-group": changed(
        GOOD, groups=[group("reach", ["Cornell University", "Rutgers University"]), *GOOD["groups"][1:]]
    ),
    "missing-school": changed(GOOD, groups=[group("reach", ["Cornell University"]), *GOOD["groups"][1:]]),
    "missing-group": changed(GOOD, groups=GOOD["groups"][:2]),
    "invented-school": changed(
        GOOD, groups=[group("reach", ["Cornell University", "MIT"]), *GOOD["groups"][1:]]
    ),
    "invented-number": changed(
        GOOD, intro="Với SAT 1500, có 12 trường dễ vào hơn, bạn gần như chắc chắn đậu."
    ),
    "stranger-name": changed(GOOD, closing="Bạn cũng có thể nhắm tới Harvard University nhé."),
}


@pytest.mark.parametrize("bad", BAD.values(), ids=BAD.keys())
async def test_a_reply_that_breaks_the_data_is_rejected_then_retried_once(bad):
    llm = FakeLLM(bad, GOOD)

    res = await writer(llm).write(req())

    assert res.status == "OK" and "- Khó vào: Cornell University, Duke University." in res.answer
    assert len(llm.calls) == 2


async def test_groups_must_be_empty_when_nothing_changed():
    listed = changed(NONE_GOOD, groups=[group("reach", ["Harvard University"])])
    llm = FakeLLM(listed, NONE_GOOD)

    res = await writer(llm).write(req(NONE_REQUEST))

    assert res.status == "OK" and len(llm.calls) == 2


async def test_two_bad_replies_fail_so_backend_uses_its_own_sentence():
    bad = BAD["invented-number"]

    res = await writer(FakeLLM(bad, bad)).write(req())

    assert res.status == "FAILED" and res.answer == ""


async def test_the_number_check_ignores_thousand_and_decimal_formatting():
    ok = changed(GOOD, intro="Với ngân sách khoảng 40000 USD và SAT 1.500, danh sách khá hơn.")

    assert (await writer(FakeLLM(ok)).write(req())).status == "OK"


async def test_a_name_that_is_part_of_a_longer_valid_name_is_not_a_stranger():
    data = copy.deepcopy(REQUEST)
    data["groups"][0]["schools"][0]["name"] = "Washington University in St. Louis"
    data["others"] = ["Washington University"]
    good = copy.deepcopy(GOOD)
    good["groups"][0]["schools"][0] = "Washington University in St. Louis"

    res = await writer(FakeLLM(good)).write(ScenarioNoteRequest.model_validate(data))

    assert res.status == "OK"


async def test_an_empty_or_short_intro_fails_validation():
    llm = FakeLLM(changed(GOOD, intro="ok"), changed(GOOD, intro=""))

    assert (await writer(llm).write(req())).status == "FAILED"


async def test_a_gemini_error_is_retried_then_reported_as_failed():
    llm = FakeLLM(OllamaError("hết hạn mức"), OllamaError("hết hạn mức"))

    assert (await writer(llm).write(req())).status == "FAILED"


# ---- khuôn yêu cầu ----
def test_more_than_four_schools_in_a_group_are_refused():
    data = copy.deepcopy(REQUEST)
    data["groups"][0]["schools"] = [{"name": f"School {i}"} for i in range(5)]

    with pytest.raises(ValueError):
        ScenarioNoteRequest.model_validate(data)


def test_the_route_returns_the_message_with_the_skill_version():
    app = create_app()
    app.dependency_overrides[get_scenario_writer] = lambda: writer(FakeLLM(GOOD))

    res = TestClient(app).post("/api/v1/recommend/what-if", json=REQUEST)

    assert res.status_code == 200
    body = res.json()
    assert body["status"] == "OK" and "Cornell University, Duke University" in body["answer"]
    assert body["skill_version"] == "2.0" and body["model"] == "gemini-test"
