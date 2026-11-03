from datetime import date

from app.schemas.chat import ChatState, ProfileField
from app.services.ollama import OllamaError
from app.services.skills.skill_lo_trinh import LoTrinhHandler, RoutePlanner, choose_route
from tests.fakes import FakeLLM

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
    route, _, _ = choose_route(p, TODAY)
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
    route, _, _ = choose_route(good(intake="Fall 2026"), TODAY)
    assert (route.route, route.codes[0]) == ("postpone", "DEADLINE_SOON")
    assert route.months_to_deadline < 1


def ai_answer(route, alt=(), reasons=("Hồ sơ của bạn đủ điều kiện nộp thẳng vào đại học.",)):
    return {"route": route, "alt_routes": list(alt), "reasons": list(reasons)}


async def test_ai_reasons_used_when_route_matches():
    llm = FakeLLM(ai_answer("direct"))
    route, _ = await RoutePlanner(llm, "qwen3:8b").plan(good(), TODAY)

    assert route.ai_used is True
    assert route.reasons == ["Hồ sơ của bạn đủ điều kiện nộp thẳng vào đại học."]


# TH-36: Qwen đổi lộ trình hoặc bịa số → gọi lại, vẫn sai thì lộ trình của code và câu mẫu
async def test_th36_wrong_route_or_made_up_number_falls_back(caplog):
    llm = FakeLLM(ai_answer("pathway"), ai_answer("direct", reasons=["Tỉ lệ đỗ của bạn khoảng 90%."]))
    route, _ = await RoutePlanner(llm, "qwen3:8b").plan(good(), TODAY)

    assert len(llm.calls) == 2
    assert (route.route, route.ai_used) == ("direct", False)
    assert route.reasons == ["Hồ sơ của bạn đủ điều kiện nộp thẳng vào đại học."]
    assert "skill_lo_trinh" in caplog.text


async def test_ollama_down_uses_template():
    p = good(budget="tier_under_20k")
    route, _ = await RoutePlanner(FakeLLM(OllamaError("x"), OllamaError("x")), "qwen3:8b").plan(p, TODAY)

    assert route.route == "cc_2plus2"
    assert route.reasons[0].startswith("Ngân sách còn hẹp")


async def test_handler_direct_has_timeline_and_deadline_note():
    state = ChatState(current_step="B4", profile=good())
    out = await LoTrinhHandler(FakeLLM(ai_answer("direct")), "qwen3:8b").run(state, "Có")

    assert "Lộ trình phù hợp với bạn: Đại học thẳng." in out.answer
    assert "Nộp sớm (EA/ED): khoảng tháng 11/2026" in out.answer
    assert "Nộp thường lệ (RD): khoảng tháng 1/2027" in out.answer
    assert "Xin visa: khoảng tháng 5/2027" in out.answer
    assert "kiểm tra hạn nộp chính xác trên trang của từng trường" in out.answer
    assert state.question_id is None


async def test_handler_other_route_says_no_list_and_gives_criteria():
    state = ChatState(current_step="B4", profile=good(budget="tier_under_20k"))
    reasons = ["Ngân sách còn hẹp, 2+2 tiết kiệm hơn."]
    out = await LoTrinhHandler(FakeLLM(ai_answer("cc_2plus2", reasons=reasons)), "qwen3:8b").run(state, "Có")

    assert "Hệ thống chưa có danh sách trường cho lộ trình này" in out.answer
    assert "Có ngành bạn muốn học (Computer Science)" in out.answer
