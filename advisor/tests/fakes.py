"""Đồ giả dùng chung cho các file test."""

from typing import Any

from app.schemas.slm import SlmResult
from app.services.branches import build_branches
from app.services.chat_flow import ChatFlow
from app.services.slm_contract import FixedSlm


# ChatFlow với SLM giả: tin nào cũng đọc ra cùng một kết quả (mặc định: trả lời câu hồ sơ đang chờ).
# handlers là các skill cũ, được bọc thành nhánh như lúc chạy thật
def flow_with(handlers: dict[str, Any], slm: SlmResult | None = None) -> ChatFlow:
    return ChatFlow(FixedSlm(slm or SlmResult(type="mac_dinh")), build_branches(handlers))


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
