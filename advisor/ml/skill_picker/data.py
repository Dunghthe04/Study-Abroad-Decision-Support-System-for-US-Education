"""Đọc dữ liệu train của model chọn skill và biến mỗi dòng thành (đoạn chữ, vector nhãn).
1. đọc data từ file
2. ghép thông tin thành văn bản -> để bge mã hóa
3. chuyển nhãn skill thành 0/1
Chạy để xem thống kê:  python -m ml.skill_picker.data
"""

import csv
import sys
from collections import Counter
from pathlib import Path

DATA_DIR = Path(__file__).resolve().parent / "data"
DATA_PATH = DATA_DIR / "skill_picker.csv"

# 11 nhãn, thứ tự này cố định: vị trí của nhãn trong danh sách là vị trí trong vector đầu ra của model
LABELS = [
    "skill_nhu_cau",
    "skill_bac_hoc",
    "skill_ho_so",
    "skill_danh_gia",
    "skill_lo_trinh",
    "skill_explain",
    "skill_kich_ban",
    "skill_tra_loi",
    "skill_chuyen_trung_tam",
    "ngoai_le",
    "ket_thuc",
]


# Đọc file csv, mỗi dòng thành một dict (id, current_step, last_question, message, missing, labels, source, split...)
def load_rows(path: Path = DATA_PATH) -> list[dict[str, str]]:
    with open(path, encoding="utf-8-sig", newline="") as f:
        return list(csv.DictReader(f))


# Ghép 4 trường đầu vào thành MỘT đoạn chữ để đưa cho BGE-M3
"""
row = {
    "current_step": "thu_thap_ngan_sach",
    "last_question": "Ngân sách mỗi năm của bạn là bao nhiêu?",
    "message": "Khoảng 30.000 USD",
    "missing": "GPA, ngành học",
}

==> [BƯỚC] thu_thap_ngan_sach [HỎI] Ngân sách mỗi năm của bạn là bao nhiêu? [TIN] Khoảng 30.000 USD [THIẾU] GPA, ngành học
"""


def build_text(row: dict[str, str]) -> str:
    last_question = row["last_question"].strip() or "(không có)"
    missing = row["missing"].strip() or "(không thiếu)"
    return f"[BƯỚC] {row['current_step']} [HỎI] {last_question} [TIN] {row['message']} [THIẾU] {missing}"


# Chuyển "skill_ho_so, skill_tra_loi" thành vector 11 số 0/1 (multi-hot): có nhãn nào thì ô đó là 1
# [0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0]
def labels_to_vector(labels: str) -> list[int]:
    # Tách skills ra
    names = [x.strip() for x in labels.split(",") if x.strip()]
    # Nếu k có
    unknown = [n for n in names if n not in LABELS]
    if unknown:
        raise ValueError(f"Nhãn lạ: {unknown}")
    return [1 if label in names else 0 for label in LABELS]


# Tách các dòng theo cột split (train, val, test)
"""
rows = [
    {"id": "1", "split": "train", "labels": "skill_ho_so"},
    {"id": "2", "split": "val",   "labels": "skill_tra_loi"},
    {"id": "3", "split": "train", "labels": "skill_nhu_cau"},
    {"id": "4", "split": "test",  "labels": "ket_thuc"}
]

==>
{
    "train": [
        {"id": "1", "split": "train"},
        {"id": "3", "split": "train"}
    ],
    "val": [
        {"id": "2", "split": "val"}
    ],
    "test": []
}
"""


def split_rows(rows: list[dict[str, str]]) -> dict[str, list[dict[str, str]]]:
    out: dict[str, list[dict[str, str]]] = {"train": [], "val": [], "test": []}
    for row in rows:
        out[row["split"]].append(row)
    return out


def main() -> None:
    sys.stdout.reconfigure(encoding="utf-8")  # PowerShell cũ in tiếng Việt bị lỗi mã hóa nếu thiếu dòng này
    rows = load_rows()
    parts = split_rows(rows)
    print(f"Tổng {len(rows)} dòng")
    for name, items in parts.items():
        print(f"  {name:5s}: {len(items)} dòng")
    print(f"{'nhãn':26s}{'train':>7s}{'val':>7s}{'test':>7s}")
    for label in LABELS:
        counts = [sum(label in r["labels"].split(", ") for r in parts[n]) for n in ("train", "val", "test")]
        print(f"{label:26s}{counts[0]:7d}{counts[1]:7d}{counts[2]:7d}")
    print("Số dòng có 2 nhãn:", sum(", " in r["labels"] for r in rows))
    print("Nguồn:", dict(Counter(r["source"] for r in rows)))
    print("Ví dụ đoạn chữ gửi BGE-M3:")
    print(" ", build_text(rows[0]))


if __name__ == "__main__":
    main()
