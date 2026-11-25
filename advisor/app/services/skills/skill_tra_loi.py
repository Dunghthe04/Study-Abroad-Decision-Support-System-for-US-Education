"""skill_tra_loi (B5): trả lời câu hỏi kiến thức từ kho kiến thức, có trích nguồn.

Nguyên tắc: nội dung kiến thức trả cho học sinh LUÔN là nguyên văn câu trả lời đã được người viết và kiểm chứng trong kho; AI không viết
hay sửa chữ nào. (Các nhánh khác chỉ có số liệu thô nên Claude phải viết thành câu và code đối chiếu số; ở đây câu trả lời đã có sẵn.)

Luồng: embedding câu hỏi -> lấy vài dòng gần nghĩa nhất trong kho -> dòng đầu dưới ngưỡng thì từ chối (không gọi Claude)
-> chỉ dòng đạt ngưỡng (và đúng mã form nếu câu hỏi nêu mã) là ứng viên -> Claude CHỌN một trong tối đa MAX_CANDIDATES ứng viên
đúng ý hỏi, hoặc nói không ứng viên nào đúng -> trả nguyên văn dòng được chọn kèm nguồn, ảnh và nút gợi ý.
Embedding chỉ tìm ứng viên (gần nghĩa chưa chắc đúng ý: hỏi "trượt visa F-1" có thể ra dòng visa B-2); Claude chọn dòng đúng ý.
Claude lỗi hay chọn sai khuôn hai lần thì chỉ dùng dòng đầu khi điểm cao (FALLBACK_MIN_SCORE), thấp hơn thì từ chối.
"""

import logging
import re
from dataclasses import dataclass
from typing import Any

from app.schemas.chat import ChatState, Citation, Media, QuickReply
from app.services.chat_flow import SkillOutput
from app.services.knowledge import KnowledgeHit, KnowledgeRetriever, TopicPart
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

# Cấu trúc mong muốn trả về: Claude chỉ CHỌN, không viết chữ nào cho học sinh
# chosen: số thứ tự ứng viên (1, 2, 3) trả lời đúng ý hỏi; 0 = không ứng viên nào đúng
SCHEMA = {
    "type": "object",
    "properties": {"chosen": {"type": "integer", "minimum": 0}},
    "required": ["chosen"],
}

# Số ứng viên tối đa đưa cho Claude chọn
MAX_CANDIDATES = 3
# Claude lỗi hay chọn sai khuôn hai lần thì chỉ dùng dòng đầu khi điểm của nó từ mức này trở lên (đo 61 câu: dòng đầu 0.76 là dòng sai)
FALLBACK_MIN_SCORE = 0.85

# Mã form/thủ tục hay bị hỏi nhầm sang nhau. Câu hỏi nêu mã nào thì chỉ nhận dòng của đúng mã đó (dòng không có mã, như hỏi đáp chung,
# vẫn được xét). Mã của các dòng tìm được cũng tính, nên thủ tục mới thêm vào kho tự được bảo vệ khi dòng đã tìm ra
FORM_CODES = ("I-20", "I-901", "DS-160", "I-94", "I-797", "I-134", "I-983", "I-765", "I-539", "I-129")


# "I-20" khớp "i20", "I 20", "i-20"; không khớp "I-200" hay "AI-20"
def code_pattern(code: str) -> re.Pattern[str]:
    tokens = re.findall(r"[A-Za-z]+|\d+", code)
    return re.compile(
        r"(?<![A-Za-z0-9])" + r"[-\s]?".join(re.escape(t) for t in tokens) + r"(?![A-Za-z0-9])", re.IGNORECASE
    )


def mentioned_codes(message: str, hits: list[KnowledgeHit]) -> set[str]:
    codes = set(FORM_CODES) | {h.topic_code for h in hits if h.topic_code}
    return {c for c in codes if code_pattern(c).search(message)}


NO_BASIS = KNOWLEDGE_NO_BASIS
UNAVAILABLE = KNOWLEDGE_UNAVAILABLE
DISCLAIMER = KNOWLEDGE_DISCLAIMER

# Giới hạn trích nội dung
SNIPPET_CHARS = 200


# Các ứng viên đưa cho Claude: tối đa MAX_CANDIDATES dòng, dòng nào cũng phải đạt ngưỡng (ngưỡng áp dụng cho mọi dòng, không để
# Claude trả lời từ dòng dưới ngưỡng) và, nếu câu hỏi nêu mã form (I-20, DS-160...), phải thuộc đúng mã đó hoặc không thuộc mã nào
def pick_candidates(hits: list[KnowledgeHit], min_score: float, message: str = "") -> list[KnowledgeHit]:
    named = mentioned_codes(message, hits)
    ok = [
        h
        for h in hits
        if h.score >= min_score and (not named or h.topic_code is None or h.topic_code in named)
    ]
    return ok[:MAX_CANDIDATES]


# Lựa chọn đã kiểm của Claude. hit None = Claude nói không ứng viên nào đúng ý (khác với việc Claude lỗi, khi SkillCall trả None)
@dataclass(frozen=True)
class Choice:
    hit: KnowledgeHit | None


# Kiểm tra lựa chọn của Claude; sai khuôn thì ném ValueError để SkillCall gọi lại
def check_choice(raw: dict[str, Any], candidates: list[KnowledgeHit]) -> Choice:
    chosen = raw.get("chosen")
    # bool là con của int trong Python: True không được coi là "ứng viên số 1"
    if isinstance(chosen, bool) or not isinstance(chosen, int) or not 0 <= chosen <= len(candidates):
        raise ValueError(f"chosen phải là số nguyên từ 0 đến {len(candidates)}, nhận được {chosen!r}")
    return Choice(None if chosen == 0 else candidates[chosen - 1])


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


# Tên nguồn khi mục tri thức không ghi tên nguồn (UC-37)
DEFAULT_SOURCE_NAME = "Kho kiến thức tư vấn du học"


# lấy thông tin của một đoạn kiến thức và chuyển nó thành một đối tượng trích dẫn (Citation) để đính kèm vào câu trả lời cho người dùng.
# Mục không có tên nguồn thì ghi DEFAULT_SOURCE_NAME (không lấy đường link làm tên); link vẫn đi kèm ở trường url, có thể nhiều link cách nhau bằng ";"
def citation_of(hit: KnowledgeHit) -> Citation:
    title = (hit.source or "").strip() or DEFAULT_SOURCE_NAME
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


# Nút "các phần khác của thủ tục": thứ tự hỏi tự nhiên (là gì, chuẩn bị, các bước, danh sách kiểm tra, lỗi hay gặp) và chữ trên nút
TOPIC_PART_ORDER = ("proc_overview", "proc_prepare", "proc_steps", "proc_checklist", "proc_mistakes")
TOPIC_PART_LABEL = {
    "proc_overview": "là gì",
    "proc_prepare": "chuẩn bị",
    "proc_steps": "các bước",
    "proc_checklist": "danh sách kiểm tra",
    "proc_mistakes": "lỗi hay gặp",
}
MAX_TOPIC_BUTTONS = 3


# Nút sang các phần khác của cùng thủ tục. Nút gửi đúng câu hỏi mẫu của dòng đó nên lần sau kho tìm ra chính nó
def topic_buttons(best: KnowledgeHit, parts: list[TopicPart]) -> list[QuickReply]:
    if not best.topic_code:
        return []
    by_type = {p.doc_type: p for p in parts if p.entry_no != best.entry_no and p.doc_type in TOPIC_PART_LABEL}
    chosen = [by_type[t] for t in TOPIC_PART_ORDER if t in by_type][:MAX_TOPIC_BUTTONS]
    return [
        QuickReply(label=f"{best.topic_code}: {TOPIC_PART_LABEL[p.doc_type]}", value=p.question)
        for p in chosen
    ]


# Nút sau câu trả lời kiến thức: các phần khác của thủ tục nếu dòng thuộc một thủ tục, không thì tối đa 2 câu hỏi liên quan trong kho;
# rồi gợi ý trường. Đã trả lời được thì không đưa nút trung tâm tư vấn (nút đó dành cho câu kho không trả lời được, xem các nhánh NO_BASIS)
def after_answer(
    hits: list[KnowledgeHit],
    best: KnowledgeHit,
    message: str,
    min_score: float,
    parts: list[TopicPart] | None = None,
) -> list[QuickReply]:
    topic = topic_buttons(best, parts or [])
    return [*(topic or related_buttons(hits, best, message, min_score)), MENU["schools"].model_copy()]


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

    # Các phần khác của thủ tục của dòng được dùng (để làm nút). Lỗi gì thì không có nút đó, câu trả lời vẫn đi
    async def _topic_parts(self, best: KnowledgeHit) -> list[TopicPart]:
        if not best.topic_code:
            return []
        try:
            return await self._retriever.by_topic(best.topic_code)
        except Exception:
            logger.exception("Không lấy được các phần cùng thủ tục %s", best.topic_code)
            return []

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
        candidates = pick_candidates(hits, self._min_score, message)
        if not candidates:  # dòng tìm ra đều thuộc mã form khác với mã học sinh hỏi (hỏi DS-160 mà ra I-20)
            logger.info(
                "knowledge_form_mismatch top=%s codes=%s question=%r",
                hits[0].entry_no,
                sorted(mentioned_codes(message, hits)),
                mask_personal_info(message),
            )
            return SkillOutput(NO_BASIS, quick_replies=menu_buttons("knowledge"))
        user = {
            "question": message,
            "candidates": [
                {"id": i, "question": h.question, "passage": h.answer}
                for i, h in enumerate(candidates, start=1)
            ],
        }

        # Claude chỉ chọn ứng viên đúng ý; câu trả lời là nguyên văn dòng được chọn
        choice = await self._call.run(user, SCHEMA, lambda raw: check_choice(raw, candidates))
        if choice is not None and choice.hit is None:  # không ứng viên nào đúng ý
            return SkillOutput(NO_BASIS, quick_replies=menu_buttons("knowledge"))
        if choice is None:  # Claude lỗi hay chọn sai khuôn hai lần
            best = candidates[0]
            if best.score < FALLBACK_MIN_SCORE:  # dòng đầu chưa chắc đúng ý: không đưa cho học sinh
                logger.warning(
                    "Claude không chọn được sau 2 lần, dòng đầu #%s chỉ %.2f nên từ chối",
                    best.entry_no,
                    best.score,
                )
                return SkillOutput(NO_BASIS, quick_replies=menu_buttons("knowledge"))
            logger.warning("Claude không chọn được sau 2 lần, dùng dòng đầu #%s", best.entry_no)
        else:
            best = choice.hit
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
            best.answer,
            quick_replies=after_answer(hits, best, message, self._min_score, await self._topic_parts(best)),
            citations=[citation_of(best)],
            media=media_of(best),
            disclaimer=DISCLAIMER,
        )
