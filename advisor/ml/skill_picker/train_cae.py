"""Train CAE 1D + lớp phân loại bằng PyTorch. So kết quả với Logistic Regression.

Chạy:  python -m ml.skill_picker.train_cae
Ra:    artifacts/model.pt (trọng số tốt nhất), config.json, report_cae.json
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

# Nơi lưu kết quả sau huấn luyện
MODEL_PATH = ARTIFACTS_DIR / "model.pt"  # lưu các trọng số đã học được.
CONFIG_PATH = ARTIFACTS_DIR / "config.json"  # lưu thông tin cấu hình của mô hình.
REPORT_PATH = ARTIFACTS_DIR / "report_cae.json"  # lưu chỉ số đánh giá

# Các tham số huẩn luyện
SEED = 42  # Giúp các lần chạy có tính tái lập tốt hơn
BATCH_SIZE = 32  # Mỗi lần cập nhập trong số, mô hình xử lý tối đa 32 mẫu
MAX_EPOCHS = (
    100  # Huấn luyện tối đa 100 vòng. Một vòng là đi qua toàn bộ dữ liệu train. Mỗi vòng chia thành 32 dòng
)
PATIENCE = 10  # Dừng sớm nếu Macro-F1 trên validation không đạt mức tốt hơn trong 10 vòng liên tiếp.
LEARNING_RATE = 1e-3  # Tốc độ điều chỉnh trọng số, bằng 0.001.
WEIGHT_DECAY = 1e-4  # Hình thức điều chuẩn giúp hạn chế trọng số tăng quá lớn.
RECON_WEIGHT = 0.5  # tỉ trọng phần dựng lại vector trong loss

# Mỗi vòng : model đoán -> tính loss -> loss.backward() tìm xem sửa trọng số nào loss giảm -> oprimizer sửa trọng số


# Xác suất model đoán cho một bảng vector (qua sigmoid), trả về mảng numpy
def predict_prob(model: SkillCAE, x: torch.Tensor) -> np.ndarray:
    model.eval()  # tắt dropout khi đoán
    with torch.no_grad():
        logits, _ = model(x)
    return torch.sigmoid(logits).numpy()


def main() -> None:
    sys.stdout.reconfigure(encoding="utf-8")  # PowerShell cũ in tiếng Việt bị lỗi mã hóa nếu thiếu dòng này
    torch.manual_seed(SEED)
    np.random.seed(SEED)

    splits = load_splits()
    x_train_np, y_train_np, _ = splits["train"]
    x_val_np, y_val_np, _ = splits["val"]
    # PyTorch làm việc với tensor, không phải mảng numpy
    x_train, y_train = torch.tensor(x_train_np), torch.tensor(y_train_np)
    x_val = torch.tensor(x_val_np)
    print(f"Train {len(x_train)} dòng, val {len(x_val)} dòng")

    model = SkillCAE()
    optimizer = torch.optim.Adam(model.parameters(), lr=LEARNING_RATE, weight_decay=WEIGHT_DECAY)

    # pos_weight: nhãn càng ít dòng thì tính càng nặng (giống class_weight="balanced" của Logistic Regression)
    positives = y_train.sum(dim=0).clamp(min=1)
    pos_weight = (len(y_train) - positives) / positives
    bce = nn.BCEWithLogitsLoss(pos_weight=pos_weight)
    mse = nn.MSELoss()

    best_f1, best_state, best_epoch = -1.0, None, 0
    for epoch in range(1, MAX_EPOCHS + 1):
        model.train()
        order = torch.randperm(len(x_train))  # xáo trộn thứ tự mỗi vòng
        total = 0.0
        for start in range(0, len(order), BATCH_SIZE):
            idx = order[start : start + BATCH_SIZE]
            xb, yb = x_train[idx], y_train[idx]
            logits, recon = model(xb)
            # loss = sai nhãn bao nhiêu + 0,5 x dựng lại vector sai bao nhiêu
            # recon và xb nhân INPUT_SCALE: số trong vector rất nhỏ nên MSE gốc gần bằng 0, nhân lên cho cân với BCE
            loss = bce(logits, yb) + RECON_WEIGHT * mse(recon * INPUT_SCALE, xb * INPUT_SCALE)
            optimizer.zero_grad()
            loss.backward()
            optimizer.step()
            total += loss.item() * len(idx)

        val_metrics = compute_metrics(y_val_np, predict_prob(model, x_val))
        if val_metrics["macro_f1"] > best_f1:
            best_f1, best_epoch = val_metrics["macro_f1"], epoch
            best_state = {k: v.clone() for k, v in model.state_dict().items()}
        if epoch % 5 == 0 or epoch == 1:
            print(
                f"vòng {epoch:3d} | loss {total / len(x_train):.4f} | val macro-F1 {val_metrics['macro_f1']:.3f} | exact {val_metrics['exact_match']:.3f}"
            )
        if epoch - best_epoch >= PATIENCE:
            print(f"Dừng sớm ở vòng {epoch} (tốt nhất ở vòng {best_epoch})")
            break

    model.load_state_dict(best_state)  # quay về trọng số tốt nhất
    metrics = compute_metrics(y_val_np, predict_prob(model, x_val))
    print_metrics(metrics, f"CAE trên tập val, vòng {best_epoch} (dữ liệu tạo thêm, chỉ để kiểm tra code)")

    torch.save(best_state, MODEL_PATH)
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
    REPORT_PATH.write_text(json.dumps(metrics, ensure_ascii=False, indent=2), encoding="utf-8")

    # So với Logistic Regression nếu đã có báo cáo
    logreg_path = ARTIFACTS_DIR / "report_logreg.json"
    if logreg_path.exists():
        base = json.loads(logreg_path.read_text(encoding="utf-8"))
        print("\nSo sánh trên tập val:")
        print(f"{'':20s}{'Exact match':>12s}{'Macro-F1':>10s}{'Micro-F1':>10s}")
        print(
            f"{'Logistic Regression':20s}{base['exact_match']:12.3f}{base['macro_f1']:10.3f}{base['micro_f1']:10.3f}"
        )
        print(
            f"{'CAE':20s}{metrics['exact_match']:12.3f}{metrics['macro_f1']:10.3f}{metrics['micro_f1']:10.3f}"
        )
    print(f"\nĐã lưu {MODEL_PATH}")


if __name__ == "__main__":
    main()
