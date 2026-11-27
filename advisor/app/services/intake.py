"""Bắt đầu hỏi hồ sơ: câu mời chọn cách điền, hai nút, và câu hỏi đầu tiên cần hỏi.

Chỉ chứa chữ và hàm thuần (không sửa state), nên không phụ thuộc chat_flow.py.
"""

from app.schemas.chat import ProfileField
from app.schemas.slm import SlmY
from app.services.profile_sync import DRAFT_KEYS
from app.services.replies import INTAKE_ASK, INTAKE_ASK_MISSING
from app.services.skill_rules import REQUIRED_B3, missing_fields, unanswered_fields

# question_id đặt trong state khi đang chờ học sinh chọn chat hay form
INTAKE_ID = "INTAKE"
# Các câu và nút của bước chọn cách điền hồ sơ nằm ở replies.py. Hai câu INTAKE_ASK, INTAKE_FORM không nằm trong QUESTIONS
# vì model chọn skill chưa được train trên chúng

# Khóa hồ sơ còn thiếu -> (bước hội thoại, mã câu hỏi trong QUESTIONS) dùng để hỏi
FIELD_QUESTION = {
    "major": ("B1", "B1.3"),
    "gpa": ("B2", "D2"),
    "english": ("B2", "E1"),
    "sat": ("B2", "D3"),
    "budget": ("B2", "F1"),
    "scholarship": ("B2", "F2"),
}


# Hồ sơ đã có thông tin nào trong phần form quản lý chưa (chat đã nhớ hoặc form đã điền)
def has_profile(profile: dict[str, ProfileField]) -> bool:
    return any(key in DRAFT_KEYS for key in profile)


# Câu mời chọn cách điền; đã có một phần hồ sơ thì nói còn thiếu mấy mục bắt buộc
def intake_ask(profile: dict[str, ProfileField], required: list[str]) -> str:
    # Chỉ đếm mục chưa nói: nói "còn thiếu 5 mục" ngay sau khi học sinh vừa cho GPA và IELTS là sai với số câu sẽ hỏi
    missing = len(unanswered_fields(profile, required))
    if not has_profile(profile) or missing == 0:
        return INTAKE_ASK
    return INTAKE_ASK_MISSING.format(missing=missing)


# Học sinh chọn gì: nhận bằng chữ trên nút (hoặc câu ngắn có chữ "form", "chat"), không nhờ model
def intake_choice(message: str) -> str | None:
    text = message.strip().lower()
    if len(text) > 60:
        return None
    if "form" in text:
        return "form"
    if "chat" in text:
        return "chat"
    return None


# Câu hỏi đầu tiên cần hỏi: thiếu thông tin cho B3 thì hỏi trước, hết mới tính tới B4
def next_question(profile: dict[str, ProfileField]) -> tuple[str, str] | None:
    missing = missing_fields(profile, REQUIRED_B3) or missing_fields(profile)
    return FIELD_QUESTION[missing[0]] if missing else None


# Mục tiêu tích sẵn trên form khi mở từ chat (link /analysis?from=chat&goal=...).
# Mã phải khớp FormGoal trong frontend/src/lib/form-goals.ts
FORM_GOALS = {"assessment": "danh_gia", "roadmap": "lo_trinh", "school_list": "goi_y_truong"}
# Sửa hồ sơ trong chat mà không có câu tư vấn nào đang chờ: form tích "Chỉ sửa hồ sơ", lưu xong về chat báo đã lưu
EDIT_ONLY_GOAL = "chi_sua"


# Câu tư vấn đang chờ hồ sơ cần những việc nào, giống cách GeneratedAdviceBranch chọn việc:
# - xin trường: SLM tách ra school_list, hoặc chỉ có so sánh / giải thích trường
# - việc của khuôn (đánh giá, lộ trình); không có việc nào thì làm cả hai
# Không có câu chờ, hay câu chờ không phải profile_review: trả rỗng, form để học sinh tự tích
def form_goals(pending: SlmY | None) -> list[str]:
    if pending is None or pending.type != "profile_review":
        return []
    # Nhập trong hàm: branches.py nhập chat_flow.py, chat_flow.py lại nhập file này
    from app.services.skill_template import TEMPLATE_TASKS, tasks_of

    tasks = pending.entities.get("tasks") or []
    template = [t for t in tasks if t in TEMPLATE_TASKS]
    others_only = bool(tasks) and not template and "school_list" not in tasks
    school_list = "school_list" in tasks or others_only
    work = tasks_of(template) if template or not school_list else []
    if school_list:
        work.append("school_list")
    order = list(FORM_GOALS)
    return [FORM_GOALS[t] for t in sorted(work, key=order.index)]
