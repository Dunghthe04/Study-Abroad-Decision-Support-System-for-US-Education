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

    def __init__(
        self,
        http: httpx.AsyncClient,
        model: str,
        *,
        num_ctx: int = 6144,
        num_gpu: int = 99,
        seed: int = 42,
        keep_alive: str = "30m",
        embedding_model: str = "bge-m3",
    ) -> None:
        self._http = http
        self._model = model
        self._embedding_model = embedding_model
        self._options = {"num_ctx": num_ctx, "num_gpu": num_gpu, "seed": seed}
        self._keep_alive = keep_alive

    # Biến mỗi đoạn chữ thành một vector (bge-m3: 1024 số) để so sánh nghĩa với kho kiến thức
    async def embed(self, texts: list[str]) -> list[list[float]]:
        payload = {"model": self._embedding_model, "input": texts, "keep_alive": self._keep_alive}
        try:
            resp = await self._http.post("/api/embed", json=payload)
            resp.raise_for_status()
        except httpx.HTTPError as ex:
            raise OllamaError(f"Gọi Ollama embed thất bại: {ex!r}") from ex

        try:
            vectors = resp.json()["embeddings"]
        except (KeyError, ValueError) as ex:
            raise OllamaError("Ollama embed trả về nội dung không đọc được") from ex
        if len(vectors) != len(texts):  # mỗi đoạn chữ phải có đúng một vector
            raise OllamaError(f"Gửi {len(texts)} đoạn chữ nhưng nhận {len(vectors)} vector")
        return vectors

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
            "keep_alive": self._keep_alive,
            # temperature 0.0: câu trả lời ổn định, ít ngẫu nhiên; num_gpu/num_ctx: chạy hết trên GPU
            "options": {**self._options, "temperature": temperature},
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
