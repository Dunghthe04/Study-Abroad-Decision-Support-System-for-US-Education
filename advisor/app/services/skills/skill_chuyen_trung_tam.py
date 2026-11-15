"""skill_chuyen_trung_tam (B6): học sinh cần một dịch vụ hệ thống không làm thay được thì gợi ý trung tâm.

Luồng: SLM tách ý đã đọc nhu cầu (service, city trong entities của ý center_referral) -> code đổi mã dịch vụ sang mã DB,
chuẩn hóa thành phố -> backend chọn tối đa 3 trung tâm -> Claude (skill giải thích trung tâm) viết đoạn mở đầu và mỗi
trung tâm một câu "vì sao hợp" (AI không thấy tên trung tâm) -> code kiểm lời cấm, ghép tên và website.
AI không chọn trung tâm, không xin liên hệ, không nêu giá, không khen chê. Đặt lịch và so sánh trung tâm chưa có backend.
"""

import logging
import re
from collections.abc import Sequence
from dataclasses import dataclass
from typing import Any

from app.schemas.chat import ChatState, QuickReply, Widget
from app.services.centers import SERVICES, CenterFinder, CenterHit, CityCount, db_service, match_city
from app.services.chat_flow import SkillOutput
from app.services.skill_call import SKILLS_DIR, SkillCall
from app.services.small_talk import plain

logger = logging.getLogger("advisor.centers")

EXPLAIN_PATH = SKILLS_DIR / "skill_giai_thich_trung_tam.md"

ANSWER_SCHEMA = {
    "type": "object",
    "properties": {
        "opening": {"type": "string"},
        "reasons": {
            "type": "array",
            "items": {
                "type": "object",
                "properties": {"id": {"type": "integer"}, "reason": {"type": "string"}},
                "required": ["id", "reason"],
            },
        },
    },
    "required": ["opening", "reasons"],
}

ASK_SERVICE = "Bạn cần hỗ trợ phần nào? Bạn chọn giúp mình nhé."
INTRO = "Dưới đây là một số trung tâm đã xác minh có hỗ trợ {label}:"
NO_CENTER = (
    "Hiện mình chưa có trung tâm đã xác minh nào hỗ trợ {label}. "
    "Bạn có thể xem danh sách trung tâm đầy đủ ở trang Trung tâm."
)
UNAVAILABLE = "Phần gợi ý trung tâm đang tạm gián đoạn, bạn thử lại sau ít phút nhé."
# Học sinh nêu thành phố chưa có trung tâm nào hỗ trợ dịch vụ này: không đưa trung tâm ở nơi khác cho đủ, mà cho chọn nơi đang có
NO_CENTER_IN_CITY = (
    "Hiện chưa có trung tâm đã xác minh ở {city} hỗ trợ {label}. Bạn chọn thành phố khác đang có nhé:"
)
# Việc model tách ra (tasks) mà hệ thống chưa làm được: nói rõ rồi vẫn gợi ý trung tâm theo dịch vụ
TASK_NOTES = {
    "booking": "Mình chưa đặt lịch hẹn thay bạn được, nhưng mình gợi ý các trung tâm để bạn tự tìm hiểu nhé.",
    "compare": "Mình chưa so sánh các trung tâm với nhau được, nhưng mình gợi ý các trung tâm phù hợp để bạn tự tìm hiểu nhé.",
}
# Học sinh xin điều hệ thống không cung cấp (số điện thoại, giá, xếp hạng, đánh giá trung tâm)
NOT_PROVIDED = (
    "Mình không cung cấp số điện thoại, mức giá hay xếp hạng, đánh giá của trung tâm. "
    "Mình chỉ gợi ý các trung tâm đã xác minh, bạn xem thông tin trên website của họ và tự hỏi trực tiếp nhé."
)
_NOT_PROVIDED_RE = re.compile(
    r"\bso dien thoai\b|\bsdt\b|\bhotline\b|\bzalo\b|\bemail\b|\bgmail\b|\bbao gia\b|\bmuc gia\b"
    r"|\bgia\b.*\bbao nhieu\b|\bgia (?:ca|tien|re)\b|\bphi (?:dich vu|bao nhieu)\b|\bchi phi\b|\bhoc phi\b"
    r"|\bbao nhieu tien\b|\bre nhat\b"
    r"|\buy tin\b|\btot nhat\b|\bdang tin\b|\bxep hang\b|\bdanh gia\b|\breview\b"
)
DISCLAIMER = (
    "Danh sách chỉ gồm trung tâm đã được xác minh, xếp theo thành phố rồi theo tên, không phải xếp hạng chất lượng. "
    "Bạn nên tự tìm hiểu kỹ trước khi liên hệ."
)

# Bậc học (mã chung của hệ thống) -> chữ cho AI và cho câu giải thích dự phòng
LEVEL_LABELS = {
    "secondary": "THPT",
    "community_college": "cao đẳng cộng đồng",
    "undergraduate": "đại học",
    "master": "thạc sĩ",
    "phd": "tiến sĩ",
}
# Lời AI không được viết (TH-64): xin liên hệ, nêu giá, mời chào, khen chê trung tâm
BANNED = (
    "điện thoại",
    "email",
    "zalo",
    "facebook",
    "hotline",
    "inbox",
    "liên hệ ngay",
    "đăng ký ngay",
    "ưu đãi",
    "khuyến mãi",
    "giảm giá",
    "mức giá",
    "báo giá",
    "phí dịch vụ",
    "uy tín",
    "tốt nhất",
    "hàng đầu",
    "chất lượng",
    "cam kết",
    "đảm bảo",
)
MAX_REASON_WORDS = 40
# Trang đầu: số trung tâm hiện ngay (có AI viết lời giải thích). Còn lại hiện sau khi bấm Xem thêm, lời giải thích dựng bằng code từ dữ liệu
FIRST_PAGE = 3
MAX_OPENING_WORDS = 45
_DIGIT_RE = re.compile(r"\d")


# Nhu cầu đã đọc từ model: service là mã DB (None = chưa rõ dịch vụ), model_service là mã model trả (đưa lại cho model
# làm ngữ cảnh), city là tên thành phố trong DB (None = không nói hoặc không khớp thành phố nào),
# asked_city là thành phố học sinh nói đúng chữ họ viết (có cả khi không có trung tâm ở đó, để nói rõ với học sinh)
@dataclass(frozen=True)
class Need:
    service: str | None
    model_service: str | None
    city: str | None
    asked_city: str | None = None


# Phần AI viết: đoạn mở đầu và một câu cho mỗi trung tâm
@dataclass(frozen=True)
class Explained:
    opening: str
    reasons: dict[int, str]


# Lời của AI có chữ cấm hoặc con số thì ném ValueError để SkillCall gọi lại
def check_text(text: str, forbidden_names: Sequence[str] = ()) -> None:
    lowered = text.lower()
    for word in BANNED:
        if word in lowered:
            raise ValueError(f"có chữ cấm: {word}")
    for name in (
        forbidden_names
    ):  # nguyên tên (cả từ), để tên ngắn như "Trung tâm C" không dính vào "trung tâm cùng thành phố"
        if re.search(rf"(?<!\w){re.escape(name.lower())}(?!\w)", lowered):
            raise ValueError(f"tự nêu tên trung tâm: {name}")
    if _DIGIT_RE.search(text):
        raise ValueError("có con số (số liệu thuộc B5, phải có nguồn)")


# Đọc nhu cầu từ entities của ý center_referral (service, city, follow_up). Câu nối tiếp thiếu dịch vụ hay thành phố
# thì lấy từ ngữ cảnh lượt trước. Dịch vụ lạ coi như chưa nói; thành phố không khớp thành phố nào trong DB coi như không nói
def read_need(entities: dict[str, Any], context: Any, cities: list[str]) -> Need:
    follow_up = entities.get("follow_up") is True
    model_service = entities.get("service") or (context.service if follow_up else None)
    asked_city = entities.get("city") or (context.city if follow_up else None)
    city = match_city(asked_city, cities)
    service = db_service(model_service)
    return Need(service, model_service if service else None, city, asked_city)


def same_city(center_city: str | None, student_city: str | None) -> bool:
    return bool(student_city and center_city and center_city.lower() == student_city.lower())


# Câu giải thích chỉ được nói điều đúng với dữ liệu: dịch vụ, bậc học trung tâm thật sự có;
# "cùng thành phố" chỉ khi đúng là cùng thành phố với học sinh
def check_grounded(reason: str, center: CenterHit, student_city: str | None) -> None:
    lowered = reason.lower()
    if "cùng thành phố" in lowered and not same_city(center.city, student_city):
        raise ValueError("nói cùng thành phố nhưng không đúng")
    for code, label in SERVICES.items():
        if label.lower() in lowered and code not in center.services:
            raise ValueError(f"nêu dịch vụ trung tâm không có: {label}")
    for code, label in LEVEL_LABELS.items():
        if f"bậc {label.lower()}" in lowered and code not in center.study_levels:
            raise ValueError(f"nêu bậc học trung tâm không nhận: {label}")


# Kiểm tra lượt AI: đủ một câu cho mỗi trung tâm, câu ngắn, không chữ cấm, không tên trung tâm, đúng dữ liệu
def check_reasons(raw: dict[str, Any], centers: list[CenterHit], student_city: str | None) -> dict[int, str]:
    reasons = {int(item["id"]): str(item["reason"]).strip() for item in raw["reasons"]}
    if set(reasons) != set(range(1, len(centers) + 1)):
        raise ValueError("thiếu hoặc thừa câu giải thích")
    names = [c.name for c in centers]
    for i, center in enumerate(centers, start=1):
        reason = reasons[i]
        if not reason or len(reason.split()) > MAX_REASON_WORDS:
            raise ValueError("câu giải thích rỗng hoặc quá dài")
        check_text(reason, names)
        check_grounded(reason, center, student_city)
    return reasons


# Đoạn mở đầu của AI: ngắn, không chữ cấm, không tên trung tâm. Học sinh nêu thành phố mà danh sách không có trung tâm nào ở đó
# thì phải nói thẳng điều đó (chép đúng tên thành phố) và không được nói như thể trung tâm ở gần học sinh
def check_opening(
    opening: str, centers: list[CenterHit], asked_city: str | None, city_has_centers: bool | None
) -> str:
    opening = opening.strip()
    if not opening or len(opening.split()) > MAX_OPENING_WORDS:
        raise ValueError("đoạn mở đầu rỗng hoặc quá dài")
    # Tên thành phố của học sinh có thể có số ("Quận 7"): không tính vào luật cấm con số
    scanned = re.sub(re.escape(asked_city), "", opening, flags=re.IGNORECASE) if asked_city else opening
    check_text(scanned, [c.name for c in centers])
    if asked_city and city_has_centers is False:
        if asked_city.lower() not in opening.lower():
            raise ValueError("không nói thẳng chưa có trung tâm ở thành phố học sinh nêu")
        if "cùng thành phố" in opening.lower() or "gần bạn" in opening.lower():
            raise ValueError("nói trung tâm ở gần trong khi không có trung tâm nào ở thành phố đó")
    return opening


# Mở đầu dựng bằng code: dùng khi AI sai hai lần
def fallback_opening(label: str, asked_city: str | None, city_has_centers: bool | None) -> str:
    if asked_city and city_has_centers is False:
        return f"Hiện chưa có trung tâm đã xác minh ở {asked_city}, các trung tâm dưới đây ở nơi khác:"
    return INTRO.format(label=label)


# Câu giải thích dựng bằng code từ dữ liệu trung tâm: dùng khi AI sai hai lần
def template_reason(center: CenterHit, service: str, level: str | None, city: str | None) -> str:
    parts = [f"Có {SERVICES[service]}"]
    if level and level in center.study_levels:
        parts.append(f"nhận học sinh bậc {LEVEL_LABELS.get(level, level)}")
    if same_city(center.city, city):
        parts.append("cùng thành phố với bạn")
    return ", ".join(parts) + "."


# Thẻ trung tâm cho giao diện: mỗi trung tâm có mã (để mở trang chi tiết và form liên hệ), tên, thành phố, website, một câu "vì sao hợp".
# Giao diện hiện firstPage trung tâm đầu, nút Xem thêm mở phần còn lại, và thanh gõ thành phố (cities: thành phố đang có trung tâm cho
# dịch vụ này, kèm số trung tâm và câu gửi lại khi chọn). answer vẫn có danh sách chữ của trang đầu (nhật ký, kiểm tra mẫu)
def centers_widget(
    centers: list[CenterHit], reasons: dict[int, str], cities: list[CityCount], service: str
) -> Widget:
    items = [
        {"id": c.id, "name": c.name, "city": c.city, "website": c.website, "reason": reasons[i]}
        for i, c in enumerate(centers, start=1)
    ]
    label = SERVICES[service]
    options = [
        {"city": c.city, "count": c.count, "ask": f"Cho mình xem trung tâm {label} ở {c.city}"}
        for c in cities
    ]
    return Widget(type="centers", data={"centers": items, "firstPage": FIRST_PAGE, "cities": options})


def service_buttons() -> list[QuickReply]:
    return [
        QuickReply(label=label[:1].upper() + label[1:], value=f"Mình cần hỗ trợ {label}")
        for label in SERVICES.values()
    ]


class ChuyenTrungTamHandler:
    def __init__(self, llm: Any, model: str, finder: CenterFinder) -> None:
        self._finder = finder
        self._explain = SkillCall(llm, model, EXPLAIN_PATH)

    # entities: entities của ý center_referral do SLM tách (service, city, tasks, follow_up). Thiếu thì coi như chưa rõ dịch vụ.
    # Học sinh xin số điện thoại, giá, xếp hạng: nói rõ không cung cấp rồi gợi ý trung tâm như bình thường
    async def run(
        self, state: ChatState, message: str, entities: dict[str, Any] | None = None
    ) -> SkillOutput:
        output = await self._suggest(state, entities or {})
        if _NOT_PROVIDED_RE.search(plain(message)):
            output.answer = f"{NOT_PROVIDED}\n\n{output.answer}"
        return output

    async def _suggest(self, state: ChatState, entities: dict[str, Any]) -> SkillOutput:
        try:
            cities = await self._finder.cities()
        except Exception:  # backend tắt hoặc lỗi: nói thật là đang gián đoạn, không bịa danh sách
            logger.exception("Lấy danh sách thành phố thất bại")
            return SkillOutput(UNAVAILABLE)

        need = read_need(entities, state.context, cities)
        # Nhớ thành phố ngay khi học sinh nói, kể cả khi chưa rõ dịch vụ: bấm nút dịch vụ ở câu sau vẫn giữ thành phố
        if need.city:
            state.context.city = need.city
        notes = [TASK_NOTES[task] for task in entities.get("tasks") or [] if task in TASK_NOTES]
        # Chưa rõ dịch vụ: hỏi lại bằng nút, học sinh bấm là gửi lại một câu rõ dịch vụ
        if need.service is None:
            return SkillOutput("\n\n".join([*notes, ASK_SERVICE]), quick_replies=service_buttons())

        # Nhớ dịch vụ và thành phố để hiểu câu nối tiếp ("thế còn luyện phỏng vấn")
        state.context.service, state.context.city = need.model_service, need.city
        level = state.study_level
        try:
            # Học sinh nêu một thành phố không có trung tâm nào (không khớp thành phố trong DB): không tìm, vì không có gì ở đó
            centers = (
                []
                if need.asked_city and not need.city
                else await self._finder.find(need.service, level, need.city)
            )
        except Exception:
            logger.exception("Lấy trung tâm thất bại")
            return SkillOutput(UNAVAILABLE)
        counts = await self._city_counts(need.service, level)
        label = SERVICES[need.service]
        parts = list(notes)
        if (
            not centers
        ):  # TH-63: không có trung tâm đã xác minh cho dịch vụ này (hoặc ở thành phố học sinh nêu)
            if need.asked_city and counts:
                parts.append(NO_CENTER_IN_CITY.format(city=need.asked_city, label=label))
                return SkillOutput("\n\n".join(parts), widget=centers_widget([], {}, counts, need.service))
            parts.append(NO_CENTER.format(label=label))
            return SkillOutput("\n\n".join(parts))

        # AI viết lời giải thích cho trang đầu; các trung tâm còn lại dùng câu dựng bằng code từ cùng dữ liệu (nhanh, rẻ, vẫn đúng)
        first = centers[:FIRST_PAGE]
        explained = await self._explain_centers(first, need, level)
        reasons = dict(explained.reasons)
        for i in range(len(first) + 1, len(centers) + 1):
            reasons[i] = template_reason(centers[i - 1], need.service, level, need.city)
        lines = [explained.opening]
        for i, center in enumerate(first, start=1):
            where = f" ({center.city})" if center.city else ""
            site = f" Website: {center.website}" if center.website else ""
            lines.append(f"- {center.name}{where}: {reasons[i]}{site}")
        parts.append("\n".join(lines))
        if len(centers) > len(first):
            parts.append(f"Còn {len(centers) - len(first)} trung tâm khác, bạn bấm Xem thêm để xem hết.")
        # Thanh thành phố khác: khi đã có thành phố thì các nơi còn lại, khi chưa nêu thì mọi nơi (nếu có từ hai nơi trở lên)
        others = (
            [c for c in counts if not same_city(c.city, need.city)]
            if need.city
            else (counts if len(counts) > 1 else [])
        )
        return SkillOutput(
            "\n\n".join(parts),
            widget=centers_widget(centers, reasons, others, need.service),
            disclaimer=DISCLAIMER,
        )

    # Số trung tâm theo thành phố cho dịch vụ này; lỗi thì không có nút thành phố, danh sách chính vẫn hiện
    async def _city_counts(self, service: str, level: str | None) -> list[CityCount]:
        try:
            return await self._finder.city_counts(service, level)
        except Exception:
            logger.exception("Lấy số trung tâm theo thành phố thất bại")
            return []

    # Claude viết đoạn mở đầu và mỗi trung tâm một câu "vì sao hợp". AI không thấy tên trung tâm; code tự so thành phố, bậc học
    # rồi đưa kết quả đúng/sai cho AI (AI so chữ hay nhầm, ví dụ nói "cùng thành phố" khi không phải)
    async def _explain_centers(self, centers: list[CenterHit], need: Need, level: str | None) -> Explained:
        if not need.asked_city:
            city_has_centers = None
        elif need.city:
            city_has_centers = any(same_city(c.city, need.city) for c in centers)
        else:
            city_has_centers = False
        payload = {
            "need": SERVICES[need.service],
            "student_level": LEVEL_LABELS.get(level) if level else None,
            "student_city": need.asked_city,
            "city_has_centers": city_has_centers,
            "centers": [
                {
                    "id": i,
                    "same_city": same_city(c.city, need.city),
                    "accepts_level": bool(level) and level in c.study_levels,
                    "other_services": [SERVICES.get(s, s) for s in c.services if s != need.service],
                }
                for i, c in enumerate(centers, start=1)
            ],
        }

        def check(raw: dict[str, Any]) -> Explained:
            opening = check_opening(str(raw.get("opening") or ""), centers, need.asked_city, city_has_centers)
            return Explained(opening, check_reasons(raw, centers, need.city))

        explained = await self._explain.run(payload, ANSWER_SCHEMA, check)
        if explained is None:
            logger.warning("AI không viết được câu trả lời hợp lệ, dùng câu dựng sẵn")
            return Explained(
                fallback_opening(SERVICES[need.service], need.asked_city, city_has_centers),
                {
                    i: template_reason(c, need.service, level, need.city)
                    for i, c in enumerate(centers, start=1)
                },
            )
        return explained
