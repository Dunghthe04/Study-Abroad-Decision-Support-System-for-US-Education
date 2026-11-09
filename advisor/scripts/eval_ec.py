"""Đo SLM đọc hoạt động ngoại khóa trên bộ mẫu gán nhãn: đúng vai trò, đúng phạm vi, có nâng phạm vi trái luật không, có ổn định không.

Mỗi hoạt động gọi một lượt, giống lúc học sinh bấm "Thêm vào hồ sơ" (skill_ec.md, temperature 0, JSON theo khuôn cố định).
Cần Ollama đang chạy và đã pull model. Chạy trong thư mục advisor:
  python -m scripts.eval_ec                        # model SLM mặc định
  python -m scripts.eval_ec --model qwen3:4b --limit 10
  python -m scripts.eval_ec --repeat 1             # bỏ kiểm tra ổn định (nhanh hơn)

Ghi từng dự đoán vào data/ec/ket_qua/<model>.jsonl để xem lại mẫu sai. Bộ mẫu: python -m scripts.build_ec_dataset
"""

import argparse
import asyncio
import json
import sys
import time
from collections import defaultdict
from pathlib import Path
from typing import Any

import httpx

from app.core.config import get_settings
from app.schemas.ec import EcActivityRead, EcRead, ec_schema
from app.services.ec_reader import SKILL_PATH
from app.services.ollama import OllamaClient
from app.services.skill_call import SkillCall
from scripts.build_ec_dataset import JSONL

RESULT_DIR = JSONL.parent / "ket_qua"

Prediction = tuple[float, int] | None  # (vai trò, phạm vi); None khi model trả sai khuôn cả hai lần


def load_records(path: Path = JSONL) -> list[dict[str, Any]]:
    return [json.loads(line) for line in path.read_text(encoding="utf-8").splitlines() if line.strip()]


# Đầu vào của một hoạt động: chữ, vai trò và phạm vi học sinh đã chọn trong form (bắt buộc chọn), số tháng
def model_input(record: dict[str, Any]) -> dict[str, Any]:
    return {
        "act": [
            {
                "text": record["text"],
                "role": record["role_chosen"],
                "scope": record["scope_chosen"],
                "months": record["months"],
            }
        ]
    }


# Kiểm tra đầu ra: đúng một phần tử, đúng khuôn
def check_one(raw: dict[str, Any]) -> EcActivityRead:
    read = EcRead.model_validate(raw)
    if len(read.act) != 1:
        raise ValueError(f"Gửi 1 hoạt động nhưng nhận {len(read.act)}")
    return read.act[0]


# Gọi model cho từng mẫu, mỗi mẫu `repeat` lần. Trả danh sách các lần dự đoán của từng mẫu
async def predict(call: SkillCall, records: list[dict[str, Any]], repeat: int) -> list[list[Prediction]]:
    results: list[list[Prediction]] = []
    for i, record in enumerate(records, start=1):
        runs: list[Prediction] = []
        for _ in range(repeat):
            read = await call.run(model_input(record), ec_schema(1), check_one)
            runs.append(None if read is None else (read.role, read.scope))
        results.append(runs)
        print(f"\r{i}/{len(records)}", end="", flush=True)
    print()
    return results


# So dự đoán (lần chạy đầu) với nhãn, trả các con số tổng hợp
def score(records: list[dict[str, Any]], runs: list[list[Prediction]]) -> dict[str, Any]:
    n = len(records)
    first = [r[0] for r in runs]
    role_ok = [p is not None and p[0] == r["expected_role"] for r, p in zip(records, first, strict=True)]
    scope_ok = [p is not None and p[1] == r["expected_scope"] for r, p in zip(records, first, strict=True)]
    both_ok = [a and b for a, b in zip(role_ok, scope_ok, strict=True)]
    raised = [p is not None and p[1] > r["scope_chosen"] for r, p in zip(records, first, strict=True)]
    role_raised = [p is not None and p[0] > r["role_chosen"] for r, p in zip(records, first, strict=True)]
    stable = [len(set(r)) == 1 for r in runs]

    by_group: dict[str, list[bool]] = defaultdict(list)
    for r, ok in zip(records, both_ok, strict=True):
        by_group[r["group"]].append(ok)
    hard = [ok for r, ok in zip(records, both_ok, strict=True) if r["hard"]]
    clear = [ok for r, ok in zip(records, both_ok, strict=True) if not r["hard"]]
    lowered = [ok for r, ok in zip(records, scope_ok, strict=True) if r["expected_scope"] < r["scope_chosen"]]
    role_lowered = [
        ok for r, ok in zip(records, role_ok, strict=True) if r["expected_role"] < r["role_chosen"]
    ]
    unchanged = [
        ok
        for r, ok in zip(records, both_ok, strict=True)
        if r["expected_role"] == r["role_chosen"] and r["expected_scope"] == r["scope_chosen"]
    ]

    return {
        "mau": n,
        "tra_sai_khuon": sum(p is None for p in first),
        "dung_vai_tro": round(sum(role_ok) / n, 3),
        "dung_pham_vi": round(sum(scope_ok) / n, 3),
        "dung_ca_hai": round(sum(both_ok) / n, 3),
        "dung_ca_hai_mau_ro": round(sum(clear) / len(clear), 3) if clear else None,
        "dung_ca_hai_mau_kho": round(sum(hard) / len(hard), 3) if hard else None,
        "ha_pham_vi_dung": round(sum(lowered) / len(lowered), 3) if lowered else None,
        "ha_vai_tro_dung": round(sum(role_lowered) / len(role_lowered), 3) if role_lowered else None,
        "giu_nguyen_dung": round(sum(unchanged) / len(unchanged), 3) if unchanged else None,
        "nang_pham_vi_trai_luat": sum(raised),
        "nang_vai_tro_trai_luat": sum(role_raised),
        "on_dinh_giua_cac_lan": round(sum(stable) / n, 3),
        "theo_nhom": {g: f"{sum(v)}/{len(v)}" for g, v in sorted(by_group.items())},
    }


async def run(model: str, limit: int | None, repeat: int) -> None:
    settings = get_settings()
    records = load_records()[:limit]
    async with httpx.AsyncClient(
        base_url=settings.ollama_base_url, timeout=settings.llm_timeout_seconds
    ) as http:
        client = OllamaClient(
            http, model, num_ctx=settings.llm_num_ctx, num_gpu=settings.llm_num_gpu, seed=settings.llm_seed
        )
        call = SkillCall(client, model, SKILL_PATH)
        start = time.perf_counter()
        runs = await predict(call, records, repeat)
        seconds = time.perf_counter() - start

    result = score(records, runs)
    result |= {
        "giay_moi_lan_goi": round(seconds / (len(records) * repeat), 2),
        "skill_version": call.skill_version,
        "so_lan_lap": repeat,
    }

    RESULT_DIR.mkdir(parents=True, exist_ok=True)
    out = RESULT_DIR / f"{model.replace(':', '_').replace('/', '_')}.jsonl"
    rows = [
        {
            "id": r["id"],
            "text": r["text"],
            "role_chosen": r["role_chosen"],
            "scope_chosen": r["scope_chosen"],
            "dap_an": [r["expected_role"], r["expected_scope"]],
            "du_doan": [list(p) if p else None for p in runs_of_one],
            "dung": runs_of_one[0] is not None
            and runs_of_one[0][0] == r["expected_role"]
            and runs_of_one[0][1] == r["expected_scope"],
        }
        for r, runs_of_one in zip(records, runs, strict=True)
    ]
    out.write_text("".join(json.dumps(row, ensure_ascii=False) + "\n" for row in rows), encoding="utf-8")

    print(f"Model {model}, {len(records)} mẫu")
    print(json.dumps(result, ensure_ascii=False, indent=2))
    wrong = [row for row in rows if not row["dung"]]
    if wrong:
        print(f"\n{len(wrong)} mẫu sai (đáp án [vai trò, phạm vi] so với dự đoán lần đầu):")
        for row in wrong:
            print(f"  {row['id']} đáp án {row['dap_an']} dự đoán {row['du_doan'][0]}: {row['text'][:70]}")
    print(f"\nChi tiết từng mẫu: {out}")


def main() -> None:
    sys.stdout.reconfigure(encoding="utf-8")
    parser = argparse.ArgumentParser(
        description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter
    )
    parser.add_argument("--model", default=get_settings().slm_model)
    parser.add_argument("--limit", type=int, default=None)
    parser.add_argument("--repeat", type=int, default=2, help="số lần gọi mỗi mẫu để kiểm tra độ ổn định")
    args = parser.parse_args()
    asyncio.run(run(args.model, args.limit, args.repeat))


if __name__ == "__main__":
    main()
