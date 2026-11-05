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
    # Chạy hết trên GPU, ngữ cảnh vừa VRAM, kết quả lặp lại được
    assert body["options"]["num_gpu"] == 99
    assert body["options"]["num_ctx"] == 6144
    assert body["options"]["seed"] == 42
    assert body["keep_alive"] == "30m"


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


async def test_embed_sends_the_embedding_model_and_returns_one_vector_per_text():
    sent = {}

    def handler(request: httpx.Request) -> httpx.Response:
        sent["path"] = request.url.path
        sent["body"] = json.loads(request.content)
        return httpx.Response(200, json={"embeddings": [[0.1, 0.2], [0.3, 0.4]]})

    vectors = await make_client(handler).embed(["I-20 là gì?", "SEVIS là gì?"])

    assert vectors == [[0.1, 0.2], [0.3, 0.4]]
    assert sent["path"] == "/api/embed"
    assert sent["body"]["model"] == "bge-m3"  # model tạo vector, khác model chat
    assert sent["body"]["input"] == ["I-20 là gì?", "SEVIS là gì?"]
    assert sent["body"]["keep_alive"] == "30m"


async def test_embed_uses_the_configured_embedding_model():
    sent = {}

    def handler(request: httpx.Request) -> httpx.Response:
        sent["model"] = json.loads(request.content)["model"]
        return httpx.Response(200, json={"embeddings": [[0.0]]})

    http = httpx.AsyncClient(transport=httpx.MockTransport(handler), base_url="http://ollama.test")
    await OllamaClient(http, model="qwen3:8b", embedding_model="nomic-embed-text").embed(["x"])

    assert sent["model"] == "nomic-embed-text"


async def test_embed_raises_when_vector_count_does_not_match_the_texts():
    def handler(request: httpx.Request) -> httpx.Response:
        return httpx.Response(200, json={"embeddings": [[0.1]]})

    with pytest.raises(OllamaError, match="2 đoạn chữ"):
        await make_client(handler).embed(["a", "b"])


async def test_embed_raises_when_ollama_returns_500():
    def handler(request: httpx.Request) -> httpx.Response:
        return httpx.Response(500, text="boom")

    with pytest.raises(OllamaError):
        await make_client(handler).embed(["a"])


async def test_embed_raises_when_ollama_is_down():
    def handler(request: httpx.Request) -> httpx.Response:
        raise httpx.ConnectError("refused")

    with pytest.raises(OllamaError):
        await make_client(handler).embed(["a"])


async def test_embed_raises_when_the_response_has_no_embeddings():
    def handler(request: httpx.Request) -> httpx.Response:
        return httpx.Response(200, json={"oops": 1})

    with pytest.raises(OllamaError):
        await make_client(handler).embed(["a"])
