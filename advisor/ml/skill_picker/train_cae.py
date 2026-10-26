"""
Huấn luyện mô hình SkillCAE bằng PyTorch và so sánh với Logistic Regression.

Luồng chính:
    1. Đọc dữ liệu train và validation.
    2. Khởi tạo mô hình, bộ tối ưu Adam và các hàm loss.
    3. Huấn luyện mô hình theo từng batch qua nhiều epoch.
    4. Đánh giá Macro-F1 trên tập validation sau mỗi epoch.
    5. Lưu trọng số tại epoch có Macro-F1 tốt nhất.
    6. Lưu mô hình, cấu hình, báo cáo và so sánh với Logistic Regression.

Cách chạy:
    python -m ml.skill_picker.train_cae

Các file đầu ra:
    artifacts/model.pt        : Trọng số của mô hình tốt nhất.
    artifacts/config.json     : Cấu hình mô hình.
    artifacts/report_cae.json : Kết quả đánh giá trên validation.
"""

import json
import sys

import numpy as np
import torch
from torch import nn

from ml.skill_picker.data import LABELS
from ml.skill_picker.dataset import load_splits
from ml.skill_picker.embed import ARTIFACTS_DIR
from ml.skill_picker.metrics import THRESHOLD, compute_metrics, print_metrics
from ml.skill_picker.model import INPUT_SCALE, SkillCAE

# ============================================================
# 1. ĐƯỜNG DẪN LƯU KẾT QUẢ
# ============================================================

MODEL_PATH = ARTIFACTS_DIR / "model.pt"
CONFIG_PATH = ARTIFACTS_DIR / "config.json"
REPORT_PATH = ARTIFACTS_DIR / "report_cae.json"


# ============================================================
# 2. CẤU HÌNH HUẤN LUYỆN
# ============================================================

SEED = 42
# Cố định seed để kết quả có tính tái lập tốt hơn.

BATCH_SIZE = 32
# Mỗi batch xử lý tối đa 32 mẫu trước khi cập nhật trọng số.

MAX_EPOCHS = 100
# Số epoch tối đa. Một epoch là một lượt đi qua toàn bộ tập train.

PATIENCE = 10
# Dừng sớm nếu Macro-F1 trên validation không cải thiện
# trong 10 epoch liên tiếp.

LEARNING_RATE = 1e-3
# Learning rate điều chỉnh quy mô cập nhật tham số trong quá trình tối ưu.

WEIGHT_DECAY = 1e-4
# Weight decay giúp hạn chế trọng số quá lớn, hỗ trợ giảm overfitting.

RECON_WEIGHT = 0.5
# Hệ số điều chỉnh mức đóng góp của loss tái tạo embedding.


# ============================================================
# 3. HÀM DỰ ĐOÁN XÁC SUẤT
# ============================================================


def predict_prob(model: SkillCAE, x: torch.Tensor) -> np.ndarray:
    """Chuyển đầu ra của mô hình thành xác suất dự đoán cho 11 nhãn kỹ năng."""

    model.eval()  # Chuyển sang chế độ đánh giá, tắt Dropout.

    with torch.no_grad():  # Không tính gradient vì không huấn luyện ở bước này.
        logits, _ = model(x)  # Lấy logits, bỏ qua embedding được tái tạo.

    # Sigmoid chuyển logits thành xác suất trong khoảng [0, 1].
    return torch.sigmoid(logits).numpy()


# ============================================================
# 4. HÀM HUẤN LUYỆN CHÍNH
# ============================================================


def main() -> None:
    # Giúp PowerShell hiển thị tiếng Việt đúng mã hóa.
    sys.stdout.reconfigure(encoding="utf-8")

    # Cố định seed cho PyTorch và NumPy.
    torch.manual_seed(SEED)
    np.random.seed(SEED)

    # --------------------------------------------------------
    # Bước 1: Đọc và chuẩn bị dữ liệu
    # --------------------------------------------------------

    splits = load_splits()

    # Lấy dữ liệu đầu vào X và nhãn Y của tập train, validation.
    # Phần tử thứ ba trong mỗi tập được bỏ qua vì không dùng ở đây.
    x_train_np, y_train_np, _ = splits["train"]
    x_val_np, y_val_np, _ = splits["val"]

    # Chuyển dữ liệu NumPy thành Tensor để PyTorch xử lý.
    x_train = torch.tensor(x_train_np)
    y_train = torch.tensor(y_train_np)
    x_val = torch.tensor(x_val_np)

    print(f"Train {len(x_train)} dòng, val {len(x_val)} dòng")

    # --------------------------------------------------------
    # Bước 2: Khởi tạo mô hình và bộ tối ưu
    # --------------------------------------------------------

    # Tạo mô hình SkillCAE với các trọng số được khởi tạo ban đầu.
    model = SkillCAE()

    # Adam sử dụng gradient để cập nhật các tham số có thể học.
    optimizer = torch.optim.Adam(
        model.parameters(),
        lr=LEARNING_RATE,
        weight_decay=WEIGHT_DECAY,
    )

    # --------------------------------------------------------
    # Bước 3: Cấu hình các hàm loss
    # --------------------------------------------------------

    # Đếm số mẫu có nhãn dương trong từng cột của tập train.
    # dim=0: cộng theo chiều hàng để đếm số mẫu dương của từng nhãn.
    # clamp(min=1): tránh chia cho 0 nếu một nhãn không có mẫu dương.
    positives = y_train.sum(dim=0).clamp(min=1)

    # Tính trọng số cho từng nhãn.
    # Nhãn càng ít mẫu dương thì pos_weight thường càng lớn.
    pos_weight = (len(y_train) - positives) / positives

    # BCE đo sai lệch giữa logits dự đoán và nhãn thực tế.
    # Hàm tự xử lý sigmoid bên trong, không cần gọi sigmoid trước.
    bce = nn.BCEWithLogitsLoss(pos_weight=pos_weight)

    # MSE đo sai lệch giữa embedding gốc và embedding tái tạo.
    mse = nn.MSELoss()

    # --------------------------------------------------------
    # Bước 4: Khởi tạo thông tin theo dõi quá trình huấn luyện
    # --------------------------------------------------------

    best_f1 = -1.0
    best_state = None
    best_epoch = 0

    # Huấn luyện tối đa MAX_EPOCHS epoch.
    for epoch in range(1, MAX_EPOCHS + 1):
        model.train()  # Bật chế độ huấn luyện, cho phép Dropout hoạt động.

        # Xáo trộn thứ tự các mẫu ở mỗi epoch.
        order = torch.randperm(len(x_train))

        # Tổng loss của các batch, dùng để tính loss trung bình của epoch.
        total = 0.0

        # ====================================================
        # 4.1. HUẤN LUYỆN THEO TỪNG BATCH
        # ====================================================

        # Chia tập train thành các batch, mỗi batch tối đa 32 mẫu.
        for start in range(0, len(order), BATCH_SIZE):
            idx = order[start : start + BATCH_SIZE]

            # Lấy dữ liệu đầu vào và nhãn tương ứng của batch hiện tại.
            xb = x_train[idx]
            yb = y_train[idx]

            # Bước 1: Lan truyền xuôi (forward).
            # logits: điểm dự đoán cho 11 nhãn kỹ năng.
            # recon : embedding được tái tạo từ vector latent.
            logits, recon = model(xb)

            # Bước 2: Tính tổng loss.
            # Loss tổng = loss phân loại + hệ số * loss tái tạo.
            # Nhân INPUT_SCALE ở cả hai đầu vào làm thay đổi thang đo MSE.
            loss = bce(logits, yb) + RECON_WEIGHT * mse(
                recon * INPUT_SCALE,
                xb * INPUT_SCALE,
            )

            # Bước 3: Xóa gradient từ lần cập nhật trước.
            optimizer.zero_grad()

            # Bước 4: Lan truyền ngược để tính gradient của các tham số.
            loss.backward()

            # Bước 5: Adam dùng gradient để cập nhật trọng số mô hình.
            optimizer.step()

            # Cộng loss theo số mẫu trong batch.
            total += loss.item() * len(idx)

        # ====================================================
        # 4.2. ĐÁNH GIÁ TRÊN TẬP VALIDATION
        # ====================================================

        # Dự đoán xác suất rồi tính các chỉ số đánh giá.
        # Validation dùng để theo dõi và chọn epoch tốt nhất.
        val_metrics = compute_metrics(
            y_val_np,
            predict_prob(model, x_val),
        )

        # Nếu Macro-F1 cao hơn trước đó, lưu lại trọng số hiện tại.
        if val_metrics["macro_f1"] > best_f1:
            best_f1 = val_metrics["macro_f1"]
            best_epoch = epoch

            # Sao chép trọng số để giữ nguyên trạng thái tốt nhất.
            best_state = {k: v.clone() for k, v in model.state_dict().items()}

        # In thông tin ở epoch đầu tiên và sau mỗi 5 epoch.
        if epoch % 5 == 0 or epoch == 1:
            print(
                f"Epoch {epoch:3d} | "
                f"Loss {total / len(x_train):.4f} | "
                f"Val Macro-F1 {val_metrics['macro_f1']:.3f} | "
                f"Exact Match {val_metrics['exact_match']:.3f}"
            )

        # Nếu đã qua PATIENCE epoch mà Macro-F1 không cải thiện,
        # dừng huấn luyện sớm để tránh tiếp tục học không cần thiết.
        if epoch - best_epoch >= PATIENCE:
            print(f"Dừng sớm ở epoch {epoch} (epoch tốt nhất: {best_epoch})")
            break

    # --------------------------------------------------------
    # Bước 5: Khôi phục và đánh giá mô hình tốt nhất
    # --------------------------------------------------------

    # Bảo đảm đã có trọng số tốt nhất trước khi khôi phục.
    if best_state is None:
        raise RuntimeError(
            "Không có trọng số mô hình tốt nhất. Hãy kiểm tra dữ liệu validation và quá trình huấn luyện."
        )

    # Đưa mô hình về trạng thái có Macro-F1 validation cao nhất.
    model.load_state_dict(best_state)

    # Đánh giá lại mô hình tốt nhất trên tập validation.
    metrics = compute_metrics(
        y_val_np,
        predict_prob(model, x_val),
    )

    print_metrics(
        metrics,
        f"CAE trên tập validation, epoch tốt nhất: {best_epoch}",
    )

    # --------------------------------------------------------
    # Bước 6: Lưu trọng số, cấu hình và báo cáo
    # --------------------------------------------------------

    # Lưu trọng số mô hình tốt nhất.
    torch.save(best_state, MODEL_PATH)

    # Lưu thông tin cấu hình để sử dụng khi nạp mô hình.
    CONFIG_PATH.write_text(
        json.dumps(
            {
                "labels": LABELS,
                "threshold": THRESHOLD,
                "best_epoch": best_epoch,
                "seed": SEED,
                "embedding_model": "bge-m3",
            },
            ensure_ascii=False,
            indent=2,
        ),
        encoding="utf-8",
    )

    # Lưu các chỉ số đánh giá trên validation.
    REPORT_PATH.write_text(
        json.dumps(
            metrics,
            ensure_ascii=False,
            indent=2,
        ),
        encoding="utf-8",
    )

    # --------------------------------------------------------
    # Bước 7: So sánh với Logistic Regression nếu có báo cáo
    # --------------------------------------------------------

    logreg_path = ARTIFACTS_DIR / "report_logreg.json"

    if logreg_path.exists():
        # Đọc kết quả Logistic Regression đã được lưu trước đó.
        base = json.loads(logreg_path.read_text(encoding="utf-8"))

        # So sánh hai mô hình trên cùng tập validation.
        print("\nSo sánh trên tập validation:")
        print(f"{'':20s}{'Exact Match':>12s}{'Macro-F1':>10s}{'Micro-F1':>10s}")

        print(
            f"{'Logistic Regression':20s}"
            f"{base['exact_match']:12.3f}"
            f"{base['macro_f1']:10.3f}"
            f"{base['micro_f1']:10.3f}"
        )

        print(
            f"{'CAE':20s}{metrics['exact_match']:12.3f}{metrics['macro_f1']:10.3f}{metrics['micro_f1']:10.3f}"
        )

    print(f"\nĐã lưu trọng số mô hình: {MODEL_PATH}")
    print(f"Đã lưu cấu hình: {CONFIG_PATH}")
    print(f"Đã lưu báo cáo: {REPORT_PATH}")


# Chỉ chạy hàm main khi thực thi file này trực tiếp dưới dạng module.
if __name__ == "__main__":
    main()
