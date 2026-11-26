"""Lộ trình theo trường mục tiêu trong chat: roadmap .NET gửi chỉ được đổi ra chữ (cùng số với Tổng quan),
hướng đi xét theo trường đòi hỏi cao nhất, chat nêu việc gấp và chỉ sang /dashboard#lo-trinh."""

from datetime import date

from app.schemas.chat import ChatState, ProfileField
from app.services.profile_calc import calc_profile, target_schools, urgent_tasks
from app.services.skill_input import skill_request_of
from app.services.skill_template import ROADMAP_LINK, build_skill
from app.services.skills.skill_lo_trinh import choose_route, template_reasons
from tests.vef import VEF

TODAY = date(2026, 11, 26)


def prof(**values) -> dict[str, ProfileField]:
    return {k: ProfileField(value=v, status="confirmed") for k, v in values.items()}


def good() -> dict[str, ProfileField]:
    return prof(
        gpa={"value": 9.0, "scale": 10},
        english={"type": "ielts", "value": 7.0, "date": None},
        budget="tier_above_70k",
        major="Computer Science",
        intake="Fall 2027",
    )


def crit(key, mine, mark, status, type_=None):
    return {"key": key, "type": type_, "mine": mine, "mark": mark, "status": status}


def roadmap(schools=None, tasks=None, mode="target", province_missing=False):
    return {
        "mode": mode,
        "summary": "Đạt đủ điều kiện 1/2 trường mục tiêu",
        "schools": schools or [],
        "tasks": tasks or [],
        "provinceMissing": province_missing,
    }


SCHOOLS = [
    {
        "name": "Trường A",
        "criteria": [crit("gpa", 3.6, 3.8, "near"), crit("english", 7, 6.5, "met", "ielts")],
    },
    {
        "name": "Trường B",
        "criteria": [crit("budget", 60000, 55000, "met"), crit("sat", None, 1400, "unknown")],
    },
]


# ---- profile_calc: chỉ đổi ra chữ ----


def test_target_schools_text_per_school():
    text = target_schools(roadmap(SCHOOLS))
    assert text.startswith("Đạt đủ điều kiện 1/2 trường mục tiêu; ")
    assert "Trường A: GPA 3.6 so với mốc trường 3.8, gần đạt, IELTS 7 so với mốc trường 6.5, đạt" in text
    assert (
        "Trường B: Ngân sách 60.000 USD/năm so với chi phí 55.000 USD/năm, đạt, SAT chưa đủ dữ liệu để so"
        in text
    )


def test_target_schools_only_in_target_mode():
    assert target_schools(roadmap(SCHOOLS, mode="average")) is None
    assert target_schools(roadmap([])) is None
    assert target_schools(None) is None


def test_urgent_tasks_overdue_first_then_by_due_max_two():
    tasks = [
        {"label": "Thi IELTS", "due": "2027-03-01", "status": "todo"},
        {"label": "Viết luận", "due": "2027-01-15", "status": "overdue"},
        {"label": "Xin visa", "due": "2027-05-01", "status": "todo"},
        {"label": "Có điểm SAT", "due": None, "status": "done"},
    ]
    assert (
        urgent_tasks(roadmap(tasks=tasks))
        == "Viết luận (hạn 01/2027, quá hạn); Thi IELTS (hạn 03/2027, chưa xong)"
    )


def test_urgent_tasks_none_when_all_done():
    assert urgent_tasks(roadmap(tasks=[{"label": "X", "due": None, "status": "done"}])) is None
    assert urgent_tasks(None) is None


def test_calc_profile_carries_roadmap_keys():
    out = calc_profile(
        {}, None, TODAY, roadmap=roadmap(SCHOOLS, [{"label": "Thi IELTS", "due": None, "status": "todo"}])
    )
    assert set(out) == {"target_schools", "urgent_tasks"}
    assert out["urgent_tasks"] == "Thi IELTS (chưa xong)"


# ---- skill_lo_trinh: xét theo trường đòi hỏi cao nhất ----


def route_with(rm):
    route, _, _ = choose_route(good(), TODAY, VEF, rm)
    return route


def test_choose_route_english_below_any_target_gives_pathway():
    rm = roadmap([{"name": "A", "criteria": [crit("english", 7, 7.5, "below", "ielts")]}])
    route = route_with(rm)
    assert route.route == "pathway"
    assert route.target
    assert "mốc của trường mục tiêu" in template_reasons(route)[0]


def test_choose_route_gpa_below_target_gives_cc():
    rm = roadmap([{"name": "A", "criteria": [crit("gpa", 3.6, 3.9, "below")]}])
    assert route_with(rm).route == "cc_2plus2"


def test_choose_route_met_or_near_keeps_direct():
    rm = roadmap(
        [{"name": "A", "criteria": [crit("gpa", 3.6, 3.7, "near"), crit("english", 7, 6.5, "met", "ielts")]}]
    )
    route = route_with(rm)
    assert route.route == "direct"


def test_choose_route_average_mode_ignores_roadmap():
    rm = roadmap([{"name": "A", "criteria": [crit("english", 7, 7.5, "below", "ielts")]}], mode="average")
    route = route_with(rm)
    assert route.route == "direct"
    assert not route.target


def test_choose_route_weak_english_becomes_good_when_targets_met():
    weak = prof(
        **{
            **{k: f.value for k, f in good().items()},
            "english": {"type": "ielts", "value": 5.0, "date": None},
        }
    )
    assert choose_route(weak, TODAY, VEF)[0].route == "pathway"
    rm = roadmap([{"name": "CC", "criteria": [crit("english", 5, 5, "met", "ielts")]}])
    assert choose_route(weak, TODAY, VEF, rm)[0].route == "direct"


# ---- skill_template: dòng trường mục tiêu, việc gấp, link Tổng quan ----


def skill_of(rm, tasks):
    state = ChatState(profile=good(), roadmap=rm)
    req = skill_request_of(state, "đánh giá và lộ trình", TODAY)
    return build_skill(req, tasks, state.profile, TODAY, None, rm)


def test_skill_has_target_line_urgent_and_link():
    rm = roadmap(
        SCHOOLS, [{"label": "Thi IELTS", "due": "2027-03-01", "status": "todo"}], province_missing=True
    )
    skill = skill_of(rm, ["assessment", "roadmap"])
    assert "- So với trường mục tiêu: Đạt đủ điều kiện 1/2 trường mục tiêu;" in skill
    assert "Việc gấp nhất lúc này (nêu đúng các việc này): Thi IELTS (hạn 03/2027, chưa xong)." in skill
    assert ROADMAP_LINK in skill
    assert "chưa chọn tỉnh / thành" not in skill  # chat hỏi tỉnh sau câu trả lời (skill_tinh)


def test_skill_without_roadmap_has_no_roadmap_lines():
    skill = skill_of(None, ["assessment", "roadmap"])
    assert "trường mục tiêu:" not in skill
    assert ROADMAP_LINK not in skill
    assert "Việc gấp nhất" not in skill


# ---- chat_flow: roadmap từ .NET vào state ----


def test_apply_turn_inputs_keeps_roadmap():
    from app.schemas.chat import TurnInputs
    from app.services.chat_flow import apply_turn_inputs

    state = ChatState()
    rm = roadmap(SCHOOLS)
    apply_turn_inputs(state, TurnInputs(roadmap=rm))
    assert state.roadmap == rm
    apply_turn_inputs(state, TurnInputs())  # lượt không gửi roadmap thì giữ cái cũ
    assert state.roadmap == rm
