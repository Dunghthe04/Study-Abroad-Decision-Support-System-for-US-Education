"""Chấm điểm của scripts.eval_slm trên danh sách ý: không cần Ollama."""

from scripts.eval_slm import score, types_of, word_f1


def y(kind, doan="x", **thuc_the):
    return {"type": kind, "doan": doan, "thuc_the": thuc_the}


def rows(*pairs):
    """Mỗi cặp (nhãn, dự đoán) là danh sách các ý."""
    records = [{"output": {"cac_y": gold}} for gold, _ in pairs]
    preds = [{"cac_y": pred} for _, pred in pairs]
    return records, preds


def test_a_perfect_prediction_scores_one_everywhere():
    gold = [y("mac_dinh", gpa="GPA 3.5"), y("kien_thuc")]

    result = score(*rows((gold, gold)))

    assert result["dung_loai_cac_y"] == result["dung_thu_tu"] == 1
    assert result["dung_khoa_thuc_the"] == result["dung_het_thuc_the"] == 1
    assert result["hay_thieu"] == [] and result["hay_thua"] == []


def test_the_same_types_in_another_order_count_as_right_types_but_wrong_order():
    gold = [y("kien_thuc"), y("ho_so")]
    pred = [y("ho_so"), y("kien_thuc")]

    result = score(*rows((gold, pred)))

    assert result["dung_loai_cac_y"] == 1 and result["dung_thu_tu"] == 0
    assert result["dung_khoa_thuc_the"] is None  # sai thứ tự thì không so thực thể


def test_a_missing_intent_is_reported_with_its_type():
    gold = [y("kien_thuc"), y("kien_thuc")]
    pred = [y("kien_thuc")]  # hai câu kiến thức mà chỉ tách ra một: lỗi của luật nhãn phụ cũ

    result = score(*rows((gold, pred)))

    assert result["dung_loai_cac_y"] == 0
    assert result["hay_thieu"] == ["kien_thuc: 1"] and result["hay_thua"] == []
    assert result["thu_hoi_theo_loai"] == {"kien_thuc": 0.5}


def test_an_extra_intent_is_reported():
    result = score(*rows(([y("kien_thuc")], [y("kien_thuc"), y("trung_tam")])))

    assert result["hay_thua"] == ["trung_tam: 1"]


def test_entities_are_compared_by_keys_and_by_values():
    gold = [y("mac_dinh", gpa="GPA 3.5")]

    right_key_wrong_value = score(*rows((gold, [y("mac_dinh", gpa="GPA 3.6")])))
    wrong_key = score(*rows((gold, [y("mac_dinh", sat="GPA 3.5")])))

    assert (
        right_key_wrong_value["dung_khoa_thuc_the"] == 1 and right_key_wrong_value["dung_het_thuc_the"] == 0
    )
    assert wrong_key["dung_khoa_thuc_the"] == 0


def test_the_score_is_split_by_how_many_intents_the_message_has():
    one, two, three = [y("kien_thuc")], [y("kien_thuc"), y("trung_tam")], [y("kien_thuc")] * 3
    result = score(*rows((one, one), (two, [y("kien_thuc")]), (three, three)))

    assert result["theo_so_y"] == {"1": 1.0, "2": 0.0, "3+": 1.0}


def test_types_of_keeps_the_order():
    assert types_of({"cac_y": [y("mac_dinh"), y("ho_so")]}) == ["mac_dinh", "ho_so"]


def test_doan_is_scored_by_words_so_a_slightly_different_rewrite_still_earns_most_of_the_point():
    gold = [y("kien_thuc", "I-20 là gì")]

    same = score(*rows((gold, [y("kien_thuc", "I-20 là gì")])))
    close = score(*rows((gold, [y("kien_thuc", "I-20 là gì vậy")])))
    other = score(*rows((gold, [y("kien_thuc", "SAT bao nhiêu điểm")])))

    assert same["doan_f1"] == 1 and same["doan_giong_het"] == 1
    assert 0.8 < close["doan_f1"] < 1 and close["doan_giong_het"] == 0
    assert other["doan_f1"] == 0


def test_word_f1_ignores_case_and_punctuation_and_handles_nothing_in_common():
    assert word_f1("I-20 là gì?", "i-20 LÀ GÌ") == 1
    assert word_f1("abc", "xyz") == 0


def test_doan_is_not_scored_when_the_intents_were_not_split_the_same_way():
    result = score(*rows(([y("kien_thuc"), y("kien_thuc")], [y("kien_thuc")])))

    assert result["doan_f1"] is None
