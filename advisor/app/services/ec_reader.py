"""Lượt ① (bậc ngoại khóa): gọi AI 1 lần theo skill_ec.md, kiểm tra JSON, sai thì gọi lại 1 lần."""

from pathlib import Path
from typing import Any

from app.schemas.ec import EcRequest, EcResponse, EcResult, ec_schema
from app.services.skill_call import SKILLS_DIR, SkillCall

SKILL_PATH = SKILLS_DIR / "skill_ec.md"
#Mốc chia bậc m :  m < 0.10 → 1, < 0.25 → 2, < 0.45 → 3, < 0.65 → 4, còn lại 5
TIER_CUTS = [0.10, 0.25, 0.45, 0.65]

class EcError(Exception):
    """AI trả sai 2 lần liền: không có bậc ngoại khóa để dùng"""

def tier_of(m: float) -> int:
    tier=1
    for cut in TIER_CUTS:
        if m >= cut:
            tier+=1
    return tier

def check_ec(raw: dict[str, Any], req: EcRequest) -> EcResult:
    """Kiểm tra kết quả JSON AI trả về có đúng định dạng k. Sai ném ValueError"""
    result = EcResult.model_validate(raw) # Kiểm tra schema

    if len(result.act) != len(req.act): 
        raise ValueError(f"Có {len(req.act)} hoạt động nhưng AI trả {len(result.act)}")

    # ghép act kết quả và req vào vd (1, 2,2)
    for i, (got, sent) in enumerate(zip(result.act, req.act, strict=True)):
        #Ai chỉ được giữ or hạ phạm vi
        if got.scope > sent.scope:
             raise ValueError(f"Hoạt động {i}: AI nâng phạm vi {sent.scope} → {got.scope}")
        if not 0 <= got.q<=1:
             raise ValueError(f"Hoạt động {i}: q = {got.q} ngoài 0..1")
    if not 0 <= result.bonus <= 0.5:
        raise ValueError(f"bonus = {result.bonus} ngoài 0..0.5")
    if not 0 <= result.m <= 1:
        raise ValueError(f"m = {result.m} ngoài 0..1")
    # Bậc phải khớp với m AI vừa tính (tra bảng, không phải tính lại công thức)
    if result.tier != tier_of(result.m):
        raise ValueError(f"tier {result.tier} không khớp m = {result.m}")
    return result 

class EcReader:
    def __init__(self, llm: Any, model: str, skill_path: Path = SKILL_PATH) -> None:
        self._call = SkillCall(llm, model, skill_path)
        self.skill_version = self._call.skill_version

    async def read(self, req: EcRequest) -> EcResponse:
        # Bước N7: không có hoạt động và giải nào thì m = 0, bậc 1. Không cần gọi AI.
        if not req.act and not req.awards:
            return self._response(EcResult(act=[], tong_hd=0, bonus=0, m=0, tier=1))

        result = await self._call.run(req.model_dump(), ec_schema(len(req.act)), lambda raw: check_ec(raw, req))
        if result is None:
            raise EcError("AI không trả được bậc ngoại khóa hợp lệ sau 2 lần gọi")
        return self._response(result)

    def _response(self, result: EcResult) -> EcResponse:
        return EcResponse(**result.model_dump(), skill_version=self.skill_version, model=self._call.model)
