"""Luật kiểm tra giá trị học sinh nói trong chat, trước khi ghi vào hồ sơ (HUONG_DAN mục 7.1).

Giá trị sai thì ném AskAgain kèm câu hỏi lại bằng tiếng Việt, không ghi bừa vào hồ sơ.
Mã và luật lấy đúng theo form và .NET (KE_HOACH_DONG_BO_CHAT_FORM.md), để chat nhận thì commit cũng nhận:
- giới hạn điểm theo metadata chứng chỉ (DbSeeder.cs, mảng certifications);
- mã vai trò, phạm vi, học bổng, loại trường theo profile-flow-options.ts;
- luật hoạt động / giải thưởng theo Step5Extracurricular.tsx và ValidateForCommit.
"""

import re
from datetime import date
from typing import NamedTuple

from app.services.skills import exchange_rate

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
    "cambridge": (80, 230, 1),
    "itep": (0, 6, 0.5),
    "languagecert": (0, 100, 1),
    "ap": (1, 5, 1),
    "ib": (1, 45, 1),
    "gre": (260, 340, 1),
    "gmat": (200, 800, 10),
}
ENGLISH_CERTS = ("ielts", "toefl", "duolingo", "pte")
CERT_NAMES = {
    "sat": "SAT",
    "act": "ACT",
    "ielts": "IELTS",
    "toefl": "TOEFL",
    "duolingo": "Duolingo",
    "pte": "PTE",
    "cambridge": "Cambridge English",
    "itep": "iTEP",
    "languagecert": "LanguageCert",
    "ap": "AP",
    "ib": "IB",
    "alevel": "A-Level",
    "gre": "GRE",
    "gmat": "GMAT",
}


# Loại chứng chỉ của form (metadata certifications trong DbSeeder.cs): tên như form, nhóm, có cần tên môn.
# Loại có thang số thì giới hạn ở CERT_LIMITS; A-Level chấm điểm chữ (ALEVEL_GRADES)
class CertType(NamedTuple):
    name: str
    category: str
    requires_subject: bool = False


CERT_TYPES = {
    "ielts": CertType("IELTS Academic", "english"),
    "toefl": CertType("TOEFL iBT", "english"),
    "duolingo": CertType("Duolingo English Test", "english"),
    "pte": CertType("PTE Academic", "english"),
    "cambridge": CertType("Cambridge English", "english"),
    "itep": CertType("iTEP Academic", "english"),
    "languagecert": CertType("LanguageCert Academic", "english"),
    "sat": CertType("SAT", "standardized"),
    "act": CertType("ACT", "standardized"),
    "ap": CertType("AP", "standardized", requires_subject=True),
    "ib": CertType("IB Diploma", "standardized"),
    "alevel": CertType("A-Level", "standardized", requires_subject=True),
    "gre": CertType("GRE General", "graduate"),
    "gmat": CertType("GMAT", "graduate"),
}
ALEVEL_GRADES = ("A*", "A", "B", "C", "D", "E")

# Tỷ giá VNĐ/USD lấy mới mỗi ngày ở exchange_rate.py
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
ALL_TIERS = (*(tier for _, tier in BUDGET_TIERS), TOP_TIER)

# Mã của form (Bước 4, Bước 1, Bước 5)
SCHOLARSHIP_LEVELS = ("mandatory", "preferred", "optional")
SCHOOL_TYPES = ("all", "public", "private")
ACTIVITY_ROLES = ("founder", "leader", "vice_leader", "member")
# Phạm vi theo thứ tự impact_level 1..5 trong DB
IMPACT_LEVELS = ("school", "district", "province", "national", "international")

# Chat chặt hơn .NET (cho 50 mục): chỉ nhận vài mục tiêu biểu nhất
MAX_ACTIVITIES = 4
MAX_AWARDS = 3
# Năm đạt giải nhỏ nhất (FE bắt từ 2000, .NET từ 1900; lấy luật chặt hơn)
MIN_AWARD_YEAR = 2000

# Độ dài tối đa giống ValidateForCommit
MAX_NAME_LEN = 256
MAX_STATE_LEN = 64
MAX_STATES = 60

MONTH_YEAR = re.compile(r"^(\d{1,2})/(\d{4})$")  # "09/2024", dạng của hoạt động
YEAR_MONTH = re.compile(r"^(\d{4})-(\d{2})$")  # "2024-09", dạng ngày thi chứng chỉ


# Lỗi "cần hỏi lại": skill bắt lỗi này rồi gửi .question cho học sinh
class AskAgain(Exception):
    def __init__(self, question: str) -> None:
        super().__init__(question)
        self.question = question


# Số có đúng bước không (vd IELTS 6.5 đúng, 6.3 sai). Chia ra rồi so với số tròn để tránh sai số float
def on_step(value: float, step: float) -> bool:
    ratio = value / step
    return abs(ratio - round(ratio)) < 1e-9


# Đổi tháng thành một số để so sánh trước/sau (giống year * 12 + month ở FE)
def month_index(year: int, month: int) -> int:
    return year * 12 + month


# "09/2024" → số tháng để so sánh; sai dạng thì None
def parse_month_year(text: str | None) -> int | None:
    match = MONTH_YEAR.match((text or "").strip())
    if not match:
        return None
    month, year = int(match[1]), int(match[2])
    return month_index(year, month) if 1 <= month <= 12 and year >= 1900 else None


# "2024-09" → số tháng để so sánh; sai dạng thì None
def parse_year_month(text: str | None) -> int | None:
    match = YEAR_MONTH.match((text or "").strip())
    if not match:
        return None
    year, month = int(match[1]), int(match[2])
    return month_index(year, month) if 1 <= month <= 12 and year >= 1900 else None


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


# A3. Chứng chỉ tiếng Anh. IELTS là IELTS Academic như form, không hỏi loại.
# Ngày thi "yyyy-MM" không được ở tương lai (giống ValidateCertifications). Trả về (mã, điểm, ngày thi)
def check_english(
    cert: str, score: float, test_date: str | None = None, today: date | None = None
) -> tuple[str, float, str | None]:
    cert = cert.strip().lower()
    if cert not in ENGLISH_CERTS:
        raise AskAgain("Bạn có chứng chỉ tiếng Anh nào: IELTS, TOEFL, Duolingo hay PTE?")
    check_score(cert, score)
    return cert, score, check_test_date(cert, test_date, today)


# Ngày thi "yyyy-MM" không được ở tương lai; không nói ngày thì None
def check_test_date(cert: str, test_date: str | None, today: date | None = None) -> str | None:
    if test_date is None:
        return None
    today = today or date.today()
    taken = parse_year_month(test_date)
    if taken is None:
        raise AskAgain(f"Bạn thi {CERT_NAMES[cert]} vào tháng mấy, năm nào?")
    if taken > month_index(today.year, today.month):
        raise AskAgain("Ngày thi không được ở tương lai. Bạn thi vào tháng mấy, năm nào?")
    return test_date.strip()


# A3. Một thẻ chứng chỉ như bước 3 của form (validateCertification): đúng loại, đúng thang điểm
# (A-Level là điểm chữ, ghi ở scaleText), có tên môn khi loại cần. Trả về thẻ dạng form, chưa có id
def check_certification(
    cert: str,
    score: str | float,
    test_date: str | None = None,
    subject: str | None = None,
    today: date | None = None,
) -> dict:
    cert = cert.strip().lower()
    spec = CERT_TYPES.get(cert)
    if spec is None:
        raise AskAgain("Mình chưa biết chứng chỉ này. Bạn chọn loại chứng chỉ trong danh sách giúp mình nhé?")
    subject = (subject or "").strip() or None
    if spec.requires_subject and subject is None:
        raise AskAgain(f"Chứng chỉ {CERT_NAMES[cert]} của bạn là môn nào?")
    overall, scale_text = None, None
    if cert in CERT_LIMITS:
        try:
            value = float(str(score).replace(",", "."))
        except ValueError:
            raise AskAgain(f"Điểm {CERT_NAMES[cert]} của bạn là bao nhiêu?") from None
        overall = check_score(cert, value)
        overall = int(overall) if float(overall).is_integer() else overall
    else:
        scale_text = str(score).strip().upper()
        if scale_text not in ALEVEL_GRADES:
            raise AskAgain("Điểm A-Level là điểm chữ từ A* đến E. Bạn được điểm mấy?")
    return {
        "category": spec.category,
        "type": cert,
        "subject": subject,
        "overall": overall,
        "scaleText": scale_text,
        "testDate": check_test_date(cert, test_date, today),
    }


# A3. Kế hoạch thi tiếng Anh, đúng 2 ô englishPlanned / englishPlannedDate của form.
# Chưa có kế hoạch → planned False; có kế hoạch thì tháng không bắt buộc, có thì không được ở quá khứ
def check_english_plan(planned: bool, plan_date: str | None = None, today: date | None = None) -> dict:
    if not planned or plan_date is None:
        return {"planned": planned, "date": None}
    today = today or date.today()
    month = parse_year_month(plan_date)
    if month is None:
        raise AskAgain("Bạn dự định thi tiếng Anh vào tháng mấy, năm nào?")
    if month < month_index(today.year, today.month):
        raise AskAgain("Tháng dự kiến thi không được ở quá khứ. Bạn định thi vào tháng nào?")
    return {"planned": True, "date": plan_date.strip()}


# A4. Đổi số tiền sang USD (VNĐ chia tỷ giá hôm nay)
def to_usd(amount: float, currency: str) -> float:
    return amount / exchange_rate.vnd_per_usd() if currency == "VND" else amount


# A4. Chọn mức ngân sách theo số USD mỗi năm. Giống form: đúng bằng trần thì sang mức trên (20.000 → 20k–35k)
def budget_tier(usd: float) -> str:
    for ceiling, tier in BUDGET_TIERS:
        if usd < ceiling:
            return tier
    return TOP_TIER


# A4. Ngân sách mỗi năm: chỉ lưu mức tier (giống annualBudgetTier của form), số tiền chỉ dùng để quy đổi.
# Học sinh nói khoảng thì skill truyền số lớn hơn
# Cách học sinh gọi đơn vị tiền → mã chuẩn
CURRENCY_ALIASES = {"VNĐ": "VND", "ĐỒNG": "VND", "Đ": "VND", "$": "USD", "ĐÔ": "USD", "US$": "USD"}
# Không nói đơn vị mà số từ 1 triệu trở lên thì là VNĐ (ngân sách 1 triệu USD/năm là không thật)
VND_GUESS_FROM = 1_000_000


def check_budget(amount: float, currency: str | None = None) -> str:
    if currency is None or not currency.strip():
        currency = "VND" if amount >= VND_GUESS_FROM else "USD"
    currency = currency.strip().upper()
    currency = CURRENCY_ALIASES.get(currency, currency)
    if currency not in CURRENCIES:
        raise AskAgain("Bạn cho mình biết số tiền theo USD hoặc VNĐ nhé?")
    if amount <= 0:
        raise AskAgain("Gia đình bạn có thể chi khoảng bao nhiêu mỗi năm? Bạn cho mình một con số nhé.")
    usd = to_usd(amount, currency)
    if usd > MAX_BUDGET_USD:
        raise AskAgain(
            "Số tiền này lớn bất thường. Bạn kiểm tra lại số tiền và đơn vị (USD hay VNĐ) giúp mình nhé?"
        )
    return budget_tier(usd)


# Câu nói số tiền: "800 triệu", "1,2 tỷ", "900.000.000 đ", "36.000 USD", "40k usd/năm"
MONEY_TEXT = re.compile(
    r"(?:khoảng|tầm|chừng)?\s*(\d+(?:[.,]\d+)*)\s*(triệu|tr|tỷ|tỉ|k|nghìn|ngàn)?\s*"
    r"(usd|us\$|\$|đô|vnđ|vnd|đồng|đ)?\s*(?:(?:/|mỗi|một)\s*năm)?",
    re.IGNORECASE,
)
MONEY_UNITS = {"triệu": 1e6, "tr": 1e6, "tỷ": 1e9, "tỉ": 1e9, "k": 1e3, "nghìn": 1e3, "ngàn": 1e3}


# Đọc câu số tiền thành (số tiền, đơn vị). Không nói đơn vị thì đơn vị None (check_budget tự đoán).
# Câu khác thì None
def read_money(text: str) -> tuple[float, str | None] | None:
    match = MONEY_TEXT.fullmatch(text.strip().rstrip("."))
    if match is None:
        return None
    number, unit, currency = match[1], match[2], match[3]
    if re.fullmatch(r"\d{1,3}(?:[.,]\d{3})+", number):
        amount = float(re.sub(r"[.,]", "", number))
    elif number.count(".") + number.count(",") <= 1:
        amount = float(number.replace(",", "."))
    else:
        return None
    # Chỉ một số nhỏ, không đơn vị ("800") thì chưa rõ là 800 USD hay 800 triệu
    if not unit and not currency and amount < 1000:
        return None
    if unit:
        amount *= MONEY_UNITS[unit.lower()]
        # "800 triệu" không nói đơn vị là VNĐ; "40k" không nói đơn vị là USD
        currency = currency or ("USD" if unit.lower() == "k" else "VND")
    return amount, currency


# A4. Học sinh bấm nút chọn mức ngân sách: mã phải là một trong 5 mức của form
def check_budget_tier(tier: str) -> str:
    tier = tier.strip().lower()
    if tier not in ALL_TIERS:
        raise AskAgain("Gia đình bạn có thể chi khoảng bao nhiêu mỗi năm? Bạn chọn một mức giúp mình nhé.")
    return tier


# A4. Mức cần học bổng: bắt buộc trả lời, không được bỏ qua
def check_scholarship(level: str | None) -> str:
    if level is None or level.strip().lower() not in SCHOLARSHIP_LEVELS:
        raise AskAgain("Bạn cần học bổng ở mức nào: không cần, nên có, hay bắt buộc phải có mới đi được?")
    return level.strip().lower()


# Loại trường muốn học: all (không lọc) | public | private, giống Bước 1 của form
def check_school_type(kind: str) -> str:
    kind = kind.strip().lower()
    if kind not in SCHOOL_TYPES:
        raise AskAgain("Bạn muốn học trường công lập, tư thục, hay trường nào cũng được?")
    return kind


# Bang muốn học: tối đa 60 bang, mỗi tên tối đa 64 ký tự (giống ValidateForCommit)
def check_states(states: list[str]) -> list[str]:
    names = [s.strip() for s in states if s.strip()]
    if len(names) > MAX_STATES:
        raise AskAgain(f"Bạn chọn giúp mình tối đa {MAX_STATES} bang nhé?")
    if any(len(s) > MAX_STATE_LEN for s in names):
        raise AskAgain("Mình chưa nhận ra tên bang này. Bạn viết lại tên bang giúp mình nhé?")
    return names


# Tên hoạt động / giải thưởng: không rỗng, tối đa 256 ký tự
def check_item_name(name: str | None, ask: str) -> str:
    name = (name or "").strip()
    if not name:
        raise AskAgain(ask)
    if len(name) > MAX_NAME_LEN:
        raise AskAgain(f"Tên này dài quá, bạn viết ngắn lại giúp mình (tối đa {MAX_NAME_LEN} ký tự) nhé?")
    return name


# Phạm vi của hoạt động / giải thưởng, mã giống IMPACT_LEVEL_OPTIONS của form
def check_impact(name: str, impact_level: str | None) -> str:
    impact = (impact_level or "").strip().lower()
    if impact not in IMPACT_LEVELS:
        raise AskAgain(f"{name} ở phạm vi nào: trường, quận/huyện, tỉnh/thành phố, quốc gia hay quốc tế?")
    return impact


# A5. Một hoạt động ngoại khóa, đúng dạng một mục Bước 5 của form (FlowActivityDraftDto).
# Luật giống Step5Extracurricular: có vai trò, phạm vi, tháng bắt đầu không sau tháng này,
# có tháng kết thúc (không trước tháng bắt đầu) hoặc "đến nay". Thiếu ý nào thì hỏi đúng ý đó
def check_activity(
    name: str | None,
    role: str | None = None,
    impact_level: str | None = None,
    start_date: str | None = None,
    end_date: str | None = None,
    is_ongoing: bool = False,
    today: date | None = None,
) -> dict:
    name = check_item_name(name, "Hoạt động này tên là gì vậy bạn?")
    role = (role or "").strip().lower()
    if role not in ACTIVITY_ROLES:
        raise AskAgain(f"Trong {name}, bạn là người sáng lập, trưởng/chủ nhiệm, phó hay thành viên?")
    impact = check_impact(name, impact_level)

    today = today or date.today()
    start = parse_month_year(start_date)
    if start is None:
        raise AskAgain(f"Bạn bắt đầu tham gia {name} từ tháng nào, năm nào?")
    if start > month_index(today.year, today.month):
        raise AskAgain("Tháng bắt đầu không được sau tháng hiện tại. Bạn bắt đầu từ tháng nào?")
    if not is_ongoing:
        end = parse_month_year(end_date)
        if end is None:
            raise AskAgain(f"Bạn tham gia {name} đến tháng nào, hay vẫn đang tham gia đến nay?")
        if end < start:
            raise AskAgain("Tháng kết thúc phải sau hoặc bằng tháng bắt đầu. Bạn xem lại giúp mình nhé?")

    return {
        "name": name,
        "category": "activity",
        "role": role,
        "impactLevel": impact,
        "startDate": start_date.strip(),
        "endDate": None if is_ongoing else end_date.strip(),
        "isOngoing": is_ongoing,
    }


# A5. Danh sách hoạt động: tối đa 4, kiểm tra từng cái
def check_activities(items: list[dict], today: date | None = None) -> list[dict]:
    if len(items) > MAX_ACTIVITIES:
        raise AskAgain(f"Bạn chọn giúp mình tối đa {MAX_ACTIVITIES} hoạt động tiêu biểu nhất nhé?")
    return [check_activity(**item, today=today) for item in items]


# A9. Một giải thưởng, đúng dạng mục "achievement" của form: tên, phạm vi, năm đạt giải 2000..năm nay
def check_award(
    name: str | None, impact_level: str | None = None, year: int | None = None, today: date | None = None
) -> dict:
    name = check_item_name(name, "Giải thưởng này tên là gì vậy bạn?")
    impact = check_impact(name, impact_level)
    this_year = (today or date.today()).year
    if year is None:
        raise AskAgain(f"Bạn đạt {name} vào năm nào?")
    if not MIN_AWARD_YEAR <= year <= this_year:
        raise AskAgain(f"Năm đạt giải phải từ {MIN_AWARD_YEAR} đến {this_year}. Bạn đạt {name} năm nào?")
    return {"name": name, "category": "achievement", "impactLevel": impact, "year": year}


# A9. Danh sách giải thưởng: tối đa 3, kiểm tra từng cái
def check_awards(items: list[dict], today: date | None = None) -> list[dict]:
    if len(items) > MAX_AWARDS:
        raise AskAgain(f"Bạn chọn giúp mình tối đa {MAX_AWARDS} giải thưởng cao nhất nhé?")
    return [check_award(**item, today=today) for item in items]
