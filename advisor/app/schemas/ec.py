"""Dữ liệu vào/ra của bậc ngoại khóa: AI đọc vai trò, phạm vi; Python tính q, m, bậc."""

from typing import Any, Literal

from pydantic import BaseModel, Field

MAX_ACTIVITIES = 10  # tối đa 10 hoạt động
MAX_AWARD = 10
ROLES = [1.0, 0.8, 0.6, 0.3]  # sáng lập, trưởng nhóm, phó nhóm, thành viên
# Vai trò học sinh chọn ở form (luồng 6 bước) → giá trị; dùng khi AI lỗi
FORM_ROLES = {"founder": 1.0, "leader": 0.8, "vice_leader": 0.6, "member": 0.3}


class EcActivity(BaseModel):
    # mô tả
    text: str = Field(min_length=1, max_length=500)
    # phạm vi học sinh chọn: 1 trường ... 5 quốc tế
    scope: int = Field(ge=1, le=5)
    # Tháng
    months: int = Field(ge=0, le=120)
    # vai trò chọn ở form; không gửi cho AI, chỉ dùng khi AI lỗi
    role: Literal["founder", "leader", "vice_leader", "member"] | None = None


class EcAward(BaseModel):
    text: str = Field(min_length=1, max_length=300)
    # cấp giải, cùng thang với scope
    level: int = Field(ge=1, le=5)


class EcRequest(BaseModel):
    act: list[EcActivity] = Field(default=[], max_length=MAX_ACTIVITIES)
    awards: list[EcAward] = Field(default=[], max_length=MAX_AWARD)


class EcActivityRead(BaseModel):
    """Phần AI đọc được cho 1 hoạt động."""

    role: Literal[1.0, 0.8, 0.6, 0.3]  # vai trò đọc từ text
    scope: int = Field(ge=1, le=5)  # phạm vi sau khi xét (chỉ giữ hoặc hạ)


class EcRead(BaseModel):
    """JSON AI trả về: chỉ có đọc hiểu, không có phép tính."""

    act: list[EcActivityRead]


class EcActivityResult(EcActivityRead):
    q: float  # điểm hoạt động, Python tính


class EcResponse(BaseModel):
    act: list[EcActivityResult]
    tong_hd: float  # tổng q của tối đa 4 hoạt động cao nhất
    bonus: float  # điểm thưởng giải, tối đa 0,5
    m: float  # mức ngoại khóa = (tong_hd + bonus) / 4
    tier: int = Field(ge=1, le=5)  # bậc ngoại khóa
    skill_version: str
    model: str
    ai_used: bool  # false = không gọi AI hoặc AI lỗi, vai trò lấy từ form


def ec_schema(n_act: int) -> dict[str, Any]:
    """JSON Schema cho format của Ollama: đúng n hoạt động, mỗi hoạt động chỉ có role và scope."""
    return {
        "type": "object",
        "required": ["act"],
        "properties": {
            "act": {
                "type": "array",
                "minItems": n_act,
                "maxItems": n_act,
                "items": {
                    "type": "object",
                    "required": ["role", "scope"],
                    "properties": {
                        "role": {"enum": ROLES},
                        "scope": {"type": "integer", "minimum": 1, "maximum": 5},
                    },
                },
            },
        },
    }
