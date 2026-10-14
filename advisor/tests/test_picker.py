"""Lượt ③ chọn danh sách: kiểm tra khối, hạn mức, thứ tự X, gọi lại khi sai, endpoint /recommend/pick."""

import json

import pytest
from fastapi.testclient import TestClient

from app.api.deps import get_picker
from app.main import create_app
from app.schemas.pick import PickRequest, pick_schema
from app.services.ollama import OllamaError
from app.services.picker import Picker, PickError
from tests.fakes import FakeLLM

# Ví dụ trong mục 8 file AI_CACH_THUC_HIEN
EXAMPLE = {"pref": [2, 2, 2], "needAid": "preferred", "items": [
    {"c": "190099", "g2": "RF", "blk": "AID", "gap": 45610, "pg": 17.2, "ps": 0, "adj": 0, "r": 0.25},
    {"c": "B", "g2": "S", "blk": "MAIN", "F": 0.24, "H": 0.105, "Q": 0.8, "pg": 70, "ps": 87.5, "adj": 0},
    {"c": "C", "g2": "M", "blk": "MAIN", "F": 0.08, "H": 0.261, "Q": 0.9, "pg": 50, "ps": 58.33, "adj": 0}]}
EXAMPLE_RESULT = {
    "status": "OK", "reach": [],
    "match": [{"c": "C", "X": 0.414, "why": ["GPA_MID", "SAT_IN_RANGE", "BUDGET_OK"]}],
    "safety": [{"c": "B", "X": 0.382, "why": ["GPA_MID", "SAT_ABOVE", "BUDGET_OK"]}],
    "aid": [{"c": "190099", "X": None, "why": ["GPA_LOW", "SAT_BELOW", "NEED_AID", "INTL_LOW"]}],
    "other": []}


def request(data=EXAMPLE):
    return PickRequest.model_validate(data)


def entry(c, x=None):
    return {"c": c, "X": x, "why": []}


async def test_returns_blocks_and_drops_missing_keys():
    llm = FakeLLM(EXAMPLE_RESULT)
    res = await Picker(llm, "qwen3:8b").pick(request())

    assert [e.c for e in res.match] == ["C"] and [e.c for e in res.aid] == ["190099"]
    assert res.skill_version == "1.1"
    assert "SKILL CHỌN DANH SÁCH" in llm.calls[0]["messages"][0]["content"]
    sent = json.loads(llm.calls[0]["messages"][1]["content"])
    assert "F" not in sent["items"][0]          # Colgate không có F: bỏ key, không gửi null


async def test_changing_school_group_is_rejected():
    moved = {**EXAMPLE_RESULT, "match": [entry("B", 0.382), entry("C", 0.414)], "safety": []}   # B là S
    llm = FakeLLM(moved, EXAMPLE_RESULT)
    await Picker(llm, "m").pick(request())
    assert len(llm.calls) == 2


async def test_unknown_or_duplicate_code_is_rejected():
    dup = {**EXAMPLE_RESULT, "aid": [entry("190099")], "other": [entry("190099")]}
    llm = FakeLLM(dup, EXAMPLE_RESULT)
    await Picker(llm, "m").pick(request())
    assert len(llm.calls) == 2


async def test_aid_block_empty_when_student_does_not_need_aid():
    data = {**EXAMPLE, "needAid": "no"}
    with_aid = EXAMPLE_RESULT                                     # AI vẫn đưa Colgate vào aid: sai
    without_aid = {**EXAMPLE_RESULT, "aid": []}
    llm = FakeLLM(with_aid, without_aid)
    res = await Picker(llm, "m").pick(request(data))
    assert len(llm.calls) == 2 and res.aid == []


async def test_skipping_an_eligible_school_is_rejected():
    llm = FakeLLM({**EXAMPLE_RESULT, "match": []}, EXAMPLE_RESULT)   # bỏ sót C
    await Picker(llm, "m").pick(request())
    assert len(llm.calls) == 2


async def test_block_is_cut_at_limit_and_sorted_by_x():
    items = [{"c": f"M{i}", "g2": "M", "blk": "MAIN", "F": 0.1, "H": 0.1, "Q": 0.1} for i in range(7)]
    data = {"pref": [2, 2, 2], "needAid": "no", "items": items}
    good = {"status": "OK", "reach": [], "safety": [], "aid": [], "other": [],
            "match": [entry(f"M{i}", 0.9 - i / 10) for i in range(5)]}          # 7 trường → lấy 5
    unsorted = {**good, "match": [entry(f"M{i}", 0.1 + i / 10) for i in range(5)]}
    llm = FakeLLM(unsorted, good)
    res = await Picker(llm, "m").pick(request(data))
    assert len(llm.calls) == 2 and len(res.match) == 5


async def test_failing_twice_raises():
    llm = FakeLLM(OllamaError("timeout"), {"status": "OK"})
    with pytest.raises(PickError):
        await Picker(llm, "m").pick(request())


def test_schema_limits_blocks_and_codes():
    schema = pick_schema(["A", "B"])["properties"]
    assert (schema["reach"]["maxItems"], schema["match"]["maxItems"], schema["safety"]["maxItems"]) == (3, 5, 4)
    assert schema["match"]["items"]["properties"]["c"]["enum"] == ["A", "B"]


def test_endpoint_returns_blocks():
    app = create_app()
    app.dependency_overrides[get_picker] = lambda: Picker(FakeLLM(EXAMPLE_RESULT), "m")
    resp = TestClient(app).post("/recommend/pick", json=EXAMPLE)
    assert resp.status_code == 200
    assert resp.json()["safety"][0]["c"] == "B"


def test_endpoint_returns_502_when_ai_fails():
    app = create_app()
    app.dependency_overrides[get_picker] = lambda: Picker(FakeLLM(OllamaError("x"), OllamaError("x")), "m")
    assert TestClient(app).post("/recommend/pick", json=EXAMPLE).status_code == 502


def test_endpoint_rejects_pref_out_of_range():
    app = create_app()
    app.dependency_overrides[get_picker] = lambda: Picker(FakeLLM(), "m")
    assert TestClient(app).post("/recommend/pick", json={**EXAMPLE, "pref": [5, 2, 2]}).status_code == 422
