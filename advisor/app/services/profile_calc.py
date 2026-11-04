"""Tính sẵn các dòng chữ cho skill ho_so, để skill chỉ đọc rồi nhận xét, không tự tính.

- Mức GPA / tiếng Anh / SAT / tài chính, bậc ngoại khóa, cơ hội học bổng: lấy từ assess_levels (skill_danh_gia),
  để chat, trang phân tích và skill cùng một kết luận.
- Điểm TB từng năm từ học bạ theo năm (state.transcript): chỉ ghi điểm, SLM tự nhận xét tăng / giảm.
- Mốc thời gian: chỉ tháng bắt đầu của kỳ là quy ước; hạn nộp lấy từ dữ liệu trường (DeadlinesJson), không có thì không ghi.
Thiếu dữ liệu thì bỏ khóa, không đoán.
"""

import re
from datetime import date
from typing import Any

from app.schemas.chat import ProfileField
from app.services.skills.buttons import BUDGET_LABELS, is_middle_school_profile
from app.services.skills.skill_danh_gia import Facts, assess_levels, read_facts
from app.services.skills.validators import month_index, parse_year_month

# Chữ của từng mức (mức "none" = thiếu dữ liệu, không có chữ)
ACADEMIC_WORDS = {"strong": "giỏi", "medium": "khá", "weak": "trung bình"}
SAT_WORDS = {"strong": "cao", "medium": "trung bình", "weak": "thấp"}
ENGLISH_WORDS = {"excellent": "rất tốt", "good": "khá", "near": "gần đạt", "low": "chưa đạt"}
FINANCE_WORDS = {"ok": "đủ", "tight": "sát", "short": "thiếu"}
SCHOLARSHIP_WORDS = {"high": "cao", "medium": "trung bình", "low": "thấp"}
TEST_NAMES = {
    "ielts": "IELTS",
    "toefl": "TOEFL",
    "duolingo": "Duolingo",
    "pte": "PTE",
    "sat": "SAT",
    "act": "ACT",
}

# Tháng bắt đầu của từng kỳ nhập học (theo bài ATS). Hạn nộp lấy từ dữ liệu trường, không ghi cứng ở đây
TERM_START_MONTH = {"Fall": 8, "Spring": 1, "Summer": 5}
INTAKE = re.compile(r"^(Fall|Spring|Summer)\s+(\d{4})$")
PLANS = (("english_plan", "tiếng Anh"), ("standardized_plan", "chuẩn hóa"))


def num(x: float) -> str:
    return f"{x:g}"


def test_name(raw: Any, default: str) -> str:
    text = str(raw or "").strip()
    return TEST_NAMES.get(text.lower(), text) or default


def month_text(index: int) -> str:
    year, month = divmod(index - 1, 12)
    return f"{month + 1:02d}/{year}"


def value_of(profile: dict[str, ProfileField], key: str) -> Any:
    item = profile.get(key)
    return item.value if item is not None else None


def gpa_level(facts: Facts, levels: dict[str, Any], middle_school: bool) -> str | None:
    if facts.gpa4 is None:
        return None
    prefix = "mục tiêu " if middle_school else ""
    return f"{prefix}{num(facts.gpa4)} thang 4, mức {ACADEMIC_WORDS[levels['academic']]}"


# Ghi điểm gốc của chứng chỉ (không quy đổi IELTS); PTE chưa có ngưỡng nên chỉ ghi điểm
def english_level(facts: Facts, levels: dict[str, Any]) -> str | None:
    if facts.english_type is None or facts.english_score is None:
        return None
    text = f"{test_name(facts.english_type, '')} {num(facts.english_score)}"
    word = ENGLISH_WORDS.get(levels["english"])
    return f"{text}, mức {word}" if word else text


# TB từng năm, không so sánh: SLM tự đọc rồi nhận xét đoạn nào tăng, đoạn nào giảm
def grade_by_year(transcript: dict | None) -> str | None:
    years = [y for y in (transcript or {}).get("years", []) if y.get("average") is not None]
    # nhập GPA tổng, hoặc THCS chỉ có GPA mục tiêu → không có dòng này
    return "; ".join(f"{y['grade']}: {num(y['average'])}" for y in years) or None


# Hạn nộp thật của các trường: [{"RD": "2027-02-01", "EA": "2026-11-01"}, ...] → [(tháng, "RD"), ...] tăng dần
def deadline_months(deadlines: list[dict] | None) -> list[tuple[int, str]]:
    found = []
    for item in deadlines or []:
        for kind, day in item.items():
            month = parse_year_month(day[:7]) if isinstance(day, str) else None
            if month is not None:
                found.append((month, kind))
    return sorted(found)


def timeline(profile: dict[str, ProfileField], today: date, deadlines: list[dict] | None) -> str | None:
    match = INTAKE.match(str(value_of(profile, "intake") or "").strip())
    if not match:
        return None
    start = month_index(int(match[2]), TERM_START_MONTH[match[1]])
    now = month_index(today.year, today.month)
    head = f"hôm nay {month_text(now)}, nhập học kỳ {match[1]} {month_text(start)}"
    if start <= now:
        return f"{head}, kỳ nhập học đã qua"

    parts = [f"{head}, còn {start - now} tháng"]
    found = deadline_months(deadlines)
    if found:  # chỉ khi có hạn nộp thật của trường
        (first, first_kind), (last, last_kind) = found[0], found[-1]
        parts.append(
            f"hạn nộp các trường đã gợi ý: sớm nhất {month_text(first)} ({first_kind}), "
            f"muộn nhất {month_text(last)} ({last_kind})"
        )
        for key, default in PLANS:
            plan = value_of(profile, key)
            when = parse_year_month(plan.get("date")) if isinstance(plan, dict) else None
            if when is not None:
                side = "sau" if when > last else "trước"
                name = test_name(plan.get("test_type"), default)
                parts.append(f"kế hoạch thi {name} {month_text(when)} là {side} hạn nộp muộn nhất")
    return "; ".join(parts)


def calc_profile(
    profile: dict[str, ProfileField],
    transcript: dict | None,
    today: date,
    deadlines: list[dict] | None = None,
) -> dict[str, str]:
    facts = read_facts(profile, today)
    levels = assess_levels(facts)
    result = {
        "grade_by_year": grade_by_year(transcript),
        "gpa_level": gpa_level(facts, levels, is_middle_school_profile(profile)),
        "english_level": english_level(facts, levels),
        "sat_level": f"{facts.sat}, mức {SAT_WORDS[levels['sat']]}" if facts.sat is not None else None,
        "budget_level": (
            f"{BUDGET_LABELS[facts.budget_tier]} mỗi năm, mức {FINANCE_WORDS[levels['finance']]}"
            if facts.budget_tier
            else None
        ),
        "ec_level": f"bậc {facts.ec_tier}/5" if facts.ec_tier is not None else None,
        "scholarship_chance": SCHOLARSHIP_WORDS[levels["scholarship"]] if facts.gpa4 is not None else None,
        "timeline": timeline(profile, today, deadlines),
    }
    return {key: text for key, text in result.items() if text}
