"""skill_tra_loi (B5): trả lời câu hỏi kiến thức từ kho kiến thức, có trích nguồn.

Luồng: embedding câu hỏi -> lấy dòng gần nghĩa nhất trong kho -> dưới ngưỡng thì từ chối (không gọi Qwen)
-> Qwen viết lại câu trả lời chuẩn của dòng đó cho tự nhiên, giữ đủ mọi ý -> code kiểm tra không bịa số, không bỏ ý.
Chỉ dùng MỘT dòng tốt nhất: đưa nhiều dòng thì Qwen trộn cả chuyện khác vào câu trả lời.
Qwen sai hai lần thì trả thẳng câu trả lời chuẩn của dòng đó, không để học sinh nhận câu trả lời chưa kiểm tra.
"""

import logging
import re
from dataclasses import dataclass
from typing import Any

from app.schemas.chat import ChatState, Citation, QuickReply
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
    },
    "required": ["enough", "answer"],
}

NO_BASIS = (
    "Hiện hệ thống chưa có thông tin chắc chắn về vấn đề này. "
    "Bạn có thể hỏi lại bằng cách khác, hoặc mình gợi ý các trung tâm tư vấn đã xác minh nếu bạn cần người hỗ trợ trực tiếp."
)
# Nút dẫn sang B6 (skill_chuyen_trung_tam): bấm là gửi câu xin gặp tư vấn viên, hệ thống hỏi cần dịch vụ nào
CENTER_BUTTON = QuickReply(label="Gợi ý trung tâm tư vấn", value="Em muốn gặp tư vấn viên")
UNAVAILABLE = "Phần trả lời kiến thức đang tạm gián đoạn, bạn thử lại sau ít phút nhé."
DISCLAIMER = (
    "Thông tin chỉ mang tính tham khảo. Quy định visa, học phí và học bổng có thể thay đổi; "
    "hãy kiểm tra lại với nguồn chính thức hoặc trung tâm tư vấn."
)

# Giới hạn trích nội dung
SNIPPET_CHARS = 200
# Câu viết lại phải dài ít nhất chừng này so với câu chuẩn; ngắn hơn nhiều là đã bỏ ý
MIN_KEEP_RATIO = 0.6
# Câu trả lời không được hứa kết quả
FORBIDDEN = ("chắc chắn", "đảm bảo", "100%", "tuyệt đối")

# Dấu [1], [2] Qwen có thể vẫn viết thói quen: bỏ đi, nguồn đã hiện riêng bên dưới câu trả lời
_MARKER_RE = re.compile(r"\s*\[\d+\]")
_NUMBER_RE = re.compile(r"\d+(?:[.,]\d+)*")
# Số thứ tự liệt kê "(1)", "(2)": viết lại thành "thứ nhất" cũng được, không tính là bỏ ý
_ENUM_RE = re.compile(r"\(\d+\)")


# Đóng gói kq sau khi kiểm tra trả lời qwen
@dataclass(frozen=True)
class Composed:
    enough: bool
    answer: str


# "4.000" và "4000" là cùng một số; "6,5" và "6.5" cũng vậy
def _normalize(number: str) -> str:
    n = number.replace(",", ".")
    if re.fullmatch(r"\d{1,3}(\.\d{3})+", n):
        n = n.replace(".", "")
    return n


# Các con số trong một đoạn chữ
def numbers_in(text: str) -> set[str]:
    return {_normalize(n) for n in _NUMBER_RE.findall(text)}


# Kiểm tra câu trả lời của Qwen; sai thì ném ValueError để SkillCall gọi lại
def check_answer(raw: dict[str, Any], question: str, hit: KnowledgeHit) -> Composed:
    if not bool(raw["enough"]):
        return Composed(False, "")
    answer = _MARKER_RE.sub("", str(raw["answer"])).strip()
    if not answer:
        raise ValueError("enough = true nhưng không có câu trả lời")
    lowered = answer.lower()
    for word in FORBIDDEN:
        if word in lowered:
            raise ValueError(f"dùng từ hứa kết quả: {word}")
    extra = numbers_in(answer) - numbers_in(question) - numbers_in(hit.content)
    if extra:
        raise ValueError(f"số không có trong nội dung tham khảo: {sorted(extra)}")
    # Giữ đủ ý: mọi con số của câu chuẩn phải còn, và câu viết lại không được ngắn đi quá nhiều
    dropped = numbers_in(_ENUM_RE.sub("", hit.content)) - numbers_in(answer)
    if dropped:
        raise ValueError(f"bỏ mất số của câu chuẩn: {sorted(dropped)}")
    if len(answer) < MIN_KEEP_RATIO * len(hit.content):
        raise ValueError("câu viết lại ngắn hơn nhiều so với câu chuẩn, có thể đã bỏ ý")
    return Composed(True, answer)


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
            # Lấy ra các dòng kiến thức gần nhất (đã xếp điểm cao trước)
            hits = await self._retriever.search(vector, state.study_level)
        except Exception:  # Ollama tắt hoặc DB lỗi: nói thật là đang gián đoạn, không bịa
            logger.exception("Tìm kiến thức thất bại")
            return SkillOutput(UNAVAILABLE)
        # Chỉ lấy dòng tốt nhất, và phải đạt ngưỡng
        if not hits or hits[0].score < self._min_score:
            return SkillOutput(NO_BASIS, quick_replies=[CENTER_BUTTON])
        best = hits[0]

        # Gọi ollama chạy skill
        composed = await self._call.run(
            {"question": message, "passage": best.content},
            SCHEMA,
            lambda raw: check_answer(raw, message, best),
        )
        if composed is not None and not composed.enough:
            return SkillOutput(NO_BASIS, quick_replies=[CENTER_BUTTON])
        if composed is None:  # Qwen sai hai lần: dùng câu trả lời chuẩn của dòng đó
            logger.warning("Qwen không trả được câu hợp lệ sau 2 lần, dùng câu chuẩn của %s", best.code)
            answer = best.content
        else:
            answer = composed.answer
        return SkillOutput(answer, citations=[citation_of(best)], disclaimer=DISCLAIMER)
