"""B5. Nút gợi ý (quick_replies) và widget đi kèm câu hỏi. Chữ trên nút giống form (profile-flow-options.ts).

Bấm nút thì frontend gửi đúng chữ trên nút như một tin nhắn. Skill nhận ra chữ đó và ghi thẳng giá trị
vào hồ sơ, không cần gọi Qwen.
"""

from dataclasses import dataclass, field
from datetime import date

from app.schemas.chat import ChatState, ProfileField, QuickReply, Widget
from app.services.chat_flow import SkillOutput, ask
from app.services.skills import exchange_rate
from app.services.skills.validators import ALEVEL_GRADES, CERT_LIMITS, CERT_TYPES


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
# Hệ chương trình và nguồn tài chính: mã và chữ giống EDUCATION_SYSTEM_OPTIONS, FUNDING_SOURCE_OPTIONS của form
EDUCATION_SYSTEM_LABELS = {
    "standard": "Hệ chuẩn công lập Việt Nam",
    "specialized": "Hệ chuyên (Trường THPT Chuyên / Năng khiếu)",
    "dual_degree": "Hệ song bằng (Việt - Anh / Cambridge)",
    "international": "Hệ quốc tế hoàn toàn (IB, AP, Cambridge)",
    "private": "Dân lập / Tư thục chất lượng cao",
    "other": "Hệ khác",
}
FUNDING_SOURCE_LABELS = {
    "family_support": "Gia đình hỗ trợ toàn bộ / một phần",
    "personal_savings": "Tiết kiệm cá nhân",
    "bank_loan": "Vay vốn du học ngân hàng",
    "scholarship": "Trông đợi vào học bổng",
    "other": "Nguồn tài chính khác",
}
SCHOLARSHIP_LABELS = {
    "mandatory": "Bắt buộc",
    "preferred": "Nên có",
    "optional": "Không cần",
}
# Loại trường: chữ và thứ tự giống SCHOOL_TYPE_OPTIONS của form
SCHOOL_TYPE_LABELS = {"all": "Không lọc", "public": "Công lập", "private": "Tư thục"}
# Vai trò, phạm vi của hoạt động / giải thưởng: chữ giống ACTIVITY_ROLE_OPTIONS, IMPACT_LEVEL_OPTIONS của form
ROLE_LABELS = {
    "founder": "Sáng lập",
    "leader": "Trưởng / Chủ nhiệm",
    "vice_leader": "Phó",
    "member": "Thành viên",
}
IMPACT_LABELS = {
    "school": "Trường",
    "district": "Quận / Huyện",
    "province": "Tỉnh / Thành phố",
    "national": "Quốc gia",
    "international": "Quốc tế",
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
    # Chưa thi thì tích luôn ô "sẽ thi sau" của form; E2 hỏi tiếp tháng dự kiến (bỏ qua thì ô vẫn tích)
    "E1": (
        Button(
            "Em chưa thi tiếng Anh",
            {"english": NONE, "english_plan": confirmed({"planned": True, "date": None})},
        ),
    ),
    "F1": tuple(Button(label, {"budget": confirmed(tier)}) for tier, label in BUDGET_LABELS.items()),
    "F2": tuple(
        Button(label, {"scholarship": confirmed(level)}) for level, label in SCHOLARSHIP_LABELS.items()
    ),
    "D4": (Button("Em chưa có hoạt động", {"activities": NONE}),),
    "D5": (Button("Em chưa có giải", {"awards": NONE}),),
    "N1": (Button("Không có người thân ở Mỹ", {"relatives_state": NONE}),),
    "N3": tuple(
        Button(label, {"school_type": confirmed(code)}) for code, label in SCHOOL_TYPE_LABELS.items()
    ),
    "N4": (
        Button("Chưa từng", {"visa_refused": confirmed(False)}),
        Button("Đã từng bị từ chối", {"visa_refused": confirmed(True)}),
    ),
}

# Câu hỏi có widget: frontend vẽ ô chọn, chọn xong thì gửi chữ (label) như bấm nút
WIDGETS = {"F1": ("budget", BUDGET_LABELS), "F2": ("scholarship", SCHOLARSHIP_LABELS)}

# Cấp học và lớp: chữ giống EDUCATION_LEVEL_OPTIONS, getGradeOptionsForLevel của form
CURRENT_LEVEL_LABELS = {
    "middle_school": "Trung học cơ sở",
    "high_school": "Trung học phổ thông",
    "college_university": "Cao đẳng / Đại học",
    "master": "Thạc sĩ",
}
GRADE_OPTIONS = {
    "middle_school": ("Lớp 6", "Lớp 7", "Lớp 8", "Lớp 9"),
    "high_school": ("Lớp 10", "Lớp 11", "Lớp 12"),
    "college_university": ("Năm 1", "Năm 2", "Năm 3", "Năm 4", "Năm cuối", "Đã tốt nghiệp"),
    "master": ("Năm 1", "Năm 2", "Đã tốt nghiệp"),
}
# Kỳ nhập học: chữ giống INTAKE_TERM_OPTIONS của form, năm từ năm nay tới 5 năm sau
INTAKE_TERM_LABELS = {
    "Fall": "Kỳ mùa thu (Fall)",
    "Spring": "Kỳ mùa xuân (Spring)",
    "Summer": "Kỳ mùa hè (Summer)",
}
YEARS_AHEAD = 6
# Năm bắt đầu hoạt động / năm đạt giải: từ năm nay lùi lại ngần này năm
YEARS_BACK = 8


def years(today: date | None = None) -> list[str]:
    this_year = (today or date.today()).year
    return [str(this_year + i) for i in range(YEARS_AHEAD)]


def past_years(today: date | None = None) -> list[str]:
    this_year = (today or date.today()).year
    return [str(this_year - i) for i in range(YEARS_BACK)]


# Lựa chọn trong ô chọn (widget "select") mà không hiện thành nút: chọn xong frontend gửi chữ ghép,
# trùng chữ thì ghi thẳng vào hồ sơ như bấm nút
def choices_of(question_id: str | None) -> tuple[Button, ...]:
    if question_id == "B1.1":
        return tuple(
            Button(
                f"{label}, {grade}",
                {"current_level": confirmed(level), "current_grade": confirmed(grade)},
            )
            for level, label in CURRENT_LEVEL_LABELS.items()
            for grade in GRADE_OPTIONS[level]
        )
    if question_id == "B1.4":
        return tuple(
            Button(f"{term} {year}", {"intake": confirmed(f"{term} {year}")})
            for term in INTAKE_TERM_LABELS
            for year in years()
        )
    # D1, F3: ô chọn giống dropdown của form, chọn xong gửi đúng chữ của lựa chọn
    if question_id == "D1":
        return tuple(
            Button(label, {"education_system": confirmed(code)})
            for code, label in EDUCATION_SYSTEM_LABELS.items()
        )
    if question_id == "F3":
        return tuple(
            Button(label, {"funding_source": confirmed(code)})
            for code, label in FUNDING_SOURCE_LABELS.items()
        )
    return ()


def options(labels: dict[str, str]) -> list[dict[str, str]]:
    return [{"value": value, "label": label} for value, label in labels.items()]


def same_options(values: list[str] | tuple[str, ...]) -> list[dict[str, str]]:
    return [{"value": v, "label": v} for v in values]


# Tháng / năm cho hoạt động (D4): hai ô chọn cạnh nhau như form
def month_year(prefix: str, label: str) -> list[dict]:
    months = [str(m) for m in range(1, 13)]
    return [
        {"name": f"{prefix}_month", "label": label, "placeholder": "Tháng", "options": same_options(months)},
        {"name": f"{prefix}_year", "label": " ", "placeholder": "Năm", "options": same_options(past_years())},
    ]


# Ô nhập một chứng chỉ như thẻ chứng chỉ ở bước 3 form: loại (theo nhóm), môn (AP, A-Level), điểm (giới hạn
# theo loại), điểm chữ (A-Level), tháng / năm thi. Gửi đi thành câu skill_ho_so.CERT_TEXT đọc được
def cert_fields(categories: tuple[str, ...], placeholder: str) -> dict:
    types = {code: spec for code, spec in CERT_TYPES.items() if spec.category in categories}
    numeric = {spec.name: CERT_LIMITS[code] for code, spec in types.items() if code in CERT_LIMITS}
    with_subject = [spec.name for spec in types.values() if spec.requires_subject]
    letter = [spec.name for code, spec in types.items() if code not in CERT_LIMITS]
    fields = [
        {
            "name": "type",
            "label": "Loại chứng chỉ",
            "placeholder": "Chọn chứng chỉ",
            "options": same_options([spec.name for spec in types.values()]),
        },
        {
            "name": "score",
            "kind": "number",
            "label": "Điểm",
            "placeholder": placeholder,
            "minBy": {"type": {name: low for name, (low, _, _) in numeric.items()}},
            "maxBy": {"type": {name: high for name, (_, high, _) in numeric.items()}},
            "stepBy": {"type": {name: step for name, (_, _, step) in numeric.items()}},
            "shownWhen": {"field": "type", "values": list(numeric)},
        },
        *month_year("test", "Tháng thi"),
    ]
    if with_subject:
        subject = {
            "name": "subject",
            "kind": "text",
            "label": "Môn",
            "placeholder": "VD: Calculus BC",
            "prefix": ", môn ",
            "shownWhen": {"field": "type", "values": with_subject},
        }
        fields.insert(1, subject)
    if letter:
        grade = {
            "name": "grade",
            "label": "Điểm",
            "placeholder": "Chọn điểm",
            "options": same_options(list(ALEVEL_GRADES)),
            "shownWhen": {"field": "type", "values": letter},
        }
        fields.insert(len(fields) - 2, grade)
    return {
        "fields": fields,
        "format": "Chứng chỉ {type}{subject}, điểm {score}{grade}, thi tháng {test_month}/{test_year}",
    }


# Ô chọn theo mẫu form cho từng câu. fields: các ô (select / number / states / text / check), label: nhãn
# trên ô như form, format: ghép giá trị các ô thành tin nhắn gửi đi (altFormat: dùng khi ô check được tích;
# ô hiddenBy ẩn khi ô check đó được tích). Học sinh vẫn gõ chat được, không bắt buộc dùng ô chọn
def select_fields(question_id: str) -> dict | None:
    months = [str(m) for m in range(1, 13)]
    activity = "Hoạt động {name}, vai trò {role}, phạm vi {impact}, từ {start_month}/{start_year}"
    forms = {
        "D1": {
            "fields": [
                {
                    "name": "system",
                    "label": "Hệ chương trình",
                    "placeholder": "Chọn hệ chương trình",
                    "options": same_options(list(EDUCATION_SYSTEM_LABELS.values())),
                }
            ],
            "format": "{system}",
        },
        "F3": {
            "fields": [
                {
                    "name": "source",
                    "label": "Nguồn tài chính chính",
                    "placeholder": "Chọn nguồn tài chính",
                    "options": same_options(list(FUNDING_SOURCE_LABELS.values())),
                }
            ],
            "format": "{source}",
        },
        # D4, D5: nhập từng hoạt động / giải thưởng giống bước 5 của form; skill_ho_so đọc câu ghép
        "D4": {
            "fields": [
                {
                    "name": "name",
                    "kind": "text",
                    "label": "Tên hoạt động",
                    "placeholder": "VD: CLB Tiếng Anh",
                },
                {
                    "name": "role",
                    "label": "Vai trò",
                    "placeholder": "Chọn vai trò",
                    "options": same_options(list(ROLE_LABELS.values())),
                },
                {
                    "name": "impact",
                    "label": "Phạm vi ảnh hưởng",
                    "placeholder": "Chọn phạm vi",
                    "options": same_options(list(IMPACT_LABELS.values())),
                },
                {"name": "ongoing", "kind": "check", "label": "Vẫn đang tham gia"},
                *month_year("start", "Bắt đầu"),
                *[{**f, "hiddenBy": "ongoing"} for f in month_year("end", "Kết thúc")],
            ],
            "format": activity + " đến {end_month}/{end_year}",
            "altFormat": {"field": "ongoing", "format": activity + ", đang tham gia"},
        },
        "D5": {
            "fields": [
                {
                    "name": "name",
                    "kind": "text",
                    "label": "Tên giải thưởng",
                    "placeholder": "VD: Giải Nhì HSG Toán",
                },
                {
                    "name": "impact",
                    "label": "Cấp giải",
                    "placeholder": "Chọn cấp",
                    "options": same_options(list(IMPACT_LABELS.values())),
                },
                {
                    "name": "year",
                    "label": "Năm đạt giải",
                    "placeholder": "Chọn năm",
                    "options": same_options(past_years()),
                },
            ],
            "format": "Giải thưởng {name}, phạm vi {impact}, năm {year}",
        },
        "B1.1": {
            "fields": [
                {
                    "name": "level",
                    "label": "Cấp học hiện tại",
                    "placeholder": "Chọn cấp học",
                    "options": same_options(list(CURRENT_LEVEL_LABELS.values())),
                },
                {
                    "name": "grade",
                    "label": "Lớp / năm",
                    "placeholder": "Chọn lớp / năm",
                    "dependsOn": "level",
                    "optionsBy": {
                        CURRENT_LEVEL_LABELS[level]: same_options(grades)
                        for level, grades in GRADE_OPTIONS.items()
                    },
                },
            ],
            "format": "{level}, {grade}",
        },
        "B1.4": {
            "fields": [
                {
                    "name": "term",
                    "label": "Kỳ nhập học",
                    "placeholder": "Chọn kỳ",
                    "options": options(INTAKE_TERM_LABELS),
                },
                {"name": "year", "label": "Năm", "placeholder": "Chọn năm", "options": same_options(years())},
            ],
            "format": "{term} {year}",
        },
        "D2": {
            # Giống GPA tổng ở bước 2 của form: chọn thang trước rồi nhập điểm.
            # upload: frontend có thêm nút "Tải học bạ" như form (đọc điểm rồi gửi "GPA x thang 10")
            "upload": "transcript",
            "fields": [
                {
                    "name": "scale",
                    "label": "Thang điểm",
                    "placeholder": "Chọn thang",
                    "options": [{"value": "10", "label": "Thang 10"}, {"value": "4", "label": "Thang 4"}],
                },
                {
                    "name": "gpa",
                    "kind": "number",
                    "label": "GPA tổng",
                    "placeholder": "VD: 8.5",
                    "min": 0,
                    "maxBy": {"scale": {"10": 10, "4": 4}},
                    "step": 0.01,
                },
            ],
            "format": "GPA {gpa} thang {scale}",
        },
        "D3": cert_fields(("standardized", "graduate"), "VD: 1350"),
        "E1": cert_fields(("english",), "VD: 6.5"),
        "E2": {
            "fields": [
                {
                    "name": "month",
                    "label": "Dự kiến thi",
                    "placeholder": "Tháng",
                    "options": same_options(months),
                },
                {"name": "year", "label": " ", "placeholder": "Năm", "options": same_options(years())},
            ],
            "format": "Em dự kiến thi tháng {month}/{year}",
        },
        "N1": {
            "fields": [
                {"name": "state", "kind": "states", "label": "Bang có người thân", "placeholder": "Chọn bang"}
            ],
            "format": "{state}",
        },
        "N2": {
            "fields": [
                {
                    "name": "states",
                    "kind": "states",
                    "multiple": True,
                    "label": "Bang muốn học",
                    "placeholder": "Chọn bang",
                }
            ],
            "format": "Em muốn học ở {states}",
        },
    }
    return forms.get(question_id)


def buttons_of(question_id: str | None, can_skip: bool = False) -> tuple[Button, ...]:
    found = BUTTONS.get(question_id or "", ())
    return (*found, SKIP) if can_skip else found


def quick_replies(question_id: str | None, can_skip: bool = False) -> list[QuickReply]:
    return [QuickReply(label=b.label, value=b.label) for b in buttons_of(question_id, can_skip)]


def widget(question_id: str | None) -> Widget | None:
    if question_id in WIDGETS:
        kind, labels = WIDGETS[question_id]
        data: dict = {"options": options(labels)}
        if kind == "budget":
            # Ô nhập số tiền đổi VNĐ sang USD bằng cùng tỷ giá với chat
            data["rate"] = round(exchange_rate.vnd_per_usd())
        return Widget(type=kind, data=data)
    # B1.3: ô tìm ngành giống bước 1 của form
    if question_id == "B1.3":
        return Widget(type="major")
    form = select_fields(question_id or "")
    return Widget(type="select", data=form) if form else None


def same_text(a: str, b: str) -> bool:
    return a.strip().strip(".!").lower() == b.strip().strip(".!").lower()


# Tin nhắn trùng chữ một nút của câu đang hỏi thì trả nút đó, không thì None
def pressed(question_id: str | None, message: str, can_skip: bool = False) -> Button | None:
    found = (*buttons_of(question_id, can_skip), *choices_of(question_id))
    return next((b for b in found if same_text(b.label, message)), None)


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
