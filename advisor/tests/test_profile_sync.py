"""Việc 1.5: gộp hồ sơ từ bản nháp (.NET gửi) vào state chat, và báo khóa nào đổi. Hàm thuần nên không cần AI hay DB."""

from app.schemas.chat import ProfileField
from app.services.profile_sync import DRAFT_KEYS, changed_keys, merge_profile


def field(value="x", status="confirmed") -> ProfileField:
    return ProfileField(value=value, status=status)


def test_draft_keys_are_the_ones_that_have_a_place_in_the_form():
    assert {"gpa", "english", "budget", "major", "target_level", "scholarship"} <= DRAFT_KEYS
    # lớp đang học (Bước 1) và kế hoạch thi tiếng Anh (Bước 3) cũng có ô trong form
    assert {"current_grade", "english_plan"} <= DRAFT_KEYS
    # form chưa có chỗ lưu những khóa này, chỉ chat giữ
    assert (
        not {"education_system", "intake", "funding_source", "relatives_state", "visa_refused"} & DRAFT_KEYS
    )


def test_values_from_the_draft_are_added():
    merged = merge_profile({}, {"gpa": field(3.4), "budget": field("tier_20k_35k")})

    assert merged["gpa"].value == 3.4
    assert merged["budget"].value == "tier_20k_35k"


def test_draft_value_replaces_a_different_value_in_the_chat():
    current = {"english": field({"type": "ielts", "total": 6.5})}
    incoming = {"english": field({"type": "ielts", "total": 7.0})}

    assert merge_profile(current, incoming)["english"].value["total"] == 7.0  # học sinh sửa ở form


def test_same_value_keeps_the_status_the_chat_already_has():
    # .NET ghi mọi ô có giá trị là "confirmed", nhưng thông tin học sinh mới nói chưa được xác nhận
    current = {"gpa": field(3.4, "said_in_B0")}
    incoming = {"gpa": field(3.4, "confirmed")}

    assert merge_profile(current, incoming)["gpa"].status == "said_in_B0"


def test_chat_only_keys_are_kept():
    current = {"education_system": field("IB"), "funding_source": field("bố mẹ")}

    merged = merge_profile(current, {"gpa": field(3.4)})

    assert merged["education_system"].value == "IB"
    assert merged["funding_source"].value == "bố mẹ"


def test_draft_cannot_overwrite_chat_only_keys():
    current = {"education_system": field("IB")}

    merged = merge_profile(current, {"education_system": field("A-level")})

    assert merged["education_system"].value == "IB"


def test_draft_key_missing_from_the_draft_is_dropped():
    current = {"gpa": field(3.4), "english": field("x")}

    merged = merge_profile(current, {"english": field("x")})  # form đã xóa GPA

    assert "gpa" not in merged
    assert "english" in merged


def test_empty_draft_clears_every_draft_key_but_not_chat_only_keys():
    current = {"gpa": field(3.4), "intake": field("fall 2027")}

    merged = merge_profile(current, {})

    assert set(merged) == {"intake"}


def test_merge_does_not_modify_the_inputs():
    current = {"gpa": field(3.4)}
    incoming = {"english": field("x")}

    merge_profile(current, incoming)

    assert set(current) == {"gpa"} and set(incoming) == {"english"}


def test_changed_keys_finds_added_changed_and_removed_keys_sorted():
    before = {"gpa": field(3.4), "budget": field("a"), "english": field("x")}
    after = {"gpa": field(3.4), "budget": field("b"), "major": field("CS")}

    # budget đổi giá trị, english bị xóa, major mới thêm, gpa giữ nguyên
    assert changed_keys(before, after) == ["budget", "english", "major"]


def test_changed_keys_sees_a_status_change():
    assert changed_keys({"gpa": field(3.4, "said_in_B0")}, {"gpa": field(3.4, "confirmed")}) == ["gpa"]


def test_changed_keys_ignores_chat_only_keys_and_returns_empty_when_nothing_changed():
    before = {"gpa": field(3.4)}

    assert changed_keys(before, {"gpa": field(3.4), "funding_source": field("bố mẹ")}) == []
    assert changed_keys(before, {"gpa": field(3.4)}) == []
