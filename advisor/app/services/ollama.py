import httpx

from app.core.config import Settings


class OllamaClient:
    """Thin client for the Ollama HTTP API (chat + embeddings)."""

    def __init__(self, settings: Settings, http: httpx.AsyncClient | None = None):
        self._settings = settings
        self._http = http or httpx.AsyncClient(
            base_url=settings.ollama_base_url, timeout=settings.llm_timeout_seconds
        )

    async def chat(self, messages: list[dict[str, str]]) -> str:
        resp = await self._http.post(
            "/api/chat",
            json={
                "model": self._settings.llm_model,
                "messages": messages,
                "stream": False,
                "think": False,
                "options": {"temperature": self._settings.llm_temperature},
            },
        )
        resp.raise_for_status()
        return resp.json()["message"]["content"].strip()

    async def embed(self, texts: list[str]) -> list[list[float]]:
        resp = await self._http.post(
            "/api/embed", json={"model": self._settings.embedding_model, "input": texts}
        )
        resp.raise_for_status()
        return resp.json()["embeddings"]

    async def is_healthy(self) -> bool:
        try:
            resp = await self._http.get("/api/tags", timeout=5)
            return resp.status_code == 200
        except httpx.HTTPError:
            return False

    async def aclose(self) -> None:
        await self._http.aclose()
