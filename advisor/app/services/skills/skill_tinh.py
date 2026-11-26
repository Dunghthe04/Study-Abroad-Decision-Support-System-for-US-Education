"""C4. Hỏi tỉnh / thành trong chat (docs/KE_HOACH_LO_TRINH_THEO_TRUONG_MUC_TIEU.md).

Trả lời lộ trình mà .NET báo chưa có tỉnh (roadmap.provinceMissing): hỏi thêm một câu, có nút Bỏ qua, mỗi phiên hỏi
một lần. Lượt sau học sinh gõ tên tỉnh thì trả action "set_province": .NET kiểm tra trong 34 tỉnh (VietnamProvinces),
lưu như ô chọn tỉnh ở Tổng quan rồi viết lại câu trả lời (ChatProvince). Danh sách tỉnh chỉ có ở .NET nên advisor
chỉ gửi chữ học sinh gõ, bỏ phần "em ở".
"""

import re

from app.schemas.chat import ChatState, QuickReply
from app.services.chat_flow import SkillOutput
from app.services.skill_template import ROADMAP_LINK
from app.services.skills import buttons

# Nút mở lộ trình đầy đủ ở Tổng quan (frontend đi theo href)
ROADMAP_BUTTON = QuickReply(label="Xem lộ trình", value="Em muốn xem lộ trình đầy đủ", href=ROADMAP_LINK)

PROVINCE_ID = "LT.TINH"
PROVINCE_QUESTION = "Bạn đang sống ở tỉnh / thành nào? Mình sẽ ưu tiên gợi ý trung tâm gần bạn."
SET_PROVINCE = "set_province"
# .NET thay câu này bằng kết quả lưu; .NET không chạy được thì học sinh vẫn có câu trả lời
SAVING = "Mình ghi nhận tỉnh / thành của bạn: {province}."
SKIPPED = "Không sao, khi cần bạn chọn tỉnh / thành ở trang Tổng quan nhé."
SKIP_WORDS = {"bỏ qua", "không", "thôi", "không muốn nói", "không muốn trả lời", "skip"}
# Tên dài nhất là "Thành phố Hồ Chí Minh" (5 chữ); dài hơn thì là câu khác, để SLM đọc
MAX_WORDS = 6
# "em ở Hà Nội", "mình sống tại Đà Nẵng" → phần sau chữ "ở" / "tại" cuối cùng
PLACE_PREFIX = re.compile(r"^.*\b(?:ở|tại)\s+", re.IGNORECASE)


def wants_to_ask(state: ChatState) -> bool:
    return bool(
        state.roadmap
        and state.roadmap.get("provinceMissing")
        and state.question_id is None
        and not state.asked.get(PROVINCE_ID)
    )


# Ghi câu đang chờ như ask() của chat_flow (câu này không nằm trong QUESTIONS của chat_flow)
def ask_province(state: ChatState) -> SkillOutput:
    state.question_id = PROVINCE_ID
    state.last_question = PROVINCE_QUESTION
    state.asked[PROVINCE_ID] = state.asked.get(PROVINCE_ID, 0) + 1
    return SkillOutput(PROVINCE_QUESTION, quick_replies=buttons.quick_replies(PROVINCE_ID, can_skip=True))


def is_skip(message: str) -> bool:
    return message.strip().strip(".!").lower() in SKIP_WORDS


# Chữ trông như tên một nơi: ngắn, không phải câu hỏi. Không thì None
def province_text(message: str) -> str | None:
    text = PLACE_PREFIX.sub("", message.strip().strip(".!")).strip()
    if not text or "?" in text or len(text.split()) > MAX_WORDS:
        return None
    return text


# Câu đang chờ là câu tỉnh và tin là câu trả lời của nó: ChatFlow không cần SLM đọc (skill_ho_so.is_form_text)
def is_province_answer(question_id: str | None, message: str) -> bool:
    return question_id == PROVINCE_ID and (is_skip(message) or province_text(message) is not None)


# Trả lời câu tỉnh. Câu này chỉ hỏi một lần nên luôn bỏ câu đang chờ; tin không phải tên nơi nào thì None,
# skill_ho_so xử lý tiếp như không có câu chờ
def answer_province(state: ChatState, message: str) -> SkillOutput | None:
    state.question_id = None
    state.last_question = ""
    if is_skip(message):
        return SkillOutput(SKIPPED)
    province = province_text(message)
    if province is None:
        return None
    return SkillOutput(
        SAVING.format(province=province),
        quick_replies=[ROADMAP_BUTTON.model_copy()],
        action=SET_PROVINCE,
        action_data={"province": province},
    )


# Thêm câu hỏi tỉnh sau câu trả lời lộ trình, giữ nút của câu trả lời
def with_province_question(state: ChatState, output: SkillOutput) -> SkillOutput:
    if not wants_to_ask(state):
        return output
    question = ask_province(state)
    output.answer = f"{output.answer}\n\n{question.answer}"
    output.quick_replies = [*output.quick_replies, *question.quick_replies]
    return output
