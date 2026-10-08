from typing import Annotated

from fastapi import APIRouter, Depends

from app.api.deps import get_activity_reader
from app.schemas.extracurricular import ExtracurricularRequest, ExtracurricularResponse
from app.services.activity_reader import ActivityReader

router = APIRouter(prefix="/api/v1/profile", tags=["profile"])


@router.post("/extracurricular")
async def score_extracurricular(
    request: ExtracurricularRequest,
    reader: Annotated[ActivityReader, Depends(get_activity_reader)],
) -> ExtracurricularResponse:
    """Điểm ngoại khóa 0–4: LLM đọc chữ thành thuộc tính, công thức của nhóm tính điểm. LLM lỗi vẫn trả điểm."""
    return await reader.score(request)
