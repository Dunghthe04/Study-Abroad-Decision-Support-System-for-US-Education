"""skill_nhu_cau (B0): Qwen đọc câu đầu tiên ra need, branch và các thông tin đã nói sẵn, code ghi state.

Thông tin đã nói sẵn là chữ thô ("8.5", "800 triệu"), nên được đọc lại bằng skill_bac_hoc và skill_ho_so
để ra giá trị chuẩn đã kiểm tra, rồi ghi status="said_in_B0": B1, B2 không hỏi lại, X1 cho học sinh xác nhận.
"""

from dataclasses import dataclass, field
from pathlib import Path
from typing import Any

from app.schemas.chat import ChatState, QuickReply
from app.services.chat_flow import SkillOutput, ask, go_to, start_intake
from app.services.skill_call import SKILLS_DIR, SkillCall
from app.services.skill_rules import REQUIRED_B3, missing_fields
from app.services.skills import skill_bac_hoc, skill_ho_so
from app.services.skills.info_to_field import to_fields
from app.services.skills.skill_bac_hoc import BacHocReader
from app.services.skills.skill_ho_so import MAX_ASK, HoSoReader

SKILL_PATH = SKILLS_DIR / "skill_nhu_cau.md"
NEEDS = ("profile_advice", "knowledge", "service", "unclear")
BRANCHES = ("DH",)
# Hỏi kiến thức → B5 (skill_tra_loi), hỏi dịch vụ → B6 (skill_chuyen_trung_tam)
NEED_STEP = {"knowledge": "B5", "service": "B6"}
# Nút cho câu B0.2
B0_REPLIES = [
    QuickReply(label="Tìm trường theo hồ sơ", value="Em muốn tìm trường theo hồ sơ của em"),
    QuickReply(label="Em có câu hỏi", value="Em có câu hỏi về du học Mỹ"),
]
# Thông tin nào cần đọc lại bằng skill nào
BAC_HOC_FIELDS = set(skill_bac_hoc.READ_FIELDS)
HO_SO_FIELDS = {name for names in skill_ho_so.QUESTION_FIELDS.values() for name in names}

# Khuôn JSON Ollama phải trả
SCHEMA = {
    "type": "object",
    "required": ["need", "branch", "info_given"],
    "properties": {
        "need": {"enum": list(NEEDS)},
        "branch": {"enum": [*BRANCHES, None]},
        "info_given": {"type": "object", "additionalProperties": {"type": "string"}},
    },
}


# Kết quả đọc: info đã đổi sang khóa chuẩn (gpa, english...), giá trị vẫn là chữ học sinh viết
@dataclass
class NhuCauRead:
    need: str
    branch: str | None = None
    info: dict[str, Any] = field(default_factory=dict)
    ai_used: bool = False


# Kiểm tra JSON Qwen trả, sai thì ném ValueError để SkillCall gọi lại
def check_read(raw: dict[str, Any]) -> NhuCauRead:
    need = raw.get("need")
    if need not in NEEDS:
        raise ValueError(f"need lạ: {need}")
    branch = raw.get("branch")
    if branch is not None and branch not in BRANCHES:
        raise ValueError(f"branch lạ: {branch}")
    info = raw.get("info_given") or {}
    if not isinstance(info, dict):
        raise ValueError("info_given phải là object")
    return NhuCauRead(need=need, branch=branch, info=to_fields(info), ai_used=True)


# Qwen lỗi 2 lần: coi như chưa rõ nhu cầu, không đoán, không ghi thông tin nào
def fallback() -> NhuCauRead:
    return NhuCauRead(need="unclear")


# Gọi Qwen đọc câu đầu tiên
class NhuCauReader:
    def __init__(self, llm: Any, model: str, skill_path: Path = SKILL_PATH) -> None:
        self._call = SkillCall(llm, model, skill_path)
        self.skill_version = self._call.skill_version

    async def read(self, message: str) -> NhuCauRead:
        result = await self._call.run({"message": message}, SCHEMA, check_read)
        return result if result is not None else fallback()


class NhuCauHandler:
    def __init__(self, llm: Any, model: str) -> None:
        self._reader = NhuCauReader(llm, model)
        self._bac_hoc = BacHocReader(llm, model)
        self._ho_so = HoSoReader(llm, model)

    async def run(self, state: ChatState, message: str) -> SkillOutput:
        read = await self._reader.read(message)
        state.need = read.need
        state.branch = read.branch or state.branch

        # Chưa rõ nhu cầu thì hỏi B0.2 (tối đa 2 lần); vẫn chưa rõ thì coi như muốn tư vấn hồ sơ
        if read.need == "unclear" and state.asked.get("B0.2", 0) < MAX_ASK:
            return SkillOutput(ask(state, "B0.2"), quick_replies=B0_REPLIES)
        # Hỏi kiến thức, dịch vụ: chuyển bước, xong thì quay lại B1 thu hồ sơ.
        # Câu trả lời do skill_tra_loi / skill_chuyen_trung_tam viết (chưa có thì báo đang hoàn thiện)
        if read.need in NEED_STEP:
            go_to(state, NEED_STEP[read.need])
            state.return_to = "B1"
            return SkillOutput("")

        await self._save_info(state, message, set(read.info))
        # Muốn tư vấn hồ sơ mà còn thiếu mục bắt buộc: mời chọn trả lời trong chat hay điền form (giống bước đánh giá)
        if state.intake_mode != "chat" and missing_fields(state.profile, REQUIRED_B3):
            return start_intake(state)
        go_to(state, "B1")
        return skill_bac_hoc.ask_next(state)

    # Đọc lại câu đầu bằng skill có khuôn giá trị chuẩn, chỉ khi câu đó có thông tin của skill ấy
    async def _save_info(self, state: ChatState, message: str, said: set[str]) -> None:
        if said & BAC_HOC_FIELDS:
            bac_hoc = await self._bac_hoc.read("B1.1", message)
            skill_bac_hoc.save_read(state, bac_hoc, status="said_in_B0", keep_existing=True)
        if said & HO_SO_FIELDS:
            known = {name: f.value for name, f in state.profile.items() if f.value is not None}
            ho_so = await self._ho_so.read(None, message, known, state.branch)
            skill_ho_so.save_read(state, ho_so, status="said_in_B0", keep_existing=True)
