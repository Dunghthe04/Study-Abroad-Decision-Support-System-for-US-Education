"""skill_danh_gia (B3): đánh giá hồ sơ mạnh / yếu từng mặt (framework 4.5).

F1. Mức từng mặt do code tính, ngưỡng chép đúng assessProfile (frontend ProfileSummary.tsx), để chat và trang
phân tích cho cùng một kết luận. Ngưỡng là [tham khảo], chỉ sửa ở đây và sửa cả hai bên.
F2. Qwen chỉ viết lời nhận xét từ các mức đó. Qwen đổi mức, bịa số hay hứa đỗ thì gọi lại 1 lần,
vẫn sai thì dùng câu mẫu của code. Viết xong thì chuyển B4 và hỏi X2.
Thiếu SAT, hoạt động... thì mức "none", khuyên đi thi / bổ sung, không đoán điểm.
"""

import logging
import re
from dataclasses import dataclass, field
from datetime import date
from typing import Any

from pydantic import BaseModel, Field

from app.schemas.chat import ChatState, ProfileField, QuickReply
from app.services.chat_flow import SkillOutput, ask, go_to
from app.services.skill_call import SKILLS_DIR, SkillCall
from app.services.skills.validators import IMPACT_LEVELS, month_index, parse_month_year

logger = logging.getLogger("advisor.chat")

SKILL_PATH = SKILLS_DIR / "skill_danh_gia.md"

# Ngưỡng [tham khảo] (giống assessProfile): (ngưỡng dưới, mức), dò từ cao xuống thấp; không đạt ngưỡng nào là mức cuối
ACADEMIC_CUTS = [(3.5, "strong"), (3.0, "medium")]  # GPA thang 4
SAT_CUTS = [(1400, "strong"), (1200, "medium")]
ENGLISH_CUTS = [(7.0, "excellent"), (6.0, "good"), (5.5, "near")]  # quy về IELTS
FINANCE_CUTS = [(60_000, "ok"), (35_000, "tight")]  # USD mỗi năm. Chưa chốt Q3b: không sửa
LOWEST = {"academic": "weak", "sat": "weak", "english": "low", "finance": "short"}

# Đổi TOEFL / Duolingo sang IELTS tương đương (ieltsEquivalent bên frontend): (điểm từ, IELTS)
TOEFL_TO_IELTS = [(110, 8.0), (94, 7.0), (79, 6.0), (72, 5.5)]
DUOLINGO_TO_IELTS = [(135, 7.5), (120, 7.0), (105, 6.0), (95, 5.5)]
BELOW_TABLE_IELTS = 5.0

# Mức ngân sách → số USD dùng để so ngưỡng (budgetMaxUsd, mức cao nhất không có trần thì lấy budgetMinUsd)
TIER_USD = {
    "tier_under_20k": 20_000,
    "tier_20k_35k": 35_000,
    "tier_35k_50k": 50_000,
    "tier_50k_70k": 70_000,
    "tier_above_70k": 70_000,
}

# Bậc ngoại khóa 1–5 (ecTierOf bên frontend)
ROLE_WEIGHT = {"founder": 1.0, "leader": 1.0, "vice_leader": 0.75, "member": 0.5}
DEFAULT_ROLE_WEIGHT = 0.5
DEFAULT_MONTHS = 3
EC_CUTS = [0.10, 0.25, 0.45, 0.65]
EC_STRONG = 4  # từ bậc 4 là điểm mạnh
EC_WEAK = 2  # bậc 2 trở xuống là điểm yếu
MAX_POINTS = 3  # tối đa 3 điểm mạnh, 3 điểm yếu

ENGLISH_PASS = ("excellent", "good")
SCHOLARSHIP_CHANCES = ("high", "medium", "low")
LEVEL_VALUES = {
    "academic": ("strong", "medium", "weak", "none"),
    "sat": ("strong", "medium", "weak", "none"),
    "english": ("excellent", "good", "near", "low", "none"),
    "finance": ("ok", "tight", "short", "none"),
    "scholarship": SCHOLARSHIP_CHANCES,
}

# Không hứa đỗ
BANNED_WORDS = ("chắc chắn", "đảm bảo", "100%", "chắc đỗ", "chắc đậu")
# Số được phép xuất hiện ngoài số trong hồ sơ: thang điểm 4, 10, "bậc x/5" và các số trong câu mẫu
# (thi trước 2 tháng, SAT khoảng 1400, ngân sách 35.000 USD)
FREE_NUMBERS = {2.0, 4.0, 5.0, 10.0, 1400.0, 35_000.0}

YES_REPLY = QuickReply(label="Có, gợi ý giúp mình", value="Có")
NO_REPLY = QuickReply(label="Để sau", value="Để sau")


# Mức của một giá trị theo bảng ngưỡng
def level_of(value: float | None, cuts: list[tuple[float, str]], lowest: str) -> str:
    if value is None:
        return "none"
    for cut, level in cuts:
        if value >= cut:
            return level
    return lowest


def from_table(score: float, table: list[tuple[float, float]]) -> float:
    for cut, ielts in table:
        if score >= cut:
            return ielts
    return BELOW_TABLE_IELTS


# Các con số đã đọc từ hồ sơ chat, đủ để tính mức
@dataclass
class Facts:
    gpa: float | None = None  # đúng số học sinh nói
    gpa_scale: int | None = None
    gpa4: float | None = None
    sat: int | None = None
    english_type: str | None = None
    english_score: float | None = None
    ielts: float | None = None  # IELTS tương đương
    budget_tier: str | None = None
    budget_usd: int | None = None
    ec_tier: int | None = None
    activities: int = 0


def value_of(profile: dict[str, ProfileField], name: str) -> Any:
    item = profile.get(name)
    return item.value if item is not None else None


def ielts_equivalent(cert: str | None, score: float | None) -> float | None:
    if cert is None or score is None:
        return None
    if cert == "ielts":
        return score
    if cert == "toefl":
        return from_table(score, TOEFL_TO_IELTS)
    if cert == "duolingo":
        return from_table(score, DUOLINGO_TO_IELTS)
    return None  # PTE: frontend chưa quy đổi, giữ giống để hai bên cùng kết luận


# Số tháng tham gia một hoạt động (countMonths bên frontend)
def months_of(activity: dict, today: date) -> int:
    start = parse_month_year(activity.get("startDate"))
    end = (
        month_index(today.year, today.month)
        if activity.get("isOngoing")
        else parse_month_year(activity.get("endDate"))
    )
    if start is None or end is None:
        return DEFAULT_MONTHS
    return max(1, end - start + 1)


# Bậc ngoại khóa từ hoạt động và giải thưởng trong hồ sơ chat (ecTierOf bên frontend)
def ec_tier_of(activities: list[dict], awards: list[dict], today: date) -> int:
    scores = []
    for a in activities:
        scope = IMPACT_LEVELS.index(a["impactLevel"]) + 1 if a.get("impactLevel") in IMPACT_LEVELS else 1
        role = ROLE_WEIGHT.get(a.get("role") or "", DEFAULT_ROLE_WEIGHT)
        months = months_of(a, today)
        scores.append(0.4 * scope / 5 + 0.35 * role + 0.25 * min(months / 24, 1))
    top = sorted(scores, reverse=True)[:4]
    award_points = 0.0
    for w in awards[:3]:
        scope = IMPACT_LEVELS.index(w["impactLevel"]) + 1 if w.get("impactLevel") in IMPACT_LEVELS else 1
        award_points += 0.05 * scope
    m = (sum(top) + min(0.5, award_points)) / 4
    return 1 + sum(1 for cut in EC_CUTS if m >= cut)


def read_facts(profile: dict[str, ProfileField], today: date | None = None) -> Facts:
    today = today or date.today()
    facts = Facts()

    gpa = value_of(profile, "gpa")
    if isinstance(gpa, dict) and gpa.get("value") is not None:
        facts.gpa, facts.gpa_scale = float(gpa["value"]), int(gpa.get("scale") or 10)
        facts.gpa4 = facts.gpa if facts.gpa_scale == 4 else round(facts.gpa / 10 * 4, 2)

    sat = value_of(profile, "sat")
    facts.sat = int(sat) if isinstance(sat, int | float) else None

    english = value_of(profile, "english")
    if isinstance(english, dict) and english.get("value") is not None:
        facts.english_type = str(english.get("type") or "").lower() or None
        facts.english_score = float(english["value"])
        facts.ielts = ielts_equivalent(facts.english_type, facts.english_score)

    tier = value_of(profile, "budget")
    if tier in TIER_USD:
        facts.budget_tier, facts.budget_usd = tier, TIER_USD[tier]

    activities = value_of(profile, "activities")
    activities = activities if isinstance(activities, list) else []
    awards = value_of(profile, "awards")
    awards = awards if isinstance(awards, list) else []
    facts.activities = len(activities)
    if activities or awards:
        facts.ec_tier = ec_tier_of(activities, awards, today)
    return facts


# Kết quả đánh giá: mức từng mặt + lời nhận xét
@dataclass
class Assessment:
    levels: dict[str, Any]
    summary: str = ""
    strengths: list[str] = field(default_factory=list)
    weaknesses: list[str] = field(default_factory=list)
    ai_used: bool = False


# F1. Mức từng mặt bằng code
def assess_levels(facts: Facts) -> dict[str, Any]:
    academic = level_of(facts.gpa4, ACADEMIC_CUTS, LOWEST["academic"])
    english = level_of(facts.ielts, ENGLISH_CUTS, LOWEST["english"])
    # Học bổng: học lực mạnh và tiếng Anh đạt → cao; một trong hai → trung bình; không có → thấp
    hits = (academic == "strong") + (english in ENGLISH_PASS)
    return {
        "academic": academic,
        "sat": level_of(facts.sat, SAT_CUTS, LOWEST["sat"]),
        "english": english,
        "finance": level_of(facts.budget_usd, FINANCE_CUTS, LOWEST["finance"]),
        "ec_tier": facts.ec_tier,
        "scholarship": SCHOLARSHIP_CHANCES[2 - hits],
    }


def fmt(x: float) -> str:
    return f"{x:g}".replace(".", ",")


def english_name(facts: Facts) -> str:
    return f"{(facts.english_type or '').upper()} {fmt(facts.english_score or 0)}"


# Câu mẫu của code (giống chữ của assessProfile), dùng khi Qwen lỗi
def template_points(levels: dict[str, Any], facts: Facts) -> tuple[list[str], list[str]]:
    strengths: list[str] = []
    weak: list[str] = []
    gpa = f"GPA {fmt(facts.gpa or 0)} (thang {facts.gpa_scale})"
    if levels["academic"] == "strong":
        strengths.append(f"{gpa} ở mức mạnh.")
    elif levels["academic"] == "medium":
        weak.append(f"{gpa} ở mức trung bình. Giữ hoặc tăng điểm học kỳ tới sẽ mở thêm trường.")
    elif levels["academic"] == "weak":
        weak.append(f"{gpa} còn thấp so với đa số trường.")

    if levels["english"] in ENGLISH_PASS:
        strengths.append(f"Tiếng Anh {english_name(facts)} đạt yêu cầu của phần lớn trường.")
    elif levels["english"] in ("near", "low"):
        weak.append(
            f"{english_name(facts)} chưa đạt mức nhiều trường yêu cầu. Nên thi lại hoặc cân nhắc năm dự bị."
        )
    elif facts.english_type:
        weak.append(f"Hệ thống chưa quy đổi được điểm {english_name(facts)}, tư vấn viên sẽ xem giúp bạn.")
    else:
        weak.append("Chưa có chứng chỉ tiếng Anh. Cần thi trước hạn nộp ít nhất 2 tháng.")

    if levels["sat"] == "strong":
        strengths.append(f"SAT {facts.sat} ở mức mạnh.")
    elif levels["sat"] == "medium":
        weak.append(
            f"SAT {facts.sat} ở mức trung bình. Thi lại lên khoảng 1400 giúp nhiều trường chuyển nhóm."
        )
    elif levels["sat"] == "none":
        weak.append("Chưa có SAT. Hệ thống ưu tiên trường không bắt buộc SAT.")

    if levels["finance"] == "ok":
        strengths.append("Ngân sách đủ cho phần lớn trường.")
    elif levels["finance"] == "short":
        weak.append("Ngân sách dưới 35.000 USD mỗi năm, lựa chọn trường sẽ hẹp.")

    ec = levels["ec_tier"]
    if ec is not None and ec >= EC_STRONG:
        strengths.append("Hoạt động ngoại khóa có vai trò và phạm vi tốt.")
    if ec is None or ec <= EC_WEAK:
        weak.append("Hoạt động ngoại khóa còn ít. Một hoạt động có vai trò rõ sẽ giúp hồ sơ.")
    return strengths[:MAX_POINTS], weak[:MAX_POINTS]


SCHOLARSHIP_TEXT = {
    "high": "Khả năng xin học bổng khá tốt vì học lực mạnh và tiếng Anh đạt.",
    "medium": "Có thể xin học bổng ở một số trường; cải thiện học lực hoặc tiếng Anh sẽ tăng cơ hội.",
    "low": "Cơ hội học bổng còn thấp; nên ưu tiên cải thiện học lực và tiếng Anh trước.",
}


def template(levels: dict[str, Any], facts: Facts) -> Assessment:
    strengths, weak = template_points(levels, facts)
    summary = "Mình đã xem hồ sơ của bạn. " + SCHOLARSHIP_TEXT[levels["scholarship"]]
    return Assessment(levels=levels, summary=summary, strengths=strengths, weaknesses=weak)


# F2. Dạng JSON Qwen trả
class AiLevels(BaseModel):
    academic: str
    sat: str
    english: str
    finance: str
    ec_tier: int | None = None
    scholarship: str


class AiAssessment(BaseModel):
    levels: AiLevels
    summary: str = Field(min_length=1, max_length=600)
    strengths: list[str] = Field(max_length=MAX_POINTS)
    weaknesses: list[str] = Field(max_length=MAX_POINTS)


SCHEMA = {
    "type": "object",
    "required": ["levels", "summary", "strengths", "weaknesses"],
    "properties": {
        "levels": {
            "type": "object",
            "required": ["academic", "sat", "english", "finance", "ec_tier", "scholarship"],
            "properties": {
                **{name: {"enum": list(values)} for name, values in LEVEL_VALUES.items()},
                "ec_tier": {"type": ["integer", "null"]},
            },
        },
        "summary": {"type": "string"},
        "strengths": {"type": "array", "items": {"type": "string"}, "maxItems": MAX_POINTS},
        "weaknesses": {"type": "array", "items": {"type": "string"}, "maxItems": MAX_POINTS},
    },
}

NUMBER = re.compile(r"\d+(?:[.,]\d+)*")


# Một cụm số trong câu có thể hiểu theo nhiều cách: "35.000" = 35000, "3.5" hay "3,5" = 3.5
def readings(token: str) -> set[float]:
    found = set()
    if re.fullmatch(r"\d{1,3}(?:[.,]\d{3})+", token):
        found.add(float(re.sub(r"[.,]", "", token)))
    if token.count(".") + token.count(",") <= 1:
        found.add(float(token.replace(",", ".")))
    return found


# Các số được phép xuất hiện trong lời nhận xét: số trong hồ sơ, thang điểm
def allowed_numbers(facts: Facts) -> set[float]:
    numbers = {facts.gpa, facts.gpa_scale, facts.gpa4, facts.sat, facts.english_score, facts.ielts}
    numbers |= {facts.budget_usd, facts.ec_tier, facts.activities or None}
    if facts.budget_tier:
        numbers |= {float(n) * 1000 for n in re.findall(r"\d+", facts.budget_tier)}
        numbers |= {float(n) for n in re.findall(r"\d+", facts.budget_tier)}
    return {float(n) for n in numbers if n is not None} | FREE_NUMBERS


# extra: số khác được phép (vd năm, tháng trong mốc thời gian của skill_lo_trinh)
def check_text(texts: list[str], facts: Facts, extra: frozenset[float] = frozenset()) -> None:
    allowed = allowed_numbers(facts) | extra
    for text in texts:
        low = text.lower()
        for word in BANNED_WORDS:
            if word in low:
                raise ValueError(f"Lời nhận xét hứa hẹn: {word}")
        for token in NUMBER.findall(text):
            if not any(abs(r - a) < 0.005 for r in readings(token) for a in allowed):
                raise ValueError(f"Số {token} không có trong hồ sơ")


# Kiểm tra JSON Qwen: mức phải đúng mức code tính, lời không bịa số, không hứa đỗ. Sai thì ném ValueError
def check_assessment(raw: dict[str, Any], levels: dict[str, Any], facts: Facts) -> Assessment:
    ai = AiAssessment.model_validate(raw)
    if ai.levels.model_dump() != levels:
        raise ValueError(f"Qwen đổi mức: {ai.levels.model_dump()} khác {levels}")
    check_text([ai.summary, *ai.strengths, *ai.weaknesses], facts)
    return Assessment(
        levels=levels, summary=ai.summary, strengths=ai.strengths, weaknesses=ai.weaknesses, ai_used=True
    )


# Dữ liệu gửi Qwen: chỉ số trong hồ sơ và mức code đã tính, không có tin nhắn của học sinh
def ai_input(levels: dict[str, Any], facts: Facts) -> dict[str, Any]:
    return {
        "profile": {
            "gpa": facts.gpa,
            "gpa_scale": facts.gpa_scale,
            "sat": facts.sat,
            "english": {"type": facts.english_type, "value": facts.english_score}
            if facts.english_type
            else None,
            "budget_usd_per_year": facts.budget_usd,
            "activities": facts.activities,
        },
        "levels": levels,
    }


class Assessor:
    def __init__(self, llm: Any, model: str) -> None:
        self._call = SkillCall(llm, model, SKILL_PATH)
        self.skill_version = self._call.skill_version

    async def assess(self, profile: dict[str, ProfileField], today: date | None = None) -> Assessment:
        facts = read_facts(profile, today)
        levels = assess_levels(facts)

        def check(raw: dict[str, Any]) -> Assessment:
            return check_assessment(raw, levels, facts)

        result = await self._call.run(ai_input(levels, facts), SCHEMA, check)
        if result is None:
            logger.warning("skill_danh_gia: Qwen sai 2 lần, dùng câu mẫu theo mức %s", levels)
            return template(levels, facts)
        return result


def render(result: Assessment) -> str:
    lines = [result.summary]
    if result.strengths:
        lines.append("Điểm mạnh:\n" + "\n".join(f"- {s}" for s in result.strengths))
    if result.weaknesses:
        lines.append("Cần cải thiện:\n" + "\n".join(f"- {s}" for s in result.weaknesses))
    lines.append("Đây là đánh giá tham khảo, không phải kết quả tuyển sinh.")
    return "\n\n".join(lines)


class DanhGiaHandler:
    def __init__(self, llm: Any, model: str) -> None:
        self._assessor = Assessor(llm, model)

    async def run(self, state: ChatState, message: str) -> SkillOutput:
        result = await self._assessor.assess(state.profile)
        go_to(state, "B4")
        return SkillOutput(f"{render(result)}\n\n{ask(state, 'X2')}", quick_replies=[YES_REPLY, NO_REPLY])
