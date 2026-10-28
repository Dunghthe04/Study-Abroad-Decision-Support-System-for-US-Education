"""Luật kiểm tra giá trị học sinh nói trong chat, trước khi ghi vào hồ sơ (HUONG_DAN mục 7.1).

Giá trị sai thì ném AskAgain kèm câu hỏi lại bằng tiếng Việt, không ghi bừa vào hồ sơ.
Giới hạn điểm lấy đúng theo metadata chứng chỉ của .NET (DbSeeder.cs, mảng certifications).
"""

# Thang GPA được nhận
GPA_SCALES = (4, 10)

# Giới hạn điểm theo mã chứng chỉ của .NET: (nhỏ nhất, lớn nhất, bước)
CERT_LIMITS = {
    "sat": (400, 1600, 10),
    "act": (1, 36, 1),
    "ielts": (0, 9, 0.5),
    "toefl": (0, 120, 1),
    "duolingo": (10, 160, 5),
    "pte": (10, 90, 1),
}
ENGLISH_CERTS = ("ielts", "toefl", "duolingo", "pte")
CERT_NAMES = {
    "sat": "SAT",
    "act": "ACT",
    "ielts": "IELTS",
    "toefl": "TOEFL",
    "duolingo": "Duolingo",
    "pte": "PTE",
}
IELTS_KINDS = ("academic", "general")

# Tỷ giá cố định để đổi VNĐ sang USD, chỉ sửa ở đây
VND_PER_USD = 25_000
CURRENCIES = ("USD", "VND")
# Ngân sách tối đa mỗi năm (giống MaxAnnualBudget của .NET)
MAX_BUDGET_USD = 10_000_000
# Mức ngân sách: (trần USD, mã tier), dò từ thấp lên cao
BUDGET_TIERS = [
    (20_000, "tier_under_20k"),
    (35_000, "tier_20k_35k"),
    (50_000, "tier_35k_50k"),
    (70_000, "tier_50k_70k"),
]
TOP_TIER = "tier_above_70k"

SCHOLARSHIP_LEVELS = ("required", "preferred", "no")

MAX_ACTIVITIES = 4
ACTIVITY_ROLES = ("founder", "lead", "vice", "member")
# Phạm vi 1 đến 5: trường, quận, tỉnh/thành phố, quốc gia, quốc tế
MIN_SCOPE, MAX_SCOPE = 1, 5
# Giải thưởng: tối đa 3, cấp giải cùng thang 1-5 với phạm vi
MAX_AWARDS = 3


# Lỗi "cần hỏi lại": skill bắt lỗi này rồi gửi .question cho học sinh
class AskAgain(Exception):
    def __init__(self, question: str) -> None:
        super().__init__(question)
        self.question = question


# Số có đúng bước không (vd IELTS 6.5 đúng, 6.3 sai). Chia ra rồi so với số tròn để tránh sai số float
def on_step(value: float, step: float) -> bool:
    ratio = value / step
    return abs(ratio - round(ratio)) < 1e-9


# A1. GPA: không nói thang thì đoán (<= 4 là thang 4, còn lại thang 10). Trả về (gpa, thang)
def check_gpa(value: float, scale: int | None = None) -> tuple[float, int]:
    if value < 0:
        raise AskAgain("GPA không thể là số âm. Bạn kiểm tra lại GPA giúp mình nhé?")
    if scale is None:
        scale = 4 if value <= 4 else 10
    if scale not in GPA_SCALES:
        raise AskAgain("Mình chỉ nhận GPA thang 4 hoặc thang 10. GPA của bạn tính trên thang nào?")
    if value > scale:
        raise AskAgain(f"GPA {value} lớn hơn thang {scale}. Bạn kiểm tra lại giúp mình nhé?")
    return value, scale


# Kiểm tra điểm một chứng chỉ theo CERT_LIMITS: đúng khoảng và đúng bước
def check_score(cert: str, score: float) -> float:
    low, high, step = CERT_LIMITS[cert]
    name = CERT_NAMES[cert]
    if not low <= score <= high:
        raise AskAgain(f"Điểm {name} chỉ từ {low} đến {high}. Bạn kiểm tra lại điểm {name} giúp mình nhé?")
    if not on_step(score, step):
        raise AskAgain(
            f"Điểm {name} tính theo bước {step}, mình chưa thấy mức {score}. Bạn xem lại giúp mình nhé?"
        )
    return score


# A2. SAT: 400 đến 1600, bước 10
def check_sat(score: int) -> int:
    return int(check_score("sat", score))


# A2. ACT: 1 đến 36, số nguyên
def check_act(score: int) -> int:
    return int(check_score("act", score))


# A3. Chứng chỉ tiếng Anh. IELTS phải biết Academic hay General, chưa nói thì hỏi lại
# Trả về (mã chứng chỉ, điểm, loại IELTS hoặc None)
def check_english(cert: str, score: float, ielts_kind: str | None = None) -> tuple[str, float, str | None]:
    cert = cert.strip().lower()
    if cert not in ENGLISH_CERTS:
        raise AskAgain("Bạn có chứng chỉ tiếng Anh nào: IELTS, TOEFL, Duolingo hay PTE?")
    check_score(cert, score)
    if cert != "ielts":
        return cert, score, None
    if ielts_kind is None:
        raise AskAgain("Chứng chỉ IELTS của bạn là Academic hay General Training?")
    if ielts_kind not in IELTS_KINDS:
        raise AskAgain("Mình chưa rõ loại IELTS. Bạn thi Academic hay General Training?")
    return cert, score, ielts_kind


# A4. Đổi số tiền sang USD (VNĐ chia tỷ giá)
def to_usd(amount: float, currency: str) -> float:
    return amount / VND_PER_USD if currency == "VND" else amount


# A4. Chọn mức ngân sách theo số USD mỗi năm
def budget_tier(usd: float) -> str:
    for ceiling, tier in BUDGET_TIERS:
        if usd <= ceiling:
            return tier
    return TOP_TIER


# A4. Ngân sách mỗi năm. Học sinh nói khoảng thì skill truyền số lớn hơn.
# Trả về (số tiền gốc, loại tiền, mã tier) để giữ được số học sinh nói
def check_budget(amount: float, currency: str = "USD") -> tuple[float, str, str]:
    currency = currency.strip().upper()
    if currency not in CURRENCIES:
        raise AskAgain("Bạn cho mình biết số tiền theo USD hoặc VNĐ nhé?")
    if amount <= 0:
        raise AskAgain("Gia đình bạn có thể chi khoảng bao nhiêu mỗi năm? Bạn cho mình một con số nhé.")
    usd = to_usd(amount, currency)
    if usd > MAX_BUDGET_USD:
        raise AskAgain(
            "Số tiền này lớn bất thường. Bạn kiểm tra lại số tiền và đơn vị (USD hay VNĐ) giúp mình nhé?"
        )
    return amount, currency, budget_tier(usd)


# A4. Mức cần học bổng: bắt buộc trả lời, không được bỏ qua
def check_scholarship(level: str | None) -> str:
    if level is None or level.strip().lower() not in SCHOLARSHIP_LEVELS:
        raise AskAgain("Bạn cần học bổng ở mức nào: không cần, nên có, hay bắt buộc phải có mới đi được?")
    return level.strip().lower()


# A5. Một hoạt động ngoại khóa: tên, vai trò, phạm vi 1-5, số tháng
def check_activity(name: str, role: str, scope: int, months: int) -> dict:
    name = name.strip()
    if not name:
        raise AskAgain("Hoạt động này tên là gì vậy bạn?")
    role = role.strip().lower()
    if role not in ACTIVITY_ROLES:
        raise AskAgain(f"Trong {name}, bạn là người sáng lập, trưởng nhóm, phó nhóm hay thành viên?")
    if not MIN_SCOPE <= scope <= MAX_SCOPE:
        raise AskAgain(f"{name} diễn ra ở phạm vi nào: trường, quận, thành phố, quốc gia hay quốc tế?")
    if months <= 0:
        raise AskAgain(f"Bạn tham gia {name} được bao nhiêu tháng rồi?")
    return {"name": name, "role": role, "scope": scope, "months": months}


# A5. Danh sách hoạt động: tối đa 4, kiểm tra từng cái
def check_activities(items: list[dict]) -> list[dict]:
    if len(items) > MAX_ACTIVITIES:
        raise AskAgain(f"Bạn chọn giúp mình tối đa {MAX_ACTIVITIES} hoạt động tiêu biểu nhất nhé?")
    return [check_activity(**item) for item in items]


# A9. Một giải thưởng: tên và cấp giải 1-5
def check_award(title: str, level: int) -> dict:
    title = title.strip()
    if not title:
        raise AskAgain("Giải thưởng này tên là gì vậy bạn?")
    if not MIN_SCOPE <= level <= MAX_SCOPE:
        raise AskAgain(f"{title} là giải cấp nào: trường, quận, thành phố, quốc gia hay quốc tế?")
    return {"title": title, "level": level}


# A9. Danh sách giải thưởng: tối đa 3, kiểm tra từng cái
def check_awards(items: list[dict]) -> list[dict]:
    if len(items) > MAX_AWARDS:
        raise AskAgain(f"Bạn chọn giúp mình tối đa {MAX_AWARDS} giải thưởng cao nhất nhé?")
    return [check_award(**item) for item in items]
