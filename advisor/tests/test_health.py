from fastapi.testclient import TestClient

from app.main import create_app


def test_live_returns_ok():
    resp = TestClient(create_app()).get("/health/live")

    assert resp.status_code == 200
    assert resp.json() == {"status": "ok"}
