"""Nối vector (đầu vào) với nhãn (đáp án) theo id, chia sẵn train, val, test.
Dùng chung cho Logistic Regression và CAE."""

import json

import numpy as np

from ml.skill_picker.data import labels_to_vector, load_rows
from ml.skill_picker.embed import META_PATH, VECTORS_PATH


# Đọc bảng vector và danh sách id đi kèm
def load_vectors() -> tuple[np.ndarray, list[str]]:  # np.ndarray : 1 mảng chứa các vector
    # Đọc file vector (n hàng, 1024 cột)
    vectors = np.load(VECTORS_PATH)
    # Đọc file -> chuyển dạng python -> lấy id
    ids = json.loads(META_PATH.read_text(encoding="utf-8"))["ids"]
    return vectors, ids


# Trả về {"train": (X, Y, rows), "val": (...), "test": (...)}
#   X: bảng vector đầu vào, mỗi hàng 1024 số
#   Y: bảng đáp án, mỗi hàng 11 số 0/1
#   rows: các dòng dữ liệu gốc (để xem model sai ở câu nào)
"""
Chia dữ liệu thành 3 tập: train, val và test.

Kết quả trả về có dạng:
{
    "train": (X_train, Y_train, rows_train),
    "val":   (X_val,   Y_val,   rows_val),
    "test":  (X_test,  Y_test,  rows_test)
}

Trong mỗi tập:
- X: Các vector đặc trưng đầu vào của dữ liệu.
     Mỗi dòng gồm 1024 số, biểu diễn nội dung dữ liệu dưới dạng số.

- Y: Các nhãn đúng tương ứng với từng dòng trong X.
     Mỗi dòng gồm 11 số 0/1, biểu thị dữ liệu có hay không có từng nhãn.

- rows: Các dòng dữ liệu gốc tương ứng với X và Y.
        Dùng để xem lại dữ liệu khi kiểm tra kết quả dự đoán của model.

Ba tập dữ liệu có mục đích:
- train: Dùng để huấn luyện model.
- val:   Dùng để đánh giá và điều chỉnh model trong quá trình phát triển.
- test:  Dùng để đánh giá model sau khi hoàn tất huấn luyện.
"""


def load_splits() -> dict[str, tuple[np.ndarray, np.ndarray, list[dict[str, str]]]]:
    rows = load_rows()
    vectors, ids = load_vectors()

    # Kiểm tra vector còn khớp với file csv không: cùng số dòng, cùng thứ tự id
    if ids != [r["id"] for r in rows]:
        raise ValueError(
            "File dữ liệu đã đổi so với lúc tính vector. Chạy lại: python -m ml.skill_picker.embed"
        )

    # Chuyển nhãn thành vector
    labels = np.array([labels_to_vector(r["labels"]) for r in rows], dtype=np.float32)
    out = {}
    for name in ("train", "val", "test"):
        # Lấy vector đầu vào, nhãn và dữ liệu gốc tại cùng các vị trí.
        index = [i for i, r in enumerate(rows) if r["split"] == name]
        out[name] = (vectors[index], labels[index], [rows[i] for i in index])
    return out
