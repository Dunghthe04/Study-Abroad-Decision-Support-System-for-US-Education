"""Đo SLM trên dữ liệu gán nhãn: tách đủ ý, đúng loại, đúng thứ tự, thực thể, tốc độ. Dùng để có baseline trước fine-tune và so sau.

Bộ phân loại (OllamaSlm) phải đã trả danh sách ý cac_y. Cần Ollama đang chạy và đã pull model. Chạy trong thư mục advisor:
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
from app.services.slm_classifier import OllamaSlm, output_format
from scripts.build_slm_jsonl import DEFAULT_OUT

RESULT_DIR = DEFAULT_OUT.parent / "ket_qua"


# Loại của các ý, theo thứ tự
def types_of(output: dict[str, Any]) -> list[str]:
    return [y["type"] for y in output["cac_y"]]


# So dự đoán với nhãn, trả các con số tổng hợp. preds[i] là output dự đoán ({"cac_y": [...]}) của records[i]
def score(records: list[dict[str, Any]], preds: list[dict[str, Any]]) -> dict[str, Any]:
    n = len(records)
    gold = [r["output"] for r in records]
    same_types = [sorted(types_of(g)) == sorted(types_of(p)) for g, p in zip(gold, preds, strict=True)]
    same_order = [types_of(g) == types_of(p) for g, p in zip(gold, preds, strict=True)]
    # Thực thể chỉ so khi đã tách đúng loại và thứ tự các ý, nếu không thì không biết ý nào ứng với ý nào
    keys = [
        all(set(a["thuc_the"]) == set(b["thuc_the"]) for a, b in zip(g["cac_y"], p["cac_y"], strict=True))
        for g, p, ok in zip(gold, preds, same_order, strict=True)
        if ok
    ]
    values = [
        all(a["thuc_the"] == b["thuc_the"] for a, b in zip(g["cac_y"], p["cac_y"], strict=True))
        for g, p, ok in zip(gold, preds, same_order, strict=True)
        if ok
    ]
    recall: dict[str, Counter] = defaultdict(Counter)
    missing: Counter = Counter()
    extra: Counter = Counter()
    for g, p in zip(gold, preds, strict=True):
        want, got = Counter(types_of(g)), Counter(types_of(p))
        for kind, count in want.items():
            recall[kind]["n"] += count
            recall[kind]["dung"] += min(count, got[kind])
        missing.update(want - got)
        extra.update(got - want)
    by_count: dict[str, list[bool]] = defaultdict(list)
    for g, ok in zip(gold, same_types, strict=True):
        by_count[str(min(len(g["cac_y"]), 3)) + ("+" if len(g["cac_y"]) >= 3 else "")].append(ok)
    return {
        "so_dong": n,
        "dung_loai_cac_y": sum(same_types) / n,  # tách đủ ý, đúng loại (không xét thứ tự)
        "dung_thu_tu": sum(same_order) / n,  # thêm điều kiện đúng thứ tự
        "theo_so_y": {k: round(sum(v) / len(v), 3) for k, v in sorted(by_count.items())},
        "dung_khoa_thuc_the": sum(keys) / len(keys) if keys else None,
        "dung_het_thuc_the": sum(values) / len(values) if values else None,
        "thu_hoi_theo_loai": {t: recall[t]["dung"] / recall[t]["n"] for t in INTENTS if recall[t]["n"]},
        "hay_thieu": [f"{t}: {c}" for t, c in missing.most_common(5)],
        "hay_thua": [f"{t}: {c}" for t, c in extra.most_common(5)],
    }


async def run(model: str, split: str, limit: int | None, fmt: str) -> None:
    settings = get_settings()
    records = [json.loads(line) for line in DEFAULT_OUT.read_text(encoding="utf-8").splitlines()]
    records = [r for r in records if r["split"] == split][:limit]
    async with httpx.AsyncClient(
        base_url=settings.ollama_base_url, timeout=settings.llm_timeout_seconds
    ) as http:
        client = OllamaClient(
            http, model, num_ctx=settings.llm_num_ctx, num_gpu=settings.llm_num_gpu, seed=settings.llm_seed
        )
        slm = OllamaSlm(client, output_format(fmt))
        preds, rows, seconds = [], [], []
        fallbacks = 0  # số câu lùi về mơ hồ (model trả sai luật 2 lần)
        for i, r in enumerate(records, start=1):
            start = time.perf_counter()
            result = await slm.read(
                r["input"]["cau_hoi"], r["input"]["ho_so"], r["input"]["cau_he_thong_dang_hoi"]
            )
            fallbacks += result.confidence == 0.0
            seconds.append(time.perf_counter() - start)
            pred = {"cac_y": [y.model_dump() for y in (*result.cac_y, *result.con_lai)]}
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
        if sorted(types_of(row["du_doan"])) == sorted(types_of(r["output"]))
    ]
    conf_wrong = [
        row["confidence"]
        for row, r in zip(rows, records, strict=True)
        if sorted(types_of(row["du_doan"])) != sorted(types_of(r["output"]))
    ]
    result |= {
        "giay_trung_binh": round(statistics.mean(seconds), 2),
        "giay_p90": round(sorted(seconds)[int(0.9 * (len(seconds) - 1))], 2),
        "lui_ve_mo_ho": fallbacks,
        "confidence_khi_dung": round(statistics.mean(conf_right), 3) if conf_right else None,
        "confidence_khi_sai": round(statistics.mean(conf_wrong), 3) if conf_wrong else None,
        "prompt_version": slm.prompt_version,
        "dinh_dang": fmt,
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
    parser.add_argument("--format", default=get_settings().slm_format, choices=("json", "schema"))

    args = parser.parse_args()
    asyncio.run(run(args.model, args.split, args.limit, args.format))


if __name__ == "__main__":
    main()
