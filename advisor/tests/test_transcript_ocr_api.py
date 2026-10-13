from fastapi.testclient import TestClient

from app.api.deps import get_transcript_reader
from app.core.config import Settings
from app.main import create_app
from app.services.transcript_reader import TranscriptReader


class FakeHttpClient:
    async def post(self, *args, **kwargs):
        raise RuntimeError("No Ollama in mock")

    async def get(self, *args, **kwargs):
        raise RuntimeError("No Ollama in mock")


def make_client() -> TestClient:
    app = create_app()
    reader = TranscriptReader(FakeHttpClient(), Settings())
    app.dependency_overrides[get_transcript_reader] = lambda: reader
    return TestClient(app)


def test_transcript_ocr_fallback_template():
    client = make_client()
    resp = client.post(
        "/api/v1/profile/transcript-ocr",
        json={
            "imageBase64": "dGVzdF9pbWFnZV9kYXRh",
            "fileName": "test_hoc_ba.png",
            "contentType": "image/png",
        },
    )

    assert resp.status_code == 200
    body = resp.json()
    assert "terms" in body
    assert len(body["terms"]) > 0
    assert body["terms"][0]["termName"] == "Lớp 10 Học kỳ 1"
    assert len(body["terms"][0]["scores"]) >= 6
    assert body["calculatedGpa4"] is not None
    assert body["calculatedGpa4"] > 0
