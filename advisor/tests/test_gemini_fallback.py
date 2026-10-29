"""Gemini có model dự phòng: model chính quá tải (503, 429) hoặc treo thì tự gọi model dự phòng.

Dùng transport giả của httpx nên không gọi Gemini thật; bỏ thời gian chờ giữa các lần thử để test chạy nhanh.
"""

import json

import httpx
import pytest

from app.core.config import Settings
from app.services.gemini import GeminiClient, GeminiError, GeminiUnavailable

SCHEMA = {"type": "object"}
MESSAGES = [{"role": "system", "content": "skill"}, {"role": "user", "content": "{}"}]


def ok(payload: dict) -> httpx.Response:
    """Phản hồi thành công đúng dạng Interactions API: JSON nằm trong bước model_output."""
    return httpx.Response(
        200,
        json={
            "status": "completed",
            "steps": [{"type": "model_output", "content": [{"type": "text", "text": json.dumps(payload)}]}],
        },
    )


def client(
    replies: dict, *, fallback: str | None = "lite", retries: int = 2
) -> tuple[GeminiClient, list[dict]]:
    """replies: model -> hàm nhận số lần đã gọi model đó, trả Response hoặc ném lỗi. Trả client và danh sách body đã gửi."""
    sent: list[dict] = []
    calls: dict[str, int] = {}

    def handler(request: httpx.Request) -> httpx.Response:
        body = json.loads(request.content)
        sent.append(body)
        model = body["model"]
        calls[model] = calls.get(model, 0) + 1
        return replies[model](calls[model])

    http = httpx.AsyncClient(base_url="http://gemini.test", transport=httpx.MockTransport(handler))
    return GeminiClient(http, "main", "key", max_retries=retries, fallback_model=fallback), sent


@pytest.fixture(autouse=True)
def no_wait(monkeypatch):
    async def instant(_seconds):
        return None

    monkeypatch.setattr("app.services.gemini.asyncio.sleep", instant)


def busy(_n):
    return httpx.Response(503, json={"error": {"message": "high demand"}})


def hang(_n):
    raise httpx.ReadTimeout("không trả lời")


async def test_uses_only_the_main_model_when_it_answers():
    gemini, sent = client({"main": lambda n: ok({"a": 1})})

    assert await gemini.chat_json(MESSAGES, SCHEMA) == {"a": 1}
    assert [b["model"] for b in sent] == ["main"]


async def test_switches_to_the_fallback_when_the_main_model_is_overloaded():
    gemini, sent = client({"main": busy, "lite": lambda n: ok({"a": 2})})

    assert await gemini.chat_json(MESSAGES, SCHEMA) == {"a": 2}
    assert [b["model"] for b in sent] == ["main", "main", "lite"]  # thử model chính đủ 2 lần rồi mới đổi


async def test_switches_to_the_fallback_when_the_main_model_hangs():
    gemini, sent = client({"main": hang, "lite": lambda n: ok({"a": 3})})

    assert await gemini.chat_json(MESSAGES, SCHEMA) == {"a": 3}
    assert sent[-1]["model"] == "lite"


async def test_switches_after_running_out_of_quota_too():
    gemini, sent = client({"main": lambda n: httpx.Response(429), "lite": lambda n: ok({"a": 4})})

    assert await gemini.chat_json(MESSAGES, SCHEMA) == {"a": 4}


async def test_the_fallback_request_keeps_the_prompt_but_drops_thinking_level():
    gemini, sent = client({"main": busy, "lite": lambda n: ok({})})

    await gemini.chat_json(MESSAGES, SCHEMA)

    main_body, fallback_body = sent[0], sent[-1]
    assert main_body["generation_config"]["thinking_level"] == "low"
    assert "thinking_level" not in fallback_body["generation_config"]  # model nhẹ có thể không nhận
    assert fallback_body["system_instruction"] == main_body["system_instruction"] == "skill"
    assert fallback_body["response_format"] == main_body["response_format"]


async def test_a_refused_request_does_not_switch_model():
    # 4xx (khóa sai, yêu cầu sai) là lỗi phía mình: đổi model không giúp được, báo lỗi ngay
    gemini, sent = client({"main": lambda n: httpx.Response(400, text="bad key"), "lite": lambda n: ok({})})

    with pytest.raises(GeminiError) as caught:
        await gemini.chat_json(MESSAGES, SCHEMA)

    assert not isinstance(caught.value, GeminiUnavailable)
    assert [b["model"] for b in sent] == ["main"]


async def test_without_a_fallback_the_overload_error_is_raised():
    gemini, sent = client({"main": busy}, fallback=None)

    with pytest.raises(GeminiUnavailable):
        await gemini.chat_json(MESSAGES, SCHEMA)
    assert len(sent) == 2


async def test_raises_when_both_models_are_unavailable():
    gemini, _ = client({"main": busy, "lite": hang})

    with pytest.raises(GeminiUnavailable):
        await gemini.chat_json(MESSAGES, SCHEMA)


async def test_usage_log_records_which_model_answered():
    gemini, _ = client({"main": busy, "lite": lambda n: ok({})})

    await gemini.chat_json(MESSAGES, SCHEMA)

    assert gemini.usage_log[-1]["model"] == "lite"


def test_settings_default_to_no_fallback_and_stay_under_the_backend_timeout():
    s = Settings(_env_file=None)

    assert s.gemini_fallback_model == ""
    # mỗi model: số lần thử x thời gian chờ; 2 model vẫn phải dưới 300 giây .NET chờ advisor
    assert 2 * s.gemini_max_retries * s.gemini_timeout_seconds < 300
