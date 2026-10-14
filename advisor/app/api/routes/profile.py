from typing import Annotated

from fastapi import APIRouter, Depends

from app.api.deps import get_transcript_reader
from app.schemas.transcript_ocr import TranscriptOcrRequest, TranscriptOcrResponse
from app.services.transcript_reader import TranscriptReader

router = APIRouter(prefix="/api/v1/profile", tags=["profile"])


@router.post("/transcript-ocr")
async def ocr_transcript(
    request: TranscriptOcrRequest,
    reader: Annotated[TranscriptReader, Depends(get_transcript_reader)],
) -> TranscriptOcrResponse:
    """[USAS-640] OCR đọc và bóc tách bảng điểm học bạ THPT thành các môn và học kỳ bằng Ollama Vision / LLM."""
    return await reader.read_transcript(request)
