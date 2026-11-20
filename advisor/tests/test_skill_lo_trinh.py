from datetime import date

from app.schemas.chat import ProfileField
from app.services.skills.skill_lo_trinh import (
    RECOMMEND_ACTION,
    action_for,
    choose_route,
    render,
    template_reasons,
)
from tests.vef import VEF

TODAY = date(2026, 10, 28)


def profile(**values):
    return {name: ProfileField(value=value, status="confirmed") for name, value in values.items()}


def good(**changes):
    values = {
        "gpa": {"value": 9.0, "scale": 10},
        "english": {"type": "ielts", "value": 7.0, "date": None},
        "budget": "tier_above_70k",
        "major": "Computer Science",
        "intake": "Fall 2027",
        **changes,
    }
    return profile(**{k: v for k, v in values.items() if v is not None})


def route_of(p):
    route, _, _ = choose_route(p, TODAY, VEF)
    return route.route, route.alt_routes


# TH-32: hồ sơ đủ mạnh → đại học thẳng
def test_th32_direct():
    assert route_of(good()) == ("direct", [])


# TH-33: tiếng Anh thấp → dự bị; chưa thi mà không kịp thi cũng là dự bị
def test_th33_english_low_pathway():
    assert route_of(good(english={"type": "ielts", "value": 5.0})) == ("pathway", [])
    assert route_of(good(english=None, intake="Fall 2027"))[0] == "direct"  # còn 15 tháng, kịp thi
    no_time = good(english=None, intake="Spring 2027")  # hạn 10/2026 đã qua
    assert route_of(no_time) == ("postpone", ["pathway"])
    late_plan = good(english=None, english_plan={"planned": True, "date": "2027-12"})
    assert route_of(late_plan) == ("pathway", [])


# TH-34: tài chính thiếu hoặc học lực yếu → 2+2; luật đúng khác là lộ trình thay thế
def test_th34_finance_or_academic_cc():
    assert route_of(good(budget="tier_under_20k")) == ("cc_2plus2", [])
    assert route_of(good(gpa={"value": 6.5, "scale": 10})) == ("cc_2plus2", [])
    both = good(english={"type": "ielts", "value": 5.0}, budget="tier_under_20k")
    assert route_of(both) == ("pathway", ["cc_2plus2"])


# TH-35: sát hạn nộp → dời kỳ
def test_th35_deadline_soon_postpone():
    route, _, _ = choose_route(good(intake="Fall 2026"), TODAY, VEF)
    assert (route.route, route.codes[0]) == ("postpone", "DEADLINE_SOON")
    assert route.months_to_deadline < 1


def planned(p):
    route, _, intake = choose_route(p, TODAY, VEF)
    route.reasons = template_reasons(route)
    return route, intake


def test_template_reasons_name_the_cause_of_the_route():
    route, _ = planned(good(budget="tier_under_20k"))

    assert route.route == "cc_2plus2"
    assert route.reasons[0].startswith("Ngân sách còn hẹp")


def test_direct_route_text_has_timeline_and_deadline_note():
    route, intake = planned(good())
    text = render(route, intake, "Computer Science")

    assert "Lộ trình phù hợp với bạn: Đại học thẳng." in text
    assert "Hồ sơ của bạn đủ điều kiện nộp thẳng vào đại học." in text
    assert "Nộp sớm (EA/ED): khoảng tháng 11/2026" in text
    assert "Nộp thường lệ (RD): khoảng tháng 1/2027" in text
    assert "Xin visa: khoảng tháng 5/2027" in text
    assert "kiểm tra hạn nộp chính xác trên trang của từng trường" in text


def test_other_route_text_says_no_list_and_gives_criteria():
    route, intake = planned(good(budget="tier_under_20k"))
    text = render(route, intake, "Computer Science")

    assert "Hệ thống chưa có danh sách trường cho lộ trình này" in text
    assert "Có ngành bạn muốn học (Computer Science)" in text


def test_only_the_direct_route_leads_to_the_school_list():
    direct, _ = planned(good())
    other, _ = planned(good(budget="tier_under_20k"))

    assert action_for(direct) == RECOMMEND_ACTION
    assert action_for(other) is None
