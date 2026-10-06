"""Chạy thử Ollama bằng tay: python -m scripts.try_ollama

Cần Ollama đang chạy và đã có model (ollama pull qwen3:8b).
"""

import asyncio
import time

import httpx

from app.core.config import get_settings
from app.services.ollama import OllamaClient

SCHEMA = {
    "type": "object",
    "properties": {
        "greeting": {"type": "string"},
        "lucky_number": {"type": "integer"},
    },
    "required": ["greeting", "lucky_number"],
}


async def main() -> None:
    settings = get_settings()
    print(f"Model: {settings.llm_model} @ {settings.ollama_base_url}")

    async with httpx.AsyncClient(
        base_url=settings.ollama_base_url, timeout=settings.llm_timeout_seconds
    ) as http:
        client = OllamaClient(http, settings.llm_model)
        start = time.perf_counter()
        result = await client.chat_json(
            [{"role": "user", "content": "Chào bằng tiếng Việt và chọn một số may mắn từ 1 đến 9."}],
            SCHEMA,
        )
        print(f"{time.perf_counter() - start:.1f}s ->", result)


if __name__ == "__main__":
    asyncio.run(main())
