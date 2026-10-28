"""Việc B5 (Phần C): skill_tra_loi trả lời câu hỏi kiến thức từ kho, có nguồn, không bịa."""

import pytest

from app.schemas.chat import ChatMessage, ChatRequest, ChatState
from app.services.chat_flow import ChatFlow
from app.services.knowledge import KnowledgeHit
from app.services.ollama import OllamaError
from app.services.skill_rules import ORDER
from app.services.skills import build_handlers
from app.services.skills.skill_tra_loi import (
    DISCLAIMER,
    NO_BASIS,
    UNAVAILABLE,
    TraLoiHandler,
    check_answer,
    citation_of,
    numbers_in,
)
from tests.fakes import FakeLLM

SEVIS = "Phí SEVIS I-901 là 350 USD cho diện F-1, nộp một lần trước khi phỏng vấn visa."
FINANCE = (
    "Cần giấy xác nhận số dư tài khoản có giá trị tối thiểu bằng chi phí năm đầu, khoảng 4.000 USD mỗi tháng."
)


def hit(code="KB-001", content=SEVIS, score=0.9, quote="8 CFR 214.13", url=None, note="Nguồn A"):
    return KnowledgeHit(code, "câu hỏi mẫu", content, quote, url, note, None, score)


class FakeLLMWithEmbed(FakeLLM):
    """LLM giả có thêm embed: trả vector cố định hoặc ném lỗi."""

    def __init__(self, *answers, embed_error: Exception | None = None) -> None:
        super().__init__(*answers)
        self.embedded: list[list[str]] = []
        self._embed_error = embed_error

    async def embed(self, texts):
        self.embedded.append(texts)
        if self._embed_error:
            raise self._embed_error
        return [[0.1, 0.2] for _ in texts]


class FakeRetriever:
    def __init__(self, hits=None, error: Exception | None = None) -> None:
        self._hits = hits or []
        self._error = error
        self.calls: list[tuple] = []

    async def search(self, embedding, study_level=None):
        self.calls.append((embedding, study_level))
        if self._error:
            raise self._error
        return self._hits


def handler_with(llm, retriever, min_score=0.72):
    return TraLoiHandler(llm, "qwen3:8b", retriever, min_score)


# Câu Qwen viết lại đủ ý của SEVIS
REWRITTEN = "Phí SEVIS I-901 cho diện F-1 là 350 USD, bạn nộp một lần trước khi phỏng vấn visa."


def good(answer=REWRITTEN):
    return {"enough": True, "answer": answer}


# ---------- đọc số trong câu ----------


def test_numbers_in_treats_thousand_separators_and_decimal_commas_the_same():
    assert numbers_in("Cần 4.000 USD") == numbers_in("Cần 4000 USD") == {"4000"}
    assert numbers_in("IELTS 6,5") == numbers_in("IELTS 6.5") == {"6.5"}


# ---------- kiểm tra câu trả lời của Qwen ----------


def test_check_answer_accepts_a_grounded_answer():
    result = check_answer(good(), "Phí SEVIS bao nhiêu?", hit())

    assert result.enough and result.answer == REWRITTEN


def test_check_answer_not_enough_needs_no_checking():
    result = check_answer({"enough": False, "answer": ""}, "q", hit())

    assert not result.enough


@pytest.mark.parametrize(
    "raw",
    [
        good(answer=""),  # nói đủ nhưng không có câu trả lời
        good(answer="Chắc chắn " + REWRITTEN),  # hứa kết quả
        good(answer=REWRITTEN.replace("350", "380")),  # số không có trong nội dung, và mất số 350
        good(
            answer="Phí SEVIS I-901 cho diện F-1, bạn nộp một lần trước khi phỏng vấn visa nhé."
        ),  # bỏ mất con số 350
        good(
            answer="Phí SEVIS I-901 là 350 USD."
        ),  # ngắn hơn nhiều: bỏ ý "diện F-1, nộp một lần trước phỏng vấn"
    ],
)
def test_check_answer_rejects_bad_answers(raw):
    with pytest.raises(ValueError):
        check_answer(raw, "Phí SEVIS bao nhiêu?", hit())


def test_check_answer_removes_source_markers_the_model_still_writes():
    raw = good(
        answer="Phí SEVIS I-901 cho diện F-1 là 350 USD [1]. Bạn nộp một lần trước khi phỏng vấn visa [2]."
    )

    result = check_answer(raw, "q", hit())

    assert (
        result.answer == "Phí SEVIS I-901 cho diện F-1 là 350 USD. Bạn nộp một lần trước khi phỏng vấn visa."
    )  # người dùng không phải nhìn [1] [2]


def test_check_answer_does_not_count_a_marker_number_as_a_made_up_figure():
    # [7] không có trong nội dung, nhưng chỉ là dấu nguồn nên bị bỏ chứ không bị coi là số bịa
    assert check_answer(good(answer=REWRITTEN.replace("USD,", "USD [7],")), "q", hit()).enough


def test_check_answer_allows_numbers_from_the_question():
    raw = good(
        answer="Dù học 3 năm, phí SEVIS I-901 cho diện F-1 vẫn là 350 USD, nộp một lần trước khi phỏng vấn visa."
    )

    assert check_answer(raw, "Nếu học 3 năm thì phí SEVIS bao nhiêu?", hit()).enough


def test_check_answer_allows_thousand_separator_written_either_way():
    raw = good(
        answer="Bạn cần giấy xác nhận số dư tài khoản tối thiểu bằng chi phí năm đầu, khoảng 4000 USD mỗi tháng."
    )

    assert check_answer(raw, "Cần chứng minh bao nhiêu?", hit(content=FINANCE)).enough


def test_check_answer_does_not_require_list_numbering_to_be_kept():
    content = "Hai yếu tố: (1) năng lực học thuật; (2) năng lực tài chính cho năm học đầu tiên."
    raw = good(
        answer="Có hai yếu tố: thứ nhất là năng lực học thuật, thứ hai là năng lực tài chính cho năm học đầu tiên."
    )

    assert check_answer(raw, "q", hit(content=content)).enough  # "(1)", "(2)" viết thành "thứ nhất" vẫn đủ ý


# ---------- nguồn trích dẫn ----------


def test_citation_prefers_the_quote_then_the_note_then_a_generic_title():
    assert citation_of(hit(quote="8 CFR 214.13")).title == "8 CFR 214.13"
    assert citation_of(hit(quote=None, note="Nguồn A")).title == "Nguồn A"
    assert citation_of(hit(quote=None, note=None)).title == "Kho kiến thức tư vấn du học"


def test_citation_snippet_is_cut_short():
    assert len(citation_of(hit(content="x" * 500)).snippet) == 200


# ---------- handler ----------


async def test_answers_with_one_citation_and_disclaimer():
    llm = FakeLLMWithEmbed(good())
    retriever = FakeRetriever([hit(url="https://example.org/sevis")])

    out = await handler_with(llm, retriever).run(ChatState(), "Phí SEVIS bao nhiêu?")

    assert out.answer == REWRITTEN  # không còn dấu [1]
    assert [c.title for c in out.citations] == ["8 CFR 214.13"]
    assert out.citations[0].url == "https://example.org/sevis"
    assert out.disclaimer == DISCLAIMER
    assert llm.embedded == [["Phí SEVIS bao nhiêu?"]]


async def test_sends_qwen_only_the_best_passage_even_when_others_pass_the_threshold():
    # dòng 2 (0.83) cũng đạt ngưỡng nhưng là chuyện khác: đưa cho Qwen thì Qwen trộn vào câu trả lời
    llm = FakeLLMWithEmbed(good())
    retriever = FakeRetriever([hit(score=1.0), hit("KB-069", FINANCE, score=0.83)])

    out = await handler_with(llm, retriever).run(ChatState(), "Phí SEVIS bao nhiêu?")

    user = llm.calls[0]["messages"][1]["content"]
    assert "350 USD" in user and "4.000" not in user
    assert [c.title for c in out.citations] == ["8 CFR 214.13"]  # và chỉ một nguồn


async def test_refuses_without_calling_qwen_when_the_best_passage_is_below_the_threshold():
    llm = FakeLLMWithEmbed()
    retriever = FakeRetriever([hit(score=0.5)])

    out = await handler_with(llm, retriever).run(ChatState(), "Thời tiết Hà Nội?")

    assert out.answer == NO_BASIS and not out.citations and out.disclaimer is None
    assert llm.calls == []  # không gọi Qwen thì Qwen không có cơ hội bịa


async def test_refuses_when_the_store_returns_nothing():
    out = await handler_with(FakeLLMWithEmbed(), FakeRetriever([])).run(ChatState(), "q")

    assert out.answer == NO_BASIS


async def test_refuses_when_qwen_says_the_passage_is_not_enough():
    llm = FakeLLMWithEmbed({"enough": False, "answer": ""})

    out = await handler_with(llm, FakeRetriever([hit()])).run(ChatState(), "q")

    assert out.answer == NO_BASIS and not out.citations


async def test_retries_once_then_uses_the_checked_second_answer():
    llm = FakeLLMWithEmbed(good(answer=REWRITTEN.replace("350", "999")), good())

    out = await handler_with(llm, FakeRetriever([hit()])).run(ChatState(), "Phí SEVIS bao nhiêu?")

    assert out.answer == REWRITTEN
    assert len(llm.calls) == 2


async def test_falls_back_to_the_standard_answer_when_qwen_is_wrong_twice():
    llm = FakeLLMWithEmbed(good(answer="Phí là 999 USD."), good(answer="Phí là 888 USD."))

    out = await handler_with(llm, FakeRetriever([hit()])).run(ChatState(), "Phí SEVIS bao nhiêu?")

    assert out.answer == SEVIS  # nguyên văn câu trả lời chuẩn, không phải lời Qwen, không có [1]
    assert [c.title for c in out.citations] == ["8 CFR 214.13"]


async def test_logs_why_it_fell_back_to_the_standard_answer(caplog):
    llm = FakeLLMWithEmbed(good(answer="Phí là 999 USD."), good(answer="Phí là 888 USD."))

    with caplog.at_level("WARNING", logger="advisor.knowledge"):
        await handler_with(llm, FakeRetriever([hit()])).run(ChatState(), "Phí SEVIS bao nhiêu?")

    assert "KB-001" in caplog.text  # biết dòng nào được dùng làm câu chuẩn khi Qwen không dùng được


async def test_falls_back_when_qwen_is_unreachable_twice():
    llm = FakeLLMWithEmbed(OllamaError("tắt"), OllamaError("tắt"))

    out = await handler_with(llm, FakeRetriever([hit()])).run(ChatState(), "q")

    assert out.answer == SEVIS


async def test_says_service_is_unavailable_when_embedding_fails():
    llm = FakeLLMWithEmbed(embed_error=OllamaError("Ollama tắt"))

    out = await handler_with(llm, FakeRetriever([hit()])).run(ChatState(), "q")

    assert out.answer == UNAVAILABLE and not out.citations
    assert llm.calls == []


async def test_says_service_is_unavailable_when_the_database_fails():
    out = await handler_with(FakeLLMWithEmbed(), FakeRetriever(error=RuntimeError("DB lỗi"))).run(
        ChatState(), "q"
    )

    assert out.answer == UNAVAILABLE


async def test_passes_the_study_level_to_the_search():
    retriever = FakeRetriever([])

    await handler_with(FakeLLMWithEmbed(), retriever).run(ChatState(study_level="master"), "q")

    assert retriever.calls == [([0.1, 0.2], "master")]


# ---------- đăng ký và luồng chat ----------


def test_skill_tra_loi_is_registered_only_when_the_knowledge_store_exists():
    assert "skill_tra_loi" not in build_handlers(llm=object(), model="m")

    handlers = build_handlers(llm=object(), model="m", knowledge=FakeRetriever())

    assert "skill_tra_loi" in handlers and "skill_tra_loi" in ORDER


class PickerOf:
    def __init__(self, *skills: str) -> None:
        self._skills = list(skills)

    async def predict(self, step, last_question, message, missing):
        class Result:
            skills = self._skills
            probs = {s: 0.9 for s in self._skills}

        return Result()


async def test_chat_returns_the_citations_and_disclaimer_to_the_client():
    llm = FakeLLMWithEmbed(good())
    flow = ChatFlow(
        PickerOf("skill_tra_loi"),
        build_handlers(llm, "m", knowledge=FakeRetriever([hit(url="https://example.org/sevis")])),
    )
    req = ChatRequest(
        messages=[ChatMessage(role="user", content="Phí SEVIS bao nhiêu?")],
        state=ChatState(current_step="B5"),
    )

    res = await flow.reply(req)

    assert res.answer == REWRITTEN
    assert [c.url for c in res.citations] == ["https://example.org/sevis"]
    assert res.disclaimer == DISCLAIMER


async def test_chat_remembers_the_study_level_sent_by_the_client():
    retriever = FakeRetriever([])
    flow = ChatFlow(PickerOf("skill_tra_loi"), build_handlers(FakeLLMWithEmbed(), "m", knowledge=retriever))
    first = ChatRequest(
        messages=[ChatMessage(role="user", content="Phí SEVIS bao nhiêu?")],
        state=ChatState(current_step="B5"),
        study_level="undergraduate",
    )

    res = await flow.reply(first)
    # lượt sau .NET không gửi bậc học: vẫn dùng bậc đã nhớ trong state
    await flow.reply(
        ChatRequest(messages=[ChatMessage(role="user", content="Còn visa thì sao?")], state=res.state)
    )

    assert [call[1] for call in retriever.calls] == ["undergraduate", "undergraduate"]
