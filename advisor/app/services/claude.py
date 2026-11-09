"""Gọi Claude (Anthropic Messages API) và ép trả JSON đúng schema. Cùng giao diện chat_json với OllamaClient."""

import asyncio
from typing import Any

import httpx

from app.services import usage_meter
from app.services.ollama import OllamaError

ANTHROPIC_VERSION = "2023-06-01"
TOOL_NAME = "tra_loi"


class ClaudeError(OllamaError):
    """Kế thừa OllamaError để SkillCall vẫn gọi lại khi lỗi mạng hoặc JSON sai."""


class ClaudeUnavailable(ClaudeError):
    """Claude quá tải (429, 5xx, 529) hoặc không trả lời kịp sau mọi lần thử: lỗi phía máy chủ."""


class ClaudeClient:
    def __init__(
        self,
        http: httpx.AsyncClient,
        model: str,
        api_key: str,
        *,
        max_tokens: int = 4096,
        max_retries: int = 2,
        fallback_model: str | None = None,
    ) -> None:
        self._http = http
        self._model = model
        self._fallback_model = fallback_model
        self._api_key = api_key
        self._max_tokens = max_tokens
        self._max_retries = max_retries

    async def chat_json(
        self, messages: list[dict[str, str]], schema: dict[str, Any], temperature: float = 0.0
    ) -> dict[str, Any]:
        system = "\n\n".join(m["content"] for m in messages if m["role"] == "system")
        user = "\n\n".join(m["content"] for m in messages if m["role"] == "user")
        try:
            data = await self._post_with_retry(self._body(self._model, system, user, schema, temperature))
        except ClaudeUnavailable:
            if not self._fallback_model:
                raise
            data = await self._post_with_retry(
                self._body(self._fallback_model, system, user, schema, temperature)
            )
        return self._extract_json(data)

    # Ép trả JSON bằng cách bắt Claude "gọi" một công cụ có input_schema đúng schema cần; phần input chính là JSON
    def _body(
        self, model: str, system: str, user: str, schema: dict[str, Any], temperature: float
    ) -> dict[str, Any]:
        body: dict[str, Any] = {
            "model": model,
            "max_tokens": self._max_tokens,
            "temperature": temperature,
            "messages": [{"role": "user", "content": user}],
            "tools": [
                {
                    "name": TOOL_NAME,
                    "description": "Trả lời theo đúng cấu trúc yêu cầu.",
                    "input_schema": schema,
                }
            ],
            "tool_choice": {"type": "tool", "name": TOOL_NAME},
        }
        if system:
            body["system"] = system
        return body

    async def _post_with_retry(self, body: dict[str, Any]) -> dict[str, Any]:
        last = "chưa gọi"
        for attempt in range(self._max_retries):
            started = asyncio.get_running_loop().time()
            try:
                resp = await self._http.post(
                    "/v1/messages",
                    json=body,
                    headers={"x-api-key": self._api_key, "anthropic-version": ANTHROPIC_VERSION},
                )
            except httpx.HTTPError as ex:
                last = repr(ex)
                usage_meter.failed("claude", body["model"], last, self._elapsed_ms(started))
            else:
                if resp.status_code in (429, 529) or resp.status_code >= 500:
                    last = f"HTTP {resp.status_code}"
                    usage_meter.failed("claude", body["model"], last, self._elapsed_ms(started))
                else:
                    try:
                        resp.raise_for_status()
                    except httpx.HTTPError as ex:
                        usage_meter.failed(
                            "claude", body["model"], f"HTTP {resp.status_code}", self._elapsed_ms(started)
                        )
                        raise ClaudeError(f"Claude từ chối yêu cầu: {resp.text[:300]}") from ex
                    data = resp.json()
                    usage_meter.claude_ok(body["model"], data.get("usage"), self._elapsed_ms(started))
                    return data
            await asyncio.sleep(2**attempt * 2)
        raise ClaudeUnavailable(f"Gọi Claude {body['model']} thất bại sau {self._max_retries} lần: {last}")

    @staticmethod
    def _elapsed_ms(started: float) -> int:
        return round((asyncio.get_running_loop().time() - started) * 1000)

    @staticmethod
    def _extract_json(data: dict[str, Any]) -> dict[str, Any]:
        if data.get("stop_reason") == "max_tokens":
            raise ClaudeError("Claude bị cắt giữa chừng vì hết max_tokens, JSON không đầy đủ")
        for block in data.get("content", []):
            if block.get("type") == "tool_use" and block.get("name") == TOOL_NAME:
                result = block.get("input")
                if isinstance(result, dict):
                    return result
        raise ClaudeError(f"Không thấy kết quả JSON trong phản hồi. stop_reason = {data.get('stop_reason')}")
