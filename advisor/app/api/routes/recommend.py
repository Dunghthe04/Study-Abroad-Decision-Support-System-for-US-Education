from fastapi import APIRouter, Depends, HTTPException

from app.api.deps import get_ec_reader, get_explainer
from app.schemas.ec import EcRequest, EcResponse
from app.schemas.explain import ExplainRequest, ExplainResponse
from app.services.ec_reader import EcReader
from app.services.explainer import Explainer, ExplainError

router = APIRouter(prefix="/recommend", tags=["recommend"])


# yêu cầu FastAPI định dạng và kiểm tra dữ liệu trả về theo schema EcResponse.
@router.post("/ec", response_model=EcResponse)
async def ec(req: EcRequest, reader: EcReader = Depends(get_ec_reader)) -> EcResponse:
    """Bậc ngoại khóa: AI đọc vai trò, phạm vi từng hoạt động; Python tính q, m, bậc 1–5.

    AI lỗi thì dùng vai trò học sinh chọn ở form (ai_used = false), không trả lỗi.
    Chấm trường và chia khối chạy ở backend .NET (SchoolScorer, SchoolPicker).
    """
    return await reader.read(req)


@router.post("/explain", response_model=ExplainResponse)
async def explain(req: ExplainRequest, explainer: Explainer = Depends(get_explainer)) -> ExplainResponse:
    """AI tư vấn 1 trường: giải thích kết quả đã chấm và gợi ý việc nên làm (gọi khi học sinh mở chi tiết trường)."""
    try:
        return await explainer.explain(req)
    except ExplainError as ex:
        raise HTTPException(status_code=502, detail=str(ex)) from ex
