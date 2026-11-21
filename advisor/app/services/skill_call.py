"""Phần dùng chung của các lượt AI: đọc skill, ghép message, gọi AI, kiểm tra, sai thì gọi lại 1 lần (nói rõ lỗi)."""

import asyncio
import json
import logging
import re
from collections.abc import Callable
from pathlib import Path
from typing import Any, TypeVar

from pydantic import ValidationError

from app.core.config import get_settings
from app.services import usage_meter
from app.services.ollama import OllamaError

logger = logging.getLogger("advisor.skill_call")
SKILLS_DIR = Path(__file__).resolve().parent.parent / "skills"
MAX_ATTEMPTS = 2  # sai thì gọi lại đúng 1 lần

# Lần gọi lại sau khi kết quả bị bộ kiểm từ chối: nói rõ lỗi để model sửa đúng chỗ.
# Gửi y nguyên yêu cầu cũ thì model (Claude Haiku không nhận temperature) dễ ra lại đúng kết quả sai.
RETRY_NOTE = (
    "Kết quả trước của bạn bị hệ thống từ chối.\n"
    "Lý do: {defect}\n"
    "Kết quả trước (JSON): {previous}\n"
    "Viết lại toàn bộ kết quả theo đúng khuôn JSON, sửa đúng chỗ bị từ chối và giữ nguyên phần đã đúng."
)
MAX_DEFECT_CHARS = 400
MAX_PREVIOUS_CHARS = 1500

T = TypeVar("T")  # kiểu kết quả sau khi kiểm tra: list[ScoreItem], EcResult, PickResult...


# Ngân sách thời gian cho một lượt viết lời (config.writer_budget_seconds); hàm riêng để test đổi được
def writer_budget() -> float:
    return get_settings().writer_budget_seconds


def load_skill(path: Path) -> tuple[str, str]:
    """Đọc file skill, lấy số phiên bản ở dòng đầu ("skill_version 1.1")."""
    text = path.read_text(encoding="utf-8")
    match = re.search(r"skill_version\s+([\w.]+)", text)
    return text, match.group(1) if match else "unknown"


# Lý do ngắn gọn để nói với model: lỗi kiểm của writer là một câu tiếng Việt; lỗi khuôn pydantic thì gom "mục: lỗi"
def defect_of(error: ValueError) -> str:
    if isinstance(error, ValidationError):
        parts = [f"{'.'.join(str(x) for x in e['loc']) or 'kết quả'}: {e['msg']}" for e in error.errors()[:3]]
        text = "; ".join(parts)
    else:
        text = str(error).strip()
    return (text or "kết quả không hợp lệ")[:MAX_DEFECT_CHARS]


def retry_note_for(error: ValueError, raw: Any) -> dict[str, str]:
    previous = json.dumps(raw, ensure_ascii=False, separators=(",", ":")) if raw is not None else "(không có)"
    content = RETRY_NOTE.format(defect=defect_of(error), previous=previous[:MAX_PREVIOUS_CHARS])
    return {"role": "user", "content": content}


class SkillCall:
    """Một lượt AI: 1 file skill cố định (system) + 1 JSON đầu vào (user) + kiểm tra kết quả."""

    def __init__(self, llm: Any, model: str, skill_path: Path) -> None:
        self._llm = llm
        self.model = model
        self.skill, self.skill_version = load_skill(skill_path)  # đọc 1 lần lúc khởi động
        self.name = skill_path.stem  # tên skill, gắn nhãn cho các lần gọi AI trong báo cáo token

    async def run(
        self, user: dict[str, Any], schema: dict[str, Any], check: Callable[[dict[str, Any]], T]
    ) -> T | None:
        """Gọi AI tối đa MAX_ATTEMPTS lần. Trả kết quả đã kiểm tra, hoặc None nếu lần nào cũng sai hoặc hết ngân sách thời gian.

        check: hàm kiểm tra riêng của từng lượt, nhận JSON AI trả về, sai thì ném ValueError.
        """
        budget = writer_budget()
        try:
            async with asyncio.timeout(budget):
                return await self._attempts(user, schema, check)
        except TimeoutError:
            logger.warning("%s: hết ngân sách %.0f giây, dùng lời dự phòng", self.name, budget)
            return None

    async def _attempts(
        self, user: dict[str, Any], schema: dict[str, Any], check: Callable[[dict[str, Any]], T]
    ) -> T | None:
        messages = [
            {"role": "system", "content": self.skill},
            # viết liền không khoảng trắng để tiết kiệm token
            {"role": "user", "content": json.dumps(user, ensure_ascii=False, separators=(",", ":"))},
        ]
        retry_note: dict[str, str] | None = None
        with usage_meter.purpose(self.name, self.skill_version):
            for _ in range(MAX_ATTEMPTS):
                raw: Any = None
                try:
                    raw = await self._llm.chat_json(
                        messages + ([retry_note] if retry_note else []), schema, temperature=0.0
                    )
                    return check(raw)
                except OllamaError:
                    # lỗi đường truyền hoặc model quá tải: không có lỗi nào để nêu, gọi lại như cũ
                    continue
                except ValueError as error:  # ValidationError của pydantic cũng là ValueError
                    retry_note = retry_note_for(error, raw)
        return None
