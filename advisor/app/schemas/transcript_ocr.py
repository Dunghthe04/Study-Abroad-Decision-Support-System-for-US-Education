"""Hợp đồng dữ liệu .NET <-> advisor cho bước OCR đọc bảng điểm (học bạ THPT/ĐH đa tệp)."""

from pydantic import Field

from app.schemas.recommendation import CamelModel


class TranscriptSubjectScore(CamelModel):
    subject: str = Field(..., description="Tên môn học (Toán, Ngữ văn, Tiếng Anh...)")
    score: float = Field(..., description="Điểm số thang 10")
    raw_score: str | None = None
    credits: float | None = 1.0


class TranscriptTerm(CamelModel):
    term_order: int
    term_name: str
    scores: list[TranscriptSubjectScore] = Field(default_factory=list)


# Giới hạn request: .NET chỉ nhận tệp ≤ 10MB, base64 dài hơn ~4/3 lần (10MB -> ~14 triệu ký tự)
MAX_FILES = 10
MAX_BASE64_CHARS = 14_000_000


class TranscriptFilePayload(CamelModel):
    image_base64: str = Field(
        ..., max_length=MAX_BASE64_CHARS, description="Dữ liệu tệp ảnh hoặc PDF dạng base64"
    )
    file_name: str | None = Field(default=None, max_length=1000)
    content_type: str | None = Field(default=None, max_length=100)


class TranscriptOcrRequest(CamelModel):
    # Hỗ trợ gửi danh sách nhiều file (từng trang học bạ) hoặc 1 file đơn lẻ
    files: list[TranscriptFilePayload] = Field(default_factory=list, max_length=MAX_FILES)
    image_base64: str | None = Field(default=None, max_length=MAX_BASE64_CHARS)
    file_name: str | None = Field(default=None, max_length=1000)
    content_type: str | None = Field(default=None, max_length=100)


class TranscriptOcrResponse(CamelModel):
    success: bool
    model_used: str | None = None
    terms: list[TranscriptTerm] = Field(default_factory=list)
    average_score_scale10: float | None = None
    calculated_gpa4: float | None = None
    message: str | None = None
    warnings: list[str] = Field(default_factory=list)
