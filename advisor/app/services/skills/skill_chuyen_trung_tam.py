"""skill_chuyen_trung_tam (B6): học sinh cần một dịch vụ hệ thống không làm thay được thì gợi ý trung tâm.

Luồng: AI lượt 1 hiểu nhu cầu (dịch vụ, thành phố, lời khuyên chung) -> backend chọn tối đa 3 trung tâm
-> AI lượt 2 viết mỗi trung tâm một câu "vì sao hợp" (AI không thấy tên trung tâm) -> code ghép tên và kiểm tra lời cấm.
AI không chọn trung tâm, không xin liên hệ, không nêu giá, không khen chê. Gửi yêu cầu liên hệ làm sau.
"""

import logging
import re
from collections.abc import Sequence
from dataclasses import dataclass
from typing import Any

from app.schemas.chat import ChatState, QuickReply
from app.services.centers import SERVICES, CenterFinder, CenterHit
from app.services.chat_flow import SkillOutput
from app.services.skill_call import SKILLS_DIR, SkillCall

logger = logging.getLogger("advisor.centers")

NEED_PATH = SKILLS_DIR / "skill_chuyen_trung_tam.md"
EXPLAIN_PATH = SKILLS_DIR / "skill_giai_thich_trung_tam.md"

NEED_SCHEMA = {
    "type": "object",
    "properties": {
        "service": {"type": ["string", "null"]},
        "city": {"type": ["string", "null"]},
        "general_help": {"type": "string"},
    },
    "required": ["service", "city", "general_help"],
}
REASON_SCHEMA = {
    "type": "object",
    "properties": {
        "reasons": {
            "type": "array",
            "items": {
                "type": "object",
                "properties": {"id": {"type": "integer"}, "reason": {"type": "string"}},
                "required": ["id", "reason"],
            },
        }
    },
    "required": ["reasons"],
}

ASK_SERVICE = "Bạn cần hỗ trợ phần nào? Bạn chọn giúp mình nhé."
INTRO = "Dưới đây là một số trung tâm đã xác minh có hỗ trợ {label}:"
NO_CENTER = (
    "Hiện mình chưa có trung tâm đã xác minh nào hỗ trợ {label}. "
    "Bạn có thể xem danh sách trung tâm đầy đủ ở trang Trung tâm."
)
UNAVAILABLE = "Phần gợi ý trung tâm đang tạm gián đoạn, bạn thử lại sau ít phút nhé."
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
_DIGIT_RE = re.compile(r"\d")

# Nhu cầu đã kiểm tra
@dataclass(frozen=True)
class Need:
    service: str | None
    city: str | None
    general_help: str


# Lời của AI có chữ cấm hoặc con số thì ném ValueError để SkillCall gọi lại
def check_text(text: str, forbidden_names: Sequence[str] = ()) -> None:
    lowered = text.lower()
    for word in BANNED:
        if word in lowered:
            raise ValueError(f"có chữ cấm: {word}")
    for name in forbidden_names:
        if name.lower() in lowered:
            raise ValueError(f"tự nêu tên trung tâm: {name}")
    if _DIGIT_RE.search(text):
        raise ValueError("có con số (số liệu thuộc B5, phải có nguồn)")


# Kiểm tra lượt 1: dịch vụ phải thuộc danh mục; thành phố lạ thì coi như không nói.
# Lời khuyên có chữ cấm thì bỏ lời khuyên, vẫn giữ dịch vụ (không để học sinh mất danh sách vì câu chữ)
def check_need(raw: dict[str, Any], cities: list[str]) -> Need:
    service = raw.get("service") or None
    if service is not None and service not in SERVICES:
        raise ValueError(f"mã dịch vụ lạ: {service}")
    city = raw.get("city") or None
    if city not in cities:
        city = None
    general_help = str(raw.get("general_help") or "").strip()
    try:
        check_text(general_help)
    except ValueError as exc:
        logger.warning("Bỏ lời khuyên chung của AI: %s", exc)
        general_help = ""
    return Need(service, city, general_help if service else "")


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


# Kiểm tra lượt 2: đủ một câu cho mỗi trung tâm, câu ngắn, không chữ cấm, không tên trung tâm, đúng dữ liệu
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


# Câu giải thích dựng bằng code từ dữ liệu trung tâm: dùng khi AI sai hai lần
def template_reason(center: CenterHit, service: str, level: str | None, city: str | None) -> str:
    parts = [f"Có {SERVICES[service]}"]
    if level and level in center.study_levels:
        parts.append(f"nhận học sinh bậc {LEVEL_LABELS.get(level, level)}")
    if same_city(center.city, city):
        parts.append("cùng thành phố với bạn")
    return ", ".join(parts) + "."


def service_buttons() -> list[QuickReply]:
    return [
        QuickReply(label=label[:1].upper() + label[1:], value=f"Mình cần hỗ trợ {label}")
        for label in SERVICES.values()
    ]


class ChuyenTrungTamHandler:
    def __init__(self, llm: Any, model: str, finder: CenterFinder) -> None:
        self._finder = finder
        self._need = SkillCall(llm, model, NEED_PATH)
        self._explain = SkillCall(llm, model, EXPLAIN_PATH)

    async def run(self, state: ChatState, message: str) -> SkillOutput:
        try:
            cities = await self._finder.cities()
        except Exception:  # backend tắt hoặc lỗi: nói thật là đang gián đoạn, không bịa danh sách
            logger.exception("Lấy danh sách thành phố thất bại")
            return SkillOutput(UNAVAILABLE)

        services = [{"code": code, "label": label} for code, label in SERVICES.items()]
        need = await self._need.run(
            {"message": message, "services": services, "cities": cities},
            NEED_SCHEMA,
            lambda raw: check_need(raw, cities),
        )
        # Chưa rõ dịch vụ (hoặc AI sai hai lần): hỏi lại bằng nút, học sinh bấm là gửi lại một câu rõ dịch vụ
        if need is None or need.service is None:
            return SkillOutput(ASK_SERVICE, quick_replies=service_buttons())

        level = state.study_level
        try:
            centers = await self._finder.find(need.service, level, need.city)
        except Exception:
            logger.exception("Lấy trung tâm thất bại")
            return SkillOutput(UNAVAILABLE)

        label = SERVICES[need.service]
        parts = [need.general_help] if need.general_help else []
        if not centers:  # TH-63: không có trung tâm đã xác minh cho dịch vụ này
            parts.append(NO_CENTER.format(label=label))
            return SkillOutput("\n\n".join(parts))

        reasons = await self._explain_centers(centers, need, level)
        lines = [INTRO.format(label=label)]
        for i, center in enumerate(centers, start=1):
            where = f" ({center.city})" if center.city else ""
            site = f" Website: {center.website}" if center.website else ""
            lines.append(f"- {center.name}{where}: {reasons[i]}{site}")
        parts.append("\n".join(lines))
        return SkillOutput("\n\n".join(parts), disclaimer=DISCLAIMER)

    # AI viết mỗi trung tâm một câu "vì sao hợp". AI không thấy tên trung tâm; code tự so thành phố, bậc học
    # rồi đưa kết quả đúng/sai cho AI (AI so chữ hay nhầm, ví dụ nói "cùng thành phố" khi không phải)
    async def _explain_centers(
        self, centers: list[CenterHit], need: Need, level: str | None
    ) -> dict[int, str]:
        payload = {
            "need": SERVICES[need.service],
            "student_level": LEVEL_LABELS.get(level) if level else None,
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
        reasons = await self._explain.run(
            payload, REASON_SCHEMA, lambda raw: check_reasons(raw, centers, need.city)
        )
        if reasons is None:
            logger.warning("AI không viết được câu giải thích hợp lệ, dùng câu dựng sẵn")
            return {
                i: template_reason(c, need.service, level, need.city) for i, c in enumerate(centers, start=1)
            }
        return reasons
