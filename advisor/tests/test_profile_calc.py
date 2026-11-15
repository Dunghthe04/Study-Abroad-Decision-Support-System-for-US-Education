"""Bước 4: các dòng chữ tính sẵn cho skill ho_so. Hàm thuần, cố định ngày hôm nay."""

from datetime import date

from app.schemas.chat import ProfileField
from app.services.profile_calc import calc_profile
from tests.vef import VEF

TODAY = date(2026, 11, 5)
MARKS = {"gpa4": 3.6, "english": {"ielts": 6.5, "toefl": 80, "duolingo": 110}}


def prof(**values) -> dict[str, ProfileField]:
    return {k: ProfileField(value=v, status="confirmed") for k, v in values.items()}


def years(*items) -> dict:
    return {"years": [{"grade": grade, "average": avg} for grade, avg in items]}


def by_year(*items) -> str | None:
    return calc_profile({}, years(*items), TODAY).get("grade_by_year")


def test_empty_profile_gives_nothing():
    assert calc_profile({}, None, TODAY) == {}


def test_levels_text():
    out = calc_profile(
        prof(
            gpa={"value": 8, "scale": 10},
            english={"type": "ielts", "value": 6.5},
            sat=1250,
            budget="tier_35k_50k",
        ),
        None,
        TODAY,
        benchmarks=MARKS,
        gpa_table=VEF,
    )
    assert out["gpa_level"] == "3.5 thang 4 (quy đổi tham khảo), trung bình (TB các trường 3.6)"
    assert out["english_level"] == "IELTS 6.5, trung bình (mốc phổ biến 6.5)"
    assert out["sat_level"] == "1250, mức trung bình"
    assert out["budget_level"] == "35.000 – 50.000 USD mỗi năm, mức sát"
    assert out["scholarship_chance"] == "cao"  # 8/10 → 3.5 theo mốc VEF là học lực mạnh


def english(kind: str, score: float, marks: dict | None = MARKS) -> str:
    return calc_profile(prof(english={"type": kind, "value": score}), None, TODAY, benchmarks=marks)[
        "english_level"
    ]


def gpa(value: float, marks: dict | None = MARKS) -> str:
    return calc_profile(prof(gpa={"value": value, "scale": 4}), None, TODAY, benchmarks=marks)["gpa_level"]


def test_english_compares_with_own_certificate_mark():
    assert english("toefl", 85) == "TOEFL 85, cao (mốc phổ biến 80)"
    assert english("duolingo", 100) == "Duolingo 100, thấp (mốc phổ biến 110)"
    assert english("ielts", 6.5) == "IELTS 6.5, trung bình (mốc phổ biến 6.5)"


def test_english_without_mark_is_score_only():
    assert english("pte", 60) == "PTE 60"
    assert english("ielts", 7, marks=None) == "IELTS 7"


def test_gpa_near_mark_is_medium():
    assert gpa(3.7) == "3.7 thang 4, trung bình (TB các trường 3.6)"
    assert gpa(3.5) == "3.5 thang 4, trung bình (TB các trường 3.6)"
    assert gpa(3.75) == "3.75 thang 4, cao (TB các trường 3.6)"
    assert gpa(3.45) == "3.45 thang 4, thấp (TB các trường 3.6)"


def test_gpa_without_mark_is_score_only():
    assert gpa(3.2, marks=None) == "3.2 thang 4"


def test_activities_give_ec_tier():
    activity = {"role": "leader", "impactLevel": "province", "startDate": "09/2024", "isOngoing": True}
    out = calc_profile(prof(activities=[activity]), None, TODAY)
    assert out["ec_level"].startswith("bậc ") and out["ec_level"].endswith("/5")


def test_middle_school_gpa_is_target():
    out = calc_profile(
        prof(current_level="middle_school", gpa={"value": 9, "scale": 10}),
        None,
        TODAY,
        benchmarks=MARKS,
        gpa_table=VEF,
    )
    assert out["gpa_level"] == "mục tiêu 3.75 thang 4 (quy đổi tham khảo), cao (TB các trường 3.6)"


def test_grade_by_year_lists_scores_without_judging():
    assert (
        by_year(("Lớp 10", 7.5), ("Lớp 11", 8.2), ("Lớp 12", 7.6)) == "Lớp 10: 7.5; Lớp 11: 8.2; Lớp 12: 7.6"
    )
    assert by_year(("Lớp 10", 8)) == "Lớp 10: 8"


def test_grade_by_year_missing_when_target_only():
    target_only = {"years": [{"grade": "Lớp 9", "target": 8.5}]}
    assert "grade_by_year" not in calc_profile({}, target_only, TODAY)


def test_timeline_without_deadlines_is_months_only():
    out = calc_profile(
        prof(intake="Fall 2027", english_plan={"planned": True, "date": "2027-05"}), None, TODAY
    )
    assert out["timeline"] == "hôm nay 11/2026, nhập học kỳ Fall 08/2027, còn 9 tháng"


def test_timeline_term_start_months():
    assert (
        "kỳ Spring 01/2027, còn 2 tháng" in calc_profile(prof(intake="Spring 2027"), None, TODAY)["timeline"]
    )
    assert (
        "kỳ Summer 05/2027, còn 6 tháng" in calc_profile(prof(intake="Summer 2027"), None, TODAY)["timeline"]
    )


def test_timeline_with_school_deadlines_and_plans():
    deadlines = [{"RD": "2027-02-01"}, {"EA": "2026-11-01", "RD": "2027-01-15"}, {"note": 3}]
    out = calc_profile(
        prof(
            intake="Fall 2027",
            english_plan={"planned": True, "date": "2027-05", "test_type": "ielts"},
            standardized_plan={"planned": True, "date": "2026-12", "test_type": "SAT"},
        ),
        None,
        TODAY,
        deadlines,
    )
    assert out["timeline"] == (
        "hôm nay 11/2026, nhập học kỳ Fall 08/2027, còn 9 tháng; "
        "hạn nộp các trường đã gợi ý: sớm nhất 11/2026 (EA), muộn nhất 02/2027 (RD); "
        "kế hoạch thi IELTS 05/2027 là sau hạn nộp muộn nhất; "
        "kế hoạch thi SAT 12/2026 là trước hạn nộp muộn nhất"
    )


def test_timeline_past_intake_or_missing():
    assert calc_profile(prof(intake="Fall 2026"), None, TODAY)["timeline"] == (
        "hôm nay 11/2026, nhập học kỳ Fall 08/2026, kỳ nhập học đã qua"
    )
    assert "timeline" not in calc_profile(prof(gpa={"value": 8, "scale": 10}), None, TODAY)
