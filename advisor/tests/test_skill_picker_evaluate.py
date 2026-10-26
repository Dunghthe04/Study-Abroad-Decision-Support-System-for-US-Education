"""Bước 2.7: đếm các kiểu sai của model. Không cần Ollama."""

import numpy as np

from ml.skill_picker.data import LABELS
from ml.skill_picker.evaluate import count_errors


def table(*rows: dict[str, float]) -> np.ndarray:
    return np.array([[row.get(label, 0.0) for label in LABELS] for row in rows])


def test_extra_label_is_counted_against_the_true_label():
    y_true = table({"skill_ho_so": 1})
    prob = table({"skill_ho_so": 0.9, "ket_thuc": 0.8})  # đúng ho_so nhưng gắn thừa ket_thuc

    extra_pairs, missing = count_errors(y_true, prob)

    assert extra_pairs[("skill_ho_so", "ket_thuc")] == 1
    assert not missing


def test_missing_label_is_counted():
    y_true = table({"skill_ho_so": 1, "skill_tra_loi": 1})
    prob = table({"skill_ho_so": 0.9, "skill_tra_loi": 0.2})  # bỏ sót skill_tra_loi

    extra_pairs, missing = count_errors(y_true, prob)

    assert missing["skill_tra_loi"] == 1
    assert not extra_pairs


def test_perfect_prediction_has_no_errors():
    y_true = table({"ket_thuc": 1})

    extra_pairs, missing = count_errors(y_true, table({"ket_thuc": 0.95}))

    assert not extra_pairs
    assert not missing
