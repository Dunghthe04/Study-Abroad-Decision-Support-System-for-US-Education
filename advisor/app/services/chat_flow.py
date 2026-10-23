"""Điều phối luồng chat: đọc trạng thái, chọn việc cần làm, trả câu trả lời và trạng thái mới.

Bước 1.1 mới có khung: cuộc trò chuyện mới thì chào bằng câu B0.1. Các skill thêm dần từ bước 1.2.
"""

from app.schemas.chat import ChatRequest, ChatResponse, ChatState

# Câu hỏi lấy nguyên văn từ framework, AI không tự đặt câu hỏi
QUESTIONS = {"B0.1": "Chào bạn, mình là trợ lý tư vấn du học Mỹ. Bạn đang cần mình giúp gì?"}

NOT_READY = "Phần này mình đang hoàn thiện, bạn quay lại sau nhé."


# Ghi lại câu vừa hỏi vào ChatState, lưu last question và tăng số lần hỏi
def ask(state: ChatState, question_id: str) -> str:
    # Lưu câu hỏi vừa hỏi
    state.last_question = QUESTIONS[question_id]
    # Tăng số lần hỏi
    state.asked[question_id] = state.asked.get(question_id, 0) + 1
    return state.last_question


class ChatFlow:
    async def reply(self, req: ChatRequest) -> ChatResponse:
        # Cuộc trò chuyện mới: chưa có trạng thái -> mở đầu bằng B0.1
        if req.state is None:
            state = ChatState()
            return ChatResponse(answer=ask(state, "B0.1"), state=state)

        # Đã có trạng thái: các skill làm ở bước 1.2 trở đi, tạm trả câu mẫu, giữ nguyên trạng thái
        state = req.state.model_copy(deep=True)
        return ChatResponse(answer=NOT_READY, state=state)
