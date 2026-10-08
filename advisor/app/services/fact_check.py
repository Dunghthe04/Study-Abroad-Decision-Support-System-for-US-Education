"""Giữ số liệu đúng: code tự so sánh, rồi kiểm tra lời giải thích của LLM có nói ngược không.

relations(): phép so sánh đúng (nhóm, SAT, GPA, ngân sách, tiếng Anh), dùng cho cả prompt và bước kiểm tra.
contradictions(): tìm câu khẳng định trong lý do LLM viết, so với relations().
Kiểm tra theo từ khóa: bắt được lỗi hay gặp (vd. "SAT thấp hơn mốc 25%" khi thật ra nằm trong khoảng);
câu diễn đạt không có từ khóa thì bỏ qua.
"""

import re  # là thư viện biểu thức chính quy

from app.schemas.recommendation import AiSchoolInput, AiStudentInput

# Mức ngoại khóa theo thang 0–4 (thang của thầy): ≥3 mạnh, ≥2 trung bình, còn lại còn mỏng
EXTRACURRICULAR_LEVEL_TEXT = {"strong": "mạnh", "medium": "trung bình", "weak": "còn mỏng"}


def extracurricular_level(score: float | None) -> str | None:
    if score is None:
        return None
    return "strong" if score >= 3 else "medium" if score >= 2 else "weak"


def relations(st: AiStudentInput, sc: AiSchoolInput) -> dict[str, str]:
    """Quan hệ đúng giữa hồ sơ và trường Thiếu dữ liệu thì không có khóa đó."""
    rel = {"category": sc.category, "english": sc.english}
    if sc.category_basis and sc.category != "insufficient_data":
        rel["category_basis"] = sc.category_basis
    if st.sat is not None and sc.sat25 is not None and sc.sat75 is not None:
        rel["sat"] = "above" if st.sat > sc.sat75 else "below" if st.sat < sc.sat25 else "within"
    if st.gpa4 is not None and sc.avg_gpa4 is not None:
        rel["gpa"] = "above" if st.gpa4 > sc.avg_gpa4 else "below" if st.gpa4 < sc.avg_gpa4 else "equal"
    if st.annual_budget_usd is not None and sc.total_cost_usd is not None:
        rel["budget"] = "within" if sc.total_cost_usd <= st.annual_budget_usd else "over"
    level = extracurricular_level(st.extracurricular_score)
    if level:
        rel["extracurricular"] = level
    return rel


def cost_ranks(schools: list[AiSchoolInput]) -> dict[str, str]:
    """Trường rẻ nhất / đắt nhất trong mỗi nhóm (nhóm có từ 2 trường biết chi phí). Trường còn lại: "none"."""
    ranks = {s.code: "none" for s in schools}
    by_category: dict[str, list[AiSchoolInput]] = {}
    for s in schools:
        if s.total_cost_usd is not None:
            by_category.setdefault(s.category, []).append(s)

    for group in by_category.values():
        if len(group) < 2:
            continue
        ranks[min(group, key=lambda s: s.total_cost_usd).code] = "cheapest"
        ranks[max(group, key=lambda s: s.total_cost_usd).code] = "priciest"
    return ranks


# (các cách viết, những quan hệ mà câu đó đúng). Cụm dài đặt trước cụm ngắn cùng vị trí.
_SAT_CLAIMS = [
    (("trong khoảng", "nằm giữa", "ở giữa"), {"within"}),
    (("cao hơn mốc 25%", "vượt mốc 25%", "trên mốc 25%"), {"within", "above"}),
    (("thấp hơn mốc 75%", "dưới mốc 75%", "chưa đạt mốc 75%", "chưa tới mốc 75%"), {"within", "below"}),
    (("cao hơn", "vượt", "trên mốc"), {"above"}),
    (("thấp hơn", "dưới mốc", "chưa đạt", "chưa tới", "chưa chạm", "kém hơn"), {"below"}),
]
_GPA_CLAIMS = [
    (("cao hơn", "vượt", "nhỉnh hơn", "trên mức"), {"above"}),
    (("thấp hơn", "dưới mức", "chưa đạt", "kém hơn"), {"below"}),
    (("bằng", "ngang"), {"equal"}),
]

# Một vế câu kết thúc ở dấu chấm (không phải dấu thập phân như 3.6), dấu chấm phẩy, hoặc khi nói sang chủ đề khác
_CLAUSE_END = re.compile(
    r"\.(?!\d)|;|\bSAT\b|\bGPA\b|chi phí|học phí|ngân sách|tiếng Anh|IELTS|TOEFL|Duolingo", re.IGNORECASE
)


# Tìm xem trong câu giải thích của LLM, sau từ SAT hoặc GPA, LLM đang đưa ra nhận định gì.
def _claim(reason: str, subject: str, table) -> set[str] | None:
    """Quan hệ mà lý do khẳng định cho SAT/GPA: từ so sánh đầu tiên sau chủ thể, trong cùng một vế câu."""
    # Tìm subject trong đoạn reason , tìm đúng từ subject chứ k tạp, ko pbiet hoa thường
    # m đại diện cho 1 lần tìm thấy subject
    for m in re.finditer(rf"\b{subject}\b", reason, flags=re.IGNORECASE):
        # lấy 80 kí tự sau subject(SAT/GPA)
        rest = reason[m.end() : m.end() + 80].lower()
        # Tìm xem có dấu hiệu kết thúc không
        end = _CLAUSE_END.search(rest)
        # Nếu có điểm kết thúc -> lấy phần trc đó, nếu k lấy toàn bộ rest
        clause = rest[: end.start()] if end else rest
        hits = []

        # Duyệt từng nhóm cách diễn đạt vd ("trong khoảng", "nằm giữa", "ở giữa"), {"within"}
        for words, allowed in table:
            # Duyệt từng nhóm cách diễn đạt vd trong khoảng
            for word in words:
                # Kiểm tra xem nhóm đó có trong câu (câu sau từ subject)
                if word in clause:
                    # Nếu có thêm vào hist
                    hits.append(
                        (
                            # Vị trí tìm thấy
                            clause.find(word),
                            # Độ dài, - vì nếu từ nào dài hơn ==> số to hơn để lấy từ đó
                            -len(word),
                            # Quan hệ
                            allowed,
                        )
                    )
        if hits:
            # Tìm ứng viên xuất hiện sớm nhất,
            # nếu cùng vị trí thì chọn cụm dài hơn
            best = min(hits, key=lambda h: (h[0], h[1]))

            # Lấy quan hệ của cụm đó
            allowed = best[2]

            return allowed
    return None


def _budget_claim(reason: str) -> set[str] | None:
    text = reason.lower()
    if re.search(r"vượt( quá)? ngân sách|quá ngân sách|cao hơn ngân sách", text):
        return {"over"}
    if re.search(r"(trong|vừa|phù hợp với|dưới) (mức )?ngân sách", text):
        return {"within"}
    return None


def _english_claim(reason: str) -> set[str] | None:
    text = reason.lower()
    if re.search(r"(không|chưa) công bố yêu cầu tiếng anh", text):
        return {"unknown"}
    if re.search(r"chưa đạt (yêu cầu|điều kiện) tiếng anh", text):
        return {"below_min"}
    if re.search(r"đạt (yêu cầu|điều kiện) tiếng anh", text):
        return {"met"}
    return None


def _category_claim(reason: str) -> set[str] | None:
    text = reason.lower()
    # Đã nói rõ thiếu dữ liệu thì chữ "thử sức"/"an toàn" phía sau chỉ là lời khuyên
    if "chưa đủ dữ liệu" in text:
        return {"insufficient_data"}
    if "an toàn" in text:
        return {"safety"}
    if "vừa sức" in text:
        return {"match"}
    if "thử sức" in text or "khó vào" in text:
        return {"reach"}
    return None


def _extracurricular_claim(reason: str) -> set[str] | None:
    """Lý do khen hay chê hồ sơ ngoại khóa; mức trung bình chấp nhận cả hai cách nói."""
    text = reason.lower()
    if re.search(r"còn mỏng|cần bổ sung|còn yếu|chưa nổi bật", text):
        return {"weak", "medium"}
    if re.search(r"là lợi thế|nổi bật|thế mạnh", text):
        return {"strong", "medium"}
    return None


def _cost_rank_claim(reason: str) -> set[str] | None:
    text = reason.lower()
    if "thấp nhất" in text or "rẻ nhất" in text:
        return {"cheapest"}
    if "cao nhất" in text or "đắt nhất" in text:
        return {"priciest"}
    return None


_OVERCONFIDENT = ("chắc chắn", "đảm bảo")  # không được hứa hẹn kết quả tuyển sinh


def contradictions(reason: str, truth: dict[str, str]) -> list[str]:
    """
    Kiểm tra lý do AI có nói sai dữ liệu hoặc nói quá chắc chắn hay không.
    Nếu không có lỗi thì trả về [].
    """

    errors = []

    # 1. Kiểm tra AI có dùng từ ngữ quá chắc chắn không
    for word in _OVERCONFIDENT:
        if word in reason.lower():
            errors.append(f"nói quá: '{word}'")

    # 2. Lấy những gì AI đã nói về từng tiêu chí
    claims = {
        "category": _category_claim(reason),
        "sat": _claim(reason, "SAT", _SAT_CLAIMS),
        "gpa": _claim(reason, "GPA", _GPA_CLAIMS),
        "budget": _budget_claim(reason),
        "english": _english_claim(reason),
        "extracurricular": _extracurricular_claim(reason),
        "cost_rank": _cost_rank_claim(reason),
    }

    # 3. So sánh điều AI nói với dữ liệu đúng
    for key, said in claims.items():
        # AI không nói về tiêu chí này → bỏ qua
        if said is None:
            continue

        # truth không có tiêu chí này → bỏ qua
        if key not in truth:
            continue

        # AI nói sai với dữ liệu thật
        if truth[key] not in said:
            errors.append(f"{key}: lý do nói {sorted(said)}, đúng là {truth[key]}")

    # 4. Lý do xếp nhóm phải nhắc đúng tiêu chí quyết định nhóm (vd. thử sức vì GPA thì không được chỉ nói SAT)
    basis = truth.get("category_basis")
    if basis:
        needed = {"gpa": ["gpa"], "sat": ["sat"], "gpa_sat": ["gpa", "sat"]}[basis]
        mentioned = [w for w in needed if re.search(rf"\b{w}\b", reason, flags=re.IGNORECASE)]
        if not mentioned:
            errors.append(f"category_basis: lý do không nhắc {basis.upper()}, tiêu chí quyết định nhóm")

    # 5. Trả về danh sách lỗi
    return errors
