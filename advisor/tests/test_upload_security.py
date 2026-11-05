"""Bảo mật đường OCR bảng điểm: khoá API, giới hạn request, chống ảnh/PDF "bom"."""

import io

import pytest
from fastapi.testclient import TestClient
from PIL import Image

from app.api.deps import get_transcript_reader
from app.core.config import Settings, get_settings
from app.main import create_app
from app.schemas.transcript_ocr import MAX_BASE64_CHARS, MAX_FILES
from app.services import transcript_reader as tr
from app.services.transcript_reader import TranscriptReader
from tests.test_transcript_ocr_api import FakeOllama, _png_base64

OCR_URL = "/api/v1/profile/transcript-ocr"


def make_client(api_key: str = "") -> tuple[TestClient, FakeOllama]:
    fake = FakeOllama()
    settings = Settings(_env_file=None, api_key=api_key)  # type: ignore[call-arg]
    app = create_app()
    app.dependency_overrides[get_settings] = lambda: settings
    app.dependency_overrides[get_transcript_reader] = lambda: TranscriptReader(fake, settings)  # type: ignore[arg-type]
    return TestClient(app), fake


# ---------- Khoá API ----------


@pytest.mark.parametrize("headers", [{}, {"X-Api-Key": "sai-khoa"}])
def test_missing_or_wrong_api_key_is_rejected(headers: dict):
    client, fake = make_client(api_key="khoa-test")

    resp = client.post(OCR_URL, json={"files": []}, headers=headers)

    assert resp.status_code == 401
    assert fake.calls == []


def test_correct_api_key_is_accepted():
    client, _ = make_client(api_key="khoa-test")

    resp = client.post(OCR_URL, json={"files": []}, headers={"X-Api-Key": "khoa-test"})

    assert resp.status_code == 200


def test_empty_api_key_setting_skips_check_for_local_dev():
    client, _ = make_client(api_key="")

    assert client.post(OCR_URL, json={"files": []}).status_code == 200


def test_health_stays_open_without_api_key():
    client, _ = make_client(api_key="khoa-test")

    assert client.get("/health/live").status_code == 200


# ---------- Giới hạn request ----------


def test_more_than_max_files_is_rejected():
    client, fake = make_client()
    file = {"imageBase64": _png_base64(10, 10), "fileName": "a.png", "contentType": "image/png"}

    resp = client.post(OCR_URL, json={"files": [file] * (MAX_FILES + 1)})

    assert resp.status_code == 422
    assert fake.calls == []


def test_oversized_base64_is_rejected():
    client, fake = make_client()

    resp = client.post(OCR_URL, json={"imageBase64": "A" * (MAX_BASE64_CHARS + 1), "fileName": "a.png"})

    assert resp.status_code == 422
    assert fake.calls == []


# ---------- Bom ảnh / PDF ----------


def _png(width: int, height: int) -> bytes:
    buf = io.BytesIO()
    Image.new("1", (width, height)).save(buf, format="PNG")
    return buf.getvalue()


def test_image_with_too_many_pixels_is_not_sent_to_ollama(monkeypatch: pytest.MonkeyPatch):
    # Hạ ngưỡng để test nhanh; ảnh thật ~ 1 bit/pixel nén rất nhỏ nhưng giải nén ra cực lớn
    monkeypatch.setattr(tr, "MAX_IMAGE_PIXELS", 1_000_000)
    reader = TranscriptReader(FakeOllama(), Settings(_env_file=None))  # type: ignore[arg-type,call-arg]

    assert reader._prepare_image(_png(2000, 1000)) is None


def test_pillow_decompression_bomb_is_not_sent_raw(monkeypatch: pytest.MonkeyPatch):
    # Trước đây lỗi DecompressionBombError rơi vào nhánh "gửi nguyên ảnh cho model"
    monkeypatch.setattr(tr, "MAX_IMAGE_PIXELS", 10**12)
    monkeypatch.setattr(Image, "MAX_IMAGE_PIXELS", 100_000)
    reader = TranscriptReader(FakeOllama(), Settings(_env_file=None))  # type: ignore[arg-type,call-arg]

    assert reader._prepare_image(_png(1000, 1000)) is None


def test_unreadable_large_file_is_not_sent_raw():
    reader = TranscriptReader(FakeOllama(), Settings(_env_file=None))  # type: ignore[arg-type,call-arg]

    assert reader._prepare_image(b"\0" * (tr.MAX_RAW_IMAGE_BYTES + 1)) is None


def test_bomb_image_returns_warning_without_calling_ollama(monkeypatch: pytest.MonkeyPatch):
    monkeypatch.setattr(tr, "MAX_IMAGE_PIXELS", 1_000)
    client, fake = make_client()

    resp = client.post(
        OCR_URL,
        json={
            "files": [
                {"imageBase64": _png_base64(100, 100), "fileName": "bom.png", "contentType": "image/png"}
            ]
        },
    )

    body = resp.json()
    assert resp.status_code == 200
    assert body["success"] is False
    assert any("quá lớn" in w for w in body["warnings"])
    assert not [c for c in fake.calls if c["messages"][0].get("images")]


def _pdf_with_page(width_pt: float, height_pt: float) -> bytes:
    import pypdfium2 as pdfium

    pdf = pdfium.PdfDocument.new()
    pdf.new_page(width_pt, height_pt)
    buf = io.BytesIO()
    pdf.save(buf)
    pdf.close()
    return buf.getvalue()


def test_huge_pdf_page_is_rendered_down_to_max_side():
    reader = TranscriptReader(FakeOllama(), Settings(_env_file=None, image_max_side=1000))  # type: ignore[arg-type,call-arg]

    [page] = reader._render_pdf_pages(_pdf_with_page(14400, 14400))

    assert max(Image.open(io.BytesIO(page)).size) <= 1000


def test_tiny_pdf_page_is_enlarged_at_most_max_scale():
    reader = TranscriptReader(FakeOllama(), Settings(_env_file=None, image_max_side=2000))  # type: ignore[arg-type,call-arg]

    [page] = reader._render_pdf_pages(_pdf_with_page(10, 10))

    assert max(Image.open(io.BytesIO(page)).size) <= 10 * tr.MAX_PDF_RENDER_SCALE
