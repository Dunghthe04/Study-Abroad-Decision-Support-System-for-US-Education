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


async def test_budget_vnd_saved_as_tier_only():
    ho_so, _ = reader({"values": {"budget": {"amount": 800_000_000, "currency": "VND"}}, "none_fields": []})
    res = await ho_so.read("F1", "tầm 800 triệu một năm", known={})

    assert res.values == {"budget": "tier_20k_35k"}


async def test_budget_tier_button():
    ho_so, _ = reader({"values": {"budget": {"tier": "tier_above_70k"}}, "none_fields": []})
    res = await ho_so.read("F1", "tier_above_70k", known={})

    assert res.values == {"budget": "tier_above_70k"}


async def test_budget_without_amount_or_tier_asks_again():
    ho_so, _ = reader({"values": {"budget": {"currency": "VND"}}, "none_fields": []})
    res = await ho_so.read("F1", "tiền Việt", known={})

    assert res.values == {}
    assert "bao nhiêu" in res.ask_again


async def test_english_plan_without_month():
    ho_so, _ = reader({"values": {"english_plan": {"planned": True, "date": None}}, "none_fields": []})
    res = await ho_so.read("E2", "em có định thi nhưng chưa biết khi nào", known={})

    assert res.values == {"english_plan": {"planned": True, "date": None}}


async def test_wrong_value_not_written_and_ask_again():
    ho_so, _ = reader({"values": {"gpa": {"value": 85, "scale": None}}, "none_fields": []})
    res = await ho_so.read("D2", "GPA em 85", known={})

    assert res.values == {}
    assert "85" in res.ask_again


async def test_wrong_ielts_asks_again_but_keeps_other_values():
    answer = {
        "values": {
            "english": {"type": "ielts", "score": 6.3, "date": None},
            "gpa": {"value": 8.7, "scale": 10},
        },
        "none_fields": [],
    }
    ho_so, _ = reader(answer)
    res = await ho_so.read("E1", "IELTS 6.3, à GPA em 8.7", known={"gpa": {"value": 8.5, "scale": 10}})

    assert res.values == {"gpa": {"value": 8.7, "scale": 10}}  # sửa GPA vẫn được ghi
    assert "IELTS" in res.ask_again


async def test_question_being_asked_gets_ask_again_first():
    answer = {"values": {"gpa": {"value": 85, "scale": None}, "sat": 1455}, "none_fields": []}
    ho_so, _ = reader(answer)
    res = await ho_so.read("D3", "SAT 1455, GPA 85", known={})

    assert "SAT" in res.ask_again


async def test_said_none_goes_to_none_fields():
    ho_so, _ = reader({"values": {}, "none_fields": ["sat", "act", "sat"]})
    res = await ho_so.read("D3", "em chưa thi SAT hay ACT", known={})

    assert (res.values, res.none_fields) == ({}, ["sat", "act"])


async def test_activities_saved_in_form_shape():
    act = {
        "name": "CLB Robotics",
        "role": "leader",
        "impact_level": "school",
        "start_date": "09/2023",
        "end_date": None,
        "is_ongoing": True,
    }
    ho_so, _ = reader({"values": {"activities": [act]}, "none_fields": []})
    res = await ho_so.read("D4", "Em làm chủ nhiệm CLB Robotics trường từ 09/2023 đến nay", known={})

    assert res.values == {
        "activities": [
            {
                "name": "CLB Robotics",
                "category": "activity",
                "role": "leader",
                "impactLevel": "school",
                "startDate": "09/2023",
                "endDate": None,
                "isOngoing": True,
            }
        ]
    }


async def test_activity_without_role_asks_role():
    act = {"name": "CLB Robotics", "impact_level": "school", "start_date": "09/2023", "is_ongoing": True}
    ho_so, _ = reader({"values": {"activities": [act]}, "none_fields": []})
    res = await ho_so.read("D4", "Em tham gia CLB Robotics từ 09/2023", known={})

    assert res.values == {}
    assert "sáng lập" in res.ask_again


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
