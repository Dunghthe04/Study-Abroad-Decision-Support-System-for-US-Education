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
# Thông tin bắt buộc theo mốc, theo thứ tự hỏi (framework mục 4.1)
# B3 (đánh giá hồ sơ): bậc học muốn theo, GPA, tiếng Anh, ngân sách. Chưa cần ngành.
REQUIRED_B3 = ["target_level", "gpa", "english", "budget"]
# B4 (lộ trình, gợi ý trường): thêm ngành (để lọc trường) và học bổng
REQUIRED_B4 = [*REQUIRED_B3, "major", "scholarship"]
# Phải có giá trị đã xác nhận, không được bỏ qua hay "chưa rõ".
# major: B3 không đòi ngành nên "chưa rõ" vẫn qua B3, nhưng B4 đòi ngành thật.
MUST_HAVE_VALUE = {"target_level", "gpa", "budget", "major"}

# Skill cần hồ sơ -> danh sách thông tin skill đó đòi
REQUIRED_BY_SKILL = {"skill_danh_gia": REQUIRED_B3, "skill_lo_trinh": REQUIRED_B4}
# các skill cần danh sách trường đã đề xuất
NEEDS_RESULTS = {"skill_explain", "skill_kich_ban"}  # cần đã có danh sách trường


# Ghi lại 1 skill bị chặn và lý do
@dataclass
class Blocked:
    skill: str
    reason: Literal["thieu_ho_so", "thieu_nganh", "chua_co_danh_sach"]


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

    # Nếu có giá trị bắt buộc thì phải có giá trị đã xác nhận, không được bỏ qua
    if name in MUST_HAVE_VALUE:
        return field_value.status == "confirmed"
    return field_value.status in ("confirmed", "none", "unknown")


# Danh sách thông tin bắt buộc còn thiếu, theo thứ tự hỏi (mặc định tính theo mốc B4, mốc đầy đủ nhất)
def missing_fields(profile: dict[str, ProfileField], required: list[str] = REQUIRED_B4) -> list[str]:
    return [name for name in required if not is_answered(name, profile.get(name))]


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

    # 2 danh sách được chạy và chặn
    runnable: list[str] = []
    blocked: list[Blocked] = []
    missing_for_user: list[str] = []
    for skill in skills:
        # Mỗi skill đòi 1 danh sách riêng: đánh giá (B3) ít hơn lộ trình B4
        # Nếu skill là skill_danh_gia và skill_lo_trinh --> lấy ra các kiểu ầu của skill để check
        missing = (
            missing_fields(state.profile, REQUIRED_BY_SKILL[skill]) if skill in REQUIRED_BY_SKILL else []
        )
        if missing:
            # Thiếu ngành đứng đầu thì sau này hiện ô chọn ngành; thiếu thứ khác thì hỏi tiếp hồ sơ
            reason = "thieu_nganh" if missing[0] == "major" else "thieu_ho_so"
            blocked.append(Blocked(skill, reason))
            missing_for_user = missing_for_user or missing  # lấy danh sách thiếu của skill bị chặn đầu tiên
        elif skill in NEEDS_RESULTS and not has_results:
            blocked.append(Blocked(skill, "chua_co_danh_sach"))
        else:
            runnable.append(skill)
    return Plan(skills=runnable, source=source, blocked=blocked, missing=missing_for_user)
