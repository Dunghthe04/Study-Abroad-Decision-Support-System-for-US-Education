"""skill_tra_loi (B5): trả lời câu hỏi kiến thức từ kho kiến thức, có trích nguồn.

Luồng: embedding câu hỏi -> lấy vài dòng gần nghĩa nhất trong kho -> dòng đầu dưới ngưỡng thì từ chối (không gọi Claude); chỉ dòng đạt ngưỡng là ứng viên
-> Claude CHỌN một trong tối đa MAX_CANDIDATES ứng viên đúng ý hỏi (hoặc nói không đoạn nào đúng) và viết lại câu trả lời chuẩn
của đúng dòng đó cho tự nhiên, giữ đủ mọi ý -> code kiểm tra theo dòng được chọn: không bịa số, không bỏ ý.
Embedding chỉ tìm ứng viên (gần nghĩa chưa chắc đúng ý: hỏi "trượt visa F-1" có thể ra dòng visa B-2); Claude chọn dòng đúng
ý. Câu trả lời vẫn chỉ từ MỘT dòng, một nguồn: Claude không được trộn các đoạn.
Claude sai hai lần thì chỉ trả thẳng câu chuẩn của dòng đầu khi dòng đó điểm cao (FALLBACK_MIN_SCORE); thấp hơn thì từ chối,
vì dòng đầu chưa chắc đúng ý và câu chuẩn chưa qua kiểm tra không được đưa thẳng cho học sinh.
"""

import logging
import re
from dataclasses import dataclass
from typing import Any

from app.schemas.chat import ChatState, Citation, Media, QuickReply
from app.services.chat_flow import SkillOutput
from app.services.knowledge import KnowledgeHit, KnowledgeRetriever
from app.services.pii_mask import mask_personal_info
from app.services.replies import (
    KNOWLEDGE_DISCLAIMER,
    KNOWLEDGE_NO_BASIS,
    KNOWLEDGE_UNAVAILABLE,
    MENU,
    menu_buttons,
)
from app.services.skill_call import SKILLS_DIR, SkillCall

logger = logging.getLogger("advisor.knowledge")
SKILL_PATH = SKILLS_DIR / "skill_tra_loi.md"

# Cấu trúc mong muốn trả về
# chosen: số thứ tự ứng viên (1, 2, 3) trả lời đúng ý hỏi; 0 = không ứng viên nào đúng
SCHEMA = {
    "type": "object",
    "properties": {
        "chosen": {"type": "integer", "minimum": 0},
        "answer": {"type": "string"},
    },
    "required": ["chosen", "answer"],
}

# Số ứng viên tối đa đưa cho Claude chọn
MAX_CANDIDATES = 3
# Claude sai hai lần thì chỉ trả nguyên văn dòng đầu khi điểm của nó từ mức này trở lên (đo 61 câu: dòng đầu 0.76 là dòng sai)
FALLBACK_MIN_SCORE = 0.85

NO_BASIS = KNOWLEDGE_NO_BASIS
UNAVAILABLE = KNOWLEDGE_UNAVAILABLE
DISCLAIMER = KNOWLEDGE_DISCLAIMER

# Giới hạn trích nội dung
SNIPPET_CHARS = 200
# Câu viết lại phải dài ít nhất chừng này so với câu chuẩn; ngắn hơn nhiều là đã bỏ ý
MIN_KEEP_RATIO = 0.6
# Câu trả lời không được hứa kết quả
FORBIDDEN = ("chắc chắn", "100%", "tuyệt đối")
# "đảm bảo" là động từ rất thường ("đảm bảo thông tin khớp nhau"): chỉ chặn khi nó hứa một kết quả ("đảm bảo đậu visa")
_PROMISE_RE = re.compile(
    r"đảm bảo\s+(?:sẽ\s+)?(?:đậu|đỗ|trúng|thành công|kết quả|được nhận|được cấp|có visa|nhận được|chắc)"
)

# Dấu [1], [2] Qwen có thể vẫn viết thói quen: bỏ đi, nguồn đã hiện riêng bên dưới câu trả lời
_MARKER_RE = re.compile(r"\s*\[\d+\]")
_NUMBER_RE = re.compile(r"\d+(?:[.,]\d+)*")
# Số thứ tự liệt kê "(1)", "(2)": viết lại thành "thứ nhất" cũng được, không tính là bỏ ý
_ENUM_RE = re.compile(r"\(\d+\)")


# Đóng gói kq sau khi kiểm tra trả lời qwen
@dataclass(frozen=True)
class Composed:
    hit: KnowledgeHit | None  # dòng Claude chọn; None = không ứng viên nào đúng ý
    answer: str

    @property
    def enough(self) -> bool:
        return self.hit is not None


# "4.000" và "4000" là cùng một số; "6,5" và "6.5" cũng vậy
def _normalize(number: str) -> str:
    n = number.replace(",", ".")
    if re.fullmatch(r"\d{1,3}(\.\d{3})+", n):
        n = n.replace(".", "")
    return n


# Các con số trong một đoạn chữ
def numbers_in(text: str) -> set[str]:
    return {_normalize(n) for n in _NUMBER_RE.findall(text)}


# Các ứng viên đưa cho Claude: tối đa MAX_CANDIDATES dòng đầu, và dòng nào cũng phải đạt ngưỡng (ngưỡng áp dụng cho mọi dòng,
# không để Claude trả lời từ dòng dưới ngưỡng)
def pick_candidates(hits: list[KnowledgeHit], min_score: float) -> list[KnowledgeHit]:
    return [h for h in hits[:MAX_CANDIDATES] if h.score >= min_score]


# Kiểm tra câu trả lời của Claude theo dòng nó chọn; sai thì ném ValueError để SkillCall gọi lại
def check_answer(raw: dict[str, Any], question: str, candidates: list[KnowledgeHit]) -> Composed:
    chosen = raw.get("chosen")
    # bool là con của int trong Python: True không được coi là "ứng viên số 1"
    if isinstance(chosen, bool) or not isinstance(chosen, int) or not 0 <= chosen <= len(candidates):
        raise ValueError(f"chosen phải là số nguyên từ 0 đến {len(candidates)}, nhận được {chosen!r}")
    if chosen == 0:
        return Composed(None, "")
    hit = candidates[chosen - 1]
    answer = _MARKER_RE.sub("", str(raw.get("answer", ""))).strip()
    if not answer:
        raise ValueError("đã chọn ứng viên nhưng không có câu trả lời")
    lowered = answer.lower()
    source = hit.answer.lower()
    if _PROMISE_RE.search(lowered) and not _PROMISE_RE.search(source):
        raise ValueError("hứa kết quả: đảm bảo")
    for word in FORBIDDEN:
        # Chữ nằm sẵn trong câu chuẩn đã kiểm chứng (ví dụ "đảm bảo thông tin nhất quán") thì chép lại không phải Claude hứa:
        # đo thật, Claude bị từ chối hai lần liền chỉ vì giữ đúng chữ này của câu chuẩn
        if word in lowered and word not in source:
            raise ValueError(f"dùng từ hứa kết quả: {word}")
    extra = numbers_in(answer) - numbers_in(question) - numbers_in(hit.answer)
    if extra:
        raise ValueError(f"số không có trong nội dung tham khảo: {sorted(extra)}")
    # Giữ đủ ý: mọi con số của câu chuẩn phải còn, và câu viết lại không được ngắn đi quá nhiều
    dropped = numbers_in(_ENUM_RE.sub("", hit.answer)) - numbers_in(answer)
    if dropped:
        raise ValueError(f"bỏ mất số của câu chuẩn: {sorted(dropped)}")
    if len(answer) < MIN_KEEP_RATIO * len(hit.answer):
        raise ValueError("câu viết lại ngắn hơn nhiều so với câu chuẩn, có thể đã bỏ ý")
    return Composed(hit, answer)


# Số ảnh tối đa kèm một câu trả lời
MAX_MEDIA = 3


# Ảnh minh họa của dòng kho được dùng. Link CHỈ lấy từ cột image_url (người thu thập dán, có thể nhiều link cách nhau bằng ";"),
# không bao giờ từ lời Claude viết; chỉ nhận https, bỏ link trùng và link hỏng
def media_of(hit: KnowledgeHit) -> list[Media]:
    media: list[Media] = []
    for link in (hit.image_url or "").split(";"):
        try:
            item = Media(kind="image", url=link.strip(), alt=hit.question)
        except ValueError:
            continue
        if item not in media:
            media.append(item)
    return media[:MAX_MEDIA]


# lấy thông tin của một đoạn kiến thức và chuyển nó thành một đối tượng trích dẫn (Citation) để đính kèm vào câu trả lời cho người dùng.
def citation_of(hit: KnowledgeHit) -> Citation:
    title = hit.source or hit.source_url or "Kho kiến thức tư vấn du học"
    return Citation(
        title=title, url=hit.source_url, snippet=hit.answer[:SNIPPET_CHARS], retrieved_at=hit.retrieved_at
    )


# Câu hỏi liên quan gợi ý sau câu trả lời: các dòng khác trong kho gần câu hỏi (thấp hơn ngưỡng trả lời một chút),
# khác dòng vừa dùng. Nút gửi đúng câu hỏi mẫu của dòng đó nên lần sau kho tìm ra chính nó
RELATED_MARGIN = 0.15
MAX_RELATED = 2
MAX_LABEL = 40


def related_buttons(
    hits: list[KnowledgeHit], best: KnowledgeHit, message: str, min_score: float
) -> list[QuickReply]:
    seen = {best.question.strip().lower(), message.strip().lower()}
    buttons: list[QuickReply] = []
    for other in hits:
        question = other.question.strip()
        if (
            other.entry_no == best.entry_no
            or other.score < min_score - RELATED_MARGIN
            or question.lower() in seen
        ):
            continue
        seen.add(question.lower())
        label = question if len(question) <= MAX_LABEL else question[: MAX_LABEL - 1].rstrip() + "…"
        buttons.append(QuickReply(label=label, value=question))
        if len(buttons) == MAX_RELATED:
            break
    return buttons


# Nút sau câu trả lời kiến thức: tối đa 2 câu hỏi liên quan trong kho, rồi gợi ý trường. Đã trả lời được thì không đưa nút
# trung tâm tư vấn (nút đó dành cho câu kho không trả lời được, xem các nhánh NO_BASIS)
def after_answer(
    hits: list[KnowledgeHit], best: KnowledgeHit, message: str, min_score: float
) -> list[QuickReply]:
    return [*related_buttons(hits, best, message, min_score), MENU["schools"].model_copy()]


class TraLoiHandler:
    def __init__(
        self,
        llm: Any,
        model: str,
        retriever: KnowledgeRetriever,
        min_score: float = 0.72,
        embedder: Any = None,
        restorer: Any = None,
    ) -> None:
        self._restorer = restorer  # khôi phục dấu cho câu gõ không dấu; None = không có bước này
        self._embedder = embedder or llm  # embedding chạy trên Ollama (bge-m3), khác với LLM viết câu trả lời
        self._retriever = retriever
        self._min_score = min_score
        self._call = SkillCall(llm, model, SKILL_PATH)

    # Câu gõ không dấu bị chấm điểm thấp dù kho có đáp án ("opt la gi" 0.58). Chỉ khi lần tìm đầu dưới ngưỡng mới nhờ Claude thêm dấu
    # rồi tìm lại; lần hai không tốt hơn hoặc lỗi gì thì giữ kết quả cũ. Câu học sinh gõ vẫn giữ nguyên cho bước chọn đáp án
    async def _with_accents_restored(self, message: str, hits: list[KnowledgeHit]) -> list[KnowledgeHit]:
        if self._restorer is None or (hits and hits[0].score >= self._min_score):
            return hits
        restored = await self._restorer.restore(message)
        if restored is None:
            return hits
        try:
            [vector] = await self._embedder.embed([restored])
            better = await self._retriever.search(vector)
        except Exception:  # lần hai lỗi không được làm hỏng câu trả lời: giữ kết quả lần đầu
            logger.exception("Tìm lại bằng câu đã thêm dấu thất bại")
            return hits
        before = hits[0].score if hits else 0.0
        after = better[0].score if better else 0.0
        logger.info(
            "knowledge_accent_restore before=%.3f after=%.3f question=%r restored=%r",
            before,
            after,
            mask_personal_info(message),
            mask_personal_info(restored),
        )
        return better if after > before else hits

    async def run(self, state: ChatState, message: str) -> SkillOutput:
        try:
            # Biến câu hỏi thành vector
            [vector] = await self._embedder.embed([message])
            # Lấy ra các dòng kiến thức gần nhất (đã xếp điểm cao trước)
            hits = await self._retriever.search(vector)
            hits = await self._with_accents_restored(message, hits)
        except Exception:  # Ollama tắt hoặc DB lỗi: nói thật là đang gián đoạn, không bịa
            logger.exception("Tìm kiến thức thất bại")
            return SkillOutput(UNAVAILABLE, quick_replies=menu_buttons("knowledge"))
        # Chỉ lấy dòng tốt nhất, và phải đạt ngưỡng
        if not hits or hits[0].score < self._min_score:
            # Ghi lại câu kho chưa trả lời được (đã che thông tin cá nhân) để biết cần bổ sung kiến thức gì
            logger.info(
                "knowledge_miss score=%s nearest=%s question=%r",
                f"{hits[0].score:.3f}" if hits else "none",
                hits[0].entry_no if hits else "none",
                mask_personal_info(message),
            )
            return SkillOutput(NO_BASIS, quick_replies=menu_buttons("knowledge"))
        candidates = pick_candidates(hits, self._min_score)
        user = {
            "question": message,
            "candidates": [
                {"id": i, "question": h.question, "passage": h.answer}
                for i, h in enumerate(candidates, start=1)
            ],
        }

        # Claude chọn ứng viên đúng ý rồi viết lại câu chuẩn của dòng đó
        composed = await self._call.run(user, SCHEMA, lambda raw: check_answer(raw, message, candidates))
        if composed is not None and composed.hit is None:  # không ứng viên nào đúng ý
            return SkillOutput(NO_BASIS, quick_replies=menu_buttons("knowledge"))
        if composed is None:  # Claude sai hai lần
            best = hits[0]
            if best.score < FALLBACK_MIN_SCORE:  # dòng đầu chưa chắc đúng ý: không đưa thẳng cho học sinh
                logger.warning(
                    "Claude không trả được câu hợp lệ sau 2 lần, dòng đầu #%s chỉ %.2f nên từ chối",
                    best.entry_no,
                    best.score,
                )
                return SkillOutput(NO_BASIS, quick_replies=menu_buttons("knowledge"))
            logger.warning(
                "Claude không trả được câu hợp lệ sau 2 lần, dùng câu chuẩn của dòng #%s", best.entry_no
            )
            answer = best.answer
        else:
            best = composed.hit
            answer = composed.answer
            if (
                best.entry_no != hits[0].entry_no
            ):  # đo tần suất bước chọn đổi dòng đầu, và biết kho cần bổ sung gì
                logger.info(
                    "knowledge_rerank top=%s chose=%s question=%r",
                    hits[0].entry_no,
                    best.entry_no,
                    mask_personal_info(message),
                )
        logger.info(
            "knowledge_answer entry=%s score=%.3f", best.entry_no, best.score
        )  # dòng thật sự được dùng, để đo
        return SkillOutput(
            answer,
            quick_replies=after_answer(hits, best, message, self._min_score),
            citations=[citation_of(best)],
            media=media_of(best),
            disclaimer=DISCLAIMER,
        )
