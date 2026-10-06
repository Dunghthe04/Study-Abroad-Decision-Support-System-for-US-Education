from typing import Annotated  # viết Kiểu dữ liệu + thêm thông tin/metadata cho kiểu dữ liệu đó.

from fastapi import APIRouter, Depends, HTTPException

from app.api.deps import get_recommender
from app.schemas.recommendation import AiRankRequest, AiRankResponse
from app.services.ollama import OllamaError
from app.services.recommender import Recommender, RecommenderError

router = APIRouter(prefix="/api/v1/recommendations", tags=["recommendations"])


# decorator
@router.post("/explain")
async def explain(
    request: AiRankRequest,
    # gọi đến service
    recommender: Annotated[Recommender, Depends(get_recommender)],
) -> AiRankResponse:
    """Nhận danh sách trường CRM đã lọc, trả lại thứ tự mới và lý do do LLM viết."""
    try:
        return await recommender.rank(request)
    except (OllamaError, RecommenderError) as ex:
        # 502 = lỗi ở phía LLM. .NET nhận lỗi này sẽ dùng kết quả CRM và lý do soạn sẵn
        raise HTTPException(status_code=502, detail=str(ex)) from ex
