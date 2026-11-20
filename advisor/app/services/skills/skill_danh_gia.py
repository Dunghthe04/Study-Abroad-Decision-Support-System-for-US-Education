"""Tính mức từng mặt của hồ sơ (framework 4.5): học lực, SAT, tiếng Anh, tài chính, ngoại khóa, học bổng.

Mức từng mặt do code tính, ngưỡng chép đúng assessProfile (frontend ProfileSummary.tsx), để chat và trang
phân tích cho cùng một kết luận. Ngưỡng là [tham khảo], chỉ sửa ở đây và sửa cả hai bên.
Thiếu SAT, hoạt động... thì mức "none", khuyên đi thi / bổ sung, không đoán điểm.
Lời nhận xét không viết ở đây: skill_ho_so_khuon và viet_tra_loi_ho_so để Claude viết từ các mức này.
"""

import re
from dataclasses import dataclass, field
from datetime import date
from decimal import ROUND_HALF_UP, Decimal
from typing import Any

from app.schemas.chat import GradeScaleRule, ProfileField
from app.services.skills.validators import BUDGET_RANGES, IMPACT_LEVELS, month_index, parse_month_year

# Ngưỡng [tham khảo] (giống assessProfile): (ngưỡng dưới, mức), dò từ cao xuống thấp; không đạt ngưỡng nào là mức cuối
ACADEMIC_CUTS = [(3.5, "strong"), (3.0, "medium")]  # GPA thang 4
SAT_CUTS = [(1400, "strong"), (1200, "medium")]
ENGLISH_CUTS = [(7.0, "excellent"), (6.0, "good"), (5.5, "near")]  # quy về IELTS
LOWEST = {"academic": "weak", "sat": "weak", "english": "low"}

# Đổi TOEFL / Duolingo sang IELTS tương đương (ieltsEquivalent bên frontend): (điểm từ, IELTS)
TOEFL_TO_IELTS = [(110, 8.0), (94, 7.0), (79, 6.0), (72, 5.5)]
DUOLINGO_TO_IELTS = [(135, 7.5), (120, 7.0), (105, 6.0), (95, 5.5)]
BELOW_TABLE_IELTS = 5.0

# Mức ngân sách → mức tài chính, xếp thẳng theo mã (khoảng tiền xem BUDGET_RANGES). Không đổi mức ra một con số
# vì mức trên 70k không có trần. Kết quả giữ như ngưỡng cũ (≥ 60k ok, ≥ 35k tight); chưa chốt Q3b: không sửa
TIER_FINANCE = {
    "tier_under_20k": "short",  # 0 – 20.000
    "tier_20k_35k": "tight",  # 20.000 – 35.000
    "tier_35k_50k": "tight",  # 35.000 – 50.000
    "tier_50k_70k": "ok",  # 50.000 – 70.000
    "tier_above_70k": "ok",  # từ 70.000, không trần
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
# Số được phép xuất hiện ngoài số trong hồ sơ: thang điểm 4, 10, "bậc x/5" và các số trong câu mẫu
# (thi trước 2 tháng, SAT khoảng 1400, ngân sách 35.000 USD)
FREE_NUMBERS = {2.0, 4.0, 5.0, 10.0, 1400.0, 35_000.0}


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


# Mốc (điểm thang 10, GPA 4) xếp tăng dần, lấy từ grade_scale trong state (.NET gửi mốc moderator sửa mỗi lượt).
# Không giữ bản sao mốc ở advisor: None hoặc rỗng = không có bảng, không quy đổi
def gpa_table_of(rules: list[GradeScaleRule] | None) -> list[tuple[float, float]] | None:
    if not rules:
        return None
    return sorted((float(r.min_score), float(r.gpa4)) for r in rules)


# Chỉ điểm tổng (TB thang 10) mới quy sang thang 4; điểm từng môn giữ nguyên thang 10.
# Nội suy đường thẳng giữa hai mốc kề nhau, ngoài hai đầu lấy GPA mốc đầu/cuối (giống GradeScale.Interpolate bên
# .NET). Tính bằng Decimal và làm tròn 2 chữ số nửa lên như .NET lưu: 8.5 → 3.63 (round() của Python ra 3.62).
# Không có bảng thì trả None: không đoán GPA, mức học lực là "none"
def gpa10_to_4(score: float, table: list[tuple[float, float]] | None) -> float | None:
    if not table:
        return None
    x = Decimal(str(score))
    points = [(Decimal(str(s)), Decimal(str(g))) for s, g in table]
    if x <= points[0][0]:
        gpa4 = points[0][1]
    elif x >= points[-1][0]:
        gpa4 = points[-1][1]
    else:
        upper = next(i for i, (s, _) in enumerate(points) if s >= x)
        (x0, y0), (x1, y1) = points[upper - 1], points[upper]
        gpa4 = y0 + (x - x0) * (y1 - y0) / (x1 - x0)
    return float(gpa4.quantize(Decimal("0.01"), rounding=ROUND_HALF_UP))


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


def read_facts(
    profile: dict[str, ProfileField],
    today: date | None = None,
    gpa_table: list[tuple[float, float]] | None = None,
) -> Facts:
    today = today or date.today()
    facts = Facts()

    gpa = value_of(profile, "gpa")
    if isinstance(gpa, dict) and gpa.get("value") is not None:
        facts.gpa, facts.gpa_scale = float(gpa["value"]), int(gpa.get("scale") or 10)
        facts.gpa4 = facts.gpa if facts.gpa_scale == 4 else gpa10_to_4(facts.gpa, gpa_table)

    sat = value_of(profile, "sat")
    facts.sat = int(sat) if isinstance(sat, int | float) else None

    english = value_of(profile, "english")
    if isinstance(english, dict) and english.get("value") is not None:
        facts.english_type = str(english.get("type") or "").lower() or None
        facts.english_score = float(english["value"])
        facts.ielts = ielts_equivalent(facts.english_type, facts.english_score)

    tier = value_of(profile, "budget")
    if tier in TIER_FINANCE:
        facts.budget_tier = tier

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
        "finance": TIER_FINANCE.get(facts.budget_tier, "none"),
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
    numbers |= {facts.ec_tier, facts.activities}
    if facts.budget_tier:
        # Sàn và trần của mức ngân sách, viết đủ (35.000) hoặc theo nghìn (35k); bỏ sàn 0 và trần không có
        bounds = {n for n in BUDGET_RANGES[facts.budget_tier] if n}
        numbers |= bounds | {n / 1000 for n in bounds}
    return {float(n) for n in numbers if n is not None} | FREE_NUMBERS
