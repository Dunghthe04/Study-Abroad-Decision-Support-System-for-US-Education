"""Các nhánh xử lý tin nhắn theo loại câu hỏi SLM (kiến trúc mới: không còn chuỗi bước B0 đến B6).

SLM tách mỗi tin thành các ý, mỗi ý thuộc một trong 6 loại, mỗi loại là một nhánh. ChatFlow gọi nhánh của
từng ý. Nhánh nào chưa làm xong thì tạm dùng skill cũ bên trong (lớp tên Legacy...);
làm xong thì thay lớp đó trong build_branches, không phải sửa ChatFlow.
"""

from app.schemas.chat import ChatState, QuickReply, Widget
from app.schemas.slm import SlmTinResult, SlmY
from app.services.advice_writer import AdviceWriter
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
from app.services.goal_target import parse_target, plain
from app.services.scenario import SUPPORTED_TEXT, parse_what_if
from app.services.skill_generator import SkillGenerator
from app.services.skill_input import skill_request_of
from app.services.skill_rules import REQUIRED_B3, REQUIRED_B4, REQUIRED_BY_SKILL, missing_fields
from app.services.skills.profile_handler import ChatProfileHandler
from app.services.skills.validators import AskAgain


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


# Chưa có danh sách gợi ý thì không có gì để thử kịch bản
NEED_RESULTS = "Bạn cần có danh sách trường gợi ý trước thì mình mới thử kịch bản được. Bạn muốn mình gợi ý trường trước không?"
NOTHING_TO_TRY = f"Mình chưa thử được thay đổi này. Hiện mình thử được: {SUPPORTED_TEXT}."
ASK_SCHOOL = "Bạn muốn vào trường nào? Chọn hoặc gõ tên trường ở ô bên dưới nhé."


# Kịch bản: what_if thì SLM đã trích và chuẩn hóa giá trị, đây kiểm tra rồi giao .NET chạy simulate (action "simulate");
# goal_seeking thì giao .NET tìm trường và chạy goal-seek (action "goal_seek")
class ScenarioBranch:
    async def run(self, state: ChatState, message: str, slm: SlmTinResult) -> SkillOutput:
        entities = slm.cac_y[0].thuc_the
        if entities.get("che_do") == "goal_seeking":
            return self._goal_seek(entities)
        if entities.get("che_do") != "what_if":
            return SkillOutput(NOT_READY)
        if not state.has_results:
            return SkillOutput(NEED_RESULTS)
        try:
            what_if = parse_what_if(entities.get("thay_doi", []))
        except AskAgain as ask:
            return SkillOutput(ask.question)
        if not what_if.changes:
            return SkillOutput(NOTHING_TO_TRY)
        note = f" Mình chưa thử được: {', '.join(what_if.unsupported)}." if what_if.unsupported else ""
        return SkillOutput(
            f"Mình thử kịch bản: {what_if.label()}.{note}",
            action="simulate",
            action_data={"changes": what_if.changes, "unsupported": what_if.unsupported},
        )

    # "Cần gì để vào trường X": tên trường còn là chữ, .NET khớp với 249 trường rồi chạy goal-seek.
    # Không cần danh sách gợi ý có sẵn: goal-seek tính từ hồ sơ và dữ liệu trường
    @staticmethod
    def _goal_seek(entities: dict) -> SkillOutput:
        school = str(entities.get("truong") or "").strip()
        target = parse_target(entities.get("muc_tieu"))
        if not school:
            # Chưa nêu trường: hiện ô chọn trường có gợi ý, giữ nhóm đích để câu hỏi gửi lại đúng ý
            return SkillOutput(ASK_SCHOOL, widget=Widget(type="school", data={"target": target}))
        return SkillOutput(
            f"Mình xem giúp bạn cần gì để vào {school} nhé.",
            action="goal_seek",
            action_data={"school": school, "target": target},
        )


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
        if name in REQUIRED_BY_SKILL:
            asked = ask_missing_profile(state, slm.cac_y[0], REQUIRED_BY_SKILL[name])
            if asked is not None:
                return asked
        return await self._skills[name].run(state, message)


# Thiếu hồ sơ: hỏi tiếp hồ sơ (thiếu ngành thì hiện ô chọn ngành) thay vì tư vấn khi chưa đủ thông tin.
# Đang hỏi hồ sơ dở (có câu đang chờ) thì chỉ báo cần thêm hồ sơ, ChatFlow nhắc lại câu đang chờ.
# Đủ hồ sơ thì trả None để nhánh tư vấn làm tiếp
def ask_missing_profile(state: ChatState, y: SlmY, required: list[str]) -> SkillOutput | None:
    missing = missing_fields(state.profile, required)
    if not missing:
        return None
    # Nhớ câu tư vấn để trả lời ngay khi học sinh xác nhận hồ sơ (ProfileBranch). Chỉ giữ câu đầu tiên
    if state.pending_advice is None:
        state.pending_advice = y
    if state.question_id and state.question_id not in OPENING_QUESTIONS:
        return SkillOutput(NEED_PROFILE)
    return ask_major(state) if missing[0] == "major" else start_intake(state, required)


# Câu xin danh sách trường (đã bỏ dấu). SLM xếp các câu này vào ho_so; code giao .NET chạy gợi ý trường
# (pipeline có sẵn), không sinh skill
SCHOOL_LIST_WORDS = (
    "truong nao",
    "chon truong",
    "nhung truong",
    "danh sach truong",
    "goi y truong",
    "de xuat truong",
    "nop truong",
    "apply truong",
    "which universit",
    "which school",
    "which college",
    "universities should",
    "schools should",
    "colleges should",
    "recommend universit",
    "recommend school",
    "recommend college",
)
RECOMMENDING = "Mình lọc danh sách trường hợp với hồ sơ và ngành của bạn nhé."
# Hồ sơ vừa đủ mà học sinh chưa hỏi câu tư vấn nào (tin là "Đúng rồi"): đánh giá hồ sơ như trước đây
DEFAULT_QUESTION = "Đánh giá hồ sơ của em giúp em."
SKILL_NOT_READY = (
    "Phần tư vấn hồ sơ đang bận, mình chưa trả lời được ngay. Bạn bấm hỏi lại giúp mình sau ít phút nhé."
)
CANNOT_ANSWER = "Mình chưa viết được câu trả lời cho câu này. Bạn bấm hỏi lại giúp mình nhé."
ASK_AGAIN_LABEL = "Hỏi lại"


def asks_for_school_list(text: str) -> bool:
    folded = plain(text)
    return any(word in folded for word in SCHOOL_LIST_WORDS)


# Tư vấn theo hồ sơ (phần của Việt). Câu về trường giao code có sẵn: hỏi một trường đã gợi ý thì giải thích
# trường đó (skill_explain → .NET), xin danh sách trường thì chạy gợi ý (.NET). Câu còn lại (đánh giá hồ sơ,
# lộ trình, hoặc cả hai: SLM tự nhận ra từ câu hỏi) thì SLM sinh skill riêng cho học sinh, Gemini viết câu trả lời.
# SLM hoặc Gemini sai thì báo chưa trả lời được kèm nút hỏi lại, không dùng skill tĩnh
class GeneratedAdviceBranch:
    def __init__(
        self, skills: dict[str, SkillHandler], generator: SkillGenerator, writer: AdviceWriter
    ) -> None:
        self._skills = skills
        self._generator = generator
        self._writer = writer

    async def run(self, state: ChatState, message: str, slm: SlmTinResult) -> SkillOutput:
        y = slm.cac_y[0]
        question = y.doan if y.type == "ho_so" else DEFAULT_QUESTION
        if y.type == "ho_so" and y.thuc_the.get("truong") and state.has_results:
            return await self._skills["skill_explain"].run(state, question)

        school_list = y.type == "ho_so" and asks_for_school_list(question)
        # Gợi ý trường cần ngành để lọc trường; tư vấn hồ sơ chỉ cần các mục cơ bản
        asked = ask_missing_profile(state, y, REQUIRED_B4 if school_list else REQUIRED_B3)
        if asked is not None:
            return asked
        if school_list:
            return SkillOutput(RECOMMENDING, action="recommend")

        req = skill_request_of(state, question)
        skill = await self._generator.generate(req)
        if skill is None:
            return SkillOutput(
                SKILL_NOT_READY, quick_replies=[QuickReply(label=ASK_AGAIN_LABEL, value=question)]
            )
        answer = await self._writer.write(skill, req, state.schools)
        if answer is None:
            return SkillOutput(
                CANNOT_ANSWER, quick_replies=[QuickReply(label=ASK_AGAIN_LABEL, value=question)]
            )
        return SkillOutput(answer)


# Trả lời câu hồ sơ đang chờ hoặc cập nhật hồ sơ. Hồ sơ vừa đủ trong lượt này (học sinh bấm "Đúng rồi" ở X1)
# thì tư vấn luôn, không bắt học sinh nhắn thêm một lần nữa mới thấy kết quả.
# Có câu tư vấn bị hoãn (pending_advice) thì trả lời đúng câu đó, không phải tin "Đúng rồi" của lượt này.
# Tin đã có ý ho_so thì thôi: ChatFlow sẽ chạy nhánh tư vấn cho ý đó ngay sau, tư vấn ở đây nữa là trả lời hai lần
class ProfileBranch:
    def __init__(self, handler: ChatProfileHandler, advice: Branch) -> None:
        self._handler = handler
        self._advice = advice

    async def run(self, state: ChatState, message: str, slm: SlmTinResult) -> SkillOutput:
        was_ready = self._handler.ready(state, REQUIRED_B3)
        output = await self._handler.collect(state, message, slm)
        if not was_ready and self._handler.ready(state, REQUIRED_B3) and "ho_so" not in slm.types():
            original = state.pending_advice
            if original is not None:
                state.pending_advice = None
                return combine(
                    output, await self._advice.run(state, original.doan, SlmTinResult(cac_y=[original]))
                )
            return combine(output, await self._advice.run(state, message, slm))
        return output


# Bảng nhánh theo loại câu hỏi SLM. skills: các skill cũ do skills.build_handlers tạo.
# main.py luôn truyền generator và writer nên chạy thật dùng nhánh tư vấn mới. Chỉ test cũ (chưa viết lại) không
# truyền, khi đó tạm dùng LegacyAdviceBranch
def build_branches(
    skills: dict[str, SkillHandler],
    generator: SkillGenerator | None = None,
    writer: AdviceWriter | None = None,
) -> dict[str, Branch]:
    advice: Branch = (
        GeneratedAdviceBranch(skills, generator, writer)
        if generator is not None and writer is not None
        else LegacyAdviceBranch(skills)
    )
    return {
        "mac_dinh": ProfileBranch(ChatProfileHandler(skills), advice),
        "ho_so": advice,
        "kich_ban": ScenarioBranch(),
        "kien_thuc": SkillBranch(skills["skill_tra_loi"]) if "skill_tra_loi" in skills else NotReadyBranch(),
        "trung_tam": (
            SkillBranch(skills["skill_chuyen_trung_tam"])
            if "skill_chuyen_trung_tam" in skills
            else NotReadyBranch()
        ),
        "ngoai_pham_vi": OutOfScopeBranch(),
    }
