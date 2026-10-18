"""Khuôn dữ liệu vào/ra của lượt chấm trường trên cloud."""
from typing import Any, Literal
from pydantic import BaseModel, Field

# Dữ liệu của student
class ScoreStudent(BaseModel):
    gpa: float | None = Field(default=None, ge=0, le=4)
    sat: int | None = Field(default=None, ge=400, le=1600)
    budget: float | None = Field(default=None, gt=0)
    tier: int = Field(ge=1, le=5)  # bậc ngoại khóa Qwen đã tính

# Dữ liệu vào của 1 trường
class ScoreSchool(BaseModel):
    """Một trường đã qua lọc. Thiếu số thì bỏ key, không gửi 0."""
    c: str # Code
    acc: float | None = Field(default=None, ge=0, le=1) # acceptant rate
    iacc: float | None = Field(default=None, ge=0, le=1) # international acceptant rate
    sat: list[int] | None = Field(default=None, min_length=2, max_length=2) # Mốc SAT 25 75
    subm: float | None = Field(default=None, ge=0, le=1) # submission SAT rate
    satp: Literal["req", "opt", "no"] | None = None
    gpa: list[float] | None = Field(default=None, min_length=7, max_length=7) # 7 tỷ lệ GPA trúng tuyển
    avg: float | None = Field(default=None, ge=0, le=4) # GPA trung bình của trường
    L: list[int] = Field(min_length=3, max_length=3) # 3 mức coi trọng GPA, SAT, Ngoại khóa
    coa: float | None = Field(default=None, gt=0) # Tổng chi phí
    aid: float | None = Field(default=None, ge=0) # Mức học bổng trung bình mỗi du học sinh nhaajnUSD/ năm
    q: float | None = Field(default=None, ge=0, le=1) # quality of education

class ScoreRequest(BaseModel):
    student: ScoreStudent
    schools: list[ScoreSchool] = Field(min_length=1) # ít nhất 1 trường

#Kết quả cloud trả về từng trường
class ScoreItem(BaseModel):
    c: str
    w: list[float] # trọng số [GPA, SAT]
    pg: float | None # điểm vị trí GPA
    conf: Literal["H", "L"] # độ tin cậy
    ps: float | None # điểm vị trí SAT
    A: float | None # Điểm học thuật = wgpa x pg + wsat x ps
    adj: float # điểm đã điều chỉnh ( cộng | trừ) ngoại khóa
    S: float | None # điểm sức mạnh A +adj ( điểm chia nhóm)
    g: Literal["R", "M", "S", "NA", "O"] #nhóm theo sức mạnh
    r: float | None # tỷ lệ trúng tuyển = iacc/ acc| nếu <0,5 giảm bậc
    # R: reach| RF: khó vào xa (trường khó + r<0,5) | M: match | S: safety| NA: không xác định (thiếu GPA & SAT) | O: nhận gần như tất cả (tỷ lệ nhận 99%)
    g2: Literal["R", "RF", "M", "S", "NA", "O"] # nhóm theo sức mạnh và tỷ lệ trúng tuyển
    F: float | None # độ dư ngân sách
    H: float | None # học bổng trung bình
    Q: float | None # chất lượng
    gap: float | None # Số USD còn thiếu khi chi phí cao hơn ngân sách. Trong ngân sách thì để trống

#Dữ liệu trả về
class ScoreResponse(BaseModel):
    skill_version: str
    model: str
    items: list[ScoreItem]
    failed: list[str] 

#Khuôn trả về dữ liệu
def score_schema(codes: list[str]) -> dict[str, Any]:
    """Khuôn JSON bắt cloud trả đúng số trường và đúng thứ tự key."""
    num_or_null = {"type": ["number", "null"]}
    n = len(codes)
    return {
        "type": "object",
        "required": ["items"],
        "properties": {
            "items": {
                "type": "array",
                "minItems": n,
                "maxItems": n,
                "items": {
                    "type": "object",
                    "required": [
                        "c", "w", "pg", "conf", "ps", "A", "adj", "S",
                        "g", "r", "g2", "F", "H", "Q", "gap",
                    ],
                    "properties": {
                        "c": {"type": "string", "enum": codes},
                        "w": {"type": "array", "items": {"type": "number"}, "minItems": 2, "maxItems": 2},
                        "pg": num_or_null,
                        "conf": {"enum": ["H", "L"]},
                        "ps": num_or_null,
                        "A": num_or_null,
                        "adj": {"type": "number"},
                        "S": num_or_null,
                        "g": {"enum": ["R", "M", "S", "NA", "O"]},
                        "r": num_or_null,
                        "g2": {"enum": ["R", "RF", "M", "S", "NA", "O"]},
                        "F": num_or_null,
                        "H": num_or_null,
                        "Q": num_or_null,
                        "gap": num_or_null,
                    },
                },
            }
        },
    }