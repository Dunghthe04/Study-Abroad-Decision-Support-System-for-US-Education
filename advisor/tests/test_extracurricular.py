import pytest

from app.services.extracurricular import Activity, activity_quality, extracurricular_score, role_from_text


def act(level=1, role="member", months=12, reputable=False) -> Activity:
    return Activity(impact_level=level, role=role, months=months, reputable_org=reputable)


# ---------- 5 ví dụ tính tay đã gửi thầy ----------


@pytest.mark.parametrize(
    ("activities", "expected"),
    [
        ([], 0.0),  # không có hoạt động
        ([act(level=1, role="member", months=3)], 0.61),  # thành viên CLB trường, 3 tháng
        ([act(level=3, role="founder", months=24)], 0.92),  # sáng lập CLB cấp tỉnh, 2 năm
        ([act(level=1, role="member", months=12)] * 3, 1.97),  # 3 hoạt động bình thường
        ([act(level=4, role="head", months=24)] * 4, 3.7),  # 4 hoạt động mạnh
        ([act(level=5, role="founder", months=24, reputable=True)] * 4, 4.0),  # 4 hoạt động xuất sắc
    ],
)
def test_score_matches_hand_calculation(activities, expected):
    assert extracurricular_score(activities).score == expected


# ---------- các quy tắc của công thức ----------


def test_only_best_four_activities_count():
    weak = act(level=1, role="member", months=1)
    strong = act(level=5, role="founder", months=24)

    result = extracurricular_score([weak, strong, strong, strong, strong])

    assert result.score == 4.0
    assert result.activities[0].counted is False  # hoạt động yếu nhất bị bỏ
    assert result.activities[0].points == 0.0
    assert all(a.counted for a in result.activities[1:])


def test_score_never_exceeds_four():
    many = [act(level=5, role="founder", months=60, reputable=True)] * 10

    assert extracurricular_score(many).score == 4.0


def test_reputable_bonus_is_capped_at_one():
    assert activity_quality(act(level=5, role="founder", months=24, reputable=True)) == 1.0


def test_unknown_months_uses_neutral_time():
    known = activity_quality(act(months=12))  # 12/24 = 0.5
    unknown = activity_quality(act(months=None))

    assert unknown == known


def test_more_months_than_two_years_counts_as_two_years():
    assert activity_quality(act(months=60)) == activity_quality(act(months=24))


def test_invalid_impact_level_is_rejected():
    with pytest.raises(ValueError):
        Activity(impact_level=6, role="member")


# ---------- đọc vai trò từ chữ (bước sau LLM sẽ thay) ----------


@pytest.mark.parametrize(
    ("text", "expected"),
    [
        ("Người sáng lập", "founder"),
        ("Co-founder", "founder"),
        ("Phó chủ nhiệm CLB", "deputy"),  # không được nhầm thành chủ nhiệm
        ("Trưởng ban truyền thông", "deputy"),  # không được nhầm thành trưởng
        ("Chủ nhiệm CLB Robotics", "head"),
        ("Đội trưởng đội bóng", "head"),
        ("Team Leader", "head"),
        ("Thành viên", "member"),
        ("TNV", "member"),  # không khớp từ khóa nào → thành viên
        (None, "member"),
    ],
)
def test_role_from_text(text, expected):
    assert role_from_text(text) == expected


# ---------- điểm thưởng giải thưởng ----------


def test_award_bonus_is_005_per_level():
    # 1 hoạt động 0.92 + giải cấp tỉnh (3) 0.15 + giải quốc gia (4) 0.20
    result = extracurricular_score([act(level=3, role="founder", months=24)], award_levels=[3, 4])

    assert [a.points for a in result.awards] == [0.15, 0.2]
    assert result.score == 1.27


def test_award_bonus_counts_best_three_and_caps_at_half_point():
    result = extracurricular_score([], award_levels=[1, 5, 5, 5])

    assert result.awards[0].counted is False  # giải cấp trường bị bỏ vì ngoài 3 giải cao nhất
    assert result.score == 0.5  # 3 × 0.25 = 0.75, chặn ở 0.5


def test_awards_cannot_push_score_above_four():
    full = [act(level=5, role="founder", months=24, reputable=True)] * 4

    assert extracurricular_score(full, award_levels=[5]).score == 4.0
