"""Đánh giá kỹ model CAE đã lưu:
nhãn nào hay bị nhầm, theo từng bước, theo ngưỡng, danh sách câu sai

Chạy (sau khi đã train_cae):  python -m ml.skill_picker.evaluate
Ra:    artifacts/report.json
"""

import json
import sys
from collections import Counter

import numpy as np
import torch

from ml.skill_picker.data import LABELS
from ml.skill_picker.dataset import load_splits
from ml.skill_picker.embed import ARTIFACTS_DIR
from ml.skill_picker.metrics import compute_metrics
from ml.skill_picker.model import SkillCAE
from ml.skill_picker.train_cae import MODEL_PATH, predict_prob

REPORT_PATH = ARTIFACTS_DIR / "report.json"

# Đếm các kiểu sai. Với mỗi tin nhắn: extra = nhãn đoán thừa, missing = nhãn bị bỏ sót.
#   extra_pairs: (nhãn đáp án, nhãn bị gắn thừa) -> số lần, nghĩa là "câu thuộc nhãn A bị gắn thêm nhãn B"
#   missing: nhãn đáp án -> số lần bị bỏ sót


def count_errors(y_true: np.ndarray, prob: np.ndarray, threshold: float = 0.5) -> tuple[Counter, Counter]:
    # Ví dụ [True, True, True]
    pred = prob >= threshold
    truth = y_true.astype(bool)
    extra_pairs: Counter = Counter()
    missing: Counter = Counter()
    for t_row, p_row in zip(truth, pred, strict=True):
        # Tìm nhãn đoán thừa
        extra = [LABELS[j] for j in np.where(p_row & ~t_row)[0]]
        # Tìm nhãn bỏ sót
        for j in np.where(t_row & ~p_row)[0]:
            missing[LABELS[j]] += 1
        # Ghi nhận nhãn nào bị gắn thừa vào câu thuộc nhãn nào.
        for e in extra:
            for t in np.where(t_row)[0]:
                extra_pairs[(LABELS[t], e)] += 1
    return extra_pairs, missing


def main() -> None:
    sys.stdout.reconfigure(encoding="utf-8")  # PowerShell cũ in tiếng Việt bị lỗi mã hóa nếu thiếu dòng này
    x_val_np, y_val, val_rows = load_splits()["val"]

    model = SkillCAE()
    # Nạp trọng số đã lưu bởi train_cae
    model.load_state_dict(torch.load(MODEL_PATH))
    prob = predict_prob(model, torch.tensor(x_val_np))

    # 1. So với logistic
    cae = compute_metrics(y_val, prob)
    base = json.loads((ARTIFACTS_DIR / "report_logreg.json").read_text(encoding="utf-8"))
    print("1. So sánh hai model trên tập val (dữ liệu tạo thêm, chỉ để kiểm tra code)")
    print(f"   {'':20s}{'Exact match':>12s}{'Macro-F1':>10s}{'Micro-F1':>10s}")
    for name, m in (("Logistic Regression", base), ("CAE", cae)):
        print(f"   {name:20s}{m['exact_match']:12.3f}{m['macro_f1']:10.3f}{m['micro_f1']:10.3f}")
    # 2. Các kiểu nhầm
    extra_pairs, missing = count_errors(y_val, prob)
    print("\n2. Các kiểu nhầm nhiều nhất")
    print("   Câu thuộc nhãn A bị gắn thừa nhãn B:")
    for (a, b), n in extra_pairs.most_common(6):
        print(f"     {a} -> thừa {b}: {n} lần")
    print("   Nhãn bị bỏ sót:", dict(missing.most_common(5)) or "không có")

    # 3. Theo từng bước
    print("\n3. Exact match theo bước hiện tại")
    pred = (prob >= 0.5).astype(int)
    exact_row = (pred == y_val.astype(int)).all(axis=1)
    steps = np.array([r["current_step"] for r in val_rows])
    by_step = {}
    for step in sorted(set(steps)):
        mask = steps == step
        by_step[step] = {"n": int(mask.sum()), "exact_match": float(exact_row[mask].mean())}
        print(f"   {step}: {exact_row[mask].mean():.3f} ({mask.sum()} dòng)")

    # 4. Theo ngưỡng
    print("\n4. Đổi ngưỡng chọn nhãn (hiện dùng 0,5)")
    print(f"   {'ngưỡng':8s}{'Exact':>8s}{'Macro-F1':>10s}{'Không chắc':>12s}")
    by_threshold = {}
    for th in (0.5, 0.6, 0.7, 0.8, 0.9):
        m = compute_metrics(y_val, prob, th)
        by_threshold[str(th)] = {k: m[k] for k in ("exact_match", "macro_f1", "unsure_rate")}
        print(f"   {th:<8}{m['exact_match']:8.3f}{m['macro_f1']:10.3f}{m['unsure_rate']:12.3f}")

    # 5. Câu sai
    wrong = np.where(~exact_row)[0]
    print(f"\n5. {len(wrong)}/{len(val_rows)} câu đoán sai. 10 câu đầu:")
    for i in wrong[:10]:
        got = [LABELS[j] for j in range(len(LABELS)) if prob[i][j] >= 0.5]
        print(f"   [{val_rows[i]['current_step']}] {val_rows[i]['message'][:70]}")
        print(f"        đáp án: {val_rows[i]['labels']} | đoán: {', '.join(got) or '(không chắc)'}")

    report = {
        "note": "Đo trên tập val dữ liệu tạo thêm, chưa có test thật",
        "cae": cae,
        "logreg": base,
        "by_step": by_step,
        "by_threshold": by_threshold,
        "extra_pairs": {f"{a} -> {b}": n for (a, b), n in extra_pairs.most_common()},
        "missing": dict(missing),
    }
    REPORT_PATH.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"\nĐã lưu {REPORT_PATH}")


if __name__ == "__main__":
    main()
