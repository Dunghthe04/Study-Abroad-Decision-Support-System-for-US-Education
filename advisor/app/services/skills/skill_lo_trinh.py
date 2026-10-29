"""skill_lo_trinh (B4): chọn lộ trình du học và mốc thời gian (framework 4.6.1).

F3. Lộ trình do code chọn theo 4 luật, xét từ trên xuống, luật đúng đầu tiên là lộ trình chính:
  1. còn dưới 1 tháng tới hạn nộp            → dời kỳ (postpone)
  2. tiếng Anh thấp, hoặc chưa có mà không kịp thi → năm dự bị (pathway)
  3. tài chính thiếu hoặc học lực yếu         → cao đẳng cộng đồng 2+2 (cc_2plus2)
  4. còn lại                                  → đại học thẳng (direct)
Các luật đúng khác (trừ direct) là lộ trình thay thế.
F4. Qwen chỉ viết lý do. Qwen đổi lộ trình, bịa số hay hứa đỗ thì gọi lại 1 lần, vẫn sai thì dùng câu mẫu.
Mốc thời gian và tiêu chí chọn trường do code viết.
"""

import logging
from dataclasses import dataclass, field
from datetime import date
from typing import Any

from pydantic import BaseModel, Field

from app.schemas.chat import ChatState, ProfileField
from app.services.chat_flow import SkillOutput, go_to
from app.services.skill_call import SKILLS_DIR, SkillCall
from app.services.skills.skill_danh_gia import Facts, assess_levels, check_text, read_facts, value_of
from app.services.skills.validators import month_index, parse_year_month

logger = logging.getLogger("advisor.chat")

SKILL_PATH = SKILLS_DIR / "skill_lo_trinh.md"

ROUTES = ("postpone", "pathway", "cc_2plus2", "direct")
ROUTE_LABELS = {
    "direct": "Đại học thẳng",
    "pathway": "Năm dự bị (pathway)",
    "cc_2plus2": "Cao đẳng cộng đồng 2+2",
    "postpone": "Dời sang kỳ nhập học sau",
}
REASON_CODES = ("DEADLINE_SOON", "ENGLISH_LOW", "FINANCE_SHORT", "ACADEMIC_WEAK")

# Mốc [tham khảo] theo kỳ nhập học "Fall 2027": (tháng nhập học, hạn nộp thường lệ (RD) tính bằng (lệch năm, tháng))
INTAKE_MONTH = {"Fall": 8, "Spring": 1, "Summer": 6}
RD_DEADLINE = {"Fall": (0, 1), "Spring": (-1, 10), "Summer": (0, 3)}
EARLY_DEADLINE = (-1, 11)  # EA/ED khoảng tháng 11 năm trước, chỉ áp cho kỳ Fall
TEST_LEAD_MONTHS = 2  # thi xong trước hạn nộp ít nhất 2 tháng
VISA_LEAD_MONTHS = 3  # xin visa khoảng 3 tháng trước khi nhập học
MIN_MONTHS = 1  # còn dưới 1 tháng tới hạn nộp thì dời kỳ

CHECK_DEADLINE = "Bạn nhớ kiểm tra hạn nộp chính xác trên trang của từng trường."
NO_LIST = "Hệ thống chưa có danh sách trường cho lộ trình này. Khi tự tìm trường, bạn xem các tiêu chí:"
CRITERIA = (
    "- Có ngành bạn muốn học{major}",
    "- Ở bang bạn muốn (nếu có), gần người thân càng tốt",
    "- Tổng chi phí mỗi năm vừa ngân sách gia đình",
    "- Yêu cầu đầu vào (GPA, tiếng Anh) vừa sức với hồ sơ hiện tại",
)
# Lộ trình đại học thẳng: nhờ .NET chạy gợi ý trường (lọc, chấm, chọn) rồi đưa danh sách vào chat
RECOMMEND_ACTION = "recommend"
DIRECT_NEXT = "Bước tiếp theo: hệ thống sẽ gợi ý danh sách trường theo hồ sơ và ngành của bạn."


def month_name(index: int) -> str:
    year, month = divmod(index - 1, 12)
    return f"tháng {month + 1}/{year}"


# Kỳ nhập học đã đọc ra số tháng
@dataclass
class Intake:
    term: str
    year: int
    start: int  # tháng nhập học
    deadline: int  # hạn nộp thường lệ
    early: int | None  # hạn EA/ED

    @classmethod
    def parse(cls, text: Any) -> "Intake | None":
        parts = str(text or "").split()
        if len(parts) != 2 or parts[0] not in INTAKE_MONTH or not parts[1].isdigit():
            return None
        term, year = parts[0], int(parts[1])
        dy, dm = RD_DEADLINE[term]
        early = month_index(year + EARLY_DEADLINE[0], EARLY_DEADLINE[1]) if term == "Fall" else None
        return cls(term, year, month_index(year, INTAKE_MONTH[term]), month_index(year + dy, dm), early)


# Kết quả chọn lộ trình
@dataclass
class Route:
    route: str
    alt_routes: list[str]
    codes: list[str]  # mã lý do của các luật đúng
    english_status: str  # ok | low | none_with_time | none_no_time
    months_to_deadline: int | None
    reasons: list[str] = field(default_factory=list)
    ai_used: bool = False


def english_status(english: str, plan: Any, intake: Intake | None, today_index: int) -> str:
    if english == "low":
        return "low"
    if english != "none":
        return "ok"
    if intake is None:
        return "none_with_time"
    last_test = intake.deadline - TEST_LEAD_MONTHS
    plan_month = parse_year_month(plan.get("date")) if isinstance(plan, dict) else None
    if today_index > last_test or (plan_month is not None and plan_month > last_test):
        return "none_no_time"
    return "none_with_time"


# F3. Chọn lộ trình bằng code
def choose_route(
    profile: dict[str, ProfileField], today: date | None = None
) -> tuple[Route, Facts, Intake | None]:
    today = today or date.today()
    today_index = month_index(today.year, today.month)
    facts = read_facts(profile, today)
    levels = assess_levels(facts)
    intake = Intake.parse(value_of(profile, "intake"))
    months = intake.deadline - today_index if intake else None
    status = english_status(levels["english"], value_of(profile, "english_plan"), intake, today_index)

    hits: list[tuple[str, str]] = []  # (lộ trình, mã lý do) theo thứ tự luật
    if months is not None and months < MIN_MONTHS:
        hits.append(("postpone", "DEADLINE_SOON"))
    if status in ("low", "none_no_time"):
        hits.append(("pathway", "ENGLISH_LOW"))
    if levels["finance"] == "short":
        hits.append(("cc_2plus2", "FINANCE_SHORT"))
    if levels["academic"] == "weak":
        hits.append(("cc_2plus2", "ACADEMIC_WEAK"))

    routes = list(dict.fromkeys(route for route, _ in hits)) or ["direct"]
    route = Route(
        route=routes[0],
        alt_routes=routes[1:],
        codes=[code for _, code in hits],
        english_status=status,
        months_to_deadline=months,
    )
    return route, facts, intake


REASON_TEXT = {
    "DEADLINE_SOON": "Còn chưa tới 1 tháng là hết hạn nộp hồ sơ, không đủ thời gian chuẩn bị.",
    "ENGLISH_LOW": "Tiếng Anh chưa đạt mức đa số trường yêu cầu; năm dự bị cho bạn vừa học tiếng vừa học môn đại cương.",
    "FINANCE_SHORT": "Ngân sách còn hẹp; 2 năm đầu ở cao đẳng cộng đồng rẻ hơn nhiều, sau đó chuyển tiếp lên đại học.",
    "ACADEMIC_WEAK": "Điểm học còn thấp so với đa số trường; cao đẳng cộng đồng giúp bạn có bảng điểm mới trước khi chuyển tiếp.",
}
DIRECT_TEXT = "Hồ sơ của bạn đủ điều kiện nộp thẳng vào đại học."


def template_reasons(route: Route) -> list[str]:
    return [REASON_TEXT[code] for code in route.codes] or [DIRECT_TEXT]


# Mốc thời gian theo kỳ nhập học (code viết, không qua Qwen)
def timeline(intake: Intake | None, route: Route) -> list[str]:
    if intake is None:
        return ["Bạn chưa chọn kỳ nhập học, mình chưa lập được mốc thời gian."]
    if route.route == "postpone":
        return [f"Kỳ {intake.term} {intake.year} đã quá sát hạn nộp, bạn nên nhắm kỳ nhập học sau."]
    lines = []
    if intake.early is not None:
        lines.append(f"- Nộp sớm (EA/ED): khoảng {month_name(intake.early)}")
    lines.append(f"- Nộp thường lệ (RD): khoảng {month_name(intake.deadline)}")
    if route.english_status != "ok":
        lines.append(f"- Thi xong tiếng Anh trước {month_name(intake.deadline - TEST_LEAD_MONTHS)}")
    lines.append(f"- Xin visa: khoảng {month_name(intake.start - VISA_LEAD_MONTHS)}")
    lines.append(f"- Nhập học: {month_name(intake.start)}")
    return lines


# Số được phép trong lý do Qwen viết: số trong hồ sơ + năm, tháng của các mốc thời gian
def timeline_numbers(intake: Intake | None, route: Route) -> frozenset[float]:
    numbers = {float(MIN_MONTHS), float(TEST_LEAD_MONTHS), float(VISA_LEAD_MONTHS)}
    if route.months_to_deadline is not None:
        numbers.add(float(route.months_to_deadline))
    if intake is not None:
        for index in (intake.start, intake.deadline, intake.early, intake.start - VISA_LEAD_MONTHS):
            if index is not None:
                year, month = divmod(index - 1, 12)
                numbers |= {float(year), float(month + 1)}
    return frozenset(numbers)


# F4. Dạng JSON Qwen trả
class AiRoute(BaseModel):
    route: str
    alt_routes: list[str]
    reasons: list[str] = Field(min_length=1, max_length=3)


SCHEMA = {
    "type": "object",
    "required": ["route", "alt_routes", "reasons"],
    "properties": {
        "route": {"enum": list(ROUTES)},
        "alt_routes": {"type": "array", "items": {"enum": list(ROUTES)}},
        "reasons": {"type": "array", "items": {"type": "string"}, "minItems": 1, "maxItems": 3},
    },
}


# Kiểm tra JSON Qwen: lộ trình phải đúng code chọn, lý do không bịa số, không hứa đỗ. Sai thì ném ValueError
def check_route(raw: dict[str, Any], route: Route, facts: Facts, intake: Intake | None) -> Route:
    ai = AiRoute.model_validate(raw)
    if ai.route != route.route or ai.alt_routes != route.alt_routes:
        raise ValueError(
            f"Qwen đổi lộ trình: {ai.route} {ai.alt_routes} khác {route.route} {route.alt_routes}"
        )
    check_text(ai.reasons, facts, timeline_numbers(intake, route))
    route.reasons, route.ai_used = ai.reasons, True
    return route


# Dữ liệu gửi Qwen: mức code đã tính và lộ trình code chọn, không có tin nhắn của học sinh
def ai_input(route: Route, facts: Facts) -> dict[str, Any]:
    return {
        "levels": assess_levels(facts),
        "english_status": route.english_status,
        "months_to_deadline": route.months_to_deadline,
        "route": route.route,
        "alt_routes": route.alt_routes,
        "reason_codes": route.codes,
    }


class RoutePlanner:
    def __init__(self, llm: Any, model: str) -> None:
        self._call = SkillCall(llm, model, SKILL_PATH)
        self.skill_version = self._call.skill_version

    async def plan(
        self, profile: dict[str, ProfileField], today: date | None = None
    ) -> tuple[Route, Intake | None]:
        route, facts, intake = choose_route(profile, today)

        def check(raw: dict[str, Any]) -> Route:
            return check_route(raw, route, facts, intake)

        result = await self._call.run(ai_input(route, facts), SCHEMA, check)
        if result is None:
            logger.warning("skill_lo_trinh: Qwen sai 2 lần, dùng câu mẫu cho lộ trình %s", route.route)
            route.reasons = template_reasons(route)
            return route, intake
        return result, intake


def render(route: Route, intake: Intake | None, major: Any) -> str:
    parts = [
        f"Lộ trình phù hợp với bạn: {ROUTE_LABELS[route.route]}.",
        "\n".join(f"- {r}" for r in route.reasons),
    ]
    if route.alt_routes:
        parts.append(
            "Bạn cũng có thể cân nhắc: " + ", ".join(ROUTE_LABELS[r] for r in route.alt_routes) + "."
        )
    parts.append("Mốc thời gian dự kiến:\n" + "\n".join(timeline(intake, route)) + f"\n{CHECK_DEADLINE}")
    if route.route == "direct":
        parts.append(DIRECT_NEXT)
    else:
        major_text = f" ({major})" if isinstance(major, str) and major else ""
        parts.append(NO_LIST + "\n" + "\n".join(CRITERIA).format(major=major_text))
    return "\n\n".join(parts)

# Chỉ lộ trình đại học thẳng mới có danh sách trường; lộ trình khác hệ thống chưa có dữ liệu trường
def action_for(route: Route) -> str | None:
    return RECOMMEND_ACTION if route.route == "direct" else None

class LoTrinhHandler:
    def __init__(self, llm: Any, model: str) -> None:
        self._planner = RoutePlanner(llm, model)

    async def run(self, state: ChatState, message: str) -> SkillOutput:
        route, intake = await self._planner.plan(state.profile)
        go_to(state, "B4")
        return SkillOutput(render(route, intake, value_of(state.profile, "major")), action=action_for(route))
