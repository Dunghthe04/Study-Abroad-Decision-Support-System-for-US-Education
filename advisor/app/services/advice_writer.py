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
# Lần viết lại: nói rõ lỗi để Claude sửa đúng chỗ (gửi y nguyên thì temperature 0 ra lại đúng câu cũ)
PROMISE_NOTE = (
    "Câu trả lời trước của bạn:\n{answer}\n\n"
    'Câu đó có cụm "{phrase}" bị coi là hứa kết quả tuyển sinh. Viết lại câu trả lời: giữ nguyên nội dung, '
    'chỉ đổi cách nói về khả năng đỗ thành "cơ hội", "khả năng", "phù hợp".'
)
RAW_JSON_NOTE = (
    "Câu trả lời trước của bạn bị viết dưới dạng JSON. Viết lại: ô tra_loi chỉ chứa chữ gửi thẳng cho học sinh, "
    "không viết JSON, không viết tên ô tra_loi, không bọc trong ``` hay dấu ngoặc kép."
)
# Câu trả lời bị bọc trong khung code ```json ... ```
FENCE = re.compile(r"```(?:json)?\s*(.*?)\s*```", re.DOTALL)
MAX_WRAPS = 2  # Claude bọc tối đa 2 lớp là gỡ; sâu hơn coi như hỏng


class PromiseError(ValueError):
    """Câu trả lời có cụm hứa đỗ. Lỗi chỉ nêu cụm khớp, không chép câu trả lời (có số liệu hồ sơ của học sinh)."""

    def __init__(self, phrase: str, answer: str) -> None:
        super().__init__(f'câu trả lời hứa đỗ: "{phrase}"')
        self.phrase = phrase
        self.answer = answer  # chỉ để gửi lại Claude khi viết lại, không nằm trong lời lỗi nên không vào log


class RawJsonError(ValueError):
    """Câu trả lời là JSON mà không gỡ được (hỏng, bọc dở, hoặc không có ô tra_loi)."""

    def __init__(self) -> None:
        super().__init__("câu trả lời là JSON thô")


# Claude đôi khi nhét cả {"tra_loi": "..."} vào trong ô tra_loi: gỡ ra lấy chữ bên trong.
# Không bắt đầu bằng { thì giữ nguyên; là JSON mà gỡ không được thì ném RawJsonError để viết lại
def unwrap_json(text: str) -> str:
    for _ in range(MAX_WRAPS + 1):
        body = text.strip()
        fence = FENCE.fullmatch(body)
        if fence:
            body = fence.group(1)
        if not body.startswith("{"):
            return text
        try:
            inner = json.loads(body)
        except json.JSONDecodeError:
            inner = None
        if not isinstance(inner, dict) or not isinstance(inner.get("tra_loi"), str):
            raise RawJsonError()
        # Đếm được bao lần Claude còn bọc JSON; không ghi nội dung câu trả lời
        logger.warning("Writer: đã gỡ câu trả lời bọc JSON")
        text = inner["tra_loi"]
    raise RawJsonError()  # bọc quá MAX_WRAPS lớp


# Tin nhắc kèm lần viết lại tùy lỗi; lỗi mạng/HTTP/rỗng thì không nhắc gì (gửi lại y nguyên)
def retry_note_for(error: Exception) -> dict[str, str] | None:
    if isinstance(error, PromiseError):
        content = PROMISE_NOTE.format(answer=error.answer, phrase=error.phrase)
    elif isinstance(error, RawJsonError):
        content = RAW_JSON_NOTE
    else:
        return None
    return {"role": "user", "content": content}


# Trả cụm hứa đỗ đầu tiên trong câu trả lời, None nếu không có. Cụm nằm sau phủ định cùng vế câu thì bỏ qua
def find_promise(text: str) -> str | None:
    folded = unicodedata.normalize("NFC", text).lower()  # NFC: chữ có dấu tổ hợp vẫn so được
    for match in PROMISE.finditer(folded):
        clause = CLAUSE_END.split(folded[: match.start()])[-1]
        if NEGATION.search(clause) and not NOT_NEGATION.search(clause):
            continue
        return match.group(0)
    return None


# Lấy câu trả lời từ JSON Claude trả về: gỡ JSON bọc thừa, rỗng thì ném ValueError,
# JSON không gỡ được thì RawJsonError, có câu hứa đỗ thì PromiseError
def check_answer(raw: dict[str, Any]) -> str:
    text = raw.get("tra_loi")
    if not isinstance(text, str):
        raise ValueError("câu trả lời rỗng")
    text = unwrap_json(text).strip()
    if not text:
        raise ValueError("câu trả lời rỗng")
    phrase = find_promise(text)
    if phrase:
        raise PromiseError(phrase, text)
    return text


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
                retry_note = retry_note_for(e)
        return None
