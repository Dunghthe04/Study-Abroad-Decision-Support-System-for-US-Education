"""Đo token từng lần gọi AI: Gemini và Ollama ghi đúng số nhà cung cấp trả về, lần lỗi cũng được ghi, các lần gọi của một lượt chat được gom lại."""

import asyncio
import json

import httpx
import pytest

from app.schemas.chat import ChatMessage, ChatRequest
from app.services import usage_meter
from app.services.gemini import GeminiClient, GeminiError
from app.services.ollama import OllamaClient, OllamaError
from app.services.skill_call import SkillCall
from tests.fakes import flow_with, tin

SCHEMA = {"type": "object"}
MESSAGES = [{"role": "system", "content": "skill"}, {"role": "user", "content": "{}"}]

# Khối usage thật của Gemini (lần chạy thử --think high)
GEMINI_USAGE = {
    "total_tokens": 748,
    "total_input_tokens": 83,
    "input_tokens_by_modality": [{"modality": "text", "tokens": 83}],
    "total_cached_tokens": 0,
    "total_output_tokens": 48,
    "total_tool_use_tokens": 0,
    "total_thought_tokens": 617,
    "initial_input_tokens": 126,
}


def gemini_ok(usage: dict | None = GEMINI_USAGE) -> httpx.Response:
    body = {
        "status": "completed",
        "steps": [{"type": "model_output", "content": [{"type": "text", "text": json.dumps({"a": 1})}]}],
    }
    if usage is not None:
        body["usage"] = usage
    return httpx.Response(200, json=body)


def gemini(handler, **kwargs) -> GeminiClient:
    http = httpx.AsyncClient(base_url="http://gemini.test", transport=httpx.MockTransport(handler))
    return GeminiClient(http, "main", "key", **kwargs)


def ollama(handler) -> OllamaClient:
    http = httpx.AsyncClient(base_url="http://ollama.test", transport=httpx.MockTransport(handler))
    return OllamaClient(http, "usas-slm-v2")


@pytest.fixture(autouse=True)
def no_wait(monkeypatch):
    async def instant(_seconds):
        return None

    monkeypatch.setattr("app.services.gemini.asyncio.sleep", instant)


# ---- Gemini
async def test_a_gemini_call_records_the_four_token_counts_the_provider_returned():
    calls = usage_meter.start()

    await gemini(lambda request: gemini_ok()).chat_json(MESSAGES, SCHEMA)

    call = calls[0]
    assert (call.source, call.model, call.ok) == ("gemini", "main", True)
    assert (call.input_tokens, call.output_tokens, call.thought_tokens, call.cached_tokens) == (
        83,
        48,
        617,
        0,
    )
    assert call.initial_input_tokens == 126


async def test_a_response_without_usage_records_zero_tokens_instead_of_failing():
    calls = usage_meter.start()

    await gemini(lambda request: gemini_ok(usage=None)).chat_json(MESSAGES, SCHEMA)

    assert (calls[0].input_tokens, calls[0].output_tokens, calls[0].initial_input_tokens) == (0, 0, None)


async def test_every_attempt_is_recorded_including_the_failed_ones_and_the_fallback_model():
    calls = usage_meter.start()
    seen = {"main": 0}

    def handler(request: httpx.Request) -> httpx.Response:
        model = json.loads(request.content)["model"]
        if model == "main":
            seen["main"] += 1
            return httpx.Response(503, json={"error": {"message": "high demand"}})
        return gemini_ok()

    await gemini(handler, max_retries=2, fallback_model="lite").chat_json(MESSAGES, SCHEMA)

    assert [(c.model, c.ok) for c in calls] == [("main", False), ("main", False), ("lite", True)]
    assert calls[0].error == "HTTP 503"
    assert calls[0].input_tokens == 0  # lần lỗi không có token
    assert calls[2].output_tokens == 48


async def test_a_rejected_request_is_recorded_as_failed():
    calls = usage_meter.start()

    with pytest.raises(GeminiError):
        await gemini(lambda request: httpx.Response(400, text="bad"), max_retries=1).chat_json(
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
    assert (call.source, call.model, call.input_tokens, call.output_tokens, call.thought_tokens) == (
        "ollama",
        "usas-slm-v2",
        13,
        14,
        0,
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

    await SkillCall(gemini(lambda request: gemini_ok()), "main", skill).run({}, SCHEMA, lambda raw: raw)

    assert calls[0].purpose == "skill_demo"


async def test_without_an_open_turn_nothing_is_recorded_and_nothing_breaks():
    usage_meter._calls.set(None)

    await gemini(lambda request: gemini_ok()).chat_json(MESSAGES, SCHEMA)  # không có lỗi, không ghi gì


async def test_parallel_tasks_of_the_same_turn_share_one_list():
    calls = usage_meter.start()
    client = gemini(lambda request: gemini_ok())

    await asyncio.gather(*(client.chat_json(MESSAGES, SCHEMA) for _ in range(4)))

    assert len(calls) == 4


async def test_two_turns_running_at_once_do_not_mix_their_calls():
    async def turn(n: int) -> int:
        calls = usage_meter.start()
        client = gemini(lambda request: gemini_ok())
        for _ in range(n):
            await client.chat_json(MESSAGES, SCHEMA)
        return len(calls)

    assert sorted(await asyncio.gather(asyncio.create_task(turn(2)), asyncio.create_task(turn(5)))) == [2, 5]


# ---- trong phản hồi chat
class _OneGeminiCall:
    def __init__(self) -> None:
        self._client = gemini(lambda request: gemini_ok())

    async def run(self, state, message):
        from app.services.chat_flow import SkillOutput

        await self._client.chat_json(MESSAGES, SCHEMA)
        return SkillOutput("trả lời")


async def test_the_chat_response_carries_the_token_of_every_ai_call_of_the_turn():
    flow = flow_with({"skill_tra_loi": _OneGeminiCall()}, tin("kien_thuc"))

    res = await flow.reply(ChatRequest(messages=[ChatMessage(role="user", content="I-20 là gì")]))

    assert [(c.source, c.input_tokens, c.output_tokens, c.thought_tokens) for c in res.ai_usage] == [
        ("gemini", 83, 48, 617)
    ]
    body = res.model_dump(by_alias=True)
    assert body["aiUsage"][0]["inputTokens"] == 83 and body["aiUsage"][0]["thoughtTokens"] == 617


async def test_a_turn_without_ai_calls_has_an_empty_list():
    flow = flow_with({}, tin(("ngoai_pham_vi", {"loai_con": "tam_biet"}, "tạm biệt")))

    res = await flow.reply(ChatRequest(messages=[ChatMessage(role="user", content="tạm biệt")]))

    assert res.ai_usage == []
