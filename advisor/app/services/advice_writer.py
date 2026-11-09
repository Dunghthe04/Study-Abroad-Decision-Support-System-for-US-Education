"""Claude viết câu trả lời tư vấn hồ sơ theo skill do SLM vừa sinh.

System = quy tắc chung (viet_tra_loi_ho_so.md) + skill riêng của học sinh. User = câu hỏi, hồ sơ, mức tính sẵn và
danh sách trường đã gợi ý. Sai thì gọi lại 1 lần; vẫn sai thì trả None để nhánh tư vấn báo chưa trả lời được.
"""

import json
from typing import Any

from app.schemas.chat import ResultSchool
from app.schemas.slm import SkillRequest
from app.services.explainer import GROUP_LABEL
from app.services.goal_target import plain
from app.services.ollama import OllamaError
from app.services.skill_call import MAX_ATTEMPTS, SKILLS_DIR, load_skill

PROMPT_PATH = SKILLS_DIR / "viet_tra_loi_ho_so.md"
ANSWER_SCHEMA = {
    "type": "object",
    "properties": {"tra_loi": {"type": "string"}},
    "required": ["tra_loi"],
}
# Câu hứa đỗ (đã bỏ dấu, chữ thường). Gặp là bắt viết lại: hệ thống không được hứa kết quả
PROMISES = (
    "chac chan do",
    "chac chan dau",
    "chac chan trung tuyen",
    "dam bao do",
    "dam bao dau",
    "dam bao trung tuyen",
    "100% do",
    "100% dau",
    "guaranteed",
)


# Lấy câu trả lời từ JSON Claude trả về; rỗng hoặc có câu hứa đỗ thì ném ValueError
def check_answer(raw: dict[str, Any]) -> str:
    text = raw.get("tra_loi")
    if not isinstance(text, str) or not text.strip():
        raise ValueError("câu trả lời rỗng")
    folded = plain(text)
    if any(promise in folded for promise in PROMISES):
        raise ValueError("câu trả lời hứa đỗ")
    return text.strip()


class AdviceWriter:
    def __init__(self, llm: Any) -> None:
        self._llm = llm
        self._rules, self.version = load_skill(PROMPT_PATH)  # đọc 1 lần lúc khởi động

    async def write(self, skill: str, req: SkillRequest, schools: list[ResultSchool]) -> str | None:
        """Trả câu trả lời đã kiểm, hoặc None nếu Claude sai cả MAX_ATTEMPTS lần."""
        user = {
            "cau_hoi": req.cau_hoi,
            "ho_so": req.ho_so,
            "tinh_san": req.tinh_san,
            "schools": [{"name": s.name, "group": GROUP_LABEL.get(s.group or "", s.group)} for s in schools],
        }
        messages = [
            {"role": "system", "content": self._rules},
            {"role": "system", "content": skill},
            {"role": "user", "content": json.dumps(user, ensure_ascii=False, separators=(",", ":"))},
        ]
        for _ in range(MAX_ATTEMPTS):
            try:
                raw = await self._llm.chat_json(messages, ANSWER_SCHEMA, temperature=0.0)
                return check_answer(raw)
            except (OllamaError, ValueError):  # ClaudeError là OllamaError
                continue
        return None
