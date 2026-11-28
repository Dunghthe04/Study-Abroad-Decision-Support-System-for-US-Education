from app.services.skills.skill_ho_so import QUESTION_FIELDS
from tests.fakes import FakeLLM
from tests.test_chat_skills_flow import PAGE1_ASKED, field, turn

# F1: học sinh gõ số tiền thì code tự quy ra mức như form, không gọi Qwen

PROFILE = {"gpa": field({"value": 8.5, "scale": 10}), "english": field({"type": "ielts", "value": 6.5})}
ASKED = {**PAGE1_ASKED, "D2": 1, "E1": 1, "E2": 1, "D3": 1, "F1": 1}


async def test_vnd_text_saves_tier_without_qwen():
    llm = FakeLLM()
    res = await turn(llm, "800 triệu", current_step="B2", question_id="F1", asked=ASKED, profile=PROFILE)

    assert llm.calls == []
    assert res.state.profile["budget"] == field("tier_20k_35k")
    assert res.state.question_id != "F1"


# Số học sinh nói là mức tối đa gia đình chi được (đổi 27/11/2026; trước đây xếp sang mức trên như ô số của phụ huynh)
async def test_amount_at_tier_ceiling_stays_in_that_range():
    res = await turn(
        FakeLLM(), "35.000 USD", current_step="B2", question_id="F1", asked=ASKED, profile=PROFILE
    )

    assert res.state.profile["budget"].value == "tier_20k_35k"


async def test_huge_usd_is_asked_again():
    res = await turn(
        FakeLLM(), "800000000 USD", current_step="B2", question_id="F1", asked=ASKED, profile=PROFILE
    )

    assert "budget" not in res.state.profile
    assert res.state.question_id == "F1"
    assert "USD hay VNĐ" in res.answer


def test_f1_asks_budget():
    assert QUESTION_FIELDS["F1"] == ["budget"]
