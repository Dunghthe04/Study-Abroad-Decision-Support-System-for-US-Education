"""Xuất dữ liệu SLM sang dạng hội thoại để fine-tune: system = prompt phân loại, user = JSON đầu vào, assistant = đáp án.

Ghép bằng đúng build_messages mà OllamaSlm dùng lúc chạy, và viết đáp án theo một thứ tự khóa cố định (type, doan,
thuc_the), nên model học đúng thứ nó sẽ nhận và sẽ sinh. Chỉ xuất train và val, tập test không bao giờ đem đi train.

Chạy trong thư mục advisor, sau khi đã chạy scripts.build_slm_jsonl:
  python -m scripts.export_slm_chat
Ra data/slm/finetune/train.jsonl và val.jsonl, mỗi dòng {"messages": [system, user, assistant]}. Mang 2 file lên Colab.
"""

import json
import sys
from typing import Any

from app.schemas.slm import SLM_TIN_SCHEMA
from app.services.skill_call import SKILLS_DIR, load_skill
from app.services.slm_classifier import build_messages
from scripts.build_slm_jsonl import DEFAULT_OUT

# Prompt của hợp đồng danh sách ý. OllamaSlm vẫn dùng slm_phan_loai.md cho đến khi bộ phân loại được đổi sang cac_y
PROMPT_PATH = SKILLS_DIR / "slm_phan_loai_cac_y.md"
OUT_DIR = DEFAULT_OUT.parent / "finetune"
SPLITS = ("train", "val")
# Khóa trong thuc_the của mỗi loại ý luôn viết theo một thứ tự cố định (khóa hồ sơ rồi khóa riêng của loại, đúng thứ tự
# khai báo trong khuôn) để model học nhất quán. Lưu ý: khi nhận khuôn, Ollama ép khóa theo bảng chữ cái chứ không theo
# thứ tự này, nên model đã fine-tune chạy với slm_format="json" (xem output_format trong slm_classifier.py)
_KEY_ORDER = {
    branch["properties"]["type"]["const"]: list(branch["properties"]["thuc_the"]["properties"])
    for branch in SLM_TIN_SCHEMA["properties"]["cac_y"]["items"]["anyOf"]
}


# Đáp án viết liền như model sinh ra: {"cac_y": [{type, doan, thuc_the}, ...]}, khóa trong thuc_the theo thứ tự của khuôn
def answer_text(output: dict[str, Any]) -> str:
    cac_y = []
    for y in output["cac_y"]:
        thuc_the = {key: y["thuc_the"][key] for key in _KEY_ORDER[y["type"]] if key in y["thuc_the"]}
        cac_y.append({"type": y["type"], "doan": y["doan"], "thuc_the": thuc_the})
    return json.dumps({"cac_y": cac_y}, ensure_ascii=False, separators=(",", ":"))


# Một dòng dữ liệu thành một hội thoại: 2 tin như lúc chạy thật, cộng tin đáp án. Mỗi hội thoại là một dòng trong
# data/slm/finetune/train.jsonl, dạng {"messages": [{"role": "system", ...}, {"role": "user", ...}, {"role": "assistant", ...}]}
def to_chat(record: dict[str, Any], prompt: str) -> dict[str, Any]:
    data = record["input"]
    messages = build_messages(prompt, data["cau_hoi"], data["ho_so"], data["cau_he_thong_dang_hoi"])
    return {"messages": [*messages, {"role": "assistant", "content": answer_text(record["output"])}]}


# Câu trùng chữ với một câu của tập test thì không xuất (vd câu mẫu v5 lặp lại với hồ sơ khác),
# để model không học thuộc đáp án của câu test rồi làm điểm test cao giả
def chat_rows(records: list[dict[str, Any]], prompt: str, split: str) -> list[dict[str, Any]]:
    test_questions = {r["input"]["cau_hoi"].lower() for r in records if r["split"] == "test"}
    return [
        to_chat(record, prompt)
        for record in records
        if record["split"] == split and record["input"]["cau_hoi"].lower() not in test_questions
    ]


def main() -> None:
    sys.stdout.reconfigure(encoding="utf-8")
    prompt, version = load_skill(PROMPT_PATH)
    records = [json.loads(line) for line in DEFAULT_OUT.read_text(encoding="utf-8").splitlines()]
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    for split in SPLITS:
        rows = chat_rows(records, prompt, split)
        out = OUT_DIR / f"{split}.jsonl"
        out.write_text("".join(json.dumps(row, ensure_ascii=False) + "\n" for row in rows), encoding="utf-8")
        print(f"{split}: {len(rows)} hội thoại -> {out}")
    print(f"Prompt phiên bản {version}")


if __name__ == "__main__":
    main()
