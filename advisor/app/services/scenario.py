"""Kịch bản what-if trong chat: kiểm tra `thay_doi` của SLM rồi đổi thành giá trị cho API simulate của .NET.

SLM đã hiểu câu và chuẩn hóa: gia_tri là số sạch ("1500", "3.8", "800000000"), don_vi cho biết thang GPA
(thang_4, thang_10) hoặc tiền tệ (USD, VND). Code chỉ làm hai việc SLM không làm được:
đổi VNĐ sang USD theo tỷ giá hôm nay, và chốt kiểm tra bằng đúng luật của hồ sơ (validators.py),
vì model nhỏ vẫn có lúc trả số vô lý. Sai thì hỏi lại học sinh, không đem đi mô phỏng.
Khóa đầu ra trùng SimulateRequest bên .NET, và chỉ gồm yếu tố mô phỏng được (SimulationOverrides).
"""

from dataclasses import dataclass, field
from typing import Any

from app.services.skills import validators
from app.services.skills.validators import AskAgain

# Khóa hồ sơ mô phỏng được. Chứng chỉ tiếng Anh thử được IELTS, TOEFL, Duolingo (don_vi cho biết bài nào)
SUPPORTED = ("gpa", "sat", "act", "english", "budget", "scholarship")
SUPPORTED_TEXT = "GPA, SAT, ACT, IELTS, TOEFL, Duolingo, ngân sách, mức cần học bổng"
# Học sinh nói "bỏ" một yếu tố: kịch bản chỉ đổi giá trị, không xóa được điểm
SKIP_ACTION = "bo_qua"
GPA_SCALES = {"thang_4": 4, "thang_10": 10}
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


@dataclass
class WhatIf:
    # Khóa SimulateRequest: gpa {value, scale}, sat, act, ielts, toefl, duolingo, budgetUsd, scholarshipNeed
    changes: dict[str, Any] = field(default_factory=dict)
    # Yếu tố học sinh muốn đổi nhưng mô phỏng chưa làm được (tên tiếng Việt, không trùng nhau)
    unsupported: list[str] = field(default_factory=list)

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


# gia_tri là số SLM đã chuẩn hóa. Không đọc được thì hỏi lại. Chấp nhận dấu phẩy thập phân phòng SLM chưa chuẩn
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


# Điểm tiếng Anh giả định: don_vi cho biết bài thi (ielts, toefl, duolingo). Chưa rõ bài nào thì chỉ suy ra khi điểm chắc chắn
# là IELTS (từ 0 đến 9); điểm lớn hơn có thể là TOEFL hoặc Duolingo nên hỏi lại, không đoán. Bài khác (PTE...) mô phỏng chưa làm được
ENGLISH_TESTS = {"ielts": "IELTS", "toefl": "TOEFL", "duolingo": "Duolingo"}


def _check_english(value: object, unit: str | None) -> tuple[str, Any] | None:
    if unit is not None and unit not in ENGLISH_TESTS:
        return None
    if unit is None:
        number = _number(value, "Bạn muốn thử điểm tiếng Anh bao nhiêu, theo bài thi nào (IELTS, TOEFL hay Duolingo)?")
        if number > 9:
            raise AskAgain(f"Điểm {number:g} là của bài thi nào, TOEFL hay Duolingo? Bạn cho mình biết để thử đúng nhé.")
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


# Đọc danh sách thay_doi của SLM. Giá trị sai thì ném AskAgain; yếu tố mô phỏng chưa làm được thì ghi vào unsupported
def parse_what_if(changes: list[dict[str, Any]]) -> WhatIf:
    result = WhatIf()
    for change in changes:
        key = change.get("yeu_to")
        checked = None
        if key in SUPPORTED and change.get("hanh_dong") != SKIP_ACTION:
            checked = _check(key, change.get("gia_tri", ""), change.get("don_vi"))
        if checked is None:
            label = LABELS.get(key, "yếu tố này") if key != "english" else "chứng chỉ tiếng Anh ngoài IELTS, TOEFL, Duolingo"
            if change.get("hanh_dong") == SKIP_ACTION:
                label = LABELS.get(key, "yếu tố này")
            if label not in result.unsupported:
                result.unsupported.append(label)
            continue
        name, value = checked
        result.changes[name] = value
    return result
