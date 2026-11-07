"""Điều kiện tìm trường (bo_loc của SLM) → bộ lọc đã chuẩn hóa cho .NET.

SLM trả chữ học sinh gõ ("texas", "30 nghìn đô", "top 50", "khoa học máy tính"). Ở đây đổi sang dạng .NET lọc được:
mã bang (danh mục 51 bang), tên ngành trong danh mục, số USD, tỉ lệ 0..1, hạng. Điều nào không đọc chắc chắn được thì KHÔNG bỏ qua
lặng lẽ (danh sách sẽ sai ý học sinh) mà ghi vào unresolved để hệ thống nói rõ và nhờ học sinh nói lại.
Khóa đầu ra là camelCase, khớp SchoolSearchFilters bên .NET.
"""

import re
from dataclasses import dataclass, field
from typing import Any

from app.services.skills import exchange_rate, major_catalog, state_catalog
from app.services.skills.validators import MONEY_UNITS, VND_GUESS_FROM, read_money, to_usd

# Mức điểm hợp lệ của từng bài tiếng Anh (khớp kiểm tra của hồ sơ)
ENGLISH_RANGES = {"ielts_toi_da": (0.0, 9.0), "toefl_toi_da": (0.0, 120.0), "duolingo_toi_da": (10.0, 160.0)}
ENGLISH_KEYS = {"ielts_toi_da": "maxIelts", "toefl_toi_da": "maxToefl", "duolingo_toi_da": "maxDuolingo"}
CONTROL = {"cong_lap": "public", "tu_thuc": "private"}
AID = {"co": "any", "theo_nhu_cau": "need", "theo_thanh_tich": "merit"}
TEST_POLICY = {"khong_bat_buoc": "optional", "bat_buoc": "required"}
# Cách xếp học sinh muốn → tên cách xếp bên .NET
SORT = {
    "xep_hang": "quality",
    "chi_phi_thap": "lowestCost",
    "de_vao": "easiestAdmission",
    "chon_loc": "mostSelective",
}
DEFAULT_SORT = "quality"
# Tên điều kiện nói với học sinh khi cần hỏi lại
LABELS = {
    "bang": "bang",
    "nganh": "ngành",
    "chi_phi_toi_da": "mức chi phí",
    "ielts_toi_da": "mức IELTS",
    "toefl_toi_da": "mức TOEFL",
    "duolingo_toi_da": "mức Duolingo",
    "ty_le_nhan_toi_thieu": "tỷ lệ nhận",
    "ty_le_nhan_toi_da": "tỷ lệ nhận",
    "xep_hang_toi_da": "thứ hạng",
    "ty_le_tot_nghiep_toi_thieu": "tỷ lệ tốt nghiệp",
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


async def normalize(bo_loc: dict[str, Any], sap_xep: str | None = None) -> SearchPlan:
    plan = SearchPlan(sort=SORT.get(sap_xep or "", DEFAULT_SORT))
    out = plan.filters

    def fail(key: str, text: str) -> None:
        plan.unresolved.append(f"{LABELS.get(key, key)} “{text}”")

    if names := bo_loc.get("bang"):
        states = await state_catalog.states()
        codes = []
        for name in names:
            found = state_catalog.match(name, states) if states else None
            if found is None or found.exact is None:
                fail("bang", name)
            elif found.exact.code not in codes:
                codes.append(found.exact.code)
        if codes:
            out["states"] = codes
    if city := bo_loc.get("thanh_pho"):
        out["city"] = city.strip()
    if text := bo_loc.get("nganh"):
        found = major_catalog.match(text, await major_catalog.majors())
        major = found.exact or (found.near[0] if len(found.near) == 1 else None)
        if major is None:
            fail("nganh", text)
        else:
            out["major"] = major.name
    if kind := bo_loc.get("loai_truong"):
        out["control"] = CONTROL[kind]
    if kind := bo_loc.get("hoc_bong"):
        out["aid"] = AID[kind]
    if kind := bo_loc.get("chinh_sach_thi"):
        out["testPolicy"] = TEST_POLICY[kind]
    if text := bo_loc.get("chi_phi_toi_da"):
        cost = read_cost_usd(text)
        if cost is None:
            fail("chi_phi_toi_da", text)
        else:
            out["maxCostUsd"] = cost
    for key, (low, high) in ENGLISH_RANGES.items():
        if text := bo_loc.get(key):
            score = _number(text)
            if score is None or not low <= score <= high:
                fail(key, text)
            else:
                out[ENGLISH_KEYS[key]] = score
    for key, target in (("ty_le_nhan_toi_thieu", "minAcceptRate"), ("ty_le_nhan_toi_da", "maxAcceptRate"),
                        ("ty_le_tot_nghiep_toi_thieu", "minGradRate")):  # fmt: skip
        if text := bo_loc.get(key):
            rate = read_rate(text)
            if rate is None:
                fail(key, text)
            else:
                out[target] = rate
    if text := bo_loc.get("xep_hang_toi_da"):
        rank = read_rank(text)
        if rank is None:
            fail("xep_hang_toi_da", text)
        else:
            out["maxRank"] = rank
    if bo_loc.get("ivy"):
        out["ivy"] = True
    return plan


__all__ = ["MONEY_UNITS", "SearchPlan", "exchange_rate", "normalize"]
