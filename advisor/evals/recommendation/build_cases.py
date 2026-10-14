"""Tạo bộ đánh giá cho lượt ② (chấm trường): hồ sơ mẫu × trường thật, kèm đáp án.

Chạy:  python -m evals.recommendation.build_cases <file Excel của Dương>
Ra:    evals/recommendation/cases.json

Trường được đổi sang JSON gọn giống hệt .NET sẽ gửi cho advisor (mục 4 file AI_CACH_THUC_HIEN),
nên cases.json đưa thẳng vào endpoint /recommend/score được.
"""

import itertools
import json
import random
import re
import sys
from pathlib import Path

from evals.recommendation.reference import score_school

OUT = Path(__file__).with_name("cases.json")
SCHOOLS_PER_CASE = 8   # bằng cỡ một lô gửi AI
SEED = 42

# Lưới hồ sơ mẫu: 3 mức GPA × 3 mức SAT × 3 bậc ngoại khóa = 27 hồ sơ
GPAS = [3.2, 3.6, 3.9]
SATS = [None, 1250, 1450]      # None = chưa thi SAT
EC_TIERS = [1, 3, 5]
BUDGETS = [30000, 50000, 80000]  # xoay vòng theo hồ sơ để F, H, gap có đủ trường hợp

C7 = {"very_important": 3, "important": 2, "considered": 1, "not_considered": 0}
SAT_POLICY = {"required": "req", "optional": "opt", "not_accepted": "no"}
# 7 khoảng GPA từ thấp lên cao, đúng thứ tự k1..k7 trong skill
GPA_KEYS = ["gpa_pct_below_250", "gpa_pct_250_299", "gpa_pct_300_324", "gpa_pct_325_349",
            "gpa_pct_350_374", "gpa_pct_375_399", "gpa_pct_4_0"]


def compact_school(d):
    """Một dòng Excel → JSON gọn. Thiếu số thì bỏ hẳn key. C7 thiếu thì điền mặc định (việc của .NET)."""
    selective = d.get("control") == "private" or (d.get("acceptance_rate") or 1) < 0.5
    default = (3, 2, 2) if selective else (2, 2, 1)
    levels = [C7.get(d.get(k)) for k in ("c7_gpa_importance", "c7_test_importance", "c7_ec_importance")]
    rates = [x for x in (d.get("retention_rate_1yr"), d.get("grad_rate_6yr")) if x is not None]
    gpa = [d.get(k) for k in GPA_KEYS]
    s = {
        "c": str(d["code"]),
        "acc": d.get("acceptance_rate"),
        "iacc": d.get("intl_acceptance_rate"),
        "sat": [d["sat25"], d["sat75"]] if d.get("sat25") and d.get("sat75") and d["sat25"] < d["sat75"] else None,
        "subm": d.get("sat_submit_pct"),
        "satp": SAT_POLICY.get(d.get("sat_policy")),
        "gpa": gpa if None not in gpa else None,
        "avg": d.get("avg_gpa4"),
        "L": [lv if lv is not None else df for lv, df in zip(levels, default, strict=True)],
        "coa": d.get("coa_total_usd"),
        "aid": d.get("intl_aid_avg_usd"),
        "q": round(sum(rates) / len(rates), 3) if rates else None,
    }
    return {k: v for k, v in s.items() if v is not None}


def load_schools(xlsx_path):
    """Đọc sheet Schools. Tiêu đề cột dạng 'Nhãn tiếng Việt\\n[khóa]' nên lấy phần trong []."""
    import openpyxl

    wb = openpyxl.load_workbook(xlsx_path, read_only=True, data_only=True)
    rows = list(wb["Schools"].iter_rows(values_only=True))
    keys = [re.search(r"\[(.+)\]", str(h)).group(1) if h and "[" in str(h) else h for h in rows[0]]
    schools = []
    for row in rows[1:]:
        d = dict(zip(keys, row, strict=False))
        if not d.get("code") or d.get("is_active") is False or d.get("study_level") != "undergraduate":
            continue
        schools.append(compact_school(d))
    return schools


def build(xlsx_path):
    schools = sorted(load_schools(xlsx_path), key=lambda s: s["c"])
    rng = random.Random(SEED)
    cases = []
    for i, (gpa, sat, tier) in enumerate(itertools.product(GPAS, SATS, EC_TIERS), start=1):
        student = {"gpa": gpa, "sat": sat, "budget": BUDGETS[i % 3], "tier": tier}
        student = {k: v for k, v in student.items() if v is not None}
        sample = rng.sample(schools, SCHOOLS_PER_CASE)
        cases.append({
            "id": f"P{i:02d}",
            "student": student,
            "schools": sample,
            "expected": [score_school(student, s) for s in sample],
        })
    return {"data_version": Path(xlsx_path).name, "seed": SEED, "cases": cases}


if __name__ == "__main__":
    result = build(sys.argv[1])
    OUT.write_text(json.dumps(result, ensure_ascii=False, indent=1), encoding="utf-8")
    groups = [e["g2"] for c in result["cases"] for e in c["expected"]]
    print(f"{len(result['cases'])} hồ sơ, {len(groups)} cặp hồ sơ–trường -> {OUT}")
    for g in ("R", "RF", "M", "S", "NA", "O"):
        print(f"  {g}: {groups.count(g)}")
