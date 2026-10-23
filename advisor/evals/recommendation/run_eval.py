"""Chạy AI chấm trường trên cases.json và so với đáp án (reference.py).

Chạy thử nhỏ (2 hồ sơ x 8 trường = 2 lần gọi):
    python -m evals.recommendation.run_eval --provider gemini --model gemini-3.8-flash --thinking low --limit 2 --schools 8 --interval 13
Ra:    evals/recommendation/runs/<thời điểm>.json + bảng tóm tắt in ra màn hình
"""

import argparse
import asyncio
import json
import os
import time
from collections import Counter
from datetime import datetime
from pathlib import Path

import httpx

from app.schemas.score import ScoreRequest
from app.services.scorer import Scorer

HERE = Path(__file__).parent
CASES = HERE / "cases.json"
RUNS = HERE / "runs"

# Thứ tự các bước AI tính: sai ở bước nào TRƯỚC thì ghi bước đó (các bước sau sai theo)
STEPS = ["w", "pg", "ps", "A", "adj", "S", "g", "r", "g2"]
TOLERANCE = {
    "w": 0.02,
    "pg": 1.0,
    "ps": 1.0,
    "A": 1.0,
    "adj": 0.01,
    "S": 1.0,
    "r": 0.02,
    "F": 0.01,
    "H": 0.01,
    "Q": 0.01,
}


def same(key, got, want):
    if got is None or want is None:
        return got is None and want is None
    if key == "w":
        return all(abs(a - b) <= TOLERANCE["w"] for a, b in zip(got, want, strict=True))
    if key in TOLERANCE:
        return abs(got - want) <= TOLERANCE[key]
    return got == want  # nhóm, nhãn: phải đúng tuyệt đối


def compare(got, want):
    """So 1 trường: bước sai đầu tiên, sai số điểm S, nhóm cuối đúng hay không."""
    if got is None:
        return {
            "c": want["c"],
            "ok_g2": False,
            "first_wrong": "FAILED",
            "s_err": None,
            "got": None,
            "want": None,
        }
    first = next((k for k in STEPS if not same(k, got[k], want[k])), None)
    s_err = abs(got["S"] - want["S"]) if got["S"] is not None and want["S"] is not None else None
    return {
        "c": want["c"],
        "ok_g2": got["g2"] == want["g2"],
        "first_wrong": first,
        "s_err": s_err,
        "got": got[first] if first else None,  # giá trị AI trả ở bước sai
        "want": want[first] if first else None,  # đáp án công thức ở bước đó
    }


async def run(llm, model, batch_size, cases, limit, schools=None):
    scorer = Scorer(llm, model, batch_size=batch_size)
    rows, started = [], time.perf_counter()
    for case in cases[:limit]:
        keep = case["schools"][:schools]
        codes = {x["c"] for x in keep}
        want = [w for w in case["want"] if w["c"] in codes]
        req = ScoreRequest.model_validate({"student": case["student"], "schools": keep})
        print(f"[{case['id']}] đang chấm {len(keep)} trường, chờ khoảng 1-2 phút...", flush=True)
        res = await scorer.score(req)
        got = {i.c: i.model_dump() for i in res.items}
        rows += [{"case": case["id"], **compare(got.get(w["c"]), w)} for w in want]
        print(f"[{case['id']}] xong sau {time.perf_counter() - started:.0f}s", flush=True)
    return rows, time.perf_counter() - started, scorer.skill_version


def token_totals(usage_log):
    """Cộng token từ nguyên văn `usage` Google trả về. Token suy nghĩ nằm riêng, nhưng tính tiền như token ra."""
    keys = {"input": "total_input_tokens", "output": "total_output_tokens", "thought": "total_thought_tokens"}
    totals = {name: sum((x.get("usage") or {}).get(key, 0) for x in usage_log) for name, key in keys.items()}
    totals["calls"] = len(usage_log)
    return totals


def latency(usage_log):
    secs = sorted(x["seconds"] for x in usage_log if "seconds" in x)
    if not secs:
        return None
    return {
        "calls": len(secs),
        "min": round(secs[0], 1),
        "median": round(secs[len(secs) // 2], 1),
        "max": round(secs[-1], 1),
    }


def summarize(rows):
    n = len(rows)
    errs = [r["s_err"] for r in rows if r["s_err"] is not None]
    return {
        "pairs": n,
        "g2_correct_pct": round(100 * sum(r["ok_g2"] for r in rows) / n, 1),
        "all_steps_correct_pct": round(100 * sum(r["first_wrong"] is None for r in rows) / n, 1),
        "first_wrong_step": dict(Counter(r["first_wrong"] for r in rows if r["first_wrong"])),
        "mean_s_error": round(sum(errs) / len(errs), 2) if errs else None,
    }


class LoggingLLM:
    """Bọc client AI: in ra lý do mỗi lần gọi lỗi (SkillCall nuốt lỗi nên không thấy)."""

    def __init__(self, inner):
        self._inner = inner
        self.walls = []  # thời gian thật mỗi lần gọi (gồm cả chờ giới hạn tốc độ và thử lại)

    def __getattr__(self, name):
        return getattr(self._inner, name)

    async def chat_json(self, messages, schema, temperature=0.0):
        started = time.perf_counter()
        try:
            raw = await self._inner.chat_json(messages, schema, temperature)
        except Exception as ex:  # noqa: BLE001
            print(
                f"  LỖI GỌI AI sau {time.perf_counter() - started:.0f}s: {type(ex).__name__}: {str(ex)[:300]}",
                flush=True,
            )
            raise
        self.walls.append(time.perf_counter() - started)
        n = len(raw.get("items", [])) if isinstance(raw, dict) else "?"
        print(f"  AI trả về {n} phần tử sau {time.perf_counter() - started:.0f}s", flush=True)
        return raw


def load_dotenv():
    """Chưa đặt GEMINI_API_KEY trong môi trường thì đọc từ advisor/.env (file này không commit)."""
    env = HERE.parent.parent / ".env"
    if "GEMINI_API_KEY" in os.environ or not env.exists():
        return
    for line in env.read_text(encoding="utf-8").splitlines():
        key, sep, value = line.partition("=")
        if sep and key.strip() == "GEMINI_API_KEY":
            os.environ["GEMINI_API_KEY"] = value.strip().strip('"').strip("'")


def make_llm(args, http):
    if args.provider == "gemini":
        from app.services.gemini import GeminiClient

        load_dotenv()

        return LoggingLLM(
            GeminiClient(
                http,
                args.model,
                os.environ["GEMINI_API_KEY"],
                thinking_level=args.thinking,
                min_interval=args.interval,
            )
        )
    from app.services.ollama import OllamaClient

    return LoggingLLM(OllamaClient(http, args.model))


async def main():
    p = argparse.ArgumentParser()
    p.add_argument("--provider", choices=["gemini", "ollama"], default="gemini")
    p.add_argument("--model", default="gemini-3.8-flash")
    p.add_argument("--thinking", default=None, help="low | medium | high (nếu model hỗ trợ)")
    p.add_argument("--batch-size", type=int, default=8)
    p.add_argument("--limit", type=int, default=None, help="chỉ chạy N học sinh đầu (chạy thử cho rẻ)")
    p.add_argument(
        "--schools", type=int, default=None, help="chỉ lấy N trường đầu mỗi hồ sơ (8 = 1 lần gọi mỗi hồ sơ)"
    )
    p.add_argument("--interval", type=float, default=0.0, help="giây nghỉ giữa 2 lần gọi")
    args = p.parse_args()

    base = (
        "https://generativelanguage.googleapis.com" if args.provider == "gemini" else "http://localhost:11434"
    )
    cases = json.loads(CASES.read_text(encoding="utf-8"))
    async with httpx.AsyncClient(base_url=base, timeout=180) as http:
        llm = make_llm(args, http)
        rows, seconds, skill_version = await run(
            llm, args.model, args.batch_size, cases, args.limit, args.schools
        )

    out = {
        "when": datetime.now().isoformat(timespec="seconds"),
        "provider": args.provider,
        "model": args.model,
        "thinking": args.thinking,
        "batch_size": args.batch_size,
        "skill_version": skill_version,
        "seconds": round(seconds, 1),
        "summary": summarize(rows),
        "rows": rows,
        "latency_seconds": latency(getattr(llm, "usage_log", [])),
        "wall_seconds_per_call": [round(x, 1) for x in getattr(llm, "walls", [])],
        "tokens": token_totals(getattr(llm, "usage_log", [])),
        "usage_log": getattr(llm, "usage_log", []),
    }  # nguyên số token Google trả về, chưa suy diễn
    RUNS.mkdir(exist_ok=True)
    stamp = datetime.now().strftime("%Y%m%d_%H%M%S")
    path = RUNS / f"{stamp}_{args.provider}_{args.model.replace(':', '-')}.json"
    path.write_text(json.dumps(out, ensure_ascii=False, indent=1), encoding="utf-8")
    print(json.dumps(out["summary"], ensure_ascii=False, indent=1))
    print("token:", out["tokens"])
    print("thời gian thật mỗi lần gọi (giây, gồm chờ và thử lại):", out["wall_seconds_per_call"])
    print("thời gian máy chủ báo (không gồm chờ):", out["latency_seconds"], f"| tổng {seconds:.0f}s ->", path)
    for r in rows:
        if r["first_wrong"]:
            print(f"  sai {r['case']}/{r['c']}: bước {r['first_wrong']} AI={r['got']} đáp án={r['want']}")


if __name__ == "__main__":
    asyncio.run(main())
