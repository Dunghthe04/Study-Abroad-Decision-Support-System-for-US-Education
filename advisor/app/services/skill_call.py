"""Phần dùng chung của 3 lượt AI: đọc skill, ghép message, gọi AI, kiểm tra, sai thì gọi lại 1 lần."""

import json
import re
from collections.abc import Callable
from pathlib import Path
from typing import Any, TypeVar

from app.services.ollama import OllamaError

SKILLS_DIR = Path(__file__).resolve().parent.parent / "skills"
MAX_ATTEMPTS = 2     # sai thì gọi lại đúng 1 lần

T = TypeVar("T")     # kiểu kết quả sau khi kiểm tra: list[ScoreItem], EcResult, PickResult...


def load_skill(path: Path) -> tuple[str, str]:
    """Đọc file skill, lấy số phiên bản ở dòng đầu ("skill_version 1.1")."""
    text = path.read_text(encoding="utf-8")
    match = re.search(r"skill_version\s+([\w.]+)", text)
    return text, match.group(1) if match else "unknown"


class SkillCall:
    """Một lượt AI: 1 file skill cố định (system) + 1 JSON đầu vào (user) + kiểm tra kết quả."""

    def __init__(self, llm: Any, model: str, skill_path: Path) -> None:
        self._llm = llm
        self.model = model
        self.skill, self.skill_version = load_skill(skill_path)   # đọc 1 lần lúc khởi động

    async def run(self, user: dict[str, Any], schema: dict[str, Any], check: Callable[[dict[str, Any]], T]) -> T | None:
        """Gọi AI tối đa MAX_ATTEMPTS lần. Trả kết quả đã kiểm tra, hoặc None nếu lần nào cũng sai.

        check: hàm kiểm tra riêng của từng lượt, nhận JSON AI trả về, sai thì ném ValueError.
        """
        messages = [
            {"role": "system", "content": self.skill},
            # viết liền không khoảng trắng để tiết kiệm token
            {"role": "user", "content": json.dumps(user, ensure_ascii=False, separators=(",", ":"))},
        ]
        for _ in range(MAX_ATTEMPTS):
            try:
                raw = await self._llm.chat_json(messages, schema, temperature=0.0)
                return check(raw)
            except (OllamaError, ValueError):   # ValidationError của pydantic cũng là ValueError
                continue
        return None
