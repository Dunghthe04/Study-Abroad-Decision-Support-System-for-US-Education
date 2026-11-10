"""Bước 7a: chuyển hồ sơ chữ của các dòng ho_so có skill (du_lieu_slm.jsonl) thành hồ sơ có cấu trúc như chat gửi lên.

Kết quả: data/slm/ho_so_goc.json, mỗi id một {"profile": {khóa: ProfileField}, "transcript": ... | null,
"benchmarks": ... | null}. Từ file này
script điền Excel (7b) dựng lại cột ho_so bằng profile_to_skill_input và cột profile_calc bằng calc_profile, nên dữ liệu
train giống hệt đầu vào lúc chạy thật.

Chạy một lần rồi sửa tay file JSON nếu cần. Quy ước khi chuyển:
- Ngân sách là khoảng tiền lẻ (36.000–44.000) → mức form chứa điểm giữa khoảng.
- "Chưa thi IELTS / SAT", "chưa có hoạt động", "không có giải thưởng" → status none.
- Thêm current_level = high_school và current_grade suy từ kỳ nhập học (nhập học năm sau → Lớp 12, hai năm sau → Lớp 11),
  vì chat thật luôn có hai khóa này còn dữ liệu cũ thì không.
- Một nửa số dòng (dòng thứ 2, 4, 6...) nhập GPA theo học bạ: TB từng năm quanh GPA tổng với xu hướng tăng / giảm / đều xoay vòng.
- Mốc trường (benchmarks) như .NET gửi vào chat (ChatBenchmarks.From): TB AvgGpa4 và mốc Min* phổ biến nhất của các trường
  bậc đại học đang hoạt động. Chép số cố định từ DB ngày 05/11/2026 (249 dòng, 93 dòng có AvgGpa4) để dữ liệu train không
  đổi theo DB. Vài dòng (dòng thứ 10, 20, 30) bỏ mốc để skill học cả trường hợp chưa có dữ liệu trường.
- Hồ sơ cũ lệch: GPA cao nhất 9.4/10 (3.76, chỉ trung bình) và toàn IELTS. OVERRIDES sửa vài hồ sơ trước khi chuyển để
  có đủ mức GPA cao (≥ 9.6/10 = 3.84, trên TB 3.73 quá 0.1) và đủ TOEFL / Duolingo ở cả ba mức thấp, trung bình, cao.
  Câu hỏi của các dòng này không nhắc điểm nên đổi được.
"""

import json
import re
from pathlib import Path

from app.schemas.chat import ProfileField
from app.services.skills.buttons import IMPACT_LABELS, SCHOLARSHIP_LABELS
from app.services.skills.validators import BUDGET_RANGES
from scripts.build_slm_jsonl import DATA_DIR, DEFAULT_OUT

OUT = DATA_DIR / "ho_so_goc.json"
THIS_YEAR = 2026

ROLES = (  # thứ tự quan trọng: "phó chủ nhiệm" phải khớp trước "chủ nhiệm"
    ("phó chủ nhiệm", "vice_leader"),
    ("chủ nhiệm", "leader"),
    ("trưởng nhóm", "leader"),
    ("đội trưởng", "leader"),
    ("trưởng", "leader"),
    ("sáng lập", "founder"),
    ("thành viên", "member"),
)
IMPACTS = {"trường": "school", "quận": "district", "tỉnh": "province", "thành phố": "province"} | {
    label.lower(): code for code, label in IMPACT_LABELS.items()
}
ACTIVITY = re.compile(
    r"^(?P<rest>.+?) cấp (?P<impact>.+?) từ (?P<start>\d{2}/\d{4}) đến (?P<end>nay|\d{2}/\d{4})$"
)
AWARD = re.compile(r"^(?P<name>.+?) cấp (?P<impact>.+)$")
NONE_WORDS = ("chưa thi", "chưa có", "không có")
TRENDS = (0.3, -0.3, 0.0)  # chênh TB năm cuối so với năm đầu, xoay vòng theo dòng
# Mốc bậc đại học từ DB 05/11/2026, cùng cách tính ChatBenchmarks.From
UNDERGRAD_BENCHMARKS = {"gpa4": 3.73, "english": {"ielts": 6.5, "toefl": 80, "duolingo": 110}}
# Sửa hồ sơ chữ trước khi chuyển (xem docstring); chú thích là mức so với UNDERGRAD_BENCHMARKS
OVERRIDES = {
    "ho_so_013": {"gpa": "9.7 thang 10"},  # GPA cao
    "ho_so_016": {"gpa": "9.6 thang 10"},  # GPA cao, có học bạ
    "ho_so_023": {"gpa": "9.6 thang 10"},  # GPA cao
    "ho_so_039": {"gpa": "9.8 thang 10"},  # GPA cao, hỏi học bổng
    "ho_so_002": {"gpa": "9.3 thang 10"},  # GPA trung bình (bù các dòng đã nâng lên cao)
    "ho_so_044": {"gpa": "9.4 thang 10", "english": "Duolingo 120"},  # GPA trung bình, học bạ; TA cao
    "ho_so_005": {"english": "Duolingo 95"},  # thấp
    "ho_so_008": {"english": "TOEFL 80"},  # trung bình
    "ho_so_024": {"english": "TOEFL 95"},  # cao
    "ho_so_041": {"english": "TOEFL 65"},  # thấp
}


def field(value) -> dict:
    return ProfileField(value=value, status="confirmed").model_dump()


NONE = ProfileField(value=None, status="none").model_dump()


def is_none(text: str) -> bool:
    return text.lower().startswith(NONE_WORDS)


def budget_tier(text: str) -> str:
    low, high = (int(x.replace(".", "")) for x in re.findall(r"\d[\d.]*", text)[:2])
    mid = (low + high) / 2
    return next(code for code, (_, ceiling) in BUDGET_RANGES.items() if ceiling is None or mid <= ceiling)


def impact_code(text: str) -> str:
    return IMPACTS[text.strip().lower()]


def activity(text: str) -> dict:
    match = ACTIVITY.match(text)
    assert match, text
    rest = match["rest"]
    role_text, role = next((t, code) for t, code in ROLES if rest.lower().startswith(t))
    ongoing = match["end"] == "nay"
    return {
        "name": rest[len(role_text) :].strip(),
        "category": "activity",
        "role": role,
        "impactLevel": impact_code(match["impact"]),
        "startDate": match["start"],
        "endDate": None if ongoing else match["end"],
        "isOngoing": ongoing,
    }


def award(text: str) -> dict:
    match = AWARD.match(text)
    assert match, text
    return {"name": match["name"], "category": "achievement", "impactLevel": impact_code(match["impact"])}


def grade_now(intake: str) -> str:
    years_left = int(intake.split()[-1]) - THIS_YEAR
    return "Lớp 12" if years_left <= 1 else "Lớp 11"


# Học bạ: các năm đã học xong (đang Lớp 12 → Lớp 10, 11), TB quanh GPA tổng theo xu hướng; GPA tổng = TB các năm
def transcript_of(gpa10: float, grade: str, trend: float) -> dict:
    grades = ["Lớp 10", "Lớp 11"] if grade == "Lớp 12" else ["Lớp 10"]
    if len(grades) == 1:
        averages = [gpa10]
    else:
        averages = [round(gpa10 - trend / 2, 2), round(gpa10 + trend / 2, 2)]
    return {"years": [{"grade": g, "average": a} for g, a in zip(grades, averages, strict=True)]}


def convert(ho_so: dict, index: int) -> dict:
    profile = {"current_level": field("high_school"), "current_grade": field(grade_now(ho_so["intake"]))}
    profile["target_level"] = field("undergraduate")
    profile["major"] = field(ho_so["major"])
    profile["intake"] = field(ho_so["intake"])

    gpa10 = float(ho_so["gpa"].split()[0])
    with_transcript = index % 2 == 1  # dòng thứ 2, 4, 6... có học bạ
    source = "transcript" if with_transcript else "overall"
    profile["gpa"] = field({"value": gpa10, "scale": 10, "source": source})

    english = ho_so["english"]
    if is_none(english):
        profile["english"] = NONE
    else:
        kind, score = english.rstrip(".").split()
        profile["english"] = field({"type": kind.lower(), "value": float(score)})
    profile["sat"] = NONE if is_none(ho_so["sat"]) else field(int(ho_so["sat"]))

    profile["budget"] = field(budget_tier(ho_so["budget"]))
    scholarship = {label.lower(): code for code, label in SCHOLARSHIP_LABELS.items()}
    profile["scholarship"] = field(scholarship[ho_so["scholarship"].lower()])

    activities = ho_so["activities"]
    activities = activities if isinstance(activities, list) else [activities]
    profile["activities"] = NONE if is_none(activities[0]) else field([activity(a) for a in activities])
    profile["awards"] = NONE if is_none(ho_so["awards"]) else field([award(ho_so["awards"])])

    grade = profile["current_grade"]["value"]
    transcript = transcript_of(gpa10, grade, TRENDS[index // 2 % 3]) if with_transcript else None
    benchmarks = None if index % 10 == 9 else UNDERGRAD_BENCHMARKS
    return {"profile": profile, "transcript": transcript, "benchmarks": benchmarks}


def main(source: Path = DEFAULT_OUT, out: Path = OUT) -> None:
    records = [json.loads(line) for line in source.read_text(encoding="utf-8").splitlines()]
    rows = [r for r in records if r["type"] == "ho_so" and r["skill_md"]]
    result = {
        r["id"]: convert(r["input"]["ho_so"] | OVERRIDES.get(r["id"], {}), i) for i, r in enumerate(rows)
    }
    out.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")
    print(f"{len(result)} hồ sơ → {out}")


if __name__ == "__main__":
    main()
