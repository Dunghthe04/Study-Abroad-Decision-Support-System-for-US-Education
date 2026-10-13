"""Đồ giả dùng chung cho các file test."""

from typing import Any


class FakeLLM:
    """LLM giả: trả lần lượt các câu trả lời soạn sẵn (gặp Exception thì ném ra) và ghi lại mỗi lần được gọi."""

    def __init__(self, *answers: dict[str, Any] | Exception) -> None:
        self._answers = list(answers)
        self.calls: list[dict[str, Any]] = []

    async def chat_json(
        self, messages: list[dict[str, str]], schema: dict[str, Any], temperature: float = 0.0
    ) -> dict[str, Any]:
        self.calls.append({"messages": messages, "schema": schema, "temperature": temperature})
        answer = self._answers.pop(0)
        if isinstance(answer, Exception):
            raise answer
        return answer
