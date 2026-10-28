"""skill_nhu_cau (B0), phần đọc: Qwen đọc câu đầu tiên ra need, branch và các thông tin đã nói sẵn.

Phần run(state, message) (ghi state, chuyển bước) làm ở B1. Ở đây chỉ có hàm đọc.
"""

from dataclasses import dataclass, field
from pathlib import Path
from typing import Any

from app.services.skill_call import SKILLS_DIR, SkillCall
from app.services.skills.info_to_field import to_fields

SKILL_PATH = SKILLS_DIR / "skill_nhu_cau.md"
NEEDS = ("profile_advice", "knowledge", "service", "unclear")
BRANCHES = ("DH",)

# Khuôn JSON Ollama phải trả
SCHEMA = {
    "type": "object",
    "required": ["need", "branch", "info_given"],
    "properties": {
        "need": {"enum": list(NEEDS)},
        "branch": {"enum": [*BRANCHES, None]},
        "info_given": {"type": "object", "additionalProperties": {"type": "string"}},
    },
}


# Kết quả đọc: info đã đổi sang khóa chuẩn (gpa, english...), giá trị vẫn là chữ học sinh viết
@dataclass
class NhuCauRead:
    need: str
    branch: str | None = None
    info: dict[str, Any] = field(default_factory=dict)
    ai_used: bool = False


# Kiểm tra JSON Qwen trả, sai thì ném ValueError để SkillCall gọi lại
def check_read(raw: dict[str, Any]) -> NhuCauRead:
    need = raw.get("need")
    if need not in NEEDS:
        raise ValueError(f"need lạ: {need}")
    branch = raw.get("branch")
    if branch is not None and branch not in BRANCHES:
        raise ValueError(f"branch lạ: {branch}")
    info = raw.get("info_given") or {}
    if not isinstance(info, dict):
        raise ValueError("info_given phải là object")
    return NhuCauRead(need=need, branch=branch, info=to_fields(info), ai_used=True)


# Qwen lỗi 2 lần: coi như chưa rõ nhu cầu, không đoán, không ghi thông tin nào
def fallback() -> NhuCauRead:
    return NhuCauRead(need="unclear")


# Gọi Qwen đọc câu đầu tiên
class NhuCauReader:
    def __init__(self, llm: Any, model: str, skill_path: Path = SKILL_PATH) -> None:
        self._call = SkillCall(llm, model, skill_path)
        self.skill_version = self._call.skill_version

    async def read(self, message: str) -> NhuCauRead:
        result = await self._call.run({"message": message}, SCHEMA, check_read)
        return result if result is not None else fallback()
