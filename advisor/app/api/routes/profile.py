from typing import Annotated

from fastapi import APIRouter, Depends

from app.api.deps import get_activity_reader, get_transcript_reader
from app.schemas.extracurricular import ExtracurricularRequest, ExtracurricularResponse
from app.schemas.transcript_ocr import TranscriptOcrRequest, TranscriptOcrResponse
from app.services.activity_reader import ActivityReader
from app.services.transcript_reader import TranscriptReader

router = APIRouter(prefix="/api/v1/profile", tags=["profile"])


@router.post("/extracurricular")
async def score_extracurricular(
    request: ExtracurricularRequest,
    reader: Annotated[ActivityReader, Depends(get_activity_reader)],
) -> ExtracurricularResponse:
    """Điểm ngoại khóa 0–4: LLM đọc chữ thành thuộc tính, công thức của nhóm tính điểm. LLM lỗi vẫn trả điểm."""
    return await reader.score(request)


@router.post("/transcript-ocr")
async def ocr_transcript(
    request: TranscriptOcrRequest,
    reader: Annotated[TranscriptReader, Depends(get_transcript_reader)],
) -> TranscriptOcrResponse:
    """[USAS-640] OCR đọc và bóc tách bảng điểm học bạ THPT thành các môn và học kỳ bằng Ollama Vision / LLM."""
    return await reader.read_transcript(request)
