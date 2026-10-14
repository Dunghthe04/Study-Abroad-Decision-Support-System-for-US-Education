"""Kiểm tra công thức đáp án bằng ví dụ trong GOI_Y_TRUONG_TONG_QUAN và GOI_Y_TRUONG_AI_CACH_THUC_HIEN."""

import pytest

from evals.recommendation.build_cases import compact_school
from evals.recommendation.reference import score_school

AN = {"gpa": 3.6, "sat": 1350, "budget": 50000, "tier": 3}
# 3 trường đã chuẩn hóa, đúng như mục 4 file AI_CACH_THUC_HIEN
COLGATE = {"c": "190099", "acc": 0.175, "iacc": 0.044, "sat": [1450, 1510], "subm": 0.23, "satp": "opt",
           "gpa": [0, 0, 0.02, 0.06, 0.23, 0.38, 0.31], "avg": 3.85, "L": [3, 2, 2],
           "coa": 95610, "aid": 86649, "q": 0.905}
SCHOOL_B = {"c": "B", "acc": 0.60, "sat": [1100, 1300], "subm": 1.0, "satp": "req",
            "gpa": [0.02, 0.08, 0.20, 0.28, 0.30, 0.08, 0.04], "L": [2, 2, 0],
            "coa": 38000, "aid": 4000, "q": 0.80}
SCHOOL_C = {"c": "C", "acc": 0.45, "sat": [1250, 1400], "subm": 1.0, "satp": "req",
            "gpa": [0.01, 0.05, 0.14, 0.20, 0.25, 0.20, 0.15], "L": [3, 2, 1],
            "coa": 46000, "aid": 12000, "q": 0.90}


def test_an_matches_document_output_for_all_three_schools():
    # Đầu ra mẫu trong mục 6 file AI_CACH_THUC_HIEN
    assert score_school(AN, COLGATE) == {
        "c": "190099", "w": [0.75, 0.25], "pg": 17.2, "conf": "H", "ps": 0, "A": 12.9, "adj": 0, "S": 12.9,
        "g": "R", "r": 0.25, "g2": "RF", "F": 0, "H": 0.906, "Q": 0.905, "gap": 45610}
    assert score_school(AN, SCHOOL_B) == {
        "c": "B", "w": [0.5, 0.5], "pg": 70, "conf": "H", "ps": 87.5, "A": 78.75, "adj": 0, "S": 78.75,
        "g": "S", "r": None, "g2": "S", "F": 0.24, "H": 0.105, "Q": 0.8, "gap": None}
    assert score_school(AN, SCHOOL_C) == {
        "c": "C", "w": [0.6, 0.4], "pg": 50, "conf": "H", "ps": 58.33, "A": 53.33, "adj": 0, "S": 53.33,
        "g": "M", "r": None, "g2": "M", "F": 0.08, "H": 0.261, "Q": 0.9, "gap": None}


def test_binh_at_colgate_is_downgraded_to_reach():
    r = score_school({"gpa": 3.9, "sat": 1480, "budget": 50000, "tier": 2}, COLGATE)
    assert r["ps"] == pytest.approx(50)
    assert r["pg"] == pytest.approx(53.8)
    assert r["adj"] == -2
    assert r["S"] == pytest.approx(50.85)
    assert (r["g"], r["g2"]) == ("M", "R")       # Vừa sức hạ thành Khó vào


def test_gpa_average_only_uses_z_table_and_low_confidence():
    school = {k: v for k, v in COLGATE.items() if k != "gpa"}   # chỉ còn avg 3.85
    r = score_school(AN, school)
    # z = (3.6 − 3.85) / 0.3 = −0.833 → nội suy giữa (−1, 16) và (−0.5, 31) = 21
    assert r["pg"] == pytest.approx(21, abs=0.01)
    assert r["conf"] == "L"


def test_gpa_exactly_four_sits_in_middle_of_top_band():
    r = score_school({**AN, "gpa": 4.0}, COLGATE)
    assert r["pg"] == pytest.approx(69 + 15.5)    # 100 × 0.69 + 50 × 0.31


def test_student_without_sat_uses_gpa_only():
    r = score_school({"gpa": 3.9, "budget": 50000, "tier": 3}, COLGATE)
    assert r["w"] == [1, 0] and r["ps"] is None
    assert r["S"] == pytest.approx(53.8)


def test_ec_tier_5_adds_two_times_l():
    assert score_school({**AN, "tier": 5}, COLGATE)["adj"] == 4      # (5 − 3) × 2


def test_no_gpa_and_no_sat_data_is_na():
    r = score_school(AN, {"c": "X", "acc": 0.5, "L": [3, 2, 1]})
    assert (r["S"], r["g"], r["g2"]) == (None, "NA", "NA")


def test_open_admission_is_group_o():
    r = score_school(AN, {**SCHOOL_B, "acc": 1.0})
    assert r["g"] == r["g2"] == "O"


def test_compact_school_drops_missing_values_and_fills_c7_default():
    row = {"code": 12345, "control": "public", "acceptance_rate": 0.8, "sat25": 1100, "sat75": 1300,
           "sat_policy": "optional", "intl_acceptance_rate": None, "retention_rate_1yr": 0.9,
           "grad_rate_6yr": 0.7}
    s = compact_school(row)
    assert s == {"c": "12345", "acc": 0.8, "sat": [1100, 1300], "satp": "opt", "L": [2, 2, 1], "q": 0.8}
