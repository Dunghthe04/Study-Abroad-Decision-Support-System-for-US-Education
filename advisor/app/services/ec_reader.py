"""Bậc ngoại khóa: AI đọc vai trò, phạm vi theo skill_ec.md; Python tính q, m, bậc theo GOI_Y_TRUONG_TONG_QUAN.

AI chỉ làm phần đọc hiểu vì khi để AI tự tính, q và bonus thường sai (bộ đánh giá lượt ②: AI tính đúng 33–48%).
"""

from pathlib import Path
from typing import Any

from app.schemas.ec import (
    FORM_ROLES,
    EcActivityRead,
    EcActivityResult,
    EcRead,
    EcRequest,
    EcResponse,
    ec_schema,
)
from app.services.skill_call import SKILLS_DIR, SkillCall

SKILL_PATH = SKILLS_DIR / "skill_ec.md"
# Mốc chia bậc theo m: m < 0.10 → 1, < 0.25 → 2, < 0.45 → 3, < 0.65 → 4, còn lại 5
TIER_CUTS = [0.10, 0.25, 0.45, 0.65]
TOP_ACTIVITIES = 4  # cộng tối đa 4 hoạt động cao nhất
TOP_AWARDS = 3  # cộng tối đa 3 giải cấp cao nhất
AWARD_POINT = 0.05  # điểm một giải = 0,05 × cấp
MAX_BONUS = 0.5
DEFAULT_ROLE = 0.3  # không rõ vai trò thì coi là thành viên


def tier_of(m: float) -> int:
    tier = 1
    for cut in TIER_CUTS:
        if m >= cut:
            tier += 1
    return tier


def activity_q(role: float, scope: int, months: int) -> float:
    """Bước N2: q = 0,4 × phạm vi/5 + 0,35 × vai trò + 0,25 × min(tháng/24, 1)."""
    return round(0.4 * scope / 5 + 0.35 * role + 0.25 * min(months / 24, 1), 2)


def score_ec(
    reads: list[EcActivityRead], req: EcRequest
) -> tuple[list[EcActivityResult], float, float, float, int]:
    """Bước N2–N6: q từng hoạt động → tổng 4 hoạt động cao nhất → thưởng giải → m → bậc."""
    act = [
        EcActivityResult(role=r.role, scope=r.scope, q=activity_q(r.role, r.scope, a.months))
        for r, a in zip(reads, req.act, strict=True)
    ]
    tong_hd = round(sum(sorted((x.q for x in act), reverse=True)[:TOP_ACTIVITIES]), 2)
    levels = sorted((w.level for w in req.awards), reverse=True)[:TOP_AWARDS]
    bonus = round(min(MAX_BONUS, sum(AWARD_POINT * lv for lv in levels)), 2)
    m = round(min(1.0, (tong_hd + bonus) / 4), 2)
    return act, tong_hd, bonus, m, tier_of(m)


def check_read(raw: dict[str, Any], req: EcRequest) -> list[EcActivityRead]:
    """Kiểm tra JSON AI trả về. Sai thì ném ValueError để gọi lại."""
    read = EcRead.model_validate(raw)  # sai khuôn (thiếu key, vai trò lạ) → ValidationError
    if len(read.act) != len(req.act):
        raise ValueError(f"Có {len(req.act)} hoạt động nhưng AI trả {len(read.act)}")
    for i, (got, sent) in enumerate(zip(read.act, req.act, strict=True)):
        # Luật quan trọng: AI chỉ được giữ hoặc hạ phạm vi học sinh khai, không được nâng
        if got.scope > sent.scope:
            raise ValueError(f"Hoạt động {i}: AI nâng phạm vi {sent.scope} → {got.scope}")
    return read.act


def from_form(req: EcRequest) -> list[EcActivityRead]:
    """Dự phòng khi AI lỗi: vai trò học sinh chọn ở form, phạm vi học sinh khai."""
    return [EcActivityRead(role=FORM_ROLES.get(a.role or "", DEFAULT_ROLE), scope=a.scope) for a in req.act]


class EcReader:
    def __init__(self, llm: Any, model: str, skill_path: Path = SKILL_PATH) -> None:
        self._call = SkillCall(llm, model, skill_path)
        self.skill_version = self._call.skill_version

    async def read(self, req: EcRequest) -> EcResponse:
        reads: list[EcActivityRead] | None = None
        if req.act:
            # Chỉ gửi mô tả, phạm vi, số tháng; vai trò ở form không gửi để AI tự đọc từ text
            user = {"act": [a.model_dump(exclude={"role"}) for a in req.act]}
            reads = await self._call.run(user, ec_schema(len(req.act)), lambda raw: check_read(raw, req))
        ai_used = reads is not None
        act, tong_hd, bonus, m, tier = score_ec(reads if ai_used else from_form(req), req)
        return EcResponse(
            act=act,
            tong_hd=tong_hd,
            bonus=bonus,
            m=m,
            tier=tier,
            skill_version=self.skill_version,
            model=self._call.model,
            ai_used=ai_used,
        )
