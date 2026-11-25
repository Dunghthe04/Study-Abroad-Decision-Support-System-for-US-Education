"""Kịch bản what-if: kiểm tra giá trị SLM đã chuẩn hóa và đổi sang khóa của API simulate."""

import pytest

from app.schemas.chat import ProfileField
from app.services.scenario import FILTER_FACTORS, SKIP_ACTION, parse_what_if, try_buttons
from app.services.skills import exchange_rate, skill_ho_so
from app.services.skills.major_catalog import Major
from app.services.skills.state_catalog import UsState
from app.services.skills.validators import AskAgain


def change(key: str, value: str | None = None, unit: str | None = None, **extra) -> dict[str, str]:
    out = {"factor": key, **extra}
    if value is not None:
        out["value"] = value
    if unit is not None:
        out["unit"] = unit
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
    result = parse(change("gpa", "9.0", "scale_10"))

    assert result.changes == {"gpa": {"value": 9.0, "scale": 10}}


def test_gpa_without_a_scale_is_guessed_like_the_profile_does():
    assert parse(change("gpa", "3.8")).changes["gpa"]["scale"] == 4
    assert parse(change("gpa", "8.5")).changes["gpa"]["scale"] == 10


def test_gpa_above_its_scale_asks_again():
    with pytest.raises(AskAgain, match="lớn hơn thang"):
        parse(change("gpa", "4.5", "scale_4"))


def test_a_decimal_comma_is_tolerated():
    assert parse(change("gpa", "3,8", "scale_4")).changes["gpa"]["value"] == 3.8


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
    result = parse(change("sat", action=SKIP_ACTION))

    assert result.changes == {}
    assert result.unsupported == ["SAT"]


def test_profile_keys_outside_the_simulator_are_reported_by_name_once():
    result = parse(
        change("school_type", "public"), change("school_type", "private"), change("intake", "Fall 2027")
    )

    assert result.changes == {}
    assert result.unsupported == ["loại trường", "kỳ nhập học"]


def test_supported_and_unsupported_changes_can_come_together():
    result = parse(change("sat", "1450"), change("school_type", "public"))

    assert result.changes == {"sat": 1450}
    assert result.unsupported == ["loại trường"]


def test_the_last_value_wins_when_a_factor_repeats():
    assert parse(change("sat", "1400"), change("sat", "1500")).changes == {"sat": 1500}


# ---- lời nói lại ----
def test_label_restates_every_change_in_words():
    result = parse(change("gpa", "3.8", "scale_4"), change("sat", "1500"), change("english", "7.5"),
                   change("budget", "40000", "USD"), change("scholarship", "mandatory"))  # fmt: skip

    assert result.label() == (
        "GPA 3.8 (thang 4), SAT 1500, IELTS 7.5, ngân sách khoảng 40.000 USD mỗi năm, mức cần học bổng: bắt buộc"
    )


# Mục không thử được chia nhóm: lọc trường, không ảnh hưởng danh sách; mục khác không vào nhóm nào
def test_unsupported_factors_are_grouped():
    result = parse(
        change("school_type", "public"),
        change("intake", "Fall 2027"),
        change("activities", "x"),
        change("school_type", "private"),
    )

    assert result.filters == ["school_type"]
    assert result.no_effect == ["intake"]
    assert result.unsupported == ["loại trường", "kỳ nhập học", "hoạt động ngoại khóa"]


# Nút sửa của mục lọc trường mở đúng nhóm có trong ô chọn sửa hồ sơ
def test_filter_factors_point_to_edit_sections():
    assert set(FILTER_FACTORS.values()) <= set(skill_ho_so.EDIT_SECTIONS)


def confirmed(value):
    return ProfileField(value=value, status="confirmed")


def test_try_buttons_go_one_step_above_the_profile():
    profile = {
        "sat": confirmed(1350),
        "gpa": confirmed({"value": 3.6, "scale": 4}),
        "english": confirmed({"type": "ielts", "value": 6.5}),
        "budget": confirmed("tier_35k_50k"),
    }

    buttons = try_buttons(profile)

    assert [b.label for b in buttons] == [
        "Thử SAT 1450",
        "Thử GPA 3.8",
        "Thử IELTS 7",
        "Thử ngân sách 70.000 USD",
    ]
    assert [b.value for b in buttons] == [
        "Nếu SAT của em là 1450 thì sao?",
        "Nếu GPA của em là 3.8 thì sao?",
        "Nếu IELTS của em là 7 thì sao?",
        "Nếu ngân sách của em là 70000 USD mỗi năm thì sao?",
    ]


def test_try_buttons_without_a_profile_start_from_common_marks():
    assert [b.label for b in try_buttons({})] == ["Thử SAT 1400", "Thử IELTS 6.5"]


def test_try_buttons_skip_what_is_already_at_the_top_and_other_english_tests():
    profile = {
        "sat": confirmed(1600),
        "gpa": confirmed({"value": 9.8, "scale": 10}),
        "english": confirmed({"type": "toefl", "value": 100}),
        "budget": confirmed("tier_above_70k"),
    }

    assert [b.label for b in try_buttons(profile)] == ["Thử GPA 10"]


def test_try_buttons_for_the_top_budget_tier_below_the_last_adds_a_round_amount():
    assert [b.label for b in try_buttons({"budget": confirmed("tier_50k_70k"), "sat": confirmed(1600)})] == [
        "Thử IELTS 6.5",
        "Thử ngân sách 80.000 USD",
    ]


# ---- UC-155: bang, ngành, tỉ giá, vòng nộp ----
STATES = [
    UsState("TX", "Texas"),
    UsState("CA", "California"),
    UsState("NC", "North Carolina"),
    UsState("SC", "South Carolina"),
]
MAJORS = [
    Major("Computer and information sciences", "Khoa học máy tính", ("cs",)),
    Major("History", "Lịch sử"),
]


def parse_with(*changes: dict, states=STATES, majors=MAJORS):
    return parse_what_if(list(changes), states, majors)


def test_a_state_name_becomes_the_state_code():
    assert parse_with(change("state", "Texas")).changes == {"states": ["TX"]}


def test_several_states_and_the_old_profile_key_are_accepted():
    assert parse_with(change("state", ["texas", "California"])).changes == {"states": ["TX", "CA"]}
    assert parse_with(change("preferred_states", "Texas, California")).changes == {"states": ["TX", "CA"]}


def test_a_state_that_is_unclear_asks_again_and_names_the_near_ones():
    with pytest.raises(AskAgain, match="North Carolina"):
        parse_with(change("state", "Carolina"))
    with pytest.raises(AskAgain, match="chưa tìm thấy bang"):
        parse_with(change("state", "Narnia"))


def test_a_state_without_the_catalog_is_only_taken_when_it_already_is_a_code():
    assert parse_with(change("state", "ny"), states=[]).changes == {"states": ["NY"]}
    unnamed = parse_with(change("state", "Texas"), states=[])
    assert unnamed.changes == {} and unnamed.unsupported == ["bang"]


def test_a_major_becomes_the_systems_canonical_name():
    assert parse_with(change("major", "Khoa học máy tính")).changes == {
        "major": "Computer and information sciences"
    }
    assert parse_with(change("major", "cs")).changes == {"major": "Computer and information sciences"}


def test_a_major_the_system_does_not_know_asks_again():
    with pytest.raises(AskAgain, match="chưa tìm thấy ngành"):
        parse_with(change("major", "Đấu kiếm phù thủy"))


def test_a_major_without_the_catalog_cannot_be_tried():
    result = parse_with(change("major", "Lịch sử"), majors=[])

    assert result.changes == {} and result.unsupported == ["ngành"]


def test_an_exchange_rate_percent_is_kept_as_the_rise():
    assert parse(change("exchange_rate", "5", "percent")).changes == {"exchangeRatePercent": 5.0}


def test_an_exchange_rate_in_dong_must_be_above_todays_rate():
    exchange_rate.reset()
    assert parse(change("exchange_rate", "27000", "VND")).changes == {"exchangeRateVnd": 27000.0}
    with pytest.raises(AskAgain, match="chỉ thử được mức tăng"):
        parse(change("exchange_rate", "24000", "VND"))
    with pytest.raises(AskAgain, match="chỉ thử được mức tăng"):
        parse(change("exchange_rate", "25000"))  # bằng tỉ giá hiện nay: chưa phải mức tăng


@pytest.mark.parametrize("value", ["0", "-3", "150"])
def test_an_exchange_rate_percent_outside_the_range_asks_again(value):
    with pytest.raises(AskAgain, match="lớn hơn 0 và không quá 100%"):
        parse(change("exchange_rate", value, "percent"))


def test_a_rate_that_cannot_be_dong_per_dollar_asks_which_unit():
    with pytest.raises(AskAgain, match="VNĐ mỗi USD, hoặc tăng bao nhiêu phần trăm"):
        parse(change("exchange_rate", "5"))


def test_the_last_way_of_giving_the_rate_wins():
    exchange_rate.reset()
    result = parse(change("exchange_rate", "27000", "VND"), change("exchange_rate", "8", "percent"))

    assert result.changes == {"exchangeRatePercent": 8.0}


def test_the_application_round_changes_nothing_and_is_said_so():
    result = parse(change("application_round", "ED"))

    assert result.changes == {} and result.no_effect == ["application_round"]
    assert result.unsupported == ["vòng nộp (ED, EA, RD)"]


def test_label_restates_the_new_factors():
    exchange_rate.reset()
    result = parse_with(
        change("state", "Texas"), change("major", "History"), change("exchange_rate", "10", "percent")
    )

    assert result.label() == "bang TX, ngành History, tỉ giá tăng 10%"
