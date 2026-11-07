from typing import Annotated

from fastapi import APIRouter, Depends

from app.api.deps import (
    get_compare_writer,
    get_ec_reader,
    get_explainer,
    get_goal_writer,
    get_scenario_writer,
)
from app.schemas.compare_note import CompareNoteRequest, CompareNoteResponse
from app.schemas.ec import EcRequest, EcResponse
from app.schemas.explain import ExplainRequest, ExplainResponse
from app.schemas.goal_note import GoalNoteRequest, GoalNoteResponse
from app.schemas.scenario_note import ScenarioNoteRequest, ScenarioNoteResponse
from app.services.compare_writer import CompareWriter
from app.services.ec_reader import EcReader
from app.services.explainer import Explainer
from app.services.goal_writer import GoalWriter
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


@router.post("/compare", response_model=CompareNoteResponse)
async def compare(
    req: CompareNoteRequest, writer: Annotated[CompareWriter, Depends(get_compare_writer)]
) -> CompareNoteResponse:
    return await writer.write(req)


@router.post("/goal-seek", response_model=GoalNoteResponse)
async def goal_seek(
    req: GoalNoteRequest, writer: Annotated[GoalWriter, Depends(get_goal_writer)]
) -> GoalNoteResponse:
    return await writer.write(req)
