"""Luật kiểm tra giá trị học sinh nói trong chat, trước khi ghi vào hồ sơ (HUONG_DAN mục 7.1).

Giá trị sai thì ném AskAgain kèm câu hỏi lại bằng tiếng Việt, không ghi bừa vào hồ sơ.
"""

GPA_SCALES = (4, 10)


class AskAgain(Exception):
    def __init__(self, question: str) -> None:
        super().__init__(question)
        self.question = question


def check_gpa(value: float, scale: int | None = None) -> tuple[float, int]:
    """Kiểm tra GPA, ném AskAgain nếu sai. Trả về (gpa, scale)."""
    if value < 0:
        raise AskAgain("GPA không được âm. Bạn vui lòng nhập lại GPA của bạn.")

    if scale is None:
        scale = 4 if value <= 4 else 10

    if scale not in GPA_SCALES:
        raise AskAgain(f"Thang điểm {scale} không hợp lệ. Bạn vui lòng nhập lại GPA của bạn.")

    if value > scale:
        raise AskAgain(f"GPA {value} vượt quá thang điểm {scale}. Bạn vui lòng nhập lại GPA của bạn.")
    return value, scale
