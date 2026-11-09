"""Claude viết câu trả lời tư vấn hồ sơ theo skill do SLM vừa sinh.

System = quy tắc chung (viet_tra_loi_ho_so.md) + skill riêng của học sinh. User = câu hỏi, hồ sơ, mức tính sẵn và
danh sách trường đã gợi ý. Sai thì gọi lại 1 lần; vẫn sai thì trả None để nhánh tư vấn báo chưa trả lời được.
"""

import json
import logging
import re
import unicodedata
from typing import Any

from app.schemas.chat import ResultSchool
from app.schemas.slm import SkillRequest
from app.services.explainer import GROUP_LABEL
from app.services.ollama import OllamaError
from app.services.skill_call import MAX_ATTEMPTS, SKILLS_DIR, load_skill

logger = logging.getLogger(__name__)

PROMPT_PATH = SKILLS_DIR / "viet_tra_loi_ho_so.md"
ANSWER_SCHEMA = {
    "type": "object",
    "properties": {"tra_loi": {"type": "string"}},
    "required": ["tra_loi"],
}
# Câu hứa đỗ: chỉ bắt cấu trúc khẳng định kết quả (giữ dấu nên "độ/đó/đơn/đầu" không bị nhầm với "đỗ/đậu")
PROMISE = re.compile(
    r"\b(?:(?:chắc chắn|đảm bảo|bảo đảm|cam kết)(?:\s+là)?(?:\s+bạn)?(?:\s+sẽ)?\s+(?:đỗ|đậu|trúng tuyển|được nhận)"
    r"|100\s*%\s+(?:đỗ|đậu|trúng tuyển|được nhận)"
    r"|guaranteed)\b"
)
# Phủ định ngay trước cụm hứa, cùng vế câu, cách tối đa 3 từ: "không thể chắc chắn đậu" là câu đúng mực
NEGATION = re.compile(r"\b(?:không|chẳng|chưa|khó)(?:\s+\S+){0,3}\s*$")
# "không chỉ/không những" là nối ý, không phải phủ định
NOT_NEGATION = re.compile(r"\bkhông\s+(?:chỉ|những)\b")
CLAUSE_END = re.compile(r"[,.;:!?\n]")
# Lần viết lại: nói rõ cụm nào bị bắt để Claude chỉ đổi cách nói (gửi y nguyên thì temperature 0 ra lại đúng câu cũ)
RETRY_NOTE = (
    "Câu trả lời trước của bạn:\n{answer}\n\n"
    'Câu đó có cụm "{phrase}" bị coi là hứa kết quả tuyển sinh. Viết lại câu trả lời: giữ nguyên nội dung, '
    'chỉ đổi cách nói về khả năng đỗ thành "cơ hội", "khả năng", "phù hợp".'
)


class PromiseError(ValueError):
    """Câu trả lời có cụm hứa đỗ. Lỗi chỉ nêu cụm khớp, không chép câu trả lời (có số liệu hồ sơ của học sinh)."""

    def __init__(self, phrase: str) -> None:
        super().__init__(f'câu trả lời hứa đỗ: "{phrase}"')
        self.phrase = phrase


# Trả cụm hứa đỗ đầu tiên trong câu trả lời, None nếu không có. Cụm nằm sau phủ định cùng vế câu thì bỏ qua
def find_promise(text: str) -> str | None:
    folded = unicodedata.normalize("NFC", text).lower()  # NFC: chữ có dấu tổ hợp vẫn so được
    for match in PROMISE.finditer(folded):
        clause = CLAUSE_END.split(folded[: match.start()])[-1]
        if NEGATION.search(clause) and not NOT_NEGATION.search(clause):
            continue
        return match.group(0)
    return None


# Lấy câu trả lời từ JSON Claude trả về; rỗng thì ném ValueError, có câu hứa đỗ thì ném PromiseError
def check_answer(raw: dict[str, Any]) -> str:
    text = raw.get("tra_loi")
    if not isinstance(text, str) or not text.strip():
        raise ValueError("câu trả lời rỗng")
    phrase = find_promise(text)
    if phrase:
        raise PromiseError(phrase)
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
        retry_note: dict[str, str] | None = None
        for attempt in range(1, MAX_ATTEMPTS + 1):
            try:
                raw = await self._llm.chat_json(
                    messages + ([retry_note] if retry_note else []), ANSWER_SCHEMA, temperature=0.0
                )
                return check_answer(raw)
            except (OllamaError, ValueError) as e:  # ClaudeError là OllamaError
                # Ghi lý do để biết vì sao học sinh nhận câu "chưa viết được"; không ghi hồ sơ hay câu trả lời
                logger.warning("Writer hỏng lần %d/%d: %s", attempt, MAX_ATTEMPTS, e or type(e).__name__)
                if isinstance(e, PromiseError):
                    note = RETRY_NOTE.format(answer=raw["tra_loi"], phrase=e.phrase)
                    retry_note = {"role": "user", "content": note}
        return None
