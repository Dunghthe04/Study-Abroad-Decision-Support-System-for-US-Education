"""Việc B5 (Phần C): skill_tra_loi trả lời câu hỏi kiến thức từ kho, có nguồn, không bịa."""

import pytest

from app.schemas.chat import ChatMessage, ChatRequest, ChatState
from app.services.knowledge import KnowledgeHit
from app.services.ollama import OllamaError
from app.services.skills import build_handlers
from app.services.skills.skill_tra_loi import (
    DISCLAIMER,
    FALLBACK_MIN_SCORE,
    MAX_CANDIDATES,
    NO_BASIS,
    UNAVAILABLE,
    TraLoiHandler,
    check_answer,
    citation_of,
    numbers_in,
)
from tests.fakes import FakeLLM, flow_with, tin

SEVIS = "Phí SEVIS I-901 là 350 USD cho diện F-1, nộp một lần trước khi phỏng vấn visa."
FINANCE = (
    "Cần giấy xác nhận số dư tài khoản có giá trị tối thiểu bằng chi phí năm đầu, khoảng 4.000 USD mỗi tháng."
)


def hit(
    entry_no=1,
    content=SEVIS,
    score=0.9,
    source="8 CFR 214.13",
    url=None,
    retrieved=None,
    question="câu hỏi mẫu",
):
    return KnowledgeHit(
        entry_no, question, content, source, url, None, "faq", "LABEL_X", None, retrieved, score
    )


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

    async def search(self, embedding):
        self.calls.append(embedding)
        if self._error:
            raise self._error
        return self._hits


def handler_with(llm, retriever, min_score=0.72):
    return TraLoiHandler(llm, "qwen3:8b", retriever, min_score)


# Câu Qwen viết lại đủ ý của SEVIS
REWRITTEN = "Phí SEVIS I-901 cho diện F-1 là 350 USD, bạn nộp một lần trước khi phỏng vấn visa."


def good(answer=REWRITTEN, chosen=1):
    return {"chosen": chosen, "answer": answer}


NONE_FITS = {"chosen": 0, "answer": ""}  # Claude: không đoạn nào trả lời đúng ý hỏi


# ---------- đọc số trong câu ----------


def test_numbers_in_treats_thousand_separators_and_decimal_commas_the_same():
    assert numbers_in("Cần 4.000 USD") == numbers_in("Cần 4000 USD") == {"4000"}
    assert numbers_in("IELTS 6,5") == numbers_in("IELTS 6.5") == {"6.5"}


# ---------- kiểm tra câu trả lời của Qwen ----------


def test_check_answer_accepts_a_grounded_answer():
    result = check_answer(good(), "Phí SEVIS bao nhiêu?", [hit()])

    assert result.enough and result.answer == REWRITTEN


def test_check_answer_not_enough_needs_no_checking():
    result = check_answer(NONE_FITS, "q", [hit()])

    assert not result.enough and result.hit is None


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
        check_answer(raw, "Phí SEVIS bao nhiêu?", [hit()])


def test_check_answer_allows_a_forbidden_word_that_the_verified_passage_itself_uses():
    # "đảm bảo" ở đây nghĩa "làm cho chắc là", có sẵn trong câu chuẩn: chép lại không phải hứa kết quả
    content = "Bạn cần đảm bảo thông tin trên DS-160 và I-20 khớp nhau. Phí SEVIS là 350 USD."
    raw = good(answer="Hãy đảm bảo thông tin trên DS-160 và I-20 khớp nhau nhé. Phí SEVIS là 350 USD.")

    assert check_answer(raw, "q", [hit(content=content)]).enough


def test_check_answer_allows_the_everyday_verb_dam_bao_when_it_promises_nothing():
    # đo thật: Claude viết "đảm bảo thông tin giữa các giấy tờ khớp nhau" (ý "làm cho chắc"), bị loại hai lần liền
    raw = good(answer=REWRITTEN + " Hãy đảm bảo thông tin ở các giấy tờ khớp nhau.")

    assert check_answer(raw, "q", [hit()]).enough


@pytest.mark.parametrize(
    "promise",
    [
        "đảm bảo đậu visa",
        "đảm bảo sẽ đỗ",
        "đảm bảo kết quả tốt",
        "đảm bảo thành công",
        "đảm bảo được nhận học bổng",
    ],
)
def test_check_answer_rejects_dam_bao_when_it_promises_a_result(promise):
    with pytest.raises(ValueError):
        check_answer(good(answer=f"Làm theo cách này sẽ {promise}. " + REWRITTEN), "q", [hit()])


def test_check_answer_still_rejects_a_promise_the_passage_does_not_make():
    raw = good(answer="Làm theo cách này thì đảm bảo đậu visa. " + REWRITTEN)

    with pytest.raises(ValueError):
        check_answer(raw, "q", [hit()])


def test_check_answer_removes_source_markers_the_model_still_writes():
    raw = good(
        answer="Phí SEVIS I-901 cho diện F-1 là 350 USD [1]. Bạn nộp một lần trước khi phỏng vấn visa [2]."
    )

    result = check_answer(raw, "q", [hit()])

    assert (
        result.answer == "Phí SEVIS I-901 cho diện F-1 là 350 USD. Bạn nộp một lần trước khi phỏng vấn visa."
    )  # người dùng không phải nhìn [1] [2]


def test_check_answer_does_not_count_a_marker_number_as_a_made_up_figure():
    # [7] không có trong nội dung, nhưng chỉ là dấu nguồn nên bị bỏ chứ không bị coi là số bịa
    assert check_answer(good(answer=REWRITTEN.replace("USD,", "USD [7],")), "q", [hit()]).enough


def test_check_answer_allows_numbers_from_the_question():
    raw = good(
        answer="Dù học 3 năm, phí SEVIS I-901 cho diện F-1 vẫn là 350 USD, nộp một lần trước khi phỏng vấn visa."
    )

    assert check_answer(raw, "Nếu học 3 năm thì phí SEVIS bao nhiêu?", [hit()]).enough


def test_check_answer_allows_thousand_separator_written_either_way():
    raw = good(
        answer="Bạn cần giấy xác nhận số dư tài khoản tối thiểu bằng chi phí năm đầu, khoảng 4000 USD mỗi tháng."
    )

    assert check_answer(raw, "Cần chứng minh bao nhiêu?", [hit(content=FINANCE)]).enough


def test_check_answer_does_not_require_list_numbering_to_be_kept():
    content = "Hai yếu tố: (1) năng lực học thuật; (2) năng lực tài chính cho năm học đầu tiên."
    raw = good(
        answer="Có hai yếu tố: thứ nhất là năng lực học thuật, thứ hai là năng lực tài chính cho năm học đầu tiên."
    )

    assert check_answer(
        raw, "q", [hit(content=content)]
    ).enough  # "(1)", "(2)" viết thành "thứ nhất" vẫn đủ ý


# ---------- nguồn trích dẫn ----------


def test_citation_prefers_the_source_then_the_url_then_a_generic_title():
    assert citation_of(hit(source="8 CFR 214.13", url="https://a.org")).title == "8 CFR 214.13"
    assert citation_of(hit(source=None, url="https://a.org")).title == "https://a.org"
    assert citation_of(hit(source=None, url=None)).title == "Kho kiến thức tư vấn du học"


def test_citation_carries_the_date_the_source_was_collected():
    from datetime import date

    cited = citation_of(hit(retrieved=date(2026, 10, 5)))

    assert cited.retrieved_at == date(2026, 10, 5)
    # JSON gửi cho .NET dùng camelCase, ngày dạng ISO
    assert cited.model_dump(by_alias=True, mode="json")["retrievedAt"] == "2026-10-05"


def test_citation_without_a_stored_date_has_no_date_instead_of_a_guess():
    cited = citation_of(hit(retrieved=None))

    assert cited.retrieved_at is None
    assert cited.model_dump(by_alias=True, mode="json")["retrievedAt"] is None


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


async def test_sends_claude_the_qualifying_candidates_with_their_sample_questions():
    # Claude thấy cả câu hỏi mẫu lẫn câu trả lời của từng ứng viên để chọn đúng ý hỏi
    llm = FakeLLMWithEmbed(good())
    retriever = FakeRetriever(
        [
            hit(score=1.0, question="Phí SEVIS là gì?"),
            hit(69, FINANCE, score=0.95, question="Chứng minh tài chính?"),
        ]
    )

    await handler_with(llm, retriever).run(ChatState(), "Phí SEVIS bao nhiêu?")

    user = llm.calls[0]["messages"][1]["content"]
    assert "350 USD" in user and "4.000" in user
    assert "Phí SEVIS là gì?" in user and "Chứng minh tài chính?" in user


async def test_uses_the_candidate_claude_chose_even_when_it_is_not_the_top_one():
    # dòng đầu (0.78) sai ý; dòng đúng đứng thứ hai: Claude chọn 2 thì câu trả lời, nguồn và nút theo dòng 2
    answer = "Phí SEVIS I-901 cho diện F-1 là 350 USD, bạn nộp một lần trước khi phỏng vấn visa."
    wrong = hit(1, FINANCE, score=0.78, source="Nguồn sai", question="Chứng minh tài chính?")
    right = hit(2, SEVIS, score=0.75, source="8 CFR 214.13", question="Phí SEVIS là gì?")
    llm = FakeLLMWithEmbed(good(answer, chosen=2))

    out = await handler_with(llm, FakeRetriever([wrong, right])).run(ChatState(), "Phí SEVIS bao nhiêu?")

    assert out.answer == answer
    assert [c.title for c in out.citations] == ["8 CFR 214.13"]  # chỉ một nguồn, là nguồn của dòng được chọn
    assert "Chứng minh tài chính?" in [
        r.value for r in out.quick_replies
    ]  # dòng bị bỏ lại thành gợi ý liên quan
    assert "Phí SEVIS là gì?" not in [r.value for r in out.quick_replies]  # dòng vừa dùng không gợi ý lại


async def test_refuses_when_claude_says_none_of_the_candidates_fits():
    llm = FakeLLMWithEmbed(NONE_FITS)

    out = await handler_with(llm, FakeRetriever([hit(score=0.8), hit(2, FINANCE, score=0.77)])).run(
        ChatState(), "q"
    )

    assert out.answer == NO_BASIS and not out.citations and out.disclaimer is None


async def test_a_candidate_below_the_threshold_is_never_sent_to_claude():
    # ngưỡng áp dụng cho mọi dòng: dòng thứ ba 0.71 sát dòng đầu nhưng dưới 0.72 nên không được xét
    hits = [
        hit(1, SEVIS, score=0.80),
        hit(2, FINANCE, score=0.72),  # đúng bằng ngưỡng: được gửi
        hit(3, "Đoạn về học bổng toàn phần.", score=0.71),
    ]
    llm = FakeLLMWithEmbed(good())

    await handler_with(llm, FakeRetriever(hits)).run(ChatState(), "Phí SEVIS bao nhiêu?")

    user = llm.calls[0]["messages"][1]["content"]
    assert SEVIS in user and FINANCE in user and "học bổng toàn phần" not in user


async def test_at_most_max_candidates_go_to_claude():
    hits = [hit(n, f"Nội dung riêng của dòng số {n}.", score=0.80 - n / 100) for n in range(1, 6)]
    llm = FakeLLMWithEmbed(
        good(answer="x"), good(answer="x")
    )  # sai cả hai lần: ở đây chỉ xem Claude được gửi gì

    await handler_with(llm, FakeRetriever(hits)).run(ChatState(), "q")

    user = llm.calls[0]["messages"][1]["content"]
    assert user.count('"passage"') == MAX_CANDIDATES
    assert "dòng số 3" in user and "dòng số 4" not in user


async def test_numbers_from_a_candidate_that_was_not_chosen_are_rejected_and_retried():
    # Claude chọn dòng 1 nhưng chép con số 4.000 của dòng 2: bị loại, gọi lại, lần sau đúng
    mixed = "Phí SEVIS I-901 cho diện F-1 là 350 USD, bạn nộp một lần trước khi phỏng vấn visa, tương đương 4.000 USD."
    llm = FakeLLMWithEmbed(good(mixed, chosen=1), good())

    out = await handler_with(llm, FakeRetriever([hit(1, SEVIS, 0.9), hit(2, FINANCE, 0.85)])).run(
        ChatState(), "Phí SEVIS bao nhiêu?"
    )

    assert out.answer == REWRITTEN
    assert len(llm.calls) == 2


@pytest.mark.parametrize("chosen", [3, -1, 1.5, "1", None, True])
def test_check_answer_rejects_a_choice_that_is_not_a_valid_candidate_number(chosen):
    with pytest.raises(ValueError):
        check_answer({"chosen": chosen, "answer": REWRITTEN}, "q", [hit(), hit(2, FINANCE)])


def test_check_answer_reports_which_candidate_was_chosen():
    result = check_answer(
        good(
            chosen=2,
            answer="Khoảng 4000 USD mỗi tháng, kèm giấy xác nhận số dư tài khoản tối thiểu bằng chi phí năm đầu.",
        ),
        "q",
        [hit(1, SEVIS), hit(2, FINANCE)],
    )

    assert result.hit.entry_no == 2


async def test_logs_when_claude_picks_a_candidate_other_than_the_top_one(caplog):
    answer = "Phí SEVIS I-901 cho diện F-1 là 350 USD, bạn nộp một lần trước khi phỏng vấn visa."
    llm = FakeLLMWithEmbed(good(answer, chosen=2))

    with caplog.at_level("INFO", logger="advisor.knowledge"):
        await handler_with(llm, FakeRetriever([hit(1, FINANCE, 0.8), hit(2, SEVIS, 0.78)])).run(
            ChatState(), "q"
        )

    assert "knowledge_rerank" in caplog.text and "chose=2" in caplog.text and "top=1" in caplog.text


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
    llm = FakeLLMWithEmbed(NONE_FITS)

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


async def test_does_not_fall_back_to_a_row_that_is_not_clearly_the_right_one():
    # Claude sai hai lần mà dòng đầu chỉ 0.76: có thể là dòng sai chủ đề, trả nguyên văn là đưa thẳng cho học sinh
    llm = FakeLLMWithEmbed(good(answer="Phí là 999 USD."), good(answer="Phí là 888 USD."))
    retriever = FakeRetriever([hit(score=FALLBACK_MIN_SCORE - 0.01)])

    out = await handler_with(llm, retriever).run(ChatState(), "Phí SEVIS bao nhiêu?")

    assert out.answer == NO_BASIS and not out.citations


async def test_fallback_threshold_is_inclusive():
    llm = FakeLLMWithEmbed(good(answer="Phí là 999 USD."), good(answer="Phí là 888 USD."))

    out = await handler_with(llm, FakeRetriever([hit(score=FALLBACK_MIN_SCORE)])).run(ChatState(), "q")

    assert out.answer == SEVIS


async def test_logs_why_it_fell_back_to_the_standard_answer(caplog):
    llm = FakeLLMWithEmbed(good(answer="Phí là 999 USD."), good(answer="Phí là 888 USD."))

    with caplog.at_level("WARNING", logger="advisor.knowledge"):
        await handler_with(llm, FakeRetriever([hit()])).run(ChatState(), "Phí SEVIS bao nhiêu?")

    assert "#1" in caplog.text  # biết dòng nào được dùng làm câu chuẩn khi Qwen không dùng được


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


# ---------- đăng ký và luồng chat ----------


def test_skill_tra_loi_is_registered_only_when_the_knowledge_store_exists():
    assert "skill_tra_loi" not in build_handlers(llm=object(), model="m")

    handlers = build_handlers(llm=object(), model="m", knowledge=FakeRetriever())

    assert "skill_tra_loi" in handlers


# SLM đọc là câu hỏi kiến thức: nhánh kiến thức gọi skill_tra_loi
KNOWLEDGE = tin("knowledge")


async def test_chat_returns_the_citations_and_disclaimer_to_the_client():
    llm = FakeLLMWithEmbed(good())
    flow = flow_with(
        build_handlers(llm, "m", knowledge=FakeRetriever([hit(url="https://example.org/sevis")])), KNOWLEDGE
    )
    req = ChatRequest(
        messages=[ChatMessage(role="user", content="Phí SEVIS bao nhiêu?")],
        state=ChatState(current_step="B5"),
    )

    res = await flow.reply(req)

    assert res.answer == REWRITTEN
    assert [c.url for c in res.citations] == ["https://example.org/sevis"]
    assert res.disclaimer == DISCLAIMER


async def test_a_refusal_does_not_vouch_for_any_center_and_offers_the_center_button():
    out = await handler_with(FakeLLMWithEmbed(), FakeRetriever([hit(score=0.5)])).run(ChatState(), "q")

    assert "uy tín" not in out.answer  # hệ thống không khen hay bảo chứng trung tâm nào
    buttons = [(b.label, b.value) for b in out.quick_replies]
    assert ("Tìm trung tâm tư vấn", "Em muốn tìm trung tâm tư vấn") in buttons
    assert not any(label == "Hỏi về visa, học phí" for label, _ in buttons)  # đang ở đúng việc hỏi kiến thức


async def test_a_real_answer_has_no_center_button():
    out = await handler_with(FakeLLMWithEmbed(good()), FakeRetriever([hit()])).run(ChatState(), "q")

    assert "Tìm trung tâm tư vấn" not in [r.label for r in out.quick_replies]


# Câu trả lời kiến thức có nút đi tiếp: câu hỏi liên quan trong kho (gần câu hỏi, khác dòng vừa dùng), rồi gợi ý trường
async def test_answer_offers_related_questions_then_schools():
    def h(entry_no, question, score):
        return hit(entry_no, SEVIS, score, "Nguồn", question=question)

    hits = [
        h(1, "Phí SEVIS là gì?", 0.9),
        h(2, "Phí SEVIS nộp khi nào?", 0.8),
        h(1, "Phí SEVIS là gì?", 0.85),  # cùng dòng vừa dùng: bỏ
        h(3, "Đóng phí SEVIS bằng thẻ nào?", 0.6),
        h(4, "Visa F-1 là gì?", 0.4),  # quá xa: bỏ
    ]
    out = await handler_with(FakeLLMWithEmbed(good()), FakeRetriever(hits)).run(
        ChatState(), "Phí SEVIS bao nhiêu?"
    )

    assert [r.label for r in out.quick_replies] == [
        "Phí SEVIS nộp khi nào?",
        "Đóng phí SEVIS bằng thẻ nào?",
        "Gợi ý trường từ hồ sơ",
    ]
    assert out.quick_replies[0].value == "Phí SEVIS nộp khi nào?"


async def test_answer_without_related_questions_still_has_buttons():
    out = await handler_with(FakeLLMWithEmbed(good()), FakeRetriever([hit()])).run(
        ChatState(), "Phí SEVIS bao nhiêu?"
    )

    assert [r.label for r in out.quick_replies] == ["Gợi ý trường từ hồ sơ"]


# ---------- câu gõ không dấu: chỉ thử khôi phục dấu khi tìm lần đầu dưới ngưỡng ----------


class SequencedRetriever:
    """Mỗi lần tìm trả danh sách kế tiếp: lần đầu theo câu gốc không dấu, lần hai theo câu đã thêm dấu."""

    def __init__(self, *results) -> None:
        self._results = list(results)
        self.calls = 0

    async def search(self, embedding):
        self.calls += 1
        return self._results.pop(0)


class FakeRestorer:
    def __init__(self, restored: str | None) -> None:
        self._restored = restored
        self.asked: list[str] = []

    async def restore(self, text):
        self.asked.append(text)
        return self._restored


def accent_handler(llm, retriever, restorer):
    return TraLoiHandler(llm, "m", retriever, 0.72, restorer=restorer)


async def test_an_unaccented_question_below_the_threshold_is_searched_again_with_diacritics():
    llm = FakeLLMWithEmbed(good())
    retriever = SequencedRetriever([hit(score=0.58)], [hit(score=0.95)])
    restorer = FakeRestorer("Phí SEVIS bao nhiêu?")

    out = await accent_handler(llm, retriever, restorer).run(ChatState(), "phi sevis bao nhieu")

    assert out.answer == REWRITTEN and [c.title for c in out.citations] == ["8 CFR 214.13"]
    assert llm.embedded == [["phi sevis bao nhieu"], ["Phí SEVIS bao nhiêu?"]]  # tìm lại bằng câu có dấu
    # Claude vẫn nhận câu học sinh gõ, câu có dấu chỉ dùng để tìm
    assert '"question":"phi sevis bao nhieu"' in llm.calls[0]["messages"][1]["content"]


async def test_no_second_search_when_the_first_one_already_passes_the_threshold():
    restorer = FakeRestorer("x")
    retriever = SequencedRetriever([hit(score=0.9)])

    await accent_handler(FakeLLMWithEmbed(good()), retriever, restorer).run(
        ChatState(), "phi sevis bao nhieu"
    )

    assert restorer.asked == [] and retriever.calls == 1


async def test_nothing_to_restore_keeps_the_refusal_without_a_second_search():
    restorer = FakeRestorer(None)  # câu đã có dấu, hoặc Claude không thêm được
    retriever = SequencedRetriever([hit(score=0.5)])
    llm = FakeLLMWithEmbed()

    out = await accent_handler(llm, retriever, restorer).run(ChatState(), "Thời tiết hôm nay")

    assert out.answer == NO_BASIS and retriever.calls == 1 and llm.calls == []


async def test_a_second_search_that_is_not_better_keeps_the_first_result():
    retriever = SequencedRetriever([hit(score=0.6)], [hit(score=0.55)])
    llm = FakeLLMWithEmbed()

    out = await accent_handler(llm, retriever, FakeRestorer("Thời tiết hôm nay")).run(
        ChatState(), "thoi tiet hom nay"
    )

    assert out.answer == NO_BASIS and llm.calls == []


async def test_a_second_search_that_is_better_but_still_below_the_threshold_is_refused():
    retriever = SequencedRetriever([hit(score=0.5)], [hit(score=0.7)])

    out = await accent_handler(FakeLLMWithEmbed(), retriever, FakeRestorer("câu")).run(ChatState(), "cau")

    assert out.answer == NO_BASIS


async def test_a_failing_second_embedding_keeps_the_refusal_and_does_not_crash():
    class Llm(FakeLLMWithEmbed):
        async def embed(self, texts):
            self.embedded.append(texts)
            if len(self.embedded) == 2:
                raise OllamaError("Ollama tắt giữa chừng")
            return [[0.1, 0.2] for _ in texts]

    out = await accent_handler(Llm(), SequencedRetriever([hit(score=0.5)]), FakeRestorer("Phí SEVIS")).run(
        ChatState(), "phi sevis"
    )

    assert out.answer == NO_BASIS


async def test_logs_the_accent_restoration_with_scores_and_the_entry_used(caplog):
    retriever = SequencedRetriever([hit(score=0.58)], [hit(score=0.95)])

    with caplog.at_level("INFO", logger="advisor.knowledge"):
        await accent_handler(FakeLLMWithEmbed(good()), retriever, FakeRestorer("Phí SEVIS bao nhiêu?")).run(
            ChatState(), "phi sevis bao nhieu"
        )

    assert "knowledge_accent_restore before=0.580 after=0.950" in caplog.text
    assert "knowledge_answer entry=1" in caplog.text


async def test_without_a_restorer_the_old_behaviour_is_unchanged():
    out = await handler_with(FakeLLMWithEmbed(), FakeRetriever([hit(score=0.5)])).run(
        ChatState(), "phi sevis"
    )

    assert out.answer == NO_BASIS
