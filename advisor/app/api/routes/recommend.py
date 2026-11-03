from typing import Annotated

from fastapi import APIRouter, Depends

from app.api.deps import get_ec_reader, get_explainer
from app.schemas.ec import EcRequest, EcResponse
from app.schemas.explain import ExplainRequest, ExplainResponse
from app.services.ec_reader import EcReader
from app.services.explainer import Explainer

router = APIRouter(prefix="/api/v1/recommend", tags=["recommend"])


@router.post("/ec", response_model=EcResponse)  # EcResponse là định dạng FastApi trả về
async def ec(req: EcRequest, reader: Annotated[EcReader, Depends(get_ec_reader)]) -> EcResponse:
    return await reader.read(req)


@router.post("/explain", response_model=ExplainResponse)
async def explain(
    req: ExplainRequest, explainer: Annotated[Explainer, Depends(get_explainer)]
) -> ExplainResponse:
    return await explainer.explain(req)
