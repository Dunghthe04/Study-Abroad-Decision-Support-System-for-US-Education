"""Kiểm mọi nhánh trả lời của chat bằng câu hỏi nhiều kiểu, đi đúng đường chat thật: SLM -> nhánh -> Claude -> kiểm.

Dùng SLM, Claude, kho kiến thức thật và danh bạ trung tâm của backend. Phần do backend .NET dựng sau khi advisor trả "action"
(số liệu trường, tìm trường, thử giả định, cần gì để vào trường, gợi ý danh sách) KHÔNG chạy ở đây: chỉ ghi action và dữ liệu
gửi sang. Hồ sơ và danh sách trường gợi ý là dữ liệu mẫu. Gọi Claude nên tốn tiền API (Haiku, vài cent cả bộ).

Chạy trong thư mục advisor (cần Ollama, Postgres, backend):  .venv/Scripts/python.exe -m slm.eval.thu_cac_nhanh [--nhom kiến thức]
"""

import argparse
import asyncio
import json
import sys
from pathlib import Path

import httpx

from app.core.config import get_settings
from app.core.db import open_pool
from app.schemas.chat import ChatMessage, ChatRequest, ChatState, ProfileField, ResultSchool
from app.services.advice_writer import AdviceWriter
from app.services.branches import build_branches
from app.services.centers import CenterFinder
from app.services.chat_flow import ChatFlow
from app.services.claude import ClaudeClient
from app.services.knowledge import KnowledgeRetriever
from app.services.ollama import OllamaClient
from app.services.skills import build_handlers
from app.services.slm_classifier import OllamaSlm, output_format
from app.services.what_if_writer import WhatIfWriter
from tests.test_skill_template import BENCHMARKS, FULL, TRANSCRIPT
from tests.vef import VEF_RULES

OUT = Path("slm/results/thu_tu_nhien/cac_nhanh.jsonl")
PRICE_IN, PRICE_OUT = 0.10 / 1e6, 0.50 / 1e6  # claude-haiku-5-5, USD mỗi token
SCHOOLS = [
    ResultSchool(code="243780", name="Purdue University-Main Campus", group="reach"),
    ResultSchool(code="104151", name="Arizona State University Campus Immersion", group="target"),
    ResultSchool(code="212054", name="Drexel University", group="safety"),
]

# Mỗi câu một kiểu hỏi khác nhau
CAU_HOI: dict[str, list[str]] = {
    "kiến thức": [
        "du học mỹ tốn tầm bao nhiêu 1 năm",
        "Học xong đại học có được ở lại Mỹ làm việc không ạ?",
        "I-20 là cái gì",
        "xin học bổng khó ko",
        "Gia đình cần chứng minh tài chính bao nhiêu tiền?",
        "bị rớt visa rồi nộp lại được ko",
        "bắt buộc phải có SAT hay chỉ cần ielts là đủ",
        "qua bển được đi làm thêm mấy tiếng một tuần",
        "Cho em xin lịch nghỉ hè năm 2027 của ký túc xá",
        "trường mỹ có nhận chứng chỉ tin học MOS không",
    ],
    "số liệu và tìm trường": [
        "học phí Purdue bao nhiêu",
        "UCLA với USC trường nào khó vào hơn",
        "trường nào ở Texas dưới 30k một năm",
        "asu có học bổng cho du học sinh không",
        "còn hạn nộp thì sao",
    ],
    "giả định": [
        "nếu SAT em lên 1450 thì sao",
        "giả sử nhà em chỉ lo được 25k một năm thôi",
        "em đổi sang ngành kinh tế thì danh sách có khác không",
        "lỡ ielts chỉ được 6.0 thì có mất trường nào không",
        "nếu em học thêm piano thì cơ hội có tăng không",
    ],
    "cần gì để vào trường": [
        "muốn đậu NYU thì em cần cải thiện gì",
        "SAT bao nhiêu mới vào được Purdue",
        "làm sao để vào Harvard",
    ],
    "hợp trường và so sánh": [
        "sao Purdue lại bị xếp vào nhóm khó vào",
        "Drexel có hợp với em không",
        "trong danh sách thì trường nào hợp em nhất",
        "điểm phù hợp tính kiểu gì vậy",
        "so sánh giúp em Purdue với Drexel",
        "gợi ý trường cho em đi",
    ],
    "trung tâm": [
        "tìm trung tâm luyện ielts ở Đà Nẵng",
        "có chỗ nào sửa bài luận giúp em không",
        "em muốn đặt lịch tư vấn phỏng vấn visa",
        "trung tâm nào uy tín nhất vậy",
    ],
    "ngoài phạm vi và xã giao": [
        "bạn là ai vậy",
        "cho em gặp người thật được không",
        "du học Nhật thì sao",
        "tối nay ăn gì ngon",
        "ok em cảm ơn nhiều nha",
    ],
    "hồ sơ": [
        "sửa SAT thành 1450 giúp em",
        "hồ sơ em đang lưu những gì",
        "em muốn làm lại hồ sơ từ đầu",
        "em muốn sửa hồ sơ",
    ],
}


def new_state() -> ChatState:
    profile = {key: ProfileField(value=value, status="confirmed") for key, value in FULL.items()}
    return ChatState(
        current_step="B3",
        profile=profile,
        transcript=TRANSCRIPT,
        benchmarks=BENCHMARKS,
        grade_scale=VEF_RULES,
        has_results=True,
        schools=SCHOOLS,
        has_committed_profile=True,
    )


async def main(group: str | None) -> None:
    s = get_settings()
    if not s.anthropic_api_key:
        raise SystemExit("Chưa có ANTHROPIC_API_KEY trong advisor/.env")
    pool = open_pool(s.database_url)
    rows, total_in, total_out = [], 0, 0
    try:
        async with (
            httpx.AsyncClient(base_url=s.ollama_base_url, timeout=s.llm_timeout_seconds) as ollama_http,
            httpx.AsyncClient(
                base_url=s.anthropic_base_url, timeout=s.anthropic_timeout_seconds
            ) as claude_http,
            httpx.AsyncClient(base_url=s.backend_base_url, timeout=s.backend_timeout_seconds) as backend_http,
        ):
            claude = ClaudeClient(
                claude_http,
                s.anthropic_model,
                s.anthropic_api_key,
                max_retries=s.anthropic_max_retries,
                fallback_model=s.anthropic_fallback_model or None,
            )
            slm = OllamaSlm(
                OllamaClient(
                    ollama_http,
                    s.slm_model,
                    num_ctx=s.llm_num_ctx,
                    num_gpu=s.llm_num_gpu,
                    seed=s.llm_seed,
                    keep_alive=s.llm_keep_alive,
                ),
                output_format(s.slm_format),
                s.slm_use_context,
                s.slm_prompt,
            )
            embedder = OllamaClient(ollama_http, s.slm_model, embedding_model=s.embedding_model)
            skills = build_handlers(
                claude,
                s.anthropic_model,
                KnowledgeRetriever(pool, s.knowledge_top_k),
                s.knowledge_min_score,
                CenterFinder(backend_http),
                embedder,
            )
            flow = ChatFlow(
                slm, build_branches(skills, AdviceWriter(claude), WhatIfWriter(claude, s.anthropic_model))
            )
            for name, questions in CAU_HOI.items():
                if group and name != group:
                    continue
                state = new_state()
                for question in questions:
                    # Nhóm "số liệu và tìm trường" giữ state giữa các câu để câu nối tiếp có ngữ cảnh
                    if name != "số liệu và tìm trường":
                        state = new_state()
                    row = {"nhom": name, "cau": question}
                    try:
                        res = await flow.reply(
                            ChatRequest(messages=[ChatMessage(role="user", content=question)], state=state)
                        )
                        state = res.state
                        used = [c for c in res.ai_usage if c.source == "claude"]
                        total_in += sum(c.input_tokens for c in used)
                        total_out += sum(c.output_tokens for c in used)
                        row |= {
                            "slm": [
                                f"{y.type}:{y.entities.get('mode') or y.entities.get('subtype') or '+'.join(y.entities.get('tasks') or [])}"
                                for y in res.slm.model_intents
                            ],
                            "sai_khuon": res.slm.fallback,
                            "chay": res.skills,
                            "action": res.action,
                            "action_data": res.action_data,
                            "tra_loi": res.answer,
                            "nguon": [c.entry_no for c in res.citations],
                            "nut": [q.label for q in res.quick_replies],
                            "widget": res.widget.type if res.widget else None,
                        }
                    except Exception as ex:  # ghi lỗi rồi chạy tiếp câu sau
                        row["loi"] = repr(ex)[:400]
                    rows.append(row)
    finally:
        pool.close()
    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(
        "".join(json.dumps(r, ensure_ascii=False, default=str) + "\n" for r in rows), encoding="utf-8"
    )
    for r in rows:
        print(f"\n## [{r['nhom']}] {r['cau']}")
        if "loi" in r:
            print("   LỖI:", r["loi"])
            continue
        print(
            f"   SLM {r['slm']}{' (SAI KHUÔN)' if r['sai_khuon'] else ''}  chạy {r['chay']}  nguồn {r['nguon']}"
        )
        if r["action"]:
            print(
                f"   action={r['action']} {json.dumps(r['action_data'], ensure_ascii=False, default=str)[:260]}"
            )
        print(f"   widget={r['widget']} nút={r['nut'][:4]}")
        print("   >> " + r["tra_loi"][:420].replace("\n", " / "))
    print(
        f"\nToken Claude: vào {total_in}, ra {total_out}, khoảng {total_in * PRICE_IN + total_out * PRICE_OUT:.4f} USD"
    )


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    parser = argparse.ArgumentParser()
    parser.add_argument("--nhom", default=None)
    asyncio.run(main(parser.parse_args().nhom))
