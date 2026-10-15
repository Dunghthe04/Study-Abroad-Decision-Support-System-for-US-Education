"""Chạy bộ đánh giá lượt ② trên Ollama thật và so với đáp án (bước 4.3).

Chạy:  python -m evals.recommendation.run_eval [--limit 5] [--cases P02,P14] [--skill file.md] [--batch-size 1]
Ra:    evals/recommendation/runs/<thời điểm>.json + bảng tóm tắt in ra màn hình

Chỉ số theo mục 11 file AI_CACH_THUC_HIEN: tỉ lệ nhóm đúng (g2), sai số S, bước sai đầu tiên, thời gian.
"""

import argparse
import asyncio
import json
import time
from collections import Counter
from datetime import datetime
from pathlib import Path

import httpx

from app.core.config import get_settings
from app.services.ollama import OllamaClient
from evals.recommendation.ai_score_schema import ScoreRequest
from evals.recommendation.ai_scorer import Scorer

HERE = Path(__file__).parent
CASES = HERE / "cases.json"
RUNS = HERE / "runs"

# Thứ tự các bước AI tính; sai ở bước nào trước thì ghi bước đó
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
    return got == want


def compare(got, want):
    """So 1 trường: bước sai đầu tiên, sai số S, đúng nhóm hay không."""
    if got is None:
        return {"c": want["c"], "ok_g2": False, "first_wrong": "FAILED", "s_err": None}
    first = next((k for k in STEPS if not same(k, got[k], want[k])), None)
    s_err = abs(got["S"] - want["S"]) if got["S"] is not None and want["S"] is not None else None
    return {
        "c": want["c"],
        "ok_g2": got["g2"] == want["g2"],
        "first_wrong": first,
        "s_err": s_err,
        "got": {k: got[k] for k in STEPS},
        "want": {k: want[k] for k in STEPS},
        "ok_FHQ": all(same(k, got[k], want[k]) for k in ("F", "H", "Q")),
    }


async def run(limit, case_ids, skill, batch_size):
    settings = get_settings()
    data = json.loads(CASES.read_text(encoding="utf-8"))
    cases = data["cases"]
    if case_ids:
        cases = [c for c in cases if c["id"] in case_ids]
    if limit:
        cases = cases[:limit]
    rows, times = [], []
    async with httpx.AsyncClient(base_url=settings.ollama_base_url, timeout=600) as http:
        llm = OllamaClient(
            http,
            settings.llm_model,
            num_ctx=settings.llm_num_ctx,
            num_gpu=settings.llm_num_gpu,
            seed=settings.llm_seed,
            keep_alive=settings.llm_keep_alive,
        )
        scorer = Scorer(
            llm, settings.llm_model, **({"skill_path": Path(skill)} if skill else {}), batch_size=batch_size
        )
        for case in cases:
            start = time.perf_counter()
            res = await scorer.score(ScoreRequest.model_validate(case))
            times.append(time.perf_counter() - start)
            got = {i.c: i.model_dump() for i in res.items}
            for want in case["expected"]:
                rows.append({"case": case["id"], **compare(got.get(want["c"]), want)})
            ok = sum(r["ok_g2"] for r in rows[-len(case["expected"]) :])
            print(f"{case['id']}: {ok}/{len(case['expected'])} nhóm đúng, {times[-1]:.1f} giây", flush=True)

    s_errs = [r["s_err"] for r in rows if r["s_err"] is not None]
    summary = {
        "skill_version": scorer.skill_version,
        "model": settings.llm_model,
        "data_version": data["data_version"],
        "cases": len(cases),
        "pairs": len(rows),
        "g2_accuracy": round(sum(r["ok_g2"] for r in rows) / len(rows), 3),
        "s_mae": round(sum(s_errs) / len(s_errs), 2) if s_errs else None,
        "all_steps_correct": round(sum(r["first_wrong"] is None for r in rows) / len(rows), 3),
        "fhq_correct": round(sum(r.get("ok_FHQ", False) for r in rows) / len(rows), 3),
        "first_wrong_step": dict(Counter(r["first_wrong"] for r in rows if r["first_wrong"])),
        "failed_pairs": sum(r["first_wrong"] == "FAILED" for r in rows),
        "batch_size": batch_size,
        "seconds_per_case": round(sum(times) / len(times), 1),
        "seconds_total": round(sum(times), 1),
    }
    RUNS.mkdir(exist_ok=True)
    out = RUNS / f"{datetime.now():%Y%m%d_%H%M%S}_skill{scorer.skill_version}.json"
    out.write_text(
        json.dumps({"summary": summary, "rows": rows}, ensure_ascii=False, indent=1), encoding="utf-8"
    )
    print(json.dumps(summary, ensure_ascii=False, indent=1))
    print(f"-> {out}")


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--limit", type=int, default=0, help="chỉ chạy N hồ sơ đầu (0 = tất cả)")
    parser.add_argument("--cases", default="", help="chỉ chạy các hồ sơ này, ví dụ P02,P14")
    parser.add_argument(
        "--skill",
        default="",
        help="file skill khác để thử (mặc định evals/recommendation/skills/skill_score.md)",
    )
    parser.add_argument("--batch-size", type=int, default=8)
    a = parser.parse_args()
    asyncio.run(run(a.limit, [c for c in a.cases.split(",") if c], a.skill, a.batch_size))
