"""Đồng bộ hồ sơ giữa bản nháp (do .NET gửi mỗi lượt) và trạng thái chat."""

from app.schemas.chat import ProfileField

# Khóa hồ sơ có chỗ lưu trong bản nháp form: nháp là nguồn thật, form sửa thì chat thấy.
# Khóa còn lại (education_system, intake, funding_source, relatives_state, visa_refused) chỉ có trong
# trạng thái chat, nháp không đụng tới.
DRAFT_KEYS = {
    "current_level",
    "current_grade",
    "target_level",
    "major",
    "gpa",
    "sat",
    "act",
    "english",
    "english_plan",
    "budget",
    "scholarship",
    "activities",
    "awards",
    "preferred_states",
    "school_type",
}


# Gộp hồ sơ từ nháp vào hồ sơ trong state đầu lượt
def merge_profile(
    # Hồ sơ đang tồn tại trong chat, và hồ sơ bản nháp .NET gửi sàn
    current: dict[str, ProfileField],
    incoming: dict[str, ProfileField],
) -> dict[str, ProfileField]:
    # giữ lại các trường không do nháp quản lý
    merged = {key: field for key, field in current.items() if key not in DRAFT_KEYS}
    for key, field in incoming.items():
        if key not in DRAFT_KEYS:
            continue
        # lấy giá trị cũ và so sánh với mới
        old = current.get(key)
        # Cùng giá trị thì giữ trạng thái đang có (ví dụ said_in_B0 chưa xác nhận), khác giá trị thì lấy của nháp
        merged[key] = old if old is not None and old.value == field.value else field
    # Khóa có trong nháp mà nháp không gửi (form đã xóa) thì không giữ lại
    return merged


# Khóa của nháp đổi giữa đầu và cuối lượt, để .NET ghi vào nháp và form nạp lại
def changed_keys(before: dict[str, ProfileField], after: dict[str, ProfileField]) -> list[str]:
    # Lấy ra Lấy tất cả khóa xuất hiện trong before và after rồi & với Draft > lấy ra trong drap thôi
    keys = {*before, *after} & DRAFT_KEYS
    # so sánh trước và sau, nếu key nào khác trả về
    return sorted(key for key in keys if before.get(key) != after.get(key))
