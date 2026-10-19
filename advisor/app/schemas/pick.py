"""Khuôn dữ liệu vào/ra của lượt chọn danh sách trên cloud."""

from typing import Any, Literal

from pydantic import BaseModel, ConfigDict, Field

# Lý do chọn trường
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
]


# một trường đã có kết quả chấm, gửi vào lượt chọ
class PickSchool(BaseModel):
    """Một trường đã chấm. blk do hard filter gắn: MAIN hoặc AID."""

    c: str
    g2: Literal["R", "RF", "M", "S", "NA", "O"]
    blk: Literal["MAIN", "AID"]
    F: float | None = None
    H: float | None = None
    Q: float | None = None
    gap: float | None = None
    pg: float | None = None
    ps: float | None = None
    adj: float | None = None
    r: float | None = None


# Dữ liệu gửi vào lượt chọn
class PickRequest(BaseModel):
    model_config = ConfigDict(populate_by_name=True)
    pref: list[int] = Field(
        min_length=3, max_length=3
    )  # [Chi phí, học bổng, chất lượng] [2,2,2] [3,1,1][1,3,1] [1,1,3]
    need_aid: Literal["no", "preferred", "required"] = Field(alias="needAid")
    items: list[PickSchool] = Field(min_length=1)


# Dữ liệu của một trường được đưa vào lượt chọn
class PickEntry(BaseModel):
    c: str
    X: float | None = None  # Điểm xếp hạng của trường
    why: list[WhyCode]  # Lý do chọn trường


class PickResponse(BaseModel):
    skill_version: str
    model: str
    status: str
    reach: list[PickEntry]
    match: list[PickEntry]
    safety: list[PickEntry]
    aid: list[PickEntry]  # Danh sách trường được học bổng
    other: list[PickEntry]  # Danh sách trường không được chọn


def pick_schema(codes: list[str]) -> dict[str, Any]:
    """Khuôn JSON bắt cloud chỉ dùng mã trường đã gửi."""
    why = {
        "type": "array",
        "items": {
            "enum": [
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
            ]
        },
    }
    entry = {
        "type": "object",
        "required": ["c", "why"],
        "properties": {
            "c": {"type": "string", "enum": codes},
            "X": {"type": ["number", "null"]},
            "why": why,
        },
    }
    block = {"type": "array", "items": entry}
    return {
        "type": "object",
        "required": ["status", "reach", "match", "safety", "aid", "other"],
        "properties": {
            "status": {"type": "string"},
            "reach": block,
            "match": block,
            "safety": block,
            "aid": block,
            "other": block,
        },
    }
