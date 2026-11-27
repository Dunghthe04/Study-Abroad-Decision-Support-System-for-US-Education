"""Mốc thời gian đã trôi qua phải ghi rõ, và kỳ nhập học sớm hơn lúc tốt nghiệp THPT phải được nhắc.
Lỗi gặp ngày 27/11/2026: lộ trình liệt kê hạn nộp 01/11/2026 như việc sắp tới; học sinh lớp 11 chọn Spring 2027
không được nhắc là chưa tốt nghiệp."""

from datetime import date

import pytest

from app.schemas.chat import ChatState, ProfileField
from app.services.skills.buttons import early_intake_note
from app.services.skills.skill_ho_so import review
from app.services.skills.skill_lo_trinh import (
    PASSED,
    PASSED_NOTE,
    Intake,
    Route,
    render,
    roadmap_timeline,
    timeline,
)

TODAY = date(2026, 11, 27)
ROADMAP = {
    "milestones": [
        {"date": "2026-11-01", "label": "Hạn nộp MIT"},
        {"date": "2026-11-27", "label": "Hạn đúng hôm nay"},
        {"date": "2027-05-01", "label": "Nộp hồ sơ visa", "isEstimate": True},
    ]
}


def prof(**values) -> dict[str, ProfileField]:
    return {k: ProfileField(value=v, status="confirmed") for k, v in values.items()}


def direct() -> Route:
    return Route(route="direct", alt_routes=[], codes=[], english_status="ok", months_to_deadline=None)


def test_roadmap_marks_only_dates_before_today():
    assert roadmap_timeline(ROADMAP, TODAY) == [
        f"- 01/11/2026: Hạn nộp MIT{PASSED}",
        "- 27/11/2026: Hạn đúng hôm nay",
        "- 01/05/2027: Nộp hồ sơ visa (tham khảo)",
    ]


def test_computed_timeline_marks_passed_months():
    lines = timeline(Intake.parse("Fall 2027"), direct(), TODAY)
    assert lines[0] == "- Nộp sớm (EA/ED): khoảng tháng 11/2026"  # cùng tháng với hôm nay thì chưa qua
    assert not any(line.endswith(PASSED) for line in lines)

    later = timeline(Intake.parse("Fall 2027"), direct(), date(2027, 2, 1))
    assert later[0].endswith(PASSED) and later[1].endswith(PASSED) and not later[-1].endswith(PASSED)


def test_passed_note_only_when_something_passed():
    assert PASSED_NOTE in render(direct(), Intake.parse("Fall 2027"), None, ROADMAP, TODAY)
    assert PASSED_NOTE not in render(direct(), Intake.parse("Fall 2028"), None, None, TODAY)


@pytest.mark.parametrize(
    ("values", "year"),
    [
        ({"current_level": "high_school", "current_grade": "Lớp 11", "intake": "Spring 2027"}, 2028),
        ({"current_level": "high_school", "current_grade": "Lớp 11", "intake": "Fall 2027"}, 2028),
        ({"current_level": "high_school", "current_grade": "Lớp 11", "intake": "Spring 2028"}, 2028),
        ({"current_level": "middle_school", "current_grade": "Lớp 9", "intake": "Fall 2028"}, 2030),
    ],
)
def test_intake_before_graduation_is_noted(values, year):
    note = early_intake_note(prof(**values), TODAY)
    assert note is not None
    assert f"tốt nghiệp THPT năm {year}" in note and values["intake"] in note and f"Fall {year}" in note


@pytest.mark.parametrize(
    "values",
    [
        {"current_level": "high_school", "current_grade": "Lớp 11", "intake": "Summer 2028"},
        {"current_level": "high_school", "current_grade": "Lớp 11", "intake": "Fall 2028"},
        {"current_level": "high_school", "current_grade": "Lớp 12", "intake": "Fall 2027"},
        # Năm tốt nghiệp đã lưu (tốt nghiệp sớm) được tin hơn lớp
        {
            "current_level": "high_school",
            "current_grade": "Lớp 11",
            "graduation_year": 2027,
            "intake": "Fall 2027",
        },
        {"current_level": "college_university", "current_grade": "Năm 1", "intake": "Fall 2027"},
        {"current_level": "high_school", "current_grade": "Lớp 11"},
        {"intake": "Spring 2027"},
    ],
)
def test_no_note_when_intake_fits_or_unknown(values):
    assert early_intake_note(prof(**values), TODAY) is None


def test_review_board_carries_the_note():
    state = ChatState(profile=prof(current_level="high_school", current_grade="Lớp 11", intake="Spring 2020"))
    output = review(state)
    assert output.answer.startswith("Lưu ý:") and "Spring 2020" in output.answer
    # Câu hỏi đang chờ giữ đúng chữ gốc vì SLM đã học câu đó
    assert not state.last_question.startswith("Lưu ý:")
