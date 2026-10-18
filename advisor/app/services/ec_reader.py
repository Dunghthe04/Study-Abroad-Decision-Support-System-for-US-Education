"""Bậc ngoại khóa: Qwen đọc vai trò và phạm vi; Python tính q, m, bậc."""
"""
q = 0,4 × (phạm vi / 5) + 0,35 × vai trò + 0,25 × min(số tháng / 24, 1)
tong_hd = tổng q của tối đa 4 hoạt động cao nhất
bonus = min(0,5, tổng của tối đa 3 giải cao nhất × 0,05 × cấp)
m = min(1, (tong_hd + bonus) / 4)
bậc: m < 0,10 → 1; < 0,25 → 2; < 0,45 → 3; < 0,65 → 4; còn lại 5
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
# Các mức bậc ngoại khóa
TIER_CUTS = [0.10, 0.25, 0.45, 0.65] 
# Số hoạt động cao nhất
TOP_ACTIVITIES = 4
# Số giải thưởng cao nhất
TOP_AWARDS = 3
# Điểm giải thưởng
AWARD_POINT = 0.05
# Điểm giải thưởng tối đa
MAX_BONUS = 0.5
# Vai trò mặc định
DEFAULT_ROLE = 0.3

# Quy đổi điểm thành bậc
def tier_of(m: float) -> int:
    tier =1
    for cut in TIER_CUTS:
        if m >= cut:
            tier+=1
    return tier

#Công thức tính điểm cho mỗi hoạt động
def activity_q(role: float, scope: int, months: int) -> float:
    return round(0.4 * (scope/5) + 0.35 * role + 0.25 * min(months/24, 1), 2)

#Tính điểm cho mỗi hoạt động
def score_ec(reads: list[EcActivityRead], req: EcRequest) -> EcResponse:
    # tính điểm cho mỗi hoạt động
    act = []
    # tính điểm cho mỗi hoạt động
    #R: KẾT QUẢ ĐỌC CỦA AI
    #A: HOẠT ĐỘNG TRONG YÊU CẦU
    # zip: ghép R và A theo thứ tự
    # strict=True: yêu cầu R và A phải có cùng số lượng
    # zip(reads, req.act, strict=True): ghép R và A theo thứ tự
    # zip(reads, req.act, strict=True): ghép R và A theo thứ tự
    for r, a in zip(reads, req.act, strict=True):
        q = activity_q(r.role, r.scope, a.months)
        result = EcActivityResult(
            role=r.role,
            scope=r.scope,
            q=q
        )

        act.append(result)
    
    diem_hoat_dong = []
    for hoat_dong in act:
        diem_hoat_dong.append(hoat_dong.q)
    
    #Sắp xếp từ cao xuống thấp
    diem_hoat_dong.sort(reverse=True)
    #Chỉ lấy top hoạt động điểm cao nhất
    top_score_activities = diem_hoat_dong[:TOP_ACTIVITIES]
    #tong va lay tron 2 chu so
    tong_hd = round(sum(top_score_activities), 2)
    
    #Tinh giai thuong
    cap_giai_thuong = []
    for giai_thuong in req.awards:
        cap_giai_thuong.append(giai_thuong.level)
    
    #Sắp xếp từ cao xuống thấp
    cap_giai_thuong.sort(reverse=True)
    #Lấy 3 giai thuong cao nhat
    top_score_awards = cap_giai_thuong[:TOP_AWARDS]

    #Tính tổng điểm thưởng
    tong_diem_thuong = 0
    for cap in top_score_awards:
        tong_diem_thuong += cap * AWARD_POINT
    
    #max chi là 0,5
    bonus = round(min(MAX_BONUS, tong_diem_thuong), 2)

    #Tính điểm m, tối đa =1
    m = round(min(1, (tong_hd + bonus)/4),2)
    tier = tier_of(m)

    return act, tong_hd, bonus, tier

#Kiểm tra kết quả đọc của AI
def check_read(raw: dict[str, Any], req: EcRequest) -> list[EcActivityRead]:
    read = EcRead.model_validate(raw)
    if len(read.act) != len(req.act):
        raise ValueError(f"Có {len(req.act)} hoạt động nhưng Qwen trả {len(read.act)}")
    for i, (got, sent) in enumerate(zip(read.act, req.act, strict=True)):
        if got.scope > sent.scope:
            raise ValueError(f"Hoạt động {i}: Qwen nâng phạm vi {sent.scope} → {got.scope}")
    return read.act

#Lấy vai trò học sinh đã chọn, giữ nguyên phạm vi
def from_form(req: EcRequest) -> list[EcActivityRead]:
    """Qwen lỗi: lấy vai trò học sinh đã chọn, giữ nguyên phạm vi."""
    return [
        EcActivityRead(role=FORM_ROLES.get(a.role or "", DEFAULT_ROLE), scope=a.scope) for a in req.act
    ]
    
 
#Gọi Qwen đọc hoạt động
class EcReader:
    def __init__(self, llm: Any, model: str, skill_path: Path = SKILL_PATH) -> None:
        self._call = SkillCall(llm, model, skill_path)
        self.skill_version = self._call.skill_version
    
    async def read(self, req: EcRequest) -> EcResponse:
        # Bước 1: Mặc định chưa có kết quả AI
        reads = None

        # Bước 2: Nếu có hoạt động thì chuẩn bị dữ liệu gửi AI
        if req.act:
            activities = []

            for activity in req.act:
                # Loại bỏ role khỏi dữ liệu gửi AI, model_dump: chuyển đổi Pydantic object thành dictionary
                activity_data = activity.model_dump(exclude={"role"})
                activities.append(activity_data)

            user = {"act": activities}

            # Bước 3: Gọi AI đọc hoạt động và kiểm tra kết quả
            number_of_activities = len(req.act)
            schema = ec_schema(number_of_activities)

            def validate_result(raw):
                return check_read(raw, req)
            reads = await self._call.run(
                user,
                schema,
                validate_result
            )
        ai_used = reads is not None
        act, tong_hd, bonus, tier = score_ec(reads if ai_used else from_form(req), req)
        return EcResponse(
            act=act,
            tong_hd=tong_hd,
            bonus=bonus,
            tier=tier,
            skill_version=self.skill_version,
            model=self._call.model,
            ai_used=ai_used,
        )




