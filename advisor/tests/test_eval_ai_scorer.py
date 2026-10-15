"""Thực nghiệm AI tự chấm trường (evals): chia lô, kiểm tra JSON, gọi lại khi sai."""

import json

from app.services.ollama import OllamaError
from app.services.skill_call import load_skill
from evals.recommendation.ai_score_schema import ScoreRequest, score_schema
from evals.recommendation.ai_scorer import SKILL_PATH, Scorer
from tests.fakes import FakeLLM

STUDENT = {"gpa": 3.6, "sat": 1350, "budget": 50000, "tier": 3}
COLGATE = {
    "c": "190099",
    "acc": 0.175,
    "iacc": 0.044,
    "sat": [1450, 1510],
    "subm": 0.23,
    "satp": "opt",
    "gpa": [0, 0, 0.02, 0.06, 0.23, 0.38, 0.31],
    "avg": 3.85,
    "L": [3, 2, 2],
    "coa": 95610,
    "aid": 86649,
    "q": 0.905,
}


def school(code):
    return {"c": code, "acc": 0.5, "sat": [1200, 1400], "L": [3, 2, 1]}


def item(code, **over):
    """Một kết quả hợp lệ (số của Colgate trong tài liệu), đổi mã và ghi đè key khi cần."""
    base = {
        "c": code,
        "w": [0.75, 0.25],
        "pg": 17.2,
        "conf": "H",
        "ps": 0,
        "A": 12.9,
        "adj": 0,
        "S": 12.9,
        "g": "R",
        "r": 0.25,
        "g2": "RF",
        "F": 0,
        "H": 0.906,
        "Q": 0.905,
        "gap": 45610,
    }
    return {**base, **over}


def request(*schools):
    return ScoreRequest.model_validate({"student": STUDENT, "schools": list(schools)})


async def test_sends_skill_as_system_and_compact_json_as_user():
    llm = FakeLLM({"items": [item("190099")]})
    res = await Scorer(llm, "qwen3:8b").score(request(COLGATE))

    call = llm.calls[0]
    assert call["messages"][0]["role"] == "system"
    assert "SKILL CHẤM TRƯỜNG" in call["messages"][0]["content"]
    user = call["messages"][1]["content"]
    assert " " not in user.split('"schools"')[0]  # JSON viết liền, không khoảng trắng
    assert json.loads(user)["schools"][0]["c"] == "190099"
    assert call["temperature"] == 0
    assert res.items[0].g2 == "RF" and res.failed == []
    assert res.skill_version == "1.1" and res.model == "qwen3:8b"


async def test_missing_values_are_dropped_not_sent_as_null():
    llm = FakeLLM({"items": [item("X")]})
    await Scorer(llm, "m").score(request({"c": "X", "L": [3, 2, 1]}))
    sent = json.loads(llm.calls[0]["messages"][1]["content"])["schools"][0]
    assert sent == {"c": "X", "L": [3, 2, 1]}  # không có "iacc": null...


async def test_splits_into_batches_of_eight():
    schools = [school(f"S{i:02d}") for i in range(20)]
    answers = [{"items": [item(s["c"]) for s in schools[i : i + 8]]} for i in (0, 8, 16)]
    llm = FakeLLM(*answers)
    res = await Scorer(llm, "m").score(request(*schools))

    assert len(llm.calls) == 3  # 8 + 8 + 4
    assert [len(json.loads(c["messages"][1]["content"])["schools"]) for c in llm.calls] == [8, 8, 4]
    assert [i.c for i in res.items] == [s["c"] for s in schools]


async def test_retries_once_when_codes_do_not_match():
    llm = FakeLLM({"items": [item("WRONG")]}, {"items": [item("190099")]})
    res = await Scorer(llm, "m").score(request(COLGATE))
    assert len(llm.calls) == 2
    assert res.items[0].c == "190099" and res.failed == []


async def test_retries_once_when_number_out_of_range():
    llm = FakeLLM({"items": [item("190099", pg=140)]}, {"items": [item("190099")]})
    res = await Scorer(llm, "m").score(request(COLGATE))
    assert len(llm.calls) == 2 and res.items[0].pg == 17.2


async def test_batch_failing_twice_is_marked_failed_not_invented():
    llm = FakeLLM(OllamaError("timeout"), {"items": []}, {"items": [item("B")]})
    res = await Scorer(llm, "m", batch_size=1).score(request(COLGATE, school("B")))
    assert res.failed == ["190099"]  # lô 1 lỗi 2 lần
    assert [i.c for i in res.items] == ["B"]  # lô 2 vẫn chấm bình thường


async def test_adj_out_of_range_is_rejected():
    llm = FakeLLM({"items": [item("190099", adj=8)]}, {"items": [item("190099", adj=8)]})
    res = await Scorer(llm, "m").score(request(COLGATE))
    assert res.failed == ["190099"]


def test_schema_fixes_codes_count_and_key_order():
    schema = score_schema(["A", "B"])
    items = schema["properties"]["items"]
    assert items["minItems"] == items["maxItems"] == 2
    assert items["items"]["properties"]["c"]["enum"] == ["A", "B"]
    # thứ tự key = thứ tự model tính
    assert list(items["items"]["properties"])[:11] == [
        "c",
        "w",
        "pg",
        "conf",
        "ps",
        "A",
        "adj",
        "S",
        "g",
        "r",
        "g2",
    ]


def test_skill_file_has_version():
    text, version = load_skill(SKILL_PATH)
    assert version == "1.1"
    assert "Bước 6" in text
