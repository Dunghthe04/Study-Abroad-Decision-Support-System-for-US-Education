"""Đo SLM trên dữ liệu gán nhãn: tách đủ ý, đúng loại, đúng thứ tự, thực thể, tốc độ. Dùng để có baseline trước fine-tune và so sau.

Cần Ollama đang chạy và đã pull model. Chạy trong thư mục advisor, dùng prompt slm_intents (khóa tiếng Anh):
  python -m scripts.eval_slm --model qwen3:4b-instruct --prompt slm_intents --shots 8     # baseline: model gốc, 8 ví dụ lấy từ train
  python -m scripts.eval_slm --model usas-slm-v4 --prompt slm_intents                       # model đã fine-tune, không cần ví dụ
Thêm --split val --limit 20 để thử nhanh. Nhãn lấy từ Excel qua build_slm_jsonl.

Ghi từng dự đoán vào data/slm/ket_qua/<model>[_shotsN]_<split>.jsonl để xem lại câu sai. Dựng bảng và biểu đồ so sánh: scripts.slm_report.
"""

import argparse
import asyncio
import hashlib
import json
import re
import statistics
import sys
import time
from collections import Counter, defaultdict
from typing import Any

import httpx

from app.core.config import get_settings
from app.schemas.slm import INTENTS, SlmTinResult
from app.services.ollama import OllamaClient, OllamaError
from app.services.skill_call import SKILLS_DIR, load_skill
from app.services.slm_classifier import FALLBACK, keep_verbatim, label_confidence
from scripts.build_slm_jsonl import DEFAULT_EXCEL, DEFAULT_OUT, read_records
from scripts.export_slm_chat import answer_text, user_message

RESULT_DIR = DEFAULT_OUT.parent / "ket_qua"


# Độ giống nhau của hai đoạn chữ theo từ: F1 giữa hai tập từ (1 là giống hệt từ, 0 là không chung từ nào)
def word_f1(gold: str, pred: str) -> float:
    a, b = Counter(re.findall(r"[^\W_]+", gold.lower())), Counter(re.findall(r"[^\W_]+", pred.lower()))
    common = sum((a & b).values())
    if not common:
        return 0.0
    precision, recall = common / sum(b.values()), common / sum(a.values())
    return 2 * precision * recall / (precision + recall)


# Loại của các ý, theo thứ tự
def types_of(output: dict[str, Any]) -> list[str]:
    return [y["type"] for y in output["intents"]]


# So dự đoán với nhãn, trả các con số tổng hợp. preds[i] là output dự đoán ({"intents": [...]}) của records[i]
def score(records: list[dict[str, Any]], preds: list[dict[str, Any]]) -> dict[str, Any]:
    n = len(records)
    gold = [r["output"] for r in records]
    same_types = [sorted(types_of(g)) == sorted(types_of(p)) for g, p in zip(gold, preds, strict=True)]
    same_order = [types_of(g) == types_of(p) for g, p in zip(gold, preds, strict=True)]
    # Thực thể chỉ so khi đã tách đúng loại và thứ tự các ý, nếu không thì không biết ý nào ứng với ý nào
    keys = [
        all(set(a["entities"]) == set(b["entities"]) for a, b in zip(g["intents"], p["intents"], strict=True))
        for g, p, ok in zip(gold, preds, same_order, strict=True)
        if ok
    ]
    values = [
        all(a["entities"] == b["entities"] for a, b in zip(g["intents"], p["intents"], strict=True))
        for g, p, ok in zip(gold, preds, same_order, strict=True)
        if ok
    ]
    # span chỉ so khi đã tách đúng loại và thứ tự. Viết lại được nên so theo chữ (F1 trên các từ), không bắt giống từng ký tự
    span_f1 = [
        word_f1(a["span"], b["span"])
        for g, p, ok in zip(gold, preds, same_order, strict=True)
        if ok
        for a, b in zip(g["intents"], p["intents"], strict=True)
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
        by_count[str(min(len(g["intents"]), 3)) + ("+" if len(g["intents"]) >= 3 else "")].append(ok)
    return {
        "so_dong": n,
        "dung_loai_intents": sum(same_types) / n,  # tách đủ ý, đúng loại (không xét thứ tự)
        "dung_thu_tu": sum(same_order) / n,  # thêm điều kiện đúng thứ tự
        "theo_so_y": {k: round(sum(v) / len(v), 3) for k, v in sorted(by_count.items())},
        "dung_khoa_entities": sum(keys) / len(keys) if keys else None,
        "dung_het_entities": sum(values) / len(values) if values else None,
        "span_f1": round(sum(span_f1) / len(span_f1), 3) if span_f1 else None,
        "span_giong_het": round(sum(f == 1 for f in span_f1) / len(span_f1), 3) if span_f1 else None,
        "thu_hoi_theo_loai": {t: recall[t]["dung"] / recall[t]["n"] for t in INTENTS if recall[t]["n"]},
        "hay_thieu": [f"{t}: {c}" for t, c in missing.most_common(5)],
        "hay_thua": [f"{t}: {c}" for t, c in extra.most_common(5)],
    }


def pick_shots(records: list[dict[str, Any]], n: int, exclude: set[str]) -> list[dict[str, Any]]:
    """n ví dụ lấy CHỈ từ tập train, cố định mỗi lần chạy: mỗi loại ý một ví dụ một ý, rồi các ví dụ nhiều ý.
    Bỏ câu trùng chữ với một câu của tập test để model không thấy trước đáp án."""
    train = [r for r in records if r["split"] == "train" and r["input"]["question"].lower() not in exclude]
    train.sort(key=lambda r: hashlib.sha1(r["id"].encode()).hexdigest())
    chosen: list[dict[str, Any]] = []
    for kind in INTENTS:
        single = next(
            (
                r
                for r in train
                if len(r["output"]["intents"]) == 1 and r["output"]["intents"][0]["type"] == kind
            ),
            None,
        )
        if single is not None:
            chosen.append(single)
    for r in train:
        if len(chosen) >= n:
            break
        if len(r["output"]["intents"]) >= 3 and r not in chosen:
            chosen.append(r)
    return chosen[:n]


# Tin gửi model: system = prompt, rồi từng ví dụ (user, assistant), rồi tin cần đọc
def build_messages(prompt: str, shots: list[dict[str, Any]], data: dict[str, Any]) -> list[dict[str, str]]:
    messages = [{"role": "system", "content": prompt}]
    for shot in shots:
        messages.append({"role": "user", "content": user_message(shot["input"])})
        messages.append({"role": "assistant", "content": answer_text(shot["output"])})
    messages.append({"role": "user", "content": user_message(data)})
    return messages


async def read_intents(
    client: OllamaClient, messages: list[dict[str, str]], question: str, context: dict[str, Any] | None
) -> SlmTinResult:
    """Gọi model, làm sạch như lúc chạy thật (keep_verbatim) rồi kiểm bằng hợp đồng.
    Sai luật hai lần thì lùi về tin mơ hồ (confidence 0), giống OllamaSlm."""
    names = {name.lower() for name in (context or {}).get("schools", [])}
    for _ in range(2):
        try:
            raw, content, logprobs = await client.chat_json_scored(messages, "json")
            raw = keep_verbatim(raw, question, names)
            return SlmTinResult.from_raw(raw, confidence=label_confidence(content, logprobs))
        except (OllamaError, ValueError, TypeError, KeyError, AttributeError):
            continue
    return FALLBACK.model_copy(deep=True)


async def run(model: str, split: str, limit: int | None, prompt: str, shots: int = 0) -> None:
    settings = get_settings()
    all_records = read_records(DEFAULT_EXCEL)
    records = [r for r in all_records if r["split"] == split][:limit]
    system, prompt_version = load_skill(SKILLS_DIR / f"{prompt}.md")
    test_questions = {r["input"]["question"].lower() for r in all_records if r["split"] == "test"}
    few = pick_shots(all_records, shots, test_questions)
    print(f"Prompt {prompt} {prompt_version}, {len(few)} ví dụ lấy từ tập train")
    async with httpx.AsyncClient(
        base_url=settings.ollama_base_url, timeout=settings.llm_timeout_seconds
    ) as http:
        client = OllamaClient(
            http, model, num_ctx=settings.llm_num_ctx, num_gpu=settings.llm_num_gpu, seed=settings.llm_seed
        )
        preds, rows, seconds = [], [], []
        fallbacks = 0  # số câu lùi về mơ hồ (model trả sai luật 2 lần)
        for i, r in enumerate(records, start=1):
            start = time.perf_counter()
            context = r["input"].get(
                "context"
            )  # ngữ cảnh của dòng (nếu có), đưa vào đầu vào như lúc chạy thật
            messages = build_messages(system, few, r["input"])
            result = await read_intents(client, messages, r["input"]["question"], context)
            fallbacks += result.confidence == 0.0
            seconds.append(time.perf_counter() - start)
            pred = {"intents": [y.model_dump() for y in result.intents]}
            preds.append(pred)
            rows.append({"id": r["id"], "question": r["input"]["question"], "nhan": r["output"], "du_doan": pred,
                         "confidence": round(result.confidence, 4), "giay": round(seconds[-1], 2),
                         "context": context, "lui_ve_mo_ho": result.confidence == 0.0, "model": model, "prompt": prompt, "split": split})  # fmt: skip
            print(f"\r{i}/{len(records)}", end="", flush=True)
    print()

    RESULT_DIR.mkdir(parents=True, exist_ok=True)
    tag = f"_shots{len(few)}" if few else ""  # baseline có ví dụ ghi riêng, không đè bản không ví dụ
    out = RESULT_DIR / f"{model.replace(':', '_').replace('/', '_')}{tag}_{split}.jsonl"
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
        "prompt_version": prompt_version,
        "so_vi_du": len(few),
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
    # Model fine-tune phải dùng đúng prompt lúc huấn luyện (slm_intents)
    parser.add_argument("--prompt", default=get_settings().slm_prompt)
    # Số ví dụ lấy từ train đưa kèm làm few-shot. Model gốc dùng 8, model đã fine-tune dùng 0
    parser.add_argument("--shots", type=int, default=0)

    args = parser.parse_args()
    asyncio.run(run(args.model, args.split, args.limit, args.prompt, args.shots))


if __name__ == "__main__":
    main()
