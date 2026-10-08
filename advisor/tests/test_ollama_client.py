import json

import httpx
import pytest

from app.services.ollama import OllamaClient, OllamaError

SCHEMA = {"type": "object", "properties": {"ok": {"type": "boolean"}}, "required": ["ok"]}
MESSAGES = [{"role": "user", "content": "hi"}]


def make_client(handler) -> OllamaClient:
    """Ollama giả: MockTransport trả lời thay cho server thật, không cần chạy Ollama."""
    http = httpx.AsyncClient(transport=httpx.MockTransport(handler), base_url="http://ollama.test")
    return OllamaClient(http, model="qwen3:8b")


async def test_chat_json_sends_schema_and_parses_content():
    sent = {}

    def handler(request: httpx.Request) -> httpx.Response:
        sent["path"] = request.url.path
        sent["body"] = json.loads(request.content)
        return httpx.Response(200, json={"message": {"role": "assistant", "content": '{"ok": true}'}})

    result = await make_client(handler).chat_json(MESSAGES, SCHEMA)

    assert result == {"ok": True}
    assert sent["path"] == "/api/chat"
    body = sent["body"]
    assert body["model"] == "qwen3:8b"
    assert body["format"] == SCHEMA
    assert body["stream"] is False
    assert body["think"] is False
    assert body["options"]["temperature"] == 0.0


async def test_chat_json_raises_when_content_is_not_json():
    def handler(request: httpx.Request) -> httpx.Response:
        return httpx.Response(200, json={"message": {"content": "xin chào"}})

    with pytest.raises(OllamaError):
        await make_client(handler).chat_json(MESSAGES, SCHEMA)


async def test_chat_json_raises_when_ollama_returns_500():
    def handler(request: httpx.Request) -> httpx.Response:
        return httpx.Response(500, json={"error": "model not found"})

    with pytest.raises(OllamaError):
        await make_client(handler).chat_json(MESSAGES, SCHEMA)


async def test_chat_json_raises_when_ollama_is_down():
    def handler(request: httpx.Request) -> httpx.Response:
        raise httpx.ConnectError("connection refused", request=request)

    with pytest.raises(OllamaError):
        await make_client(handler).chat_json(MESSAGES, SCHEMA)
