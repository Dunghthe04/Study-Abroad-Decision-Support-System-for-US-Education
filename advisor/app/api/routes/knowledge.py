import logging
from typing import Annotated

from fastapi import APIRouter, Depends

from app.api.deps import get_embed_job
from app.schemas.common import CamelModel
from app.services.knowledge_embedding import KnowledgeEmbedJob

router = APIRouter(prefix="/api/v1/knowledge", tags=["knowledge"])
logger = logging.getLogger("advisor.api")


# pending: số dòng còn thiếu vector (kể cả dòng đang được tạo). done, error: của lần chạy gần nhất
class EmbedStatus(CamelModel):
    pending: int
    running: bool
    done: int
    error: str | None = None


class EmbedStarted(EmbedStatus):
    started: bool  # False: không có gì để làm, hoặc đang có một lần chạy khác


# Backend gọi sau khi moderator import hoặc sửa dòng: tạo vector cho dòng còn thiếu, chạy nền, trả lời ngay
@router.post("/embed-missing", response_model=EmbedStarted, response_model_by_alias=True)
async def embed_missing(job: Annotated[KnowledgeEmbedJob, Depends(get_embed_job)]) -> EmbedStarted:
    pending = await job.count_pending()
    started = pending > 0 and job.start()
    if started:
        logger.info("Bắt đầu tạo vector cho %s dòng kho kiến thức", pending)
    return EmbedStarted(pending=pending, running=job.running, done=job.done, error=job.error, started=started)


# Trang admin hỏi tiến độ
@router.get("/embed-status", response_model=EmbedStatus, response_model_by_alias=True)
async def embed_status(job: Annotated[KnowledgeEmbedJob, Depends(get_embed_job)]) -> EmbedStatus:
    return EmbedStatus(pending=await job.count_pending(), running=job.running, done=job.done, error=job.error)
