from datetime import date

import pytest

from app.schemas.chat import ProfileField
from app.services.skills.commit_rules import commit_blockers

TODAY = date(2026, 10, 28)

ACTIVITY = {
    "name": "CLB Robotics",
    "category": "activity",
    "role": "leader",
    "impactLevel": "school",
    "startDate": "09/2024",
    "endDate": None,
    "isOngoing": True,
}
AWARD = {"name": "HSG Tin thành phố", "category": "achievement", "impactLevel": "province", "year": 2025}


def confirmed(value):
    return ProfileField(value=value, status="confirmed")


# Hồ sơ đủ để lưu: chỉ có các khóa bắt buộc
def minimal(**changes):
    profile = {
        "current_level": confirmed("high_school"),
        "target_level": confirmed("undergraduate"),
        "gpa": confirmed({"value": 8.5, "scale": 10}),
        "budget": confirmed("tier_20k_35k"),
    }
    return {**profile, **changes}


def test_minimal_profile_can_commit():
    assert commit_blockers(minimal(), today=TODAY) == []


def test_full_profile_can_commit():
    profile = minimal(
        current_grade=confirmed("Lớp 12"),
        major=confirmed("Computer Science"),
        scholarship=confirmed("preferred"),
        english=confirmed({"type": "ielts", "value": 6.5, "date": "2026-10"}),
        activities=confirmed([ACTIVITY] * 6),  # form cho nhiều hơn 4, .NET vẫn nhận
        awards=confirmed([AWARD]),
        preferred_states=confirmed(["California"]),
    )
    assert commit_blockers(profile, today=TODAY) == []


def test_empty_profile_lists_required_in_ask_order():
    assert commit_blockers({}, today=TODAY) == [
        "current_level",
        "target_level",
        "gpa",
        "budget",
    ]


def test_said_in_b0_counts_as_value():
    profile = minimal(gpa=ProfileField(value={"value": 3.6, "scale": 4}, status="said_in_B0"))
    assert commit_blockers(profile, today=TODAY) == []


def test_none_and_unknown_are_ok_for_optional():
    profile = minimal(
        english=ProfileField(status="none"),
        major=ProfileField(value="unknown", status="unknown"),
        activities=ProfileField(status="none"),
        scholarship=ProfileField(status="declined"),
    )
    assert commit_blockers(profile, today=TODAY) == []


@pytest.mark.parametrize(
    ("changes", "key"),
    [
        ({"current_level": confirmed("phd")}, "current_level"),
        ({"current_grade": confirmed("Năm 2")}, "current_grade"),  # không thuộc high_school
        ({"target_level": ProfileField(value="unknown", status="unknown")}, "target_level"),
        ({"major": confirmed("x" * 129)}, "major"),
        ({"gpa": confirmed({"value": 85, "scale": 10})}, "gpa"),
        ({"english": confirmed({"type": "ielts", "value": 6.3, "date": None})}, "english"),
        ({"english": confirmed({"type": "toefl", "value": 95, "date": "2026-11"})}, "english"),
        ({"budget": confirmed("tier_1m")}, "budget"),
        ({"scholarship": confirmed("required")}, "scholarship"),  # mã cũ, form dùng mandatory
        ({"activities": confirmed([{**ACTIVITY, "role": None}])}, "activities"),
        ({"activities": confirmed([{**ACTIVITY, "isOngoing": False}])}, "activities"),
        ({"awards": confirmed([{**AWARD, "year": None}])}, "awards"),
        ({"preferred_states": confirmed(["Texas"] * 61)}, "preferred_states"),
    ],
)
def test_value_dotnet_rejects_blocks(changes, key):
    assert commit_blockers(minimal(**changes), today=TODAY) == [key]
