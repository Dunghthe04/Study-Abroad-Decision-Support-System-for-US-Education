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
from app.services.skills import exchange_rate, major_catalog, state_catalog, validators
from app.services.skills.validators import AskAgain

# Khóa hồ sơ mô phỏng được. Chứng chỉ tiếng Anh thử được IELTS, TOEFL, Duolingo (unit cho biết bài nào)
# UC-155: bang, ngành và tỉ giá cũng thử được (bang và ngành thay điều kiện lọc trên bản sao hồ sơ; tỉ giá đổi ngân sách USD, chỉ nhận mức tăng)
SUPPORTED = (
    "ec_tier",
    "gpa",
    "sat",
    "act",
    "english",
    "budget",
    "scholarship",
    "state",
    "preferred_states",
    "major",
    "exchange_rate",
)
SUPPORTED_TEXT = (
    "GPA, SAT, ACT, IELTS, TOEFL, Duolingo, ngân sách, mức cần học bổng, bang, ngành, tỉ giá (chỉ thử mức tăng), "
    "và ưu tiên (chi phí thấp, học bổng nhiều, danh tiếng)"
)
MAX_RATE_PERCENT = 100
EC_TIER_MAX = 5  # bậc ngoại khóa 1 đến 5 (3 là trung bình), cùng khoảng với backend
MIN_VND_PER_USD = 1_000  # dưới mức này không thể là tỉ giá VNĐ mỗi USD (người dùng nhầm đơn vị)
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
    "state": "bang",
    "school_type": "loại trường",
    "visa_refused": "lịch sử visa",
    "exchange_rate": "tỉ giá",
    "application_round": "vòng nộp (ED, EA, RD)",
    "ec_tier": "bậc ngoại khóa",
}
# Mục lọc trường bên .NET (RecommendationService lọc theo ngành, bậc học, bang, loại trường): đổi là đổi hồ sơ thật,
# không thử tạm được. Mỗi mục kèm nhóm của ô chọn sửa hồ sơ (skill_ho_so.EDIT_SECTIONS) để gắn nút sửa
# Ngành và bang đã thử được trên bản sao hồ sơ (UC-155) nên không còn ở đây
FILTER_FACTORS = {
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
    "application_round",  # vòng nộp ED, EA, RD không có trong công thức gợi ý (UC-22, UC-24)
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
        if (tier := self.changes.get("ecTier")) is not None:
            parts.append(f"bậc ngoại khóa {tier}")
        if (states := self.changes.get("states")) is not None:
            parts.append(f"bang {', '.join(states)}")
        if (major := self.changes.get("major")) is not None:
            parts.append(f"ngành {major}")
        if (vnd := self.changes.get("exchangeRateVnd")) is not None:
            parts.append(f"tỉ giá {vnd:,.0f} VNĐ mỗi USD".replace(",", "."))
        if (percent := self.changes.get("exchangeRatePercent")) is not None:
            parts.append(f"tỉ giá tăng {percent:g}%")
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


# Bang học sinh nói (một tên hoặc danh sách) đổi sang mã bang. Không đọc được danh sách bang từ DB (mã không phải hai chữ) thì chưa thử được
def _check_states(value: object, states: list[state_catalog.UsState]) -> tuple[str, Any] | None:
    names = (
        [str(v).strip() for v in value]
        if isinstance(value, list)
        else [p.strip() for p in str(value).split(",")]
    )
    names = [n for n in names if n]
    if not names:
        raise AskAgain("Bạn muốn thử bang nào? Cho mình tên bang bằng tiếng Anh nhé.")
    codes = state_catalog.to_codes(names, states)
    if not all(len(code) == 2 and code.isalpha() for code in codes):
        return None
    return "states", [code.upper() for code in codes]


# Ngành đổi sang tên ngành chuẩn của hệ thống (đúng tên dùng để lọc trường). Gần giống thì hỏi lại, không đoán
def _check_major(value: object, majors: list[major_catalog.Major]) -> tuple[str, Any] | None:
    text = str(value).strip()
    if not text:
        raise AskAgain("Bạn muốn thử ngành nào?")
    if not majors:
        return None
    found = major_catalog.match(text, majors)
    if found.exact is not None:
        return "major", found.exact.name
    if found.near:
        options = ", ".join(m.label for m in found.near)
        raise AskAgain(f'Mình thấy "{text}" gần với ngành {options}. Bạn chọn lại ngành giúp mình nhé?')
    raise AskAgain(f'Mình chưa tìm thấy ngành "{text}". Bạn nói lại tên ngành giúp mình nhé?')


# Tỉ giá giả định (UC-155): unit percent là mức tăng, còn lại là số VNĐ mỗi USD mới. Chỉ nhận mức tăng so với tỉ giá hiện nay
def _check_exchange_rate(value: object, unit: str | None) -> tuple[str, Any]:
    number = _number(value, "Bạn muốn thử tỉ giá tăng bao nhiêu, theo VNĐ mỗi USD hoặc theo phần trăm?")
    if unit in ("percent", "%"):
        if not 0 < number <= MAX_RATE_PERCENT:
            raise AskAgain(
                f"Mức tăng tỉ giá phải lớn hơn 0 và không quá {MAX_RATE_PERCENT}%. Bạn muốn thử tăng bao nhiêu phần trăm?"
            )
        return "exchangeRatePercent", number
    if number < MIN_VND_PER_USD:
        raise AskAgain(
            "Tỉ giá bạn nói là bao nhiêu VNĐ mỗi USD, hoặc tăng bao nhiêu phần trăm? Cho mình một con số nhé."
        )
    current = exchange_rate.vnd_per_usd()
    if number <= current:
        shown = f"{current:,.0f}".replace(",", ".")
        raise AskAgain(
            f"Tỉ giá chỉ thử được mức tăng so với hiện nay (khoảng {shown} VNĐ mỗi USD). Bạn muốn thử tỉ giá cao hơn là bao nhiêu?"
        )
    return "exchangeRateVnd", number


# Kiểm tra một thay đổi, trả (khóa SimulateRequest, giá trị) hoặc None nếu mô phỏng chưa làm được
def _check(
    key: str,
    value: object,
    unit: str | None,
    states: list[state_catalog.UsState] | None = None,
    majors: list[major_catalog.Major] | None = None,
) -> tuple[str, Any] | None:
    unit = unit.strip().lower() if unit else None
    match key:
        case "state" | "preferred_states":
            return _check_states(value, states or [])
        case "major":
            return _check_major(value, majors or [])
        case "exchange_rate":
            return _check_exchange_rate(value, unit)
        case "ec_tier":
            tier = _number(value, "Bạn muốn thử bậc ngoại khóa mấy, từ 1 đến 5?")
            if tier != int(tier) or not 1 <= tier <= EC_TIER_MAX:
                raise AskAgain(f"Bậc ngoại khóa là số nguyên từ 1 đến {EC_TIER_MAX}. Bạn muốn thử bậc mấy?")
            return "ecTier", int(tier)
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
def merge_follow_up(entities: dict[str, Any], previous: dict[str, Any] | None) -> dict[str, Any]:
    """Gộp câu nối tiếp (follow_up) vào kịch bản lượt trước (UC-155 1a). Model chỉ nói phần ĐỔI, code gộp theo yếu tố:
    what_if: giá trị mới thay giá trị cũ của cùng yếu tố, yếu tố mới thì thêm, "bỏ" (action skip) thì xóa yếu tố đó.
    goal_seeking: trường hoặc nhóm đích còn thiếu thì lấy từ lượt trước. Không có lượt trước cùng loại thì giữ nguyên (mô phỏng mới)."""
    if entities.get("follow_up") is not True or not previous or previous.get("mode") != entities.get("mode"):
        return entities
    if entities.get("mode") == "goal_seeking":
        merged = dict(entities)
        for key in ("school", "target_group"):
            if not merged.get(key) and previous.get(key):
                merged[key] = previous[key]
        return merged
    kept: dict[str, dict[str, Any]] = {
        c["factor"]: c for c in previous.get("changes") or [] if c.get("factor")
    }
    for change in entities.get("changes") or []:
        factor = change.get("factor")
        if not factor:
            continue
        if change.get("action") == SKIP_ACTION:
            kept.pop(factor, None)
        else:
            kept[factor] = change
    return {**entities, "changes": list(kept.values())}


# states, majors: danh sách bang và ngành của hệ thống (đọc sẵn, ScenarioBranch nạp); thiếu thì bang và ngành chưa thử được
def parse_what_if(
    changes: list[dict[str, Any]],
    states: list[state_catalog.UsState] | None = None,
    majors: list[major_catalog.Major] | None = None,
) -> WhatIf:
    result = WhatIf()
    for change in changes:
        key = change.get("factor")
        if key == PRIORITY_KEY or str(key).startswith(
            "priority_"
        ):  # ưu tiên đọc riêng (priority.py), không phải giá trị hồ sơ
            continue
        checked = None
        if key in SUPPORTED and change.get("action") != SKIP_ACTION:
            checked = _check(key, change.get("value", ""), change.get("unit"), states, majors)
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
        # Tỉ giá nhập một cách thôi: câu sau thay câu trước (số VNĐ hay phần trăm)
        if name == "exchangeRateVnd":
            result.changes.pop("exchangeRatePercent", None)
        elif name == "exchangeRatePercent":
            result.changes.pop("exchangeRateVnd", None)
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
