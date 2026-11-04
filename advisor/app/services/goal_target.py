"""Goal-seeking trong chat: đọc nhóm đích học sinh nói ("Vừa sức", "an toàn", "match") thành mã backend hiểu.

SLM chỉ trích đoạn chữ (muc_tieu). Tên trường cũng chỉ là chữ: việc chọn đúng trường trong 249 trường làm ở backend,
vì cần chắc chắn và dùng chung cho các câu hỏi về một trường khác.
"""

import unicodedata

# Mã nhóm đích của API goal-seek. Dò "an toàn" trước vì "Vừa sức" và "An toàn hơn" có thể xuất hiện chung trong một câu
TARGET_WORDS = (
    ("safety", ("an toan", "safety")),
    ("match", ("vua suc", "match", "target")),
    ("reach", ("kho vao", "reach")),
)


# Bỏ dấu và hoa thường để so chữ: "Vừa sức" thành "vua suc"
def plain(text: str) -> str:
    decomposed = unicodedata.normalize("NFD", text.lower().replace("đ", "d"))
    return "".join(ch for ch in decomposed if unicodedata.category(ch) != "Mn").strip()


# Nhóm đích: safety | match | reach. Không nói hoặc không nhận ra thì None để hệ thống chọn nhóm cao hơn một bậc
def parse_target(text: object) -> str | None:
    if not isinstance(text, str) or not text.strip():
        return None
    words = plain(text)
    for code, options in TARGET_WORDS:
        if any(option in words for option in options):
            return code
    return None
