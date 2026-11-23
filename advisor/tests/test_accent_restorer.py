"""Khôi phục dấu cho câu học sinh gõ không dấu ("opt la gi" -> "OPT là gì"): chỉ dùng để tìm kiếm, và phải chịu được mọi lỗi."""

import pytest

from app.services import usage_meter
from app.services.accent_restorer import PURPOSE, AccentRestorer, fold, needs_restore
from app.services.ollama import OllamaError
from tests.fakes import FakeLLM

# ---------- khi nào cần khôi phục ----------


@pytest.mark.parametrize(
    "text", ["opt la gi", "OPT la gi", "ielts bao nhieu diem la du", "what is OPT", "ko biet"]
)
def test_text_without_any_diacritics_may_need_restoring(text):
    assert needs_restore(text)


@pytest.mark.parametrize(
    "text",
    [
        "OPT là gì",  # đã có dấu
        "đi mỹ",  # chỉ một chữ có dấu cũng đủ: người gõ có dấu
        "",
        "   ",
        "1234 ?!",  # không có chữ cái
        "x" * 301,  # quá dài: không phải câu hỏi thường, không tốn một lần gọi
    ],
)
def test_text_that_already_has_diacritics_or_is_not_a_question_is_left_alone(text):
    assert not needs_restore(text)


def test_fold_ignores_case_diacritics_spaces_and_punctuation():
    assert fold("OPT là gì?") == fold("opt la gi") == "optlagi"
    assert fold("Đại học") == "daihoc"


# ---------- gọi Claude ----------


async def test_adds_diacritics_when_claude_only_adds_them():
    llm = FakeLLM({"text": "OPT là gì?"})

    assert await AccentRestorer(llm).restore("opt la gi") == "OPT là gì?"


async def test_a_message_that_already_has_diacritics_never_calls_claude():
    llm = FakeLLM()

    assert await AccentRestorer(llm).restore("OPT là gì?") is None
    assert llm.calls == []


async def test_sends_claude_only_the_text_with_the_purpose_label():
    seen = {}

    class Llm(FakeLLM):
        async def chat_json(self, messages, schema, temperature=0.0):
            seen["purpose"] = usage_meter._purpose.get()
            seen["user"] = messages[-1]["content"]
            return {"text": "OPT là gì"}

    await AccentRestorer(Llm()).restore("opt la gi")

    assert seen["purpose"] == PURPOSE  # báo cáo token có nhãn riêng, không trống
    assert "opt la gi" in seen["user"]


@pytest.mark.parametrize(
    "answer",
    [
        {"text": "OPT nghĩa là gì vậy"},  # đổi nội dung, không chỉ thêm dấu
        {"text": "Thời tiết hôm nay"},  # trả câu khác hẳn
        {"text": ""},
        {"text": "   "},
        {"text": "opt la gi"},  # không đổi gì: không cần tìm lại
        {"text": 5},
        {"text": None},
        {},
        ["OPT là gì"],
    ],
)
async def test_an_answer_that_is_not_just_the_same_words_with_diacritics_is_discarded(answer):
    assert await AccentRestorer(FakeLLM(answer)).restore("opt la gi") is None


async def test_english_text_that_claude_returns_unchanged_gives_nothing_to_retry():
    assert await AccentRestorer(FakeLLM({"text": "what is OPT"})).restore("what is OPT") is None


@pytest.mark.parametrize("error", [OllamaError("quá tải"), RuntimeError("lỗi lạ"), TimeoutError()])
async def test_any_failure_means_no_restoration_instead_of_a_crash(error):
    assert await AccentRestorer(FakeLLM(error)).restore("opt la gi") is None
