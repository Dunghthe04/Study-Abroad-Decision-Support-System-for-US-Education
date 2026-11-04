from typing import Annotated

from fastapi import APIRouter, Depends

from app.api.deps import get_ec_reader, get_explainer, get_scenario_writer
from app.schemas.ec import EcRequest, EcResponse
from app.schemas.explain import ExplainRequest, ExplainResponse
from app.schemas.scenario_note import ScenarioNoteRequest, ScenarioNoteResponse
from app.services.ec_reader import EcReader
from app.services.explainer import Explainer
from app.services.scenario_writer import ScenarioWriter

router = APIRouter(prefix="/api/v1/recommend", tags=["recommend"])


@router.post("/ec", response_model=EcResponse)  # EcResponse là định dạng FastApi trả về
async def ec(req: EcRequest, reader: Annotated[EcReader, Depends(get_ec_reader)]) -> EcResponse:
    return await reader.read(req)


@router.post("/explain", response_model=ExplainResponse)
async def explain(
    req: ExplainRequest, explainer: Annotated[Explainer, Depends(get_explainer)]
) -> ExplainResponse:
    return await explainer.explain(req)


@router.post("/what-if", response_model=ScenarioNoteResponse)
async def what_if(
    req: ScenarioNoteRequest, writer: Annotated[ScenarioWriter, Depends(get_scenario_writer)]
) -> ScenarioNoteResponse:
    return await writer.write(req)
