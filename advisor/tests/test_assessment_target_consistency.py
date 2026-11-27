"""Dữ liệu code đưa cho Claude viết đánh giá không được tự mâu thuẫn.

Lỗi thật 27/11/2026: câu mở đầu (so với trường mục tiêu) bảo GPA, SAT, Ngân sách cần cải thiện nhất, trong khi các nhóm
(so với trung bình) xếp GPA, SAT "ở mức giữa", nhóm cần lưu ý trống và ngân sách ghi "mức đủ"."""

from datetime import date

from app.schemas.chat import ChatState, ProfileField
from app.services.skill_input import skill_request_of
from app.services.skill_template import build_skill
from tests.test_skill_template import BENCHMARKS, FULL, TRANSCRIPT
from tests.vef import VEF, VEF_RULES

TODAY = date(2026, 11, 27)


def crit(key, mine, mark, status, type_=None):
    return {"key": key, "type": type_, "mine": mine, "mark": mark, "status": status}


def target(*criteria) -> dict:
    return {
        "mode": "target",
        "summary": "Đạt đủ điều kiện 0/1 trường mục tiêu",
        "schools": [{"name": "MIT", "criteria": list(criteria)}],
        "tasks": [],
        "provinceMissing": False,
    }


def assessment(roadmap) -> list[str]:
    profile = {k: ProfileField(value=v, status="confirmed") for k, v in FULL.items()}
    state = ChatState(
        profile=profile, transcript=TRANSCRIPT, benchmarks=BENCHMARKS, grade_scale=VEF_RULES, roadmap=roadmap
    )
    request = skill_request_of(state, "đánh giá hồ sơ", TODAY)
    skill = build_skill(request, ["assessment"], state.profile, TODAY, VEF, roadmap)
    return skill.split('### Phần "Đánh giá hồ sơ"')[1].split("Cách viết:")[0].splitlines()


def line(lines, start) -> str:
    return next((x for x in lines if x.startswith(start)), "")


def test_what_misses_the_target_is_grouped_as_needing_attention():
    lines = assessment(target(crit("gpa", 3.6, 3.94, "below"), crit("budget", 50000, 97908, "below")))

    attention = line(lines, "- Cần lưu ý nhất:")
    assert "GPA" in attention and "Ngân sách" in attention
    assert (
        attention.count("chưa đạt mốc trường mục tiêu") + attention.count("chưa đủ chi phí trường mục tiêu")
        == 2
    )
    others = " ".join(x for x in lines if x.startswith(("- Điểm mạnh:", "- Ở mức giữa:")))
    assert "GPA" not in others


def test_the_budget_is_not_called_enough_anywhere_else_when_it_misses_the_target():
    lines = assessment(target(crit("budget", 50000, 97908, "below")))

    finance = line(lines, "- Tài chính:")
    assert "ngân sách" not in finance  # đã nói ở nhóm cần lưu ý, kèm so với cái gì
    assert "nguồn" in finance  # phần còn lại của tài chính vẫn giữ
    assert "so với mức chung" in line(lines, "- Cần lưu ý nhất:")


def test_the_groups_match_the_opening_sentence():
    lines = assessment(target(crit("gpa", 3.6, 3.94, "below"), crit("sat", 1300, 1550, "below")))

    opening, attention = line(lines, "Câu mở đầu"), line(lines, "- Cần lưu ý nhất:")
    for name in ("GPA", "SAT"):
        assert name in opening and name in attention


def test_without_a_missed_target_nothing_changes():
    met = assessment(target(crit("gpa", 3.9, 3.8, "met"), crit("budget", 90000, 80000, "met")))

    without = assessment(None)
    assert line(met, "- Tài chính:") == line(without, "- Tài chính:")
    assert "trường mục tiêu" not in line(met, "- Cần lưu ý nhất:")
    assert "ngân sách" in line(met, "- Tài chính:")
