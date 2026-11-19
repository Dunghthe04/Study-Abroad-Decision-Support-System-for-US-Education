"""Phần kiểm tra và minh họa cho báo cáo đo SLM: kiểm tra dữ liệu (số lượng, trùng câu giữa các tập) và chọn ví dụ lỗi tiêu biểu.
Không cần Ollama. slm.eval.slm_report gọi các hàm này để ghi vào REPORT.md."""

import hashlib
import json
import unicodedata
from collections import Counter
from pathlib import Path
from typing import Any

from slm.eval import slm_metrics as m

# Các loại lỗi đáng đưa ví dụ, theo thứ tự (bỏ "dung_het" và "sai_thu_tu" vì không có hoặc quá hiếm)
EXAMPLE_KINDS = ("json_loi", "thieu_y", "thua_y", "sai_loai", "sai_nhiem_vu", "sai_thuc_the", "sai_follow_up")


def normalize_question(text: str) -> str:
    """Chữ thường, gộp khoảng trắng, chuẩn hóa dấu: hai câu chỉ khác hoa thường hay khoảng trắng coi là trùng."""
    return " ".join(unicodedata.normalize("NFC", text).lower().split())


def data_audit(records: list[dict[str, Any]]) -> dict[str, Any]:
    """Số tin, số ý theo từng tập và số câu trùng chữ giữa các tập (rò rỉ dữ liệu: câu của test mà train hay val đã có).
    records: các dòng của du_lieu_slm.jsonl ({"split", "input": {"question"}, "output": {"intents"}})."""
    per_split: dict[str, dict[str, int]] = {}
    questions: dict[str, set[str]] = {}
    duplicates_in_test = 0
    for r in records:
        split = r["split"]
        stats = per_split.setdefault(split, {"messages": 0, "intents": 0, "multi_intent": 0})
        n_intents = len((r.get("output") or {}).get("intents", []))
        stats["messages"] += 1
        stats["intents"] += n_intents
        stats["multi_intent"] += n_intents >= 2
        question = (r.get("input") or {}).get("question")
        if question is None:  # dòng không có câu hỏi (bản dữ liệu rút gọn) chỉ được đếm, không kiểm trùng
            continue
        key = normalize_question(question)
        seen = questions.setdefault(split, set())
        if split == "test" and key in seen:
            duplicates_in_test += 1
        seen.add(key)
    test, train, val = (
        questions.get("test", set()),
        questions.get("train", set()),
        questions.get("val", set()),
    )
    return {
        "splits": per_split,
        "test_cung_chu_voi_train": len(test & train),
        "test_cung_chu_voi_val": len(test & val),
        "val_cung_chu_voi_train": len(val & train),
        "trung_trong_test": duplicates_in_test,
    }


def test_questions(records: list[dict[str, Any]]) -> set[str]:
    return {
        normalize_question(r["input"]["question"])
        for r in records
        if r["split"] == "test" and (r.get("input") or {}).get("question") is not None
    }


def finetune_overlap(directory: Path, test: set[str]) -> dict[str, int] | None:
    """Số câu trong các file DÙNG THẬT để huấn luyện (train.jsonl, val.jsonl dạng messages) trùng một câu của tập test.
    Phải bằng 0: đây mới là bằng chứng không rò rỉ, vì dữ liệu gốc có câu trùng nhưng bước xuất file loại chúng."""
    found: dict[str, int] = {}
    for name in ("train", "val"):
        path = directory / f"{name}.jsonl"
        if not path.is_file():
            continue
        hits = 0
        for line in path.read_text(encoding="utf-8").splitlines():
            if not line.strip():
                continue
            for message in json.loads(line).get("messages", []):
                if message.get("role") == "user":
                    try:
                        hits += normalize_question(json.loads(message["content"])["question"]) in test
                    except (ValueError, KeyError, TypeError):
                        pass
        found[name] = hits
    return found or None


def brief(intents: list[dict[str, Any]], limit: int = 90) -> str:
    """Tóm tắt các ý: loại/nhiệm vụ và các thực thể (bỏ follow_up và ô trống), cắt ngắn cho vừa một ô bảng."""
    parts = []
    for y in intents:
        entities = {
            k: v
            for k, v in m.canonical(y.get("entities") or {}, fold=False).items()
            if v not in (None, "", [], {})
        }
        for noise in ("tasks", "mode", "subtype"):  # nhiệm vụ đã nằm trong nhãn loại/nhiệm vụ
            entities.pop(noise, None)
        detail = json.dumps(entities, ensure_ascii=False) if entities else ""
        parts.append(m.label_of(y) + (f" {detail}" if detail else ""))
    text = "; ".join(parts) or "(no intent)"
    return text if len(text) <= limit else text[: limit - 1] + "…"


def error_examples(runs: dict[str, list[dict[str, Any]]], per_kind: int = 2) -> list[dict[str, Any]]:
    """Ví dụ lỗi tiêu biểu: với mỗi loại lỗi của model đầu tiên, chọn per_kind tin (chọn cố định theo mã băm của id nên chạy lại
    ra cùng ví dụ, không do người chọn), kèm nhãn đúng và điều từng model trả."""
    names = list(runs)
    by_id = {name: {r["id"]: r for r in rows} for name, rows in runs.items()}
    reference = runs[names[0]]
    chosen: list[dict[str, Any]] = []
    for kind in EXAMPLE_KINDS:
        pool = [r for r in reference if m.judge(r)["loi"] == kind]
        pool.sort(key=lambda r: hashlib.sha1(r["id"].encode()).hexdigest())
        for r in pool[:per_kind]:
            predictions = {}
            for name in names:
                other = by_id[name].get(r["id"])
                if other is None:
                    continue
                verdict = m.judge(other)["loi"]
                shown = "(no usable response)" if m.is_fallback(other) else brief(other["du_doan"]["intents"])
                predictions[name] = {"loi": verdict, "du_doan": shown}
            chosen.append(
                {
                    "id": r["id"],
                    "loi_cua_model_dau": kind,
                    "cau": r["question"],
                    "nhan_dung": brief(r["nhan"]["intents"]),
                    "du_doan": predictions,
                }
            )
    return chosen


def zero_f1_labels(
    rows: list[dict[str, Any]], table: dict[str, dict[str, float]], min_support: int = 10
) -> list[dict[str, Any]]:
    """Nhãn có F1 = 0 (từ min_support ý) và bao nhiêu ý của nhãn đó nằm trong tin model không trả được kết quả dùng được.
    F1 = 0 mà toàn bộ ý nằm trong tin hỏng nghĩa là model không 'sai nhãn' mà không trả lời được."""
    out = []
    for label, stats in sorted(table.items()):
        if stats["f1"] != 0 or stats["ho_tro"] < min_support:
            continue
        total = invalid = 0
        for r in rows:
            n = sum(x == label for y in r["nhan"]["intents"] for x in m.labels_of(y))
            total += n
            invalid += n if m.is_fallback(r) else 0
        out.append({"label": label, "so_y": total, "trong_tin_hong": invalid})
    return out


def slowest_message(rows: list[dict[str, Any]]) -> dict[str, Any] | None:
    timed = [r for r in rows if r.get("giay") is not None]
    if not timed:
        return None
    r = max(timed, key=lambda row: row["giay"])
    return {
        "giay": r["giay"],
        "cau": r["question"],
        "hop_le": not m.is_fallback(r),
        "ly_do": r.get("loi_ly_do"),
    }


def error_kind_counts(rows: list[dict[str, Any]]) -> Counter:
    return Counter(m.judge(r)["loi"] for r in rows)
