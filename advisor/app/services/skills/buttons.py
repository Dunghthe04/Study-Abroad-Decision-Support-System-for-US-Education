"""B5. Nút gợi ý (quick_replies) và widget đi kèm câu hỏi. Chữ trên nút giống form (profile-flow-options.ts).

Bấm nút thì frontend gửi đúng chữ trên nút như một tin nhắn. Skill nhận ra chữ đó và ghi thẳng giá trị
vào hồ sơ, không cần gọi Qwen.
"""

from dataclasses import dataclass, field

from app.schemas.chat import ChatState, ProfileField, QuickReply, Widget
from app.services.chat_flow import SkillOutput, ask


def confirmed(value: object) -> ProfileField:
    return ProfileField(value=value, status="confirmed")


# Học sinh nói "không có" (chưa thi, chưa có hoạt động...)
NONE = ProfileField(value=None, status="none")
# Học sinh nói "chưa rõ" (ngành)
UNKNOWN = ProfileField(value=None, status="unknown")


# Một nút: chữ trên nút (cũng là tin nhắn gửi đi) và các thông tin ghi vào hồ sơ khi bấm
@dataclass(frozen=True)
class Button:
    label: str
    values: dict[str, ProfileField] = field(default_factory=dict)


# Câu tùy chọn có thêm nút này; bấm thì không ghi gì, hỏi câu tiếp theo
SKIP = Button("Bỏ qua")

# Mức ngân sách và học bổng: mã và chữ giống DEFAULT_BUDGET_TIERS, SCHOLARSHIP_NEED_OPTIONS của form
BUDGET_LABELS = {
    "tier_under_20k": "Dưới 20.000 USD",
    "tier_20k_35k": "20.000 – 35.000 USD",
    "tier_35k_50k": "35.000 – 50.000 USD",
    "tier_50k_70k": "50.000 – 70.000 USD",
    "tier_above_70k": "Trên 70.000 USD",
}
SCHOLARSHIP_LABELS = {
    "optional": "Không cần học bổng",
    "preferred": "Nên có học bổng",
    "mandatory": "Bắt buộc có học bổng",
}

# Câu hỏi → các nút đi kèm
BUTTONS = {
    "B1.2": (
        Button("Đại học", {"target_level": confirmed("undergraduate")}),
        Button("Cao đẳng cộng đồng", {"target_level": confirmed("community_college")}),
        Button("Thạc sĩ", {"target_level": confirmed("master")}),
        Button("Tiến sĩ", {"target_level": confirmed("phd")}),
    ),
    "B1.3": (Button("Em chưa rõ ngành", {"major": UNKNOWN}),),
    "D3": (Button("Em chưa thi SAT/ACT", {"sat": NONE, "act": NONE}),),
    "E1": (Button("Em chưa thi tiếng Anh", {"english": NONE}),),
    "E2": (Button("Chưa có kế hoạch thi", {"english_plan": confirmed({"planned": False, "date": None})}),),
    "F1": tuple(Button(label, {"budget": confirmed(tier)}) for tier, label in BUDGET_LABELS.items()),
    "F2": tuple(
        Button(label, {"scholarship": confirmed(level)}) for level, label in SCHOLARSHIP_LABELS.items()
    ),
    "F3": (
        Button("Bố mẹ", {"funding_source": confirmed("parents")}),
        Button("Họ hàng", {"funding_source": confirmed("relatives")}),
        Button("Tự chi trả", {"funding_source": confirmed("self")}),
        Button("Vay", {"funding_source": confirmed("loan")}),
    ),
    "D4": (Button("Em chưa có hoạt động", {"activities": NONE}),),
    "D5": (Button("Em chưa có giải", {"awards": NONE}),),
    "N1": (Button("Không có người thân ở Mỹ", {"relatives_state": NONE}),),
    "N3": (
        Button("Công lập", {"school_type": confirmed("public")}),
        Button("Tư thục", {"school_type": confirmed("private")}),
        Button("Trường nào cũng được", {"school_type": confirmed("all")}),
    ),
    "N4": (
        Button("Chưa từng", {"visa_refused": confirmed(False)}),
        Button("Đã từng bị từ chối", {"visa_refused": confirmed(True)}),
    ),
}

# Câu hỏi có widget: frontend vẽ ô chọn, chọn xong thì gửi chữ (label) như bấm nút
WIDGETS = {"F1": ("budget", BUDGET_LABELS), "F2": ("scholarship", SCHOLARSHIP_LABELS)}


def buttons_of(question_id: str | None, can_skip: bool = False) -> tuple[Button, ...]:
    found = BUTTONS.get(question_id or "", ())
    return (*found, SKIP) if can_skip else found


def quick_replies(question_id: str | None, can_skip: bool = False) -> list[QuickReply]:
    return [QuickReply(label=b.label, value=b.label) for b in buttons_of(question_id, can_skip)]


def widget(question_id: str | None) -> Widget | None:
    if question_id not in WIDGETS:
        return None
    kind, labels = WIDGETS[question_id]
    return Widget(type=kind, data={"options": [{"value": v, "label": label} for v, label in labels.items()]})


def same_text(a: str, b: str) -> bool:
    return a.strip().strip(".!").lower() == b.strip().strip(".!").lower()


# Tin nhắn trùng chữ một nút của câu đang hỏi thì trả nút đó, không thì None
def pressed(question_id: str | None, message: str, can_skip: bool = False) -> Button | None:
    return next((b for b in buttons_of(question_id, can_skip) if same_text(b.label, message)), None)


# Ghi các thông tin của nút vào hồ sơ (ghi đè thông tin cũ)
def press(state: ChatState, button: Button) -> None:
    for name, value in button.values.items():
        state.profile[name] = value.model_copy()


# Hỏi một câu kèm nút và widget của câu đó. text: câu hỏi khác câu chuẩn (câu hỏi lại của validators)
def ask_with_buttons(
    state: ChatState, question_id: str, text: str | None = None, can_skip: bool = False
) -> SkillOutput:
    question = ask(state, question_id)
    return SkillOutput(
        text or question, quick_replies=quick_replies(question_id, can_skip), widget=widget(question_id)
    )
