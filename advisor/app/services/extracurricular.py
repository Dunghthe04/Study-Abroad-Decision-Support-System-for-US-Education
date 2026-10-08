"""Điểm ngoại khóa thang 0–4 (thang của thầy), tính bằng công thức của nhóm.

Mỗi hoạt động i:
  q_i = 0.40·(mức ảnh hưởng/5) + 0.35·vai trò + 0.25·min(số tháng, 24)/24   (+0.10 nếu tổ chức uy tín, tối đa 1)
  vai trò: 0.3 thành viên · 0.6 phó / trưởng ban · 0.8 chủ nhiệm / đội trưởng · 1.0 sáng lập
EC = min(4, Σ (0.5 + 0.5·q_i)) trên tối đa 4 hoạt động có q cao nhất.

Thuộc tính lấy từ form (mức ảnh hưởng, số tháng) và từ chữ (vai trò, tổ chức uy tín):
bước này đọc vai trò bằng từ khóa; bước sau LLM đọc chữ thay cho từ khóa, công thức giữ nguyên.
"""

from typing import Literal

from pydantic import BaseModel, Field

Role = Literal["member", "deputy", "head", "founder"]

WEIGHT_LEVEL = 0.40
WEIGHT_ROLE = 0.35
WEIGHT_TIME = 0.25
ROLE_VALUE: dict[str, float] = {"member": 0.3, "deputy": 0.6, "head": 0.8, "founder": 1.0}
REPUTABLE_BONUS = 0.10
FULL_MONTHS = 24  # tham gia từ 2 năm trở lên được tối đa phần thời gian
UNKNOWN_TIME = 0.5  # học sinh không khai số tháng: lấy mức trung tính
MAX_ACTIVITIES = 4
MAX_SCORE = 4.0

# Từ khóa vai trò, xét theo thứ tự: "phó chủ nhiệm" phải ra phó chứ không ra chủ nhiệm
_ROLE_KEYWORDS: list[tuple[Role, tuple[str, ...]]] = [
    ("founder", ("sáng lập", "founder", "người lập")),
    ("deputy", ("phó", "vice", "trưởng ban", "trưởng nhóm")),
    ("head", ("chủ nhiệm", "chủ tịch", "đội trưởng", "trưởng", "president", "captain", "leader", "head")),
]


class Activity(BaseModel):
    """Một hoạt động đã ở dạng thuộc tính (chưa phải điểm)."""

    impact_level: int = Field(ge=1, le=5)  # 1 trường · 2 quận/huyện · 3 tỉnh/thành · 4 quốc gia · 5 quốc tế
    role: Role
    months: int | None = Field(default=None, ge=0)
    reputable_org: bool = False


class ActivityScore(BaseModel):
    quality: float  # q_i, 0–1
    points: float  # 0.5 + 0.5·q_i nếu được tính, 0 nếu nằm ngoài 4 hoạt động tốt nhất
    counted: bool


class ExtracurricularResult(BaseModel):
    score: float  # 0–4
    activities: list[ActivityScore]  # cùng thứ tự với đầu vào, để giải thích từng hoạt động


def role_from_text(text: str | None) -> Role:
    """Đọc vai trò từ chữ học sinh nhập bằng từ khóa; không khớp từ nào thì coi là thành viên."""
    lowered = (text or "").lower()
    for role, words in _ROLE_KEYWORDS:
        if any(word in lowered for word in words):
            return role
    return "member"


def activity_quality(a: Activity) -> float:
    time = UNKNOWN_TIME if a.months is None else min(a.months, FULL_MONTHS) / FULL_MONTHS
    q = WEIGHT_LEVEL * (a.impact_level / 5) + WEIGHT_ROLE * ROLE_VALUE[a.role] + WEIGHT_TIME * time
    if a.reputable_org:
        q += REPUTABLE_BONUS
    return min(q, 1.0)


def extracurricular_score(activities: list[Activity]) -> ExtracurricularResult:
    qualities = [activity_quality(a) for a in activities]
    best = set(sorted(range(len(qualities)), key=lambda i: qualities[i], reverse=True)[:MAX_ACTIVITIES])

    details = [
        ActivityScore(
            quality=round(q, 3),
            points=round(0.5 + 0.5 * q, 3) if i in best else 0.0,
            counted=i in best,
        )
        for i, q in enumerate(qualities)
    ]
    total = sum(d.points for d in details)
    return ExtracurricularResult(score=round(min(MAX_SCORE, total), 2), activities=details)
