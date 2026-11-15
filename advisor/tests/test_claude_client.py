"""ClaudeClient: ép JSON qua tool_use, ghi token đúng số Anthropic trả, lỗi quá tải thì thử lại rồi dùng model dự phòng."""

import httpx
import pytest

from app.services import usage_meter
from app.services.claude import ClaudeClient, ClaudeError, ClaudeUnavailable

SCHEMA = {"type": "object", "properties": {"a": {"type": "integer"}}}
MESSAGES = [{"role": "system", "content": "skill"}, {"role": "user", "content": "{}"}]
USAGE = {"input_tokens": 120, "output_tokens": 30, "cache_read_input_tokens": 5}


def reply(payload: dict | None = None, stop: str = "tool_use") -> httpx.Response:
    block = {"type": "tool_use", "name": "tra_loi", "input": payload if payload is not None else {"a": 1}}
    return httpx.Response(200, json={"content": [block], "stop_reason": stop, "usage": USAGE})


def client(handler, **kw) -> ClaudeClient:
    http = httpx.AsyncClient(transport=httpx.MockTransport(handler), base_url="https://api.anthropic.com")
    return ClaudeClient(http, "claude-haiku-5-5", "khoa", max_retries=1, **kw)


@pytest.fixture(autouse=True)
def no_sleep(monkeypatch):
    async def instant(_):
        return None

    monkeypatch.setattr("app.services.claude.asyncio.sleep", instant)


async def test_tra_json_va_gui_dung_yeu_cau():
    seen = {}

    def handler(request: httpx.Request) -> httpx.Response:
        seen["headers"] = request.headers
        seen["body"] = request.read()
        return reply()

    result = await client(handler).chat_json(MESSAGES, SCHEMA)
    assert result == {"a": 1}
    assert seen["headers"]["x-api-key"] == "khoa"
    assert seen["headers"]["anthropic-version"] == "2023-06-01"
    body = httpx.Response(200, content=seen["body"]).json()
    assert body["system"] == "skill"
    assert "temperature" not in body  # Haiku 5.5 từ chối tham số này
    assert body["tool_choice"] == {"type": "tool", "name": "tra_loi"}
    assert body["tools"][0]["input_schema"] == SCHEMA


async def test_ghi_token_dung_so_nha_cung_cap_tra():
    calls = usage_meter.start()
    await client(lambda r: reply()).chat_json(MESSAGES, SCHEMA)
    (call,) = calls
    assert (call.source, call.model, call.ok) == ("claude", "claude-haiku-5-5", True)
    assert (call.input_tokens, call.output_tokens, call.cached_tokens) == (120, 30, 5)


async def test_qua_tai_thi_dung_model_du_phong():
    models = []

    def handler(request: httpx.Request) -> httpx.Response:
        model = httpx.Response(200, content=request.read()).json()["model"]
        models.append(model)
        return httpx.Response(529) if model == "claude-haiku-5-5" else reply()

    calls = usage_meter.start()
    result = await client(handler, fallback_model="claude-sonnet-5-5").chat_json(MESSAGES, SCHEMA)
    assert result == {"a": 1}
    assert models == ["claude-haiku-5-5", "claude-sonnet-5-5"]
    assert [c.ok for c in calls] == [False, True]


async def test_qua_tai_khong_co_du_phong_thi_bao_loi():
    with pytest.raises(ClaudeUnavailable):
        await client(lambda r: httpx.Response(529)).chat_json(MESSAGES, SCHEMA)


async def test_yeu_cau_sai_khong_thu_lai():
    count = 0

    def handler(request: httpx.Request) -> httpx.Response:
        nonlocal count
        count += 1
        return httpx.Response(400, json={"error": "sai"})

    with pytest.raises(ClaudeError):
        await client(handler, fallback_model="x").chat_json(MESSAGES, SCHEMA)
    assert count == 1


async def test_bi_cat_vi_het_max_tokens():
    with pytest.raises(ClaudeError, match="max_tokens"):
        await client(lambda r: reply(stop="max_tokens")).chat_json(MESSAGES, SCHEMA)


async def test_khong_co_tool_use():
    def handler(request: httpx.Request) -> httpx.Response:
        return httpx.Response(
            200, json={"content": [{"type": "text", "text": "xin chào"}], "stop_reason": "end_turn"}
        )

    with pytest.raises(ClaudeError):
        await client(handler).chat_json(MESSAGES, SCHEMA)
