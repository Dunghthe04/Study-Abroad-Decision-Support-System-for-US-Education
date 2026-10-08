from fastapi.testclient import TestClient

from app.api.deps import get_activity_reader
from app.main import create_app
from app.services.activity_reader import ActivityReader
from tests.fakes import FakeLLM

URL = "/api/v1/profile/extracurricular"
ACTIVITY = {"id": "a1", "name": "CLB Robotics", "role": "Chủ nhiệm", "impactLevel": 3, "months": 24}


def make_client(llm: FakeLLM) -> TestClient:
    app = create_app()
    app.dependency_overrides[get_activity_reader] = lambda: ActivityReader(llm)
    return TestClient(app)


def test_returns_score_in_camel_case():
    llm = FakeLLM({"activities": [{"id": "a1", "role": "head", "reputable_org": False, "impact_level": 3}]})

    resp = make_client(llm).post(URL, json={"activities": [ACTIVITY]})

    assert resp.status_code == 200
    body = resp.json()
    assert body["aiUsed"] is True
    assert body["activities"][0]["reputableOrg"] is False
    assert 0 < body["score"] <= 4


def test_rejects_impact_level_out_of_range():
    resp = make_client(FakeLLM()).post(URL, json={"activities": [{**ACTIVITY, "impactLevel": 6}]})

    assert resp.status_code == 422
