"""Mốc VEF mặc định (.NET AcademicAnalysisService.GetDefaultScaleConfig) cho test. Advisor không giữ bản sao mốc:
.NET gửi grade_scale mỗi lượt, nên test đổi GPA thang 10 phải tự truyền bảng này."""

from app.schemas.chat import GradeScaleRule
from app.services.skills.skill_danh_gia import gpa_table_of

VEF_POINTS = [
    (0, 0),
    (5.0, 2.0),
    (7.0, 3.0),
    (7.2, 3.1),
    (7.4, 3.2),
    (7.6, 3.3),
    (7.8, 3.4),
    (8.0, 3.5),
    (8.2, 3.55),
    (8.4, 3.6),
    (8.6, 3.65),
    (8.8, 3.7),
    (9.0, 3.75),
    (9.2, 3.8),
    (9.5, 3.85),
    (9.75, 3.9),
    (10, 4.0),
]
VEF_RULES = [GradeScaleRule(min_score=score, gpa4=gpa4) for score, gpa4 in VEF_POINTS]
VEF = gpa_table_of(VEF_RULES)
