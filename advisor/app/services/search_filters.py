"""Điều kiện tìm trường (filters của SLM) → bộ lọc đã chuẩn hóa cho .NET.

SLM trả chữ học sinh gõ ("texas", "30 nghìn đô", "top 50", "khoa học máy tính"). Ở đây đổi sang dạng .NET lọc được:
mã bang (danh mục 51 bang), tên ngành trong danh mục, số USD, tỉ lệ 0..1, hạng. Điều nào không đọc chắc chắn được thì KHÔNG bỏ qua
lặng lẽ (danh sách sẽ sai ý học sinh) mà ghi vào unresolved để hệ thống nói rõ và nhờ học sinh nói lại.
Khóa đầu ra là camelCase, khớp SchoolSearchFilters bên .NET.
"""

import re
import unicodedata
from dataclasses import dataclass, field
from typing import Any

from app.services.skills import exchange_rate, major_catalog, state_catalog
from app.services.skills.validators import MONEY_UNITS, VND_GUESS_FROM, read_money, to_usd

# Mức điểm hợp lệ của từng bài tiếng Anh (khớp kiểm tra của hồ sơ)
ENGLISH_RANGES = {"max_ielts": (0.0, 9.0), "max_toefl": (0.0, 120.0), "max_duolingo": (10.0, 160.0)}
ENGLISH_KEYS = {"max_ielts": "maxIelts", "max_toefl": "maxToefl", "max_duolingo": "maxDuolingo"}
# Học bổng: yes là có học bổng cho du học sinh (bất kể loại), .NET gọi là any
AID = {"yes": "any", "need_based": "need", "merit_based": "merit"}
# Cách xếp học sinh muốn → tên cách xếp bên .NET
SORT = {
    "ranking": "quality",
    "lowest_cost": "lowestCost",
    "highest_cost": "highestCost",
    "lowest_tuition": "lowestTuition",
    "highest_tuition": "highestTuition",
    "easiest_admission": "easiestAdmission",
    "most_selective": "mostSelective",
    "most_aid": "mostAid",
}
DEFAULT_SORT = "quality"
# Các cách xếp theo tiền: model trả lowest_cost/highest_cost, resolve_cost_sort có thể đổi sang mã học phí
COST_SORTS = ("lowest_cost", "highest_cost", "lowest_tuition", "highest_tuition")
NO_VALUE = {"", "none", "null", "n/a", "na", "unknown", "không", "khong", "-"}
# Tên điều kiện nói với học sinh khi cần hỏi lại
LABELS = {
    "state": "bang",
    "major": "ngành",
    "max_cost": "mức chi phí",
    "max_ielts": "mức IELTS",
    "max_toefl": "mức TOEFL",
    "max_duolingo": "mức Duolingo",
    "min_acceptance_rate": "tỷ lệ nhận",
    "max_acceptance_rate": "tỷ lệ nhận",
    "max_rank": "thứ hạng",
    "min_graduation_rate": "tỷ lệ tốt nghiệp",
}


@dataclass
class SearchPlan:
    filters: dict[str, Any] = field(default_factory=dict)
    sort: str = DEFAULT_SORT
    unresolved: list[str] = field(default_factory=list)  # điều học sinh nói mà chưa đọc chắc chắn được


_NUMBER = re.compile(r"\d+(?:[.,]\d+)?")


def _number(text: str) -> float | None:
    found = _NUMBER.search(text)
    return float(found[0].replace(",", ".")) if found else None


# "50%", "50 phần trăm", "0,5", "tỷ lệ 30": tỉ lệ 0..1. Số lớn hơn 1 hoặc có dấu % là phần trăm. Ngoài (0, 1] thì None
def read_rate(text: str) -> float | None:
    value = _number(text)
    if value is None:
        return None
    rate = value / 100 if value > 1 or "%" in text or "phần trăm" in text.lower() else value
    return rate if 0 < rate <= 1 else None


# "top 50", "50", "trong 20 trường đầu": hạng nguyên dương
def read_rank(text: str) -> int | None:
    value = _number(text)
    return int(value) if value is not None and value >= 1 and value == int(value) else None


# "30 nghìn đô", "30.000 USD", "40k", "800 triệu": USD mỗi năm. Số trần "30000" đoán USD (800 triệu mới là VNĐ)
def read_cost_usd(text: str) -> float | None:
    parsed = read_money(text)
    if parsed is None:
        bare = _number(text)
        if bare is None or bare < 1000:
            return None
        parsed = (bare, None)
    amount, currency = parsed
    currency = currency or ("VND" if amount >= VND_GUESS_FROM else "USD")
    currency = currency.strip().upper()
    if currency in ("ĐÔ", "$", "US$"):
        currency = "USD"
    elif currency in ("VNĐ", "ĐỒNG", "Đ"):
        currency = "VND"
    if currency not in ("USD", "VND") or amount <= 0:
        return None
    return round(to_usd(amount, currency))


def _fold(text: str) -> str:
    text = unicodedata.normalize("NFD", text.lower().replace("đ", "d"))
    return "".join(c for c in text if unicodedata.category(c) != "Mn")


# Model chỉ có một nhãn cho "chi phí/học phí cao nhất, rẻ nhất" (highest_cost, lowest_cost). Hai thứ khác nhau:
# học phí (chỉ tiền học) và tổng chi phí (cộng ăn ở, sinh hoạt). Học sinh nói rõ "học phí" (và không nhắc tổng, ăn ở) thì xếp theo học phí.
# Còn lại (nói "tổng", "ăn ở", "chi phí" hay chỉ nói "đắt nhất", "rẻ nhất") xếp theo tổng chi phí: đó là số tiền người ta thật sự phải chuẩn bị.
def resolve_cost_sort(sort: str | None, text: str) -> str | None:
    if sort not in ("lowest_cost", "highest_cost"):
        return sort
    folded = _fold(text)
    says_total = any(key in folded for key in ("tong chi", "tong cong", "an o", "sinh hoat"))
    if "hoc phi" in folded and not says_total:
        return "lowest_tuition" if sort == "lowest_cost" else "highest_tuition"
    return sort


async def normalize(filters: dict[str, Any], sort: str | None = None) -> SearchPlan:
    plan = SearchPlan(sort=SORT.get(sort or "", DEFAULT_SORT))
    out = plan.filters
    # Model đôi khi điền chữ giữ chỗ ("none", "null"...) vào điều kiện học sinh không nói: coi như không có điều kiện đó
    filters = {k: v for k, v in filters.items() if not (isinstance(v, str) and v.strip().lower() in NO_VALUE)}
    # "học phí cao/thấp nhất" là cách xếp, không phải mức chi phí: chữ không đọc ra số tiền thì bỏ, đừng hỏi lại học sinh.
    # sort ở đây đã qua resolve_cost_sort nên có thể là mã học phí (lowest_tuition...) chứ không chỉ mã tổng chi phí
    if (
        sort in COST_SORTS
        and isinstance(filters.get("max_cost"), str)
        and read_cost_usd(filters["max_cost"]) is None
    ):
        filters.pop("max_cost")

    def fail(key: str, text: str) -> None:
        plan.unresolved.append(f"{LABELS.get(key, key)} “{text}”")

    if names := filters.get("state"):
        names = [names] if isinstance(names, str) else names  # model trả một tên hoặc danh sách tên
        states = await state_catalog.states()
        codes = []
        for name in names:
            found = state_catalog.match(name, states) if states else None
            if found is None or found.exact is None:
                fail("state", name)
            elif found.exact.code not in codes:
                codes.append(found.exact.code)
        if codes:
            out["states"] = codes
    if city := filters.get("city"):
        out["city"] = city.strip()
    if text := filters.get("major"):
        found = major_catalog.match(text, await major_catalog.majors())
        major = found.exact or (found.near[0] if len(found.near) == 1 else None)
        if major is None:
            fail("major", text)
        else:
            out["major"] = major.name
    if kind := filters.get("school_type"):
        out["control"] = kind  # public | private, trùng mã bên .NET
    if kind := filters.get("aid"):
        out["aid"] = AID[kind]
    if kind := filters.get("test_policy"):
        out["testPolicy"] = kind  # optional | required, trùng mã bên .NET
    if text := filters.get("max_cost"):
        cost = read_cost_usd(text)
        if cost is None:
            fail("max_cost", text)
        else:
            out["maxCostUsd"] = cost
    for key, (low, high) in ENGLISH_RANGES.items():
        if text := filters.get(key):
            score = _number(text)
            if score is None or not low <= score <= high:
                fail(key, text)
            else:
                out[ENGLISH_KEYS[key]] = score
    for key, target in (("min_acceptance_rate", "minAcceptRate"), ("max_acceptance_rate", "maxAcceptRate"),
                        ("min_graduation_rate", "minGradRate")):  # fmt: skip
        if text := filters.get(key):
            rate = read_rate(text)
            if rate is None:
                fail(key, text)
            else:
                out[target] = rate
    if text := filters.get("max_rank"):
        rank = read_rank(text)
        if rank is None:
            fail("max_rank", text)
        else:
            out["maxRank"] = rank
    if filters.get("ivy"):
        out["ivy"] = True
    return plan


__all__ = ["MONEY_UNITS", "SearchPlan", "exchange_rate", "normalize"]
