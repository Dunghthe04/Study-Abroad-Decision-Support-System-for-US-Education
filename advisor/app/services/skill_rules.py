"""Luật hồ sơ: thông tin bắt buộc theo mốc và hàm tìm mục còn thiếu (framework v5, mục 4.1).

Hồ sơ đủ để tư vấn khi có các mục trong REQUIRED_B3 (đánh giá hồ sơ) hoặc REQUIRED_B4 (thêm ngành, để gợi ý trường).
"Đã trả lời" nghĩa là đã xác nhận, hoặc học sinh nói rõ "không có" / "chưa rõ" (trừ các mục bắt buộc phải có giá trị thật).
"""

from app.schemas.chat import ProfileField

# Thông tin bắt buộc theo mốc, theo thứ tự hỏi (framework mục 4.1)
# B3 (đánh giá hồ sơ): GPA, tiếng Anh, SAT/ACT, ngân sách, học bổng. Chưa cần ngành. Bậc học luôn là đại học (UC-08), không hỏi.
# sat đại diện cả câu SAT/ACT: có ACT hoặc chứng chỉ chuẩn hóa khác cũng tính là đã trả lời (xem missing_fields)
REQUIRED_B3 = ["gpa", "english", "sat", "budget", "scholarship"]
# B4 (lộ trình, gợi ý trường): thêm ngành (để lọc trường)
REQUIRED_B4 = [*REQUIRED_B3, "major"]
# Phải có giá trị đã xác nhận, không được bỏ qua hay "chưa rõ".
# major: B3 không đòi ngành nên "chưa rõ" vẫn qua B3, nhưng B4 đòi ngành thật.
MUST_HAVE_VALUE = {"gpa", "budget", "major"}


# Kiểm tra thông tin
# Trường thông tin này đã được xử lý xong theo quy tắc của hệ thống chưa?
def is_answered(name: str, field_value: ProfileField | None) -> bool:
    if field_value is None:
        return False

    # Nếu có giá trị bắt buộc thì phải có giá trị đã xác nhận, không được bỏ qua
    if name in MUST_HAVE_VALUE:
        return field_value.status == "confirmed"
    return field_value.status in ("confirmed", "none", "unknown")


# Danh sách thông tin bắt buộc còn thiếu, theo thứ tự hỏi (mặc định tính theo mốc B4, mốc đầy đủ nhất)
def missing_fields(profile: dict[str, ProfileField], required: list[str] = REQUIRED_B4) -> list[str]:
    return [
        name
        for name in required
        if not (standardized_answered(profile) if name == "sat" else is_answered(name, profile.get(name)))
    ]


# Câu SAT/ACT đã trả lời: có SAT, ACT (kể cả bấm "Em chưa thi") hoặc chứng chỉ không phải tiếng Anh (AP, IB, GRE...)
def standardized_answered(profile: dict[str, ProfileField]) -> bool:
    if is_answered("sat", profile.get("sat")) or is_answered("act", profile.get("act")):
        return True
    certs = profile.get("certifications")
    if certs is None or not isinstance(certs.value, list):
        return False
    return any(isinstance(c, dict) and c.get("category") != "english" for c in certs.value)
