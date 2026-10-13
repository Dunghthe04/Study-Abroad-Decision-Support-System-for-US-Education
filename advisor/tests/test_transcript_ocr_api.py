import json
from fastapi.testclient import TestClient

from app.api.deps import get_transcript_reader
from app.core.config import Settings
from app.main import create_app
from app.services.transcript_reader import TranscriptReader


class FakeHttpClient:
    def __init__(self, mock_response_content: str | None = None):
        self.mock_response_content = mock_response_content

    async def post(self, *args, **kwargs):
        if self.mock_response_content:
            class MockResp:
                status_code = 200
                def __init__(self, content):
                    self._content = content
                def raise_for_status(self):
                    pass
                def json(self):
                    return {"message": {"content": self._content}}
            return MockResp(self.mock_response_content)
        raise RuntimeError("No Ollama in mock")

    async def get(self, *args, **kwargs):
        class MockTags:
            status_code = 200
            def json(self):
                return {"models": [{"name": "minicpm-v:latest"}]}
        return MockTags()


def make_client(mock_json: str | None = None) -> TestClient:
    app = create_app()
    reader = TranscriptReader(FakeHttpClient(mock_json), Settings())
    app.dependency_overrides[get_transcript_reader] = lambda: reader
    return TestClient(app)


def test_transcript_ocr_when_no_terms_recognized_returns_warning():
    client = make_client(mock_json=None)
    resp = client.post(
        "/api/v1/profile/transcript-ocr",
        json={
            "imageBase64": "dGVzdF9pbWFnZV9kYXRh",
            "fileName": "anh_bi_mo.png",
            "contentType": "image/png",
        },
    )

    assert resp.status_code == 200
    body = resp.json()
    assert body["success"] is False
    assert "warnings" in body
    assert len(body["warnings"]) > 0


def test_transcript_ocr_calculates_gpa4_as_scale10_divided_by_10_times_4():
    # Mocking Vision AI response with multiple terms (Lớp 10 HK1 & HK2)
    mock_data = json.dumps({
        "terms": [
            {
                "termName": "Lớp 10 HK1",
                "scores": [
                    {"subject": "Toán", "score": 9.0},
                    {"subject": "Văn", "score": 8.0}
                ]
            },
            {
                "termName": "Lớp 10 HK2",
                "scores": [
                    {"subject": "Toán", "score": 10.0},
                    {"subject": "Văn", "score": 9.0}
                ]
            }
        ]
    })
    client = make_client(mock_json=mock_data)

    resp = client.post(
        "/api/v1/profile/transcript-ocr",
        json={
            "files": [
                {
                    "imageBase64": "dGVzdF9pbWFnZV9kYXRh",
                    "fileName": "hoc_ba_lop_10.png",
                    "contentType": "image/png"
                }
            ]
        },
    )

    assert resp.status_code == 200
    body = resp.json()
    assert body["success"] is True
    assert len(body["terms"]) == 2

    # Điểm các môn: 9.0, 8.0, 10.0, 9.0 -> TB = 9.0
    # GPA thang 4 quy đổi: 9.0 / 10 * 4 = 3.60
    assert body["averageScoreScale10"] == 9.0
    assert body["calculatedGpa4"] == 3.60
