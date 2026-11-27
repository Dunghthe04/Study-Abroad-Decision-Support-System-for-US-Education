"""Kiểm lời tư vấn hồ sơ có ĐÁP TRƯỚC rồi mới trình bày không (skill_ho_so_khuon 1.2, viet_tra_loi_ho_so 1.3).

Đi đủ đường như chat thật: SLM phân loại câu -> code dựng skill từ khuôn -> Claude viết. Câu SLM không đưa vào nhánh tư vấn
(đánh giá, lộ trình) thì chỉ ghi nhãn, không gọi Claude. Hồ sơ mẫu là hồ sơ đủ của tests/test_skill_template.py.

Chạy trong thư mục advisor (cần Ollama; có --goi-claude thì cần ANTHROPIC_API_KEY trong .env):
  .venv/Scripts/python.exe -m slm.eval.thu_tra_loi_ho_so                 # chỉ chạy SLM, không tốn tiền
  .venv/Scripts/python.exe -m slm.eval.thu_tra_loi_ho_so --goi-claude    # gọi Claude thật (Haiku, khoảng 0,1 cent mỗi câu)
Người đọc kết quả chấm: câu đầu có đáp đúng điều được hỏi không, có lan man không.
"""

import argparse
import asyncio
import json
import sys
from datetime import date
from pathlib import Path

import httpx

from app.core.config import get_settings
from app.schemas.chat import ChatState, ProfileField
from app.services import usage_meter
from app.services.advice_writer import AdviceWriter
from app.services.claude import ClaudeClient
from app.services.ollama import OllamaClient
from app.services.skill_input import skill_request_of
from app.services.skill_template import TEMPLATE_TASKS, build_skill, tasks_of
from app.services.skills.skill_danh_gia import gpa_table_of
from app.services.slm_classifier import OllamaSlm, output_format
from tests.test_skill_template import BENCHMARKS, FULL, TRANSCRIPT
from tests.vef import VEF_RULES

OUT = Path("slm/results/thu_tu_nhien/tra_loi_ho_so.jsonl")
PRICE_IN, PRICE_OUT = 0.10 / 1e6, 0.50 / 1e6  # claude-haiku-5-5, USD mỗi token

# Mỗi câu một kiểu hỏi khác nhau; cột 2 là điều câu đầu của câu trả lời phải đáp
CAU_HOI: list[tuple[str, str]] = [
    ("Em đang học lớp 11, bây giờ chuẩn bị có sớm quá không?", "có/không: sớm hay không"),
    ("hồ sơ vầy nộp mỹ có ổn ko ạ", "có/không: ổn hay chưa (gõ tắt, không dấu một phần)"),
    ("Tôi lo quá, không biết con tôi có đủ sức đi Mỹ không", "phụ huynh lo lắng: đủ sức hay không"),
    ("Điểm yếu lớn nhất trong hồ sơ của em là gì?", "hỏi mở: nêu tên điểm yếu nhất"),
    ("IELTS 6.5 vậy đủ chưa hay em phải thi lại?", "chọn một: đủ hay thi lại"),
    ("Từ giờ tới lúc nộp hồ sơ em nên ưu tiên làm gì trước?", "ưu tiên: nêu việc đầu tiên"),
    ("Giờ mới bắt đầu tìm hiểu thì có bị muộn không ạ?", "có/không: muộn hay không"),
    ("Em nên dồn sức cho SAT hay cho hoạt động ngoại khóa?", "chọn một trong hai"),
    ("đánh giá giúp mình hồ sơ với", "ra lệnh: nhận xét chung trước"),
    ("Mình có nên đợi thêm một năm rồi mới nộp không?", "nên hay không nên"),
    ("Nói thật đi, hồ sơ em có cửa không hay là thôi?", "có/không, giọng thẳng"),
    ("mấy tháng tới e cần làm j", "hỏi mở gõ tắt: việc trong mấy tháng tới"),
]


def profile_state() -> ChatState:
    profile = {key: ProfileField(value=value, status="confirmed") for key, value in FULL.items()}
    return ChatState(profile=profile, transcript=TRANSCRIPT, benchmarks=BENCHMARKS, grade_scale=VEF_RULES)


def label(intent) -> str:
    detail = "+".join(intent.entities.get("tasks") or [])
    return f"{intent.type}:{detail}" if detail else intent.type


async def main(call_claude: bool) -> None:
    s = get_settings()
    if call_claude and not s.anthropic_api_key:
        raise SystemExit("Chưa có ANTHROPIC_API_KEY trong advisor/.env")
    state, today = profile_state(), date.today()
    rows, total_in, total_out = [], 0, 0
    async with (
        httpx.AsyncClient(base_url=s.slm_base_url or s.ollama_base_url, timeout=s.llm_timeout_seconds) as slm_http,
        httpx.AsyncClient(base_url=s.anthropic_base_url, timeout=s.anthropic_timeout_seconds) as claude_http,
    ):
        slm = OllamaSlm(
            OllamaClient(
                slm_http, s.slm_model, num_ctx=s.llm_num_ctx, num_gpu=s.llm_num_gpu, seed=s.llm_seed,
                keep_alive=s.llm_keep_alive, num_predict=s.slm_num_predict,
            ),
            output_format(s.slm_format), s.slm_use_context, s.slm_prompt,
        )
        writer = AdviceWriter(ClaudeClient(
            claude_http, s.anthropic_model, s.anthropic_api_key, max_retries=s.anthropic_max_retries,
            fallback_model=s.anthropic_fallback_model or None,
        ))
        for question, expect in CAU_HOI:
            result = await slm.classify(state, question)
            labels = [label(y) for y in result.intents]
            row = {"cau": question, "can_dap": expect, "slm": labels, "fallback": result.fallback}
            review = next((y for y in result.intents if y.type == "profile_review"), None)
            tasks = (review.entities.get("tasks") or None) if review else None
            goes_to_writer = review is not None and (tasks is None or any(t in TEMPLATE_TASKS for t in tasks))
            row["vao_nhanh_tu_van"] = goes_to_writer
            if goes_to_writer and call_claude:
                req = skill_request_of(state, review.span, today)
                skill = build_skill(req, tasks, state.profile, today, gpa_table_of(state.grade_scale))
                calls = usage_meter.start()
                answer = await writer.write(skill, req, state.schools, tasks_of(tasks))
                used_in = sum(c.input_tokens for c in calls if c.source == "claude")
                used_out = sum(c.output_tokens for c in calls if c.source == "claude")
                total_in, total_out = total_in + used_in, total_out + used_out
                row |= {"tra_loi": answer, "so_lan_goi": writer.last_attempts, "loi": writer.last_errors,
                        "token_vao": used_in, "token_ra": used_out}
            rows.append(row)
            print(f"\n## {question}\n   cần đáp: {expect}\n   SLM: {labels}  vào nhánh tư vấn: {goes_to_writer}")
            if row.get("tra_loi") is not None or "so_lan_goi" in row:
                print(f"   gọi {row['so_lan_goi']} lần {row['loi']}\n---\n{row['tra_loi']}\n---")

    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text("".join(json.dumps(r, ensure_ascii=False) + "\n" for r in rows), encoding="utf-8")
    if call_claude:
        print(f"\nToken Claude: vào {total_in}, ra {total_out}, khoảng {total_in * PRICE_IN + total_out * PRICE_OUT:.4f} USD")


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    parser = argparse.ArgumentParser()
    parser.add_argument("--goi-claude", action="store_true", help="gọi Claude thật (tốn tiền API)")
    asyncio.run(main(parser.parse_args().goi_claude))
