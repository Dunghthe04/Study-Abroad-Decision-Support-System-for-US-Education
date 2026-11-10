"""C4: làm phẳng hồ sơ chat thành ho_so của SkillRequest. Hàm thuần nên không cần AI hay DB."""

from datetime import date

from app.schemas.chat import ChatState, ProfileField
from app.schemas.slm import CALC_KEYS, PROFILE_KEYS, SkillRequest
from app.services.skill_input import NONE_TEXT, profile_to_skill_input, skill_request_of
from app.services.skills.buttons import NONE, UNKNOWN


def field(value, status="confirmed") -> ProfileField:
    return ProfileField(value=value, status=status)


# Hồ sơ đủ như học sinh trả lời hết các câu D1…N4
FULL = {
    "current_level": field("high_school"),
    "current_grade": field("Lớp 11"),
    "target_level": field("undergraduate"),
    "major": field("Finance"),
    "intake": field("Fall 2027"),
    "education_system": field("standard"),
    "gpa": field({"value": 8.0, "scale": 10, "source": "overall"}),
    "sat": field(1200),
    "act": NONE,
    "english": field({"type": "ielts", "value": 6.5, "date": "2025-06"}),
    "english_plan": field({"planned": True, "date": "2027-05", "test_type": "ielts", "target_score": 7.0}),
    "standardized_plan": field({"planned": False, "date": None}),
    "budget": field("tier_35k_50k"),
    "scholarship": field("preferred"),
    "funding_source": field("family_support"),
    "activities": field(
        [
            {
                "name": "CLB Tiếng Anh",
                "category": "activity",
                "role": "leader",
                "impactLevel": "province",
                "startDate": "09/2024",
                "endDate": None,
                "isOngoing": True,
            },
            {
                "name": "CLB Văn nghệ",
                "category": "activity",
                "role": "member",
                "impactLevel": "school",
                "startDate": "09/2023",
                "endDate": "05/2024",
                "isOngoing": False,
            },
        ]
    ),
    "awards": field(
        [{"name": "Giải Nhì HSG Toán", "category": "achievement", "impactLevel": "province", "year": 2025}]
    ),
    "relatives_state": NONE,
    "preferred_states": field(["California", "Texas"]),
    "school_type": field("public"),
    "visa_refused": field(False),
}


def test_every_profile_key_is_turned_into_text():
    ho_so = profile_to_skill_input(FULL)

    assert list(ho_so) == list(PROFILE_KEYS)
    assert all(isinstance(text, str) and text for text in ho_so.values())


def test_values_read_like_the_training_data():
    ho_so = profile_to_skill_input(FULL)

    assert ho_so["gpa"] == "8 thang 10"
    assert ho_so["sat"] == "1200"
    assert ho_so["english"] == "IELTS 6.5"
    assert ho_so["budget"] == "khoảng 35.000 đến 50.000 USD mỗi năm"
    assert ho_so["target_level"] == "đại học"
    assert ho_so["scholarship"] == "nên có"
    assert ho_so["intake"] == "Fall 2027"
    assert ho_so["preferred_states"] == "California, Texas"
    assert ho_so["visa_refused"] == "chưa từng bị từ chối visa"


def test_codes_use_the_form_labels():
    ho_so = profile_to_skill_input(FULL)

    assert ho_so["current_level"] == "trung học phổ thông"
    assert ho_so["education_system"] == "hệ chuẩn công lập Việt Nam"
    assert ho_so["funding_source"] == "gia đình hỗ trợ toàn bộ / một phần"
    assert ho_so["school_type"] == "công lập"


def test_activities_and_awards_are_joined_into_one_line():
    ho_so = profile_to_skill_input(FULL)

    assert ho_so["activities"] == (
        "trưởng / chủ nhiệm CLB Tiếng Anh cấp tỉnh / thành phố từ 09/2024 đến nay; "
        "thành viên CLB Văn nghệ cấp trường từ 09/2023 đến 05/2024"
    )
    assert ho_so["awards"] == "Giải Nhì HSG Toán cấp tỉnh / thành phố năm 2025"


def test_exam_plans_keep_test_month_and_target():
    ho_so = profile_to_skill_input(FULL)

    assert ho_so["english_plan"] == "dự định thi IELTS tháng 05/2027, mục tiêu 7"
    assert ho_so["standardized_plan"] == "chưa có kế hoạch thi"
    assert profile_to_skill_input({"english_plan": field({"planned": True, "date": None})}) == {
        "english_plan": "dự định thi"
    }


def test_lowest_and_highest_budget_tiers():
    assert profile_to_skill_input({"budget": field("tier_under_20k")})["budget"] == "dưới 20.000 USD mỗi năm"
    assert profile_to_skill_input({"budget": field("tier_above_70k")})["budget"] == "trên 70.000 USD mỗi năm"


def test_none_and_unknown_are_kept_so_the_skill_knows_it_was_asked():
    ho_so = profile_to_skill_input({"activities": NONE, "awards": NONE, "major": UNKNOWN})

    assert ho_so == {"activities": NONE_TEXT, "awards": NONE_TEXT, "major": "chưa rõ"}


def test_helper_and_unanswered_keys_are_dropped():
    profile = {
        "gpa": field({"value": 3.4, "scale": 4}),
        "activities_pending": field({"name": "CLB Cờ"}),
        "cert_pending": field({"type": "ielts"}),
        "certifications": field([{"type": "ielts", "overall": 6.5}]),
        "graduation_year": field(2027),
        "sat": field(None, "declined"),
        "act": field(None),
    }

    assert profile_to_skill_input(profile) == {"gpa": "3.4 thang 4"}


def test_unknown_value_shape_falls_back_to_text():
    assert profile_to_skill_input({"major": field(["Finance", "Economics"])}) == {
        "major": "Finance; Economics"
    }


def test_result_is_a_valid_skill_request():
    req = SkillRequest(loai="ho_so", cau_hoi="Em có nên nộp Iowa không?", ho_so=profile_to_skill_input(FULL))

    assert req.ho_so["gpa"] == "8 thang 10"


TODAY = date(2026, 11, 5)
TRANSCRIPT = {
    "years": [
        {"grade": "Lớp 10", "average": 7.5, "subjects": [{"name": "Hóa học", "average": 8}]},
        {"grade": "Lớp 11", "average": 7.8, "subjects": [{"name": "Hóa học", "average": 8.5}]},
    ]
}


def ask(state: ChatState) -> SkillRequest:
    return skill_request_of(state, "Em có nên nộp Iowa không?", TODAY)


def test_calculated_lines_go_to_tinh_san_not_into_ho_so():
    req = ask(ChatState(profile=FULL, transcript=TRANSCRIPT))

    assert req.loai == "ho_so"
    assert req.ho_so == profile_to_skill_input(FULL)  # hồ sơ giữ nguyên, không lẫn dòng tính sẵn
    assert set(req.tinh_san) <= set(CALC_KEYS)
    assert req.tinh_san["grade_by_year"] == "Lớp 10: 7.5; Lớp 11: 7.8"
    assert req.tinh_san["gpa_level"] == "3.5 thang 4"  # chưa có mốc trường thì chỉ ghi điểm
    assert req.tinh_san["english_level"] == "IELTS 6.5"
    assert req.tinh_san["timeline"] == "hôm nay 11/2026, nhập học kỳ Fall 08/2027, còn 9 tháng"


def test_calculated_lines_compare_with_school_benchmarks():
    req = ask(ChatState(profile=FULL, benchmarks={"gpa4": 3.6, "english": {"ielts": 6.5}}))

    assert req.tinh_san["gpa_level"] == "3.5 thang 4, trung bình (TB các trường 3.6)"
    assert req.tinh_san["english_level"] == "IELTS 6.5, trung bình (mốc phổ biến 6.5)"


def test_the_whole_transcript_is_never_sent():
    req = ask(ChatState(profile=FULL, transcript=TRANSCRIPT))

    assert "Hóa học" not in " ".join([*req.ho_so.values(), *req.tinh_san.values()])


def test_overall_gpa_has_no_grade_by_year():
    req = ask(ChatState(profile=FULL))

    assert "grade_by_year" not in req.tinh_san
    assert "gpa_level" in req.tinh_san
