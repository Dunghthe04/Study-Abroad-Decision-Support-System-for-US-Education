"""Lượt ③ (xếp hạng và chọn danh sách): gọi AI 1 lần theo skill_pick.md, kiểm tra JSON, sai thì gọi lại 1 lần."""

from pathlib import Path
from typing import Any

from app.schemas.pick import LIMITS, PickItem, PickRequest, PickResponse, PickResult, pick_schema
from app.services.skill_call import SKILLS_DIR, SkillCall

SKILL_PATH = SKILLS_DIR / "skill_pick.md"

class PickError(Exception):
    """AI trả sai 2 lần liền: chưa có danh sách để hiển thị."""

#True: trường được nằm trong nhóm, ngược lại false
def belongs(block: str, item: PickItem, need_aid: str)-> bool:
    """Trường này có được nằm trong khối này không (skill bước P3). Dùng để chặn AI đổi nhóm."""
    if block == "aid":
        #Block aid = true: vượt ngân sách cần học bổng & cần học bổng
        return item.blk =="AID" and need_aid != "no"

    if block == "other":
        return item.g2 in ("NA", "O")
    
    if item.blk != "MAIN":
        return False

    return item.g2 in {"reach": ("R", "RF"), "match": ("M",), "safety": ("S",)}[block]


#Kiểm tra kết quả AI có đúng k
def check_pick(raw: dict[str,Any], req: PickRequest)-> PickResult:
    #Kiểm tra dl AI trả về đúng cấu trúc k
    result = PickResult.model_validate(raw)
    #Tạo dict ánh ạ mã trường tới đối tượng trường
    items = {}
    for i in req.items:
        items[i.c] = i
    seen: set[str] = set()

    for block in ("reach", "match", "safety", "aid", "other"):
        entries = getattr(result, block)
        #Duyệt từng trường trong nhóm
        for e in entries:
            # Nếu mã trường k nằm trong danh sách
            if e.c not in items:
                raise ValueError(f"{block}: mã {e.c} không có trong items")
            if e.c in seen:
                raise ValueError(f"{e.c} xuất hiện nhiều hơn 1 lần")
            seen.add(e.c)
            if not belongs(block, items[e.c], req.needAid):
                raise ValueError(f"{e.c} (g2 {items[e.c].g2}, {items[e.c].blk}) không được nằm ở khối {block}")
        
        # Không bỏ sót: khối phải lấy đủ số trường hợp lệ, tối đa theo hạn mức
        #Đếm xem có bao nhiêu trường đầu vào đủ điều kiện nhóm ktra
        eligible = sum(belongs(block, i, req.needAid) for i in req.items)

        #Số trường mà AI phải trả về
        expected = min(eligible, LIMITS.get(block, eligible))

        #Nếu số lượng AI trả về khác với số lượng yêu cầu nhóm
        if len(entries) != expected:
            raise ValueError(f"{block}: cần {expected} trường, AI trả {len(entries)}")

        #3 khối chính sẽ xếp hạng điểm x giảm dần
        if block in LIMITS:
            xs = [e.X for e in entries]
            if None in xs or xs != sorted(xs, reverse=True):
                raise ValueError(f"{block}: X không giảm dần {xs}")

    return result
class Picker:
    def __init__(self, llm: Any, model: str, skill_path: Path = SKILL_PATH) -> None:
        self._call = SkillCall(llm, model, skill_path)
        self.skill_version = self._call.skill_version

    async def pick(self, req: PickRequest) -> PickResponse:
        user = req.model_dump(exclude_none=True)   # thiếu số thì bỏ key, như lượt ②
        codes = [i.c for i in req.items]
        result = await self._call.run(user, pick_schema(codes), lambda raw: check_pick(raw, req))
        if result is None:
            raise PickError("AI không trả được danh sách hợp lệ sau 2 lần gọi")
        return PickResponse(**result.model_dump(), skill_version=self.skill_version, model=self._call.model)

    
