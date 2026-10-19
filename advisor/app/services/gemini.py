"""Gọi Gemini Interactions API và ép trả JSON đúng schema. Cùng giao diện chat_json với OllamaClient."""

import asyncio
import json
from typing import Any

import httpx  # Gửi HTTP request đến Gemini API.

from app.services.ollama import OllamaError  # Gọi gemini API thay cho Ollama.


class GeminiError(OllamaError):
    """Kế thừa OllamaError để SkillCall vẫn gọi lại khi lỗi mạng hoặc JSON sai."""


# Khởi tạo GeminiClient
class GeminiClient:
    def __init__(
        self,
        http: httpx.AsyncClient,
        model: str,
        api_key: str,
        *,
        thinking_level: str | None = "low",
        max_retries: int = 3,
        min_interval: float = 0.0,
    ) -> None:
        self._http = http
        self._model = model
        self._api_key = api_key
        self._thinking_level = thinking_level
        self._max_retries = max_retries
        self._min_interval = min_interval
        self.usage_log: list[dict[str, Any]] = []

    async def chat_json(
        self, messages: list[dict[str, str]], schema: dict[str, Any], temperature: float = 0.0
    ) -> dict[str, Any]:
        # SkillCall gửi system = skill, user = JSON hồ sơ
        system = "\n\n".join(m["content"] for m in messages if m["role"] == "system")
        user = "\n\n".join(m["content"] for m in messages if m["role"] == "user")
        generation_config: dict[str, Any] = {"temperature": temperature}
        if self._thinking_level:
            generation_config["thinking_level"] = self._thinking_level
        body: dict[str, Any] = {
            "model": self._model,
            "input": user,
            "response_format": {"type": "text", "mime_type": "application/json", "schema": schema},
            "generation_config": generation_config,
        }
        if system:
            body["system_instruction"] = system
        data = await self._post_with_retry(body)
        try:
            return json.loads(self._extract_text(data))
        except ValueError as ex:
            raise GeminiError("Gemini trả về nội dung không đọc được thành JSON") from ex

    async def _post_with_retry(self, body: dict[str, Any]) -> dict[str, Any]:
        last = "chưa gọi"
        for attempt in range(self._max_retries):
            if self._min_interval:
                await asyncio.sleep(self._min_interval)
            started = asyncio.get_running_loop().time()
            try:
                resp = await self._http.post(
                    "/v1beta/interactions",
                    json=body,
                    headers={"x-goog-api-key": self._api_key},
                )
            except httpx.HTTPError as ex:
                last = repr(ex)
            else:
                if resp.status_code == 429 or resp.status_code >= 500:
                    last = f"HTTP {resp.status_code}"
                else:
                    try:
                        resp.raise_for_status()
                    except httpx.HTTPError as ex:
                        raise GeminiError(f"Gemini từ chối yêu cầu: {resp.text[:300]}") from ex
                    data = resp.json()
                    seconds = asyncio.get_running_loop().time() - started
                    self.usage_log.append({"usage": data.get("usage"), "seconds": round(seconds, 2)})
                    return data
            await asyncio.sleep(2**attempt * 2)
        raise GeminiError(f"Gọi Gemini thất bại sau {self._max_retries} lần: {last}")

    @staticmethod
    def _extract_text(data: dict[str, Any]) -> str:
        # JSON nằm trong bước type = model_output, không nằm ở khóa ngoài cùng
        if data.get("status") not in (None, "completed"):
            raise GeminiError(f"Gemini chưa hoàn thành: status = {data.get('status')}")
        parts: list[str] = []
        for step in data.get("steps", []):
            if step.get("type") == "model_output":
                parts += [
                    c["text"] for c in step.get("content", []) if c.get("type") == "text" and "text" in c
                ]
        if not parts:
            raise GeminiError(f"Không thấy model_output trong phản hồi. Các khóa có: {sorted(data)}")
        return "".join(parts)
