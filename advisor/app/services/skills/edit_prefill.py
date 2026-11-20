"""Sửa hồ sơ trong chat: câu hỏi lại kèm giá trị cũ để học sinh biết "Giữ như cũ" là giữ gì.

- Câu có ô nhập (widget): gửi kèm `current` (giá trị thô các ô hồ sơ của câu, frontend hiện khung "Hiện tại")
  và `initial` (giá trị điền sẵn vào ô chọn cho câu một giá trị: cấp học, kỳ nhập học, GPA, kế hoạch thi...).
- Câu chỉ có nút: ghi giá trị cũ ngay trong câu hỏi.
Câu danh sách (chứng chỉ, hoạt động, giải thưởng, bang) chỉ hiện "Hiện tại", không điền sẵn.
"""

from typing import Any

from app.schemas.chat import ChatState
from app.services.chat_flow import SkillOutput
from app.services.skills import buttons
from app.services.skills.validators import CERT_TYPES


def _value(state: ChatState, name: str) -> Any:
    field = state.profile.get(name)
    return None if field is None else field.value


def current_fields(state: ChatState, names: list[str]) -> dict[str, dict[str, Any]]:
    return {
        name: {"value": state.profile[name].value, "status": state.profile[name].status}
        for name in names
        if name in state.profile
    }


# Kế hoạch thi {"planned", "test_type", "target_score", "date": "YYYY-MM"} → các ô type/score/month/year của E2, E3
def _plan_initial(plan: Any) -> dict[str, str]:
    if not isinstance(plan, dict) or not plan.get("planned") or not plan.get("test_type"):
        return {}
    spec = CERT_TYPES.get(str(plan["test_type"]).lower())
    if spec is None:
        return {}
    initial = {"type": spec.name}
    if plan.get("target_score") is not None:
        initial["score"] = f"{plan['target_score']:g}" if isinstance(plan["target_score"], float) else str(plan["target_score"])
    date = str(plan.get("date") or "")
    if len(date) >= 7 and date[4] == "-":
        initial["year"], initial["month"] = date[:4], str(int(date[5:7]))
    return initial


# Giá trị điền sẵn cho ô chọn của câu (khóa = tên ô trong buttons.select_fields / widget); rỗng = không điền
def initial_values(state: ChatState, question_id: str) -> dict[str, Any]:
    if question_id == "B1.1":
        level = buttons.CURRENT_LEVEL_LABELS.get(_value(state, "current_level") or "")
        grade = _value(state, "current_grade")
        if not level:
            return {}
        level_code = _value(state, "current_level")
        ok_grade = grade in buttons.GRADE_OPTIONS.get(level_code, ())
        return {"level": level, **({"grade": grade} if ok_grade else {})}
    if question_id == "B1.4":
        parts = str(_value(state, "intake") or "").split()
        return {"term": parts[0], "year": parts[1]} if len(parts) == 2 else {}
    if question_id == "D1":
        label = buttons.EDUCATION_SYSTEM_LABELS.get(_value(state, "education_system") or "")
        return {"system": label} if label else {}
    if question_id == "D2":
        gpa = _value(state, "gpa")
        if not isinstance(gpa, dict) or gpa.get("value") is None:
            return {}
        scale = gpa.get("scale")
        initial = {"gpa": f"{float(gpa['value']):g}"}
        if scale is not None:
            initial["scale"] = f"{float(scale):g}"
        return initial
    if question_id == "F3":
        label = buttons.FUNDING_SOURCE_LABELS.get(_value(state, "funding_source") or "")
        return {"source": label} if label else {}
    if question_id == "E2":
        return _plan_initial(_value(state, "english_plan"))
    if question_id == "E3":
        return _plan_initial(_value(state, "standardized_plan"))
    if question_id == "F2":
        need = _value(state, "scholarship")
        return {"need": need} if need in buttons.SCHOLARSHIP_LABELS else {}
    if question_id == "F1":
        vnd = _value(state, "budget_vnd")
        return {"amountVnd": vnd} if isinstance(vnd, (int, float)) and state.exact_budget else {}
    return {}


# Chữ của nút trùng giá trị đang lưu (câu chỉ có nút: B1.2, N3, N4...)
def _button_label(state: ChatState, question_id: str) -> str | None:
    for button in buttons.BUTTONS.get(question_id, ()):
        if button.values and all(
            name in state.profile
            and state.profile[name].value == field.value
            and (state.profile[name].status == "none") == (field.status == "none")
            for name, field in button.values.items()
        ):
            return button.label
    return None


def add_current(state: ChatState, question_id: str, names: list[str], output: SkillOutput) -> SkillOutput:
    current = current_fields(state, names)
    if not current:
        return output
    if output.widget is not None:
        output.widget.data = {**output.widget.data, "current": current}
        initial = initial_values(state, question_id)
        if initial:
            output.widget.data["initial"] = initial
        return output
    label = _button_label(state, question_id)
    if label:
        output.answer = f"{output.answer}\n\nHiện tại: {label}."
    return output
