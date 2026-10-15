"""THỰC NGHIỆM (không chạy trong hệ thống): AI tự chấm trường theo skill_score.md, chia lô, kiểm tra JSON.

Từng là lượt ② của advisor. Bộ đánh giá đo được AI chỉ đúng 33–48%, nên hệ thống chuyển sang
C# (SchoolScorer). Giữ lại để chạy lại thí nghiệm bằng run_eval.py cho báo cáo.
"""

from pathlib import Path
from typing import Any

from app.services.skill_call import SkillCall
from evals.recommendation.ai_score_schema import (
    ScoreItem,
    ScoreRequest,
    ScoreResponse,
    ScoreSchool,
    score_schema,
)

SKILL_PATH = Path(__file__).parent / "skills" / "skill_score.md"
BATCH_SIZE = 8  # 8 trường/lô: vừa num_ctx 6144


# kiểm tra kết quả JSON do AI trả về có đúng cấu trúc, đúng mã trường, đúng thứ tự và nằm trong các giới hạn hợp lệ hay không.
# raw dữ liệu AI trả về, codes là mã trường ban đầu
def check_batch(raw: dict[str, Any], codes: list[str]) -> list[ScoreItem]:
    """Kiểm tra JSON AI trả về cho 1 lô (mục 7). Sai thì ném ValueError."""

    items = []
    try:
        # Duyệt qua kết quả từng trường
        for x in raw["items"]:
            item = ScoreItem.model_validate(x)
            items.append(item)
    except (KeyError, TypeError) as ex:
        raise ValueError("JSON thiếu danh sách items") from ex

    actual_codes = []
    for item in items:
        actual_codes.append(item.c)
    # Nếu danh sách mã trường khác danh sách ban đầu
    if actual_codes != codes:
        raise ValueError("Mã trường hoặc thứ tự trường không khớp")

    for i in items:
        for name in ("pg", "ps", "A"):
            v = getattr(i, name)
            if v is not None and not 0 <= v <= 100:
                raise ValueError(f"{i.c}: {name} = {v} ngoài 0–100")
        if not -6 <= i.adj <= 6:
            raise ValueError(f"{i.c}: adj = {i.adj} ngoài −6..+6")
        for name in ("F", "H", "Q"):
            v = getattr(i, name)
            if v is not None and not 0 <= v <= 1:
                raise ValueError(f"{i.c}: {name} = {v} ngoài 0–1")
    return items


class Scorer:
    def __init__(
        self, llm: Any, model: str, skill_path: Path = SKILL_PATH, batch_size: int = BATCH_SIZE
    ) -> None:
        self._call = SkillCall(llm, model, skill_path)
        self.skill_version = self._call.skill_version
        self._batch_size = batch_size

    async def score(self, req: ScoreRequest) -> ScoreResponse:
        student = req.student.model_dump(exclude_none=True)
        items: list[ScoreItem] = []
        failed: list[str] = []
        for start in range(0, len(req.schools), self._batch_size):
            batch = req.schools[start : start + self._batch_size]
            result = await self._score_batch(student, batch)
            if result is None:
                failed += [s.c for s in batch]  # không bịa kết quả cho lô lỗi
            else:
                items += result
        return ScoreResponse(
            skill_version=self.skill_version, model=self._call.model, items=items, failed=failed
        )

    async def _score_batch(self, student: dict[str, Any], batch: list[ScoreSchool]) -> list[ScoreItem] | None:
        codes = [s.c for s in batch]
        user = {"student": student, "schools": [s.model_dump(exclude_none=True) for s in batch]}
        return await self._call.run(user, score_schema(codes), lambda raw: check_batch(raw, codes))
