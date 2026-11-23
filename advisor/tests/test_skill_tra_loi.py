"""Việc B5 (Phần C): skill_tra_loi trả lời câu hỏi kiến thức từ kho, có nguồn, không bịa.

Nguyên tắc đang kiểm: nội dung kiến thức LUÔN là nguyên văn dòng đã chốt trong kho; Claude chỉ CHỌN dòng đúng ý hỏi (hoặc nói không dòng nào đúng).
"""

import json

import pytest

from app.schemas.chat import ChatMessage, ChatRequest, ChatState
from app.services.knowledge import KnowledgeHit, TopicPart
from app.services.ollama import OllamaError
from app.services.skills import build_handlers
from app.services.skills.skill_tra_loi import (
    DISCLAIMER,
    FALLBACK_MIN_SCORE,
    MAX_CANDIDATES,
    MAX_MEDIA,
    MAX_TOPIC_BUTTONS,
    NO_BASIS,
    UNAVAILABLE,
    TraLoiHandler,
    check_choice,
    citation_of,
    media_of,
    mentioned_codes,
    topic_buttons,
)
from tests.fakes import FakeLLM, flow_with, tin

SEVIS = "Phí SEVIS I-901 là 350 USD cho diện F-1, nộp một lần trước khi phỏng vấn visa."
FINANCE = (
    "Cần giấy xác nhận số dư tài khoản có giá trị tối thiểu bằng chi phí năm đầu, khoảng 4.000 USD mỗi tháng."
)
STEPS = (
    "Các bước điền DS-160:\n1. Mở trang CEAC.\n2. Chọn lãnh sự quán.\n3. Điền từng phần và lưu mã 10 chữ số."
)


def hit(
    entry_no=1,
    content=SEVIS,
    score=0.9,
    source="8 CFR 214.13",
    url=None,
    retrieved=None,
    question="câu hỏi mẫu",
    image=None,
    doc_type="faq",
    topic_code=None,
):
    return KnowledgeHit(
        entry_no, question, content, source, url, image, doc_type, "LABEL_X", topic_code, retrieved, score
    )


def structured_hit(
    entry_no=1,
    doc_type="proc_steps",
    content=STEPS,
    score=0.9,
    topic="DS-160",
    question="Các bước điền DS-160?",
):
    return hit(entry_no, content, score, "CEAC", question=question, doc_type=doc_type, topic_code=topic)


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

    async def by_topic(self, topic_code):
        return []


def handler_with(llm, retriever, min_score=0.72):
    return TraLoiHandler(llm, "m", retriever, min_score)


def choose(n=1):
    return {"chosen": n}


NONE_FITS = choose(0)  # Claude: không ứng viên nào trả lời đúng ý hỏi


def sent_to_claude(llm) -> dict:
    return json.loads(llm.calls[0]["messages"][1]["content"])


# ---------- kiểm tra lựa chọn của Claude ----------


def test_check_choice_returns_the_candidate_claude_picked():
    rows = [hit(1), hit(2, FINANCE)]

    assert check_choice(choose(2), rows).hit is rows[1]
    assert check_choice(choose(1), rows).hit is rows[0]


def test_check_choice_zero_means_no_candidate_fits():
    assert check_choice(NONE_FITS, [hit()]).hit is None


@pytest.mark.parametrize(
    "raw", [choose(3), choose(-1), choose(1.5), choose("1"), choose(None), choose(True), {}, {"answer": "x"}]
)
def test_check_choice_rejects_anything_that_is_not_a_valid_candidate_number(raw):
    with pytest.raises(ValueError):
        check_choice(raw, [hit(), hit(2, FINANCE)])


def test_check_choice_ignores_extra_keys_claude_may_still_send():
    rows = [hit()]

    assert check_choice({"chosen": 1, "answer": "lời Claude tự viết"}, rows).hit is rows[0]


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


# ---------- handler: Claude chọn, câu trả lời là nguyên văn ----------


async def test_answers_with_the_row_word_for_word_one_citation_and_disclaimer():
    llm = FakeLLMWithEmbed(choose())
    retriever = FakeRetriever([hit(url="https://example.org/sevis")])

    out = await handler_with(llm, retriever).run(ChatState(), "Phí SEVIS bao nhiêu?")

    assert out.answer == SEVIS
    assert [c.title for c in out.citations] == ["8 CFR 214.13"]
    assert out.citations[0].url == "https://example.org/sevis"
    assert out.disclaimer == DISCLAIMER
    assert llm.embedded == [["Phí SEVIS bao nhiêu?"]]


@pytest.mark.parametrize(
    "content", [SEVIS, STEPS, "Dòng có **chữ đậm** và link https://a.org/x.\n- gạch đầu dòng\n| a | b |"]
)
async def test_the_answer_is_the_stored_text_untouched_including_line_breaks(content):
    llm = FakeLLMWithEmbed(
        {"chosen": 1, "answer": "Mình tóm tắt lại bằng lời khác."}
    )  # lời Claude (nếu có) không dùng

    out = await handler_with(llm, FakeRetriever([hit(content=content)])).run(ChatState(), "Hỏi gì đó?")

    assert out.answer == content


@pytest.mark.parametrize(
    "doc_type",
    [
        "faq",
        "glossary",
        "proc_overview",
        "proc_prepare",
        "proc_steps",
        "proc_checklist",
        "proc_mistakes",
        "timeline",
        "compare",
        "situation",
        "data_table",
        "resource",
        "notice",
    ],
)
async def test_every_doc_type_is_returned_the_same_way(doc_type):
    out = await handler_with(
        FakeLLMWithEmbed(choose()), FakeRetriever([hit(content=STEPS, doc_type=doc_type)])
    ).run(ChatState(), "Hỏi gì đó?")

    assert out.answer == STEPS  # một quy tắc cho mọi loại, không có nhánh viết lại


async def test_claude_is_asked_only_to_choose_and_sees_each_sample_question_with_its_passage():
    llm = FakeLLMWithEmbed(choose())
    retriever = FakeRetriever(
        [
            hit(score=1.0, question="Phí SEVIS là gì?"),
            hit(69, FINANCE, score=0.95, question="Chứng minh tài chính?"),
        ]
    )

    await handler_with(llm, retriever).run(ChatState(), "Phí SEVIS bao nhiêu?")

    assert llm.calls[0]["schema"]["required"] == ["chosen"] and list(
        llm.calls[0]["schema"]["properties"]
    ) == ["chosen"]
    sent = sent_to_claude(llm)
    assert sent["question"] == "Phí SEVIS bao nhiêu?"
    assert sent["candidates"] == [
        {"id": 1, "question": "Phí SEVIS là gì?", "passage": SEVIS},
        {"id": 2, "question": "Chứng minh tài chính?", "passage": FINANCE},
    ]


async def test_uses_the_candidate_claude_chose_even_when_it_is_not_the_top_one():
    # dòng đầu (0.78) sai ý; dòng đúng đứng thứ hai: Claude chọn 2 thì câu trả lời, nguồn và nút theo dòng 2
    wrong = hit(1, FINANCE, score=0.78, source="Nguồn sai", question="Chứng minh tài chính?")
    right = hit(2, SEVIS, score=0.75, source="8 CFR 214.13", question="Phí SEVIS là gì?")
    llm = FakeLLMWithEmbed(choose(2))

    out = await handler_with(llm, FakeRetriever([wrong, right])).run(ChatState(), "Phí SEVIS bao nhiêu?")

    assert out.answer == SEVIS
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

    assert out.answer == NO_BASIS and not out.citations and out.disclaimer is None and out.media == []


async def test_a_candidate_below_the_threshold_is_never_sent_to_claude():
    # ngưỡng áp dụng cho mọi dòng: dòng thứ ba 0.71 sát dòng đầu nhưng dưới 0.72 nên không được xét
    hits = [
        hit(1, SEVIS, score=0.80),
        hit(2, FINANCE, score=0.72),  # đúng bằng ngưỡng: được gửi
        hit(3, "Đoạn về học bổng toàn phần.", score=0.71),
    ]
    llm = FakeLLMWithEmbed(choose())

    await handler_with(llm, FakeRetriever(hits)).run(ChatState(), "Phí SEVIS bao nhiêu?")

    user = llm.calls[0]["messages"][1]["content"]
    assert SEVIS in user and FINANCE in user and "học bổng toàn phần" not in user


async def test_at_most_max_candidates_go_to_claude():
    hits = [hit(n, f"Nội dung riêng của dòng số {n}.", score=0.80 - n / 100) for n in range(1, 6)]
    llm = FakeLLMWithEmbed(choose())

    await handler_with(llm, FakeRetriever(hits)).run(ChatState(), "q")

    assert len(sent_to_claude(llm)["candidates"]) == MAX_CANDIDATES


async def test_logs_when_claude_picks_a_candidate_other_than_the_top_one(caplog):
    llm = FakeLLMWithEmbed(choose(2))

    with caplog.at_level("INFO", logger="advisor.knowledge"):
        await handler_with(llm, FakeRetriever([hit(1, FINANCE, 0.8), hit(2, SEVIS, 0.78)])).run(
            ChatState(), "q"
        )

    assert "knowledge_rerank" in caplog.text and "chose=2" in caplog.text and "top=1" in caplog.text


async def test_refuses_without_calling_claude_when_the_best_passage_is_below_the_threshold():
    llm = FakeLLMWithEmbed()

    out = await handler_with(llm, FakeRetriever([hit(score=0.5)])).run(ChatState(), "Thời tiết Hà Nội?")

    assert out.answer == NO_BASIS and not out.citations and out.disclaimer is None
    assert llm.calls == []  # không gọi Claude thì Claude không có cơ hội bịa


async def test_refuses_when_the_store_returns_nothing():
    out = await handler_with(FakeLLMWithEmbed(), FakeRetriever([])).run(ChatState(), "q")

    assert out.answer == NO_BASIS


async def test_an_invalid_choice_is_retried_once_and_the_valid_second_one_is_used():
    llm = FakeLLMWithEmbed(choose(9), choose(1))

    out = await handler_with(llm, FakeRetriever([hit()])).run(ChatState(), "Phí SEVIS bao nhiêu?")

    assert out.answer == SEVIS and len(llm.calls) == 2
    assert (
        "chosen phải là số nguyên" in llm.calls[1]["messages"][-1]["content"]
    )  # Claude được nói rõ lỗi để sửa


# ---------- Claude lỗi hoặc chọn sai khuôn hai lần ----------


async def test_falls_back_to_the_top_row_when_claude_is_invalid_twice_and_the_row_is_a_clear_match():
    llm = FakeLLMWithEmbed(choose(9), choose(9))

    out = await handler_with(llm, FakeRetriever([hit(score=0.9, image="https://b.org/x.png")])).run(
        ChatState(), "q"
    )

    assert out.answer == SEVIS and [c.title for c in out.citations] == ["8 CFR 214.13"]
    assert [m.url for m in out.media] == ["https://b.org/x.png"]


async def test_does_not_fall_back_to_a_row_that_is_not_clearly_the_right_one():
    # dòng đầu chỉ 0.76: có thể là dòng sai chủ đề, không đưa cho học sinh khi chưa ai chọn
    llm = FakeLLMWithEmbed(choose(9), choose(9))

    out = await handler_with(llm, FakeRetriever([hit(score=FALLBACK_MIN_SCORE - 0.01)])).run(ChatState(), "q")

    assert out.answer == NO_BASIS and not out.citations


async def test_fallback_threshold_is_inclusive():
    llm = FakeLLMWithEmbed(choose(9), choose(9))

    out = await handler_with(llm, FakeRetriever([hit(score=FALLBACK_MIN_SCORE)])).run(ChatState(), "q")

    assert out.answer == SEVIS


async def test_logs_why_it_fell_back(caplog):
    llm = FakeLLMWithEmbed(choose(9), choose(9))

    with caplog.at_level("WARNING", logger="advisor.knowledge"):
        await handler_with(llm, FakeRetriever([hit()])).run(ChatState(), "q")

    assert "#1" in caplog.text  # biết dòng nào được dùng khi Claude không chọn được


async def test_falls_back_the_same_way_when_claude_is_unreachable_twice():
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


async def test_chat_returns_the_stored_answer_with_citations_and_disclaimer_to_the_client():
    llm = FakeLLMWithEmbed(choose())
    flow = flow_with(
        build_handlers(llm, "m", knowledge=FakeRetriever([hit(url="https://example.org/sevis")])), KNOWLEDGE
    )
    req = ChatRequest(
        messages=[ChatMessage(role="user", content="Phí SEVIS bao nhiêu?")],
        state=ChatState(current_step="B5"),
    )

    res = await flow.reply(req)

    assert res.answer == SEVIS
    assert [c.url for c in res.citations] == ["https://example.org/sevis"]
    assert res.disclaimer == DISCLAIMER


async def test_a_refusal_does_not_vouch_for_any_center_and_offers_the_center_button():
    out = await handler_with(FakeLLMWithEmbed(), FakeRetriever([hit(score=0.5)])).run(ChatState(), "q")

    assert "uy tín" not in out.answer  # hệ thống không khen hay bảo chứng trung tâm nào
    buttons = [(b.label, b.value) for b in out.quick_replies]
    assert ("Tìm trung tâm tư vấn", "Em muốn tìm trung tâm tư vấn") in buttons
    assert not any(label == "Hỏi về visa, học phí" for label, _ in buttons)  # đang ở đúng việc hỏi kiến thức


async def test_a_real_answer_has_no_center_button():
    out = await handler_with(FakeLLMWithEmbed(choose()), FakeRetriever([hit()])).run(ChatState(), "q")

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
    out = await handler_with(FakeLLMWithEmbed(choose()), FakeRetriever(hits)).run(
        ChatState(), "Phí SEVIS bao nhiêu?"
    )

    assert [r.label for r in out.quick_replies] == [
        "Phí SEVIS nộp khi nào?",
        "Đóng phí SEVIS bằng thẻ nào?",
        "Gợi ý trường từ hồ sơ",
    ]
    assert out.quick_replies[0].value == "Phí SEVIS nộp khi nào?"


async def test_answer_without_related_questions_still_has_buttons():
    out = await handler_with(FakeLLMWithEmbed(choose()), FakeRetriever([hit()])).run(
        ChatState(), "Phí SEVIS bao nhiêu?"
    )

    assert [r.label for r in out.quick_replies] == ["Gợi ý trường từ hồ sơ"]


# ---------- câu gõ không dấu: chỉ thử khôi phục dấu khi tìm lần đầu dưới ngưỡng ----------


class SequencedRetriever(FakeRetriever):
    """Mỗi lần tìm trả danh sách kế tiếp: lần đầu theo câu gốc không dấu, lần hai theo câu đã thêm dấu."""

    def __init__(self, *results) -> None:
        super().__init__()
        self._results = list(results)
        self.search_calls = 0

    async def search(self, embedding):
        self.search_calls += 1
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
    llm = FakeLLMWithEmbed(choose())
    retriever = SequencedRetriever([hit(score=0.58)], [hit(score=0.95)])
    restorer = FakeRestorer("Phí SEVIS bao nhiêu?")

    out = await accent_handler(llm, retriever, restorer).run(ChatState(), "phi sevis bao nhieu")

    assert out.answer == SEVIS and [c.title for c in out.citations] == ["8 CFR 214.13"]
    assert llm.embedded == [["phi sevis bao nhieu"], ["Phí SEVIS bao nhiêu?"]]  # tìm lại bằng câu có dấu
    # Claude vẫn nhận câu học sinh gõ, câu có dấu chỉ dùng để tìm
    assert sent_to_claude(llm)["question"] == "phi sevis bao nhieu"


async def test_no_second_search_when_the_first_one_already_passes_the_threshold():
    restorer = FakeRestorer("x")
    retriever = SequencedRetriever([hit(score=0.9)])

    await accent_handler(FakeLLMWithEmbed(choose()), retriever, restorer).run(
        ChatState(), "phi sevis bao nhieu"
    )

    assert restorer.asked == [] and retriever.search_calls == 1


async def test_nothing_to_restore_keeps_the_refusal_without_a_second_search():
    restorer = FakeRestorer(None)  # câu đã có dấu, hoặc Claude không thêm được
    retriever = SequencedRetriever([hit(score=0.5)])
    llm = FakeLLMWithEmbed()

    out = await accent_handler(llm, retriever, restorer).run(ChatState(), "Thời tiết hôm nay")

    assert out.answer == NO_BASIS and retriever.search_calls == 1 and llm.calls == []


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
        await accent_handler(FakeLLMWithEmbed(choose()), retriever, FakeRestorer("Phí SEVIS bao nhiêu?")).run(
            ChatState(), "phi sevis bao nhieu"
        )

    assert "knowledge_accent_restore before=0.580 after=0.950" in caplog.text
    assert "knowledge_answer entry=1" in caplog.text


async def test_without_a_restorer_the_old_behaviour_is_unchanged():
    out = await handler_with(FakeLLMWithEmbed(), FakeRetriever([hit(score=0.5)])).run(
        ChatState(), "phi sevis"
    )

    assert out.answer == NO_BASIS


# ---------- ảnh minh họa: link lấy từ dòng được chọn, không bao giờ từ lời Claude ----------


def test_media_of_returns_the_https_images_of_the_row_with_the_sample_question_as_alt():
    row = hit(image="https://travel.state.gov/mau-i20.png", question="Mẫu I-20 trông thế nào?")

    assert [(m.kind, m.url, m.alt) for m in media_of(row)] == [
        ("image", "https://travel.state.gov/mau-i20.png", "Mẫu I-20 trông thế nào?")
    ]


def test_media_of_splits_several_links_keeps_only_https_and_caps_the_number():
    links = "https://a.org/1.png; http://b.org/2.png ;javascript:alert(1);https://c.org/3.png;https://c.org/3.png;https://d.org/4.png;https://e.org/5.png"

    urls = [m.url for m in media_of(hit(image=links))]

    assert urls == [
        "https://a.org/1.png",
        "https://c.org/3.png",
        "https://d.org/4.png",
    ]  # không trùng, bỏ http và javascript
    assert len(urls) <= MAX_MEDIA


@pytest.mark.parametrize(
    "image", [None, "", "   ", "ftp://x.org/a.png", "https://", "not a url", "data:image/png;base64,AAAA"]
)
def test_media_of_a_row_without_a_usable_link_is_empty(image):
    assert media_of(hit(image=image)) == []


async def test_answer_carries_the_image_of_the_chosen_row_only():
    # Claude chọn dòng 2 (có ảnh); dòng 1 có ảnh khác nhưng không được chọn thì không hiện ảnh của nó
    first = hit(1, FINANCE, score=0.8, image="https://a.org/dong-1.png")
    second = hit(2, SEVIS, score=0.78, image="https://b.org/dong-2.png")

    out = await handler_with(FakeLLMWithEmbed(choose(2)), FakeRetriever([first, second])).run(
        ChatState(), "Phí SEVIS bao nhiêu?"
    )

    assert [m.url for m in out.media] == ["https://b.org/dong-2.png"]


async def test_the_image_link_comes_from_the_row_whatever_claude_writes():
    llm = FakeLLMWithEmbed({"chosen": 1, "answer": "Xem https://evil.example/fake.png"})

    out = await handler_with(llm, FakeRetriever([hit(image="https://b.org/that.png")])).run(
        ChatState(), "Phí SEVIS bao nhiêu?"
    )

    assert [m.url for m in out.media] == ["https://b.org/that.png"]  # media chỉ từ cơ sở dữ liệu
    assert "evil.example" not in out.answer


async def test_a_refusal_carries_no_media():
    out = await handler_with(
        FakeLLMWithEmbed(NONE_FITS), FakeRetriever([hit(image="https://b.org/x.png")])
    ).run(ChatState(), "q")

    assert out.answer == NO_BASIS and out.media == []


async def test_a_structured_row_keeps_its_citation_and_image():
    row = KnowledgeHit(
        1,
        "q",
        STEPS,
        "CEAC",
        "https://ceac.state.gov",
        "https://a.org/ds160.png",
        "proc_steps",
        "L",
        "DS-160",
        None,
        0.9,
    )

    out = await handler_with(FakeLLMWithEmbed(choose()), FakeRetriever([row])).run(
        ChatState(), "Điền DS-160 thế nào?"
    )

    assert out.answer == STEPS
    assert [c.title for c in out.citations] == ["CEAC"] and [m.url for m in out.media] == [
        "https://a.org/ds160.png"
    ]


# ---------- chặn trả nhầm form: hỏi mã nào chỉ nhận dòng của mã đó ----------


@pytest.mark.parametrize(
    "message", ["DS-160 là gì", "ds160 la gi", "DS 160 điền sao", "đơn ds-160 có mấy phần?"]
)
def test_a_form_code_is_recognised_however_it_is_typed(message):
    assert mentioned_codes(message, []) == {"DS-160"}


@pytest.mark.parametrize("message", ["I-200 là gì", "AI-20 là gì", "em 20 tuổi", "mẫu đơn nào cũng được"])
def test_text_that_only_looks_like_a_code_is_not_a_code(message):
    assert mentioned_codes(message, []) == set()


def test_codes_of_the_rows_found_are_also_protected_even_when_not_in_the_built_in_list():
    rows = [structured_hit(topic="VISA-F1")]

    assert mentioned_codes("Thủ tục visa F-1 gồm gì", rows) == {"VISA-F1"}


async def test_a_question_about_one_form_never_gets_the_rows_of_another_form():
    # hỏi DS-160 mà kho chỉ tìm ra dòng I-20 (0.9): không đưa cho Claude, từ chối luôn
    llm = FakeLLMWithEmbed()

    out = await handler_with(
        llm, FakeRetriever([structured_hit(1, topic="I-20", question="Các bước nhận I-20?")])
    ).run(ChatState(), "Hướng dẫn điền DS-160")

    assert out.answer == NO_BASIS and llm.calls == []


async def test_rows_without_a_code_and_rows_of_the_named_form_stay_candidates():
    llm = FakeLLMWithEmbed(choose())
    rows = [
        structured_hit(1, topic="I-20", question="I-20 là gì?"),  # sai mã: bị loại
        hit(2, SEVIS, score=0.85, question="Phí SEVIS là gì?"),  # không có mã: giữ
        structured_hit(3, topic="DS-160", question="DS-160 gồm gì?", score=0.8),  # đúng mã: giữ
    ]

    await handler_with(llm, FakeRetriever(rows)).run(ChatState(), "DS-160 và phí SEVIS")

    assert [c["question"] for c in sent_to_claude(llm)["candidates"]] == [
        "Phí SEVIS là gì?",
        "DS-160 gồm gì?",
    ]


async def test_a_question_naming_two_forms_may_use_the_rows_of_both():
    llm = FakeLLMWithEmbed(choose())
    rows = [
        structured_hit(1, topic="I-20"),
        structured_hit(2, topic="DS-160", score=0.85),
        structured_hit(3, topic="I-901", score=0.8),
    ]

    await handler_with(llm, FakeRetriever(rows)).run(ChatState(), "I-20 và DS-160 khác nhau gì?")

    assert len(sent_to_claude(llm)["candidates"]) == 2  # I-901 không được nêu nên bị loại


async def test_without_a_form_code_in_the_question_nothing_is_filtered_out():
    llm = FakeLLMWithEmbed(choose())

    await handler_with(llm, FakeRetriever([structured_hit(1, topic="I-20")])).run(
        ChatState(), "Thủ tục nhập học gồm gì?"
    )

    assert len(llm.calls) == 1


async def test_the_fallback_also_respects_the_form_filter():
    # Claude sai hai lần; dòng đầu 0.95 là mã I-20 trong khi hỏi DS-160: không được dùng dòng đó
    llm = FakeLLMWithEmbed(choose(9), choose(9))
    rows = [hit(1, FINANCE, score=0.95, topic_code="I-20", source="Nguồn I-20"), hit(2, SEVIS, score=0.9)]

    out = await handler_with(llm, FakeRetriever(rows)).run(
        ChatState(), "Phí SEVIS khi điền DS-160 là bao nhiêu?"
    )

    assert out.answer == SEVIS and [c.title for c in out.citations] == [
        "8 CFR 214.13"
    ]  # dòng 2 (không mã), không phải dòng 1 (I-20)


# ---------- nút sang các phần khác của cùng thủ tục ----------


def parts(*items):
    return [TopicPart(n, q, t) for n, q, t in items]


def test_topic_buttons_follow_the_natural_order_and_ask_the_exact_sample_question():
    best = structured_hit(2, doc_type="proc_prepare", topic="I-20")
    others = parts(
        (5, "Lỗi hay gặp khi nhận I-20?", "proc_mistakes"),
        (3, "I-20 là gì?", "proc_overview"),
        (4, "Các bước nhận I-20?", "proc_steps"),
        (2, "Chuẩn bị gì để nhận I-20?", "proc_prepare"),  # chính dòng vừa dùng: bỏ
        (9, "I-20 mất bao lâu?", "faq"),  # không phải phần thủ tục: bỏ
    )

    assert [(b.label, b.value) for b in topic_buttons(best, others)] == [
        ("I-20: là gì", "I-20 là gì?"),
        ("I-20: các bước", "Các bước nhận I-20?"),
        ("I-20: lỗi hay gặp", "Lỗi hay gặp khi nhận I-20?"),
    ]


def test_topic_buttons_are_capped_and_empty_for_a_row_without_a_topic():
    many = parts(
        *[
            (n, f"câu {n}", t)
            for n, t in enumerate(
                ["proc_overview", "proc_prepare", "proc_steps", "proc_checklist", "proc_mistakes"], 10
            )
        ]
    )

    assert len(topic_buttons(structured_hit(1, doc_type="timeline", topic="I-20"), many)) == MAX_TOPIC_BUTTONS
    assert topic_buttons(hit(), many) == []


async def test_a_procedure_answer_offers_the_other_parts_instead_of_related_questions():
    class Retriever(FakeRetriever):
        async def by_topic(self, code):
            assert code == "DS-160"
            return parts(
                (7, "DS-160 là gì?", "proc_overview"), (8, "Lỗi hay gặp khi điền DS-160?", "proc_mistakes")
            )

    retriever = Retriever([structured_hit(1), hit(2, SEVIS, score=0.8, question="Phí SEVIS là gì?")])

    out = await handler_with(FakeLLMWithEmbed(choose()), retriever).run(ChatState(), "Điền DS-160 thế nào?")

    assert [b.label for b in out.quick_replies] == [
        "DS-160: là gì",
        "DS-160: lỗi hay gặp",
        "Gợi ý trường từ hồ sơ",
    ]


async def test_failing_to_load_the_other_parts_does_not_break_the_answer():
    class Retriever(FakeRetriever):
        async def by_topic(self, code):
            raise RuntimeError("DB lỗi")

    out = await handler_with(FakeLLMWithEmbed(choose()), Retriever([structured_hit(1)])).run(
        ChatState(), "Điền DS-160 thế nào?"
    )

    assert out.answer == STEPS and out.quick_replies[-1].label == "Gợi ý trường từ hồ sơ"


async def test_a_plain_faq_answer_never_asks_for_topic_parts():
    class Retriever(FakeRetriever):
        async def by_topic(self, code):
            raise AssertionError("dòng hỏi đáp không có mã thủ tục")

    out = await handler_with(FakeLLMWithEmbed(choose()), Retriever([hit()])).run(
        ChatState(), "Phí SEVIS bao nhiêu?"
    )

    assert out.answer == SEVIS
