"""Luật code quyết định skill nào được chạy cho một tin nhắn (framework v5, mục 3.3 và 3.4).

Model chỉ GỢI Ý danh sách skill kèm xác suất. Code quyết định chạy thật skill nào:
  1. Model không chắc (danh sách rỗng) thì dùng skill của bước hiện tại.
  2. ngoai_le đi cùng nhãn khác thì bỏ ngoai_le.
  3. Tối đa 2 skill (lấy 2 nhãn xác suất cao nhất).
  4. Xếp theo thứ tự chạy cố định.
  5. Skill chưa đủ điều kiện (thiếu hồ sơ, chưa có danh sách trường) thì chưa chạy.
bộ điều phối skill trong hệ thống tư vấn du học Mỹ của bạn. Nó đứng giữa mô hình AI dự đoán và phần thực thi skill, có nhiệm vụ quyết định:
- Model dự đoán những skill nào?
- Skill nào được phép chạy thật?
- Skill nào phải tạm hoãn vì thiếu thông tin?
- Nếu có nhiều skill thì skill nào chạy trước?
"""

from dataclasses import dataclass, field
from typing import Literal

from app.schemas.chat import ChatState, ProfileField

# Bước hiện tại -> skill của bước đó (dùng khi model không chắc)
STEP_SKILL = {
    "B0": "skill_nhu_cau",
    "B1": "skill_bac_hoc",
    "B2": "skill_ho_so",
    "B3": "skill_danh_gia",
    "B4": "skill_lo_trinh",
    "B5": "skill_tra_loi",
    "B6": "skill_chuyen_trung_tam",
}

# Thứ tự chạy khi có 2 skill: skill của bước trước, rồi trả lời kiến thức, rồi dịch vụ, rồi kết thúc
ORDER = [
    "skill_nhu_cau",
    "skill_bac_hoc",
    "skill_ho_so",
    "skill_danh_gia",
    "skill_lo_trinh",
    "skill_explain",
    "skill_kich_ban",
    "skill_tra_loi",
    "skill_chuyen_trung_tam",
    "ngoai_le",
    "ket_thuc",
]

MAX_SKILLS = 2
# Thông tin bắt buộc trước khi chạy B3, B4, theo thứ tự hỏi (framework mục 4.1): D1, D2, E1, F1, F2, D4
REQUIRED_FIELDS = ["education_system", "gpa", "english", "budget", "scholarship_need", "activities"]
# GPA và ngân sách không được bỏ qua: phải có giá trị đã xác nhận
MUST_HAVE_VALUE = {"gpa", "budget"}

# các skill cần hồ sơ đầy đủ
NEEDS_PROFILE = {"skill_danh_gia", "skill_lo_trinh"}  # cần đủ hồ sơ
# các skill cần danh sách trường đã đề xuất
NEEDS_RESULTS = {"skill_explain", "skill_kich_ban"}  # cần đã có danh sách trường


# Ghi lại 1 skill bị chặn và lý do
@dataclass
class Blocked:
    skill: str
    reason: Literal["thieu_ho_so", "chua_co_danh_sach"]


# Gói kết quả sau khi kiểm tra
@dataclass
class Plan:
    skills: list[str]  # skill chạy thật, đã xếp theo thứ tự chạy
    source: Literal["model", "fallback_step"]  # model chọn hay dùng skill bước hiện tại
    blocked: list[Blocked] = field(default_factory=list)  # skill nào bị chặn và vì sao
    missing: list[str] = field(default_factory=list)


# Kiểm tra thông tin
# Trường thông tin này đã được xử lý xong theo quy tắc của hệ thống chưa?
def is_answered(name: str, field_value: ProfileField | None) -> bool:
    if field_value is None:
        return False

    # Nếu có giá trị + name là gpa or ngân sách thì đã xác nhận chưa
    if name in MUST_HAVE_VALUE:
        return field_value.status == "confirmed"
    return field_value.status in ("confirmed", "none", "unknown")


# Danh sách thông tin bắt buộc còn thiếu, theo thứ tự hỏi
def missing_fields(profile: dict[str, ProfileField]) -> list[str]:
    return [name for name in REQUIRED_FIELDS if not is_answered(name, profile.get(name))]


# Predicted: nhãn model chọn,Đã xếp xác suất cao trước
def plan_skills(state: ChatState, predicted: list[str], has_results: bool = False) -> Plan:
    # Bỏ nhãn lạ
    known = [s for s in predicted if s in ORDER]
    if known:
        source = "model"
        skills = known
    else:
        source = "fallback_step"
        skills = [STEP_SKILL.get(state.current_step, "ngoai_le")]

    if len(skills) > 1:
        skills = [s for s in skills if s != "ngoai_le"] or ["ngoai_le"]

    skills = skills[:MAX_SKILLS]  # lấy 2 nhãn xác suất cao nhất
    skills.sort(key=ORDER.index)  # sắp theo thứ tự chạy

    missing = missing_fields(state.profile)
    # 2 danh sách được chạy và chặn
    runnable: list[str] = []
    blocked: list[Blocked] = []
    for skill in skills:
        # Nếu skill cần hồ sơ đầy đủ nhưng hồ sơ thiếu
        if skill in NEEDS_PROFILE and missing:
            blocked.append(Blocked(skill, "thieu_ho_so"))
        elif skill in NEEDS_RESULTS and not has_results:
            blocked.append(Blocked(skill, "chua_co_danh_sach"))
        else:
            runnable.append(skill)

    missing_for_user = missing if any(b.reason == "thieu_ho_so" for b in blocked) else []
    return Plan(skills=runnable, source=source, blocked=blocked, missing=missing_for_user)
