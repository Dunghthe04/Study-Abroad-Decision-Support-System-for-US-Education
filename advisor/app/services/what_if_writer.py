"""Lời cho câu "nếu đổi X" khi X không thử được: code chia nhóm mục (lọc trường, không ảnh hưởng), Claude viết lời.

Code kiểm tra lời Claude: phải nêu đủ tên mục, không nói đã thử, không có số lạ. Sai thì gọi lại một lần,
vẫn sai thì trả None để nhánh kịch bản dùng câu cố định. Nút sửa hồ sơ do code gắn, Claude không đụng tới.
"""

from pathlib import Path
from typing import Any

from app.schemas.what_if_note import WhatIfNoteResult, what_if_note_schema
from app.services.scenario import LABELS, SUPPORTED_TEXT, WhatIf
from app.services.scenario_writer import numbers
from app.services.skill_call import SKILLS_DIR, SkillCall

SKILL_PATH = Path(SKILLS_DIR) / "skill_neu_doi.md"
MAX_VALUE = 100  # giá trị học sinh nói, cắt ngắn trước khi gửi Claude
# Cụm khẳng định đã chạy thử: hệ thống chưa chạy gì nên lời có cụm này là sai
CLAIMS = ("đã thử", "thử xong", "đã chạy", "kết quả mới")


def value_text(value: Any) -> str:
    text = ", ".join(map(str, value)) if isinstance(value, list) else str(value or "")
    return text.strip()[:MAX_VALUE]


# Gom dữ liệu gửi Claude: tên mục theo nhóm và giá trị học sinh muốn đổi. Không gửi nguyên tin nhắn của học sinh
def build_user(what_if: WhatIf, changes: list[dict[str, Any]]) -> dict[str, Any]:
    grouped = what_if.filters + what_if.no_effect
    values: dict[str, str] = {}
    for change in changes:
        key = change.get("factor")
        if key in grouped and LABELS[key] not in values and (text := value_text(change.get("value"))):
            values[LABELS[key]] = text
    named = {LABELS[key] for key in grouped}
    return {
        "filters": [LABELS[key] for key in what_if.filters],
        "noEffect": [LABELS[key] for key in what_if.no_effect],
        "others": [label for label in what_if.unsupported if label not in named],
        "values": values,
        "canTry": SUPPORTED_TEXT,
    }


# Lời hợp lệ: nêu đủ tên mục lọc trường và mục không ảnh hưởng, không nói đã thử, chỉ nhắc số có trong dữ liệu.
# Sai thì ném ValueError để gọi lại một lần
def check_answer(raw: dict[str, Any], user: dict[str, Any]) -> str:
    answer = WhatIfNoteResult.model_validate(raw).answer.strip()
    lower = answer.lower()

    missing = [label for label in user["filters"] + user["noEffect"] if label.lower() not in lower]
    if missing:
        raise ValueError(f"lời thiếu tên mục: {missing}")
    claims = [claim for claim in CLAIMS if claim in lower]
    if claims:
        raise ValueError(f"lời nói đã thử: {claims}")

    texts = [*user["values"].values(), *user["filters"], *user["noEffect"], *user["others"], user["canTry"]]
    extra = sorted(set(numbers(answer)) - {n for text in texts for n in numbers(text)})
    if extra:
        raise ValueError(f"lời nhắc số không có trong dữ liệu: {extra}")
    return answer


class WhatIfWriter:
    def __init__(self, llm: Any, model: str, skill_path: Path = SKILL_PATH) -> None:
        self._call = SkillCall(llm, model, skill_path)
        self.skill_version = self._call.skill_version

    # Trả lời đã kiểm tra, hoặc None nếu Claude sai cả hai lần
    async def write(self, what_if: WhatIf, changes: list[dict[str, Any]]) -> str | None:
        user = build_user(what_if, changes)
        return await self._call.run(user, what_if_note_schema(), lambda raw: check_answer(raw, user))
