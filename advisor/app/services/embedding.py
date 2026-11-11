import time
from typing import Any

import httpx

from app.services import usage_meter
from app.services.ollama import OllamaClient, OllamaError


class OpenAiEmbeddingClient:
    def __init__(self, http: httpx.AsyncClient, model: str, api_key: str = "") -> None:
        self._http = http
        self._model = model
        self._api_key = api_key

    async def embed(self, texts: list[str]) -> list[list[float]]:
        headers: dict[str, str] = {}
        if self._api_key:
            headers["Authorization"] = f"Bearer {self._api_key}"
        payload = {"model": self._model, "input": texts}
        started = time.perf_counter()
        try:
            resp = await self._http.post("/embeddings", json=payload, headers=headers)
            resp.raise_for_status()
        except httpx.HTTPError as ex:
            usage_meter.failed("embedding", self._model, repr(ex), self._elapsed_ms(started))
            raise OllamaError(f"Gọi Embedding API thất bại: {ex!r}") from ex

        try:
            body = resp.json()
            usage = body.get("usage") or {}
            usage_meter._record(
                usage_meter.AiUsageCall(
                    source="embedding",
                    model=self._model,
                    purpose=usage_meter._purpose.get(),
                    input_tokens=usage_meter._int(usage.get("prompt_tokens")),
                    output_tokens=0,
                    ok=True,
                    latency_ms=self._elapsed_ms(started),
                )
            )
            vectors = [item["embedding"] for item in body["data"]]
        except (KeyError, ValueError, TypeError) as ex:
            raise OllamaError("Embedding API trả về nội dung không đọc được") from ex
        if len(vectors) != len(texts):
            raise OllamaError(f"Gửi {len(texts)} đoạn chữ nhưng nhận {len(vectors)} vector")
        return vectors

    def _elapsed_ms(self, started: float) -> int:
        return int((time.perf_counter() - started) * 1000)
