"""Chạy 100 câu rời và 4 hội thoại trong cau_hoi_100.py.

100 câu rời: gọi SLM ở hai ngữ cảnh (vừa mở chat, đã có hồ sơ).
Hội thoại: chạy ChatFlow thật với SLM và kho kiến thức thật, để câu đang chờ, hồ sơ, ngữ cảnh đổi như khi chat.
Claude được thay bằng bản giả luôn báo lỗi (không tốn tiền API) nên CHỮ trả lời không giống chat thật; backend .NET không chạy,
danh sách trường gợi ý ở hội thoại 1 là danh sách giả để câu "trường đầu tiên" có cái để chỉ.

Chạy trong thư mục advisor (cần Ollama và Postgres):  .venv/Scripts/python.exe -m slm.eval.thu_hoi_thoai
"""

import asyncio
import json

import httpx

from app.core.config import get_settings
from app.core.db import open_pool
from app.schemas.chat import ChatMessage, ChatRequest, ResultSchool
from app.services.branches import build_branches
from app.services.chat_flow import QUESTIONS, ChatFlow
from app.services.claude import ClaudeError
from app.services.knowledge import KnowledgeRetriever
from app.services.ollama import OllamaClient
from app.services.skills import build_handlers
from app.services.slm_classifier import OllamaSlm, output_format
from slm.eval.cau_hoi_100 import CAU_100, HOI_THOAI
from slm.eval.thu_cau_tu_nhien import HO_SO, OUT, grade, key, nearest, train_questions, write_excel

SCHOOLS_GIA = [
    ResultSchool(code="a", name="Arizona State University", group="target"),
    ResultSchool(code="b", name="Purdue University", group="reach"),
    ResultSchool(code="c", name="Drexel University", group="safety"),
]


class ClaudeGia:
    async def chat_json(self, *args, **kwargs):
        raise ClaudeError("bản giả: không gọi Claude")

    async def chat_text(self, *args, **kwargs):
        raise ClaudeError("bản giả: không gọi Claude")


class GhiLai:
    """Bọc SLM để biết đúng đầu vào và đầu ra của từng lượt."""

    def __init__(self, slm: OllamaSlm) -> None:
        self.slm, self.calls = slm, []

    async def classify(self, state, message):
        pending = state.last_question
        result = await self.slm.classify(state, message)
        self.calls.append((pending, result))
        return result


async def run_singles(slm: OllamaSlm) -> list[dict]:
    rows = []
    for number, group, question, accepted in CAU_100:
        for situation, profile, pending in (("vừa mở chat", None, QUESTIONS["B0.1"]), ("đã có hồ sơ", HO_SO, "")):
            result = await slm.read(question, profile, pending)
            predicted = [] if result.fallback else [key(y) for y in result.intents]
            rows.append({
                "so": number, "nhom": group, "ngu_canh": situation, "cau": f"{number}. {question}", "goc": question,
                "dap_an": accepted, "slm": predicted,
                "ket_qua": "SAI KHUÔN" if result.fallback else grade(predicted, accepted),
                "tin_cay": round(result.confidence, 3), "entities": [y.entities for y in result.intents],
            })
    return rows


async def run_conversations(flow: ChatFlow, spy: GhiLai) -> list[dict]:
    rows = []
    for name, turns in HOI_THOAI.items():
        state, schools = None, None
        for index, (message, accepted) in enumerate(turns, 1):
            if name == "Hội thoại 1" and index == 6:
                schools = SCHOOLS_GIA
            before = len(spy.calls)
            request = ChatRequest(messages=[ChatMessage(role="user", content=message)], state=state, schools=schools)
            try:
                response = await flow.reply(request)
                error = None
            except Exception as ex:  # ghi lại lỗi rồi chạy tiếp lượt sau
                response, error = None, repr(ex)[:300]
            row = {"hoi_thoai": name, "luot": index, "cau": message, "dap_an": accepted, "loi": error}
            if len(spy.calls) > before:
                pending, result = spy.calls[-1]
                predicted = [] if result.fallback else [key(y) for y in result.intents]
                row |= {
                    "cau_dang_cho": pending, "slm": predicted,
                    "ket_qua": "SAI KHUÔN" if result.fallback else grade(predicted, accepted),
                    "tin_cay": round(result.confidence, 3), "entities": [y.entities for y in result.intents],
                }
            else:
                row |= {"cau_dang_cho": None, "slm": ["(code tự xử lý, không gọi SLM)"], "ket_qua": "không gọi SLM"}
            if response is not None:
                state = response.state
                row |= {
                    "tra_loi": response.answer[:400], "action": response.action, "action_data": response.action_data,
                    "ho_so": {k: v.value for k, v in response.profile.items()},
                    "ngu_canh_sau": response.state.context.model_dump(exclude_defaults=True),
                }
            rows.append(row)
    return rows


async def main() -> None:
    s = get_settings()
    pool = open_pool(s.database_url)
    try:
        async with (
            httpx.AsyncClient(base_url=s.slm_base_url or s.ollama_base_url, timeout=s.llm_timeout_seconds) as slm_http,
            httpx.AsyncClient(base_url=s.ollama_base_url, timeout=s.llm_timeout_seconds) as embed_http,
        ):
            client = OllamaClient(
                slm_http, s.slm_model, num_ctx=s.llm_num_ctx, num_gpu=s.llm_num_gpu,
                seed=s.llm_seed, keep_alive=s.llm_keep_alive, num_predict=s.slm_num_predict,
            )
            slm = OllamaSlm(client, output_format(s.slm_format), s.slm_use_context, s.slm_prompt)
            embedder = OllamaClient(embed_http, s.slm_model, embedding_model=s.embedding_model)
            claude = ClaudeGia()
            knowledge = KnowledgeRetriever(pool, s.knowledge_top_k) if pool is not None else None
            skills = build_handlers(claude, s.anthropic_model, knowledge, s.knowledge_min_score, None, embedder)
            spy = GhiLai(slm)
            flow = ChatFlow(spy, build_branches(skills))
            conversations = await run_conversations(flow, spy)
            singles = await run_singles(slm)
    finally:
        if pool is not None:
            pool.close()

    train = train_questions()
    for row in singles:
        score, text, labels = nearest(row["goc"], train)
        row["train_gan_nhat"], row["nhan_train"], row["giong"] = text, labels, round(score, 2)

    OUT.mkdir(parents=True, exist_ok=True)
    for file_name, rows in (("cau_100.jsonl", singles), ("hoi_thoai.jsonl", conversations)):
        (OUT / file_name).write_text(
            "".join(json.dumps(r, ensure_ascii=False, default=str) + "\n" for r in rows), encoding="utf-8"
        )
    write_excel(singles, s.slm_model, OUT / "cau_100.xlsx")

    for situation in ("vừa mở chat", "đã có hồ sơ"):
        part = [r for r in singles if r["ngu_canh"] == situation]
        exact = sum(r["ket_qua"] == "đúng" for r in part)
        kind = sum(r["ket_qua"] in ("đúng", "đúng loại, sai chi tiết") for r in part)
        print(f"{situation}: n={len(part)} đúng loại {kind}, đúng cả chi tiết {exact}")
    print("câu trùng y nguyên một câu train:", sorted({r["so"] for r in singles if r["giong"] >= 0.999}))
    for r in sorted((r for r in singles if r["ket_qua"] != "đúng"), key=lambda r: (r["so"], r["ngu_canh"])):
        print(f"[{r['ket_qua']}] ({r['ngu_canh']}) {r['cau']}\n    đáp án {r['dap_an']}  SLM {r['slm']}  tc {r['tin_cay']}")
    print("\n=== entities (đã có hồ sơ) của câu đúng loại: kiểm tay")
    for r in singles:
        if r["ngu_canh"] == "đã có hồ sơ" and r["ket_qua"] != "SAI LOẠI" and r["entities"] not in ([{}],):
            print(r["so"], r["slm"], json.dumps(r["entities"], ensure_ascii=False))
    print("\n=== HỘI THOẠI")
    for r in conversations:
        print(f"\n{r['hoi_thoai']} lượt {r['luot']}: {r['cau']}  [{r['ket_qua']}]")
        print(f"   câu đang chờ: {r['cau_dang_cho']!r}")
        print(f"   SLM {r['slm']} tc {r.get('tin_cay')} entities {json.dumps(r.get('entities'), ensure_ascii=False)}")
        if r["loi"]:
            print("   LỖI:", r["loi"])
        else:
            print(f"   action={r['action']} data={json.dumps(r['action_data'], ensure_ascii=False, default=str)[:300]}")
            print(f"   trả lời: {r['tra_loi'][:200]!r}")
            print(f"   hồ sơ: {json.dumps(r['ho_so'], ensure_ascii=False, default=str)[:300]}")
            print(f"   ngữ cảnh: {json.dumps(r['ngu_canh_sau'], ensure_ascii=False)[:200]}")


if __name__ == "__main__":
    asyncio.run(main())
