from fastapi import APIRouter, Depends
from app.api.deps import get_ec_reader
from app.schemas.ec import EcRequest, EcResponse
from app.services.ec_reader import EcReader
from typing import Annotated
from app.api.deps import get_ec_reader, get_scorer
from app.schemas.score import ScoreRequest, ScoreResponse
from app.services.scorer import Scorer
from app.api.deps import get_ec_reader, get_scorer, get_picker
from app.schemas.pick import PickRequest, PickResponse
from app.services.picker import Picker

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