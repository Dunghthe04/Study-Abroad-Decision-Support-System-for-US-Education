"""Tính các chỉ số đo model chọn skill.
Dùng chung cho Logistic Regression và CAE (bước 2.7 sẽ dùng lại)."""

import numpy as np
from sklearn.metrics import precision_recall_fscore_support

from ml.skill_picker.data import LABELS

THRESHOLD = 0.5  # xác suất từ 0,5 trở lên thì chọn nhãn đó

"""
Tính các chỉ số đánh giá model dự đoán skill.
Dùng chung cho Logistic Regression và CAE.

Đầu vào:
- y_true: Bảng nhãn đúng của toàn bộ dữ liệu cần đánh giá.
          Mỗi dòng tương ứng với một tin nhắn, gồm 11 nhãn 0/1.
          1 = có nhãn đó, 0 = không có nhãn đó.

- prob: Bảng xác suất model dự đoán cho toàn bộ dữ liệu.
        Mỗi dòng tương ứng với một tin nhắn, gồm 11 xác suất từ 0 đến 1.
        Xác suất tại mỗi vị trí tương ứng với nhãn ở cùng vị trí trong y_true.

y_true = [
    [1, 0, 1],  # Tin nhắn 1: đáp án đúng
    [1, 0, 1],  # Tin nhắn 2: đáp án đúng
    [0, 1, 0]   # Tin nhắn 3: đáp án đúng
]

prob = [
    [0.8, 0.3, 0.9],  # Xác suất dự đoán tin nhắn 1
    [0.7, 0.2, 0.4],  # Xác suất dự đoán tin nhắn 2
    [0.1, 0.9, 0.2]   # Xác suất dự đoán tin nhắn 3
]

Đầu ra:
- exact_match: Tỷ lệ dòng mà tất cả nhãn dự đoán đều đúng.
- micro_f1: F1 tính trên tổng số dự đoán của tất cả nhãn.
- macro_f1: Trung bình F1 của từng nhãn.
- unsure_rate: Tỷ lệ dòng không có nhãn nào đạt ngưỡng xác suất.
- per_label: Các chỉ số precision, recall, F1 và số mẫu dương của từng nhãn.
"""


def compute_metrics(y_true: np.ndarray, prob: np.ndarray, threhold: float = THRESHOLD) -> dict:
    # Chuyển dự đoán và nhãn về 0-1
    pred = (prob >= threhold).astype(int)
    y = y_true.astype(int)

    # Exact match: tin nhắn mà bộ nhãn đoán giống hệt đáp án
    exact = float((pred == y).all(axis=1).mean())
    # Không chắc: tin nhắn mà k nhãn nào đạt ngưỡng (hệ thống sẽ phải dùng skill của bước hiện tại)
    unsure = float((prob.max(axis=1) < threhold).mean())
    p, r, f1, support = precision_recall_fscore_support(y, pred, average=None, zero_division=0)
    _, _, micro, _ = precision_recall_fscore_support(y, pred, average="micro", zero_division=0)
    per_label = {
        label: {
            "precision": float(p[i]),
            "recall": float(r[i]),
            "f1": float(f1[i]),
            "support": int(support[i]),
        }
        for i, label in enumerate(LABELS)
    }
    return {
        "exact_match": exact,
        "micro_f1": float(micro),
        "macro_f1": float(f1.mean()),
        "unsure_rate": unsure,
        "per_label": per_label,
    }


# In bảng kết quả dễ đọc
def print_metrics(m: dict, title: str) -> None:
    print(f"== {title} ==")
    print(
        f"Exact match: {m['exact_match']:.3f} | Micro-F1: {m['micro_f1']:.3f} | Macro-F1: {m['macro_f1']:.3f} | Không chắc: {m['unsure_rate']:.3f}"
    )
    print(f"{'nhãn':26s}{'precision':>10s}{'recall':>8s}{'F1':>7s}{'số dòng':>9s}")
    for label, v in m["per_label"].items():
        print(f"{label:26s}{v['precision']:10.3f}{v['recall']:8.3f}{v['f1']:7.3f}{v['support']:9d}")
