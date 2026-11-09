import json

import pytest

from app.schemas.chat import ResultSchool
from app.schemas.slm import SkillRequest
from app.services.advice_writer import AdviceWriter, check_answer
from app.services.claude import ClaudeError
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
    llm = FakeLLM(ClaudeError("quá tải"), {"tra_loi": ""})

    assert await AdviceWriter(llm).write(SKILL, REQ, []) is None
    assert len(llm.calls) == 2


def test_promise_error_names_only_the_matched_phrase():
    with pytest.raises(ValueError) as e:
        check_answer({"tra_loi": "GPA 8.5 của bạn tốt, chắc chắn đỗ Iowa."})

    # Chỉ nêu cụm khớp bộ lọc, không chép câu trả lời (có số liệu hồ sơ của học sinh) vào lỗi
    assert str(e.value) == 'câu trả lời hứa đỗ: "chac chan do"'


async def test_each_failed_attempt_is_logged_with_its_reason(caplog):
    llm = FakeLLM(ClaudeError("HTTP 529"), {"tra_loi": "Chắc chắn đỗ!"})

    with caplog.at_level("WARNING", logger="app.services.advice_writer"):
        assert await AdviceWriter(llm).write(SKILL, REQ, []) is None

    assert [r.getMessage() for r in caplog.records] == [
        "Writer hỏng lần 1/2: HTTP 529",
        'Writer hỏng lần 2/2: câu trả lời hứa đỗ: "chac chan do"',
    ]


async def test_a_good_answer_logs_nothing(caplog):
    llm = FakeLLM({"tra_loi": "Bạn có cơ hội tốt."})

    with caplog.at_level("WARNING", logger="app.services.advice_writer"):
        await AdviceWriter(llm).write(SKILL, REQ, [])

    assert caplog.records == []
