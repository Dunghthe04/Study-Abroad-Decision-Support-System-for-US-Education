import json

import pytest

from app.schemas.chat import ResultSchool
from app.schemas.slm import SkillRequest
from app.services.advice_writer import AdviceWriter, check_answer
from app.services.gemini import GeminiError
from tests.fakes import FakeLLM

REQ = SkillRequest(
    loai="ho_so",
    cau_hoi="Đánh giá hồ sơ giúp em",
    ho_so={"gpa": "8.5", "english": "IELTS 6.5"},
    tinh_san={"gpa_level": "3.4 thang 4, trung bình (TB các trường 3.6)"},
)
SKILL = "# Mốc: GPA trung bình\n\n## 1. Nhiệm vụ\nĐánh giá hồ sơ."


async def test_rules_and_skill_go_to_system_and_the_profile_goes_to_user():
    llm = FakeLLM({"tra_loi": "Hồ sơ của bạn ở mức trung bình."})

    answer = await AdviceWriter(llm).write(SKILL, REQ, [ResultSchool(code="1", name="Iowa", group="M")])

    assert answer == "Hồ sơ của bạn ở mức trung bình."
    rules, skill, user = llm.calls[0]["messages"]
    assert "VIẾT TRẢ LỜI HỒ SƠ" in rules["content"] and rules["role"] == "system"
    assert skill == {"role": "system", "content": SKILL}
    assert json.loads(user["content"]) == {
        "cau_hoi": REQ.cau_hoi,
        "ho_so": REQ.ho_so,
        "tinh_san": REQ.tinh_san,
        "schools": [{"name": "Iowa", "group": "Vừa sức"}],
    }


@pytest.mark.parametrize(
    "text", ["", "   ", "Bạn chắc chắn đỗ Iowa.", "Mình đảm bảo đậu nhé", "You are guaranteed"]
)
def test_empty_or_promising_answers_are_rejected(text):
    with pytest.raises(ValueError):
        check_answer({"tra_loi": text})


async def test_a_promise_is_retried_once():
    llm = FakeLLM({"tra_loi": "Chắc chắn đỗ!"}, {"tra_loi": "Bạn có cơ hội tốt."})

    assert await AdviceWriter(llm).write(SKILL, REQ, []) == "Bạn có cơ hội tốt."
    assert len(llm.calls) == 2


async def test_wrong_twice_gives_none():
    llm = FakeLLM(GeminiError("quá tải"), {"tra_loi": ""})

    assert await AdviceWriter(llm).write(SKILL, REQ, []) is None
    assert len(llm.calls) == 2
