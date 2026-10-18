"""Chọn danh sách trên cloud. Không chấm lại, không đổi nhóm g2."""
from pathlib import Path
from typing import Any
from app.schemas.pick import PickEntry, PickRequest, PickResponse, PickSchool, pick_schema
from app.services.skill_call import SKILLS_DIR, SkillCall
SKILL_PATH = SKILLS_DIR / "skill_pick.md"
BLOCK_LIMITS = {"reach": 3, "match": 5, "safety": 4}
BLOCK_GROUPS = {"reach": {"R", "RF"}, "match": {"M"}, "safety": {"S"}, "other": {"NA", "O"}}

# Kiểm tra kết quả chấm của lượt chọn, trả về danh sách trường được chọn
def check_pick(raw: dict[str, Any], schools: list[PickSchool], need_aid:str) -> list[PickEntry]:
    """Sai khối, vượt số lượng hoặc dùng mã lạ thì ném ValueError để gọi lại."""
    by_code = {school.c: school for school in schools}
    seen: set[str] = set()
    checked: dict[str, list[PickEntry]] = {}

    #Duyệt từng nhóm
    for name in ("reach", "match", "safety", "aid", "other"):
        try:
            entries = [PickEntry.model_validate(item) for item in raw[name]]
        except (KeyError, TypeError) as ex:
            raise ValueError(f"JSON thiếu khối {name}") from ex
        
        #Nếu vượt số lượng trần thì ném ValueError để gọi lại.
        if name in BLOCK_LIMITS and len(entries) > BLOCK_LIMITS[name]:
            raise ValueError(f"Khối {name} có {len(entries)} trường, vượt trần")
        
        #Duyệt từng trường trong nhóm
        for entry in entries:
            school = by_code.get(entry.c)
            if school is None:
                raise ValueError(f"Mã {entry.c} không có trong danh sách đã chấm")
            if entry.c in seen:
                raise ValueError(f"Mã {entry.c} xuất hiện hơn một lần")
            seen.add(entry.c)
            #Xếp hạng phải từ 0 đến 100
            if entry.X is not None and not 0 <= entry.X <= 1:
                raise ValueError(f"{entry.c}: X = {entry.X} ngoài 0–1")
            #Nếu là khối học bổng thì phải có blk = "AID"
            if name == "aid":
                if need_aid == "no" or school.blk != "AID":
                    raise ValueError(f"{entry.c} không được vào khối học bổng")
            #Nếu không phải là khối học bổng thì phải có g2 thuộc khối {name} và blk = "MAIN"
            elif school.g2 not in BLOCK_GROUPS[name] or school.blk != "MAIN":
                raise ValueError(f"{entry.c} không thuộc khối {name}")
        #Lưu danh sách trường được chọn vào checked
        checked[name] = entries
    return checked


class Picker:
    def __init__(self, llm: Any, model: str, skill_path: Path = SKILL_PATH) -> None:
        self._call = SkillCall(llm, model, skill_path)
        self.skill_version = self._call.skill_version
    async def pick(self, req: PickRequest) -> PickResponse:
        codes = [school.c for school in req.items]
        user = req.model_dump(by_alias=True, exclude_none=True)
        checked = await self._call.run(
            user, pick_schema(codes), lambda raw: check_pick(raw, req.items, req.need_aid)
        )
        empty: dict[str, list[PickEntry]] = {name: [] for name in ("reach", "match", "safety", "aid", "other")}
        blocks = checked if checked is not None else empty
        return PickResponse(
            skill_version=self.skill_version,
            model=self._call.model,
            status="OK" if checked is not None else "FAILED",
            **blocks,
        )