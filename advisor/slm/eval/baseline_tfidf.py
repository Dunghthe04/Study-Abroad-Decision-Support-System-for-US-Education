"""Hai mốc tham chiếu rất đơn giản để biết con số của SLM là tốt hay tệ (đọc xong không cần GPU, không cần Ollama):
  majority  luôn trả loại ý phổ biến nhất của tập train (sàn thấp nhất: model không hiểu gì cũng đạt mức này).
  tfidf-lr  TF-IDF chữ và cụm ký tự + hồi quy logistic, mỗi loại ý một bộ phân loại (nhiều nhãn). Học từ tập train.
Cả hai CHỈ dự đoán loại ý: không đoán nhiệm vụ hay thực thể, nên chỉ so sánh được ở mức loại ý (Correct category, F1 theo loại).
Chạy trong thư mục advisor, ghi cùng định dạng với eval_slm để dựng báo cáo (slm_report --baseline "Tên=file"):
  python -m slm.eval.baseline_tfidf --split test
"""

import argparse
import json
import sys
import time
from collections import Counter
from typing import Any

from app.schemas.slm import INTENTS
from slm.prepare.build_slm_jsonl import DEFAULT_OUT, RESULTS_DIR

RESULT_DIR = RESULTS_DIR / "ket_qua"


def load(split: str) -> list[dict[str, Any]]:
    rows = [json.loads(line) for line in DEFAULT_OUT.read_text(encoding="utf-8").splitlines() if line.strip()]
    return [r for r in rows if r["split"] == split]


def types_of(record: dict[str, Any]) -> list[str]:
    return [y["type"] for y in record["output"]["intents"]]


def row(
    record: dict[str, Any], kinds: list[str], confidence: float, seconds: float, model: str, split: str
) -> dict[str, Any]:
    question = record["input"]["question"]
    predicted = [{"type": k, "span": question, "entities": {}} for k in kinds]
    return {
        "id": record["id"],
        "question": question,
        "nhan": record["output"],
        "du_doan": {"intents": predicted},
        "confidence": round(confidence, 4),
        "giay": round(seconds, 5),
        "context": record["input"].get("context"),
        "lui_ve_mo_ho": False,
        "model": model,
        "prompt": "none",
        "split": split,
    }


def build(split: str) -> dict[str, list[dict[str, Any]]]:
    from sklearn.feature_extraction.text import TfidfVectorizer
    from sklearn.linear_model import LogisticRegression
    from sklearn.pipeline import make_union

    train, target = load("train"), load(split)
    questions = [r["input"]["question"] for r in train]
    # Chữ (1-2 từ) và cụm ký tự (2-5) để chịu được lỗi gõ và tiếng Việt không dấu
    vectorizer = make_union(
        TfidfVectorizer(ngram_range=(1, 2), sublinear_tf=True, lowercase=True),
        TfidfVectorizer(analyzer="char_wb", ngram_range=(2, 5), sublinear_tf=True, lowercase=True),
    )
    x_train = vectorizer.fit_transform(questions)
    classifiers = {}
    for kind in INTENTS:
        labels = [kind in types_of(r) for r in train]
        if any(labels) and not all(labels):
            classifiers[kind] = LogisticRegression(max_iter=1000, C=10.0).fit(x_train, labels)
    majority = Counter(t for r in train for t in set(types_of(r))).most_common(1)[0][0]

    tfidf_rows, majority_rows = [], []
    for r in target:
        started = time.perf_counter()
        probabilities = {
            kind: float(clf.predict_proba(vectorizer.transform([r["input"]["question"]]))[0][1])
            for kind, clf in classifiers.items()
        }
        chosen = [k for k in INTENTS if probabilities.get(k, 0.0) >= 0.5]
        if not chosen:  # tin nào cũng có ít nhất một ý: lấy loại có xác suất cao nhất
            chosen = [max(probabilities, key=probabilities.get)]
        seconds = time.perf_counter() - started
        confidence = min(probabilities[k] for k in chosen)
        tfidf_rows.append(row(r, chosen, confidence, seconds, "tfidf-lr", split))
        majority_rows.append(row(r, [majority], 1.0, 0.0, "majority", split))
    return {"baseline_tfidf": tfidf_rows, "baseline_majority": majority_rows}


def main() -> None:
    sys.stdout.reconfigure(encoding="utf-8")
    parser = argparse.ArgumentParser(
        description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter
    )
    parser.add_argument("--split", default="test", choices=("val", "test"))
    args = parser.parse_args()
    RESULT_DIR.mkdir(parents=True, exist_ok=True)
    for name, rows in build(args.split).items():
        out = RESULT_DIR / f"{name}_{args.split}.jsonl"
        out.write_text("".join(json.dumps(r, ensure_ascii=False) + "\n" for r in rows), encoding="utf-8")
        print(f"Đã ghi {len(rows)} dòng vào {out}")


if __name__ == "__main__":
    main()
