import pytest

from app.services.skills.validators import AskAgain, check_gpa


def test_gpa_above_4_is_scale_10():
    assert check_gpa(8.5) == (8.5, 10)


def test_gpa_up_to_4_is_scale_4():
    assert check_gpa(3.6) == (3.6, 4)


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
    assert "vượt quá" in err.value.question


def test_invalid_scale_ask_again():
    with pytest.raises(AskAgain) as err:
        check_gpa(3.5, 5)
    assert "không hợp lệ" in err.value.question
