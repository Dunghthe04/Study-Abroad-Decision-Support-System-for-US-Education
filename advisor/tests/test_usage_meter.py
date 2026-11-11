"""Đo token từng lần gọi AI: Claude và Ollama ghi đúng số nhà cung cấp trả về, lần lỗi cũng được ghi, các lần gọi của một lượt chat được gom lại."""

import asyncio
import json

import httpx
import pytest

from app.schemas.chat import ChatMessage, ChatRequest
from app.services import usage_meter
from app.services.claude import ClaudeClient, ClaudeError
from app.services.ollama import OllamaClient, OllamaError
from app.services.skill_call import SkillCall
from tests.fakes import flow_with, tin

SCHEMA = {"type": "object"}
MESSAGES = [{"role": "system", "content": "skill"}, {"role": "user", "content": "{}"}]

# Khối usage của Anthropic: input_tokens chưa gồm phần đọc từ cache
CLAUDE_USAGE = {"input_tokens": 83, "output_tokens": 48, "cache_read_input_tokens": 7}


def claude_ok(usage: dict | None = CLAUDE_USAGE) -> httpx.Response:
    body = {
        "stop_reason": "tool_use",
        "content": [{"type": "tool_use", "name": "tra_loi", "input": {"a": 1}}],
    }
    if usage is not None:
        body["usage"] = usage
    return httpx.Response(200, json=body)


def claude(handler, **kwargs) -> ClaudeClient:
    http = httpx.AsyncClient(base_url="http://claude.test", transport=httpx.MockTransport(handler))
    return ClaudeClient(http, "main", "key", **kwargs)


def ollama(handler) -> OllamaClient:
    http = httpx.AsyncClient(base_url="http://ollama.test", transport=httpx.MockTransport(handler))
    return OllamaClient(http, "usas-slm-v2")


@pytest.fixture(autouse=True)
def no_wait(monkeypatch):
    async def instant(_seconds):
        return None

    monkeypatch.setattr("app.services.claude.asyncio.sleep", instant)


# ---- Claude
async def test_a_claude_call_records_the_token_counts_the_provider_returned():
    calls = usage_meter.start()

    await claude(lambda request: claude_ok()).chat_json(MESSAGES, SCHEMA)

    call = calls[0]
    assert (call.source, call.model, call.ok) == ("claude", "main", True)
    assert (call.input_tokens, call.output_tokens, call.cached_tokens) == (83, 48, 7)


async def test_a_response_without_usage_records_zero_tokens_instead_of_failing():
    calls = usage_meter.start()

    await claude(lambda request: claude_ok(usage=None)).chat_json(MESSAGES, SCHEMA)

    assert (calls[0].input_tokens, calls[0].output_tokens) == (0, 0)


async def test_every_attempt_is_recorded_including_the_failed_ones_and_the_fallback_model():
    calls = usage_meter.start()
    seen = {"main": 0}

    def handler(request: httpx.Request) -> httpx.Response:
        model = json.loads(request.content)["model"]
        if model == "main":
            seen["main"] += 1
            return httpx.Response(529, json={"error": {"message": "overloaded"}})
        return claude_ok()

    await claude(handler, max_retries=2, fallback_model="lite").chat_json(MESSAGES, SCHEMA)

    assert [(c.model, c.ok) for c in calls] == [("main", False), ("main", False), ("lite", True)]
    assert calls[0].error == "HTTP 529"
    assert calls[0].input_tokens == 0  # lần lỗi không có token
    assert calls[2].output_tokens == 48


async def test_a_rejected_request_is_recorded_as_failed():
    calls = usage_meter.start()

    with pytest.raises(ClaudeError):
        await claude(lambda request: httpx.Response(400, text="bad"), max_retries=1).chat_json(
            MESSAGES, SCHEMA
        )

    assert [(c.ok, c.error) for c in calls] == [(False, "HTTP 400")]


# ---- Ollama
def ollama_body(**extra) -> dict:
    return {"message": {"content": "{}"}, "prompt_eval_count": 13, "eval_count": 14, **extra}


async def test_an_ollama_chat_records_prompt_and_eval_counts():
    calls = usage_meter.start()

    await ollama(lambda request: httpx.Response(200, json=ollama_body())).chat_json(MESSAGES, SCHEMA)

    call = calls[0]
    assert (call.source, call.model, call.input_tokens, call.output_tokens) == (
        "ollama",
        "usas-slm-v2",
        13,
        14,
    )


async def test_an_ollama_failure_is_recorded():
    calls = usage_meter.start()

    def handler(request: httpx.Request) -> httpx.Response:
        raise httpx.ConnectError("tắt")

    with pytest.raises(OllamaError):
        await ollama(handler).chat_json(MESSAGES, SCHEMA)

    assert [(c.source, c.ok) for c in calls] == [("ollama", False)]


async def test_an_ollama_embedding_records_its_input_tokens():
    calls = usage_meter.start()

    def handler(request: httpx.Request) -> httpx.Response:
        return httpx.Response(200, json={"embeddings": [[0.1, 0.2]], "prompt_eval_count": 7})

    await ollama(handler).embed(["xin chào"])

    assert (calls[0].model, calls[0].input_tokens, calls[0].output_tokens) == ("bge-m3", 7, 0)


# ---- gắn nhãn công việc và gom theo lượt
async def test_calls_inside_a_skill_are_labelled_with_the_skill_name(tmp_path):
    skill = tmp_path / "skill_demo.md"
    skill.write_text("# SKILL skill_version 1.0", encoding="utf-8")
    calls = usage_meter.start()

    await SkillCall(claude(lambda request: claude_ok()), "main", skill).run({}, SCHEMA, lambda raw: raw)

    assert calls[0].purpose == "skill_demo"
    assert calls[0].skill_version == "1.0"


async def test_slm_classifier_calls_are_labelled_with_the_prompt_name_and_version():
    from app.services.slm_classifier import OllamaSlm

    calls = usage_meter.start()

    class FakeSlm:
        # Giống OllamaClient thật: ghi token rồi trả kết quả. Ở đây trả kết quả hỏng để chỉ kiểm tra nhãn của hai lần gọi
        async def chat_json_scored(self, messages, output):
            usage_meter.ollama_ok("usas-slm-v4", {"prompt_eval_count": 10, "eval_count": 3}, 5)
            raise ValueError("kết quả không dùng được")

    await OllamaSlm(FakeSlm(), prompt="slm_intents").read("em GPA 3.5", None, "")

    assert len(calls) == 2  # gọi lại một lần khi kết quả hỏng
    assert {(c.purpose, c.skill_version) for c in calls} == {("slm_intents", "3.0")}


async def test_label_does_not_leak_outside_the_block():
    calls = usage_meter.start()
    with usage_meter.purpose("skill_a", "9.9"):
        usage_meter.ollama_ok("m", {}, 1)
    usage_meter.ollama_ok("m", {}, 1)

    assert [(c.purpose, c.skill_version) for c in calls] == [("skill_a", "9.9"), ("", "")]


async def test_without_an_open_turn_nothing_is_recorded_and_nothing_breaks():
    usage_meter._calls.set(None)

    await claude(lambda request: claude_ok()).chat_json(MESSAGES, SCHEMA)  # không có lỗi, không ghi gì


async def test_parallel_tasks_of_the_same_turn_share_one_list():
    calls = usage_meter.start()
    client = claude(lambda request: claude_ok())

    await asyncio.gather(*(client.chat_json(MESSAGES, SCHEMA) for _ in range(4)))

    assert len(calls) == 4


async def test_two_turns_running_at_once_do_not_mix_their_calls():
    async def turn(n: int) -> int:
        calls = usage_meter.start()
        client = claude(lambda request: claude_ok())
        for _ in range(n):
            await client.chat_json(MESSAGES, SCHEMA)
        return len(calls)

    assert sorted(await asyncio.gather(asyncio.create_task(turn(2)), asyncio.create_task(turn(5)))) == [2, 5]


# ---- trong phản hồi chat
class _OneClaudeCall:
    def __init__(self) -> None:
        self._client = claude(lambda request: claude_ok())

    async def run(self, state, message):
        from app.services.chat_flow import SkillOutput

        await self._client.chat_json(MESSAGES, SCHEMA)
        return SkillOutput("trả lời")


async def test_the_chat_response_carries_the_token_of_every_ai_call_of_the_turn():
    flow = flow_with({"skill_tra_loi": _OneClaudeCall()}, tin("knowledge"))

    res = await flow.reply(ChatRequest(messages=[ChatMessage(role="user", content="I-20 là gì")]))

    assert [(c.source, c.input_tokens, c.output_tokens, c.cached_tokens) for c in res.ai_usage] == [
        ("claude", 83, 48, 7)
    ]
    body = res.model_dump(by_alias=True)
    assert body["aiUsage"][0]["inputTokens"] == 83 and body["aiUsage"][0]["cachedTokens"] == 7


async def test_a_turn_without_ai_calls_has_an_empty_list():
    flow = flow_with({}, tin(("out_of_scope", {"subtype": "farewell"}, "tạm biệt")))

    res = await flow.reply(ChatRequest(messages=[ChatMessage(role="user", content="tạm biệt")]))

    assert res.ai_usage == []


# ---- route ngoài chat (/recommend): token cũng nằm trong phản hồi, khóa aiUsage; các khóa cũ giữ nguyên
def test_a_recommend_route_returns_the_token_of_its_ai_call_without_changing_other_keys():
    from fastapi.testclient import TestClient

    from app.api.deps import get_compare_writer
    from app.main import create_app
    from app.services.compare_writer import CompareWriter
    from tests.test_compare_writer import GOOD, REQUEST

    def handler(request: httpx.Request) -> httpx.Response:
        body = {
            "stop_reason": "tool_use",
            "content": [{"type": "tool_use", "name": "tra_loi", "input": GOOD}],
            "usage": CLAUDE_USAGE,
        }
        return httpx.Response(200, json=body)

    app = create_app()
    app.dependency_overrides[get_compare_writer] = lambda: CompareWriter(claude(handler), "main")

    res = TestClient(app).post("/api/v1/recommend/compare", json=REQUEST)

    body = res.json()
    assert body["status"] == "OK" and "skill_version" in body
    usage = body["aiUsage"]
    assert [(u["source"], u["purpose"], u["inputTokens"], u["cachedTokens"]) for u in usage] == [
        ("claude", "skill_so_sanh", 83, 7)
    ]


# ---- đường OCR bảng điểm: trước đây không đo gì, token của Ollama vision và Claude khi đọc học bạ bị bỏ
class _OcrReaderThatCallsTheAi:
    """Đọc học bạ giả: gọi Ollama (đọc ảnh) rồi Claude (bóc tách), như TranscriptReader thật, rồi trả kết quả."""

    def __init__(self) -> None:
        self._claude = claude(lambda request: claude_ok())

    async def read_transcript(self, request):
        from app.schemas.transcript_ocr import TranscriptOcrResponse

        usage_meter.ollama_ok("qwen3-vl", {"prompt_eval_count": 900, "eval_count": 400}, 18000)
        await self._claude.chat_json(MESSAGES, SCHEMA)
        return TranscriptOcrResponse(success=True, model_used="qwen3-vl")


def _ocr_client(reader):
    from fastapi.testclient import TestClient

    from app.api.deps import get_transcript_reader
    from app.main import create_app

    app = create_app()
    app.dependency_overrides[get_transcript_reader] = lambda: reader
    return TestClient(app)


def test_the_ocr_route_returns_the_token_of_both_ai_calls_labelled_transcript_ocr():
    res = _ocr_client(_OcrReaderThatCallsTheAi()).post("/api/v1/profile/transcript-ocr", json={})

    usage = res.json()["aiUsage"]
    assert [(u["source"], u["purpose"], u["inputTokens"], u["outputTokens"]) for u in usage] == [
        ("ollama", "transcript_ocr", 900, 400),
        ("claude", "transcript_ocr", 83, 48),
    ]


def test_the_ocr_route_keeps_its_existing_keys_in_camel_case():
    body = _ocr_client(_OcrReaderThatCallsTheAi()).post("/api/v1/profile/transcript-ocr", json={}).json()

    assert body["success"] is True and body["modelUsed"] == "qwen3-vl"
    assert {"terms", "averageScoreScale10", "calculatedGpa4", "message", "warnings"} <= set(body)


def test_the_ocr_route_without_any_ai_call_has_an_empty_usage_list():
    class NoAiReader:
        async def read_transcript(self, request):
            from app.schemas.transcript_ocr import TranscriptOcrResponse

            return TranscriptOcrResponse(success=False, message="Không đọc được")

    body = _ocr_client(NoAiReader()).post("/api/v1/profile/transcript-ocr", json={}).json()

    assert body["aiUsage"] == []


def test_the_purpose_label_does_not_leak_out_of_the_ocr_route():
    _ocr_client(_OcrReaderThatCallsTheAi()).post("/api/v1/profile/transcript-ocr", json={})

    assert usage_meter._purpose.get() == ""  # route xong thì nhãn được trả về như cũ
