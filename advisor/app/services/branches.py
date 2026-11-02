"""Các nhánh xử lý tin nhắn theo loại câu hỏi SLM (kiến trúc mới: không còn chuỗi bước B0 đến B6).

SLM tách mỗi tin thành các ý, mỗi ý thuộc một trong 6 loại, mỗi loại là một nhánh. ChatFlow gọi nhánh của
từng ý. Nhánh nào chưa làm xong thì tạm dùng skill cũ bên trong (lớp tên Legacy...);
làm xong thì thay lớp đó trong build_branches, không phải sửa ChatFlow.
"""

from app.schemas.chat import ChatState
from app.schemas.slm import SlmTinResult
from app.services.chat_flow import (
    NEED_PROFILE,
    NOT_READY,
    OPENING_QUESTIONS,
    Branch,
    SkillHandler,
    SkillOutput,
    ask_major,
    combine,
    start_intake,
)
from app.services.skill_rules import REQUIRED_B3, REQUIRED_BY_SKILL, missing_fields
from app.services.skills.profile_handler import ChatProfileHandler


# Nhánh dùng nguyên một skill có sẵn (kiến thức, trung tâm)
class SkillBranch:
    def __init__(self, skill: SkillHandler) -> None:
        self._skill = skill

    async def run(self, state: ChatState, message: str, slm: SlmTinResult) -> SkillOutput:
        return await self._skill.run(state, message)


# Nhánh chưa làm: báo đang hoàn thiện (kịch bản; kiến thức khi chưa có DB; trung tâm khi chưa có backend)
class NotReadyBranch:
    async def run(self, state: ChatState, message: str, slm: SlmTinResult) -> SkillOutput:
        return SkillOutput(NOT_READY)


# Câu trả lời cố định theo loai_con, không gọi model.
# tam_biet gồm cả chào, cảm ơn, tạm biệt nên dùng một câu hợp với cả ba
OUT_OF_SCOPE = {
    "ngoai_le": "Mình chỉ hỗ trợ tư vấn du học Mỹ thôi nhé.",
    "nhay_cam": "Mình không hỗ trợ việc này. Hồ sơ du học cần trung thực, mình giúp bạn chuẩn bị đúng cách nhé.",
    "tam_biet": "Rất vui được trò chuyện với bạn! Bạn cần mình giúp gì về du học Mỹ không?",
    "mo_ho": "Mình chưa rõ ý bạn lắm. Bạn nói cụ thể hơn giúp mình nhé.",
}


# Ngoài phạm vi: trả câu cố định. Câu hồ sơ đang chờ (nếu có) do ChatFlow nhắc lại như mọi câu hỏi xen ngang
class OutOfScopeBranch:
    async def run(self, state: ChatState, message: str, slm: SlmTinResult) -> SkillOutput:
        kind = slm.cac_y[0].thuc_the.get("loai_con", "mo_ho")
        return SkillOutput(OUT_OF_SCOPE.get(kind, OUT_OF_SCOPE["mo_ho"]))


# TẠM, phần của bạn: tư vấn theo hồ sơ bằng skill cũ (đánh giá, lộ trình, giải thích một trường).
# Làm xong nhánh mới (SLM sinh skill cá nhân hóa, Gemini trả lời) thì thay lớp này
class LegacyAdviceBranch:
    def __init__(self, skills: dict[str, SkillHandler]) -> None:
        self._skills = skills

    async def run(self, state: ChatState, message: str, slm: SlmTinResult) -> SkillOutput:
        if not state.has_results:
            name = "skill_danh_gia"
        elif slm.cac_y[0].thuc_the.get("truong"):
            name = "skill_explain"
        else:
            name = "skill_lo_trinh"
        # Thiếu hồ sơ: hỏi tiếp hồ sơ (thiếu ngành thì hiện ô chọn ngành) thay vì tư vấn khi chưa đủ thông tin.
        # Đang hỏi hồ sơ dở (có câu đang chờ) thì chỉ báo cần thêm hồ sơ, ChatFlow nhắc lại câu đang chờ
        missing = missing_fields(state.profile, REQUIRED_BY_SKILL[name]) if name in REQUIRED_BY_SKILL else []
        if missing:
            if state.question_id and state.question_id not in OPENING_QUESTIONS:
                return SkillOutput(NEED_PROFILE)
            return ask_major(state) if missing[0] == "major" else start_intake(state, REQUIRED_BY_SKILL[name])
        return await self._skills[name].run(state, message)


# Trả lời câu hồ sơ đang chờ hoặc cập nhật hồ sơ. Hồ sơ vừa đủ trong lượt này thì tư vấn luôn,
# không bắt học sinh nhắn thêm một lần nữa mới thấy kết quả
class ProfileBranch:
    def __init__(self, handler: ChatProfileHandler, advice: Branch) -> None:
        self._handler = handler
        self._advice = advice

    async def run(self, state: ChatState, message: str, slm: SlmTinResult) -> SkillOutput:
        was_ready = self._handler.ready(state, REQUIRED_B3)
        output = await self._handler.collect(state, message, slm)
        if not was_ready and self._handler.ready(state, REQUIRED_B3):
            return combine(output, await self._advice.run(state, message, slm))
        return output


# Bảng nhánh theo loại câu hỏi SLM. skills: các skill cũ do skills.build_handlers tạo
def build_branches(skills: dict[str, SkillHandler]) -> dict[str, Branch]:
    advice = LegacyAdviceBranch(skills)
    return {
        "mac_dinh": ProfileBranch(ChatProfileHandler(skills), advice),
        "ho_so": advice,
        "kich_ban": NotReadyBranch(),
        "kien_thuc": SkillBranch(skills["skill_tra_loi"]) if "skill_tra_loi" in skills else NotReadyBranch(),
        "trung_tam": (
            SkillBranch(skills["skill_chuyen_trung_tam"])
            if "skill_chuyen_trung_tam" in skills
            else NotReadyBranch()
        ),
        "ngoai_pham_vi": OutOfScopeBranch(),
    }
