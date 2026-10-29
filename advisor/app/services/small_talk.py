"""Lời xã giao ngắn (chào, cảm ơn, tạm biệt) nhận ra bằng luật, không cần AI.

Model chọn skill chưa học kỹ các câu này ở mọi bước, nên có lúc xếp "chào bạn" vào ngoài phạm vi và học sinh bị đáp
"Mình chỉ hỗ trợ tư vấn du học Mỹ thôi nhé". OffTopicHandler gọi classify() trước khi trả câu từ chối.
Chỉ nhận câu NGẮN thuần xã giao; câu có nội dung hỏi (ví dụ "chào bạn, IELTS là gì") không bị coi là xã giao.
"""

import re
import unicodedata
from typing import Literal

SmallTalk = Literal["greeting", "thanks", "goodbye"]

MAX_WORDS = 6  # dài hơn thì học sinh đang hỏi điều gì đó, không phải xã giao thuần
GREETING_WORDS = {"chao", "hello", "hi", "hey", "alo", "helo"}
THANKS_WORDS = {"cam", "thanks", "thank", "thankyou"}
GOODBYE_WORDS = {"tam", "biet", "bye", "goodbye", "hen", "gap", "lai"}
# Chữ đi kèm lời xã giao mà không thêm nội dung hỏi
FILLER = {
    "xin", "on", "ban", "bot", "ad", "admin", "anh", "chi", "em", "minh", "a", "nhe", "nha", "nhen", "ha",
    "you", "nhieu", "rat", "da", "vang", "oi", "tu", "van", "vien", "tro", "ly",
}  # fmt: skip


# Bỏ dấu, chữ thường, bỏ dấu câu: "Cảm ơn bạn!" thành "cam on ban"
def plain(text: str) -> str:
    text = text.lower().replace("đ", "d")
    text = "".join(c for c in unicodedata.normalize("NFD", text) if unicodedata.category(c) != "Mn")
    return re.sub(r"[^a-z0-9 ]+", " ", text)


# Trả "greeting" / "thanks" / "goodbye" nếu cả câu chỉ là xã giao, không thì None
def classify(message: str) -> SmallTalk | None:
    words = plain(message).split()
    if not words or len(words) > MAX_WORDS:
        return None
    known = GREETING_WORDS | THANKS_WORDS | GOODBYE_WORDS | FILLER
    if any(w not in known for w in words):  # có chữ lạ: học sinh đang nói điều khác
        return None
    # "cảm ơn bạn, tạm biệt" vẫn là tạm biệt nên xét tạm biệt trước
    if "biet" in words or "bye" in words or "goodbye" in words or ("hen" in words and "gap" in words):
        return "goodbye"
    if THANKS_WORDS & set(words):
        return "thanks"
    if GREETING_WORDS & set(words):
        return "greeting"
    return None
