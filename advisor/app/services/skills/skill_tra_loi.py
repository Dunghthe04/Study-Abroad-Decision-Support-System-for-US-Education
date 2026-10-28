"""skill_tra_loi (B5): trả lời câu hỏi kiến thức từ kho kiến thức, có trích nguồn.

Luồng: embedding câu hỏi -> tìm các dòng gần nghĩa trong kho -> dưới ngưỡng thì từ chối (không gọi Qwen)
-> Qwen viết câu trả lời chỉ từ các đoạn tìm được -> code kiểm tra số và nguồn.
Qwen sai hai lần thì trả thẳng câu trả lời chuẩn của đoạn gần nhất, không để học sinh nhận câu trả lời chưa kiểm tra.
"""

import logging
import re
from dataclasses import dataclass
from typing import Any

from app.schemas.chat import ChatState, Citation
from app.services.chat_flow import SkillOutput
from app.services.knowledge import KnowledgeHit, KnowledgeRetriever
from app.services.skill_call import SKILLS_DIR, SkillCall

logger = logging.getLogger("advisor.knowledge")
SKILL_PATH = SKILLS_DIR / "skill_tra_loi.md"

# Cấu trúc mong muốn trả về
SCHEMA = {
    "type": "object",
    "properties": {
        "enough": {"type": "boolean"},
        "answer": {"type": "string"},
        "used": {"type": "array", "items": {"type": "integer"}},
    },
    "required": ["enough", "answer", "used"],
}

NO_BASIS = (
    "Hiện hệ thống chưa có thông tin chắc chắn về vấn đề này. "
    "Bạn có thể hỏi lại bằng cách khác, hoặc liên hệ một trung tâm tư vấn du học uy tín để được hỗ trợ trực tiếp."
)
UNAVAILABLE = "Phần trả lời kiến thức đang tạm gián đoạn, bạn thử lại sau ít phút nhé."
DISCLAIMER = (
    "Thông tin chỉ mang tính tham khảo. Quy định visa, học phí và học bổng có thể thay đổi; "
    "hãy kiểm tra lại với nguồn chính thức hoặc trung tâm tư vấn."
)

# Giới hạn trích nội dung
SNIPPET_CHARS = 200
# Câu trả lời không được hứa kết quả
FORBIDDEN = ("chắc chắn", "đảm bảo", "100%", "tuyệt đối")

# Tìm trong nội dung trong chuỗi
_MARKER_RE = re.compile(r"\[(\d+)\]")
_NUMBER_RE = re.compile(r"\d+(?:[.,]\d+)*")


# Đóng gói kq sau khi kiểm tra trả lời qwen
@dataclass(frozen=True)
class Composed:
    enough: bool
    answer: str
    used: list[int]


# "4.000" và "4000" là cùng một số; "6,5" và "6.5" cũng vậy
def _normalize(number: str) -> str:
    n = number.replace(",", ".")
    if re.fullmatch(r"\d{1,3}(\.\d{3})+", n):
        n = n.replace(".", "")
    return n


# Các con số trong một đoạn chữ (đã bỏ dấu [1], [2] chỉ số đoạn)
def numbers_in(text: str) -> set[str]:
    return {_normalize(n) for n in _NUMBER_RE.findall(_MARKER_RE.sub("", text))}


# Kiểm tra câu trả lời của Qwen; sai thì ném ValueError để SkillCall gọi lại
def check_answer(raw: dict[str, Any], question: str, hits: list[KnowledgeHit]) -> Composed:
    enough, answer, used = bool(raw["enough"]), str(raw["answer"]).strip(), list(raw["used"])
    if not enough:
        return Composed(False, "", [])
    if not answer:
        raise ValueError("enough = true nhưng không có câu trả lời")
    valid_ids = set(range(1, len(hits) + 1))
    markers = {int(m) for m in _MARKER_RE.findall(answer)}
    if not markers:
        raise ValueError("câu trả lời không ghi nguồn [n]")
    if not used or not set(used) <= valid_ids or not markers <= valid_ids:
        raise ValueError("số đoạn trích dẫn không hợp lệ")
    lowered = answer.lower()
    for word in FORBIDDEN:
        if word in lowered:
            raise ValueError(f"dùng từ hứa kết quả: {word}")
    allowed = numbers_in(question).union(*(numbers_in(h.content) for h in hits))
    extra = numbers_in(answer) - allowed
    if extra:
        raise ValueError(f"số không có trong đoạn tham khảo: {sorted(extra)}")
    return Composed(True, answer, sorted(set(used) | markers))


# lấy thông tin của một đoạn kiến thức và chuyển nó thành một đối tượng trích dẫn (Citation) để đính kèm vào câu trả lời cho người dùng.
def citation_of(hit: KnowledgeHit) -> Citation:
    title = hit.quote or hit.source_note or "Kho kiến thức tư vấn du học"
    return Citation(title=title, url=hit.source_url, snippet=hit.content[:SNIPPET_CHARS])


class TraLoiHandler:
    def __init__(self, llm: Any, model: str, retriever: KnowledgeRetriever, min_score: float = 0.72) -> None:
        self._llm = llm
        self._retriever = retriever
        self._min_score = min_score
        self._call = SkillCall(llm, model, SKILL_PATH)

    async def run(self, state: ChatState, message: str) -> SkillOutput:
        try:
            # Biến câu hỏi thành vector
            [vector] = await self._llm.embed([message])
            # Lấy ra danh sách các kiến thức trong kho
            hits = await self._retriever.search(vector, state.study_level)
        except Exception:  # Ollama tắt hoặc DB lỗi: nói thật là đang gián đoạn, không bịa
            logger.exception("Tìm kiến thức thất bại")
            return SkillOutput(UNAVAILABLE)
        # Lấy ra các kiesn thức có điểm >= 0.72
        grounded = [h for h in hits if h.score >= self._min_score]
        # Nếu k có
        if not grounded:
            return SkillOutput(NO_BASIS)

        # Nếu có tạo mảng các kiến thức
        passages = [{"id": i, "text": h.content} for i, h in enumerate(grounded, start=1)]
        # Gọi ollama chạy skill
        composed = await self._call.run(
            {"question": message, "passages": passages},
            SCHEMA,
            lambda raw: check_answer(raw, message, grounded),
        )
        if composed is not None and not composed.enough:
            return SkillOutput(NO_BASIS)
        if composed is None:  # Qwen sai hai lần: dùng câu trả lời chuẩn của đoạn gần nhất
            answer, used = f"{grounded[0].content} [1]", [1]
        else:
            # Lấy ra câu trả lời + nguồn
            answer, used = composed.answer, composed.used
        return SkillOutput(
            answer, citations=[citation_of(grounded[i - 1]) for i in used], disclaimer=DISCLAIMER
        )
