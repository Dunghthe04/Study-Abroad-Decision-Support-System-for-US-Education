from collections.abc import Sequence

from app.core.study_levels import LABELS_VI
from app.rag.retriever import RetrievedChunk

SYSTEM_PROMPT = """Bạn là trợ lý tư vấn du học Mỹ của hệ thống Study Abroad Decision Support System.
Hệ thống hỗ trợ mọi bậc học: THCS/THPT, cao đẳng cộng đồng, đại học, thạc sĩ, tiến sĩ.
Quy tắc:
- Chỉ trả lời dựa trên phần NGỮ CẢNH được cung cấp. Không bịa số liệu, tên trường, học phí, hạn nộp.
- Mỗi đoạn ngữ cảnh có ghi bậc học. Không dùng thông tin của bậc này để trả lời cho bậc khác
  (ví dụ: yêu cầu SAT của đại học không áp dụng cho thạc sĩ; thạc sĩ/tiến sĩ thường xét GRE/GMAT, SOP, thư giới thiệu).
- Nếu câu trả lời phụ thuộc vào bậc học mà người dùng chưa nói rõ, hãy hỏi lại người dùng đang quan tâm bậc nào.
- Nếu ngữ cảnh không đủ, nói rõ "Hiện hệ thống chưa có thông tin về vấn đề này" và gợi ý liên hệ trung tâm tư vấn.
- Ghi số thứ tự nguồn dạng [1], [2] sau thông tin lấy từ ngữ cảnh.
- Trả lời bằng tiếng Việt, ngắn gọn, rõ ràng."""

DISCLAIMER = (
    "Thông tin chỉ mang tính tham khảo. Quy định visa, học phí và học bổng có thể thay đổi; "
    "hãy kiểm tra lại với nguồn chính thức hoặc trung tâm tư vấn."
)


def build_context(chunks: Sequence[RetrievedChunk]) -> str:
    if not chunks:
        return "(Không tìm thấy tài liệu liên quan.)"
    return "\n\n".join(
        f"[{i}] {c.title} (bậc: {LABELS_VI.get(c.study_level, c.study_level)})\n{c.content}"
        for i, c in enumerate(chunks, start=1)
    )


def build_user_level_note(study_level: str | None) -> str:
    if study_level is None:
        return "Người dùng chưa chọn bậc học."
    return f"Người dùng đang quan tâm bậc: {LABELS_VI[study_level]}."
