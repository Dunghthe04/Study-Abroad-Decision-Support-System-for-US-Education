import asyncio
import json
import logging
from collections.abc import AsyncIterator
from typing import Annotated, Any

from fastapi import APIRouter, Depends
from fastapi.responses import StreamingResponse

from app.api.deps import get_chat_flow
from app.schemas.chat import ChatRequest, ChatResponse
from app.services.chat_flow import ChatFlow

router = APIRouter(prefix="/api/v1/chat", tags=["chat"])
logger = logging.getLogger("advisor.api")


# by_alias: trả JSON camelCase (currentStep, lastQuestion) cho .NET
@router.post("", response_model=ChatResponse, response_model_by_alias=True)
async def chat(req: ChatRequest, flow: Annotated[ChatFlow, Depends(get_chat_flow)]) -> ChatResponse:
    return await flow.reply(req)


# Trả lời theo từng ý khi ý đó xong. Mỗi dòng là một JSON (NDJSON):
#   {"type": "progress", "answers": N}   sau khi SLM tách ý: sắp trả lời N câu hỏi
#   {"type": "partial", "text": "..."}   câu trả lời của một ý, đúng thứ tự học sinh hỏi
#   {"type": "final", "response": {...}} đúng JSON của POST /api/v1/chat; state, hồ sơ, nút chỉ đúng ở dòng này
#   {"type": "error", "message": "..."}  lỗi giữa chừng
# .NET chuyển tiếp các dòng cho trình duyệt; câu hiện cuối cùng lấy từ dòng final
@router.post("/stream")
async def chat_stream(
    req: ChatRequest, flow: Annotated[ChatFlow, Depends(get_chat_flow)]
) -> StreamingResponse:
    events: asyncio.Queue[dict[str, Any] | None] = asyncio.Queue()

    async def produce() -> None:
        try:
            res = await flow.reply(req, emit=events.put_nowait)
            events.put_nowait({"type": "final", "response": res.model_dump(mode="json", by_alias=True)})
        except Exception:
            logger.exception("Chat stream bị lỗi")
            events.put_nowait({"type": "error", "message": "Advisor gặp lỗi khi trả lời."})
        finally:
            events.put_nowait(None)

    async def lines() -> AsyncIterator[str]:
        task = asyncio.create_task(produce())
        try:
            while (event := await events.get()) is not None:
                yield json.dumps(event, ensure_ascii=False) + "\n"
        finally:
            task.cancel()  # trình duyệt ngắt giữa chừng thì thôi, không chạy tiếp

    # X-Accel-Buffering: bảo Nginx không giữ lại, gửi từng dòng ngay
    return StreamingResponse(
        lines(),
        media_type="application/x-ndjson",
        headers={"Cache-Control": "no-cache", "X-Accel-Buffering": "no"},
    )
