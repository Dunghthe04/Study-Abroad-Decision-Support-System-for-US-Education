"""Biến mỗi dòng dữ liệu thành vector 1024 số bằng BGE-M3 (chạy qua Ollama) rồi lưu ra file.

Chạy (cần Ollama đang bật và đã `ollama pull bge-m3`):  python -m ml.skill_picker.embed
Ra: ml/skill_picker/artifacts/vectors.npy (mỗi dòng dữ liệu một vector) và vectors_meta.json
"""

import json
import sys
from pathlib import Path

import httpx
import numpy as np

from ml.skill_picker.data import build_text, load_rows

ARTIFACTS_DIR = Path(__file__).resolve().parent / "artifacts"
VECTORS_PATH = ARTIFACTS_DIR / "vectors.npy"
META_PATH = ARTIFACTS_DIR / "vectors_meta.json"


OLLAMA_URL = "http://localhost:11434"
MODEL = "bge-m3"
BATCH = 32  # gửi 32 đoạn chữ mỗi lần cho nhanh
DIM = 1024  # BGE-M3 trả vector 1024 số


# Gửi 1 nhóm đoạn chữ cho Ollama, nhận về danh sách (mảng numpy) chứa Vector
def embed_texts(texts: list[str], client: httpx.Client) -> np.ndarray:
    res = client.post("/api/embed", json={"model": MODEL, "input": texts})
    # Kiểm tra trạng thái phản hồi
    res.raise_for_status()
    vectors = np.array(res.json()["embeddings"], dtype=np.float32)
    # Nếu kích thước ko bằng độ dài text , DIM số chiều mong muốn
    if vectors.shape != (len(texts), DIM):
        raise ValueError(f"Vector sai kích thước: {vectors.shape}, cần {(len(texts), DIM)}")
    return vectors


def main() -> None:
    sys.stdout.reconfigure(encoding="utf-8")  # PowerShell cũ in tiếng Việt bị lỗi mã hóa nếu thiếu dòng này
    rows = load_rows()
    # Chuyển dữ liệu thành đoạn text
    texts = [build_text(r) for r in rows]
    # Tạo danh sách lưu các batch
    parts = []
    # Gọi Ollama tính embedding theo từng batch
    with httpx.Client(base_url=OLLAMA_URL, timeout=300) as client:
        for start in range(0, len(texts), BATCH):
            # texts[0:8]    # 8 văn bản đầu texts[8:16]   # 8 văn bản tiếp theo texts[16:24]  # Chỉ lấy các phần tử còn lại, tối đa 4
            parts.append(embed_texts(texts[start : start + BATCH], client))
            print(f"  đã tính {min(start + BATCH, len(texts))}/{len(texts)}", file=sys.stderr)
    # Ghép các vector thành ma trận
    vectors = np.vstack(parts)

    ARTIFACTS_DIR.mkdir(exist_ok=True)
    np.save(VECTORS_PATH, vectors)
    # ids cùng thứ tự với vectors: dòng i của vectors là của rows[i]
    META_PATH.write_text(
        json.dumps({"model": MODEL, "dim": DIM, "ids": [r["id"] for r in rows]}, ensure_ascii=False),
        encoding="utf-8",
    )
    print(f"Đã lưu {vectors.shape[0]} vector, mỗi vector {vectors.shape[1]} số -> {VECTORS_PATH}")


if __name__ == "__main__":
    main()
