from app.services.skill_rules import REQUIRED_B4
from app.services.skills.info_to_field import INFO_TO_FIELD, PROFILE_KEYS, to_fields


def test_text_keys_become_profile_keys():
    info = {"gpa_text": "8.5", "english_text": "IELTS 6.5", "budget_text": "800 triệu", "major_text": "CNTT"}
    assert to_fields(info) == {"gpa": "8.5", "english": "IELTS 6.5", "budget": "800 triệu", "major": "CNTT"}


def test_profile_keys_pass_through():
    assert to_fields({"current_level": "lớp 12", "sat": 1450}) == {"current_level": "lớp 12", "sat": 1450}


def test_unknown_keys_and_empty_values_dropped():
    assert to_fields({"hobby": "đá bóng", "gpa_text": "", "english_text": None}) == {}


def test_profile_key_wins_over_text_key():
    assert to_fields({"gpa_text": "8", "gpa": "8.5"}) == {"gpa": "8.5"}
    assert to_fields({"gpa": "8.5", "gpa_text": "8"}) == {"gpa": "8.5"}


def test_every_required_key_is_known():
    # skill_rules hỏi khóa nào thì bảng phải biết khóa đó, và mọi khóa đích đều là khóa chuẩn
    assert set(REQUIRED_B4) <= set(PROFILE_KEYS)
    assert set(INFO_TO_FIELD.values()) <= set(PROFILE_KEYS)
