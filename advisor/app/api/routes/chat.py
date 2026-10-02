from fastapi import APIRouter, Depends

from app.api.deps import get_advisor
from app.core.security import verify_api_key
from app.schemas.chat import ChatRequest, ChatResponse
from app.services.advisor import AdvisorService

router = APIRouter(prefix="/api/v1", tags=["chat"], dependencies=[Depends(verify_api_key)])


@router.post("/chat", response_model=ChatResponse)
async def chat(request: ChatRequest, advisor: AdvisorService = Depends(get_advisor)) -> ChatResponse:
    return await advisor.chat(request)
