from typing import Annotated

from fastapi import APIRouter, Depends

from app.api.deps import get_transcript_reader
from app.schemas.transcript_ocr import TranscriptOcrRequest, TranscriptOcrResponse
from app.services import usage_meter
from app.services.transcript_reader import TranscriptReader

router = APIRouter(prefix="/api/v1/profile", tags=["profile"])


@router.post("/transcript-ocr")
async def ocr_transcript(
    request: TranscriptOcrRequest,
    reader: Annotated[TranscriptReader, Depends(get_transcript_reader)],
) -> TranscriptOcrResponse:
    """[USAS-640] OCR đọc và bóc tách bảng điểm học bạ THPT thành các môn và học kỳ bằng Ollama Vision / LLM."""
    # Đo token của mọi lần gọi AI khi đọc học bạ. Nhãn từng bước (transcript_ocr_read, transcript_ocr_parse) do TranscriptReader đặt
    return await usage_meter.metered(reader.read_transcript(request))
