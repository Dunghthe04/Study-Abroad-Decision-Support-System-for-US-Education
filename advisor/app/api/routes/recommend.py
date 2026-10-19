from typing import Annotated

from fastapi import APIRouter, Depends

from app.api.deps import get_ec_reader, get_explainer, get_picker, get_scorer
from app.schemas.ec import EcRequest, EcResponse
from app.schemas.explain import ExplainRequest, ExplainResponse
from app.schemas.pick import PickRequest, PickResponse
from app.schemas.score import ScoreRequest, ScoreResponse
from app.services.ec_reader import EcReader
from app.services.explainer import Explainer
from app.services.picker import Picker
from app.services.scorer import Scorer

router = APIRouter(prefix="/api/v1/recommend", tags=["recommend"])


@router.post("/ec", response_model=EcResponse)
async def ec(req: EcRequest, reader: Annotated[EcReader, Depends(get_ec_reader)]) -> EcResponse:
    return await reader.read(req)


@router.post("/score", response_model=ScoreResponse)
async def score(req: ScoreRequest, scorer: Annotated[Scorer, Depends(get_scorer)]) -> ScoreResponse:
    return await scorer.score(req)


@router.post("/pick", response_model=PickResponse)
async def pick(req: PickRequest, picker: Annotated[Picker, Depends(get_picker)]) -> PickResponse:
    return await picker.pick(req)


@router.post("/explain", response_model=ExplainResponse)
async def explain(
    req: ExplainRequest, explainer: Annotated[Explainer, Depends(get_explainer)]
) -> ExplainResponse:
    return await explainer.explain(req)
