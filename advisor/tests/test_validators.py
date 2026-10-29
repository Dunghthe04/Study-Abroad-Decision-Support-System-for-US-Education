from datetime import date

import pytest

from app.services.skills.validators import (
    AskAgain,
    budget_tier,
    check_act,
    check_activities,
    check_activity,
    check_award,
    check_awards,
    check_budget,
    check_budget_tier,
    check_english,
    check_english_plan,
    check_gpa,
    check_sat,
    check_scholarship,
    check_school_type,
    check_states,
    read_money,
)

# Ngày cố định để test không phụ thuộc hôm nay
TODAY = date(2026, 10, 28)

# A1. GPA


def test_gpa_above_4_is_scale_10():
    assert check_gpa(8.5) == (8.5, 10)


def test_gpa_up_to_4_is_scale_4():
    assert check_gpa(3.6) == (3.6, 4)


def test_gpa_exactly_4_is_scale_4():
    assert check_gpa(4) == (4, 4)


def test_gpa_with_given_scale():
    assert check_gpa(3.2, 10) == (3.2, 10)


def test_ask_again_carries_question():
    with pytest.raises(AskAgain) as err:
        check_gpa(85)
    assert "85" in err.value.question


def test_negative_gpa_ask_again():
    with pytest.raises(AskAgain) as err:
        check_gpa(-1)
    assert "âm" in err.value.question


def test_gpa_above_its_scale_ask_again():
    with pytest.raises(AskAgain) as err:
        check_gpa(9, 4)
    assert "thang 4" in err.value.question


def test_invalid_scale_ask_again():
    with pytest.raises(AskAgain) as err:
        check_gpa(3.5, 5)
    assert "thang 4 hoặc thang 10" in err.value.question


# A2. SAT, ACT


def test_sat_valid():
    assert check_sat(1450) == 1450


@pytest.mark.parametrize("score", [400, 1600])
def test_sat_edges(score):
    assert check_sat(score) == score


@pytest.mark.parametrize("score", [390, 1610, 1455])
def test_sat_wrong_ask_again(score):
    with pytest.raises(AskAgain) as err:
        check_sat(score)
    assert "SAT" in err.value.question


def test_act_valid():
    assert check_act(36) == 36


@pytest.mark.parametrize("score", [0, 37, 30.5])
def test_act_wrong_ask_again(score):
    with pytest.raises(AskAgain):
        check_act(score)


# A3. Tiếng Anh


def test_ielts_no_kind_needed():
    assert check_english("IELTS", 6.5) == ("ielts", 6.5, None)


def test_ielts_off_step_ask_again():
    with pytest.raises(AskAgain) as err:
        check_english("ielts", 6.3)
    assert "6.3" in err.value.question


def test_duolingo_step_5():
    assert check_english("duolingo", 120) == ("duolingo", 120, None)
    with pytest.raises(AskAgain):
        check_english("duolingo", 122)


def test_pte_below_min_ask_again():
    with pytest.raises(AskAgain):
        check_english("pte", 5)


def test_unknown_cert_ask_again():
    with pytest.raises(AskAgain) as err:
        check_english("toeic", 800)
    assert "IELTS, TOEFL" in err.value.question


def test_english_date_this_month_ok():
    assert check_english("toefl", 95, "2026-10", today=TODAY) == ("toefl", 95, "2026-10")


def test_english_date_in_future_ask_again():
    with pytest.raises(AskAgain) as err:
        check_english("toefl", 95, "2026-11", today=TODAY)
    assert "tương lai" in err.value.question


@pytest.mark.parametrize("wrong", ["2026-13", "10/2026", "sắp tới"])
def test_english_date_wrong_shape_ask_again(wrong):
    with pytest.raises(AskAgain):
        check_english("ielts", 7, wrong, today=TODAY)


# A3. Kế hoạch thi tiếng Anh: 3 lựa chọn giống form


def test_plan_none():
    assert check_english_plan(False) == {"planned": False, "date": None}


def test_plan_not_planned_drops_date():
    assert check_english_plan(False, "2027-03", today=TODAY) == {"planned": False, "date": None}


def test_plan_without_month():
    assert check_english_plan(True) == {"planned": True, "date": None}


def test_plan_with_month():
    assert check_english_plan(True, "2026-10", today=TODAY) == {"planned": True, "date": "2026-10"}


def test_plan_month_in_past_ask_again():
    with pytest.raises(AskAgain) as err:
        check_english_plan(True, "2026-09", today=TODAY)
    assert "quá khứ" in err.value.question


# A4. Ngân sách, học bổng


@pytest.mark.parametrize(
    ("usd", "tier"),
    [
        (15_000, "tier_under_20k"),
        (19_999, "tier_under_20k"),
        (20_000, "tier_20k_35k"),
        (34_999, "tier_20k_35k"),
        (35_000, "tier_35k_50k"),
        (69_999, "tier_50k_70k"),
        (70_000, "tier_above_70k"),
    ],
)
def test_budget_tier_edges(usd, tier):
    assert budget_tier(usd) == tier


def test_budget_usd():
    assert check_budget(40_000) == "tier_35k_50k"


def test_budget_vnd():
    # 800 triệu / 25.000 = 32.000 USD
    assert check_budget(800_000_000, "vnd") == "tier_20k_35k"


@pytest.mark.parametrize("amount", [0, -5_000])
def test_budget_not_positive_ask_again(amount):
    with pytest.raises(AskAgain):
        check_budget(amount)


def test_budget_huge_usd_ask_again():
    # Học sinh nói 800 triệu nhưng bị đọc nhầm là USD
    with pytest.raises(AskAgain) as err:
        check_budget(800_000_000, "USD")
    assert "USD hay VNĐ" in err.value.question


@pytest.mark.parametrize("currency", ["VNĐ", "vnđ", "đồng", "VND"])
def test_budget_vnd_spellings(currency):
    assert check_budget(800_000_000, currency) == "tier_20k_35k"


def test_budget_no_currency_big_number_is_vnd():
    # "1 tỷ" không nói đơn vị: 1.000.000.000 / 25.000 = 40.000 USD
    assert check_budget(1_000_000_000) == "tier_35k_50k"
    assert check_budget(40_000) == "tier_35k_50k"


def test_budget_unknown_currency_ask_again():
    with pytest.raises(AskAgain):
        check_budget(30_000, "EUR")


def test_budget_tier_button():
    assert check_budget_tier(" tier_50k_70k ") == "tier_50k_70k"
    with pytest.raises(AskAgain):
        check_budget_tier("tier_1m")


@pytest.mark.parametrize("level", ["mandatory", "preferred", "optional", " Mandatory "])
def test_scholarship_valid(level):
    assert check_scholarship(level) == level.strip().lower()


@pytest.mark.parametrize("level", [None, "", "required", "no"])
def test_scholarship_cannot_skip(level):
    with pytest.raises(AskAgain) as err:
        check_scholarship(level)
    assert "học bổng" in err.value.question


# Loại trường, bang


def test_school_type_form_codes():
    assert check_school_type(" Public ") == "public"
    with pytest.raises(AskAgain):
        check_school_type("any")


def test_states_max_60():
    assert check_states([" California ", ""]) == ["California"]
    with pytest.raises(AskAgain):
        check_states(["Texas"] * 61)


# A5. Hoạt động: đúng dạng một mục Bước 5 của form


def activity(**changes):
    item = {
        "name": "CLB Robotics",
        "role": "leader",
        "impact_level": "school",
        "start_date": "09/2024",
        "end_date": "05/2026",
        "is_ongoing": False,
    }
    return {**item, **changes}


def test_activity_valid():
    assert check_activity(**activity(name=" CLB Robotics ", role="Leader"), today=TODAY) == {
        "name": "CLB Robotics",
        "category": "activity",
        "role": "leader",
        "impactLevel": "school",
        "startDate": "09/2024",
        "endDate": "05/2026",
        "isOngoing": False,
    }


def test_activity_ongoing_drops_end_date():
    res = check_activity(**activity(end_date=None, is_ongoing=True), today=TODAY)
    assert (res["endDate"], res["isOngoing"]) == (None, True)


@pytest.mark.parametrize(
    ("changes", "asked"),
    [
        ({"name": "  "}, "tên"),
        ({"role": "lead"}, "sáng lập"),
        ({"impact_level": None}, "phạm vi"),
        ({"start_date": None}, "bắt đầu"),
        ({"end_date": None}, "đến nay"),
    ],
)
def test_activity_missing_part_asks_that_part(changes, asked):
    with pytest.raises(AskAgain) as err:
        check_activity(**activity(**changes), today=TODAY)
    assert asked in err.value.question


def test_activity_start_after_this_month_ask_again():
    with pytest.raises(AskAgain) as err:
        check_activity(**activity(start_date="11/2026", is_ongoing=True), today=TODAY)
    assert "hiện tại" in err.value.question


def test_activity_end_before_start_ask_again():
    with pytest.raises(AskAgain) as err:
        check_activity(**activity(start_date="09/2024", end_date="08/2024"), today=TODAY)
    assert "sau hoặc bằng" in err.value.question


def test_activity_same_start_and_end_ok():
    assert check_activity(**activity(start_date="09/2024", end_date="09/2024"), today=TODAY)


def test_activities_max_4():
    assert len(check_activities([activity()] * 4, today=TODAY)) == 4
    with pytest.raises(AskAgain) as err:
        check_activities([activity()] * 5, today=TODAY)
    assert "4" in err.value.question


# Giải thưởng (dùng ở A9)


def test_award_valid():
    assert check_award(" HSG Tin thành phố ", "province", 2025, today=TODAY) == {
        "name": "HSG Tin thành phố",
        "category": "achievement",
        "impactLevel": "province",
        "year": 2025,
    }


def test_award_wrong_impact_ask_again():
    with pytest.raises(AskAgain) as err:
        check_award("HSG Tin", "city", 2025, today=TODAY)
    assert "HSG Tin" in err.value.question


@pytest.mark.parametrize("year", [None, 1999, 2027])
def test_award_wrong_year_ask_again(year):
    with pytest.raises(AskAgain) as err:
        check_award("HSG Tin", "province", year, today=TODAY)
    assert "năm" in err.value.question


def test_awards_max_3():
    item = {"name": "HSG", "impact_level": "district", "year": 2024}
    assert len(check_awards([item] * 3, today=TODAY)) == 3
    with pytest.raises(AskAgain):
        check_awards([item] * 4, today=TODAY)


# Câu số tiền ở F1 đọc bằng code, không qua Qwen
@pytest.mark.parametrize(
    ("text", "money"),
    [
        ("800 triệu", (800_000_000, "VND")),
        ("khoảng 1,2 tỷ", (1_200_000_000, "VND")),
        ("900.000.000 đ", (900_000_000, "đ")),
        ("36.000 USD", (36_000, "USD")),
        ("40k/năm", (40_000, "USD")),
        ("50000", (50_000, None)),
    ],
)
def test_read_money(text, money):
    assert read_money(text) == money


@pytest.mark.parametrize("text", ["800", "em chưa biết", "8.5 thang 10"])
def test_read_money_other_text_is_none(text):
    assert read_money(text) is None


@pytest.mark.parametrize(
    ("text", "tier"),
    [
        ("800 triệu", "tier_20k_35k"),
        ("1,2 tỷ", "tier_35k_50k"),
        ("900.000.000 đ", "tier_35k_50k"),
        ("36.000 USD", "tier_35k_50k"),
    ],
)
def test_money_text_maps_to_form_tier(text, tier):
    assert check_budget(*read_money(text)) == tier
