"""Đo SLM trên dữ liệu gán nhãn: tách đủ ý, đúng loại, đúng thứ tự, thực thể, tốc độ. Dùng để có baseline trước fine-tune và so sau.

Cần Ollama đang chạy và đã pull model. Chạy trong thư mục advisor. Có hai đường, chọn bằng --prompt:
  Đường tiếng Anh (prompt slm_intents, khóa tiếng Anh): model gốc few-shot và model fine-tune bằng dữ liệu tiếng Anh
    python -m scripts.eval_slm --model qwen3:4b-instruct --prompt slm_intents --shots 8     # baseline: model gốc, 8 ví dụ lấy từ train
    python -m scripts.eval_slm --model usas-slm-v4 --prompt slm_intents                       # model đã fine-tune, không cần ví dụ
  Đường tiếng Việt (prompt slm_phan_loai_cac_y*, khóa tiếng Việt): model v3 cũ
    python -m scripts.eval_slm --model usas-slm-v3 --prompt slm_phan_loai_cac_y_ngan_v22
Thêm --split val --limit 20 để thử nhanh. Nhãn lấy từ Excel qua build_slm_jsonl (đã đổi sang tên nội bộ để chấm).

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
from app.services.slm_classifier import FALLBACK, OllamaSlm, keep_verbatim, label_confidence, output_format
from scripts.build_slm_jsonl import DEFAULT_EXCEL, DEFAULT_OUT, EN_SHEETS, intents_to_internal, read_all
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
    # doan chỉ so khi đã tách đúng loại và thứ tự. Viết lại được nên so theo chữ (F1 trên các từ), không bắt giống từng ký tự
    doan_f1 = [
        word_f1(a["doan"], b["doan"])
        for g, p, ok in zip(gold, preds, same_order, strict=True)
        if ok
        for a, b in zip(g["cac_y"], p["cac_y"], strict=True)
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
        "doan_f1": round(sum(doan_f1) / len(doan_f1), 3) if doan_f1 else None,
        "doan_giong_het": round(sum(f == 1 for f in doan_f1) / len(doan_f1), 3) if doan_f1 else None,
        "thu_hoi_theo_loai": {t: recall[t]["dung"] / recall[t]["n"] for t in INTENTS if recall[t]["n"]},
        "hay_thieu": [f"{t}: {c}" for t, c in missing.most_common(5)],
        "hay_thua": [f"{t}: {c}" for t, c in extra.most_common(5)],
    }


# ---- Đường tiếng Anh: prompt slm_intents (khóa tiếng Anh) cho model gốc (few-shot) và model fine-tune bằng dữ liệu tiếng Anh ----
# Câu trả lời của model là {"intents": [...]}; đổi về dạng nội bộ để chấm bằng cùng bộ đo (slm_metrics, slm_report)
ENGLISH_PROMPTS = {"slm_intents"}


def pick_shots(english: list[dict[str, Any]], n: int, exclude: set[str]) -> list[dict[str, Any]]:
    """n ví dụ lấy CHỈ từ tập train, cố định mỗi lần chạy: mỗi loại ý một ví dụ một ý, rồi các ví dụ nhiều ý.
    Bỏ câu trùng chữ với một câu của tập test để model không thấy trước đáp án."""
    train = [e for e in english if e["split"] == "train" and e["input"]["question"].lower() not in exclude]
    train.sort(key=lambda e: hashlib.sha1(e["id"].encode()).hexdigest())
    chosen: list[dict[str, Any]] = []
    for kind in EN_SHEETS:
        single = next(
            (
                e
                for e in train
                if len(e["output"]["intents"]) == 1 and e["output"]["intents"][0]["type"] == kind
            ),
            None,
        )
        if single is not None:
            chosen.append(single)
    for e in train:
        if len(chosen) >= n:
            break
        if len(e["output"]["intents"]) >= 3 and e not in chosen:
            chosen.append(e)
    return chosen[:n]


def english_messages(prompt: str, shots: list[dict[str, Any]], data: dict[str, Any]) -> list[dict[str, str]]:
    messages = [{"role": "system", "content": prompt}]
    for shot in shots:
        messages.append({"role": "user", "content": user_message(shot["input"])})
        messages.append({"role": "assistant", "content": answer_text(shot["output"])})
    messages.append({"role": "user", "content": user_message(data)})
    return messages


async def read_english(
    client: OllamaClient, messages: list[dict[str, str]], question: str, context: dict[str, Any] | None
) -> SlmTinResult:
    """Gọi model, đổi đáp án tiếng Anh về dạng nội bộ, làm sạch như lúc chạy thật (keep_verbatim) rồi kiểm bằng hợp đồng.
    Sai luật hai lần thì lùi về tin mơ hồ (confidence 0), giống OllamaSlm."""
    names = {name.lower() for name in (context or {}).get("truong", [])}
    for _ in range(2):
        try:
            raw, content, logprobs = await client.chat_json_scored(messages, "json")
            internal = {"cac_y": intents_to_internal(raw["intents"])}
            internal = keep_verbatim(internal, question, names)
            return SlmTinResult.from_raw(internal, confidence=label_confidence(content, logprobs))
        except (OllamaError, ValueError, TypeError, KeyError, AttributeError):
            continue
    return FALLBACK.model_copy(deep=True)


async def run(model: str, split: str, limit: int | None, fmt: str, prompt: str, shots: int = 0) -> None:
    settings = get_settings()
    # records: bản ghi tên nội bộ (nhãn để chấm); english: cùng dòng ở dạng tiếng Anh (đầu vào cho đường tiếng Anh). Cùng thứ tự
    all_records, all_english = read_all(DEFAULT_EXCEL)
    picked = [i for i, r in enumerate(all_records) if r["split"] == split][:limit]
    records = [all_records[i] for i in picked]
    english_inputs = [all_english[i]["input"] for i in picked]
    use_english = prompt in ENGLISH_PROMPTS
    english_prompt, prompt_version, few = "", "", []
    if use_english:
        english_prompt, prompt_version = load_skill(SKILLS_DIR / f"{prompt}.md")
        test_questions = {r["input"]["question"].lower() for r in all_english if r["split"] == "test"}
        few = pick_shots(all_english, shots, test_questions)
        print(f"Đường tiếng Anh: prompt {prompt} {prompt_version}, {len(few)} ví dụ lấy từ tập train")
    async with httpx.AsyncClient(
        base_url=settings.ollama_base_url, timeout=settings.llm_timeout_seconds
    ) as http:
        client = OllamaClient(
            http, model, num_ctx=settings.llm_num_ctx, num_gpu=settings.llm_num_gpu, seed=settings.llm_seed
        )
        slm = None if use_english else OllamaSlm(client, output_format(fmt), prompt=prompt)
        preds, rows, seconds = [], [], []
        fallbacks = 0  # số câu lùi về mơ hồ (model trả sai luật 2 lần)
        for i, r in enumerate(records, start=1):
            start = time.perf_counter()
            context = r["input"].get(
                "dang_noi_ve"
            )  # ngữ cảnh của dòng (nếu có), đưa vào đầu vào như lúc chạy thật
            if use_english:
                messages = english_messages(english_prompt, few, english_inputs[i - 1])
                result = await read_english(client, messages, r["input"]["cau_hoi"], context)
            else:
                result = await slm.read(
                    r["input"]["cau_hoi"], r["input"]["ho_so"], r["input"]["cau_he_thong_dang_hoi"], context
                )
            fallbacks += result.confidence == 0.0
            seconds.append(time.perf_counter() - start)
            pred = {"cac_y": [y.model_dump() for y in result.cac_y]}
            preds.append(pred)
            rows.append({"id": r["id"], "cau_hoi": r["input"]["cau_hoi"], "nhan": r["output"], "du_doan": pred,
                         "confidence": round(result.confidence, 4), "giay": round(seconds[-1], 2),
                         "dang_noi_ve": context, "lui_ve_mo_ho": result.confidence == 0.0, "model": model, "prompt": prompt, "split": split})  # fmt: skip
            print(f"\r{i}/{len(records)}", end="", flush=True)
    print()

    RESULT_DIR.mkdir(parents=True, exist_ok=True)
    tag = (
        f"_shots{len(few)}" if use_english and few else ""
    )  # baseline có ví dụ ghi riêng, không đè bản không ví dụ
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
        "prompt_version": prompt_version if use_english else slm.prompt_version,
        "so_vi_du": len(few),
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
    # Model chưa fine-tune dùng prompt đầy đủ (mặc định), model fine-tune dùng đúng prompt lúc huấn luyện (slm_phan_loai_cac_y_ngan)
    parser.add_argument("--prompt", default=get_settings().slm_prompt)
    # Chỉ đường tiếng Anh (--prompt slm_intents): số ví dụ lấy từ train đưa kèm làm few-shot. Model gốc dùng 8, model đã fine-tune dùng 0
    parser.add_argument("--shots", type=int, default=0)

    args = parser.parse_args()
    asyncio.run(run(args.model, args.split, args.limit, args.format, args.prompt, args.shots))


if __name__ == "__main__":
    main()
