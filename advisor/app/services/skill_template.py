"""B1. Dựng skill ho_so bằng code từ một khuôn cố định (ý thầy 09/11/2026).

Phần lớn skill giống nhau giữa các học sinh, chỉ khoảng vài phần trăm là riêng. Vì vậy không để SLM sinh cả skill:
khuôn 9 mục nằm ở skills/skill_ho_so_khuon.md, code chỉ điền ba chỗ riêng của từng học sinh:
- {nhiem_vu} (mục 1): các việc học sinh hỏi, lấy từ cac_nhiem_vu SLM phân loại;
- {du_lieu} (mục 3): bảng hồ sơ, chép nguyên chữ của ho_so và tinh_san (skill_request_of);
- {noi_dung} (mục 5): nhóm điểm mạnh / ở mức giữa / cần lưu ý theo mức code tính, và lộ trình của skill_lo_trinh.
Skill dựng bằng code không qua check_skill của SLM vì mọi chữ đều chép từ dữ liệu, không có chữ do model viết.
"""

import re
from datetime import date
from functools import cache

from app.schemas.chat import ProfileField
from app.schemas.slm import SkillRequest
from app.services.skill_call import SKILLS_DIR, load_skill
from app.services.skill_input import NONE_TEXT, UNKNOWN_TEXT
from app.services.skills.buttons import is_middle_school_profile
from app.services.skills.skill_lo_trinh import choose_route, render, template_reasons

TEMPLATE_PATH = SKILLS_DIR / "skill_ho_so_khuon.md"
# Việc khuôn này làm được. Câu không nói việc nào (hoặc chỉ nói việc khác) thì làm cả hai
TEMPLATE_TASKS = ("danh_gia", "lo_trinh")
TASK_TITLES = {"danh_gia": "Đánh giá hồ sơ", "lo_trinh": "Lộ trình chuẩn bị"}
TASK_GOALS = {
    "danh_gia": "nhận xét điểm mạnh, phần ở mức giữa và phần cần lưu ý của hồ sơ",
    "lo_trinh": "nói lộ trình phù hợp, các mốc thời gian và các bước chuẩn bị theo thứ tự",
}

# Mức trong dòng tính sẵn: "3.48 thang 4, thấp (TB các trường 3.73)", "1350, mức trung bình"
LEVEL = re.compile(r",\s(?:mức\s)?(cao|trung bình|thấp)\b")
LEVEL_GROUP = {"cao": "strong", "trung bình": "middle", "thấp": "attention"}
GROUP_TITLES = (
    ("strong", "Điểm mạnh"),
    ("middle", "Ở mức giữa"),
    ("attention", "Cần lưu ý nhất"),
    ("no_mark", "Chưa có dữ liệu trường để so"),
    ("missing", "Chưa có trong hồ sơ"),
)
# (tên mặt, khóa ho_so, khóa tinh_san) của các mặt có mức so với dữ liệu trường
SCORED = (("GPA", "gpa", "gpa_level"), ("Tiếng Anh", "english", "english_level"), ("SAT", "sat", "sat_level"))
NO_MARK = "Chưa có dữ liệu trường để so"
ONLY_NOTED = "Chỉ ghi nhận, không dùng để chọn trường"
REFUSED = "đã từng bị từ chối visa"
PLANNED = "dự định"

# Bảng mục 3: (tên dòng, khóa ho_so, khóa tinh_san hoặc chữ cố định cho cột Mức). Thứ tự giống form
ROWS = (
    ("Hệ đang học", "education_system", None),
    ("Bậc muốn học", "target_level", None),
    ("Kỳ nhập học", "intake", "timeline"),
    ("GPA", "gpa", "gpa_level"),
    ("Tiếng Anh", "english", "english_level"),
    ("Kế hoạch thi tiếng Anh", "english_plan", None),
    ("SAT", "sat", "sat_level"),
    ("ACT", "act", "=" + NO_MARK),
    ("Kế hoạch thi SAT / ACT", "standardized_plan", None),
    ("Ngân sách", "budget", "budget_level"),
    ("Cần học bổng", "scholarship", None),
    ("Nguồn tài chính", "funding_source", None),
    ("Bang có người thân", "relatives_state", "=" + ONLY_NOTED),
    ("Bang muốn học", "preferred_states", "=" + ONLY_NOTED),
    ("Loại trường", "school_type", "=" + ONLY_NOTED),
    ("Visa", "visa_refused", None),
)


@cache
def template() -> str:
    return load_skill(TEMPLATE_PATH)[0]


def upper_first(text: str) -> str:
    return text[:1].upper() + text[1:]


# Có giá trị thật (không phải "không có" / "chưa rõ")
def has_value(ho_so: dict[str, str], key: str) -> bool:
    return ho_so.get(key) not in (None, NONE_TEXT, UNKNOWN_TEXT)


# Việc của khuôn theo thứ tự học sinh hỏi; không có việc nào của khuôn thì làm cả hai
def tasks_of(tasks: list[str] | None) -> list[str]:
    found = [task for task in tasks or [] if task in TEMPLATE_TASKS]
    return list(dict.fromkeys(found)) or list(TEMPLATE_TASKS)


def fill_nhiem_vu(tasks: list[str]) -> str:
    lines = [
        f'- {TASK_TITLES[task]}: {TASK_GOALS[task]}. Tiêu đề phần này: "{TASK_TITLES[task]}".'
        for task in tasks
    ]
    if len(tasks) == 1:
        head = "Học sinh hỏi một việc, chỉ làm việc này:"
        other = TASK_TITLES[next(t for t in TEMPLATE_TASKS if t != tasks[0])].lower()
        lines.append(f"Không viết phần {other}.")
    else:
        head = "Học sinh hỏi nhiều việc, câu trả lời phải đủ từng việc, mỗi việc một phần theo đúng thứ tự:"
    return "\n".join([head, *lines])


def row(name: str, value: str, level: str = "") -> str:
    return f"| {name} | {upper_first(value)} | {level} |"


def level_cell(ho_so: dict[str, str], tinh_san: dict[str, str], key: str, calc: str | None) -> str:
    if calc is None or not has_value(ho_so, key):
        return ""
    return calc[1:] if calc.startswith("=") else tinh_san.get(calc, "")


def study_row(ho_so: dict[str, str]) -> str | None:
    parts = [ho_so[key] for key in ("current_level", "current_grade") if key in ho_so]
    return row("Đang học", ", ".join(parts)) if parts else None


def major_row(ho_so: dict[str, str]) -> str:
    if has_value(ho_so, "major"):
        return row("Ngành", ho_so["major"], "Đã chốt, không đổi")
    return row("Ngành", "Chưa chọn")


def activity_lines(ho_so: dict[str, str], tinh_san: dict[str, str]) -> list[str]:
    lines = []
    for key, name in (("activities", "Hoạt động"), ("awards", "Giải thưởng")):
        if key in ho_so:
            lines += [f"- {name}: {item}" for item in ho_so[key].split("; ")]
    lines.append(f"Mức hoạt động chung: {tinh_san.get('ec_level', 'chưa có')}.")
    return lines


# Mục 3: bảng hồ sơ. Khóa vắng (chưa nói / từ chối nói) thì không có dòng; riêng ngành vắng ghi "Chưa chọn"
def fill_du_lieu(req: SkillRequest) -> str:
    ho_so, tinh_san = req.ho_so, req.tinh_san
    lines = ["| Mục | Giá trị | Mức |", "|---|---|---|"]
    lines += [line for line in (study_row(ho_so),) if line]
    lines.append(major_row(ho_so))
    for name, key, calc in ROWS:
        if key in ho_so:
            lines.append(row(name, ho_so[key], level_cell(ho_so, tinh_san, key, calc)))
        if key == "gpa" and "grade_by_year" in tinh_san:
            lines.append(row("Điểm từng năm", "theo học bạ", tinh_san["grade_by_year"]))
    if "scholarship_chance" in tinh_san:
        lines.append(row("Khả năng học bổng", tinh_san["scholarship_chance"]))
    return "\n".join([*lines, "", "Hoạt động và giải thưởng:", *activity_lines(ho_so, tinh_san)])


# "Lớp 10: 8.5; Lớp 11: 8.9" → "đang đi lên". Dưới hai năm thì không nói xu hướng
def grade_trend(grade_by_year: str | None) -> str | None:
    scores = [float(part.rsplit(": ", 1)[1]) for part in (grade_by_year or "").split("; ") if ": " in part]
    if len(scores) < 2:
        return None
    if scores[-1] > scores[0]:
        return "đang đi lên"
    return "đang đi xuống" if scores[-1] < scores[0] else "giữ đều"


def level_word(text: str | None) -> str | None:
    match = LEVEL.search(text or "")
    return match[1] if match else None


# Nhóm các mặt theo mức code tính. Không tự xếp mặt nào không có mức
def assessment_groups(ho_so: dict[str, str], tinh_san: dict[str, str]) -> dict[str, list[str]]:
    groups: dict[str, list[str]] = {key: [] for key, _ in GROUP_TITLES}
    for name, key, calc in SCORED:
        text = tinh_san.get(calc)
        word = level_word(text)
        if word:
            groups[LEVEL_GROUP[word]].append(f"{name} {text}")
        elif text:
            groups["no_mark"].append(f"{name} {text}")
        elif not has_value(ho_so, key):
            groups["missing"].append(name)
    if has_value(ho_so, "act"):
        groups["no_mark"].append(f"ACT {ho_so['act']}")
    if ho_so.get("visa_refused") == REFUSED:
        groups["attention"].append(f"Visa: {REFUSED}")
    return groups


def finance_line(ho_so: dict[str, str], tinh_san: dict[str, str]) -> str | None:
    parts = []
    if "budget_level" in tinh_san:
        parts.append(f"ngân sách {tinh_san['budget_level']}")
    if has_value(ho_so, "funding_source"):
        parts.append(f"nguồn {ho_so['funding_source']}")
    if has_value(ho_so, "scholarship"):
        parts.append(f"cần học bổng: {ho_so['scholarship']}")
    if "scholarship_chance" in tinh_san:
        parts.append(f"khả năng học bổng {tinh_san['scholarship_chance']}")
    return "- Tài chính: " + ", ".join(parts) + "." if parts else None


def assessment_part(req: SkillRequest, middle_school: bool) -> str:
    ho_so, tinh_san = req.ho_so, req.tinh_san
    groups = assessment_groups(ho_so, tinh_san)
    lines = ['### Phần "Đánh giá hồ sơ"', "Các mặt đã nhóm sẵn theo mức (chép đúng, không đổi nhóm):"]
    lines += [f"- {title}: {'; '.join(groups[key])}." for key, title in GROUP_TITLES if groups[key]]
    trend = grade_trend(tinh_san.get("grade_by_year"))
    if trend:
        lines.append(f"- Điểm từng năm ({tinh_san['grade_by_year']}) {trend}.")
    ec = tinh_san.get("ec_level")
    lines.append(
        f"- Hoạt động: {ec}, nhận xét theo vai trò, cấp và thời gian của từng hoạt động, giải thưởng ở mục 3."
        if ec
        else "- Hoạt động: chưa có, nói đây là phần nên bắt đầu."
    )
    lines += [line for line in (finance_line(ho_so, tinh_san),) if line]
    if middle_school:
        lines.append("- Bạn đang học THCS: GPA là mục tiêu, nhận xét theo hướng chuẩn bị dài hạn.")
    lines.append(
        "Cách viết: mở bằng một câu nhận xét chung (phần nổi bật nhất và phần cần cải thiện nhất), rồi đi lần lượt "
        "các nhóm trên, mỗi ý nêu đúng số và mức. Nhóm nào không có thì bỏ, không tự thêm."
    )
    return "\n".join(lines)


def plan_step(ho_so: dict[str, str], key: str, name: str, needed: bool) -> str | None:
    plan = ho_so.get(key, "")
    if plan.startswith(PLANNED):
        return f"- {name}: giữ đúng kế hoạch, {plan}."
    if needed:
        return f"- {name}: đặt lịch thi sớm."
    return None


# Các bước thêm theo hồ sơ, đứng sau lộ trình code chọn
def extra_steps(ho_so: dict[str, str], tinh_san: dict[str, str]) -> list[str]:
    steps = []
    if not has_value(ho_so, "major"):
        steps.append(
            "- Ngành: bạn chưa chọn ngành, nên chốt sớm vì ngành quyết định trường và hoạt động cần làm."
        )
    gpa_word = level_word(tinh_san.get("gpa_level"))
    trend = grade_trend(tinh_san.get("grade_by_year"))
    if gpa_word == "thấp" or trend == "đang đi xuống":
        steps.append("- GPA: tập trung kéo điểm các học kỳ còn lại, làm đều mỗi học kỳ.")
    elif trend == "đang đi lên":
        steps.append("- GPA: giữ đà điểm đang đi lên ở các học kỳ còn lại.")
    english_needed = not has_value(ho_so, "english") or level_word(tinh_san.get("english_level")) == "thấp"
    steps.append(plan_step(ho_so, "english_plan", "Tiếng Anh", english_needed))
    steps.append(plan_step(ho_so, "standardized_plan", "SAT / ACT", False))
    if has_value(ho_so, "sat") and has_value(ho_so, "act"):
        steps.append("- SAT / ACT: đã có cả hai điểm, chọn một bài để tập trung.")
    if "ec_level" in tinh_san:
        steps.append("- Hoạt động: giữ các hoạt động đang làm, ghi lại kết quả cụ thể của từng việc.")
    else:
        steps.append("- Hoạt động: bắt đầu một hoạt động gắn với ngành muốn học.")
    if re.search(r"mức (sát|thiếu)$", tinh_san.get("budget_level", "")):
        steps.append("- Tài chính: chuẩn bị giấy tờ chứng minh tài chính và hồ sơ xin học bổng.")
    if ho_so.get("visa_refused") == REFUSED:
        steps.append("- Visa: chuẩn bị giải thích rõ lần bị từ chối trước.")
    return [step for step in steps if step]


def roadmap_part(
    req: SkillRequest,
    profile: dict[str, ProfileField],
    today: date | None,
    gpa_table: list[tuple[float, float]] | None,
    middle_school: bool,
) -> str:
    route, _, intake = choose_route(profile, today, gpa_table)
    route.reasons = template_reasons(route)
    major = req.ho_so.get("major") if has_value(req.ho_so, "major") else None
    lines = [
        '### Phần "Lộ trình chuẩn bị"',
        "Lộ trình đã chọn (giữ đúng tên lộ trình, lý do và các mốc, không thêm mốc):",
        render(route, intake, major),
        "",
        "Các bước chuẩn bị theo hồ sơ, theo thứ tự:",
        *extra_steps(req.ho_so, req.tinh_san),
    ]
    if middle_school:
        lines.append("Bạn đang học THCS: lộ trình trên là định hướng dài hạn, ưu tiên học tốt và tiếng Anh.")
    lines.append(
        "Cách viết: nói lộ trình và lý do, rồi các mốc thời gian, rồi các bước trên bằng lời của bạn."
    )
    return "\n".join(lines)


def fill_noi_dung(
    req: SkillRequest,
    tasks: list[str],
    profile: dict[str, ProfileField],
    today: date | None = None,
    gpa_table: list[tuple[float, float]] | None = None,
) -> str:
    middle_school = is_middle_school_profile(profile)
    parts = []
    for task in tasks:
        if task == "danh_gia":
            parts.append(assessment_part(req, middle_school))
        else:
            parts.append(roadmap_part(req, profile, today, gpa_table, middle_school))
    return "\n\n".join(parts)


# Skill hoàn chỉnh cho một câu hỏi hồ sơ. Dùng .replace thay vì .format vì khuôn có ngoặc nhọn của JSON
def build_skill(
    req: SkillRequest,
    tasks: list[str] | None,
    profile: dict[str, ProfileField],
    today: date | None = None,
    gpa_table: list[tuple[float, float]] | None = None,
) -> str:
    chosen = tasks_of(tasks)
    return (
        template()
        .replace("{nhiem_vu}", fill_nhiem_vu(chosen))
        .replace("{du_lieu}", fill_du_lieu(req))
        .replace("{noi_dung}", fill_noi_dung(req, chosen, profile, today, gpa_table))
    )
