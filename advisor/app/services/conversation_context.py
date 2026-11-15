"""Ngữ cảnh hội thoại: nhớ trường, mục và điều kiện tìm trường vừa nói để hiểu câu hỏi tiếp ("còn học bổng?", "còn Yale?", "chỉ lấy công lập").

Chỉ nhớ thực thể có cấu trúc (không nhớ chữ), nằm trong ChatState.context nên đi cùng trạng thái phiên và mất khi bắt đầu cuộc trò chuyện mới.
Hai nguồn quyết định câu có phải tiếp nối không:
  - Model (khóa follow_up trong entities, khi đã được huấn luyện với đầu vào context): follow_up = true thì điền phần câu còn thiếu từ ngữ cảnh.
  - Quy tắc dự phòng (khi model không trả follow_up): chỉ điền khi câu chắc chắn thiếu, nghiêng về "tự đứng được" để không gắn nhầm trường cho câu kiến thức chung.
Model chỉ chọn thực thể; số liệu luôn do .NET lấy từ cơ sở dữ liệu.
"""

import re
from typing import Any

from app.schemas.chat import ChatContext, ChatState
from app.schemas.slm import SlmTinResult, SlmY, task_of
from app.services.priority import unaccent

# Mục số liệu trường hay hỏi tiếp (bỏ dấu), khớp bộ từ khóa .NET ChatSchoolInfo
_INFO_WORDS = re.compile(
    r"\b(gpa|hoc phi|chi phi|hoc bong|ielts|toefl|duolingo|sat|act|ty le nhan|han nop|xep hang|ranking|"
    r"ty le tot nghiep|nganh|website|o dau|bang nao)\b"
)
# Chỉ lại trường đã nói
_POINTS_BACK = re.compile(r"\b(truong nay|truong do|truong ay|no|ben do|ben kia|noi do)\b")
# Câu hỏi định nghĩa hoặc kiến thức chung: không gắn với trường đang nói
_DEFINITION = re.compile(r"\b(la gi|nghia la gi|co nghia|khac gi|the nao la)\b")
# Mở đầu câu hỏi tiếp, chỉ đổi một thành phần ("còn Yale?", "thế còn ...", "vậy ...")
_FOLLOW_UP = re.compile(r"^(con|the con|vay con|vay|the)\b")


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
    return out or None


def _has_school(y: SlmY) -> bool:
    return bool(y.entities.get("schools"))


def _continue_knowledge(ctx: ChatContext, message: str, y: SlmY, model_says: bool | None) -> dict[str, Any]:
    """Các khóa cần điền thêm vào entities của một ý kiến thức. Rỗng nghĩa là câu tự đứng được hoặc chưa đủ chắc để điền."""
    plain = unaccent(message)
    fill: dict[str, Any] = {}
    if model_says is False:
        return fill
    if model_says is None and _DEFINITION.search(plain):
        return fill
    has_info_word = bool(_INFO_WORDS.search(plain))
    task = task_of(y.entities)

    # Tìm trường: điều kiện mới thêm vào bộ lọc cũ (bỏ điều kiện thì học sinh nói rõ), cách xếp mới thay cách cũ
    if task == "search":
        if model_says and ctx.filters:
            fill["filters"] = {**ctx.filters, **(y.entities.get("filters") or {})}
            if not y.entities.get("sort") and ctx.sort:
                fill["sort"] = ctx.sort
        return fill

    if not _has_school(y):
        # Thiếu trường: lấy từ ngữ cảnh khi model nói tiếp nối, hoặc (dự phòng) có mục hỏi / từ chỉ lại
        if model_says or has_info_word or _POINTS_BACK.search(plain):
            if len(ctx.schools) == 1:
                fill["schools"] = [ctx.schools[0]]
            elif len(ctx.schools) >= 2:
                fill["schools"] = list(ctx.schools)
                fill["tasks"] = ["compare_facts"]
            if fill and not y.entities.get("fields") and ctx.fields and not has_info_word and model_says:
                fill["fields"] = ctx.fields
        return fill

    # Có trường mà thiếu mục ("còn Yale?"): giữ mục vừa hỏi. Dự phòng chỉ khi câu ngắn và mở đầu kiểu hỏi tiếp
    if not y.entities.get("fields") and ctx.fields and not has_info_word:
        short = len(plain.split()) <= 6
        if model_says or (short and _FOLLOW_UP.search(plain)):
            fill["fields"] = list(ctx.fields)
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
        if y.type != "knowledge":
            out.append(y)
            continue
        flag = y.entities.get("follow_up")
        model_says = flag if isinstance(flag, bool) else None
        fill = _continue_knowledge(ctx, y.span if y.span else message, y, model_says)
        if fill:
            changed = True
            y = y.model_copy(update={"entities": {**y.entities, **fill}})
        out.append(y)
    return slm.model_copy(update={"intents": out}) if changed else slm


def update_context(state: ChatState, output: Any) -> None:
    """Ghi lại trường, mục và điều kiện của câu vừa trả lời (theo action cuối của lượt)."""
    data = output.action_data or {}
    ctx = state.context
    match output.action:
        case "school_info":
            if data.get("school"):
                ctx.schools = [str(data["school"])]
            if data.get("fields"):
                ctx.fields = list(data["fields"])
        case "school_compare":
            if data.get("schools"):
                ctx.schools = list(data["schools"])
            if data.get("fields"):
                ctx.fields = list(data["fields"])
        case "school_search":
            ctx.filters = data.get("model_filters") or None
            ctx.sort = data.get("model_sort") or None
            ctx.schools = []  # chưa chọn trường nào
        case "goal_seek":
            if data.get("school"):
                ctx.schools = [str(data["school"])]
