"""C4. Hỏi tỉnh / thành trong chat (docs/KE_HOACH_LO_TRINH_THEO_TRUONG_MUC_TIEU.md).

Trả lời lộ trình mà .NET báo chưa có tỉnh (roadmap.provinceMissing): hỏi thêm một câu, có nút Bỏ qua, mỗi phiên hỏi
một lần, kèm ô chọn lấy từ API tỉnh / thành. Lượt sau câu trả lời ĐÚNG LÀ TÊN TỈNH (backend kiểm, kể cả tên gọi khác,
xem province_catalog) thì trả action "set_province": .NET lưu như ô chọn tỉnh ở Tổng quan rồi viết lại câu trả lời
(ChatProvince). Câu khác để model đọc như tin bình thường; không rõ thì hỏi lại kèm ô chọn.
"""

import re

from app.schemas.chat import ChatState, QuickReply, Widget
from app.services.chat_flow import SkillOutput
from app.services.skills import buttons, province_catalog

# Nút mở lộ trình đầy đủ ở Tổng quan (frontend đi theo href)
# Lộ trình đầy đủ (mốc, nhiệm vụ, trung tâm gợi ý) vẽ ở Tổng quan; chat chỉ dẫn sang bằng nút, không viết đường dẫn
ROADMAP_LINK = "/dashboard#lo-trinh"
ROADMAP_BUTTON = QuickReply(label="Xem lộ trình", value="Em muốn xem lộ trình đầy đủ", href=ROADMAP_LINK)

PROVINCE_ID = "LT.TINH"
PROVINCE_QUESTION = "Bạn đang sống ở tỉnh / thành nào? Mình sẽ ưu tiên gợi ý trung tâm gần bạn."
SET_PROVINCE = "set_province"
# .NET thay câu này bằng kết quả lưu; .NET không chạy được thì học sinh vẫn có câu trả lời
SAVING = "Mình ghi nhận tỉnh / thành của bạn: {province}."
# Trả lời không nhận ra tỉnh nào: hỏi lại kèm ô chọn
PROVINCE_UNCLEAR = "Mình chưa rõ bạn đang ở tỉnh / thành nào. Bạn chọn trong danh sách giúp mình nhé."
SKIPPED = "Không sao, khi cần bạn chọn tỉnh / thành ở trang Tổng quan nhé."
SKIP_WORDS = {"bỏ qua", "không", "thôi", "không muốn nói", "không muốn trả lời", "skip"}
# Tên dài nhất là "Thành phố Hồ Chí Minh" (5 chữ); dài hơn thì là câu khác, để SLM đọc
MAX_WORDS = 6
# Chữ đệm đầu và cuối câu trả lời: "dạ em ở Huế ạ" → "Huế"
POLITE_START = re.compile(r"^(?:dạ|vâng|ừm?|ờ)[\s,]+", re.IGNORECASE)
POLITE_END = re.compile(r"(?:[\s,]+(?:ạ|nhé|nha|nhen|ấy|đó|thôi))+$", re.IGNORECASE)
# Hỏi lần đầu và hỏi lại tối đa hai lần khi câu trả lời không rõ
MAX_ASKS = 3
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
    return SkillOutput(
        PROVINCE_QUESTION,
        quick_replies=buttons.quick_replies(PROVINCE_ID, can_skip=True),
        widget=province_widget(),
    )


# Ô chọn tỉnh / thành (widget "select" của chat), chọn xong frontend gửi đúng tên. Chưa lấy được danh sách thì không có ô
def province_widget() -> Widget | None:
    names = province_catalog.loaded()
    if not names:
        return None
    field = {
        "name": "province",
        "label": "Tỉnh / thành",
        "placeholder": "Chọn tỉnh / thành",
        "options": [{"value": name, "label": name} for name in names],
    }
    return Widget(type="select", data={"fields": [field], "format": "{province}"})


# Câu trả lời không rõ: hỏi lại kèm ô chọn, quá số lần thì thôi để không giữ học sinh ở câu này
def ask_again(state: ChatState) -> SkillOutput:
    if state.asked.get(PROVINCE_ID, 0) >= MAX_ASKS:
        state.question_id, state.last_question = None, ""
        return SkillOutput(SKIPPED)
    output = ask_province(state)
    output.answer = PROVINCE_UNCLEAR
    return output


def is_skip(message: str) -> bool:
    return message.strip().strip(".!").lower() in SKIP_WORDS


# Phần có thể là tên nơi: bỏ chữ đệm và "em ở", phải ngắn và không phải câu hỏi. Chỉ là ứng viên, còn phải khớp tỉnh
def province_text(message: str) -> str | None:
    text = POLITE_END.sub("", POLITE_START.sub("", message.strip().strip(".!")))
    text = PLACE_PREFIX.sub("", text).strip()
    if not text or "?" in text or len(text.split()) > MAX_WORDS:
        return None
    return text


# Tên tỉnh / thành chính thức nếu cả câu trả lời là một tỉnh, không thì None
async def recognise(message: str) -> str | None:
    text = province_text(message)
    return None if text is None else await province_catalog.resolve(text)


# Câu đang chờ là câu tỉnh và tin trả lời đúng nó (bỏ qua, hoặc một tên tỉnh): ChatFlow không cần SLM đọc
async def is_reply(message: str) -> bool:
    return is_skip(message) or await recognise(message) is not None


# Trả lời câu tỉnh. Là bỏ qua hoặc tên tỉnh thì bỏ câu đang chờ; câu khác thì None, không đụng state
async def answer_province(state: ChatState, message: str) -> SkillOutput | None:
    province = None if is_skip(message) else await recognise(message)
    if province is None and not is_skip(message):
        return None
    state.question_id, state.last_question = None, ""
    if province is None:
        return SkillOutput(SKIPPED)
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
    output.widget = output.widget or question.widget
    return output
