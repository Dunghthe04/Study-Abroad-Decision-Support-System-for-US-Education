"""Chấm điểm của scripts.eval_slm trên danh sách ý: không cần Ollama."""

from scripts.eval_slm import score, types_of, word_f1


def y(kind, span="x", **entities):
    return {"type": kind, "span": span, "entities": entities}


def rows(*pairs):
    """Mỗi cặp (nhãn, dự đoán) là danh sách các ý."""
    records = [{"output": {"intents": gold}} for gold, _ in pairs]
    preds = [{"intents": pred} for _, pred in pairs]
    return records, preds


def test_a_perfect_prediction_scores_one_everywhere():
    gold = [y("collect_profile", gpa="GPA 3.5"), y("knowledge")]

    result = score(*rows((gold, gold)))

    assert result["dung_loai_intents"] == result["dung_thu_tu"] == 1
    assert result["dung_khoa_entities"] == result["dung_het_entities"] == 1
    assert result["hay_thieu"] == [] and result["hay_thua"] == []


def test_the_same_types_in_another_order_count_as_right_types_but_wrong_order():
    gold = [y("knowledge"), y("profile_review")]
    pred = [y("profile_review"), y("knowledge")]

    result = score(*rows((gold, pred)))

    assert result["dung_loai_intents"] == 1 and result["dung_thu_tu"] == 0
    assert result["dung_khoa_entities"] is None  # sai thứ tự thì không so thực thể


def test_a_missing_intent_is_reported_with_its_type():
    gold = [y("knowledge"), y("knowledge")]
    pred = [y("knowledge")]  # hai câu kiến thức mà chỉ tách ra một: lỗi của luật nhãn phụ cũ

    result = score(*rows((gold, pred)))

    assert result["dung_loai_intents"] == 0
    assert result["hay_thieu"] == ["knowledge: 1"] and result["hay_thua"] == []
    assert result["thu_hoi_theo_loai"] == {"knowledge": 0.5}


def test_an_extra_intent_is_reported():
    result = score(*rows(([y("knowledge")], [y("knowledge"), y("center_referral")])))

    assert result["hay_thua"] == ["center_referral: 1"]


def test_entities_are_compared_by_keys_and_by_values():
    gold = [y("collect_profile", gpa="GPA 3.5")]

    right_key_wrong_value = score(*rows((gold, [y("collect_profile", gpa="GPA 3.6")])))
    wrong_key = score(*rows((gold, [y("collect_profile", sat="GPA 3.5")])))

    assert (
        right_key_wrong_value["dung_khoa_entities"] == 1 and right_key_wrong_value["dung_het_entities"] == 0
    )
    assert wrong_key["dung_khoa_entities"] == 0


def test_the_score_is_split_by_how_many_intents_the_message_has():
    one, two, three = [y("knowledge")], [y("knowledge"), y("center_referral")], [y("knowledge")] * 3
    result = score(*rows((one, one), (two, [y("knowledge")]), (three, three)))

    assert result["theo_so_y"] == {"1": 1.0, "2": 0.0, "3+": 1.0}


def test_types_of_keeps_the_order():
    assert types_of({"intents": [y("collect_profile"), y("profile_review")]}) == [
        "collect_profile",
        "profile_review",
    ]


def test_span_is_scored_by_words_so_a_slightly_different_rewrite_still_earns_most_of_the_point():
    gold = [y("knowledge", "I-20 là gì")]

    same = score(*rows((gold, [y("knowledge", "I-20 là gì")])))
    close = score(*rows((gold, [y("knowledge", "I-20 là gì vậy")])))
    other = score(*rows((gold, [y("knowledge", "SAT bao nhiêu điểm")])))

    assert same["span_f1"] == 1 and same["span_giong_het"] == 1
    assert 0.8 < close["span_f1"] < 1 and close["span_giong_het"] == 0
    assert other["span_f1"] == 0


def test_word_f1_ignores_case_and_punctuation_and_handles_nothing_in_common():
    assert word_f1("I-20 là gì?", "i-20 LÀ GÌ") == 1
    assert word_f1("abc", "xyz") == 0


def test_span_is_not_scored_when_the_intents_were_not_split_the_same_way():
    result = score(*rows(([y("knowledge"), y("knowledge")], [y("knowledge")])))

    assert result["span_f1"] is None
