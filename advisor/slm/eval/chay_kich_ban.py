"""Chạy bộ kịch bản (kich_ban.py) qua ChatFlow thật và tự chấm những điều có đúng sai rõ ràng.

Dùng thật: SLM (Ollama), kho kiến thức (Postgres), Claude, danh bạ trung tâm (API .NET đang chạy).
KHÔNG chạy qua backend .NET: lượt hệ thống giao việc cho backend (gợi ý, so sánh, what-if, goal seek, số liệu trường) chỉ
kiểm ĐÚNG VIỆC được giao, chưa kiểm câu trả lời cuối. Danh sách trường gợi ý và lộ trình là dữ liệu mẫu.
Máy không chấm được câu trả lời hay hay dở: đọc bản ghi hội thoại trong file báo cáo .md.
Cột "chặng" là suy đoán từ dấu hiệu, người sửa phải kiểm lại.

Gọi Claude nên tốn tiền API. Chạy trong thư mục advisor:
  .venv/Scripts/python.exe -m slm.eval.chay_kich_ban                 # chạy cả bộ một lần
  .venv/Scripts/python.exe -m slm.eval.chay_kich_ban --chi "01 " "05"  # chỉ kịch bản có tên chứa các chuỗi này
  .venv/Scripts/python.exe -m slm.eval.chay_kich_ban --lap 3         # chạy 3 lần để tách lỗi luôn có và lỗi chập chờn
"""

import argparse
import asyncio
import json
import re
import sys
from collections import Counter
from datetime import date, datetime, timedelta
from pathlib import Path

import httpx

from app.core.config import get_settings
from app.core.db import open_pool
from app.schemas.chat import ChatMessage, ChatRequest, ChatResponse, ChatState, ProfileField, ResultSchool
from app.services.advice_writer import AdviceWriter
from app.services.branches import build_branches
from app.services.centers import CenterFinder
from app.services.chat_flow import ChatFlow
from app.services.claude import ClaudeClient
from app.services.fit_method_writer import FitMethodWriter
from app.services.knowledge import KnowledgeRetriever
from app.services.ollama import OllamaClient
from app.services.replies import KNOWLEDGE_NO_BASIS, KNOWLEDGE_UNAVAILABLE
from app.services.skills import build_handlers
from app.services.slm_classifier import OllamaSlm, output_format
from app.services.what_if_writer import WhatIfWriter
from slm.eval.kich_ban import CHUA_CO_DIEM, DS_TRUONG, KHONG, KICH_BAN
from slm.eval.thu_cau_tu_nhien import grade, key
from tests.test_skill_template import BENCHMARKS
from tests.vef import VEF_RULES

OUT = Path("slm/results/kich_ban")
MAX_FORM_TURNS = 35
PRICE_IN, PRICE_OUT = 0.10 / 1e6, 0.50 / 1e6  # claude-haiku-5-5, USD mỗi token

SLM, CODE, CLAUDE, KHO, HE_QUA = "SLM", "code", "Claude/prompt", "kho/bộ chọn", "hệ quả của nhãn sai"

# ---------------------------------------------------------------------------------------------------------------------
# Điều LUÔN phải đúng ở mọi lượt
# ---------------------------------------------------------------------------------------------------------------------
RO_RI = re.compile(r"\"tra_loi\"|pham_vi|```|\bNone\b|\bnull\b|\{\"|skill_version|Mã: RAW|CẦN HỘI ĐỒNG DUYỆT")
MO_DAU_CO_KHONG = re.compile(r"^\W*(Có|Không)\s*[,:]")
HUA_DO = re.compile(
    r"(?<!không )(?<!chưa )(?<!không thể )(?<!không dám )(?:chắc chắn (?:sẽ )?(?:đỗ|đậu|được nhận)|"
    r"đảm bảo (?:sẽ )?(?:đỗ|đậu|trúng tuyển|được nhận))",
    re.IGNORECASE,
)
CHU_LA = re.compile(r"[一-鿿Ѐ-ӿ가-힯]")
NGAY = re.compile(r"\b(\d{2})/(\d{2})/(\d{4})\b")
HAN_THANG = re.compile(r"\bhạn (\d{2})/(\d{4})\b", re.IGNORECASE)
# Dòng nhắc ngày cũ mà có một trong các chữ này thì không bị coi là trình bày như việc sắp tới
DA_BAO = re.compile(
    r"đã qua|quá hạn|đã trôi|thu thập|cập nhật|tính đến|tỷ giá|hiệu lực|áp dụng|từ ngày|kể từ|hôm nay",
    re.IGNORECASE,
)


def moc_cu_khong_bao(answer: str, today: date) -> list[str]:
    """Các dòng nêu một hạn đã qua mà không nói là đã qua."""
    bad = []
    for line in answer.splitlines():
        if DA_BAO.search(line):
            continue
        past = False
        for d, m, y in NGAY.findall(line):
            try:
                past = past or date(int(y), int(m), int(d)) < today
            except ValueError:
                pass
        past = past or any((int(y), int(m)) < (today.year, today.month) for m, y in HAN_THANG.findall(line))
        if past:
            bad.append(line.strip()[:140])
    return bad


def kiem_chung(answer: str, citations: list[str], today: date) -> list[str]:
    fails = []
    if not answer.strip():
        fails.append("câu trả lời rỗng")
    if hit := RO_RI.search(answer):
        fails.append(f"lộ chữ nội bộ trong câu trả lời: {hit[0]!r}")
    for title in citations:
        if hit := RO_RI.search(title):
            fails.append(f"lộ mã nội bộ ở tên nguồn: {title[:90]!r}")
            break
    if MO_DAU_CO_KHONG.search(answer):
        fails.append(f"mở đầu bằng 'Có/Không' đứng riêng: {answer.strip()[:60]!r}")
    if hit := HUA_DO.search(answer):
        fails.append(f"hứa kết quả tuyển sinh: {hit[0]!r}")
    if CHU_LA.search(answer):
        fails.append("có chữ Trung/Nga/Hàn")
    fails += [f"hạn đã qua mà không báo: {line!r}" for line in moc_cu_khong_bao(answer, today)]
    return fails


# ---------------------------------------------------------------------------------------------------------------------
# Kỳ vọng riêng của lượt
# ---------------------------------------------------------------------------------------------------------------------
def cham_nhan(accepted: list[str], predicted: list[str], fallback: bool) -> tuple[str | None, str | None]:
    """(lỗi, cảnh báo). Sai chi tiết của knowledge / profile_review chỉ cảnh báo; sai loại con của kịch bản và
    ngoài phạm vi là lỗi vì đổi hẳn câu trả lời."""
    if fallback:
        return f"SLM trả sai khuôn (cần {accepted})", None
    result = grade(predicted, accepted)
    if result == "đúng":
        return None, None
    text = f"nhãn {predicted}, cần một trong {accepted}"
    strict = any(p.startswith(("scenario", "out_of_scope")) for p in predicted)
    if result == "đúng loại, sai chi tiết" and not strict:
        return None, text
    return text, None


def cham_noi_dung(
    turn: dict, answer: str, action: str | None, citations: list, profile: dict, before: dict
) -> list[str]:
    fails = []
    if "action" in turn:
        wanted = turn["action"]
        if wanted == KHONG:
            if action is not None:
                fails.append(f"không được giao việc cho backend nhưng giao {action!r}")
        elif action not in ([wanted] if isinstance(wanted, str) else wanted):
            fails.append(f"cần giao backend {wanted!r}, thực tế {action!r}")
    # Lượt giao việc cho backend: chữ ở đây chỉ là câu chờ nên không chấm nội dung
    waiting = action is not None and action != "school_info"
    if not waiting:
        for pattern in turn.get("co", []):
            if not re.search(pattern, answer, re.IGNORECASE):
                fails.append(f"thiếu ý /{pattern}/")
        if turn.get("nguon") and not citations:
            fails.append("không kèm nguồn")
    for pattern in turn.get("khong", []):
        if hit := re.search(pattern, answer, re.IGNORECASE):
            fails.append(f"có điều không được có: {hit[0][:70]!r}")
    if "toi_da" in turn and len(answer.split()) > turn["toi_da"]:
        fails.append(f"dài {len(answer.split())} từ, tối đa {turn['toi_da']}")
    for name, pattern in turn.get("luu", {}).items():
        value = json.dumps(profile.get(name), ensure_ascii=False, default=str)
        if not re.search(pattern, value, re.IGNORECASE):
            fails.append(f"hồ sơ '{name}' đang là {value[:90]}, cần khớp /{pattern}/")
    for name in turn.get("giu", []):
        if profile.get(name) != before.get(name):
            fails.append(f"hồ sơ '{name}' bị đổi: {before.get(name)!r} -> {profile.get(name)!r}"[:160])
    return fails


def chang_cua(turn: dict, answer: str, used_claude: bool, label_wrong: bool) -> str:
    if label_wrong:
        return HE_QUA
    if turn.get("co") and (KNOWLEDGE_NO_BASIS in answer or KNOWLEDGE_UNAVAILABLE in answer):
        return KHO
    return CLAUDE if used_claude else CODE


# ---------------------------------------------------------------------------------------------------------------------
# Chạy
# ---------------------------------------------------------------------------------------------------------------------
def lo_trinh_mau(today: date) -> dict:
    """Lộ trình kiểu .NET gửi cho học sinh chưa chọn kỳ: có một mốc đã qua để kiểm chat có báo là đã qua không."""
    past, soon, later = today - timedelta(days=26), today + timedelta(days=150), today + timedelta(days=500)
    year = today.year + 2
    return {
        "mode": "target",
        "intake": f"Fall {year}",
        "intakeIsEstimate": True,
        "summary": "Đạt đủ điều kiện 0/1 trường mục tiêu",
        "schools": [
            {
                "name": "Purdue University",
                "criteria": [{"key": "gpa", "type": None, "mine": 3.6, "mark": 3.8, "status": "below"}],
            }
        ],
        "milestones": [
            {"date": past.isoformat(), "label": "Thi SAT", "isEstimate": True},
            {"date": soon.isoformat(), "label": "Hoàn thiện hồ sơ hoạt động", "isEstimate": True},
            {"date": later.isoformat(), "label": f"Nhập học Fall {year}", "isEstimate": True},
        ],
        "tasks": [
            {"label": "Đạt SAT 1450", "due": past.isoformat(), "status": "overdue"},
            {"label": "Hoàn thiện bài luận", "due": soon.isoformat(), "status": "todo"},
        ],
        "provinceMissing": False,
    }


def lap_cau(row: dict, previous: dict | None) -> bool:
    """Học sinh nói khác mà nhận y nguyên câu cũ. Câu chờ backend và câu từ chối soạn sẵn giống nhau là bình thường."""
    if previous is None or row["noi"] == previous["noi"] or row["action"] is not None:
        return False
    if any(label.startswith("out_of_scope") for label in row["nhan"]):
        return False
    return row["tra_loi"] == previous.get("tra_loi")


def profile_values(state: ChatState) -> dict:
    return {name: field.value for name, field in state.profile.items()}


async def run_scenario(flow: ChatFlow, scenario: dict, today: date) -> tuple[list[dict], int, int]:
    schools = [ResultSchool(code=c, name=n, group=g) for c, n, g in DS_TRUONG]
    profile = scenario.get("hs")
    state = None
    if profile:
        state = ChatState(
            profile={
                k: ProfileField(value=None, status="none")
                if v == CHUA_CO_DIEM
                else ProfileField(value=v, status="confirmed")
                for k, v in profile.items()
            }
        )
    has_list = bool(scenario.get("co_ds"))
    rows, tokens_in, tokens_out = [], 0, 0

    async def send(turn: dict, message: str) -> ChatResponse | None:
        nonlocal state, has_list, tokens_in, tokens_out
        before = profile_values(state) if state else {}
        row = {"kich_ban": scenario["ten"], "luot": len(rows) + 1, "noi": message, "loi": [], "canh_bao": []}
        rows.append(row)
        request = ChatRequest(
            messages=[ChatMessage(role="user", content=message)],
            state=state,
            study_level="undergraduate",
            grade_scale=VEF_RULES,
            benchmarks=BENCHMARKS,
            has_committed_profile=bool(profile),
            schools=schools if has_list else None,
            roadmap=lo_trinh_mau(today) if scenario.get("lo_trinh") else None,
        )
        try:
            res = await flow.reply(request)
        except Exception as ex:  # ghi lại rồi chạy tiếp lượt sau
            row |= {"tra_loi": "", "loi": [{"chang": CODE, "chi_tiet": f"hệ thống báo lỗi: {ex!r}"[:300]}]}
            return None
        state = res.state
        if res.action == "recommend":
            has_list = True
        claude_calls = [c for c in res.ai_usage if c.source == "claude"]
        tokens_in += sum(c.input_tokens for c in claude_calls)
        tokens_out += sum(c.output_tokens for c in claude_calls)
        labels = [key(y) for y in res.slm.model_intents] if res.slm else []
        fallback = bool(res.slm and res.slm.fallback)
        label_wrong = False
        if "nhan" in turn and res.slm is not None:
            error, warning = cham_nhan(turn["nhan"], labels, fallback)
            if error:
                label_wrong = True
                row["loi"].append({"chang": SLM, "chi_tiet": error})
            if warning:
                row["canh_bao"].append(warning)
        titles = [c.title for c in res.citations]
        others = cham_noi_dung(turn, res.answer, res.action, res.citations, profile_values(state), before)
        others += kiem_chung(res.answer, titles, today)
        current = {"noi": message, "action": res.action, "nhan": labels, "tra_loi": res.answer}
        if lap_cau(current, rows[-2] if len(rows) > 1 else None):
            others.append("trả lời y hệt lượt trước dù học sinh nói khác")
        stage = chang_cua(turn, res.answer, bool(claude_calls), label_wrong)
        row["loi"] += [{"chang": stage, "chi_tiet": text} for text in others]
        row |= {
            "nhan": ["SAI KHUÔN"] if fallback else labels,
            "tra_loi": res.answer,
            "action": res.action,
            "action_data": res.action_data,
            "nut": [b.label for b in res.quick_replies],
            "nguon": titles,
            "cau_dang_cho": state.question_id,
        }
        return res

    for turn in scenario["luot"]:
        if "dien_ho_so" not in turn:
            await send(turn, turn["noi"])
            continue
        script, asked = turn["dien_ho_so"], Counter()
        res = None
        for _ in range(MAX_FORM_TURNS):
            question = state.question_id if state else None
            if question is None or question not in script:
                break
            asked[question] += 1
            if asked[question] > 3:
                rows[-1]["loi"].append(
                    {"chang": CODE, "chi_tiet": f"kẹt khi điền hồ sơ: câu {question} bị hỏi quá 3 lần"}
                )
                break
            answers = script[question] if isinstance(script[question], list) else [script[question]]
            message = answers[min(asked[question], len(answers)) - 1]
            # Bị hỏi lại mà không còn câu khác để nói thì làm như học sinh thật: bấm nút gợi ý đầu tiên
            if asked[question] > len(answers) and res is not None and res.quick_replies:
                message = res.quick_replies[0].value
            res = await send({}, message)
            if question == "X1":
                break
    return rows, tokens_in, tokens_out


def bao_cao(runs: list[list[dict]], cost: float, when: str) -> str:
    rows = [row for run in runs for row in run]
    failed = [row for row in rows if row["loi"]]
    stages = Counter(item["chang"] for row in failed for item in row["loi"])
    lines = [
        f"# Báo cáo bộ kịch bản {when}",
        "",
        f"- Số lần chạy: {len(runs)}; số lượt hỏi: {len(rows)}; lượt có lỗi: {len(failed)}",
        f"- Lỗi theo chặng (suy đoán, cần kiểm lại): {dict(stages)}",
        f"- Tiền Claude ước tính: {cost:.4f} USD",
        "- Chưa kiểm: câu trả lời cuối do backend .NET ghép, giao diện, chất lượng lời văn.",
        "",
    ]
    # Một lỗi lặp lại ở mọi lần chạy khác với lỗi lúc có lúc không
    seen: dict[tuple[str, int], list[dict]] = {}
    for run in runs:
        for row in run:
            if row["loi"]:
                seen.setdefault((row["kich_ban"], row["luot"]), []).append(row)
    lines.append("## Các lượt có lỗi")
    for (name, number), items in sorted(seen.items()):
        often = "" if len(runs) == 1 else f" ({len(items)}/{len(runs)} lần)"
        row = items[0]
        lines += [
            "",
            f"### {name} · lượt {number}{often}",
            f"> {row['noi']}",
            f"- nhãn: {row.get('nhan')}; action: {row.get('action')}",
        ]
        lines += [f"- **[{item['chang']}]** {item['chi_tiet']}" for item in row["loi"]]
        lines.append("- trả lời: " + row.get("tra_loi", "")[:600].replace("\n", " / "))
    lines += ["", "## Toàn bộ hội thoại (lần chạy đầu), để người đọc chấm lời văn"]
    current = None
    for row in runs[0]:
        if row["kich_ban"] != current:
            current = row["kich_ban"]
            lines += ["", f"### {current}"]
        mark = "❌" if row["loi"] else "✅"
        lines += [
            "",
            f"**{mark} {row['luot']}. {row['noi']}**  ",
            f"`{row.get('nhan')}` action `{row.get('action')}`",
            "",
        ]
        lines += ["> " + part for part in row.get("tra_loi", "").splitlines()]
    return "\n".join(lines) + "\n"


async def main(only: list[str], repeat: int) -> None:
    s = get_settings()
    if not s.anthropic_api_key:
        raise SystemExit("Chưa có ANTHROPIC_API_KEY trong advisor/.env")
    chosen = [k for k in KICH_BAN if not only or any(text in k["ten"] for text in only)]
    if not chosen:
        raise SystemExit("Không có kịch bản nào khớp")
    pool = open_pool(s.database_url)
    if pool is None:
        raise SystemExit("Không nối được Postgres")
    today = date.today()
    runs, total_in, total_out = [], 0, 0
    try:
        async with (
            httpx.AsyncClient(base_url=s.ollama_base_url, timeout=s.llm_timeout_seconds) as ollama_http,
            httpx.AsyncClient(
                base_url=s.anthropic_base_url, timeout=s.anthropic_timeout_seconds
            ) as claude_http,
            httpx.AsyncClient(base_url=s.backend_base_url, timeout=s.backend_timeout_seconds) as backend_http,
        ):
            claude = ClaudeClient(claude_http, s.anthropic_model, s.anthropic_api_key, max_retries=2)
            slm_client = OllamaClient(
                ollama_http,
                s.slm_model,
                num_ctx=s.llm_num_ctx,
                num_gpu=s.llm_num_gpu,
                seed=s.llm_seed,
                keep_alive=s.llm_keep_alive,
            )
            slm = OllamaSlm(slm_client, output_format(s.slm_format), s.slm_use_context, s.slm_prompt)
            embedder = OllamaClient(ollama_http, s.slm_model, embedding_model=s.embedding_model)
            skills = build_handlers(
                claude,
                s.anthropic_model,
                KnowledgeRetriever(pool, s.knowledge_top_k),
                s.knowledge_min_score,
                CenterFinder(backend_http),
                embedder,
            )
            # Ghép giống app/main.py
            flow = ChatFlow(
                slm,
                build_branches(
                    skills,
                    AdviceWriter(claude),
                    WhatIfWriter(claude, s.anthropic_model),
                    FitMethodWriter(claude, s.anthropic_model, backend_http),
                ),
            )
            for number in range(repeat):
                run = []
                for scenario in chosen:
                    rows, used_in, used_out = await run_scenario(flow, scenario, today)
                    total_in, total_out = total_in + used_in, total_out + used_out
                    run += rows
                    bad = sum(bool(r["loi"]) for r in rows)
                    print(
                        f"[lần {number + 1}] {scenario['ten']}: {len(rows)} lượt, {bad} lượt có lỗi",
                        flush=True,
                    )
                runs.append(run)
    finally:
        pool.close()

    cost = total_in * PRICE_IN + total_out * PRICE_OUT
    when = datetime.now().strftime("%Y%m%d_%H%M")
    OUT.mkdir(parents=True, exist_ok=True)
    (OUT / f"ket_qua_{when}.jsonl").write_text(
        "".join(
            json.dumps({"lan": i + 1, **row}, ensure_ascii=False, default=str) + "\n"
            for i, run in enumerate(runs)
            for row in run
        ),
        encoding="utf-8",
    )
    report = OUT / f"bao_cao_{when}.md"
    report.write_text(bao_cao(runs, cost, when), encoding="utf-8")
    rows = [row for run in runs for row in run]
    print(
        f"\nTổng: {len(rows)} lượt, {sum(bool(r['loi']) for r in rows)} lượt có lỗi, Claude ≈ {cost:.4f} USD"
    )
    print("Lỗi theo chặng:", dict(Counter(i["chang"] for r in rows for i in r["loi"])))
    print("Báo cáo:", report.resolve())


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--chi", nargs="*", default=[], help="chỉ chạy kịch bản có tên chứa một trong các chuỗi này"
    )
    parser.add_argument("--lap", type=int, default=1, help="số lần chạy cả bộ")
    args = parser.parse_args()
    asyncio.run(main(args.chi, args.lap))
