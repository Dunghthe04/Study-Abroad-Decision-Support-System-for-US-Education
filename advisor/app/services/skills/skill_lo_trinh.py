"""Chọn lộ trình du học và mốc thời gian (framework 4.6.1).

F3. Lộ trình do code chọn theo 4 luật, xét từ trên xuống, luật đúng đầu tiên là lộ trình chính:
  1. còn dưới 1 tháng tới hạn nộp            → dời kỳ (postpone)
  2. tiếng Anh thấp, hoặc chưa có mà không kịp thi → năm dự bị (pathway)
  3. tài chính thiếu hoặc học lực yếu         → cao đẳng cộng đồng 2+2 (cc_2plus2)
  4. còn lại                                  → đại học thẳng (direct)
Các luật đúng khác (trừ direct) là lộ trình thay thế.
Lý do, mốc thời gian và tiêu chí chọn trường do code viết (câu mẫu). Claude chỉ diễn đạt lại ở skill_ho_so_khuon.
"""

from dataclasses import dataclass, field
from datetime import date
from typing import Any

from app.schemas.chat import ProfileField
from app.services.skills.skill_danh_gia import (
    Facts,
    assess_levels,
    read_facts,
    value_of,
)
from app.services.skills.validators import month_index, parse_year_month

ROUTE_LABELS = {
    "direct": "Đại học thẳng",
    "pathway": "Năm dự bị (pathway)",
    "cc_2plus2": "Cao đẳng cộng đồng 2+2",
    "postpone": "Dời sang kỳ nhập học sau",
}

# Mốc [tham khảo] theo kỳ nhập học "Fall 2027": (tháng nhập học, hạn nộp thường lệ (RD) tính bằng (lệch năm, tháng))
INTAKE_MONTH = {"Fall": 8, "Spring": 1, "Summer": 6}
RD_DEADLINE = {"Fall": (0, 1), "Spring": (-1, 10), "Summer": (0, 3)}
EARLY_DEADLINE = (-1, 11)  # EA/ED khoảng tháng 11 năm trước, chỉ áp cho kỳ Fall
TEST_LEAD_MONTHS = 2  # thi xong trước hạn nộp ít nhất 2 tháng
VISA_LEAD_MONTHS = 3  # xin visa khoảng 3 tháng trước khi nhập học
MIN_MONTHS = 1  # còn dưới 1 tháng tới hạn nộp thì dời kỳ

CHECK_DEADLINE = "Bạn nhớ kiểm tra hạn nộp chính xác trên trang của từng trường."
NO_LIST = "Hệ thống chưa có danh sách trường cho lộ trình này. Khi tự tìm trường, bạn xem các tiêu chí:"
TARGET_MORE = "Khi tìm thêm trường dự phòng hợp với lộ trình này, bạn xem các tiêu chí:"
CRITERIA = (
    "- Có ngành bạn muốn học{major}",
    "- Ở bang bạn muốn (nếu có), gần người thân càng tốt",
    "- Tổng chi phí mỗi năm vừa ngân sách gia đình",
    "- Yêu cầu đầu vào (GPA, tiếng Anh) vừa sức với hồ sơ hiện tại",
)


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
    target: bool = (
        False  # đã xét theo trường mục tiêu (roadmap), lý do nói "trường mục tiêu" thay cho "đa số trường"
    )


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


# Có trường mục tiêu (roadmap mode "target"): xét theo trường đòi hỏi cao nhất thay cho ngưỡng chung.
# Tiếng Anh / GPA chưa đạt mốc ở bất kỳ trường mục tiêu nào thì coi là thấp / yếu; đạt hoặc gần đạt hết thì không.
# Mặt nào không trường nào so được (thiếu dữ liệu) thì giữ mức theo ngưỡng chung. Tài chính vẫn theo mức ngân sách
TARGET_LEVEL = {"english": ("low", "good"), "academic": ("weak", "medium")}
TARGET_KEY = {"english": "english", "academic": "gpa"}


def target_levels(levels: dict[str, Any], roadmap: dict | None) -> dict[str, Any]:
    if not roadmap or roadmap.get("mode") != "target":
        return levels
    result = dict(levels)
    for side, key in TARGET_KEY.items():
        if levels[side] == "none":
            continue  # chưa có điểm (ví dụ chưa thi tiếng Anh): giữ cách xét "kịp thi hay không"
        statuses = [
            c.get("status")
            for school in roadmap.get("schools", [])
            for c in school.get("criteria", [])
            if c.get("key") == key and c.get("status") in ("met", "near", "below")
        ]
        if statuses:
            low, ok = TARGET_LEVEL[side]
            result[side] = low if "below" in statuses else ok
    return result


# F3. Chọn lộ trình bằng code
def choose_route(
    profile: dict[str, ProfileField],
    today: date | None = None,
    gpa_table: list[tuple[float, float]] | None = None,
    roadmap: dict | None = None,
) -> tuple[Route, Facts, Intake | None]:
    today = today or date.today()
    today_index = month_index(today.year, today.month)
    facts = read_facts(profile, today, gpa_table)
    base = assess_levels(facts)
    levels = target_levels(base, roadmap)
    # Hồ sơ chưa chọn kỳ thì dùng kỳ Tổng quan đang vẽ (ước tính), để chat và Tổng quan nói cùng một kỳ
    intake = Intake.parse(value_of(profile, "intake")) or Intake.parse((roadmap or {}).get("intake"))
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
        target=levels is not base,
    )
    return route, facts, intake


REASON_TEXT = {
    "DEADLINE_SOON": "Còn chưa tới 1 tháng là hết hạn nộp hồ sơ, không đủ thời gian chuẩn bị.",
    "ENGLISH_LOW": "Tiếng Anh chưa đạt mức đa số trường yêu cầu; năm dự bị cho bạn vừa học tiếng vừa học môn đại cương.",
    "FINANCE_SHORT": "Ngân sách còn hẹp; 2 năm đầu ở cao đẳng cộng đồng rẻ hơn nhiều, sau đó chuyển tiếp lên đại học.",
    "ACADEMIC_WEAK": "Điểm học còn thấp so với đa số trường; cao đẳng cộng đồng giúp bạn có bảng điểm mới trước khi chuyển tiếp.",
}
# Nói đúng điều code đã xét (không vướng lý do nào để đi đường khác), không để hiểu thành chắc đỗ
DIRECT_TEXT = (
    "Theo hồ sơ hiện tại, bạn chưa cần đi qua năm dự bị hay cao đẳng cộng đồng. "
    "Đây chưa phải đánh giá khả năng trúng tuyển vào một trường cụ thể."
)


# Xét theo trường mục tiêu thì tiếng Anh / học lực so với mốc của trường mục tiêu, không phải đa số trường
TARGET_REASON_TEXT = {
    "ENGLISH_LOW": "Tiếng Anh chưa đạt mốc của trường mục tiêu; năm dự bị cho bạn vừa học tiếng vừa học môn đại cương.",
    "ACADEMIC_WEAK": "GPA chưa đạt mốc của trường mục tiêu; cao đẳng cộng đồng giúp bạn có bảng điểm mới trước khi chuyển tiếp.",
}


def template_reasons(route: Route) -> list[str]:
    texts = {**REASON_TEXT, **TARGET_REASON_TEXT} if route.target else REASON_TEXT
    return [texts[code] for code in route.codes] or [DIRECT_TEXT]


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


ESTIMATE_INTAKE = "Kỳ {intake} là kỳ ước tính, bạn chọn kỳ nhập học trong hồ sơ để mốc sát hơn."


# "2027-01-05" → "05/01/2027"
def day_text(raw: Any) -> str | None:
    try:
        return date.fromisoformat(str(raw)[:10]).strftime("%d/%m/%Y")
    except ValueError:
        return None


# Có lộ trình từ .NET thì chép đúng các mốc Tổng quan đang vẽ, không tự tính mốc khác
def roadmap_timeline(roadmap: dict | None) -> list[str]:
    lines = []
    for m in (roadmap or {}).get("milestones", []):
        day = day_text(m.get("date"))
        if day and m.get("label"):
            lines.append(f"- {day}: {m['label']}" + (" (tham khảo)" if m.get("isEstimate") else ""))
    if lines and (roadmap or {}).get("intakeIsEstimate"):
        lines.append(ESTIMATE_INTAKE.format(intake=roadmap.get("intake")))
    return lines


def target_names(roadmap: dict | None) -> list[str]:
    if not roadmap or roadmap.get("mode") != "target":
        return []
    return [str(s["name"]) for s in roadmap.get("schools", []) if s.get("name")]


def render(route: Route, intake: Intake | None, major: Any, roadmap: dict | None = None) -> str:
    parts = [
        f"Lộ trình phù hợp với bạn: {ROUTE_LABELS[route.route]}.",
        "\n".join(f"- {r}" for r in route.reasons),
    ]
    if route.alt_routes:
        parts.append(
            "Bạn cũng có thể cân nhắc: " + ", ".join(ROUTE_LABELS[r] for r in route.alt_routes) + "."
        )
    lines = (route.route != "postpone" and roadmap_timeline(roadmap)) or timeline(intake, route)
    parts.append("Mốc thời gian dự kiến:\n" + "\n".join(lines) + f"\n{CHECK_DEADLINE}")
    major_text = f" ({major})" if isinstance(major, str) and major else ""
    criteria = "\n".join(CRITERIA).format(major=major_text)
    names = target_names(roadmap)
    # Đại học thẳng không có đoạn chọn trường: không hứa "hệ thống sẽ gợi ý danh sách" vì chỉ hỏi lộ trình thì không chạy
    # gợi ý trường, học sinh chờ mãi không thấy danh sách
    if route.route != "direct":
        if names:
            parts.append(f"Trường mục tiêu của bạn: {', '.join(names)}. {TARGET_MORE}\n{criteria}")
        else:
            parts.append(NO_LIST + "\n" + criteria)
    return "\n\n".join(parts)
