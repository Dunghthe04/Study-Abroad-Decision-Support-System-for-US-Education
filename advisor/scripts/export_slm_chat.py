"""Xuất dữ liệu SLM sang dạng hội thoại để fine-tune: system = prompt, user = JSON đầu vào, assistant = đáp án.
Đọc thẳng data/slm/du_lieu_slm.jsonl (tiếng Anh, đúng tên khóa như Excel) và dùng prompt tiếng Anh app/skills/slm_intents.md,
nên dữ liệu, prompt và đáp án dùng MỘT bộ tên. Skill hồ sơ không train (code điền khuôn).

Tin user là JSON viết liền {"question", "profile", "pending_question", ["context"]}; đáp án là {"intents":[{"type","span","entities"}]}
với khóa trong entities theo một thứ tự cố định, để model học nhất quán. Chỉ xuất train và val, tập test không bao giờ đem đi train.
Lúc chạy thật phải dựng tin user đúng như hàm user_message ở đây và dùng đúng prompt này.

Chạy trong thư mục advisor, sau khi đã chạy scripts.build_slm_jsonl:
  python -m scripts.export_slm_chat
Ra data/slm/finetune/train.jsonl và val.jsonl, mỗi dòng {"messages": [system, user, assistant]}. Mang 2 file lên Colab.
"""

import argparse
import json
import sys
from typing import Any

from app.schemas.slm import PROFILE_KEYS
from app.services.skill_call import SKILLS_DIR, load_skill
from scripts.build_slm_jsonl import DEFAULT_OUT, profile_text

# Prompt tách ý dùng để huấn luyện (tiếng Anh). Chạy thật phải dùng đúng prompt này
TRAIN_PROMPT = "slm_intents"
PROMPT_PATH = SKILLS_DIR / f"{TRAIN_PROMPT}.md"  # file prompt dùng để huấn luyện (tests đọc để kiểm nội dung)
OUT_DIR = DEFAULT_OUT.parent / "finetune"
SPLITS = ("train", "val")
# Khóa trong entities luôn viết theo một thứ tự cố định (khóa hồ sơ, rồi các khóa còn lại) để model học nhất quán.
# Khóa lạ (nếu có) viết sau cùng theo thứ tự gặp
ENTITY_ORDER = [
    *PROFILE_KEYS, "tasks", "schools", "school", "fields", "filters", "sort", "mode", "changes", "factor", "target_group",
    "service", "city", "centers", "subtype", "follow_up",
]  # fmt: skip


def ordered_entities(entities: dict[str, Any]) -> dict[str, Any]:
    known = {key: entities[key] for key in ENTITY_ORDER if key in entities}
    return {**known, **{key: value for key, value in entities.items() if key not in known}}


# Đáp án viết liền như model sinh ra: {"intents": [{type, span, entities}, ...]}
def answer_text(output: dict[str, Any]) -> str:
    intents = [
        {"type": y["type"], "span": y["span"], "entities": ordered_entities(y["entities"])}
        for y in output["intents"]
    ]
    return json.dumps({"intents": intents}, ensure_ascii=False, separators=(",", ":"))


# Đầu vào của SLM: JSON viết liền, hồ sơ thành chữ (giá trị danh sách nối bằng "; "), context chỉ có khi có
def user_message(data: dict[str, Any]) -> str:
    user: dict[str, Any] = {
        "question": data["question"],
        "profile": profile_text(data["profile"]) if data.get("profile") else None,
        "pending_question": data["pending_question"],
    }
    if data.get("context"):
        user["context"] = data["context"]
    return json.dumps(user, ensure_ascii=False, separators=(",", ":"))


# Một dòng dữ liệu thành một hội thoại: system = prompt, user = đầu vào, assistant = đáp án. Mỗi hội thoại là một dòng trong
# data/slm/finetune/train.jsonl, dạng {"messages": [{"role": "system", ...}, {"role": "user", ...}, {"role": "assistant", ...}]}
def to_chat(record: dict[str, Any], prompt: str) -> dict[str, Any]:
    return {
        "messages": [
            {"role": "system", "content": prompt},
            {"role": "user", "content": user_message(record["input"])},
            {"role": "assistant", "content": answer_text(record["output"])},
        ]
    }


# Câu trùng chữ với một câu của tập test thì không xuất (vd câu mẫu v5 lặp lại với hồ sơ khác),
# để model không học thuộc đáp án của câu test rồi làm điểm test cao giả
def chat_rows(records: list[dict[str, Any]], prompt: str, split: str) -> list[dict[str, Any]]:
    test_questions = {r["input"]["question"].lower() for r in records if r["split"] == "test"}
    return [
        to_chat(record, prompt)
        for record in records
        if record["split"] == split and record["input"]["question"].lower() not in test_questions
    ]


def main() -> None:
    sys.stdout.reconfigure(encoding="utf-8")
    parser = argparse.ArgumentParser(
        description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter
    )
    parser.add_argument("--prompt", default=TRAIN_PROMPT, help="tên prompt trong app/skills (không có .md)")
    args = parser.parse_args()
    prompt, version = load_skill(SKILLS_DIR / f"{args.prompt}.md")
    records = [
        json.loads(line) for line in DEFAULT_OUT.read_text(encoding="utf-8").splitlines() if line.strip()
    ]
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    for split in SPLITS:
        rows = chat_rows(records, prompt, split)
        out = OUT_DIR / f"{split}.jsonl"
        out.write_text("".join(json.dumps(row, ensure_ascii=False) + "\n" for row in rows), encoding="utf-8")
        print(f"{split}: {len(rows)} hội thoại -> {out}")
    print(f"Prompt {args.prompt} phiên bản {version}")


if __name__ == "__main__":
    main()
