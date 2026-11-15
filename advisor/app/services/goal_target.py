"""Goal-seeking trong chat: nhóm đích model trả (target_group) thành mã backend hiểu.

SLM trả đúng một trong reach | target | safety. Tên trường chỉ là chữ: việc chọn đúng trường trong 249 trường làm ở backend,
vì cần chắc chắn và dùng chung cho các câu hỏi về một trường khác.
"""

import unicodedata

# Nhóm đích model trả -> mã nhóm của API goal-seek (backend gọi nhóm "target" là "match")
TARGET_CODES = {"reach": "reach", "target": "match", "safety": "safety"}


# Bỏ dấu và hoa thường để so chữ: "Vừa sức" thành "vua suc"
def plain(text: str) -> str:
    decomposed = unicodedata.normalize("NFD", text.lower().replace("đ", "d"))
    return "".join(ch for ch in decomposed if unicodedata.category(ch) != "Mn").strip()


# Mã nhóm đích của API: safety | match | reach. Không nói thì None để hệ thống chọn nhóm cao hơn một bậc
def target_code(group: object) -> str | None:
    return TARGET_CODES.get(group) if isinstance(group, str) else None
