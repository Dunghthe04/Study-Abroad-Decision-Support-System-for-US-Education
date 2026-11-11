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
from app.services import usage_meter
from app.services.skill_call import MAX_ATTEMPTS, SKILLS_DIR, load_skill

logger = logging.getLogger(__name__)

PROMPT_PATH = SKILLS_DIR / "viet_tra_loi_ho_so.md"
PURPOSE = PROMPT_PATH.stem  # nhãn trong báo cáo token (ai_calls.purpose): viet_tra_loi_ho_so
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
# Tên tiếng Việt của các việc bộ phân loại tách ra (REVIEW_TASKS): writer phải làm đủ các việc này, kể cả khi skill
# SLM có dòng bảo bỏ một việc (skill đôi khi ghi 2 việc ở mục 1 rồi lại ghi "chỉ đánh giá, không đưa lộ trình")
TASK_LABEL = {
    "assessment": "đánh giá hồ sơ",
    "roadmap": "lộ trình chuẩn bị",
    "school_list": "chọn trường",
    "compare": "so sánh trường",
    "explain_school": "giải thích về trường",
}
# Việc kiểm được bằng chữ trong câu trả lời: học sinh có hỏi lộ trình mà câu trả lời không nhắc "lộ trình" là
# thiếu phần đó (skill có lúc ghi "chỉ đánh giá, không đưa lộ trình" ở cuối và Claude làm theo)
TASK_WORD = {"roadmap": "lộ trình"}
MISSING_TASK_NOTE = (
    "Câu trả lời trước của bạn thiếu phần {label} mà học sinh có hỏi. Viết lại đầy đủ mọi việc trong nhiem_vu, "
    'có một phần tiêu đề "{title}" dựa theo mục Lộ trình của skill, bỏ qua dòng nào trong skill bảo không đưa phần này.'
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


class MissingTaskError(ValueError):
    """Câu trả lời thiếu một việc học sinh hỏi. Giữ câu trả lời để dùng nếu lần viết lại vẫn thiếu."""

    def __init__(self, task: str, answer: str) -> None:
        super().__init__(f"câu trả lời thiếu phần {TASK_LABEL[task]}")
        self.task = task
        self.answer = answer  # không nằm trong lời lỗi nên không vào log


# Việc đầu tiên trong tasks mà câu trả lời không nhắc tới (chỉ xét việc có trong TASK_WORD), None nếu đủ
def missing_task(text: str, tasks: list[str] | None) -> str | None:
    folded = unicodedata.normalize("NFC", text).lower()
    for task in tasks or []:
        word = TASK_WORD.get(task)
        if word and word not in folded:
            return task
    return None


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
    elif isinstance(error, MissingTaskError):
        label = TASK_LABEL[error.task]
        content = MISSING_TASK_NOTE.format(label=label, title=label.capitalize())
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


# Mã ngắn của lỗi một lần viết, để script đo đếm theo loại (không chứa câu trả lời hay hồ sơ)
def error_code(error: Exception) -> str:
    if isinstance(error, PromiseError):
        return "hua_do"
    if isinstance(error, RawJsonError):
        return "json_tho"
    if isinstance(error, MissingTaskError):
        return "thieu_viec"
    if isinstance(error, OllamaError):
        return "goi_model"
    return "rong"


class AdviceWriter:
    def __init__(self, llm: Any) -> None:
        self._llm = llm
        self._rules, self.version = load_skill(PROMPT_PATH)  # đọc 1 lần lúc khởi động
        # Lần write() gần nhất: số lần gọi Claude và mã lỗi từng lần hỏng (để scripts đo, không dùng khi chạy thật)
        self.last_attempts = 0
        self.last_errors: list[str] = []

    async def write(
        self, skill: str, req: SkillRequest, schools: list[ResultSchool], tasks: list[str] | None = None
    ) -> str | None:
        """Trả câu trả lời đã kiểm, hoặc None nếu Claude sai cả MAX_ATTEMPTS lần.

        tasks: mã việc bộ phân loại tách từ câu hỏi (tasks). Rỗng/None thì không gửi, để skill quyết định.
        """
        user: dict[str, Any] = {
            "cau_hoi": req.cau_hoi,
            "ho_so": req.ho_so,
            "tinh_san": req.tinh_san,
            "schools": [{"name": s.name, "group": GROUP_LABEL.get(s.group or "", s.group)} for s in schools],
        }
        labels = [TASK_LABEL[t] for t in tasks or [] if t in TASK_LABEL]
        if labels:
            user["nhiem_vu"] = labels
        messages = [
            {"role": "system", "content": self._rules},
            {"role": "system", "content": skill},
            {"role": "user", "content": json.dumps(user, ensure_ascii=False, separators=(",", ":"))},
        ]
        self.last_attempts, self.last_errors = 0, []
        retry_note: dict[str, str] | None = None
        partial: str | None = None  # câu trả lời đúng mực nhưng thiếu một việc, dùng nếu viết lại vẫn hỏng
        for attempt in range(1, MAX_ATTEMPTS + 1):
            self.last_attempts = attempt
            try:
                # Gắn nhãn để báo cáo token biết đây là lời tư vấn hồ sơ (trước đây dòng này để trống nhãn)
                with usage_meter.purpose(PURPOSE, self.version):
                    raw = await self._llm.chat_json(
                        messages + ([retry_note] if retry_note else []), ANSWER_SCHEMA, temperature=0.0
                    )
                text = check_answer(raw)
                task = missing_task(text, tasks)
                if task:
                    raise MissingTaskError(task, text)
                return text
            except (OllamaError, ValueError) as e:  # ClaudeError là OllamaError
                # Ghi lý do để biết vì sao học sinh nhận câu "chưa viết được"; không ghi hồ sơ hay câu trả lời
                logger.warning("Writer hỏng lần %d/%d: %s", attempt, MAX_ATTEMPTS, e or type(e).__name__)
                self.last_errors.append(error_code(e))
                if isinstance(e, MissingTaskError):
                    partial = e.answer
                retry_note = retry_note_for(e)
        # Thiếu một phần vẫn hơn báo "chưa viết được": câu đó đã qua kiểm hứa đỗ và JSON
        return partial
