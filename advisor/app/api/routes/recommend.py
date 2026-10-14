from fastapi import APIRouter, Depends, HTTPException

from app.api.deps import get_ec_reader, get_picker, get_scorer
from app.schemas.ec import EcRequest, EcResponse
from app.schemas.pick import PickRequest, PickResponse
from app.schemas.score import ScoreRequest, ScoreResponse
from app.services.ec_reader import EcError, EcReader
from app.services.picker import Picker, PickError
from app.services.scorer import Scorer

router = APIRouter(prefix="/recommend", tags=["recommend"])


# yêu cầu FastAPI định dạng và kiểm tra dữ liệu trả về theo schema EcResponse.
@router.post("/ec", response_model=EcResponse)
async def ec(req: EcRequest, reader: EcReader = Depends(get_ec_reader)) -> EcResponse:
    """Lượt ①: đọc hoạt động và giải thưởng, ra bậc ngoại khóa 1–5 (gọi 1 lần cho mỗi hồ sơ)."""
    try:
        return await reader.read(req)
    except EcError as ex:
        # 502: lỗi nằm ở dịch vụ phía sau (AI), không phải ở request của .NET
        raise HTTPException(status_code=502, detail=str(ex)) from ex


@router.post("/score", response_model=ScoreResponse)
async def score(req: ScoreRequest, scorer: Scorer = Depends(get_scorer)) -> ScoreResponse:
    """Lượt ②: chấm các trường còn lại sau lọc cứng, chia lô 8 trường."""
    return await scorer.score(req)


@router.post("/pick", response_model=PickResponse)
async def pick(req: PickRequest, picker: Picker = Depends(get_picker)) -> PickResponse:
    """Lượt ③: xếp hạng theo mức quan tâm, chia 4 khối, gắn mã lý do. Đổi nút ưu tiên thì chỉ gọi lại lượt này."""
    try:
        return await picker.pick(req)
    except PickError as ex:
        raise HTTPException(status_code=502, detail=str(ex)) from ex
