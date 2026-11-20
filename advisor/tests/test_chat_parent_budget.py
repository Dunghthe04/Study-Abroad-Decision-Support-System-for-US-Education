from app.services.skills import buttons, exchange_rate
from app.services.skills.validators import budget_tier
from tests.fakes import FakeLLM
from tests.test_chat_budget import ASKED, PROFILE
from tests.test_chat_skills_flow import field, turn

# UC-94: phụ huynh nhập hộ con managed (exact_budget) thì F1 hỏi số tiền tối đa/năm chính xác bằng VNĐ như form.
# "Chưa biết chính xác" chuyển sang chọn mức (ước tính), "Nhập số tiền chính xác" quay lại ô nhập số


def parent(**extra):
    return {"current_step": "B2", "question_id": "F1", "asked": ASKED, "exact_budget": True, **extra}


async def test_exact_vnd_saved_with_tier_without_qwen():
    llm = FakeLLM()
    res = await turn(llm, "1,3 tỷ", profile=dict(PROFILE, budget_usd=field(40000)), **parent())

    assert llm.calls == []
    assert res.state.profile["budget_vnd"] == field(1_300_000_000)
    assert res.state.profile["budget"] == field(budget_tier(1_300_000_000 / exchange_rate.vnd_per_usd()))
    assert "budget_usd" not in res.state.profile  # số USD cũ bỏ, BE đổi lại từ số VNĐ mới
    assert res.state.question_id != "F1"


async def test_out_of_range_is_asked_again():
    res = await turn(FakeLLM(), "50 triệu", profile=dict(PROFILE), **parent())

    assert "budget_vnd" not in res.state.profile
    assert res.state.question_id == "F1"
    assert "100 triệu đến 10 tỷ" in res.answer
    assert res.widget is not None and res.widget.data["exact"] is True


async def test_usd_is_asked_again_in_vnd():
    res = await turn(FakeLLM(), "50000 USD", profile=dict(PROFILE), **parent())

    assert "budget_vnd" not in res.state.profile
    assert "VNĐ" in res.answer


async def test_unsure_switches_to_tiers_then_tier_is_estimate():
    res = await turn(FakeLLM(), buttons.BUDGET_UNSURE, profile=dict(PROFILE), **parent())

    assert res.state.budget_unsure is True
    assert res.state.question_id == "F1"
    labels = [q.label for q in res.quick_replies]
    assert buttons.BUDGET_EXACT in labels
    assert "Bỏ qua" not in labels
    assert "ước tính" in res.answer

    tier_label = next(b.label for b in buttons.BUTTONS["F1"])
    profile = dict(PROFILE, budget_vnd=field(900_000_000), budget_usd=field(36000))
    res = await turn(FakeLLM(), tier_label, profile=profile, **parent(budget_unsure=True))

    assert "budget" in res.state.profile
    assert "budget_vnd" not in res.state.profile
    assert "budget_usd" not in res.state.profile


async def test_exact_button_returns_to_amount_input():
    res = await turn(FakeLLM(), buttons.BUDGET_EXACT, profile=dict(PROFILE), **parent(budget_unsure=True))

    assert res.state.budget_unsure is False
    assert res.widget is not None and res.widget.data["exact"] is True
    assert [q.label for q in res.quick_replies] == [buttons.BUDGET_UNSURE]


async def test_student_keeps_tier_question():
    res = await turn(FakeLLM(), "800 triệu", current_step="B2", question_id="F1", asked=ASKED, profile=dict(PROFILE))

    assert res.state.profile["budget"] == field("tier_20k_35k")
    assert "budget_vnd" not in res.state.profile
