import copy

from fastapi.testclient import TestClient

from app.api.deps import get_recommender
from app.main import create_app
from app.services.ollama import OllamaError
from app.services.recommender import Recommender
from tests.fakes import FakeLLM

URL = "/api/v1/recommendations/explain"

# Đúng dạng JSON mà .NET (RecommendationAiClient) gửi sang: camelCase
SAMPLE = {
    "student": {
        "studyLevel": "undergraduate",
        "major": "Computer Science",
        "gpa4": 3.6,
        "sat": 1380,
        "annualBudgetUsd": 60000,
        "ielts": 7.0,
        "toefl": None,
        "duolingo": None,
        "extracurricularScore": 7.5,
    },
    "schools": [
        {
            "code": "UW",
            "name": "University of Washington",
            "state": "WA",
            "category": "match",
            "avgGpa4": 3.7,
            "sat25": 1250,
            "sat75": 1480,
            "tuitionUsd": 41997,
            "totalCostUsd": 57168,
            "english": "met",
        },
        {
            "code": "ASU",
            "name": "Arizona State University",
            "state": "AZ",
            "category": "safety",
            "avgGpa4": 3.5,
            "sat25": 1120,
            "sat75": 1360,
            "tuitionUsd": 33666,
            "totalCostUsd": 52000,
            "english": "met",
        },
    ],
}

ASU_FIRST = {"items": [{"code": "ASU", "reason": "Lý do ASU"}, {"code": "UW", "reason": "Lý do UW"}]}


def make_client(llm: FakeLLM) -> TestClient:
    """App thật nhưng Recommender dùng LLM giả (giống thay service trong DI khi test .NET)."""
    app = create_app()
    app.dependency_overrides[get_recommender] = lambda: Recommender(llm)
    return TestClient(app)


def test_explain_returns_llm_order_and_reasons():
    resp = make_client(FakeLLM(ASU_FIRST)).post(URL, json=SAMPLE)

    assert resp.status_code == 200
    assert resp.json() == ASU_FIRST


def test_explain_rejects_unknown_category():
    bad = copy.deepcopy(SAMPLE)
    bad["schools"][0]["category"] = "dream"

    resp = make_client(FakeLLM()).post(URL, json=bad)

    assert resp.status_code == 422


def test_explain_rejects_empty_school_list():
    bad = copy.deepcopy(SAMPLE)
    bad["schools"] = []

    resp = make_client(FakeLLM()).post(URL, json=bad)

    assert resp.status_code == 422


def test_explain_returns_502_when_llm_returns_wrong_shape():
    resp = make_client(FakeLLM({"answer": "xin chào"})).post(URL, json=SAMPLE)

    assert resp.status_code == 502


def test_explain_returns_502_when_ollama_is_down():
    resp = make_client(FakeLLM(OllamaError("connection refused"))).post(URL, json=SAMPLE)

    assert resp.status_code == 502
