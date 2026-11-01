"""Đo SLM trên dữ liệu gán nhãn: nhãn chính, nhãn phụ, thực thể, tốc độ. Dùng để có baseline trước fine-tune và so sau.

Cần Ollama đang chạy và đã pull model. Chạy trong thư mục advisor:
  python -m scripts.eval_slm                       # qwen3:4b trên tập test
  python -m scripts.eval_slm --split val --limit 20
  python -m scripts.eval_slm --model usas-slm      # model đã fine-tune

Ghi từng dự đoán vào data/slm/ket_qua/<model>_<split>.jsonl để xem lại câu sai.
"""

import argparse
import asyncio
import json
import statistics
import sys
import time
from collections import Counter, defaultdict
from typing import Any

import httpx

from app.core.config import get_settings
from app.schemas.slm import INTENTS
from app.services.ollama import OllamaClient
from app.services.slm_classifier import OllamaSlm
from scripts.build_slm_jsonl import DEFAULT_OUT

RESULT_DIR = DEFAULT_OUT.parent / "ket_qua"


# So dự đoán với nhãn, trả các con số tổng hợp. preds[i] là output dự đoán (type, nhan_phu, thuc_the) của records[i]
def score(records: list[dict[str, Any]], preds: list[dict[str, Any]]) -> dict[str, Any]:
    n = len(records)
    gold = [r["output"] for r in records]
    by_type: dict[str, Counter] = defaultdict(Counter)
    confusion: Counter = Counter()
    for g, p in zip(gold, preds, strict=True):
        by_type[g["type"]]["n"] += 1
        by_type[g["type"]]["dung"] += g["type"] == p["type"]
        if g["type"] != p["type"]:
            confusion[(g["type"], p["type"])] += 1
    two = [(g, p) for g, p in zip(gold, preds, strict=True) if g["nhan_phu"]]
    return {
        "so_dong": n,
        "nhan_chinh": sum(g["type"] == p["type"] for g, p in zip(gold, preds, strict=True)) / n,
        "nhan_phu": sum(g["nhan_phu"] == p["nhan_phu"] for g, p in zip(gold, preds, strict=True)) / n,
        "nhan_phu_tin_hai_y": sum(g["nhan_phu"] == p["nhan_phu"] for g, p in two) / len(two) if two else None,
        "dung_khoa_thuc_the": sum(
            set(g["thuc_the"]) == set(p["thuc_the"]) for g, p in zip(gold, preds, strict=True)
        )
        / n,
        "dung_het_thuc_the": sum(g["thuc_the"] == p["thuc_the"] for g, p in zip(gold, preds, strict=True))
        / n,
        "theo_loai": {t: by_type[t]["dung"] / by_type[t]["n"] for t in INTENTS if by_type[t]["n"]},
        "nham_nhieu_nhat": [f"{g} -> {p}: {c}" for (g, p), c in confusion.most_common(5)],
    }


async def run(model: str, split: str, limit: int | None) -> None:
    settings = get_settings()
    records = [json.loads(line) for line in DEFAULT_OUT.read_text(encoding="utf-8").splitlines()]
    records = [r for r in records if r["split"] == split][:limit]
    async with httpx.AsyncClient(
        base_url=settings.ollama_base_url, timeout=settings.llm_timeout_seconds
    ) as http:
        client = OllamaClient(
            http, model, num_ctx=settings.llm_num_ctx, num_gpu=settings.llm_num_gpu, seed=settings.llm_seed
        )
        slm = OllamaSlm(client)
        preds, rows, seconds = [], [], []
        for i, r in enumerate(records, start=1):
            start = time.perf_counter()
            result = await slm.read(
                r["input"]["cau_hoi"], r["input"]["ho_so"], r["input"]["cau_he_thong_dang_hoi"]
            )
            seconds.append(time.perf_counter() - start)
            pred = {"type": result.type, "nhan_phu": result.nhan_phu, "thuc_the": result.thuc_the}
            preds.append(pred)
            rows.append({"id": r["id"], "cau_hoi": r["input"]["cau_hoi"], "nhan": r["output"], "du_doan": pred,
                         "confidence": round(result.confidence, 4), "giay": round(seconds[-1], 2)})  # fmt: skip
            print(f"\r{i}/{len(records)}", end="", flush=True)
    print()

    RESULT_DIR.mkdir(parents=True, exist_ok=True)
    out = RESULT_DIR / f"{model.replace(':', '_').replace('/', '_')}_{split}.jsonl"
    out.write_text("".join(json.dumps(row, ensure_ascii=False) + "\n" for row in rows), encoding="utf-8")

    result = score(records, preds)
    conf_right = [
        row["confidence"]
        for row, r in zip(rows, records, strict=True)
        if row["du_doan"]["type"] == r["output"]["type"]
    ]
    conf_wrong = [
        row["confidence"]
        for row, r in zip(rows, records, strict=True)
        if row["du_doan"]["type"] != r["output"]["type"]
    ]
    result |= {
        "giay_trung_binh": round(statistics.mean(seconds), 2),
        "giay_p90": round(sorted(seconds)[int(0.9 * (len(seconds) - 1))], 2),
        "lui_ve_mo_ho": sum(row["confidence"] == 0.0 for row in rows),
        "confidence_khi_dung": round(statistics.mean(conf_right), 3) if conf_right else None,
        "confidence_khi_sai": round(statistics.mean(conf_wrong), 3) if conf_wrong else None,
        "prompt_version": slm.prompt_version,
    }
    print(f"Model {model}, tập {split}")
    print(json.dumps(result, ensure_ascii=False, indent=2))
    print(f"Chi tiết từng câu: {out}")


def main() -> None:
    sys.stdout.reconfigure(encoding="utf-8")
    parser = argparse.ArgumentParser(
        description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter
    )
    parser.add_argument("--model", default=get_settings().slm_model)
    parser.add_argument("--split", default="test", choices=("train", "val", "test"))
    parser.add_argument("--limit", type=int, default=None)
    args = parser.parse_args()
    asyncio.run(run(args.model, args.split, args.limit))


if __name__ == "__main__":
    main()
