"""Thử nhanh kết nối tới Claude bằng khóa và model trong .env (gọi 1 lần, tốn vài chục token).

Chạy trong thư mục advisor:
  python -m scripts.check_claude
In kết quả OK hoặc lý do lỗi (401 = khóa sai, 404 = tên model sai, 429/529 = quá tải). Không in khóa.
"""

import asyncio
import sys

import httpx

from app.core.config import get_settings
from app.services import usage_meter
from app.services.claude import ClaudeClient, ClaudeError

SCHEMA = {
    "type": "object",
    "properties": {"tra_loi": {"type": "string"}},
    "required": ["tra_loi"],
}


async def main() -> int:
    s = get_settings()
    print(
        f"Model: {s.anthropic_model} | địa chỉ: {s.anthropic_base_url} | có khóa: {bool(s.anthropic_api_key)}"
    )
    if not s.anthropic_api_key:
        print("Thiếu ANTHROPIC_API_KEY trong .env")
        return 1
    async with httpx.AsyncClient(base_url=s.anthropic_base_url, timeout=s.anthropic_timeout_seconds) as http:
        client = ClaudeClient(http, s.anthropic_model, s.anthropic_api_key, max_retries=1)
        calls = usage_meter.start()
        try:
            result = await client.chat_json(
                [
                    {"role": "system", "content": "Trả lời một câu ngắn bằng tiếng Việt."},
                    {"role": "user", "content": "Nói xin chào."},
                ],
                SCHEMA,
            )
        except ClaudeError as ex:
            print("LỖI:", str(ex)[:400])
            return 1
    call = calls[0]
    print("OK:", result["tra_loi"])
    print(f"Token vào {call.input_tokens}, ra {call.output_tokens}, {call.latency_ms} ms")
    return 0


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    raise SystemExit(asyncio.run(main()))
