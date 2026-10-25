"""
Huấn luyện Logistic Regression làm mô hình cơ sở (baseline)
để so sánh với mô hình CAE.

Quy trình:
1. Đọc dữ liệu đã được chia thành train, val và test.
2. Dùng tập train để huấn luyện mô hình.
3. Dùng tập val để đánh giá mô hình.
4. In các chỉ số đánh giá và một số câu dự đoán sai.
5. Lưu kết quả đánh giá vào file report_logreg.json.

Lưu ý:
- Tập train dùng để học.
- Tập val dùng để kiểm tra, điều chỉnh mô hình.
- Tập test dành cho đánh giá cuối cùng, không được sử dụng trong file này.

Chạy:
    python -m ml.skill_picker.train_logreg
"""

import json  # Chuyển kết quả đánh giá thành JSON để lưu file
import sys  # Cấu hình cách in văn bản ra terminal

import numpy as np  # Xử lý mảng số và tìm các dòng dự đoán sai

# Thuật toán Logistic Regression và cách mở rộng thành phân loại nhiều nhãn
from sklearn.linear_model import LogisticRegression
from sklearn.multiclass import OneVsRestClassifier

# Danh sách 11 nhãn, hàm tải dữ liệu và thư mục lưu kết quả
from ml.skill_picker.data import LABELS
from ml.skill_picker.dataset import load_splits
from ml.skill_picker.embed import ARTIFACTS_DIR

# Tính và in các chỉ số đánh giá mô hình
from ml.skill_picker.metrics import compute_metrics, print_metrics

REPORT_PATH = ARTIFACTS_DIR / "report_logreg.json"


def main() -> None:
    # Đảm bảo được terminal hiển thị tiếng việt
    sys.stdout.reconfigure(encoding="utf-8")

    # Đọc dữ liệu đã chia train, val, test
    splits = load_splits()
    # x_train: vector đặc trưng của các câu dùng để huấn luyện
    # y_train: nhãn đúng tương ứng với từng câu
    x_train, y_train, _ = splits["train"]
    x_val, y_val, val_rows = splits["val"]
    print(f"Train {len(x_train)} dòng, val {len(x_val)} dòng, mỗi dòng {x_train.shape[1]} số")

    # One-vs-Rest (OvR):
    # Huấn luyện một bộ phân loại riêng cho mỗi nhãn.
    # Mỗi bộ phân loại dự đoán câu đó có thuộc nhãn tương ứng hay không.
    #
    # max_iter=3000: số vòng lặp tối đa để thuật toán tìm nghiệm.
    # C=100: mức điều chỉnh độ phức tạp của mô hình.
    #         C lớn hơn thường làm giảm mức regularization.
    # class_weight="balanced": tự điều chỉnh trọng số lớp
    #         để giảm ảnh hưởng của sự chênh lệch số lượng mẫu giữa các lớp.
    model = OneVsRestClassifier(LogisticRegression(max_iter=3000, C=100, class_weight="balanced"))
    # Huấn luyện mô hình bằng vector đặc trưng và nhãn đúng của tập train
    model.fit(x_train, y_train)

    # Dự đoán xác xuất từng nhãn cho mỗi câu trong tập val
    # bảng xác suất, mỗi hàng 11 số
    prob = model.predict_proba(x_val)

    # So sánh xác suất dự đoán với nhãn đúng y_val
    # để tính Exact Match, Micro-F1, Macro-F1, Unsure Rate và chỉ số từng nhãn.
    metrics = compute_metrics(y_val, prob)

    # In các chỉ số đánh giá ra terminal
    print_metrics(metrics, "Logistic Regression trên tập val (dữ liệu tạo thêm, chỉ để kiểm tra code)")

    # Tìm vị trí các câu có ít nhất một nhãn dự đoán khác nhãn đúng.
    # prob >= 0.5: chuyển xác suất thành nhãn 0/1 theo ngưỡng 0.5.
    # != y_val: so sánh nhãn dự đoán với nhãn đúng.
    # any(axis=1): đánh dấu câu sai nếu có ít nhất một nhãn bị dự đoán sai.
    # np.where(...)[0]: lấy danh sách vị trí của những câu đó.

    wrong = np.where(((prob >= 0.5).astype(int) != y_val.astype(int)).any(axis=1))[0]
    print(f"\nSố câu val đoán sai: {len(wrong)}/{len(x_val)}. Một vài câu:")
    for i in wrong[:8]:
        got = [LABELS[j] for j in range(len(LABELS)) if prob[i][j] >= 0.5]
        print(f"  [{val_rows[i]['current_step']}] {val_rows[i]['message'][:60]}")
        print(f"     đáp án: {val_rows[i]['labels']} | đoán: {', '.join(got) or '(không chắc)'}")

    REPORT_PATH.write_text(json.dumps(metrics, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"\nĐã lưu {REPORT_PATH}")


if __name__ == "__main__":
    main()
