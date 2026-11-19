"""Kịch bản what-if trong chat: kiểm tra `changes` của SLM rồi đổi thành giá trị cho API simulate của .NET.

SLM đã hiểu câu và chuẩn hóa: value là số sạch ("1500", "3.8", "800000000"), unit cho biết thang GPA
(scale_4, scale_10) hoặc tiền tệ (USD, VND). Code chỉ làm hai việc SLM không làm được:
đổi VNĐ sang USD theo tỷ giá hôm nay, và chốt kiểm tra bằng đúng luật của hồ sơ (validators.py),
vì model nhỏ vẫn có lúc trả số vô lý. Sai thì hỏi lại học sinh, không đem đi mô phỏng.
Khóa đầu ra trùng SimulateRequest bên .NET, và chỉ gồm yếu tố mô phỏng được (SimulationOverrides).
"""

from dataclasses import dataclass, field
from typing import Any

from app.schemas.chat import ProfileField, QuickReply
from app.services.priority import PRIORITY_KEY
from app.services.skills import validators
from app.services.skills.validators import AskAgain

# Khóa hồ sơ mô phỏng được. Chứng chỉ tiếng Anh thử được IELTS, TOEFL, Duolingo (unit cho biết bài nào)
SUPPORTED = ("gpa", "sat", "act", "english", "budget", "scholarship")
SUPPORTED_TEXT = "GPA, SAT, ACT, IELTS, TOEFL, Duolingo, ngân sách, mức cần học bổng, và ưu tiên (chi phí thấp, học bổng nhiều, danh tiếng)"
# Học sinh nói "bỏ" một yếu tố: kịch bản chỉ đổi giá trị, không xóa được điểm
SKIP_ACTION = "skip"
GPA_SCALES = {"scale_4": 4, "scale_10": 10}
SCHOLARSHIP_TEXT = {"optional": "không cần", "preferred": "nên có", "mandatory": "bắt buộc"}

# Tên tiếng Việt của yếu tố chưa thử được, để câu trả lời nói rõ
LABELS = {
    "gpa": "GPA",
    "sat": "SAT",
    "act": "ACT",
    "english": "điểm tiếng Anh",
    "current_level": "trình độ hiện tại",
    "current_grade": "lớp đang học",
    "target_level": "bậc học",
    "major": "ngành",
    "intake": "kỳ nhập học",
    "education_system": "hệ học",
    "english_plan": "kế hoạch thi tiếng Anh",
    "standardized_plan": "kế hoạch thi SAT/ACT",
    "funding_source": "nguồn tài chính",
    "activities": "hoạt động ngoại khóa",
    "awards": "giải thưởng",
    "relatives_state": "người thân tại Mỹ",
    "preferred_states": "bang muốn học",
    "school_type": "loại trường",
    "visa_refused": "lịch sử visa",
}
# Mục lọc trường bên .NET (RecommendationService lọc theo ngành, bậc học, bang, loại trường): đổi là đổi hồ sơ thật,
# không thử tạm được. Mỗi mục kèm nhóm của ô chọn sửa hồ sơ (skill_ho_so.EDIT_SECTIONS) để gắn nút sửa
FILTER_FACTORS = {
    "major": "Mục tiêu",
    "target_level": "Mục tiêu",
    "preferred_states": "Nguyện vọng",
    "school_type": "Nguyện vọng",
}
# Mục phần gợi ý trường bên .NET không dùng: đổi cũng không làm đổi danh sách trường
NO_EFFECT_FACTORS = (
    "intake",
    "education_system",
    "current_grade",
    "current_level",
    "english_plan",
    "standardized_plan",
    "funding_source",
    "relatives_state",
    "visa_refused",
)


@dataclass
class WhatIf:
    # Khóa SimulateRequest: gpa {value, scale}, sat, act, ielts, toefl, duolingo, budgetUsd, scholarshipNeed
    changes: dict[str, Any] = field(default_factory=dict)
    # Yếu tố học sinh muốn đổi nhưng mô phỏng chưa làm được (tên tiếng Việt, không trùng nhau)
    unsupported: list[str] = field(default_factory=list)
    # Trong số đó: mục lọc trường và mục không làm đổi danh sách trường (mã mục, không trùng nhau)
    filters: list[str] = field(default_factory=list)
    no_effect: list[str] = field(default_factory=list)

    def label(self) -> str:
        """Nói lại các thay đổi bằng lời, để học sinh biết mình đang thử gì."""
        parts = []
        if (gpa := self.changes.get("gpa")) is not None:
            parts.append(f"GPA {gpa['value']:g} (thang {gpa['scale']})")
        if (sat := self.changes.get("sat")) is not None:
            parts.append(f"SAT {sat}")
        if (act := self.changes.get("act")) is not None:
            parts.append(f"ACT {act}")
        if (ielts := self.changes.get("ielts")) is not None:
            parts.append(f"IELTS {ielts:g}")
        if (toefl := self.changes.get("toefl")) is not None:
            parts.append(f"TOEFL {toefl:g}")
        if (duolingo := self.changes.get("duolingo")) is not None:
            parts.append(f"Duolingo {duolingo:g}")
        if (usd := self.changes.get("budgetUsd")) is not None:
            parts.append(f"ngân sách khoảng {usd:,.0f} USD mỗi năm".replace(",", "."))
        if (need := self.changes.get("scholarshipNeed")) is not None:
            parts.append(f"mức cần học bổng: {SCHOLARSHIP_TEXT[need]}")
        return ", ".join(parts)


# value là số SLM đã chuẩn hóa. Không đọc được thì hỏi lại. Chấp nhận dấu phẩy thập phân phòng SLM chưa chuẩn
def _number(value: object, ask: str) -> float:
    try:
        return float(str(value).strip().replace(",", "."))
    except ValueError:
        raise AskAgain(ask) from None


def _budget_usd(amount: float, unit: str | None) -> int:
    currency = (unit or ("VND" if amount >= validators.VND_GUESS_FROM else "USD")).strip().upper()
    currency = validators.CURRENCY_ALIASES.get(currency, currency)
    validators.check_budget(amount, currency)  # kiểm số tiền và đơn vị, sai thì hỏi lại
    return round(validators.to_usd(amount, currency))


# Điểm tiếng Anh giả định: unit cho biết bài thi (ielts, toefl, duolingo). Chưa rõ bài nào thì chỉ suy ra khi điểm chắc chắn
# là IELTS (từ 0 đến 9); điểm lớn hơn có thể là TOEFL hoặc Duolingo nên hỏi lại, không đoán. Bài khác (PTE...) mô phỏng chưa làm được
ENGLISH_TESTS = {"ielts": "IELTS", "toefl": "TOEFL", "duolingo": "Duolingo"}


def _check_english(value: object, unit: str | None) -> tuple[str, Any] | None:
    if unit is not None and unit not in ENGLISH_TESTS:
        return None
    if unit is None:
        number = _number(
            value, "Bạn muốn thử điểm tiếng Anh bao nhiêu, theo bài thi nào (IELTS, TOEFL hay Duolingo)?"
        )
        if number > 9:
            raise AskAgain(
                f"Điểm {number:g} là của bài thi nào, TOEFL hay Duolingo? Bạn cho mình biết để thử đúng nhé."
            )
        unit = "ielts"
    name = ENGLISH_TESTS[unit]
    return unit, validators.check_score(unit, _number(value, f"Bạn muốn thử {name} bao nhiêu điểm?"))


# Kiểm tra một thay đổi, trả (khóa SimulateRequest, giá trị) hoặc None nếu mô phỏng chưa làm được
def _check(key: str, value: object, unit: str | None) -> tuple[str, Any] | None:
    unit = unit.strip().lower() if unit else None
    match key:
        case "gpa":
            gpa, scale = validators.check_gpa(
                _number(value, "Bạn muốn thử GPA bao nhiêu?"), GPA_SCALES.get(unit or "")
            )
            return "gpa", {"value": gpa, "scale": scale}
        case "sat":
            return "sat", int(
                validators.check_score("sat", _number(value, "Bạn muốn thử SAT bao nhiêu điểm?"))
            )
        case "act":
            return "act", int(
                validators.check_score("act", _number(value, "Bạn muốn thử ACT bao nhiêu điểm?"))
            )
        case "english":
            return _check_english(value, unit)
        case "budget":
            amount = _number(value, "Bạn muốn thử ngân sách bao nhiêu mỗi năm? Cho mình một con số nhé.")
            return "budgetUsd", _budget_usd(amount, unit)
        case _:
            return "scholarshipNeed", validators.check_scholarship(str(value))


# Đọc danh sách changes của SLM. Giá trị sai thì ném AskAgain; yếu tố mô phỏng chưa làm được thì ghi vào unsupported
def parse_what_if(changes: list[dict[str, Any]]) -> WhatIf:
    result = WhatIf()
    for change in changes:
        key = change.get("factor")
        if key == PRIORITY_KEY or str(key).startswith(
            "priority_"
        ):  # ưu tiên đọc riêng (priority.py), không phải giá trị hồ sơ
            continue
        checked = None
        if key in SUPPORTED and change.get("action") != SKIP_ACTION:
            checked = _check(key, change.get("value", ""), change.get("unit"))
        if checked is None:
            label = (
                LABELS.get(key, "yếu tố này")
                if key != "english"
                else "chứng chỉ tiếng Anh ngoài IELTS, TOEFL, Duolingo"
            )
            if change.get("action") == SKIP_ACTION:
                label = LABELS.get(key, "yếu tố này")
            if label not in result.unsupported:
                result.unsupported.append(label)
            group = (
                result.filters
                if key in FILTER_FACTORS
                else result.no_effect
                if key in NO_EFFECT_FACTORS
                else None
            )
            if group is not None and key not in group:
                group.append(key)
            continue
        name, value = checked
        result.changes[name] = value
    return result


# ---------------------------------------------------------------------------------------------------------------------
# Nút thử nhanh: mỗi nút là một thay đổi cụ thể, cao hơn hồ sơ hiện tại một bậc. Câu gửi đi đều đã thử bằng model
# (đọc đúng what_if kèm số), nên bấm là chạy được, không bị hỏi lại. Học sinh vẫn gõ được mức khác.
# ---------------------------------------------------------------------------------------------------------------------
TRY_SAT = "Nếu SAT của em là {value} thì sao?"
TRY_GPA = "Nếu GPA của em là {value} thì sao?"
TRY_IELTS = "Nếu IELTS của em là {value} thì sao?"
TRY_BUDGET = "Nếu ngân sách của em là {value} USD mỗi năm thì sao?"
SAT_STEP, SAT_START, SAT_MAX = 100, 1400, 1600
IELTS_STEP, IELTS_START, IELTS_MAX = 0.5, 6.5, 9.0
GPA_STEP = {4: 0.2, 10: 0.5}
BUDGET_EXTRA = 10_000  # mức trên cùng không có trần: thử thêm chừng này


def _value(profile: dict[str, ProfileField], key: str) -> Any:
    item = profile.get(key)
    return item.value if item is not None else None


def _shown(number: float) -> str:
    return f"{number:g}"


def _usd(amount: int) -> str:
    return f"{amount:,}".replace(",", ".")


def try_buttons(profile: dict[str, ProfileField]) -> list[QuickReply]:
    """Nút thử nhanh SAT, GPA, IELTS, ngân sách theo hồ sơ. Mục đã ở mức cao nhất thì bỏ."""
    buttons: list[QuickReply] = []

    sat = _value(profile, "sat")
    if isinstance(sat, int | float) and not isinstance(sat, bool):
        sat_try = min(int(sat) + SAT_STEP, SAT_MAX) if sat < SAT_MAX else None
    else:
        sat_try = SAT_START
    if sat_try is not None:
        buttons.append(QuickReply(label=f"Thử SAT {sat_try}", value=TRY_SAT.format(value=sat_try)))

    gpa = _value(profile, "gpa")
    if isinstance(gpa, dict) and isinstance(gpa.get("value"), int | float) and gpa.get("scale") in GPA_STEP:
        scale = gpa["scale"]
        if gpa["value"] < scale:
            target = round(min(gpa["value"] + GPA_STEP[scale], scale), 1)
            buttons.append(
                QuickReply(label=f"Thử GPA {_shown(target)}", value=TRY_GPA.format(value=_shown(target)))
            )

    english = _value(profile, "english")
    ielts = english.get("value") if isinstance(english, dict) and english.get("type") == "ielts" else None
    if isinstance(ielts, int | float):
        ielts_try = min(ielts + IELTS_STEP, IELTS_MAX) if ielts < IELTS_MAX else None
    elif not isinstance(english, dict):
        ielts_try = IELTS_START  # chưa có chứng chỉ tiếng Anh
    else:
        ielts_try = None  # TOEFL, Duolingo...: không đoán mức quy đổi
    if ielts_try is not None:
        buttons.append(
            QuickReply(
                label=f"Thử IELTS {_shown(ielts_try)}", value=TRY_IELTS.format(value=_shown(ielts_try))
            )
        )

    budget = _value(profile, "budget")
    tiers = list(validators.BUDGET_RANGES)
    if isinstance(budget, str) and budget in tiers and budget != tiers[-1]:
        floor, ceiling = validators.BUDGET_RANGES[tiers[tiers.index(budget) + 1]]
        amount = ceiling if ceiling is not None else floor + BUDGET_EXTRA
        buttons.append(
            QuickReply(label=f"Thử ngân sách {_usd(amount)} USD", value=TRY_BUDGET.format(value=amount))
        )

    return buttons
