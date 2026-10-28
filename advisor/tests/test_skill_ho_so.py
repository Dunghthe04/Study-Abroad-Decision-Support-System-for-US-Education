import json

from app.services.ollama import OllamaError
from app.services.skills.skill_ho_so import HoSoRead, HoSoReader
from tests.fakes import FakeLLM


def reader(*answers):
    llm = FakeLLM(*answers)
    return HoSoReader(llm, "qwen3:8b"), llm


async def test_reads_gpa_and_guesses_scale():
    ho_so, llm = reader({"values": {"gpa": {"value": 8.5, "scale": None}}, "none_fields": []})
    res = await ho_so.read("D2", "GPA em 8.5 ạ", known={})

    assert res.values == {"gpa": {"value": 8.5, "scale": 10}}
    assert (res.ask_again, res.ai_used) == (None, True)
    sent = json.loads(llm.calls[0]["messages"][1]["content"])
    assert sent == {
        "branch": "DH",
        "question_id": "D2",
        "fields": ["gpa"],
        "message": "GPA em 8.5 ạ",
        "known": {},
    }


async def test_budget_vnd_gets_tier():
    ho_so, _ = reader({"values": {"budget": {"amount": 800_000_000, "currency": "VND"}}, "none_fields": []})
    res = await ho_so.read("F1", "tầm 800 triệu một năm", known={})

    assert res.values == {"budget": {"amount": 800_000_000, "currency": "VND", "tier": "tier_20k_35k"}}


async def test_wrong_value_not_written_and_ask_again():
    ho_so, _ = reader({"values": {"gpa": {"value": 85, "scale": None}}, "none_fields": []})
    res = await ho_so.read("D2", "GPA em 85", known={})

    assert res.values == {}
    assert "85" in res.ask_again


async def test_ielts_without_kind_asks_kind_but_keeps_other_values():
    answer = {
        "values": {
            "english": {"type": "ielts", "score": 6.5, "ielts_kind": None, "date": None},
            "gpa": {"value": 8.7, "scale": 10},
        },
        "none_fields": [],
    }
    ho_so, _ = reader(answer)
    res = await ho_so.read("E1", "IELTS 6.5, à GPA em 8.7", known={"gpa": {"value": 8.5, "scale": 10}})

    assert res.values == {"gpa": {"value": 8.7, "scale": 10}}  # sửa GPA vẫn được ghi
    assert "Academic hay General" in res.ask_again


async def test_question_being_asked_gets_ask_again_first():
    answer = {"values": {"gpa": {"value": 85, "scale": None}, "sat": 1455}, "none_fields": []}
    ho_so, _ = reader(answer)
    res = await ho_so.read("D3", "SAT 1455, GPA 85", known={})

    assert "SAT" in res.ask_again


async def test_said_none_goes_to_none_fields():
    ho_so, _ = reader({"values": {}, "none_fields": ["sat", "act", "sat"]})
    res = await ho_so.read("D3", "em chưa thi SAT hay ACT", known={})

    assert (res.values, res.none_fields) == ({}, ["sat", "act"])


async def test_activities_checked_by_validators():
    act = {"name": "CLB Robotics", "role": "lead", "scope": 1, "months": 24}
    ho_so, _ = reader({"values": {"activities": [act]}, "none_fields": []})
    res = await ho_so.read("D4", "Em làm đội trưởng CLB Robotics trường 2 năm", known={})

    assert res.values == {"activities": [act]}


async def test_wrong_shape_twice_returns_empty_read():
    sai = {"values": {"gpa": "tám rưỡi"}, "none_fields": []}
    ho_so, llm = reader(sai, sai)
    res = await ho_so.read("D2", "GPA em tám rưỡi", known={})

    assert res == HoSoRead()
    assert len(llm.calls) == 2


async def test_ollama_error_returns_empty_read():
    ho_so, _ = reader(OllamaError("tắt máy"), OllamaError("tắt máy"))
    res = await ho_so.read("F1", "800 triệu", known={})

    assert res == HoSoRead()
