"""Khôi phục dấu tiếng Việt cho câu học sinh gõ không dấu ("opt la gi" -> "OPT là gì").

Chỉ dùng để TÌM kiếm trong kho: máy tìm theo nghĩa chấm điểm thấp khi thiếu dấu (đo thật: "opt la gi" 0.58 so với 1.00 của "OPT là gì?"),
nên câu không dấu bị từ chối dù kho có đáp án. Câu học sinh gõ vẫn được giữ nguyên cho bước chọn đáp án và trả lời.

An toàn: chỉ chấp nhận kết quả nếu Claude CHỈ thêm dấu (bỏ dấu đi thì ra đúng chữ gốc). Claude đổi chữ, trả rỗng hay lỗi
thì coi như không khôi phục được, chat quay về hành vi cũ chứ không bao giờ hỏng.
"""

import json
import logging
import unicodedata
from typing import Any

from app.services import usage_meter

logger = logging.getLogger("advisor.accent")

PURPOSE = "khoi_phuc_dau"  # nhãn trong báo cáo token
MAX_CHARS = 300  # dài hơn không phải câu hỏi thường: không tốn một lần gọi

SYSTEM = (
    "Bạn thêm dấu tiếng Việt cho câu người dùng gõ không dấu. "
    "Chỉ THÊM DẤU, giữ nguyên từng chữ, thứ tự và các từ viết tắt hay tên riêng (OPT, F-1, I-20, DS-160, IELTS...). "
    "Không thêm, bỏ, sửa hay dịch chữ nào. Nếu câu không phải tiếng Việt hoặc đã đúng thì trả nguyên văn. "
    'Đầu vào JSON {"text": "..."}. Trả JSON {"text": "câu đã thêm dấu"}.'
)
SCHEMA = {
    "type": "object",
    "properties": {"text": {"type": "string"}},
    "required": ["text"],
}


# Chỉ còn chữ cái và số viết thường, bỏ dấu: so sánh hai câu có "cùng chữ" hay không
def fold(text: str) -> str:
    decomposed = unicodedata.normalize("NFD", text.replace("đ", "d").replace("Đ", "D"))
    return "".join(c for c in decomposed if c.isalnum() and not unicodedata.combining(c)).lower()


# Câu toàn ký tự ASCII (không một chữ có dấu nào) và có chữ cái: ứng viên để khôi phục dấu
def needs_restore(text: str) -> bool:
    text = text.strip()
    return 0 < len(text) <= MAX_CHARS and text.isascii() and any(c.isalpha() for c in text)


class AccentRestorer:
    def __init__(self, llm: Any) -> None:
        self._llm = llm  # có chat_json(messages, schema, temperature) như ClaudeClient

    async def restore(self, text: str) -> str | None:
        """Câu đã thêm dấu; None nếu không cần, lỗi, hoặc Claude trả câu khác ngoài việc thêm dấu."""
        if not needs_restore(text):
            return None
        messages = [
            {"role": "system", "content": SYSTEM},
            {"role": "user", "content": json.dumps({"text": text}, ensure_ascii=False)},
        ]
        try:
            with usage_meter.purpose(PURPOSE):
                raw = await self._llm.chat_json(messages, SCHEMA, temperature=0.0)
        except Exception:  # lỗi mạng, quá tải, hết thời gian: không khôi phục, đường chính vẫn chạy
            logger.warning("Không khôi phục được dấu cho câu không dấu", exc_info=True)
            return None
        restored = raw.get("text") if isinstance(raw, dict) else None
        if not isinstance(restored, str):
            return None
        restored = restored.strip()
        if not restored or restored == text.strip() or fold(restored) != fold(text):
            return None
        return restored
