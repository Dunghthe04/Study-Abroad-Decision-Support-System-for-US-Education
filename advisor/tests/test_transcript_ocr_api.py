import asyncio
import base64
import io
import json
from contextlib import asynccontextmanager

import httpx
from fastapi.testclient import TestClient
from PIL import Image

from app.api.deps import get_transcript_reader
from app.core.config import Settings
from app.main import create_app
from app.schemas.transcript_ocr import TranscriptFilePayload, TranscriptOcrRequest
from app.services.transcript_reader import TranscriptReader, _FileResult, _RawTerm

HIGH_SCHOOL_MARKDOWN = """BẢN TRÍCH SAO KẾT QUẢ HỌC TẬP
Lớp 10
| | Toán | Lý | Văn | Thể dục | TB | HL |
|---|---|---|---|---|---|---|
| HK1 | 7,2 | 4,2 | 6,7 | Đ | 6,0 | TB |
| HK2 | 7,5 | 7,8 | 7,1 | Đ | 7,4 | Khá |
| Cả năm | 7,4 | 6,6 | 7,0 | Đ | 7,0 | Khá |
"""

HIGH_SCHOOL_PARSED = {
    "scale": "10",
    "terms": [
        {
            "grade": "Lớp 10",
            "term": "HK1",
            "subjects": [
                {"name": "Toán", "score": "7,2"},
                {"name": "Lý", "score": "4,2"},
                {"name": "Văn", "score": "6,7"},
                {"name": "Thể dục", "score": "Đ"},
            ],
        },
        {
            "grade": "Lớp 10",
            "term": "HK2",
            "subjects": [
                {"name": "Toán", "score": "7,5"},
                {"name": "Lý", "score": "7,8"},
                {"name": "Văn", "score": "7,1"},
            ],
        },
        {"grade": "Lớp 10", "term": "Cả năm", "subjects": [{"name": "Toán", "score": "7,4"}]},
    ],
}

UNIVERSITY_MARKDOWN = """| Term | Course | Score |
|---|---|---|
| Fall2023 | Mathematics | 8,2 |
| Spring2024 | Physics | 7,0 |
| Summer2024 | English | 9,1 |
| Fall2024 | Chemistry | 6,5 |
"""

UNIVERSITY_PARSED = {
    "scale": "10",
    "terms": [
        {"grade": "", "term": "Fall2023", "subjects": [{"name": "Mathematics", "score": "8,2"}]},
        {"grade": "", "term": "Spring2024", "subjects": [{"name": "Physics", "score": "7,0"}]},
        {"grade": "", "term": "Summer2024", "subjects": [{"name": "English", "score": "9,1"}]},
        {"grade": "", "term": "Fall2024", "subjects": [{"name": "Chemistry", "score": "6,5"}]},
    ],
}


def _response(status: int, body: dict | None = None, text: str = "") -> httpx.Response:
    request = httpx.Request("POST", "http://ollama/api/chat")
    if body is not None:
        return httpx.Response(status, json=body, request=request)
    return httpx.Response(status, text=text, request=request)


class FakeOllama:
    """Ollama giả: bước A (có ảnh) trả Markdown soạn sẵn, bước B (có format) trả JSON soạn sẵn."""

    def __init__(
        self,
        markdown: str = "",
        parsed: dict | None = None,
        models: tuple[str, ...] = ("glm-ocr:latest", "qwen3:4b"),
        vision_errors: int = 0,
        vision_error_text: str = '{"error":"model runner has unexpectedly stopped"}',
        stream_error: str = "",
    ) -> None:
        self.vision_error_text = vision_error_text
        self.stream_error = stream_error
        self.markdown = markdown
        self.parsed = parsed
        self.models = models
        self.vision_errors = vision_errors
        self.calls: list[dict] = []

    async def post(self, url: str, json: dict, timeout: float | None = None) -> httpx.Response:
        self.calls.append(json)
        if json["messages"][0].get("images"):
            if self.vision_errors > 0:
                self.vision_errors -= 1
                return _response(500, text=self.vision_error_text)
            return _response(200, {"message": {"content": self.markdown}})
        content = "" if self.parsed is None else _dumps(self.parsed)
        return _response(200, {"message": {"content": content}})

    @asynccontextmanager
    async def stream(self, method: str, url: str, json: dict, timeout: float | None = None):
        """Bước A gửi stream=True: trả từng dòng JSON như Ollama, lỗi giữa chừng thì thêm dòng {"error": ...}."""
        resp = await self.post(url, json=json, timeout=timeout)
        if resp.status_code == 200:
            lines = [_dumps({"message": {"content": line + "\n"}}) for line in self.markdown.splitlines()]
            if self.stream_error:
                lines.append(_dumps({"error": self.stream_error}))
            resp = httpx.Response(200, text="\n".join(lines), request=resp.request)
        yield resp

    async def get(self, url: str, timeout: float | None = None) -> httpx.Response:
        return httpx.Response(200, json={"models": [{"name": m} for m in self.models]})


def _dumps(value: dict) -> str:
    return json.dumps(value, ensure_ascii=False)


def _png_base64(width: int = 300, height: int = 200) -> str:
    buf = io.BytesIO()
    Image.new("RGB", (width, height), "white").save(buf, format="PNG")
    return base64.b64encode(buf.getvalue()).decode()


def make_client(fake: FakeOllama, **settings) -> TestClient:
    app = create_app()
    # Bỏ qua advisor/.env để kết quả test không phụ thuộc máy đang chạy
    settings = {"llm_model": "qwen3:8b", "transcript_parse_model": "qwen3:4b", **settings}
    reader = TranscriptReader(fake, Settings(_env_file=None, **settings))  # type: ignore[arg-type,call-arg]
    app.dependency_overrides[get_transcript_reader] = lambda: reader
    return TestClient(app)


def post_image(client: TestClient, name: str = "hoc_ba.png") -> dict:
    resp = client.post(
        "/api/v1/profile/transcript-ocr",
        json={"files": [{"imageBase64": _png_base64(), "fileName": name, "contentType": "image/png"}]},
    )
    assert resp.status_code == 200
    return resp.json()


def scores_by_term(body: dict) -> dict[str, dict[str, float]]:
    return {t["termName"]: {s["subject"]: s["score"] for s in t["scores"]} for t in body["terms"]}


def test_high_school_layout_reads_each_semester_and_skips_whole_year_and_pe():
    body = post_image(make_client(FakeOllama(HIGH_SCHOOL_MARKDOWN, HIGH_SCHOOL_PARSED)))

    assert body["success"] is True
    assert scores_by_term(body) == {
        "Lớp 10 HK1": {"Toán": 7.2, "Vật lý": 4.2, "Ngữ văn": 6.7},
        "Lớp 10 HK2": {"Toán": 7.5, "Vật lý": 7.8, "Ngữ văn": 7.1},
    }
    # TB = (7.2+4.2+6.7+7.5+7.8+7.1)/6 = 6.75 -> GPA4 = 2.7
    assert body["averageScoreScale10"] == 6.75
    assert body["calculatedGpa4"] == 2.7
    assert body["modelUsed"] == "glm-ocr:latest + qwen3:4b"


def test_empty_parse_model_falls_back_to_llm_model():
    fake = FakeOllama(HIGH_SCHOOL_MARKDOWN, HIGH_SCHOOL_PARSED)
    body = post_image(make_client(fake, transcript_parse_model=""))

    parse_call = next(c for c in fake.calls if "format" in c)
    assert parse_call["model"] == "qwen3:8b"
    assert body["modelUsed"] == "glm-ocr:latest + qwen3:8b"


def test_university_portal_maps_seasons_to_years_and_summer_joins_spring():
    body = post_image(make_client(FakeOllama(UNIVERSITY_MARKDOWN, UNIVERSITY_PARSED)))

    assert scores_by_term(body) == {
        "Năm 1 HK1": {"Toán": 8.2},
        "Năm 1 HK2": {"Vật lý": 7.0, "Tiếng Anh": 9.1},
        "Năm 2 HK1": {"Hóa học": 6.5},
    }
    assert [t["termName"] for t in body["terms"]] == ["Năm 1 HK1", "Năm 1 HK2", "Năm 2 HK1"]


def test_score_not_in_transcribed_text_is_dropped_with_warning():
    parsed = {
        "scale": "10",
        "terms": [
            {
                "grade": "Lớp 10",
                "term": "HK1",
                "subjects": [
                    {"name": "Toán", "score": "7,2"},
                    {"name": "Hóa", "score": "9,9"},
                ],
            }
        ],
    }
    body = post_image(make_client(FakeOllama(HIGH_SCHOOL_MARKDOWN, parsed)))

    assert scores_by_term(body) == {"Lớp 10 HK1": {"Toán": 7.2}}
    assert any("Hóa học 9,9" in w for w in body["warnings"])


def test_vision_500_retries_once_on_cpu():
    fake = FakeOllama(HIGH_SCHOOL_MARKDOWN, HIGH_SCHOOL_PARSED, vision_errors=1)
    body = post_image(make_client(fake))

    assert body["success"] is True
    vision_calls = [c for c in fake.calls if c["messages"][0].get("images")]
    assert len(vision_calls) == 2
    assert "num_gpu" not in vision_calls[0]["options"]
    assert vision_calls[1]["options"]["num_gpu"] == 0
    assert vision_calls[0]["keep_alive"] == 0


def test_vision_repeat_limit_is_not_retried_on_cpu():
    fake = FakeOllama(
        HIGH_SCHOOL_MARKDOWN,
        HIGH_SCHOOL_PARSED,
        vision_errors=1,
        vision_error_text='{"error":"prediction aborted, token repeat limit reached"}',
    )
    body = post_image(make_client(fake))

    assert body["success"] is False
    assert len([c for c in fake.calls if c["messages"][0].get("images")]) == 1


def test_repeat_limit_after_table_keeps_text_already_transcribed():
    fake = FakeOllama(
        HIGH_SCHOOL_MARKDOWN,
        HIGH_SCHOOL_PARSED,
        stream_error="prediction aborted, token repeat limit reached",
    )
    body = post_image(make_client(fake))

    assert body["success"] is True
    assert scores_by_term(body)["Lớp 10 HK1"] == {"Toán": 7.2, "Vật lý": 4.2, "Ngữ văn": 6.7}
    assert len([c for c in fake.calls if c["messages"][0].get("images")]) == 1


def test_qwen3_vl_transcribe_disables_thinking():
    fake = FakeOllama(HIGH_SCHOOL_MARKDOWN, HIGH_SCHOOL_PARSED, models=("qwen3-vl:2b", "qwen3:4b"))
    post_image(make_client(fake))

    vision_call = next(c for c in fake.calls if c["messages"][0].get("images"))
    assert vision_call["think"] is False


def test_vision_failing_twice_returns_friendly_warning_without_raw_error():
    body = post_image(make_client(FakeOllama(HIGH_SCHOOL_MARKDOWN, HIGH_SCHOOL_PARSED, vision_errors=2)))

    assert body["success"] is False
    assert any("thử lại sau" in w for w in body["warnings"])
    assert not any("500" in w or "runner" in w for w in body["warnings"])


def test_no_vision_model_installed_asks_for_manual_entry():
    body = post_image(make_client(FakeOllama(models=("qwen3:4b",))))

    assert body["success"] is False
    assert any("chưa cài model đọc ảnh" in w for w in body["warnings"])


def test_non_ten_point_scale_is_not_filled_in():
    parsed = {
        "scale": "4",
        "terms": [{"grade": "Năm 1", "term": "HK1", "subjects": [{"name": "Math", "score": "3,5"}]}],
    }
    body = post_image(make_client(FakeOllama("Math 3,5", parsed)))

    assert body["success"] is False
    assert any("thang 4" in w for w in body["warnings"])


def test_empty_request_returns_warning():
    client = make_client(FakeOllama())
    resp = client.post("/api/v1/profile/transcript-ocr", json={"files": []})

    body = resp.json()
    assert body["success"] is False
    assert body["warnings"]


def test_prepare_image_shrinks_large_and_applies_exif_rotation():
    reader = TranscriptReader(FakeOllama(), Settings(image_max_side=1000))  # type: ignore[arg-type]
    img = Image.new("RGB", (4000, 1000), "white")
    exif = img.getexif()
    exif[0x0112] = 6  # Orientation: xoay 90° theo chiều kim đồng hồ khi hiển thị
    buf = io.BytesIO()
    img.save(buf, format="JPEG", exif=exif)

    out = Image.open(io.BytesIO(base64.b64decode(reader._prepare_image(buf.getvalue()))))

    assert out.size == (250, 1000)  # đã xoay thành ảnh dọc, cạnh dài = 1000
    assert out.mode == "L"


def test_prepare_image_enlarges_small_images():
    reader = TranscriptReader(FakeOllama(), Settings())  # type: ignore[arg-type]
    buf = io.BytesIO()
    Image.new("RGB", (600, 300), "white").save(buf, format="PNG")

    out = Image.open(io.BytesIO(base64.b64decode(reader._prepare_image(buf.getvalue()))))

    assert max(out.size) == 1200


def test_resolve_term_variants():
    resolve = TranscriptReader._resolve_term
    assert resolve("", "Học kỳ II năm học 2023-2024").academic_year == 2023
    assert resolve("", "Học kỳ II năm học 2023-2024").semester == 2
    assert resolve("Lớp 11", "Học kỳ I").grade_name == "Lớp 11"
    third = resolve("", "Học kỳ 3")
    assert (third.grade_name, third.semester) == ("Năm 2", 1)
    assert resolve("Năm thứ 2", "HK2").grade_name == "Năm 2"
    assert resolve("Lớp 12", "Cả năm") is None


def test_high_school_year_only_maps_to_grade_10_with_warning():
    markdown = "KẾT QUẢ HỌC TẬP Năm học 2019 - 2020\n| Môn | Kỳ 1 | Kỳ 2 |\n| Toán | 9,9 | 9,8 |"
    parsed = {
        "scale": "10",
        "level": "thpt",
        "terms": [
            {"grade": "2019 - 2020", "term": "Kỳ 1", "subjects": [{"name": "Toán học", "score": "9,9"}]},
            {"grade": "2019 - 2020", "term": "Kỳ 2", "subjects": [{"name": "Toán học", "score": "9,8"}]},
        ],
    }
    body = post_image(make_client(FakeOllama(markdown, parsed)))

    assert scores_by_term(body) == {"Lớp 10 HK1": {"Toán": 9.9}, "Lớp 10 HK2": {"Toán": 9.8}}
    assert any("Lớp 10" in w for w in body["warnings"])


def test_class_code_like_11a1_is_read_as_grade_11():
    assert TranscriptReader._resolve_term("Lớp: 11A1", "Kỳ 2").grade_name == "Lớp 11"


def test_grade_not_written_in_source_is_not_trusted():
    markdown = "KẾT QUẢ HỌC TẬP Năm học 2021 - 2022\n| Môn | Kỳ 1 |\n| Toán | 8,5 |"
    parsed = {
        "scale": "10",
        "level": "thpt",
        "terms": [
            {"grade": "Lớp 12", "term": "Kỳ 1", "subjects": [{"name": "Toán", "score": "8,5"}]},
        ],
    }
    body = post_image(make_client(FakeOllama(markdown, parsed)))

    assert scores_by_term(body) == {"Lớp 10 HK1": {"Toán": 8.5}}
    assert any("không ghi rõ lớp" in w for w in body["warnings"])


def test_spaced_high_school_table_is_read_by_code_without_llm():
    markdown = (
        "Lớp 10:\n"
        "Môn Toán Lý Ngữ vân Sù Dja Ngoại ngữ Thể dµc GD CD TB môn HL HK\n"
        "HK1 7.2 4.2 6.7 7.6 6.6 5.7 D 6 6.6 TB Tôt\n"
        "HK2 6.7 6.8 6.3 6.8 7.5 7.2 D 8 7.3 Khá Khá\n"
        "Cá nám 6.9 6.9 6.4 7.1 7.2 6.7 D 7.3 7.1 Khá Khá\n"
    )
    fake = FakeOllama(markdown, parsed=None)
    body = post_image(make_client(fake))

    assert scores_by_term(body)["Lớp 10 HK1"] == {
        "Toán": 7.2,
        "Vật lý": 4.2,
        "Ngữ văn": 6.7,
        "Lịch sử": 7.6,
        "Địa lý": 6.6,
        "Tiếng Anh": 5.7,
        "GDCD": 6.0,
    }
    assert set(scores_by_term(body)) == {"Lớp 10 HK1", "Lớp 10 HK2"}
    assert not [c for c in fake.calls if "format" in c]  # không gọi LLM bước B


def test_scores_read_from_image_are_flagged_for_review():
    body = post_image(make_client(FakeOllama(HIGH_SCHOOL_MARKDOWN, HIGH_SCHOOL_PARSED)))

    cells = [s for t in body["terms"] for s in t["scores"]]
    assert cells and all(s["needsReview"] is True for s in cells)
    assert {s["reviewReason"] for s in cells} == {"source_image"}


def test_outlier_score_is_flagged_and_text_pdf_scores_are_not():
    reader = TranscriptReader(FakeOllama(), Settings(_env_file=None))  # type: ignore[arg-type,call-arg]
    terms = [
        _RawTerm(
            label="Lớp 10 HK1",
            grade_name="Lớp 10",
            semester=1,
            scores={"Toán": 8.0, "Vật lý": 7.5, "Ngữ văn": 7.8, "Tiếng Anh": 8.2, "Tin học": 2.0},
        ),
    ]

    async def fake_read_file(_file_item, _file_name):
        return _FileResult(terms=terms)

    reader._read_file = fake_read_file  # type: ignore[method-assign]
    request = TranscriptOcrRequest(files=[TranscriptFilePayload(image_base64="eA==", file_name="a.pdf")])
    body = asyncio.run(reader.read_transcript(request))

    flags = {s.subject: (s.needs_review, s.review_reason) for s in body.terms[0].scores}
    assert flags["Tin học"] == (True, "outlier")
    assert flags["Toán"] == (False, None)
