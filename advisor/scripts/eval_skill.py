"""Đo SLM sinh skill hồ sơ trên các dòng kiểm skill: bao nhiêu skill qua check_skill, hỏng vì gì, mất bao lâu.

Chạy đúng đường chạy thật (SkillGenerator: cùng prompt, cùng check_skill, cùng thử lại 1 lần). Dòng kiểm lấy từ dữ liệu
gốc theo đúng cách chia của export_slm_chat (dòng val + cứ 8 dòng train giữ lại 1), không đọc data/slm/finetune/val.jsonl,
nên prompt và đầu vào luôn khớp code kể cả khi file xuất đã cũ.

Cần Ollama đang chạy và đã nạp model. Nạp model đã fine-tune (file .gguf và Modelfile nằm trong models/<tên>/):
  ollama create usas-slm-v2 -f models/usas-slm-v2/Modelfile
Chạy trong thư mục advisor:
  python -m scripts.eval_skill                      # model trong SLM_MODEL (mặc định usas-slm-v2)
  python -m scripts.eval_skill --model usas-slm-v3 --limit 3

Ghi từng dòng (skill sinh ra, skill đáp án, lỗi từng lần, số giây) vào data/slm/ket_qua/<model>_skill_val.jsonl.
"""

import argparse
import asyncio
import json
import statistics
import sys
import time
from collections import Counter
from typing import Any

import httpx

from app.core.config import get_settings
from app.schemas.slm import SkillRequest, check_skill
from app.services.ollama import OllamaClient, OllamaError
from app.services.skill_generator import SkillGenerator
from scripts.build_slm_jsonl import DEFAULT_OUT
from scripts.export_slm_chat import SKILL_HOLDOUT_EVERY, skill_request

RESULT_DIR = DEFAULT_OUT.parent / "ket_qua"


# Dòng kiểm skill: giống hệt cách export_slm_chat.skill_rows chọn tập val (dòng val + dòng train giữ lại)
def eval_records(records: list[dict[str, Any]]) -> list[dict[str, Any]]:
    skills = sorted((r for r in records if r["skill_md"] and r["type"] == "ho_so"), key=lambda r: r["id"])
    train = [r for r in skills if r["split"] == "train"]
    return [r for r in skills if r["split"] == "val"] + train[::SKILL_HOLDOUT_EVERY]


class RecordingSlm:
    """Bọc client SLM: ghi lại từng lần gọi (chữ trả về hoặc lỗi gọi model, số giây) để biết lần nào hỏng vì sao."""

    def __init__(self, slm: Any) -> None:
        self._slm = slm
        self.calls: list[dict[str, Any]] = []

    async def chat_text(self, messages: list[dict[str, str]], temperature: float = 0.0) -> str:
        start = time.perf_counter()
        try:
            text = await self._slm.chat_text(messages, temperature)
        except OllamaError as e:
            self.calls.append({"loi_goi": str(e) or type(e).__name__, "giay": time.perf_counter() - start})
            raise
        self.calls.append({"text": text, "giay": time.perf_counter() - start})
        return text


# Lý do một lần gọi hỏng (None = qua): lỗi gọi model, hoặc lỗi check_skill như lúc chạy thật
def attempt_error(call: dict[str, Any], req: SkillRequest) -> str | None:
    if "loi_goi" in call:
        return f"gọi model: {call['loi_goi']}"
    try:
        check_skill(call["text"].strip(), req)
    except ValueError as e:
        return str(e)
    return None


# Chạy từng dòng qua SkillGenerator, trả kết quả từng dòng. Mỗi dòng một generator mới để không dùng lại cache
async def evaluate(slm: Any, records: list[dict[str, Any]]) -> list[dict[str, Any]]:
    rows = []
    for i, record in enumerate(records, start=1):
        req = SkillRequest(**skill_request(record))
        recorder = RecordingSlm(slm)
        skill = await SkillGenerator(recorder).generate(req)
        loi = [attempt_error(call, req) for call in recorder.calls]
        if skill is None:
            ket_qua = "hong"
        else:
            ket_qua = "lan_1" if len(recorder.calls) == 1 else "sau_thu_lai"
        rows.append({
            "id": record["id"],
            "cau_hoi": req.cau_hoi,
            "ket_qua": ket_qua,
            "loi_tung_lan": loi,
            "giay_tung_lan": [round(call["giay"], 2) for call in recorder.calls],
            "skill": skill,
            "dap_an": record["skill_md"].strip(),
        })  # fmt: skip
        print(f"\r{i}/{len(records)}", end="", flush=True)
    print()
    return rows


# Loại lỗi để đếm: phần trước dấu ":" (vd "skill bỏ mất số của hồ sơ"), bỏ chi tiết riêng từng dòng
def error_kind(error: str) -> str:
    return error.split(":")[0].strip()


def score(rows: list[dict[str, Any]]) -> dict[str, Any]:
    n = len(rows)
    outcome = Counter(row["ket_qua"] for row in rows)
    calls = [s for row in rows for s in row["giay_tung_lan"]]
    per_row = [sum(row["giay_tung_lan"]) for row in rows]
    errors = Counter(error_kind(e) for row in rows for e in row["loi_tung_lan"] if e)
    return {
        "so_dong": n,
        "qua_lan_1": outcome["lan_1"],
        "qua_sau_thu_lai": outcome["sau_thu_lai"],
        "hong_ca_2_lan": outcome["hong"],
        "ti_le_qua": round((outcome["lan_1"] + outcome["sau_thu_lai"]) / n, 3) if n else None,
        "loi_hay_gap": [f"{kind}: {count}" for kind, count in errors.most_common(5)],
        "giay_moi_lan_goi": {
            "trung_binh": round(statistics.mean(calls), 2),
            "p90": round(sorted(calls)[int(0.9 * (len(calls) - 1))], 2),
            "lau_nhat": round(max(calls), 2),
        }
        if calls
        else None,
        "giay_moi_cau_lau_nhat": round(max(per_row), 2) if per_row else None,  # gồm cả lần thử lại
    }


async def run(model: str, limit: int | None) -> None:
    settings = get_settings()
    records = [json.loads(line) for line in DEFAULT_OUT.read_text(encoding="utf-8").splitlines()]
    records = eval_records(records)[:limit]
    async with httpx.AsyncClient(
        base_url=settings.ollama_base_url, timeout=settings.llm_timeout_seconds
    ) as http:
        client = OllamaClient(
            http, model, num_ctx=settings.llm_num_ctx, num_gpu=settings.llm_num_gpu, seed=settings.llm_seed
        )
        rows = await evaluate(client, records)

    RESULT_DIR.mkdir(parents=True, exist_ok=True)
    out = RESULT_DIR / f"{model.replace(':', '_').replace('/', '_')}_skill_val.jsonl"
    out.write_text("".join(json.dumps(row, ensure_ascii=False) + "\n" for row in rows), encoding="utf-8")

    result = score(rows) | {"timeout_moi_lan_goi": settings.llm_timeout_seconds}
    print(f"Model {model}, sinh skill hồ sơ trên {len(rows)} dòng kiểm")
    print(json.dumps(result, ensure_ascii=False, indent=2))
    print(f"Chi tiết từng dòng: {out}")


def main() -> None:
    sys.stdout.reconfigure(encoding="utf-8")
    parser = argparse.ArgumentParser(
        description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter
    )
    parser.add_argument("--model", default=get_settings().slm_model)
    parser.add_argument("--limit", type=int, default=None)

    args = parser.parse_args()
    asyncio.run(run(args.model, args.limit))


if __name__ == "__main__":
    main()
