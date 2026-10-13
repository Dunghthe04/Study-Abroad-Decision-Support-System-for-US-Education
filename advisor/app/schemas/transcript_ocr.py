"""Hợp đồng dữ liệu .NET <-> advisor cho bước OCR đọc bảng điểm (học bạ THPT)."""

from typing import Any
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


class TranscriptOcrRequest(CamelModel):
    image_base64: str = Field(..., description="Dữ liệu tệp ảnh hoặc PDF dạng base64")
    file_name: str | None = None
    content_type: str | None = None


class TranscriptOcrResponse(CamelModel):
    success: bool
    model_used: str | None = None
    terms: list[TranscriptTerm] = Field(default_factory=list)
    calculated_gpa4: float | None = None
    message: str | None = None
