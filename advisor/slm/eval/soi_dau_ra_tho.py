"""Gọi lại những tin mà model B đúng còn model A sai ở một nhãn, ghi NGUYÊN VĂN đầu ra của cả hai model và lý do bị loại.

File kết quả đo không lưu đầu ra thô nên không biết model sai khuôn JSON ở đâu; script này lấy đúng phần đó.
Dùng cùng prompt, cùng 8 ví dụ, cùng cài đặt như lúc đo (slm.eval.eval_slm). CẦN GPU: đừng chạy khi đang đo model khác.

Chạy trong thư mục advisor:
  python -m slm.eval.soi_dau_ra_tho --label scenario/goal_seeking ^
      --a "qwen3:4b-instruct=slm/results/ket_qua/qwen3_4b-instruct_shots8_test.jsonl" ^
      --b "gemma3:4b=slm/results/ket_qua/gemma3_4b_shots8_test.jsonl"
Thêm --dry-run để chỉ xem chọn được bao nhiêu tin, không gọi model.
"""

import argparse
import asyncio
import json
import re
from collections import Counter

import httpx

from app.core.config import get_settings
from app.schemas.slm import SlmTinResult
from app.services.ollama import OllamaClient, OllamaError
from app.services.skill_call import SKILLS_DIR, load_skill
from app.services.slm_classifier import keep_verbatim
from slm.eval import slm_metrics as M
from slm.eval.eval_slm import build_messages, pick_shots
from slm.prepare.build_slm_jsonl import DEFAULT_EXCEL, RESULTS_DIR, read_records


def named(text: str) -> tuple[str, str]:
    model, _, path = text.partition("=")
    return model, path


def found(row: dict, label: str) -> bool:
    return not M.is_fallback(row) and label in M.names_of(M.predicted_intents(row), "task")


def short_error(ex: Exception) -> str:
    """Rút gọn lỗi hợp đồng thành một dòng để đếm được (bỏ giá trị cụ thể của từng tin)."""
    errors = getattr(ex, "errors", None)
    if callable(errors):
        text = "; ".join(str(e.get("msg", "")) for e in errors()[:2])
    else:
        text = str(ex)
    return re.sub(r"\s+", " ", text.replace("Value error, ", ""))[:200]


async def ask(client: OllamaClient, messages: list, record: dict) -> dict:
    """Một lần gọi: trả đầu ra thô, và lý do bị loại nếu không qua được hợp đồng."""
    try:
        raw, content, _ = await client.chat_json_scored(messages, "json")
    except OllamaError as ex:
        return {"content": "", "ok": False, "error": f"Ollama: {ex}"[:200]}
    try:
        context = record["input"].get("context")
        names = {n.lower() for n in (context or {}).get("schools", [])}
        result = SlmTinResult.from_raw(keep_verbatim(raw, record["input"]["question"], names))
        return {
            "content": content,
            "ok": True,
            "error": None,
            "intents": [y.model_dump() for y in result.intents],
        }
    except (ValueError, TypeError, KeyError, AttributeError) as ex:
        return {"content": content, "ok": False, "error": short_error(ex)}


async def run(args: argparse.Namespace) -> None:
    (model_a, path_a), (model_b, path_b) = args.a, args.b
    rows_a = {r["id"]: r for r in M.load_rows(path_a)}
    rows_b = {r["id"]: r for r in M.load_rows(path_b)}
    ids = [
        i for i in sorted(rows_a)
        if i in rows_b and args.label in M.names_of(rows_a[i]["nhan"]["intents"], "task")
        and found(rows_b[i], args.label) and not found(rows_a[i], args.label)
    ]  # fmt: skip
    print(f"Nhãn {args.label}: {len(ids)} tin {model_b} đúng mà {model_a} sai (lấy tối đa {args.limit})")
    ids = ids[: args.limit]
    if args.dry_run or not ids:
        for i in ids[:5]:
            print(" -", rows_a[i]["question"])
        return

    settings = get_settings()
    records = read_records(DEFAULT_EXCEL)
    by_id = {r["id"]: r for r in records}
    system, _ = load_skill(SKILLS_DIR / f"{settings.slm_prompt}.md")
    test_questions = {r["input"]["question"].lower() for r in records if r["split"] == "test"}
    shots = pick_shots(records, 8, test_questions)
    url = settings.slm_base_url or settings.ollama_base_url
    headers = {"Authorization": f"Bearer {settings.slm_api_key}"} if settings.slm_api_key else {}

    out: list[dict] = []
    async with httpx.AsyncClient(base_url=url, timeout=settings.llm_timeout_seconds, headers=headers) as http:
        # Chạy hết model A rồi mới tới model B, để GPU không phải đổi model liên tục
        for model in (model_a, model_b):
            client = OllamaClient(
                http, model, num_ctx=settings.llm_num_ctx, num_gpu=settings.llm_num_gpu, seed=settings.llm_seed,
                num_predict=settings.slm_num_predict,
            )  # fmt: skip
            for k, i in enumerate(ids, start=1):
                record = by_id[i]
                answer = await ask(client, build_messages(system, shots, record["input"]), record)
                out.append(
                    {
                        "id": i,
                        "model": model,
                        "question": record["input"]["question"],
                        "gold": record["output"],
                    }
                    | answer
                )
                print(f"\r{model}: {k}/{len(ids)}", end="", flush=True)
            print()

    name = args.label.replace("/", "_")
    folder = RESULTS_DIR / "soi_dau_ra"
    folder.mkdir(parents=True, exist_ok=True)
    (folder / f"{name}.jsonl").write_text(
        "".join(json.dumps(r, ensure_ascii=False) + "\n" for r in out), encoding="utf-8"
    )

    lines = [f"# Đầu ra thô ở nhãn {args.label} ({len(ids)} tin {model_b} đúng mà {model_a} sai)", ""]
    mine = [r for r in out if r["model"] == model_a]
    failures = Counter(r["error"] for r in mine if not r["ok"])
    lines += [f"## {model_a}: {sum(not r['ok'] for r in mine)}/{len(mine)} tin bị loại. Lý do:", ""]
    lines += [f"- {count} tin: {error}" for error, count in failures.most_common()]
    lines += ["", f"## {min(args.examples, len(ids))} tin đầu, hai model đặt cạnh nhau", ""]
    for i in ids[: args.examples]:
        pair = {r["model"]: r for r in out if r["id"] == i}
        lines += [
            f"### {pair[model_a]['question']}",
            "",
            f"Đáp án: `{json.dumps(pair[model_a]['gold'], ensure_ascii=False)}`",
            "",
        ]
        for model in (model_a, model_b):
            r = pair[model]
            lines += [
                f"**{model}** ({'hợp lệ' if r['ok'] else 'BỊ LOẠI: ' + r['error']})",
                "```json",
                r["content"][:1200],
                "```",
                "",
            ]
    (folder / f"{name}.md").write_text("\n".join(lines) + "\n", encoding="utf-8")
    print("\n".join(lines[: 3 + len(failures)]))
    print(f"\nĐã ghi: {folder / (name + '.md')}")


def main() -> None:
    parser = argparse.ArgumentParser(
        description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter
    )
    parser.add_argument(
        "--a", required=True, type=named, help='"model Ollama=file kết quả" của model được chọn'
    )
    parser.add_argument(
        "--b", required=True, type=named, help='"model Ollama=file kết quả" của model so sánh'
    )
    parser.add_argument("--label", required=True, help="vd scenario/goal_seeking")
    parser.add_argument("--limit", type=int, default=30)
    parser.add_argument("--examples", type=int, default=5)
    parser.add_argument("--dry-run", action="store_true")
    asyncio.run(run(parser.parse_args()))


if __name__ == "__main__":
    main()
