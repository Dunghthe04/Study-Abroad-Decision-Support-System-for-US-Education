"""Lời giải thích "điểm phù hợp tính thế nào" (UC-153 1a): Claude viết từ tham số backend đang công bố.

Tham số lấy từ trang công khai GET /api/v1/methodology/parameters (cùng nguồn với trang Căn cứ tham số, UC-83) nên lời luôn khớp
bảng công bố. Code kiểm: nhắc đủ năm thành phần, mọi số phải có trong tham số, không hứa kết quả. Sai hoặc backend không trả lời được
thì trả None, nhánh dùng câu cố định replies.FIT_METHOD.
"""

import logging
from pathlib import Path
from typing import Any

import httpx

from app.schemas.fit_method_note import FitMethodResult, fit_method_schema
from app.services.note_checks import check_mentions_all, check_no_promise, check_numbers, numbers
from app.services.skill_call import SKILLS_DIR, SkillCall

logger = logging.getLogger("advisor.fit_method")
SKILL_PATH = Path(SKILLS_DIR) / "skill_cach_tinh_diem.md"
PARAMETERS_URL = "/api/v1/methodology/parameters"
FIT_STEP = "B10"  # bước điểm phù hợp trong danh mục tham số
COMPONENTS = ("vừa tầm", "ngân sách", "chất lượng", "học bổng", "tiếng anh")


def fit_parameters(payload: dict[str, Any]) -> list[dict[str, str]]:
    return [
        {"name": p["name"], "value": p["value"], "meaning": p["meaning"]}
        for p in payload.get("parameters", [])
        if p.get("step") == FIT_STEP
    ]


def check_answer(raw: dict[str, Any], user: dict[str, Any]) -> str:
    answer = FitMethodResult.model_validate(raw).answer.strip()
    check_mentions_all(answer, COMPONENTS, "năm thành phần của điểm phù hợp")
    check_no_promise(answer)
    allowed = {n for p in user["parameters"] for n in numbers(f"{p['value']} {p['meaning']}")}
    check_numbers(answer, allowed | {100.0}, "tham số công bố")  # 100: thang chấm của từng thành phần
    return answer


class FitMethodWriter:
    def __init__(self, llm: Any, model: str, http: httpx.AsyncClient, skill_path: Path = SKILL_PATH) -> None:
        self._call = SkillCall(llm, model, skill_path)
        self._http = http
        self.skill_version = self._call.skill_version

    async def write(self) -> str | None:
        try:
            response = await self._http.get(PARAMETERS_URL)
            response.raise_for_status()
            parameters = fit_parameters(response.json())
        except (httpx.HTTPError, ValueError, KeyError):
            logger.warning("không lấy được tham số công bố", exc_info=True)
            return None
        if not parameters:
            return None
        user = {"parameters": parameters}
        return await self._call.run(user, fit_method_schema(), lambda raw: check_answer(raw, user))
