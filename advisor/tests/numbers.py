"""Đồ nghề của test: tập số được phép xuất hiện trong skill do code dựng từ hồ sơ.

Dùng để kiểm "mọi số code điền vào skill đều truy ra được từ hồ sơ, dòng tính sẵn hoặc mốc lộ trình" (test_skill_template).
Trước đây các hàm này nằm trong app (skill_danh_gia, skill_lo_trinh) để kiểm lời Qwen viết; Qwen không còn viết lời đó nên chỉ test dùng.
"""

import re

from app.services.skills.skill_danh_gia import Facts
from app.services.skills.skill_lo_trinh import (
    MIN_MONTHS,
    TEST_LEAD_MONTHS,
    VISA_LEAD_MONTHS,
    Intake,
    Route,
)
from app.services.skills.validators import BUDGET_RANGES

NUMBER = re.compile(r"\d+(?:[.,]\d+)*")

# Số được phép xuất hiện ngoài số trong hồ sơ: thang điểm 4, 10, "bậc x/5" và các số trong câu mẫu
# (thi trước 2 tháng, SAT khoảng 1400, ngân sách 35.000 USD)
FREE_NUMBERS = {2.0, 4.0, 5.0, 10.0, 1400.0, 35_000.0}


# Một cụm số trong câu có thể hiểu theo nhiều cách: "35.000" = 35000, "3.5" hay "3,5" = 3.5
def readings(token: str) -> set[float]:
    found = set()
    if re.fullmatch(r"\d{1,3}(?:[.,]\d{3})+", token):
        found.add(float(re.sub(r"[.,]", "", token)))
    if token.count(".") + token.count(",") <= 1:
        found.add(float(token.replace(",", ".")))
    return found


# Các số được phép xuất hiện: số trong hồ sơ, thang điểm
def allowed_numbers(facts: Facts) -> set[float]:
    numbers = {facts.gpa, facts.gpa_scale, facts.gpa4, facts.sat, facts.english_score, facts.ielts}
    numbers |= {facts.ec_tier, facts.activities}
    if facts.budget_tier:
        # Sàn và trần của mức ngân sách, viết đủ (35.000) hoặc theo nghìn (35k); bỏ sàn 0 và trần không có
        bounds = {n for n in BUDGET_RANGES[facts.budget_tier] if n}
        numbers |= bounds | {n / 1000 for n in bounds}
    return {float(n) for n in numbers if n is not None} | FREE_NUMBERS


# Số được phép trong phần lộ trình: số trong hồ sơ + năm, tháng của các mốc thời gian
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
