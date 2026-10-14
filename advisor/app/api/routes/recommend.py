from fastapi import APIRouter, Depends

from app.api.deps import get_scorer
from app.schemas.score import ScoreRequest, ScoreResponse
from app.services.scorer import Scorer

router = APIRouter(prefix="/recommend", tags=["recommend"])
@router.post("/score", response_model = ScoreResponse)
async def score(req: ScoreRequest, scorer: Scorer = Depends(get_scorer)) -> ScoreResponse:
    """Lượt ②: chấm các trường còn lại sau lọc cứng, chia lô 8 trường."""
    return await scorer.score(req)