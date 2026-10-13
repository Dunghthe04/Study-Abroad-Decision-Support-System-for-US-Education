from fastapi import APIRouter

router = APIRouter(prefix="/health", tags=["health"])


@router.get("/live")
async def live() -> dict[str, str]:
    """Service còn sống không (Docker HEALTHCHECK gọi endpoint này)."""
    return {"status": "ok"}
