"""Xuất dữ liệu SLM sang dạng hội thoại để fine-tune: system = prompt, user = JSON đầu vào, assistant = đáp án.
Hai việc trong cùng một file, phân biệt bằng prompt: tách ý (prompt slm_phan_loai_cac_y) và sinh skill hồ sơ (prompt
slm_sinh_skill_ho_so). Skill sinh từ cột skill_md của dòng có skill; vài dòng skill giữ riêng làm tập kiểm skill,
và skill train được lặp lại vì chỉ có vài chục mẫu mà mỗi mẫu dài.

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
from app.services.slm_classifier import PROMPT_PATH, build_messages
from scripts.build_slm_jsonl import DEFAULT_OUT, profile_text

SKILL_PROMPT_PATH = SKILLS_DIR / "slm_sinh_skill_ho_so.md"
SKILL_REPEAT = 3  # mỗi skill train xuất 3 lần, để model nhỏ học được khuôn dài từ ít mẫu
SKILL_HOLDOUT_EVERY = 8  # cứ 8 skill train giữ lại 1 để kiểm skill, không đem đi train
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
    messages = build_messages(
        prompt, data["cau_hoi"], data["ho_so"], data["cau_he_thong_dang_hoi"], data.get("dang_noi_ve")
    )
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


# Đầu vào của việc sinh skill: đúng các trường của SkillRequest (loại, câu hỏi, hồ sơ đã đủ, dòng code tính sẵn)
def skill_request(record: dict[str, Any]) -> dict[str, Any]:
    return {
        "loai": record["type"],
        "cau_hoi": record["input"]["cau_hoi"],
        "ho_so": profile_text(record["input"]["ho_so"]),
        "tinh_san": record.get("tinh_san") or {},
    }


# Hai tin như lúc chạy thật: prompt sinh skill và JSON đầu vào viết liền
def skill_messages(prompt: str, request: dict[str, Any]) -> list[dict[str, str]]:
    return [
        {"role": "system", "content": prompt},
        {"role": "user", "content": json.dumps(request, ensure_ascii=False, separators=(",", ":"))},
    ]


# Dòng skill của một tập. val gồm các dòng val và các dòng train được giữ lại; train lặp SKILL_REPEAT lần
def skill_rows(records: list[dict[str, Any]], prompt: str, split: str) -> list[dict[str, Any]]:
    skills = sorted((r for r in records if r["skill_md"] and r["type"] == "ho_so"), key=lambda r: r["id"])
    train = [r for r in skills if r["split"] == "train"]
    holdout = train[::SKILL_HOLDOUT_EVERY]
    chosen = {
        "train": [r for r in train if r not in holdout],
        "val": [r for r in skills if r["split"] == "val"] + holdout,
    }[split]
    repeat = SKILL_REPEAT if split == "train" else 1
    return [
        {
            "messages": [
                *skill_messages(prompt, skill_request(r)),
                {"role": "assistant", "content": r["skill_md"].strip()},
            ]
        }
        for r in chosen
        for _ in range(repeat)
    ]


def main() -> None:
    sys.stdout.reconfigure(encoding="utf-8")
    prompt, version = load_skill(PROMPT_PATH)
    skill_prompt, skill_version = load_skill(SKILL_PROMPT_PATH)
    records = [json.loads(line) for line in DEFAULT_OUT.read_text(encoding="utf-8").splitlines()]
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    for split in SPLITS:
        rows = chat_rows(records, prompt, split) + skill_rows(records, skill_prompt, split)
        out = OUT_DIR / f"{split}.jsonl"
        out.write_text("".join(json.dumps(row, ensure_ascii=False) + "\n" for row in rows), encoding="utf-8")
        print(f"{split}: {len(rows)} hội thoại -> {out}")
    print(f"Prompt tách ý phiên bản {version}, prompt sinh skill phiên bản {skill_version}")


if __name__ == "__main__":
    main()
