"""Mọi lần gọi AI phải có nhãn (purpose) rõ nghĩa trong báo cáo token, không có dòng trống nhãn hay hai bước khác nhau trùng một tên.
Các nhãn: slm_intents, các skill_*, viet_tra_loi_ho_so, embedding, transcript_ocr_read, transcript_ocr_parse."""

import json
import re
from contextlib import asynccontextmanager
from pathlib import Path

import httpx

from app.core.config import Settings
from app.schemas.chat import ResultSchool
from app.schemas.slm import SkillRequest
from app.schemas.transcript_ocr import TranscriptFilePayload, TranscriptOcrRequest
from app.services import usage_meter
from app.services.advice_writer import AdviceWriter
from app.services.transcript_reader import TranscriptReader
from tests.test_transcript_ocr_api import UNIVERSITY_PARSED, FakeOllama, _dumps, _png_base64
from tests.test_usage_meter import CLAUDE_USAGE, claude, ollama

# Văn bản không có bảng nên code không đọc thẳng được: bước B (bóc tách) phải gọi AI
PROSE = "Học bạ trường A. Mathematics 8,2 Fall2023. Physics 7,0 Spring2024. English 9,1 Summer2024. Chemistry 6,5 Fall2024."


class CountingOllama(FakeOllama):
    """Ollama giả có số token như Ollama thật: dòng cuối của stream (done) và phản hồi không stream đều mang prompt_eval_count, eval_count."""

    VISION = {"prompt_eval_count": 700, "eval_count": 300}
    PARSE = {"prompt_eval_count": 120, "eval_count": 60}

    async def post(self, url: str, json: dict, timeout: float | None = None) -> httpx.Response:
        resp = await super().post(url, json=json, timeout=timeout)
        if resp.status_code != 200 or json["messages"][0].get("images"):
            return resp
        return httpx.Response(200, json={**resp.json(), **self.PARSE}, request=resp.request)

    @asynccontextmanager
    async def stream(self, method: str, url: str, json: dict, timeout: float | None = None):
        async with super().stream(method, url, json=json, timeout=timeout) as resp:
            if resp.status_code == 200 and not self.stream_error:
                done = _dumps({"done": True, "message": {"content": ""}, **self.VISION})
                resp = httpx.Response(200, text=f"{resp.text}\n{done}", request=resp.request)
            yield resp


def _reader(fake: FakeOllama, parser=None) -> TranscriptReader:
    settings = Settings(_env_file=None, llm_model="qwen3:8b", transcript_parse_model="qwen3:4b")  # type: ignore[call-arg]
    return TranscriptReader(fake, settings, parser, "claude-haiku-5-5" if parser else None)  # type: ignore[arg-type]


def _request() -> TranscriptOcrRequest:
    return TranscriptOcrRequest(
        files=[
            TranscriptFilePayload(image_base64=_png_base64(), file_name="hb.png", content_type="image/png")
        ]
    )


def _claude_parser():
    def handler(request: httpx.Request) -> httpx.Response:
        body = {
            "stop_reason": "tool_use",
            "content": [{"type": "tool_use", "name": "tra_loi", "input": UNIVERSITY_PARSED}],
            "usage": CLAUDE_USAGE,
        }
        return httpx.Response(200, json=body)

    return claude(handler)


def _rows(calls) -> list[tuple]:
    return [(c.source, c.purpose, c.input_tokens, c.output_tokens, c.ok) for c in calls]


# ---- OCR bảng điểm: hai bước, hai nhãn, token thật của Ollama


async def test_ocr_labels_the_vision_step_and_the_parse_step_differently():
    calls = usage_meter.start()

    await _reader(CountingOllama(PROSE, UNIVERSITY_PARSED), _claude_parser()).read_transcript(_request())

    assert _rows(calls) == [
        (
            "ollama",
            "transcript_ocr_read",
            700,
            300,
            True,
        ),  # AI local đọc ảnh, token lấy từ dòng cuối của stream
        ("claude", "transcript_ocr_parse", 83, 48, True),  # Claude bóc tách văn bản
    ]


async def test_ocr_parse_step_on_ollama_is_measured_too_when_there_is_no_claude_key():
    calls = usage_meter.start()

    await _reader(CountingOllama(PROSE, UNIVERSITY_PARSED), parser=None).read_transcript(_request())

    assert _rows(calls) == [
        ("ollama", "transcript_ocr_read", 700, 300, True),
        ("ollama", "transcript_ocr_parse", 120, 60, True),
    ]


async def test_ocr_vision_failure_is_recorded_with_a_short_reason_and_never_the_server_address():
    calls = usage_meter.start()

    # Hai lần đều lỗi 500: lần đầu rồi thử lại trên CPU
    await _reader(CountingOllama(PROSE, UNIVERSITY_PARSED, vision_errors=2)).read_transcript(_request())

    assert [(c.purpose, c.ok, c.error) for c in calls] == [("transcript_ocr_read", False, "HTTP 500")] * 2
    assert all("ollama" not in (c.error or "").lower() for c in calls)


async def test_ocr_vision_stopped_by_repeat_limit_is_still_recorded_as_a_call():
    calls = usage_meter.start()

    await _reader(
        CountingOllama(
            PROSE, UNIVERSITY_PARSED, stream_error="prediction aborted, token repeat limit reached"
        ),
        _claude_parser(),
    ).read_transcript(_request())

    read = next(c for c in calls if c.purpose == "transcript_ocr_read")
    assert (read.ok, read.input_tokens) == (
        True,
        0,
    )  # giữ phần đã chép, không có dòng done nên chưa biết token


async def test_the_ocr_label_does_not_leak_after_reading():
    usage_meter.start()

    await _reader(CountingOllama(PROSE, UNIVERSITY_PARSED), _claude_parser()).read_transcript(_request())

    assert usage_meter._purpose.get() == ""


# ---- Lời tư vấn hồ sơ và embedding: trước đây để trống nhãn


class _ClaudeLike:
    """Giống ClaudeClient: ghi token của lần gọi rồi trả câu trả lời."""

    async def chat_json(self, messages, schema, temperature=0.0):
        usage_meter.claude_ok("claude-haiku-5-5", CLAUDE_USAGE, 7)
        return {"tra_loi": "Hồ sơ của bạn ở mức trung bình."}


async def test_the_profile_advice_writer_is_labelled_with_its_prompt_name_and_version():
    calls = usage_meter.start()
    writer = AdviceWriter(_ClaudeLike())
    req = SkillRequest(loai="ho_so", cau_hoi="Đánh giá hồ sơ giúp em", ho_so={"gpa": "8.5"}, tinh_san={})

    await writer.write("# skill", req, [ResultSchool(code="1", name="Iowa", group="M")])

    (call,) = calls
    assert (call.source, call.purpose, call.skill_version) == ("claude", "viet_tra_loi_ho_so", writer.version)


async def test_embedding_is_labelled_embedding():
    calls = usage_meter.start()

    def handler(request: httpx.Request) -> httpx.Response:
        return httpx.Response(200, json={"embeddings": [[0.1, 0.2]], "prompt_eval_count": 7})

    await ollama(handler).embed(["xin chào"])

    assert [(c.source, c.purpose, c.input_tokens) for c in calls] == [("ollama", "embedding", 7)]


async def test_a_failed_embedding_is_labelled_too():
    calls = usage_meter.start()

    def handler(request: httpx.Request) -> httpx.Response:
        return httpx.Response(500)

    try:
        await ollama(handler).embed(["xin chào"])
    except Exception:  # noqa: BLE001 - lỗi được ném tiếp là hành vi cũ, ở đây chỉ kiểm nhãn của dòng lỗi
        pass

    assert [(c.purpose, c.ok) for c in calls] == [("embedding", False)]


async def test_the_embedding_label_does_not_leak():
    usage_meter.start()

    await ollama(
        lambda request: httpx.Response(200, json={"embeddings": [[0.1]], "prompt_eval_count": 1})
    ).embed(["x"])

    assert usage_meter._purpose.get() == ""


# ---- Giữ cho bộ nhãn luôn đủ: ai thêm một chỗ gọi AI mới mà quên nhãn thì test này báo lỗi

# Các file được phép chứa lời gọi AI trực tiếp, và nhãn của từng chỗ do đâu đặt
KNOWN_AI_CALL_SITES = {
    "services/skill_call.py": "SkillCall đặt nhãn = tên file skill",
    "services/advice_writer.py": "usage_meter.purpose(PURPOSE, version) = viet_tra_loi_ho_so",
    "services/transcript_reader.py": "usage_meter.purpose(PURPOSE_READ / PURPOSE_PARSE)",
    "services/slm_classifier.py": "usage_meter.purpose(prompt_name, version) = slm_intents",
    "services/skills/skill_tra_loi.py": "gọi embed(); OllamaClient.embed tự gắn nhãn embedding",
}
CALL = re.compile(r"\.(chat_json|chat_json_scored|embed)\(")


def test_every_ai_call_site_is_known_so_no_call_goes_unlabelled():
    app_dir = Path(__file__).resolve().parents[1] / "app"
    found = {
        path.relative_to(app_dir).as_posix()
        for path in app_dir.rglob("*.py")
        if CALL.search(path.read_text(encoding="utf-8"))
    }

    unknown = found - set(KNOWN_AI_CALL_SITES)
    assert not unknown, (
        f"Chỗ gọi AI mới chưa có nhãn trong báo cáo token: {sorted(unknown)}. "
        "Bọc lời gọi bằng usage_meter.purpose('<nhãn>') (hoặc dùng SkillCall) rồi thêm file vào KNOWN_AI_CALL_SITES."
    )
    assert not set(KNOWN_AI_CALL_SITES) - found, (
        "Có file trong KNOWN_AI_CALL_SITES không còn gọi AI nữa, bỏ khỏi danh sách"
    )


def test_every_skill_call_gets_its_file_name_as_label():
    # SkillCall.name là tên file skill nên chỗ nào dùng SkillCall cũng có nhãn không trống
    from app.services.skill_call import SKILLS_DIR, SkillCall

    class Llm:
        async def chat_json(self, *args, **kwargs):  # pragma: no cover - không được gọi
            raise AssertionError

    for skill in sorted(Path(SKILLS_DIR).glob("skill_*.md")):
        call = SkillCall(Llm(), "m", skill)
        assert call.name == skill.stem and call.name


def test_json_dump_helper_is_not_shadowed():
    assert json.loads(_dumps({"a": 1})) == {"a": 1}
