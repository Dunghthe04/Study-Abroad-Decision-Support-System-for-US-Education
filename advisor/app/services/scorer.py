"""Chấm trường trên cloud: chia lô, kiểm tra JSON, lô sai thì ghi failed."""

from pathlib import Path
from typing import Any

from app.schemas.score import ScoreItem, ScoreRequest, ScoreResponse, ScoreSchool, score_schema
from app.services.skill_call import SKILLS_DIR, SkillCall

SKILL_PATH = SKILLS_DIR / "skill_score.md"
BATCH_SIZE = 8  # một lần gọi không nhồi quá nhiều trường


# Đầu vào danh sách AI trả về, và danh sách mã trường gửi AI chấm. Đầu ra danh sách ScoreItem đã kiểm tra.
def check_batch(raw: dict[str, Any], codes: list[str]) -> list[ScoreItem]:
    """Sai khuôn, sai mã hoặc số ngoài khoảng thì ném ValueError để SkillCall gọi lại."""
    try:
        # Kiểm tra schema từng trường AI trả về
        items = []
        for x in raw["items"]:
            item = ScoreItem.model_validate(x)
            items.append(item)
    except (KeyError, TypeError) as ex:
        raise ValueError("JSON thiếu danh sách items") from ex

    for item in items:
        if item.c not in codes:
            raise ValueError("Mã trường hoặc thứ tự trường không khớp")

    # Kiểm tra các điểm trong khoảng 0-100
    # Duyể từng trường
    for item in items:
        # Duyệt từng thuộc tính
        for name in ("pg", "ps", "A"):
            value = getattr(item, name)
            if value is not None and not 0 <= value <= 100:
                raise ValueError(f"{item.c}: {name} = {value} ngoài 0–100")
        if not -6 <= item.adj <= 6:
            raise ValueError(f"{item.c}: adj = {item.adj} ngoài −6..+6")
        for name in ("F", "H", "Q"):
            value = getattr(item, name)
            if value is not None and not 0 <= value <= 1:
                raise ValueError(f"{item.c}: {name} = {value} ngoài 0–1")
    return items


class Scorer:
    def __init__(
        self, llm: Any, model: str, skill_path: Path = SKILL_PATH, batch_size: int = BATCH_SIZE
    ) -> None:
        self._call = SkillCall(llm, model, skill_path)
        self.skill_version = self._call.skill_version
        self._batch_size = batch_size

    async def score(self, req: ScoreRequest) -> ScoreResponse:
        # Kiểm tra dữ liệu đầu vào, nếu thiếu thì bỏ key, không gửi 0.
        student = req.student.model_dump(exclude_none=True)
        items: list[ScoreItem] = []
        failed: list[str] = []
        for start in range(0, len(req.schools), self._batch_size):
            # Lấy các trường ở batch hiện tại
            batch = req.schools[start : start + self._batch_size]
            # Gọi SkillCall để chấm batch
            result = await self._score_batch(student, batch)
            if result is None:
                failed += [school.c for school in batch]
            else:
                items += result
        return ScoreResponse(
            skill_version=self.skill_version,
            model=self._call.model,
            items=items,
            failed=failed,
        )

    async def _score_batch(self, student: dict[str, Any], batch: list[ScoreSchool]) -> list[ScoreItem] | None:
        codes = [school.c for school in batch]
        user = {
            "student": student,
            "schools": [school.model_dump(exclude_none=True) for school in batch],
        }
        return await self._call.run(user, score_schema(codes), lambda raw: check_batch(raw, codes))
