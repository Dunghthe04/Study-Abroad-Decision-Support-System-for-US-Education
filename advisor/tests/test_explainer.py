"""AI tư vấn từng trường: gửi số đã chấm (bỏ key thiếu), nhận đoạn tư vấn, endpoint /recommend/explain."""

import json

from fastapi.testclient import TestClient

from app.api.deps import get_explainer
from app.main import create_app
from app.schemas.explain import ExplainRequest, explain_schema
from app.services.explainer import Explainer
from app.services.ollama import OllamaError
from tests.fakes import FakeLLM

REQ = {
    "student": {"gpa": 3.6, "sat": 1350, "budget": 50000, "ec_tier": 3},
    "school": {
        "name": "Colgate University",
        "control": "private",
        "group": "Khó vào xa",
        "need_aid": True,
        "sat25": 1450,
        "sat75": 1510,
        "gpa_position": 17.2,
        "sat_position": 0,
        "cost": 95610,
        "over_budget": 45610,
    },
}
ANSWER = {
    "summary": "Colgate là trường rất khó vào với hồ sơ hiện tại của bạn.",
    "strengths": [],
    "improvements": ["Nâng SAT lên khoảng 1450."],
}


async def test_sends_skill_and_only_known_numbers():
    llm = FakeLLM(ANSWER)
    res = await Explainer(llm, "qwen3:8b").explain(ExplainRequest.model_validate(REQ))

    system, user = llm.calls[0]["messages"]
    assert "SKILL TƯ VẤN TỪNG TRƯỜNG" in system["content"]
    sent = json.loads(user["content"])
    assert "acceptance" not in sent["school"] and "sat" in sent["student"]  # thiếu số thì bỏ key
    assert res.summary.startswith("Colgate") and res.improvements == ["Nâng SAT lên khoảng 1450."]
    assert (res.skill_version, res.model) == ("1.1", "qwen3:8b")
    assert "SAT 1350 thấp hơn mốc 25% của trường (1450), thiếu khoảng 100 điểm" in sent["facts"]


async def test_bad_answer_is_retried():
    llm = FakeLLM({"summary": "ngắn"}, ANSWER)  # tóm tắt quá ngắn, thiếu key → gọi lại
    res = await Explainer(llm, "m").explain(ExplainRequest.model_validate(REQ))
    assert len(llm.calls) == 2 and res.summary == ANSWER["summary"]


def test_schema_limits_lists():
    schema = explain_schema()
    assert schema["required"] == ["summary", "strengths", "improvements"]
    assert schema["properties"]["improvements"]["maxItems"] == 3


def test_endpoint_returns_advice():
    app = create_app()
    app.dependency_overrides[get_explainer] = lambda: Explainer(FakeLLM(ANSWER), "m")
    resp = TestClient(app).post("/recommend/explain", json=REQ)
    assert resp.status_code == 200 and resp.json()["summary"] == ANSWER["summary"]


def test_endpoint_returns_502_when_ai_fails():
    app = create_app()
    app.dependency_overrides[get_explainer] = lambda: Explainer(
        FakeLLM(OllamaError("x"), OllamaError("x")), "m"
    )
    assert TestClient(app).post("/recommend/explain", json=REQ).status_code == 502


def test_facts_compare_numbers_in_code():
    from app.services.explainer import build_facts

    req = ExplainRequest.model_validate(
        {
            "student": {"gpa": 3.8, "sat": 1400, "budget": 70000, "ec_tier": 2},
            "school": {
                "name": "Penn State",
                "group": "An toàn hơn",
                "sat25": 1280,
                "sat75": 1410,
                "gpa_position": 80,
                "cost": 67828,
                "ec_adjustment": -1,
            },
        }
    )
    facts = build_facts(req)
    assert (
        "SAT 1400 nằm trong khoảng 25–75 của trường (1280–1410)" in facts
    )  # 1400 < 1410: không phải "cao hơn"
    assert any("GPA 3.8 cao hơn phần lớn" in f for f in facts)
    assert any("dưới trung bình" in f and "làm hồ sơ yếu đi" in f for f in facts)
    assert any("nằm trong ngân sách" in f for f in facts)
