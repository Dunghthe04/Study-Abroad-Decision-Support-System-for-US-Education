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


def good(answer="Phí SEVIS là 350 USD [1].", used=(1,)):
    return {"enough": True, "answer": answer, "used": list(used)}


# ---------- đọc số trong câu ----------


def test_numbers_in_treats_thousand_separators_and_decimal_commas_the_same():
    assert numbers_in("Cần 4.000 USD") == numbers_in("Cần 4000 USD") == {"4000"}
    assert numbers_in("IELTS 6,5") == numbers_in("IELTS 6.5") == {"6.5"}


def test_numbers_in_ignores_the_source_markers():
    assert numbers_in("Phí là 350 USD [1] và [2].") == {"350"}


# ---------- kiểm tra câu trả lời của Qwen ----------


def test_check_answer_accepts_a_grounded_answer():
    result = check_answer(good(), "Phí SEVIS bao nhiêu?", [hit()])

    assert result.enough and result.used == [1]


def test_check_answer_not_enough_needs_no_checking():
    result = check_answer({"enough": False, "answer": "", "used": []}, "q", [hit()])

    assert not result.enough


@pytest.mark.parametrize(
    "raw",
    [
        good(answer=""),  # nói đủ nhưng không có câu trả lời
        good(answer="Phí SEVIS là 350 USD."),  # không ghi nguồn
        good(answer="Phí SEVIS là 350 USD [3].", used=(3,)),  # đoạn 3 không tồn tại
        good(used=()),  # không nói đã dùng đoạn nào
        good(answer="Chắc chắn phí SEVIS là 350 USD [1]."),  # hứa kết quả
        good(answer="Phí SEVIS là 380 USD [1]."),  # số không có trong đoạn
    ],
)
def test_check_answer_rejects_bad_answers(raw):
    with pytest.raises(ValueError):
        check_answer(raw, "Phí SEVIS bao nhiêu?", [hit()])


def test_check_answer_allows_numbers_from_the_question():
    raw = good(answer="Với 3 năm học thì phí SEVIS vẫn là 350 USD [1].")

    assert check_answer(raw, "Nếu học 3 năm thì phí SEVIS bao nhiêu?", [hit()]).enough


def test_check_answer_allows_thousand_separator_written_either_way():
    raw = good(answer="Khoảng 4000 USD mỗi tháng [1].")

    assert check_answer(raw, "Cần chứng minh bao nhiêu?", [hit(content=FINANCE)]).enough


def test_check_answer_adds_cited_markers_to_used():
    two = [hit(), hit("KB-002", FINANCE)]
    raw = good(answer="Phí là 350 USD [1], cần khoảng 4.000 USD [2].", used=(1,))

    assert check_answer(raw, "q", two).used == [1, 2]


# ---------- nguồn trích dẫn ----------


def test_citation_prefers_the_quote_then_the_note_then_a_generic_title():
    assert citation_of(hit(quote="8 CFR 214.13")).title == "8 CFR 214.13"
    assert citation_of(hit(quote=None, note="Nguồn A")).title == "Nguồn A"
    assert citation_of(hit(quote=None, note=None)).title == "Kho kiến thức tư vấn du học"


def test_citation_snippet_is_cut_short():
    assert len(citation_of(hit(content="x" * 500)).snippet) == 200


# ---------- handler ----------


async def test_answers_with_citation_and_disclaimer():
    llm = FakeLLMWithEmbed(good())
    retriever = FakeRetriever([hit(url="https://example.org/sevis")])

    out = await handler_with(llm, retriever).run(ChatState(), "Phí SEVIS bao nhiêu?")

    assert out.answer == "Phí SEVIS là 350 USD [1]."
    assert [c.title for c in out.citations] == ["8 CFR 214.13"]
    assert out.citations[0].url == "https://example.org/sevis"
    assert out.disclaimer == DISCLAIMER
    assert llm.embedded == [["Phí SEVIS bao nhiêu?"]]


async def test_sends_qwen_only_the_passages_above_the_threshold():
    llm = FakeLLMWithEmbed(good())
    retriever = FakeRetriever([hit(score=0.9), hit("KB-002", FINANCE, score=0.5)])

    await handler_with(llm, retriever).run(ChatState(), "Phí SEVIS bao nhiêu?")

    user = llm.calls[0]["messages"][1]["content"]
    assert "350 USD" in user and "4.000" not in user  # đoạn điểm thấp không được đưa cho Qwen


async def test_refuses_without_calling_qwen_when_nothing_reaches_the_threshold():
    llm = FakeLLMWithEmbed()
    retriever = FakeRetriever([hit(score=0.5)])

    out = await handler_with(llm, retriever).run(ChatState(), "Thời tiết Hà Nội?")

    assert out.answer == NO_BASIS and not out.citations and out.disclaimer is None
    assert llm.calls == []  # không gọi Qwen thì Qwen không có cơ hội bịa


async def test_refuses_when_the_store_returns_nothing():
    out = await handler_with(FakeLLMWithEmbed(), FakeRetriever([])).run(ChatState(), "q")

    assert out.answer == NO_BASIS


async def test_refuses_when_qwen_says_the_passages_are_not_enough():
    llm = FakeLLMWithEmbed({"enough": False, "answer": "", "used": []})

    out = await handler_with(llm, FakeRetriever([hit()])).run(ChatState(), "q")

    assert out.answer == NO_BASIS and not out.citations


async def test_retries_once_then_uses_the_checked_second_answer():
    llm = FakeLLMWithEmbed(good(answer="Phí SEVIS là 999 USD [1]."), good())

    out = await handler_with(llm, FakeRetriever([hit()])).run(ChatState(), "Phí SEVIS bao nhiêu?")

    assert out.answer == "Phí SEVIS là 350 USD [1]."
    assert len(llm.calls) == 2


async def test_falls_back_to_the_standard_answer_when_qwen_is_wrong_twice():
    llm = FakeLLMWithEmbed(good(answer="Phí là 999 USD [1]."), good(answer="Phí là 888 USD [1]."))

    out = await handler_with(llm, FakeRetriever([hit()])).run(ChatState(), "Phí SEVIS bao nhiêu?")

    assert out.answer == f"{SEVIS} [1]"  # nguyên văn câu trả lời chuẩn, không phải lời Qwen
    assert [c.title for c in out.citations] == ["8 CFR 214.13"]


async def test_falls_back_when_qwen_is_unreachable_twice():
    llm = FakeLLMWithEmbed(OllamaError("tắt"), OllamaError("tắt"))

    out = await handler_with(llm, FakeRetriever([hit()])).run(ChatState(), "q")

    assert out.answer.startswith(SEVIS)


async def test_only_the_cited_passage_becomes_a_citation():
    llm = FakeLLMWithEmbed(good(answer="Cần khoảng 4.000 USD mỗi tháng [2].", used=(2,)))
    retriever = FakeRetriever([hit(), hit("KB-002", FINANCE, quote="Hướng dẫn tài chính")])

    out = await handler_with(llm, retriever).run(ChatState(), "Chứng minh tài chính?")

    assert [c.title for c in out.citations] == ["Hướng dẫn tài chính"]


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

    assert res.answer == "Phí SEVIS là 350 USD [1]."
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
