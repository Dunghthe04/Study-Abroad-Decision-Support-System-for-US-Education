"""B3. Đo skill hồ sơ dựng bằng khuôn (build_skill) so với skill SLM, qua câu trả lời Claude viết từ mỗi skill.

Cùng dòng kiểm với eval_skill (dòng val + cứ 8 dòng train giữ lại 1), cùng câu hỏi, cùng hồ sơ, cùng việc học sinh hỏi.
Mỗi dòng Claude (AdviceWriter, đúng đường chạy thật) viết 2 lần: một lần từ skill khuôn, một lần từ skill nguồn so sánh:
  --slm slm     skill do SLM sinh lúc chạy (SkillGenerator, cần Ollama đã nạp model)
  --slm dap_an  skill đáp án trong dữ liệu (skill_md): mức tốt nhất SLM có thể sinh, không cần Ollama
Đếm cho từng nguồn: qua lần 1 / qua sau viết lại / thiếu phần / hỏng, loại lỗi, số ngoài dữ liệu, độ dài, giây, token.

Dữ liệu chỉ có hồ sơ dạng chữ ("8.2 thang 10"), nên profile_of đổi ngược về giá trị như form để khuôn chọn lộ trình.
Hoạt động, giải thưởng giữ dạng chữ: lộ trình không dùng tới, phần đánh giá đọc mức hoạt động trong tinh_san.

Cần ANTHROPIC_API_KEY trong .env (tốn token Claude: mỗi dòng 2 đến 4 lần gọi). Chạy trong thư mục advisor:
  python -m scripts.eval_skill_khuon --slm dap_an --limit 5
  python -m scripts.eval_skill_khuon --slm slm --model usas-slm-v3 --limit 15
  python -m scripts.eval_skill_khuon --slm slm --model usas-slm-v3 --tu-dong 16   (chạy tiếp, giữ 15 dòng đã có)

In ra số đếm, không in hồ sơ hay câu trả lời. Chi tiết từng dòng (không có chữ, trừ khi thêm --ghi-chu) ghi ngay
khi xong dòng đó vào data/slm/ket_qua/skill_khuon_vs_<nguồn>.jsonl, nên dừng giữa chừng không mất phần đã chạy.
"""

import argparse
import asyncio
import json
import re
import statistics
import sys
import time
from collections import Counter
from datetime import date
from pathlib import Path
from typing import Any

import httpx

from app.core.config import get_settings
from app.schemas.chat import ProfileField
from app.schemas.slm import SkillRequest
from app.services import usage_meter
from app.services.advice_writer import AdviceWriter, missing_task
from app.services.claude import ClaudeClient
from app.services.ollama import OllamaClient
from app.services.skill_generator import SkillGenerator
from app.services.skill_input import CODE_LABELS, NONE_TEXT, UNKNOWN_TEXT, budget_text, lower_first
from app.services.skill_template import build_skill, tasks_of
from app.services.skills.buttons import BUDGET_LABELS
from app.services.skills.skill_danh_gia import NUMBER, allowed_numbers, read_facts, readings
from app.services.skills.skill_lo_trinh import choose_route, timeline_numbers
from app.services.skills.validators import BUDGET_RANGES, CERT_NAMES
from scripts.build_slm_jsonl import DEFAULT_OUT
from scripts.eval_skill import eval_records
from scripts.export_slm_chat import skill_request

RESULT_DIR = DEFAULT_OUT.parent / "ket_qua"
DEFAULT_TODAY = date(2026, 11, 10)
OUTCOMES = ("lan_1", "sau_viet_lai", "thieu_phan", "hong", "khong_co_skill")

# Đổi ngược chữ của skill_input.value_text về giá trị như form
GPA = re.compile(r"(\d+(?:\.\d+)?) thang (\d+)")
SCORE = re.compile(r"(.+) (\d+(?:\.\d+)?)")
PLAN = re.compile(
    r"dự định thi(?: (?P<cert>.+?))?(?: tháng (?P<month>\d{2})/(?P<year>\d{4}))?(?:, mục tiêu (?P<target>[\d.]+))?"
)
CERT_BY_NAME = {name.lower(): code for code, name in CERT_NAMES.items()}
BUDGET_BY_TEXT = {budget_text(tier): tier for tier in {*BUDGET_RANGES, *BUDGET_LABELS}}
CODE_BY_LABEL = {
    key: {lower_first(label): code for code, label in labels.items()} for key, labels in CODE_LABELS.items()
}
VISA = {"đã từng bị từ chối visa": True, "chưa từng bị từ chối visa": False}
BUDGET_LEVEL = re.compile(r"mức (thiếu|sát|đủ)$")
TIER_OF_LEVEL = {"thiếu": "tier_under_20k", "sát": "tier_35k_50k", "đủ": "tier_50k_70k"}  # short / tight / ok
TODAY_IN_TIMELINE = re.compile(r"hôm nay (\d{2})/(\d{4})")


# "1450" → 1450, "6.5" → 6.5
def number_of(text: str) -> int | float:
    return float(text) if "." in text else int(text)


def cert_of(name: str) -> str:
    return CERT_BY_NAME.get(name.lower(), name.lower())


def plan_of(text: str) -> dict[str, Any] | str:
    if text == "chưa có kế hoạch thi":
        return {"planned": False}
    match = PLAN.fullmatch(text)
    if not match:
        return text
    plan: dict[str, Any] = {"planned": True}
    if match["cert"]:
        plan["test_type"] = cert_of(match["cert"])
    if match["year"]:
        plan["date"] = f"{match['year']}-{match['month']}"
    if match["target"]:
        plan["target_score"] = number_of(match["target"])
    return plan


# Một ô chữ → giá trị như form. Không đọc được thì giữ nguyên chữ (read_facts bỏ qua, không vỡ)
def value_of_text(key: str, text: str) -> Any:
    if key in CODE_BY_LABEL:
        return CODE_BY_LABEL[key].get(text, text)
    if key == "gpa" and (match := GPA.fullmatch(text)):
        return {"value": float(match[1]), "scale": int(match[2])}
    if key == "english" and (match := SCORE.fullmatch(text)):
        return {"type": cert_of(match[1]), "value": float(match[2])}
    if key in ("sat", "act") and text.isdigit():
        return int(text)
    if key == "budget":
        return BUDGET_BY_TEXT.get(text, text)
    if key in ("english_plan", "standardized_plan"):
        return plan_of(text)
    if key == "visa_refused":
        return VISA.get(text, text)
    if key == "preferred_states":
        return text.split(", ")
    return text


# ho_so dạng chữ của dữ liệu → hồ sơ như state.profile. "không có" / "chưa rõ" giữ đúng trạng thái
def profile_of(ho_so: dict[str, str]) -> dict[str, ProfileField]:
    profile = {}
    for key, text in ho_so.items():
        if text == NONE_TEXT:
            profile[key] = ProfileField(status="none")
        elif text == UNKNOWN_TEXT:
            profile[key] = ProfileField(status="unknown")
        else:
            profile[key] = ProfileField(value=value_of_text(key, text), status="confirmed")
    return profile


# Dữ liệu cũ ghi ngân sách theo khoảng tự do ("khoảng 36.000 đến 44.000 USD"), không phải 4 mức của form, nên
# read_facts không biết mức tài chính và khuôn không chọn được lộ trình cao đẳng vì thiếu tiền. Khi đó lấy mức
# từ dòng tính sẵn budget_level ("..., mức thiếu") của chính dòng đó, chỉ để chọn lộ trình; bảng hồ sơ vẫn ghi chữ gốc
def with_budget_tier(profile: dict[str, ProfileField], tinh_san: dict[str, str]) -> dict[str, ProfileField]:
    budget = profile.get("budget")
    if budget is None or budget.value in BUDGET_RANGES:
        return profile
    match = BUDGET_LEVEL.search(tinh_san.get("budget_level") or "")
    if not match:
        return profile
    return profile | {"budget": ProfileField(value=TIER_OF_LEVEL[match[1]], status="confirmed")}


# Việc học sinh hỏi: cac_nhiem_vu của các ý ho_so trong đáp án tách ý, giữ thứ tự, bỏ trùng. Không có thì None
def tasks_of_record(record: dict[str, Any]) -> list[str] | None:
    tasks = [
        task
        for y in (record.get("output") or {}).get("cac_y") or []
        if y.get("type") == "ho_so"
        for task in (y.get("thuc_the") or {}).get("cac_nhiem_vu") or []
    ]
    return list(dict.fromkeys(tasks)) or None


# Ngày tính lộ trình: tháng "hôm nay" ghi trong dòng timeline của dữ liệu, để khớp các mốc đã tính sẵn
def today_of(record: dict[str, Any], default: date) -> date:
    match = TODAY_IN_TIMELINE.search((record.get("tinh_san") or {}).get("timeline") or "")
    return date(int(match[2]), int(match[1]), 1) if match else default


# Số được phép trong câu trả lời: số trong câu hỏi, hồ sơ, dòng tính sẵn, số của hồ sơ và các mốc lộ trình
def allowed_for(req: SkillRequest, profile: dict[str, ProfileField], today: date) -> set[float]:
    route, _, intake = choose_route(profile, today)
    allowed = allowed_numbers(read_facts(profile, today)) | timeline_numbers(intake, route)
    for text in (req.cau_hoi, *req.ho_so.values(), *req.tinh_san.values()):
        for token in NUMBER.findall(text):
            allowed |= readings(token)
    return allowed


def numbers_outside(text: str, allowed: set[float]) -> list[str]:
    return [
        token
        for token in NUMBER.findall(text)
        if not any(abs(r - a) < 0.005 for r in readings(token) for a in allowed)
    ]


# Kết quả một lần viết: hỏng (None), thiếu phần học sinh hỏi, qua ngay lần 1 hay sau khi viết lại
def outcome(text: str | None, attempts: int, tasks: list[str]) -> str:
    if text is None:
        return "hong"
    if missing_task(text, tasks):
        return "thieu_phan"
    return "lan_1" if attempts == 1 else "sau_viet_lai"


async def write_one(
    writer: AdviceWriter,
    skill: str | None,
    skill_seconds: float,
    req: SkillRequest,
    tasks: list[str] | None,
    allowed: set[float],
    keep_text: bool,
) -> dict[str, Any]:
    result: dict[str, Any] = {"giay_skill": round(skill_seconds, 2)}
    if skill is None:
        return result | {"ket_qua": "khong_co_skill"}
    calls = usage_meter.start()
    start = time.perf_counter()
    text = await writer.write(skill, req, [], tasks)
    result |= {
        "ket_qua": outcome(text, writer.last_attempts, tasks_of(tasks)),
        "so_lan": writer.last_attempts,
        "loi": list(writer.last_errors),
        "giay_viet": round(time.perf_counter() - start, 2),
        "token_vao": sum(c.input_tokens + c.cached_tokens for c in calls),
        "token_ra": sum(c.output_tokens for c in calls),
        "do_dai": len(text or ""),
        "so_ngoai": numbers_outside(text or "", allowed),
    }
    if keep_text:
        result |= {"skill": skill, "tra_loi": text}
    return result


# Chạy từng dòng: dựng skill khuôn và skill nguồn so sánh, cho Claude viết từ mỗi skill.
# slm_generate: hàm async (SkillRequest) → skill hoặc None; None nghĩa là dùng skill đáp án của dữ liệu.
# Phía khuôn chỉ gửi cho Claude việc của khuôn (danh_gia, lo_trinh): chọn trường, so sánh, giải thích trường là
# phần của Dũng, gửi kèm thì Claude tự viết thêm phần đó. Phía nguồn so sánh gửi nguyên việc như luồng hiện tại.
# on_row: gọi ngay khi xong mỗi dòng (ghi file), để dừng giữa chừng không mất các dòng đã chạy.
# first: số thứ tự của dòng đầu trong records (khi chạy tiếp từ dòng N), chỉ để in tiến độ
async def evaluate(
    writer: AdviceWriter,
    records: list[dict[str, Any]],
    slm_generate: Any = None,
    default_today: date = DEFAULT_TODAY,
    keep_text: bool = False,
    on_row: Any = None,
    first: int = 1,
) -> list[dict[str, Any]]:
    rows = []
    last = first + len(records) - 1
    for i, record in enumerate(records, start=first):
        req = SkillRequest(**skill_request(record))
        profile = profile_of(req.ho_so)
        tasks = tasks_of_record(record)
        today = today_of(record, default_today)
        allowed = allowed_for(req, profile, today)  # hồ sơ gốc: không thêm sàn/trần của mức ngân sách suy ra

        start = time.perf_counter()
        khuon = build_skill(req, tasks, with_budget_tier(profile, req.tinh_san), today)
        khuon_seconds = time.perf_counter() - start
        start = time.perf_counter()
        other = await slm_generate(req) if slm_generate else (record["skill_md"] or "").strip() or None
        other_seconds = time.perf_counter() - start

        row = {
            "id": record["id"],
            "nhiem_vu": tasks,
            "khuon": await write_one(writer, khuon, khuon_seconds, req, tasks_of(tasks), allowed, keep_text),
            "slm": await write_one(writer, other, other_seconds, req, tasks, allowed, keep_text),
        }  # fmt: skip
        rows.append(row)
        if on_row:
            on_row(row)
        print(f"\r{i}/{last} (dòng vừa xong: skill nguồn {other_seconds:.0f} giây)   ", end="", flush=True)
    print()
    return rows


# Đọc lại file chi tiết; dòng trùng id (chạy tiếp từ dòng đã có) thì lấy lần chạy sau
def read_rows(path: Path) -> list[dict[str, Any]]:
    if not path.exists():
        return []
    rows = {}
    for line in path.read_text(encoding="utf-8").splitlines():
        if line.strip():
            row = json.loads(line)
            rows[row["id"]] = row
    return list(rows.values())


def spread(values: list[float]) -> dict[str, float] | None:
    if not values:
        return None
    return {
        "trung_binh": round(statistics.mean(values), 2),
        "p90": round(sorted(values)[int(0.9 * (len(values) - 1))], 2),
        "lon_nhat": round(max(values), 2),
    }


def score(rows: list[dict[str, Any]], source: str) -> dict[str, Any]:
    results = [row[source] for row in rows]
    written = [r for r in results if r["ket_qua"] != "khong_co_skill"]
    counts = Counter(r["ket_qua"] for r in results)
    outside = Counter(token for r in written for token in r["so_ngoai"])
    lengths = [r["do_dai"] for r in written if r["ket_qua"] != "hong"]
    return {
        "so_dong": len(results),
        **{name: counts[name] for name in OUTCOMES},
        "ti_le_qua": round((counts["lan_1"] + counts["sau_viet_lai"]) / len(results), 3) if results else None,
        "loi_hay_gap": [
            f"{kind}: {n}" for kind, n in Counter(e for r in written for e in r["loi"]).most_common()
        ],
        "dong_co_so_ngoai_du_lieu": sum(1 for r in written if r["so_ngoai"]),
        "so_ngoai_hay_gap": [f"{token}: {n}" for token, n in outside.most_common(5)],
        "do_dai": {
            "trung_binh": round(statistics.mean(lengths)),
            "nho_nhat": min(lengths),
            "lon_nhat": max(lengths),
        }
        if lengths
        else None,
        "giay_skill": spread([r["giay_skill"] for r in results]),
        "giay_viet": spread([r["giay_viet"] for r in written]),
        "token_vao_trung_binh": round(statistics.mean(r["token_vao"] for r in written)) if written else None,
        "token_ra_trung_binh": round(statistics.mean(r["token_ra"] for r in written)) if written else None,
    }


async def run(
    source: str, model: str, limit: int | None, default_today: date, keep_text: bool, first: int = 1
) -> int:
    settings = get_settings()
    if not settings.anthropic_api_key:
        print("Thiếu ANTHROPIC_API_KEY trong .env")
        return 1
    records = [json.loads(line) for line in DEFAULT_OUT.read_text(encoding="utf-8").splitlines()]
    records = eval_records(records)[:limit][first - 1 :]

    name = source if source == "dap_an" else model.replace(":", "_").replace("/", "_")
    RESULT_DIR.mkdir(parents=True, exist_ok=True)
    out = RESULT_DIR / f"skill_khuon_vs_{name}.jsonl"
    if first == 1:
        out.write_text("", encoding="utf-8")  # chạy từ đầu: xóa kết quả cũ; chạy tiếp thì ghi nối

    def save(row: dict[str, Any]) -> None:
        with out.open("a", encoding="utf-8") as f:
            f.write(json.dumps(row, ensure_ascii=False) + "\n")

    async with (
        httpx.AsyncClient(
            base_url=settings.anthropic_base_url, timeout=settings.anthropic_timeout_seconds
        ) as claude_http,
        httpx.AsyncClient(
            base_url=settings.ollama_base_url, timeout=settings.llm_timeout_seconds
        ) as slm_http,
    ):
        claude = ClaudeClient(
            claude_http,
            settings.anthropic_model,
            settings.anthropic_api_key,
            max_retries=settings.anthropic_max_retries,
        )
        slm_generate = None
        if source == "slm":
            slm = OllamaClient(
                slm_http,
                model,
                num_ctx=settings.llm_num_ctx,
                num_gpu=settings.llm_num_gpu,
                seed=settings.llm_seed,
            )

            # Mỗi dòng một generator mới để không dùng lại cache, như eval_skill
            async def slm_generate(req: SkillRequest) -> str | None:
                return await SkillGenerator(slm).generate(req)

        await evaluate(AdviceWriter(claude), records, slm_generate, default_today, keep_text, save, first)

    rows = read_rows(out)  # cả các dòng của lần chạy trước khi chạy tiếp
    print(f"Claude {settings.anthropic_model} viết từ 2 skill trên {len(rows)} dòng kiểm (so với: {name})")
    print(json.dumps({"khuon": score(rows, "khuon"), name: score(rows, "slm")}, ensure_ascii=False, indent=2))
    print(f"Chi tiết từng dòng: {out}")
    return 0


def main() -> None:
    sys.stdout.reconfigure(encoding="utf-8")
    parser = argparse.ArgumentParser(
        description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter
    )
    parser.add_argument(
        "--slm", choices=("dap_an", "slm"), default="dap_an", help="nguồn skill để so với khuôn"
    )
    parser.add_argument("--model", default=get_settings().slm_model, help="model Ollama khi --slm slm")
    parser.add_argument("--limit", type=int, default=None)
    parser.add_argument(
        "--hom-nay", type=date.fromisoformat, default=DEFAULT_TODAY, help="khi dòng không ghi tháng"
    )
    parser.add_argument(
        "--ghi-chu", action="store_true", help="ghi cả skill và câu trả lời vào file chi tiết"
    )

    parser.add_argument(
        "--tu-dong",
        type=int,
        default=1,
        help="chạy tiếp từ dòng N (đếm từ 1), giữ các dòng đã có trong file chi tiết",
    )

    args = parser.parse_args()
    if args.tu_dong < 1:
        parser.error("--tu-dong phải từ 1 trở lên")
    raise SystemExit(
        asyncio.run(run(args.slm, args.model, args.limit, args.hom_nay, args.ghi_chu, args.tu_dong))
    )


if __name__ == "__main__":
    main()
