"""Goal-seeking trong chat: nhóm đích model trả (target_group) thành mã backend hiểu."""

import pytest

from app.services.goal_target import plain, target_code


@pytest.mark.parametrize(
    ("group", "expected"),
    [
        ("reach", "reach"),
        ("target", "match"),  # backend gọi nhóm vừa sức là "match"
        ("safety", "safety"),
    ],
)
def test_target_groups_become_backend_codes(group, expected):
    assert target_code(group) == expected


@pytest.mark.parametrize("group", [None, "", "   ", "match", "Vừa sức", "trường tốt", 42, ["target"]])
def test_no_or_unknown_target_leaves_the_choice_to_the_system(group):
    assert target_code(group) is None


def test_plain_drops_accents_and_the_vietnamese_d_bar():
    assert plain("Đại học Vừa sức") == "dai hoc vua suc"
