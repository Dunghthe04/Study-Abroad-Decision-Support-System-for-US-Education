"""Khuôn dữ liệu vào/ra của lượt tư vấn từng trường: Qwen viết lời từ số đã lưu."""

from typing import Any, Literal

from pydantic import BaseModel, ConfigDict, Field

from app.schemas.chat import AiUsageMixin

# Mã lý do của một trường (backend suy từ số đã tính, advisor chỉ nhận để viết lời)
WhyCode = Literal[
    "GPA_HIGH",
    "GPA_MID",
    "GPA_LOW",
    "SAT_ABOVE",
    "SAT_IN_RANGE",
    "SAT_BELOW",
    "BUDGET_OK",
    "NEED_AID",
    "INTL_LOW",
    "EC_PLUS",
    "EC_MINUS",
    # Giới hạn theo độ chọn lọc (UC-22): backend vẫn gửi các mã này cho trường rất chọn lọc, thiếu thì cả lượt giải thích bị từ chối
    "LOW_RATE",
    "RATE_CAP",
    "WEAK_CAP",
]

MAX_POINTS = 3  # tối đa 3 ý
FIT_PART_KEYS = ("position", "budget", "quality", "aid", "english")  # thứ tự theo đặc tả (UC-24)


# Dữ liệu đầu vào sinh viên
class ExplainStudent(BaseModel):
    gpa: float | None = Field(default=None, ge=0, le=4)
    sat: int | None = Field(default=None, ge=400, le=1600)
    budget: float | None = None
    tier: int | None = Field(default=None, ge=1, le=5)  # bậc ngoại khóa
    major: str | None = Field(default=None, max_length=200)


# Một thành phần của điểm phù hợp: score 0–1 (null = thiếu dữ liệu nên bị bỏ khỏi tổng), weight là trọng số đã dùng
class ExplainFitPart(BaseModel):
    key: Literal["position", "budget", "quality", "aid", "english"]
    score: float | None = Field(default=None, ge=0, le=1)
    weight: float = Field(ge=0, le=1)


# Điểm phù hợp của trường (UC-24) để AI diễn giải: tổng 0–1 và năm thành phần
class ExplainFit(BaseModel):
    model_config = ConfigDict(populate_by_name=True)

    total: float | None = Field(default=None, ge=0, le=1)
    priority_adjusted: bool = Field(default=False, alias="priorityAdjusted")
    parts: list[ExplainFitPart] = Field(default=[], max_length=len(FIT_PART_KEYS))


# Dữ liệu 1 trường
class ExplainSchool(BaseModel):
    name: str = Field(min_length=1, max_length=256)
    state: str | None = None
    control: str | None = None  # public | private
    g2: Literal["R", "RF", "M", "S", "NA", "O"]
    blk: Literal["MAIN", "AID"] = "MAIN"
    why: list[WhyCode] = Field(default=[], max_length=14)  # mã lý do cloud đã chọn
    acc: float | None = None  # tỷ lệ nhận chung, 0–1
    sat25: int | None = None
    sat75: int | None = None
    pg: float | None = None  # GPA của học sinh cao hơn khoảng bao nhiêu % sinh viên trường
    ps: float | None = None
    cost: float | None = None
    gap: float | None = None  # USD còn thiếu khi vượt ngân sách
    fit: ExplainFit | None = None  # điểm phù hợp và trọng số (UC-24), không có thì AI không nói về điểm


# Dữ liệu đầu vào để tư vấn
class ExplainRequest(BaseModel):
    student: ExplainStudent
    school: ExplainSchool
    # Giả định học sinh đang xem ("GPA 3.6, SAT 1500"); None là hồ sơ thật. Số liệu trong student và school đã tính theo gốc này
    basis: str | None = Field(default=None, max_length=200)


# Kết quả trả về từ Qwen: tóm tắt, điểm mạnh, điểm cần cải thiện
class ExplainResult(BaseModel):
    summary: str = Field(min_length=10)
    strengths: list[str] = Field(default=[], max_length=MAX_POINTS)
    improvements: list[str] = Field(default=[], max_length=MAX_POINTS)


# Kết quả trả về từ API: phiên bản, trạng thái, kết quả
class ExplainResponse(AiUsageMixin):
    skill_version: str
    model: str
    status: str  # OK | FAILED
    summary: str = ""
    strengths: list[str] = []
    improvements: list[str] = []


def explain_schema() -> dict[str, Any]:
    """Khuôn JSON ép Qwen trả: đoạn tóm tắt + 2 danh sách ngắn."""
    points = {"type": "array", "items": {"type": "string"}, "maxItems": MAX_POINTS}
    return {
        "type": "object",
        "required": ["summary", "strengths", "improvements"],
        "properties": {
            "summary": {"type": "string"},
            "strengths": points,
            "improvements": points,
        },
    }
