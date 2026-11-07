"""Kịch bản what-if: kiểm tra giá trị SLM đã chuẩn hóa và đổi sang khóa của API simulate."""

import pytest

from app.services.scenario import SKIP_ACTION, parse_what_if
from app.services.skills import exchange_rate
from app.services.skills.validators import AskAgain


def change(key: str, value: str | None = None, unit: str | None = None, **extra) -> dict[str, str]:
    out = {"yeu_to": key, **extra}
    if value is not None:
        out["gia_tri"] = value
    if unit is not None:
        out["don_vi"] = unit
    return out


def parse(*changes: dict[str, str]):
    return parse_what_if(list(changes))


# ---- điểm thi ----
def test_sat_and_act_become_integers():
    result = parse(change("sat", "1500"), change("act", "32"))

    assert result.changes == {"sat": 1500, "act": 32}
    assert result.unsupported == []


@pytest.mark.parametrize("value", ["1700", "300", "15000"])
def test_sat_outside_400_to_1600_asks_again(value):
    with pytest.raises(AskAgain, match="400 đến 1600"):
        parse(change("sat", value))


def test_sat_must_be_on_step_ten():
    with pytest.raises(AskAgain, match="bước 10"):
        parse(change("sat", "1455"))


def test_a_value_that_is_not_a_number_asks_again():
    with pytest.raises(AskAgain, match="SAT bao nhiêu"):
        parse(change("sat", "cao lắm"))


def test_a_missing_value_asks_again():
    with pytest.raises(AskAgain, match="ACT bao nhiêu"):
        parse(change("act"))


# ---- GPA ----
def test_gpa_keeps_the_scale_the_slm_read():
    result = parse(change("gpa", "9.0", "thang_10"))

    assert result.changes == {"gpa": {"value": 9.0, "scale": 10}}


def test_gpa_without_a_scale_is_guessed_like_the_profile_does():
    assert parse(change("gpa", "3.8")).changes["gpa"]["scale"] == 4
    assert parse(change("gpa", "8.5")).changes["gpa"]["scale"] == 10


def test_gpa_above_its_scale_asks_again():
    with pytest.raises(AskAgain, match="lớn hơn thang"):
        parse(change("gpa", "4.5", "thang_4"))


def test_a_decimal_comma_is_tolerated():
    assert parse(change("gpa", "3,8", "thang_4")).changes["gpa"]["value"] == 3.8


# ---- tiếng Anh ----
def test_english_is_tested_as_ielts():
    assert parse(change("english", "7.5")).changes == {"ielts": 7.5}
    assert parse(change("english", "7.5", "ielts")).changes == {"ielts": 7.5}


def test_ielts_must_be_on_half_steps():
    with pytest.raises(AskAgain, match="bước 0.5"):
        parse(change("english", "7.3"))


def test_toefl_and_duolingo_are_tested_by_their_unit():
    assert parse(change("english", "100", "toefl")).changes == {"toefl": 100}
    assert parse(change("english", "120", "duolingo")).changes == {"duolingo": 120}
    assert parse(change("english", "100", "TOEFL")).changes == {"toefl": 100}


def test_a_big_english_score_without_a_unit_asks_which_test():
    # 100 có thể là TOEFL hoặc Duolingo: hỏi lại, không đoán
    with pytest.raises(AskAgain, match="TOEFL hay Duolingo"):
        parse(change("english", "100"))


def test_toefl_and_duolingo_are_checked_against_their_limits():
    with pytest.raises(AskAgain, match="chỉ từ 0 đến 120"):
        parse(change("english", "130", "toefl"))
    with pytest.raises(AskAgain, match="chỉ từ 10 đến 160"):
        parse(change("english", "5", "duolingo"))


def test_other_english_tests_are_reported_as_unsupported():
    result = parse(change("english", "70", "pte"))

    assert result.changes == {}
    assert result.unsupported == ["chứng chỉ tiếng Anh ngoài IELTS, TOEFL, Duolingo"]


def test_the_label_names_toefl_and_duolingo():
    result = parse(change("english", "100", "toefl"), change("english", "120", "duolingo"))

    assert result.label() == "TOEFL 100, Duolingo 120"


# ---- ngân sách ----
def test_usd_budget_is_kept_and_vnd_is_converted_at_todays_rate(monkeypatch):
    monkeypatch.setattr(exchange_rate, "_rate", 25_000.0)

    assert parse(change("budget", "40000", "USD")).changes == {"budgetUsd": 40_000}
    assert parse(change("budget", "800000000", "VND")).changes == {"budgetUsd": 32_000}


def test_budget_unit_is_guessed_when_the_slm_gives_none(monkeypatch):
    monkeypatch.setattr(exchange_rate, "_rate", 25_000.0)

    assert parse(change("budget", "40000")).changes == {"budgetUsd": 40_000}
    assert parse(change("budget", "800000000")).changes == {"budgetUsd": 32_000}


@pytest.mark.parametrize("value", ["0", "-5000"])
def test_budget_must_be_positive(value):
    with pytest.raises(AskAgain, match="bao nhiêu mỗi năm"):
        parse(change("budget", value, "USD"))


def test_an_absurd_budget_asks_again():
    with pytest.raises(AskAgain, match="lớn bất thường"):
        parse(change("budget", "999999999999", "USD"))


# ---- học bổng ----
def test_scholarship_must_be_one_of_the_three_levels():
    assert parse(change("scholarship", "preferred")).changes == {"scholarshipNeed": "preferred"}
    with pytest.raises(AskAgain, match="học bổng"):
        parse(change("scholarship", "nhiều"))


# ---- yếu tố chưa thử được ----
def test_skipping_a_factor_cannot_be_simulated():
    result = parse(change("sat", hanh_dong=SKIP_ACTION))

    assert result.changes == {}
    assert result.unsupported == ["SAT"]


def test_profile_keys_outside_the_simulator_are_reported_by_name_once():
    result = parse(change("major", "CS"), change("major", "Toán"), change("intake", "Fall 2027"))

    assert result.changes == {}
    assert result.unsupported == ["ngành", "kỳ nhập học"]


def test_supported_and_unsupported_changes_can_come_together():
    result = parse(change("sat", "1450"), change("major", "CS"))

    assert result.changes == {"sat": 1450}
    assert result.unsupported == ["ngành"]


def test_the_last_value_wins_when_a_factor_repeats():
    assert parse(change("sat", "1400"), change("sat", "1500")).changes == {"sat": 1500}


# ---- lời nói lại ----
def test_label_restates_every_change_in_words():
    result = parse(change("gpa", "3.8", "thang_4"), change("sat", "1500"), change("english", "7.5"),
                   change("budget", "40000", "USD"), change("scholarship", "mandatory"))  # fmt: skip

    assert result.label() == (
        "GPA 3.8 (thang 4), SAT 1500, IELTS 7.5, ngân sách khoảng 40.000 USD mỗi năm, mức cần học bổng: bắt buộc"
    )
