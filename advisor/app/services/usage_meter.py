"""Đo token của từng lần gọi AI trong một lượt chat.

Mỗi lần gọi Claude hoặc Ollama, client gọi một hàm ở đây để ghi lại token vào, ra, cache (số do chính nhà cung cấp trả về).
Các lần gọi của một lượt chat được gom vào một danh sách; ChatFlow.reply trả danh sách này trong ChatResponse.ai_usage
để backend .NET ghi vào bảng ai_calls và tính chi phí (giai đoạn 2).

Dùng contextvars để không phải đổi chữ ký các hàm: danh sách "đang mở" gắn với lượt chat hiện tại, kể cả các tác vụ chạy song song
(asyncio.gather) sinh ra trong lượt đó (chúng dùng chung đúng danh sách này). Ngoài lượt chat (ví dụ route gợi ý trường) không có
danh sách nào đang mở thì mọi hàm ghi đều không làm gì.
"""

from collections.abc import Awaitable, Iterator
from contextlib import contextmanager
from contextvars import ContextVar
from typing import Any

from app.schemas.chat import AiUsageCall

_calls: ContextVar[list[AiUsageCall] | None] = ContextVar("ai_usage_calls", default=None)
_purpose: ContextVar[str] = ContextVar("ai_usage_purpose", default="")


def start() -> list[AiUsageCall]:
    """Mở danh sách mới cho lượt chat hiện tại và trả về chính danh sách đó."""
    calls: list[AiUsageCall] = []
    _calls.set(calls)
    return calls


async def metered(awaitable: Awaitable[Any]) -> Any:
    """Chạy một lượt AI ngoài chat (route /recommend): mở danh sách, chờ kết quả, gắn token vào kết quả."""
    calls = start()
    result = await awaitable
    result.ai_usage = list(calls)
    return result


@contextmanager
def purpose(name: str) -> Iterator[None]:
    """Trong khối này, các lần gọi AI được gắn nhãn công việc (ví dụ tên skill) để báo cáo theo tính năng."""
    token = _purpose.set(name)
    try:
        yield
    finally:
        _purpose.reset(token)


def _record(call: AiUsageCall) -> None:
    calls = _calls.get()
    if calls is not None:
        calls.append(call)


def _int(value: Any) -> int:
    return int(value) if isinstance(value, (int, float)) and not isinstance(value, bool) else 0


def claude_ok(model: str, usage: dict[str, Any] | None, latency_ms: int) -> None:
    """Một lần gọi Claude thành công. usage của Anthropic: input_tokens (chưa gồm cache), output_tokens, cache_read_input_tokens."""
    u = usage or {}
    _record(
        AiUsageCall(
            source="claude",
            model=model,
            purpose=_purpose.get(),
            input_tokens=_int(u.get("input_tokens")),
            output_tokens=_int(u.get("output_tokens")),
            cached_tokens=_int(u.get("cache_read_input_tokens")),
            ok=True,
            latency_ms=latency_ms,
        )
    )


def ollama_ok(model: str, body: dict[str, Any], latency_ms: int) -> None:
    """Một lần gọi Ollama thành công. Ollama trả prompt_eval_count (vào) và eval_count (ra) trong body."""
    _record(
        AiUsageCall(
            source="ollama",
            model=model,
            purpose=_purpose.get(),
            input_tokens=_int(body.get("prompt_eval_count")),
            output_tokens=_int(body.get("eval_count")),
            ok=True,
            latency_ms=latency_ms,
        )
    )


def failed(source: str, model: str, error: str, latency_ms: int) -> None:
    """Một lần gọi lỗi (quá tải, hết thời gian, bị từ chối). Không có token nhưng vẫn ghi để biết có bao nhiêu lần gọi hỏng."""
    _record(
        AiUsageCall(
            source=source,
            model=model,
            purpose=_purpose.get(),
            ok=False,
            error=error[:200],
            latency_ms=latency_ms,
        )
    )
