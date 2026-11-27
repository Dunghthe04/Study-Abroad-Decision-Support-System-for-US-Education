"""Chạy các câu trong cau_hoi_tu_nhien.py qua SLM, đúng cách chat đang gọi, rồi ghi ra Excel để người duyệt.

Chạy trong thư mục advisor (cần Ollama):  .venv/Scripts/python.exe -m slm.eval.thu_cau_tu_nhien
Không gọi Claude, không tốn tiền API.
"""

import asyncio
import json
from collections import defaultdict
from difflib import SequenceMatcher
from pathlib import Path

import httpx
from openpyxl import Workbook
from openpyxl.styles import Alignment, Font, PatternFill

from app.core.config import get_settings
from app.services.chat_flow import QUESTIONS
from app.services.ollama import OllamaClient
from app.services.slm_classifier import OllamaSlm, output_format
from slm.eval.cau_hoi_tu_nhien import TRA_LOI, TU_DO

OUT = Path("slm/results/thu_tu_nhien")
# Hồ sơ mẫu cho ngữ cảnh "đã có hồ sơ"
HO_SO = {
    "current_grade": "lớp 11",
    "major": "Computer Science",
    "gpa": "8.5",
    "english": "IELTS 6.5",
    "budget": "35000",
}


def key(intent) -> str:
    e = intent.entities
    detail = e.get("mode") or e.get("subtype") or "+".join(sorted(e.get("tasks") or []))
    return f"{intent.type}:{detail}" if detail else intent.type


def types(answer: list[str]) -> list[str]:
    return sorted(part.split(":")[0] for part in answer)


def grade(predicted: list[str], accepted: list[str]) -> str:
    options = [[p.strip() for p in a.split("|")] for a in accepted]
    if any(sorted(predicted) == sorted(o) for o in options):
        return "đúng"
    if any(types(predicted) == types(o) for o in options):
        return "đúng loại, sai chi tiết"
    return "SAI LOẠI"


def cases():
    for group, question, accepted in TU_DO:
        yield group, "vừa mở chat", question, None, QUESTIONS["B0.1"], accepted
        yield group, "đã có hồ sơ", question, HO_SO, "", accepted
    for group, qid, question, accepted in TRA_LOI:
        yield group, f"đang chờ {qid}", question, None, QUESTIONS[qid], accepted


def train_questions() -> list[tuple[str, str]]:
    rows = []
    for line in Path("slm/data/finetune/train.jsonl").read_text(encoding="utf-8").splitlines():
        if line.strip():
            messages = json.loads(line)["messages"]
            question = json.loads(messages[1]["content"])["question"]
            labels = " | ".join(y["type"] for y in json.loads(messages[2]["content"])["intents"])
            rows.append((question, labels))
    return rows


def nearest(question: str, train: list[tuple[str, str]]) -> tuple[float, str, str]:
    """Câu train giống mặt chữ nhất (chỉ so mặt chữ, không so nghĩa)."""
    matcher = SequenceMatcher(autojunk=False)
    matcher.set_seq2(question.lower())
    best = (0.0, "", "")
    for text, labels in train:
        matcher.set_seq1(text.lower())
        if matcher.real_quick_ratio() > best[0] and matcher.quick_ratio() > best[0]:
            score = matcher.ratio()
            if score > best[0]:
                best = (score, text, labels)
    return best


async def main() -> None:
    s = get_settings()
    async with httpx.AsyncClient(
        base_url=s.slm_base_url or s.ollama_base_url, timeout=s.llm_timeout_seconds
    ) as http:
        client = OllamaClient(
            http,
            s.slm_model,
            num_ctx=s.llm_num_ctx,
            num_gpu=s.llm_num_gpu,
            seed=s.llm_seed,
            keep_alive=s.llm_keep_alive,
            num_predict=s.slm_num_predict,
        )
        slm = OllamaSlm(client, output_format(s.slm_format), s.slm_use_context, s.slm_prompt)
        rows = []
        for group, situation, question, profile, pending, accepted in cases():
            result = await slm.read(question, profile, pending)
            predicted = [] if result.fallback else [key(y) for y in result.intents]
            verdict = "SAI KHUÔN" if result.fallback else grade(predicted, accepted)
            rows.append(
                {
                    "nhom": group,
                    "ngu_canh": situation,
                    "cau": question,
                    "dap_an": accepted,
                    "slm": predicted,
                    "ket_qua": verdict,
                    "tin_cay": round(result.confidence, 3),
                    "entities": [y.entities for y in result.intents],
                }
            )

    train = train_questions()
    for row in rows:
        score, text, labels = nearest(row["cau"], train)
        row["train_gan_nhat"], row["nhan_train"], row["giong"] = text, labels, round(score, 2)

    OUT.mkdir(parents=True, exist_ok=True)
    (OUT / "ket_qua.jsonl").write_text(
        "".join(json.dumps(r, ensure_ascii=False) + "\n" for r in rows), encoding="utf-8"
    )
    write_excel(rows, s.slm_model)

    totals = defaultdict(lambda: [0, 0, 0])
    for r in rows:
        for name in (
            "TẤT CẢ",
            "ngữ cảnh: " + r["ngu_canh"].split(" B")[0].split(" D")[0],
            "nhóm: " + r["nhom"],
        ):
            t = totals[name]
            t[0] += 1
            t[1] += r["ket_qua"] == "đúng"
            t[2] += r["ket_qua"] in ("đúng", "đúng loại, sai chi tiết")
    print("model", s.slm_model)
    for name, (n, exact, kind) in sorted(totals.items()):
        print(f"{name:40} n={n:3}  đúng loại {kind:3}  đúng cả chi tiết {exact:3}")
    print("\n--- các câu chưa đúng ---")
    for r in rows:
        if r["ket_qua"] != "đúng":
            print(
                f"[{r['ket_qua']}] ({r['ngu_canh']}) {r['cau']}\n    đáp án: {r['dap_an']}  SLM: {r['slm']}  tin cậy {r['tin_cay']}"
            )


def write_excel(rows: list[dict], model: str, path: Path = OUT / "thu_cau_tu_nhien.xlsx") -> None:
    book = Workbook()
    sheet = book.active
    sheet.title = "Ket qua"
    headers = [
        "Nhóm",
        "Ngữ cảnh",
        "Câu hỏi",
        "Đáp án Claude soạn",
        f"{model} gán",
        "Chấm tự động",
        "Độ tin cậy",
        "Entities SLM tách",
        "Câu train giống mặt chữ nhất",
        "Nhãn câu train đó",
        "Độ giống (0-1)",
        "BẠN DUYỆT (đúng / sai / mơ hồ)",
        "Ghi chú",
    ]
    sheet.append(headers)
    order = {"SAI KHUÔN": 0, "SAI LOẠI": 1, "đúng loại, sai chi tiết": 2, "đúng": 3}
    for r in sorted(rows, key=lambda r: order[r["ket_qua"]]):
        sheet.append(
            [
                r["nhom"],
                r["ngu_canh"],
                r["cau"],
                "\nhoặc ".join(r["dap_an"]),
                " | ".join(r["slm"]),
                r["ket_qua"],
                r["tin_cay"],
                json.dumps(r["entities"], ensure_ascii=False),
                r["train_gan_nhat"],
                r["nhan_train"],
                r["giong"],
                None,
                None,
            ]
        )
    for column, width in zip(
        "ABCDEFGHIJKLM", [16, 16, 48, 38, 34, 22, 11, 40, 42, 24, 11, 22, 28], strict=True
    ):
        sheet.column_dimensions[column].width = width
    for line in sheet.iter_rows():
        for cell in line:
            cell.font = Font(name="Arial", size=10, bold=cell.row == 1)
            cell.alignment = Alignment(wrap_text=True, vertical="top")
    for cell in sheet[1]:
        cell.fill = PatternFill("solid", fgColor="D9E2F3")
    for row in range(2, sheet.max_row + 1):
        sheet.cell(row, 12).fill = PatternFill("solid", fgColor="FFF2CC")
    sheet.freeze_panes = "D2"
    sheet.auto_filter.ref = sheet.dimensions
    book.save(path)


if __name__ == "__main__":
    asyncio.run(main())
