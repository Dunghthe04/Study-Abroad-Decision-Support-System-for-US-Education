"""Hai "học sinh" điền hồ sơ trong chat từ đầu đến bảng xác nhận, bằng SLM và Claude thật.

Mỗi lượt trả lời đúng câu hệ thống đang hỏi (theo mã câu), ghi lại giá trị nào được lưu hay đổi, để người đọc so với điều
học sinh đã nói. Gọi Claude nên tốn tiền API (vài cent). Backend .NET không chạy phần lưu hồ sơ.

Chạy trong thư mục advisor (cần Ollama):  .venv/Scripts/python.exe -m slm.eval.thu_dien_ho_so
"""

import asyncio
import json
import sys
from pathlib import Path

import httpx

from app.core.config import get_settings
from app.schemas.chat import ChatMessage, ChatRequest
from app.services.advice_writer import AdviceWriter
from app.services.branches import build_branches
from app.services.chat_flow import ChatFlow
from app.services.claude import ClaudeClient
from app.services.intake import INTAKE_ID
from app.services.ollama import OllamaClient
from app.services.skills import build_handlers
from app.services.slm_classifier import OllamaSlm, output_format
from tests.vef import VEF_RULES

OUT = Path("slm/results/thu_tu_nhien/dien_ho_so.jsonl")
MAX_TURNS = 30

# Câu mở đầu và câu trả lời cho từng mã câu hỏi. Hai học sinh nói khác kiểu nhau
HOC_SINH: dict[str, dict[str, str]] = {
    "A (gõ có dấu)": {
        "mo_dau": "Em muốn được đánh giá hồ sơ du học",
        INTAKE_ID: "Trả lời trong chat",
        "B1.1": "Em đang học lớp 11 ạ",
        "B1.3": "Em muốn học khoa học máy tính",
        "B1.4": "Em định nhập học mùa thu năm 2028",
        "D1": "Em học chương trình THPT Việt Nam bình thường",
        "D2": "Lớp 10 em được 8.4, lớp 11 học kỳ 1 được 8.8, thang 10",
        "D3": "Em thi SAT được 1350 rồi, ACT thì chưa thi",
        "E1": "Em có IELTS 6.5, thi tháng 5 năm 2026",
        "E2": "Em định thi lại IELTS vào tháng 6/2027, mục tiêu 7.5",
        "E3": "Em định thi SAT lại tháng 10/2027, nhắm 1450",
        "F1": "Nhà em lo được khoảng 40 nghìn đô một năm",
        "F2": "Có học bổng thì tốt, không có vẫn đi được",
        "F3": "Ba mẹ em chi trả",
        "D4": "Em là người sáng lập câu lạc bộ lập trình ở trường, làm từ tháng 9/2024 đến giờ",
        "D5": "Em được huy chương đồng olympic tin học cấp tỉnh năm 2026",
        "N1": "Em có cô ruột ở California",
        "N2": "Em thích California với Texas",
        "N3": "Em thích trường công",
        "N4": "Em chưa từng bị từ chối visa",
        "X1": "Đúng rồi",
        "X2": "Có ạ",
        "X3": "Có ạ",
    },
    "B (gõ tắt, không dấu)": {
        "mo_dau": "tu van ho so giup e voi",
        INTAKE_ID: "Trả lời trong chat",
        "B1.1": "e hoc 12 r",
        "B1.3": "chac kinh te, e cung chua chac",
        "B1.4": "nam sau",
        "D1": "thpt vn thoi a",
        "D2": "tong ket 7.9",
        "D3": "chua thi sat",
        "E1": "chua co bang tieng anh nao",
        "E2": "chac thang 3 nam sau e thi ielts, muc tieu 6.0",
        "E3": "ko thi",
        "F1": "tam 700 trieu 1 nam",
        "F2": "bat buoc phai co hb moi di dc",
        "F3": "bo me",
        "D4": "ko co gi",
        "D5": "ko",
        "N1": "ko co ai",
        "N2": "chua biet",
        "N3": "sao cung dc",
        "N4": "chua",
        "X1": "Đúng rồi",
        "X2": "co",
        "X3": "co",
    },
}


async def main() -> None:
    s = get_settings()
    if not s.anthropic_api_key:
        raise SystemExit("Chưa có ANTHROPIC_API_KEY trong advisor/.env")
    rows, total_in, total_out = [], 0, 0
    async with (
        httpx.AsyncClient(base_url=s.ollama_base_url, timeout=s.llm_timeout_seconds) as ollama_http,
        httpx.AsyncClient(base_url=s.anthropic_base_url, timeout=s.anthropic_timeout_seconds) as claude_http,
    ):
        claude = ClaudeClient(
            claude_http, s.anthropic_model, s.anthropic_api_key, max_retries=s.anthropic_max_retries
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
        flow = ChatFlow(slm, build_branches(build_handlers(claude, s.anthropic_model), AdviceWriter(claude)))
        for student, script in HOC_SINH.items():
            print(f"\n######## {student}")
            state, message, seen, asked = None, script["mo_dau"], {}, {}
            for turn in range(1, MAX_TURNS + 1):
                res = await flow.reply(
                    ChatRequest(
                        messages=[ChatMessage(role="user", content=message)],
                        state=state,
                        study_level="undergraduate",
                        grade_scale=VEF_RULES,
                        has_committed_profile=False,
                    )
                )
                state = res.state
                used = [c for c in res.ai_usage if c.source == "claude"]
                total_in += sum(c.input_tokens for c in used)
                total_out += sum(c.output_tokens for c in used)
                profile = {k: [v.value, v.status] for k, v in res.profile.items()}
                changed = {k: v for k, v in profile.items() if seen.get(k) != v}
                seen = profile
                labels = [
                    f"{y.type}:{'+'.join(y.entities.get('tasks') or [])}"
                    for y in (res.slm.model_intents if res.slm else [])
                ]
                rows.append(
                    {
                        "hoc_sinh": student,
                        "luot": turn,
                        "noi": message,
                        "slm": labels,
                        "doi": changed,
                        "cau_tiep": state.question_id,
                        "tra_loi": res.answer,
                    }
                )
                print(f"\n[{turn}] NÓI: {message}")
                print(f"     SLM {labels}")
                print(f"     LƯU/ĐỔI: {json.dumps(changed, ensure_ascii=False, default=str)[:600]}")
                print(f"     HỆ THỐNG ({state.question_id}): {res.answer[:230].replace(chr(10), ' / ')}")
                question = state.question_id
                if question is None:
                    break
                asked[question] = asked.get(question, 0) + 1
                if asked[question] > 3:
                    print(f"     !! KẸT: câu {question} bị hỏi quá 3 lần, dừng")
                    break
                if question not in script:
                    print(f"     !! Không có câu trả lời soạn sẵn cho {question}, dừng")
                    break
                message = script[question]
                # Bị hỏi lại cùng một câu: làm như học sinh thật là bấm nút gợi ý đầu tiên
                if asked[question] >= 2 and res.quick_replies:
                    message = res.quick_replies[0].value
                    print(f"     (bị hỏi lại, bấm nút: {res.quick_replies[0].label})")
            print(f"\n=== HỒ SƠ CUỐI của {student}:")
            for k, v in seen.items():
                print(f"   {k:22} {json.dumps(v[0], ensure_ascii=False, default=str)[:230]}  [{v[1]}]")
    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(
        "".join(json.dumps(r, ensure_ascii=False, default=str) + "\n" for r in rows), encoding="utf-8"
    )
    print(
        f"\nToken Claude: vào {total_in}, ra {total_out}, khoảng {total_in * 0.1e-6 + total_out * 0.5e-6:.4f} USD"
    )


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    asyncio.run(main())
