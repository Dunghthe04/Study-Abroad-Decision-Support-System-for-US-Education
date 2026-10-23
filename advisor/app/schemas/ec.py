"""Dữ liệu vào/ra của bậc ngoại khóa: Qwen đọc vai trò, phạm vi; Python tính q, m, bậc."""

from typing import Any, Literal

from pydantic import BaseModel, Field

MAX_ACTIVITIES = 10
MAX_AWARD = 10
ROLES = [1.0, 0.8, 0.6, 0.3]
FORM_ROLES = {"founder": 1.0, "leader": 0.8, "vice_leader": 0.6, "member": 0.3}


# Dữ liệu vào: hoạt động ngoại khóa
class EcActivity(BaseModel):
    text: str = Field(min_length=1, max_length=500)
    scope: int = Field(ge=1, le=5)  # phạm vi học sinh chọn: 1 trường ... 5 quốc tế
    months: int = Field(ge=0, le=120)
    role: Literal["founder", "leader", "vice_leader", "member"] | None = None


# Dữ liệu vào giải thưởng
class EcAward(BaseModel):
    text: str = Field(min_length=1, max_length=300)
    level: int = Field(ge=1, le=5)  # cấp giải, cùng thang với scope


# Dữ liệu vào:  mảng hoạt động và giải thưởng, mỗi mảng có độ dài tối đa 10
class EcRequest(BaseModel):
    act: list[EcActivity] = Field(default=[], max_length=MAX_ACTIVITIES)
    awards: list[EcAward] = Field(default=[], max_length=MAX_AWARD)


# kết quả AI đọc một hoạt động
class EcActivityRead(BaseModel):
    role: Literal[1.0, 0.8, 0.6, 0.3]
    scope: int = Field(ge=1, le=5)


# mẢNG  kết quả AI đọc hoạt động
class EcRead(BaseModel):
    act: list[EcActivityRead]


# Kết quả tính mỗi activity
class EcActivityResult(EcActivityRead):
    q: float


class EcResponse(BaseModel):
    # mảng activity đã đọc
    act: list[EcActivityResult]
    # tổng điểm hoạt động
    tong_hd: float
    # tổng điểm giải thưởng
    bonus: float
    tier: int = Field(ge=1, le=5)
    skill_version: str
    model: str
    ai_used: bool


def ec_schema(n_act: int) -> dict[str, Any]:
    """Ollama phải trả đúng n hoạt động, mỗi hoạt động chỉ có role và scope."""
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
            }
        },
    }
