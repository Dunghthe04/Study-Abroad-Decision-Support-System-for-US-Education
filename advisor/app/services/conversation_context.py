"""Ngữ cảnh hội thoại: nhớ trường, mục và điều kiện tìm trường vừa nói để hiểu câu hỏi tiếp ("còn học bổng?", "còn Yale?", "chỉ lấy công lập").

Chỉ nhớ thực thể có cấu trúc (không nhớ chữ), nằm trong ChatState.context nên đi cùng trạng thái phiên và mất khi bắt đầu cuộc trò chuyện mới.
Chỉ model quyết định câu có phải tiếp nối không (khóa follow_up trong entities, theo hợp đồng SLM): follow_up = true thì điền
phần câu còn thiếu từ ngữ cảnh; false hoặc không có thì câu tự đứng được. Không đoán lại bằng chữ trong câu: model sót thì sửa
bằng dữ liệu huấn luyện.
Model chỉ chọn thực thể; số liệu luôn do .NET lấy từ cơ sở dữ liệu.
"""

import re
from typing import Any

from app.schemas.chat import ChatContext, ChatState
from app.schemas.slm import SlmTinResult, SlmY, task_of
from app.services.priority import unaccent

# Mục số liệu trường (bỏ dấu), khớp bộ từ khóa .NET ChatSchoolInfo. Chỉ để KHÔNG chép mục cũ sang khi câu tiếp nối đã nêu
# mục khác mà model không tách ra
_INFO_WORDS = re.compile(
    r"\b(gpa|hoc phi|chi phi|hoc bong|ielts|toefl|duolingo|sat|act|ty le nhan|han nop|xep hang|ranking|"
    r"ty le tot nghiep|nganh|website|o dau|bang nao)\b"
)


def context_for_model(state: ChatState) -> dict[str, Any] | None:
    """Phần ngữ cảnh đưa vào đầu vào của model (khóa context); None khi chưa có gì để nói."""
    ctx = state.context
    out: dict[str, Any] = {}
    if ctx.schools:
        out["schools"] = ctx.schools
    if ctx.fields:
        out["fields"] = ctx.fields
    if ctx.filters:
        out["filters"] = ctx.filters
    if ctx.service:
        out["service"] = ctx.service
    if ctx.city:
        out["city"] = ctx.city
    if ctx.scenario:
        out["scenario"] = ctx.scenario
    return out or None


def _has_school(y: SlmY) -> bool:
    return bool(y.entities.get("schools"))


def _continue_knowledge(ctx: ChatContext, message: str, y: SlmY) -> dict[str, Any]:
    """Các khóa cần điền thêm vào entities của một ý kiến thức mà model nói là tiếp nối. Rỗng nếu không có gì để điền."""
    names_a_field = bool(_INFO_WORDS.search(unaccent(message)))

    # Tìm trường: điều kiện mới thêm vào bộ lọc cũ (bỏ điều kiện thì học sinh nói rõ), cách xếp mới thay cách cũ
    if task_of(y.entities) == "search":
        if not ctx.filters:
            return {}
        fill: dict[str, Any] = {"filters": {**ctx.filters, **(y.entities.get("filters") or {})}}
        if not y.entities.get("sort") and ctx.sort:
            fill["sort"] = ctx.sort
        return fill

    keep_fields = bool(ctx.fields) and not y.entities.get("fields") and not names_a_field
    if not _has_school(y):
        # Thiếu trường: lấy từ ngữ cảnh, đang nói nhiều trường thì thành câu so sánh
        if not ctx.schools:
            return {}
        fill = {"schools": list(ctx.schools)}
        if len(ctx.schools) >= 2:
            fill["tasks"] = ["compare_facts"]
        if keep_fields:
            fill["fields"] = list(ctx.fields)
        return fill

    # Có trường mà thiếu mục ("còn Yale?"): giữ mục vừa hỏi
    if not keep_fields:
        return {}
    fill = {"fields": list(ctx.fields)}
    if "tasks" not in y.entities and len(y.entities["schools"]) < 2:
        fill["tasks"] = ["school_info"]
    return fill


def contextualize(state: ChatState, message: str, slm: SlmTinResult) -> SlmTinResult:
    """Điền phần còn thiếu của các ý kiến thức từ ngữ cảnh. Không đổi gì khi chưa có ngữ cảnh hoặc câu tự đứng được."""
    ctx = state.context
    if not (ctx.schools or ctx.filters):
        return slm
    changed = False
    out: list[SlmY] = []
    for y in slm.intents:
        if y.type != "knowledge" or y.entities.get("follow_up") is not True:
            out.append(y)
            continue
        fill = _continue_knowledge(ctx, y.span if y.span else message, y)
        if fill:
            changed = True
            y = y.model_copy(update={"entities": {**y.entities, **fill}})
        out.append(y)
    return slm.model_copy(update={"intents": out}) if changed else slm


def _refocus_goal_scenario(ctx: ChatContext) -> None:
    """Câu tìm mức cần đạt nối tiếp lấy trường từ kịch bản đã nhớ, nên trường đó phải là trường đang nói tới.
    Đang nói đúng một trường thì kịch bản theo trường đó; đang nói nhiều trường hoặc không trường nào thì bỏ trường,
    câu nối tiếp không nêu tên sẽ được hỏi lại thay vì lấy trường cũ."""
    if not ctx.scenario or ctx.scenario.get("mode") != "goal_seeking":
        return
    if len(ctx.schools) == 1:
        ctx.scenario = {**ctx.scenario, "school": ctx.schools[0]}
    else:
        ctx.scenario = {key: value for key, value in ctx.scenario.items() if key != "school"}


def update_context(state: ChatState, output: Any) -> None:
    """Ghi lại trường, mục và điều kiện của câu vừa trả lời (theo action cuối của lượt)."""
    data = output.action_data or {}
    ctx = state.context
    match output.action:
        case "school_info":
            if data.get("school"):
                ctx.schools = [str(data["school"])]
                _refocus_goal_scenario(ctx)
            if data.get("fields"):
                ctx.fields = list(data["fields"])
        case "school_compare" | "school_fit_compare":
            if data.get("schools"):
                ctx.schools = list(data["schools"])
                _refocus_goal_scenario(ctx)
            if data.get("fields"):
                ctx.fields = list(data["fields"])
        case "school_search":
            ctx.filters = data.get("model_filters") or None
            ctx.sort = data.get("model_sort") or None
            ctx.schools = []  # chưa chọn trường nào
            _refocus_goal_scenario(ctx)
        case "goal_seek":
            if data.get("school"):
                ctx.schools = [str(data["school"])]
