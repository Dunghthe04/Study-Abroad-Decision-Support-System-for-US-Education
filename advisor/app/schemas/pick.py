"""Dữ liệu vào/ra của lượt ③ (xếp hạng và chọn danh sách)"""
from typing import Any, Literal, get_args

from pydantic import BaseModel, Field

from app.schemas.score import MAX_SCHOOLS

LIMITS = {"reach": 3, "match": 5, "safety": 4}   # số trường tối đa mỗi khối, tổng 12
# Mã lý do (skill bước P5). Frontend đổi mã thành câu tiếng Việt.
Reason = Literal["GPA_HIGH", "GPA_MID", "GPA_LOW", "SAT_ABOVE", "SAT_IN_RANGE", "SAT_BELOW",
                 "BUDGET_OK", "NEED_AID", "INTL_LOW", "EC_PLUS", "EC_MINUS"]
REASONS = list(get_args(Reason)) # cùng danh sách, dạng list để đưa vào JSON Schema

#Mô tả 1 trường được đưa vào chọn
class PickItem(BaseModel):
    """Một trường đã chấm"""
    c: str
    #Nhóm cuối từ lượt 2 . R Khó vào, RF Khó vào xa, M Vừa sức, S An toàn hơn,
    # NA thiếu GPA lẫn SAT, O trường nhận gần như tất cả
    g2: Literal["R", "RF", "M", "S", "NA", "O"]   # nhóm cuối từ lượt ②, lượt ③ không được đổi
    blk: Literal["MAIN", "AID"]                     # từ HardFilter (C#): Keep → MAIN (trong ngân sách),
                                                    # AidBlock → AID (vượt ngân sách, cần học bổng lớn)
                                                    # → P3 khối aid, P5 BUDGET_OK / NEED_AID
    # --- Để xếp hạng (P2, P4) ---
    F: float | None=None #Finance 0-1: (Ngân sách - chi phí)/ ngân sách - tính X
    H: float | None=None #Học bổng 0-1: học bổng TB du học sinh/ chi phí -> Tính x
    Q: float | None=None #Quality 0-1 TB tỉ lệ giữ chân và tốt nghiệp → tính X
    gap: float | None = None    # số tiền vượt ngân sách (USD/năm) → sắp khối aid, gap nhỏ trước

    # --- Để chọn mã lý do (P5) ---
    pg: float | None = None     # position GPA 0–100: > 75 GPA_HIGH, 25–75 GPA_MID, < 25 GPA_LOW
    ps: float | None = None     # position SAT 0–100: > 75 SAT_ABOVE, 25–75 SAT_IN_RANGE, < 25 SAT_BELOW
    adj: float | None = None    # điều chỉnh ngoại khóa −6..+6: > 0 EC_PLUS, < 0 EC_MINUS
    r: float | None = None      # tỉ lệ nhận du học sinh / tỉ lệ chung: < 0.5 INTL_LOW (thay cho EC_*)

#Yêu cầu trường của học sinh
class PickRequest(BaseModel):
    # mức quan tâm [chi phí, học bổng, chất lượng]
    pref: list[Literal[0,1,2,3,4]] = Field(min_length=3, max_length=3)
    needAid: Literal["no", "preferred", "required"]
    items: list[PickItem] = Field(min_length=1, max_length=MAX_SCHOOLS)

class PickEntry(BaseModel):
    c: str
    X: float | None = None   # điểm xếp hạng; khối aid và other không có
    why: list[Reason] = []

#Trả kết quả các danh sách nhóm trường
class PickResult(BaseModel):
    status: Literal["OK"]
    reach: list[PickEntry]
    match: list[PickEntry]
    safety: list[PickEntry]
    aid: list[PickEntry]
    other: list[PickEntry]

class PickResponse(PickResult):
    skill_version: str
    model: str

#Tạo 1 json schema để Ollama trả về
def pick_schema(codes: list[str]) -> dict[str, Any]:
    """JSON Schema cho format của Ollama: mã trường chỉ được lấy trong items, mỗi khối không quá hạn mức."""
    #Cấu trúc 1 trường
    entry = {
        "type": "object",
        "required": ["c", "X", "why"],
        "properties": {
            "c": {"type": "string", "enum": codes},
            "X": {"type": ["number", "null"]},
            "why": {"type": "array", "items": {"enum": REASONS}},
        },
    }
    #Giới hạn từng danh sách
    blocks = {}

    for name in ("reach", "match", "safety", "aid", "other"):
        blocks[name] = {
        "type": "array",
        "items": entry,
        "maxItems": LIMITS.get(name, len(codes))
    }
    return {
        "type": "object",
        "required": ["status", "reach", "match", "safety", "aid", "other"],
        "properties": {"status": {"enum": ["OK"]}, **blocks},
    }