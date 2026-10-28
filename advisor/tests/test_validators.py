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
    check_english,
    check_gpa,
    check_sat,
    check_scholarship,
)

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


def test_ielts_academic():
    assert check_english("IELTS", 6.5, "academic") == ("ielts", 6.5, "academic")


def test_ielts_off_step_ask_again():
    with pytest.raises(AskAgain) as err:
        check_english("ielts", 6.3, "academic")
    assert "6.3" in err.value.question


def test_ielts_without_kind_ask_again():
    with pytest.raises(AskAgain) as err:
        check_english("ielts", 7)
    assert "Academic hay General" in err.value.question


def test_toefl_no_kind_needed():
    assert check_english("toefl", 95) == ("toefl", 95, None)


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


# A4. Ngân sách, học bổng


@pytest.mark.parametrize(
    ("usd", "tier"),
    [
        (15_000, "tier_under_20k"),
        (20_000, "tier_under_20k"),
        (20_001, "tier_20k_35k"),
        (35_000, "tier_20k_35k"),
        (50_000, "tier_35k_50k"),
        (70_000, "tier_50k_70k"),
        (70_001, "tier_above_70k"),
    ],
)
def test_budget_tier_edges(usd, tier):
    assert budget_tier(usd) == tier


def test_budget_usd():
    assert check_budget(40_000) == (40_000, "USD", "tier_35k_50k")


def test_budget_vnd_keeps_original_amount():
    # 800 triệu / 25.000 = 32.000 USD
    assert check_budget(800_000_000, "vnd") == (800_000_000, "VND", "tier_20k_35k")


@pytest.mark.parametrize("amount", [0, -5_000])
def test_budget_not_positive_ask_again(amount):
    with pytest.raises(AskAgain):
        check_budget(amount)


def test_budget_huge_usd_ask_again():
    # Học sinh nói 800 triệu nhưng bị đọc nhầm là USD
    with pytest.raises(AskAgain) as err:
        check_budget(800_000_000, "USD")
    assert "USD hay VNĐ" in err.value.question


def test_budget_unknown_currency_ask_again():
    with pytest.raises(AskAgain):
        check_budget(30_000, "EUR")


@pytest.mark.parametrize("level", ["required", "preferred", "no", " Required "])
def test_scholarship_valid(level):
    assert check_scholarship(level) == level.strip().lower()


@pytest.mark.parametrize("level", [None, "", "skip", "unknown"])
def test_scholarship_cannot_skip(level):
    with pytest.raises(AskAgain) as err:
        check_scholarship(level)
    assert "học bổng" in err.value.question


# A5. Hoạt động


def test_activity_valid():
    assert check_activity(" CLB Robotics ", "Lead", 3, 18) == {
        "name": "CLB Robotics",
        "role": "lead",
        "scope": 3,
        "months": 18,
    }


def test_activity_wrong_role_ask_again():
    with pytest.raises(AskAgain) as err:
        check_activity("CLB Robotics", "boss", 3, 18)
    assert "CLB Robotics" in err.value.question


@pytest.mark.parametrize("scope", [0, 6])
def test_activity_wrong_scope_ask_again(scope):
    with pytest.raises(AskAgain):
        check_activity("CLB Robotics", "member", scope, 18)


def test_activity_zero_months_ask_again():
    with pytest.raises(AskAgain):
        check_activity("CLB Robotics", "member", 1, 0)


def test_activity_empty_name_ask_again():
    with pytest.raises(AskAgain):
        check_activity("  ", "member", 1, 6)


def test_activities_max_4():
    item = {"name": "CLB", "role": "member", "scope": 1, "months": 6}
    assert len(check_activities([item] * 4)) == 4
    with pytest.raises(AskAgain) as err:
        check_activities([item] * 5)
    assert "4" in err.value.question


# Giải thưởng (dùng ở A9)


def test_award_valid():
    assert check_award(" HSG Tin thành phố ", 3) == {"title": "HSG Tin thành phố", "level": 3}


@pytest.mark.parametrize("level", [0, 6])
def test_award_wrong_level_ask_again(level):
    with pytest.raises(AskAgain) as err:
        check_award("HSG Tin", level)
    assert "HSG Tin" in err.value.question


def test_awards_max_3():
    item = {"title": "HSG", "level": 2}
    assert len(check_awards([item] * 3)) == 3
    with pytest.raises(AskAgain):
        check_awards([item] * 4)
