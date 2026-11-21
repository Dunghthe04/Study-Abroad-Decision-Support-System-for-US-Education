"""Các phép kiểm dùng chung cho lời Claude viết (số, lời hứa, phán trường, nhắc đủ tên, tên trường lạ) và cách nối tin."""

import pytest

from app.schemas.goal_note import GoalNoteResult
from app.services.note_checks import (
    PROMISES,
    check_mentions_all,
    check_no_promise,
    check_no_strangers,
    check_no_verdict,
    check_numbers,
    numbers,
    render_note,
)


# ---- đọc số
@pytest.mark.parametrize(
    ("text", "expected"),
    [
        ("chi phí 40.000 USD", [40000.0]),
        ("40,000 USD", [40000.0]),
        ("IELTS 7,5 và GPA 3.5", [7.5, 3.5]),
        ("SAT 1290 lên 1430", [1290.0, 1430.0]),
        ("không có số", []),
    ],
)
def test_numbers_reads_thousands_and_decimals_the_vietnamese_way(text, expected):
    assert numbers(text) == expected


def test_a_number_outside_the_allowed_set_is_rejected_with_the_source_in_the_message():
    check_numbers("SAT 1290", {1290.0}, "dữ kiện")

    with pytest.raises(ValueError, match=r"không có trong số liệu: \[1500.0\]"):
        check_numbers("SAT 1500", {1290.0}, "số liệu")


def test_number_formatting_does_not_make_an_allowed_number_look_foreign():
    check_numbers("khoảng 38000 USD, SAT 1.430", {38000.0, 1430.0}, "dữ kiện")


# ---- lời hứa
@pytest.mark.parametrize("promise", PROMISES)
def test_every_promise_phrase_is_rejected_in_any_case(promise):
    with pytest.raises(ValueError, match="lời hứa"):
        check_no_promise(f"Nộp theo cách này thì bạn {promise.upper()} nhé.")


def test_a_text_without_promises_passes():
    check_no_promise("Bạn có cơ hội tốt, vẫn không phải là chắc chắn.")


# ---- phán xét
def test_forbidden_words_are_listed_in_the_message():
    with pytest.raises(ValueError, match=r"xếp hạng: \['tốt nhất'\]"):
        check_no_verdict("Đây là trường tốt nhất.", ("tốt nhất", "nên chọn"), "không được xếp hạng")

    check_no_verdict("Hai trường khác nhau ở chi phí.", ("tốt nhất", "nên chọn"), "không được xếp hạng")


# ---- nhắc đủ tên
def test_every_name_must_be_mentioned_ignoring_case():
    check_mentions_all(
        "Có University of Iowa và texas state university.",
        ["University of Iowa", "Texas State University"],
        "mọi trường",
    )

    with pytest.raises(ValueError, match=r"nhắc đủ mọi trường, thiếu: \['Texas State University'\]"):
        check_mentions_all(
            "Chỉ có University of Iowa.", ["University of Iowa", "Texas State University"], "mọi trường"
        )


# ---- tên trường lạ
def test_a_school_outside_the_list_is_a_stranger():
    check_no_strangers("Nên xem University of Iowa.", {"University of Iowa"}, ["Harvard University"])

    with pytest.raises(ValueError, match="ngoài danh sách"):
        check_no_strangers("Nên xem Harvard University.", {"University of Iowa"}, ["Harvard University"])


def test_a_short_name_inside_a_longer_valid_name_is_not_a_stranger():
    check_no_strangers(
        "Nên xem Washington University in St. Louis.",
        {"Washington University in St. Louis"},
        ["Washington University"],
    )


def test_the_message_names_at_most_three_strangers():
    with pytest.raises(ValueError) as error:
        check_no_strangers("A B C D", set(), ["a", "b", "c", "d"])

    assert "['a', 'b', 'c']" in str(error.value)


# ---- nối tin
def test_a_note_is_joined_into_intro_bullets_and_closing():
    note = GoalNoteResult(intro="Câu mở đủ dài.", points=["Ý một.", "  ", "Ý hai."], closing="Câu kết.")

    assert render_note(note) == "Câu mở đủ dài.\n\n- Ý một.\n- Ý hai.\n\nCâu kết."


def test_a_note_without_points_or_closing_is_just_the_intro():
    assert render_note(GoalNoteResult(intro="Chỉ có câu mở.", points=[], closing="")) == "Chỉ có câu mở."
