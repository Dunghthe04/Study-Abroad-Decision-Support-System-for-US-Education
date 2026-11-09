"""Goal-seeking trong chat: đọc nhóm đích học sinh nói thành mã backend hiểu."""

import pytest

from app.services.goal_target import parse_target, plain


@pytest.mark.parametrize(
    ("said", "expected"),
    [
        ("Vừa sức", "match"),
        ("vua suc", "match"),
        ("VỪA SỨC", "match"),
        ("Match", "match"),
        ("An toàn", "safety"),
        ("an toàn hơn", "safety"),
        ("safety", "safety"),
        ("Khó vào", "reach"),
        ("reach", "reach"),
        ("em muốn thành vừa sức", "match"),
        ("lên nhóm an toàn", "safety"),
    ],
)
def test_target_words_become_backend_codes(said, expected):
    assert parse_target(said) == expected


@pytest.mark.parametrize("said", [None, "", "   ", "trường tốt", 42, ["match"]])
def test_no_or_unknown_target_leaves_the_choice_to_the_system(said):
    assert parse_target(said) is None


def test_a_sentence_naming_two_groups_picks_the_safer_one_first():
    # "Vừa sức" và "An toàn" cùng xuất hiện: dò An toàn trước, vì học sinh thường nhắc nhóm họ muốn đạt sau
    assert parse_target("từ vừa sức lên an toàn") == "safety"


def test_plain_drops_accents_and_the_vietnamese_d_bar():
    assert plain("Đại học Vừa sức") == "dai hoc vua suc"
