"""Bước 2.5: chỉ số đo model và việc nối vector với nhãn. Không cần Ollama."""

import numpy as np
import pytest

from ml.skill_picker import dataset
from ml.skill_picker.data import LABELS
from ml.skill_picker.metrics import compute_metrics


def prob_of(*rows: dict[str, float]) -> np.ndarray:
    """Dựng bảng xác suất 11 cột từ các dict {tên nhãn: xác suất}; nhãn không nêu thì 0."""
    return np.array([[row.get(label, 0.0) for label in LABELS] for row in rows])


def test_exact_match_needs_every_label_right():
    y_true = prob_of({"skill_ho_so": 1, "skill_tra_loi": 1}, {"ket_thuc": 1})
    prob = prob_of({"skill_ho_so": 0.9, "skill_tra_loi": 0.8}, {"ket_thuc": 0.9, "ngoai_le": 0.7})

    m = compute_metrics(y_true, prob)

    assert m["exact_match"] == 0.5  # câu 1 đúng hết, câu 2 thừa nhãn ngoai_le
    assert m["per_label"]["ngoai_le"]["precision"] == 0.0  # đoán 1 lần, sai
    assert m["per_label"]["skill_ho_so"]["f1"] == 1.0


def test_unsure_when_no_label_reaches_threshold():
    y_true = prob_of({"skill_ho_so": 1}, {"skill_ho_so": 1})
    prob = prob_of({"skill_ho_so": 0.3, "skill_tra_loi": 0.2}, {"skill_ho_so": 0.9})

    assert compute_metrics(y_true, prob)["unsure_rate"] == 0.5


def test_load_splits_matches_vectors_to_rows_by_id(monkeypatch):
    rows = [
        {"id": "S1", "split": "train", "labels": "skill_ho_so"},
        {"id": "S2", "split": "val", "labels": "skill_ho_so, skill_tra_loi"},
    ]
    vectors = np.arange(2 * 4, dtype=np.float32).reshape(2, 4)
    monkeypatch.setattr(dataset, "load_rows", lambda: rows)
    monkeypatch.setattr(dataset, "load_vectors", lambda: (vectors, ["S1", "S2"]))

    splits = dataset.load_splits()

    x_val, y_val, val_rows = splits["val"]
    assert x_val.tolist() == [[4, 5, 6, 7]]  # hàng thứ 2 của vector → dòng S2
    assert y_val[0][LABELS.index("skill_tra_loi")] == 1
    assert val_rows[0]["id"] == "S2"
    assert len(splits["test"][0]) == 0


def test_load_splits_rejects_stale_vectors(monkeypatch):
    rows = [{"id": "S1", "split": "train", "labels": "skill_ho_so"}]
    monkeypatch.setattr(dataset, "load_rows", lambda: rows)
    monkeypatch.setattr(dataset, "load_vectors", lambda: (np.zeros((2, 4)), ["S1", "S2"]))

    with pytest.raises(ValueError, match="embed"):
        dataset.load_splits()
