"""Dữ liệu vào/ ra của lượt bậc ngoại khóa"""

from typing import Any, Literal

from pydantic import BaseModel, Field

MAX_ACTIVITIES = 10  # tối đa 10 hoạt động
MAX_AWARD = 10
ROLES = [1.0, 0.8, 0.6, 0.3]  # sáng lập, trưởng nhóm, phó nhóm, thành viên


class EcActivity(BaseModel):
    # mô tả
    text: str = Field(min_length=1, max_length=500)
    # phạm vi
    scope: int = Field(ge=1, le=5)
    # Tháng
    months: int = Field(ge=0, le=120)


class EcAward(BaseModel):
    text: str = Field(min_length=1, max_length=300)
    # cấp giải, cùng thang với scope
    level: int = Field(ge=1, le=5)


class EcRequest(BaseModel):
    act: list[EcActivity] = Field(default=[], max_length=MAX_ACTIVITIES)
    awards: list[EcAward] = Field(default=[], max_length=MAX_AWARD)


class EcActivityResult(BaseModel):
    role: Literal[1.0, 0.8, 0.6, 0.3]  # vai trò AI đọc từ text
    scope: int = Field(ge=1, le=5)  # phạm vi sau khi AI xét (chỉ giữ hoặc hạ)
    q: float  # điểm hoạt động


class EcResult(BaseModel):
    act: list[EcActivityResult]
    tong_hd: float  # tổng q của tối đa 4 hđ cao nhất
    bonus: float  # điểm thưởng giải , max =0,5
    m: float  # mức ngoại khóa = (tong_hd  + bonus)/ 4
    tier: int = Field(ge=1, le=5)  # cấp ngoại khóa


class EcResponse(EcResult):
    skill_version: str
    model: str


def ec_schema(n_act: int) -> dict[str, Any]:
    """Json schema cho format của Ollama trả về"""
    return {
        "type": "object",
        "required": ["act", "tong_hd", "bonus", "m", "tier"],
        "properties": {
            "act": {
                "type": "array",
                "minItems": n_act,
                "maxItems": n_act,
                "items": {
                    "type": "object",
                    "required": ["role", "scope", "q"],
                    "properties": {
                        "role": {"enum": ROLES},
                        "scope": {"type": "integer", "minimum": 1, "maximum": 5},
                        "q": {"type": "number"},
                    },
                },
            },
            "tong_hd": {"type": "number"},
            "bonus": {"type": "number"},
            "m": {"type": "number"},
            "tier": {"type": "integer", "minimum": 1, "maximum": 5},
        },
    }
