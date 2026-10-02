from typing import Protocol

from app.rag.prompts import DISCLAIMER, SYSTEM_PROMPT, build_context, build_user_level_note
from app.rag.retriever import RetrievedChunk
from app.schemas.chat import ChatRequest, ChatResponse, Citation

MAX_HISTORY = 6


class LLM(Protocol):
    async def chat(self, messages: list[dict[str, str]]) -> str: ...
    async def embed(self, texts: list[str]) -> list[list[float]]: ...


class ChunkRetriever(Protocol):
    async def search(
        self, query: str, embedding: list[float], study_level: str | None = None
    ) -> list[RetrievedChunk]: ...


class AdvisorService:
    """RAG pipeline: embed question -> hybrid retrieve -> LLM answers from context with citations."""

    def __init__(self, llm: LLM, retriever: ChunkRetriever):
        self._llm = llm
        self._retriever = retriever

    async def chat(self, request: ChatRequest) -> ChatResponse:
        # Client-supplied system messages are dropped so they cannot override the server prompt.
        history = [m for m in request.messages if m.role != "system"][-MAX_HISTORY:]
        question = next((m.content for m in reversed(history) if m.role == "user"), "")

        [embedding] = await self._llm.embed([question])
        chunks = await self._retriever.search(question, embedding, request.study_level)

        messages = [
            {"role": "system", "content": SYSTEM_PROMPT},
            {"role": "system", "content": build_user_level_note(request.study_level)},
            {"role": "system", "content": f"NGỮ CẢNH:\n{build_context(chunks)}"},
            *({"role": m.role, "content": m.content} for m in history),
        ]
        answer = await self._llm.chat(messages)

        citations = [Citation(title=c.title, url=c.source_url, snippet=c.content[:200]) for c in chunks]
        return ChatResponse(answer=answer, citations=citations, disclaimer=DISCLAIMER)
