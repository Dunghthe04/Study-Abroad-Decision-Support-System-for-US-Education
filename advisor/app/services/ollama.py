"""Gọi Ollama /api/chat và ép LLM trả JSON đúng schema (structured outputs)."""

import json
from typing import Any

# Thư viện python dùng để gửi http request
import httpx


class OllamaError(Exception):
    """Ollama không phản hồi, trả lỗi HTTP, hoặc trả nội dung không phải JSON."""


# Class giao tiếp với ollama
class OllamaClient:
    """Giống RecommendationAiClient bên .NET: nhận sẵn http client đã cấu hình base_url và timeout."""

    def __init__(self, http: httpx.AsyncClient, model: str) -> None:
        self._http = http
        self._model = model

    # Gửi conversation + JSON schema cho Ollama và nhận JSON kết quả
    async def chat_json(
        self, messages: list[dict[str, str]], schema: dict[str, Any], temperature: float = 0.0
    ) -> dict[str, Any]:
        payload = {
            "model": self._model,
            "messages": messages,
            "format": schema,  # Ollama ép đầu ra khớp JSON schema này
            "stream": False,  # nhận 1 lần cả câu trả lời
            "think": False,  # tắt chế độ suy nghĩ của Qwen3: nhanh hơn, không lẫn <think> vào JSON
            # temperature 0.0: câu trả lời ổn định, ít ngẫu nhiên
            "options": {"temperature": temperature},
        }
        try:
            resp = await self._http.post("/api/chat", json=payload)
            resp.raise_for_status()
        except httpx.HTTPError as ex:
            raise OllamaError(f"Gọi Ollama thất bại: {ex!r}") from ex

        try:
            return json.loads(resp.json()["message"]["content"])
        except (KeyError, ValueError) as ex:  # json.JSONDecodeError là lớp con của ValueError
            raise OllamaError("Ollama trả về nội dung không đọc được thành JSON") from ex
