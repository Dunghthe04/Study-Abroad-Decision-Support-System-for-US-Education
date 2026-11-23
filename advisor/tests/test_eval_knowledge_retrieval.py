"""Phần tính điểm của scripts/eval_knowledge_retrieval: hàm thuần, không cần DB hay Ollama."""

from scripts.eval_knowledge_retrieval import CASES, Result, case, outcome, summarize, top3_recall

IN_SCOPE = case("paraphrase", "OPT là gì", expect=[366, 235])
OUT = case("out_of_scope", "Thời tiết hôm nay")


def test_in_scope_question_answered_with_an_expected_row_is_correct():
    assert outcome(Result(IN_SCOPE, [(235, 0.9)]), 0.72) == "dung"


def test_in_scope_question_answered_with_another_row_is_wrong():
    assert outcome(Result(IN_SCOPE, [(999, 0.9)]), 0.72) == "sai"


def test_in_scope_question_below_threshold_is_a_miss_even_if_the_row_is_right():
    assert outcome(Result(IN_SCOPE, [(366, 0.6)]), 0.72) == "bo_sot"


def test_threshold_is_inclusive():
    assert outcome(Result(IN_SCOPE, [(366, 0.72)]), 0.72) == "dung"


def test_out_of_scope_answered_is_wrong_and_refused_is_right():
    assert outcome(Result(OUT, [(1, 0.8)]), 0.72) == "sai"
    assert outcome(Result(OUT, [(1, 0.5)]), 0.72) == "tu_choi_dung"


def test_no_hits_at_all_means_refusal_or_miss():
    assert outcome(Result(OUT, []), 0.72) == "tu_choi_dung"
    assert outcome(Result(IN_SCOPE, []), 0.72) == "bo_sot"


def test_summary_counts_each_outcome_and_precision_per_threshold():
    results = [Result(IN_SCOPE, [(366, 0.9)]), Result(IN_SCOPE, [(999, 0.75)]), Result(OUT, [(1, 0.5)])]

    low, high = summarize(results, [0.7, 0.8])

    assert (low["dung"], low["sai"], low["bo_sot"], low["tu_choi_dung"], low["precision"]) == (
        1,
        1,
        0,
        1,
        0.5,
    )
    assert (high["dung"], high["sai"], high["bo_sot"], high["tu_choi_dung"], high["precision"]) == (
        1,
        0,
        1,
        1,
        1.0,
    )


def test_precision_is_none_when_nothing_was_answered():
    [row] = summarize([Result(OUT, [(1, 0.1)])], [0.72])

    assert row["precision"] is None


def test_top3_recall_ignores_out_of_scope_and_groups_by_kind():
    results = [
        Result(IN_SCOPE, [(1, 0.9), (2, 0.8), (366, 0.7)]),
        Result(IN_SCOPE, [(1, 0.9), (2, 0.8), (3, 0.7), (366, 0.6)]),  # đúng nhưng đứng thứ 4
        Result(OUT, [(1, 0.9)]),
    ]

    assert top3_recall(results) == {"paraphrase": (1, 2)}


def test_the_question_set_is_well_formed():
    questions = [c.question for c in CASES]
    assert len(questions) == len(set(questions))  # không trùng câu
    assert all(c.must_not.isdisjoint(c.expect) for c in CASES)  # dòng sai không nằm trong dòng đúng
    assert all(not c.expect for c in CASES if c.kind == "out_of_scope")
    assert all(c.expect for c in CASES if c.kind != "out_of_scope")


# --- toàn tuyến (có Claude): chỉ phần tính điểm, không gọi Claude ---


def full(case_, top, answered, fallback=False):
    from scripts.eval_knowledge_retrieval import FullResult

    return FullResult(case_, top, answered, "câu trả lời" if answered else "", fallback)


def test_full_answered_from_an_expected_row_is_correct_and_from_another_row_is_wrong():
    from scripts.eval_knowledge_retrieval import full_outcome

    assert full_outcome(full(IN_SCOPE, (366, 0.9), True), 0.72) == "dung"
    assert full_outcome(full(IN_SCOPE, (999, 0.9), True), 0.72) == "sai"


def test_full_refusal_tells_apart_below_threshold_from_claude_saying_not_enough():
    from scripts.eval_knowledge_retrieval import full_outcome

    assert full_outcome(full(IN_SCOPE, (366, 0.58), False), 0.72) == "bo_sot_nguong"
    assert full_outcome(full(IN_SCOPE, (366, 0.84), False), 0.72) == "bo_sot_claude"
    assert full_outcome(full(IN_SCOPE, None, False), 0.72) == "bo_sot_nguong"


def test_full_out_of_scope_answered_is_wrong_and_refused_is_right():
    from scripts.eval_knowledge_retrieval import full_outcome

    assert full_outcome(full(OUT, (1, 0.8), True), 0.72) == "sai"
    assert full_outcome(full(OUT, (1, 0.8), False), 0.72) == "tu_choi_dung"


def test_full_summary_counts_outcomes_and_fallbacks():
    from scripts.eval_knowledge_retrieval import summarize_full

    results = [
        full(IN_SCOPE, (366, 0.9), True),
        full(IN_SCOPE, (999, 0.9), True, fallback=True),
        full(OUT, (1, 0.5), False),
    ]

    assert summarize_full(results, 0.72) == {"dung": 1, "sai": 1, "tu_choi_dung": 1, "fallback": 1}
