"""Ngữ cảnh hội thoại: nhớ trường, mục và điều kiện tìm trường vừa nói để hiểu câu hỏi tiếp ("còn học bổng?", "còn Yale?", "chỉ lấy công lập").

Chỉ nhớ thực thể có cấu trúc (không nhớ chữ), nằm trong ChatState.context nên đi cùng trạng thái phiên và mất khi bắt đầu cuộc trò chuyện mới.
Hai nguồn quyết định câu có phải tiếp nối không:
  - Model (khóa tiep_noi trong thuc_the, khi đã được huấn luyện với đầu vào dang_noi_ve): tiep_noi = true thì điền phần câu còn thiếu từ ngữ cảnh.
  - Quy tắc dự phòng (khi model không trả tiep_noi): chỉ điền khi câu chắc chắn thiếu, nghiêng về "tự đứng được" để không gắn nhầm trường cho câu kiến thức chung.
Model chỉ chọn thực thể; số liệu luôn do .NET lấy từ cơ sở dữ liệu.
"""

import re
from typing import Any

from app.schemas.chat import ChatContext, ChatState
from app.schemas.slm import SlmTinResult, SlmY
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
    """Phần ngữ cảnh đưa vào đầu vào của model (khóa dang_noi_ve); None khi chưa có gì để nói."""
    ctx = state.context
    out: dict[str, Any] = {}
    if ctx.schools:
        out["truong"] = ctx.schools
    if ctx.fields:
        out["muc"] = ctx.fields
    if ctx.bo_loc:
        out["bo_loc"] = ctx.bo_loc
    return out or None


def _has_school(y: SlmY) -> bool:
    return bool(y.thuc_the.get("truong") or y.thuc_the.get("cac_truong"))


def _continue_knowledge(ctx: ChatContext, message: str, y: SlmY, model_says: bool | None) -> dict[str, Any]:
    """Các khóa cần điền thêm vào thuc_the của một ý kiến thức. Rỗng nghĩa là câu tự đứng được hoặc chưa đủ chắc để điền."""
    plain = unaccent(message)
    fill: dict[str, Any] = {}
    if model_says is False:
        return fill
    if model_says is None and _DEFINITION.search(plain):
        return fill
    has_info_word = bool(_INFO_WORDS.search(plain))
    task = y.thuc_the.get("nhiem_vu")

    # Tìm trường: điều kiện mới thêm vào bộ lọc cũ (bỏ điều kiện thì học sinh nói rõ), cách xếp mới thay cách cũ
    if task == "tim_kiem":
        if model_says and ctx.bo_loc:
            fill["bo_loc"] = {**ctx.bo_loc, **(y.thuc_the.get("bo_loc") or {})}
            if not y.thuc_the.get("sap_xep") and ctx.sort:
                fill["sap_xep"] = ctx.sort
        return fill

    if not _has_school(y):
        # Thiếu trường: lấy từ ngữ cảnh khi model nói tiếp nối, hoặc (dự phòng) có mục hỏi / từ chỉ lại
        if model_says or has_info_word or _POINTS_BACK.search(plain):
            if len(ctx.schools) == 1:
                fill["truong"] = ctx.schools[0]
            elif len(ctx.schools) >= 2:
                fill["cac_truong"] = list(ctx.schools)
                fill["nhiem_vu"] = "so_sanh"
            if fill and not y.thuc_the.get("muc") and ctx.fields and not has_info_word and model_says:
                fill["muc"] = ctx.fields
        return fill

    # Có trường mà thiếu mục ("còn Yale?"): giữ mục vừa hỏi. Dự phòng chỉ khi câu ngắn và mở đầu kiểu hỏi tiếp
    if not y.thuc_the.get("muc") and ctx.fields and not has_info_word:
        short = len(plain.split()) <= 6
        if model_says or (short and _FOLLOW_UP.search(plain)):
            fill["muc"] = list(ctx.fields)
            if "nhiem_vu" not in y.thuc_the and not y.thuc_the.get("cac_truong"):
                fill["nhiem_vu"] = "tra_cuu"
    return fill


def contextualize(state: ChatState, message: str, slm: SlmTinResult) -> SlmTinResult:
    """Điền phần còn thiếu của các ý kiến thức từ ngữ cảnh. Không đổi gì khi chưa có ngữ cảnh hoặc câu tự đứng được."""
    ctx = state.context
    if not (ctx.schools or ctx.bo_loc):
        return slm
    changed = False
    out: list[SlmY] = []
    for y in slm.cac_y:
        if y.type != "kien_thuc":
            out.append(y)
            continue
        flag = y.thuc_the.get("tiep_noi")
        model_says = flag if isinstance(flag, bool) else None
        fill = _continue_knowledge(ctx, y.doan if y.doan else message, y, model_says)
        if fill:
            changed = True
            y = y.model_copy(update={"thuc_the": {**y.thuc_the, **fill}})
        out.append(y)
    return slm.model_copy(update={"cac_y": out}) if changed else slm


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
            ctx.bo_loc = data.get("bo_loc") or None
            ctx.sort = data.get("sap_xep") or None
            ctx.schools = []  # chưa chọn trường nào
        case "goal_seek":
            if data.get("school"):
                ctx.schools = [str(data["school"])]
