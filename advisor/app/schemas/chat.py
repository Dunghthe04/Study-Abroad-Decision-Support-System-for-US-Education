"""Khuôn dữ liệu vào/ra của luồng chat (framework v5, Phần 4). .NET gửi trạng thái, advisor trả trạng thái mới."""

from typing import Any, Literal

from pydantic import Field

from app.core.study_levels import StudyLevel
from app.schemas.common import CamelModel

Step = Literal["B0", "B1", "B2", "B3", "B4", "B5", "B6", "END"]
FieldStatus = Literal["confirmed", "said_in_B0", "none", "unknown", "declined"]


# Một tin nhắn trong lịch sử chat
class ChatMessage(CamelModel):
    role: Literal["user", "assistant", "system"]
    content: str = Field(min_length=1, max_length=4000)


# Một thông tin hồ sơ + kèm status
class ProfileField(CamelModel):
    value: Any = None
    status: FieldStatus


# Một trường trong danh sách gợi ý gần nhất (do .NET gửi), để skill_explain biết học sinh đang nói trường nào
class ResultSchool(CamelModel):
    code: str
    name: str
    group: str | None = None


# Trạng thái code giữ giữa các lượt (fw 4.3)
# Hệ thống lưu lại thông tin về tiến trình hội thoại để khi người dùng nhắn tin tiếp theo, AI biết mình đang làm đến đâu.
class ChatState(CamelModel):
    current_step: Step = "B0"
    need: str | None = None
    branch: str | None = None
    profile: dict[str, ProfileField] = {}
    last_question: str = ""  # câu hệ thống vừa hỏi, là đầu vào của bước chọn skill
    # mã câu đang chờ học sinh trả lời, ví dụ "B2.D2"; None = không có câu nào chờ
    question_id: str | None = None
    asked: dict[str, int] = {}  # mã câu hỏi → số lần đã hỏi (tối đa 2) ví dụ câu hỏi B0 hỏi tối đa 2
    return_to: Step | None = None  # bước dở cần quay lại sau B5, B6
    intake_mode: Literal["chat", "form"] | None = None  # học sinh chọn điền hồ sơ bằng chat hay form
    has_results: bool = False  # đã có danh sách trường gợi ý chưa (skill_explain, skill_kich_ban cần)
    schools: list[ResultSchool] = []  # danh sách trường gợi ý gần nhất, .NET làm mới mỗi lượt khi has_results
    # Học bạ theo năm do .NET gửi mỗi lượt cùng hồ sơ; None = nhập GPA tổng. Không nằm trong profile vì chat chỉ đọc
    # (học bạ từng lớp chỉ nhập hoặc OCR ở form), SLM chọn skill và Qwen đọc tin không cần thấy
    transcript: dict[str, Any] | None = None
    # Mốc từ dữ liệu trường cùng bậc học ({"gpa4": 3.6, "english": {"ielts": 6.5, ...}}), .NET tính và gửi mỗi lượt;
    # chỉ dùng để ghi mức GPA / tiếng Anh cho skill ho_so. None = chưa có dữ liệu trường thì chỉ ghi điểm
    benchmarks: dict[str, Any] | None = None
    study_level: StudyLevel | None = None  # bậc học học sinh quan tâm (từ .NET), dùng để lọc kiến thức


# Nguồn trích dẫn, khớp record Citation bên .NET
class Citation(CamelModel):
    title: str
    url: str | None = None
    snippet: str | None = None


# Nút gợi ý: label là chữ trên nút, value là chữ gửi đi như tin nhắn của học sinh khi bấm
class QuickReply(CamelModel):
    label: str
    value: str


# Công cụ nhập ngay trong chat (ô chọn ngành, bảng rà soát...); frontend vẽ theo type
class Widget(CamelModel):
    type: Literal["major", "review", "budget", "scholarship", "form", "select", "school"]
    data: dict[str, Any] = {}


class ChatRequest(CamelModel):
    messages: list[ChatMessage] = Field(min_length=1, max_length=100)
    session_id: str | None = None
    study_level: StudyLevel | None = None
    state: ChatState | None = None  # None = cuộc trò chuyện mới
    # Hồ sơ hiện tại do .NET gửi mỗi lượt (đọc từ bản nháp dùng chung); None = không gửi
    profile: dict[str, ProfileField] | None = None
    # Danh sách trường của lần gợi ý gần nhất do .NET gửi mỗi lượt; None = không gửi
    schools: list[ResultSchool] | None = None
    # Học bạ theo năm ({"years": [...]}) do .NET gửi cùng hồ sơ; None = không có (nhập GPA tổng)
    transcript: dict[str, Any] | None = None
    # Mốc GPA / tiếng Anh từ dữ liệu trường do .NET gửi mỗi lượt; None = không gửi (giữ mốc cũ trong state)
    benchmarks: dict[str, Any] | None = None


class ChatResponse(CamelModel):
    answer: str
    citations: list[Citation] = []
    disclaimer: str | None = None  # Lời lưu ý
    state: ChatState
    skills: list[str] = []  # skill đã chạy ở lượt này, để ghi log
    quick_replies: list[QuickReply] = []  # nút gợi ý hiện dưới câu trả lời
    widget: Widget | None = None  # công cụ nhập cần hiện trong chat
    profile: dict[str, ProfileField] = {}  # hồ sơ sau lượt này (điền ở việc 1.5)
    profile_changed: list[str] = []  # khóa hồ sơ đổi trong lượt này (điền ở việc 1.5)
    # Việc .NET cần làm sau lượt này. "recommend" = chạy gợi ý trường (pipeline ở .NET),
    # "explain" = viết lời tư vấn một trường (action_data["code"]),
    # "goal_seek" = tìm mức cần đạt để vào một trường (action_data["school"]: tên trường học sinh nói, ["target"]: reach | match | safety | None),
    # "simulate" = chạy what-if (action_data["changes"]: giá trị đã kiểm tra, ["unsupported"]: yếu tố chưa thử được),
    # None = không có
    action: Literal["recommend", "explain", "simulate", "goal_seek"] | None = None
    action_data: dict[str, Any] = {}  # dữ liệu kèm action, ví dụ {"code": "ucla"} cho explain
