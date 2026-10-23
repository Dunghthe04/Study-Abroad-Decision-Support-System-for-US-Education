from typing import Annotated

from fastapi import APIRouter, Depends

from app.api.deps import get_chat_flow
from app.schemas.chat import ChatRequest, ChatResponse
from app.services.chat_flow import ChatFlow

router = APIRouter(prefix="/api/v1/chat", tags=["chat"])


# by_alias: trả JSON camelCase (currentStep, lastQuestion) cho .NET
@router.post("", response_model=ChatResponse, response_model_by_alias=True)
async def chat(req: ChatRequest, flow: Annotated[ChatFlow, Depends(get_chat_flow)]) -> ChatResponse:
    return await flow.reply(req)
