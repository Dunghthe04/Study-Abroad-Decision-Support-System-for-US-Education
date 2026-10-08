"""LLM đọc chữ của từng hoạt động ngoại khóa thành thuộc tính, công thức của nhóm tính điểm 0–4.

LLM chỉ phân loại (vai trò, tổ chức uy tín, phạm vi thực tế), không chấm điểm.
Code giữ 2 lan can: phạm vi không được cao hơn mức học sinh khai; LLM lỗi thì đọc vai trò bằng từ khóa.
Học sinh không khai phạm vi thì lấy phạm vi LLM đọc; LLM cũng lỗi thì lấy mức thấp nhất (cấp trường).
"""

import json
import logging
from typing import Any

from pydantic import BaseModel, ValidationError

from app.schemas.extracurricular import (
    ActivityInput,
    ExtracurricularRequest,
    ExtracurricularResponse,
    ScoredActivity,
)
from app.services.extracurricular import Activity, Role, extracurricular_score, role_from_text
from app.services.ollama import OllamaError
from app.services.recommender import JsonLLM

logger = logging.getLogger(__name__)

UNKNOWN_LEVEL = 1  # học sinh không khai phạm vi và LLM không đọc được: coi là cấp trường
MAX_LEVEL = 5

SYSTEM_PROMPT = """Bạn đọc các hoạt động ngoại khóa do một học sinh Việt Nam khai và PHÂN LOẠI từng hoạt động. Không chấm điểm.

- role (vai trò của học sinh trong hoạt động):
  founder = sáng lập, người lập ra · head = chủ nhiệm, chủ tịch, đội trưởng, trưởng nhóm chính, president, captain ·
  deputy = phó chủ nhiệm, phó, trưởng ban, vice · member = thành viên, tình nguyện viên, người tham gia,
  thực tập sinh, nhân viên, cộng tác viên.
- reputable_org: true khi tổ chức có uy tín rộng rãi bên ngoài trường: tổ chức quốc tế (UNICEF, UNESCO, WWF…),
  cơ quan hoặc tổ chức cấp quốc gia, cuộc thi chính thức cấp quốc gia/quốc tế, trường đại học hoặc viện nghiên cứu.
  CLB trong trường, nhóm tự lập, công ty nhỏ: false.
- impact_level: phạm vi ảnh hưởng THỰC TẾ theo mô tả: 1 trường, 2 quận/huyện, 3 tỉnh/thành, 4 quốc gia, 5 quốc tế.
  Không được cao hơn mức học sinh tự khai (khóa "muc_khai"). Học sinh hay khai cao hơn thực tế, hãy hạ xuống khi mô tả
  cho thấy phạm vi hẹp hơn. Ví dụ: CLB của lớp hoặc của trường, hoạt động chỉ diễn ra trong trường → 1, dù học sinh khai 5;
  giải hoặc hoạt động cấp quận → 2; cấp tỉnh/thành → 3. Mô tả không đủ để đánh giá thì giữ mức học sinh khai.
  "muc_khai" là null (học sinh không khai) thì tự đánh giá từ mô tả; mô tả không đủ thì chọn 1.

Trả về đủ mọi hoạt động, giữ nguyên id. Chỉ dựa vào chữ học sinh viết, không đoán thêm."""


class _ReadActivity(BaseModel):
    id: str
    role: Role
    reputable_org: bool
    impact_level: int


class _ReadResponse(BaseModel):
    activities: list[_ReadActivity]


class ActivityReader:
    def __init__(self, llm: JsonLLM) -> None:
        self._llm = llm

    async def score(self, request: ExtracurricularRequest) -> ExtracurricularResponse:
        activities = request.activities
        read: dict[str, _ReadActivity] = {}
        ai_used = False
        if activities:
            try:
                raw = await self._llm.chat_json(_messages(activities), _output_schema(activities))
                read = {r.id: r for r in _ReadResponse.model_validate(raw).activities}
                ai_used = True
            except (OllamaError, ValidationError) as ex:
                # Không chặn việc lọc trường: đọc vai trò bằng từ khóa, giữ mức học sinh khai
                logger.warning("LLM không đọc được hoạt động, dùng từ khóa: %s", ex)

        attributes = [to_activity(a, read.get(a.id)) for a in activities]
        result = extracurricular_score(attributes)
        return ExtracurricularResponse(
            score=result.score,
            ai_used=ai_used,
            activities=[
                ScoredActivity(
                    id=a.id,
                    role=attr.role,
                    reputable_org=attr.reputable_org,
                    impact_level=attr.impact_level,
                    quality=s.quality,
                    points=s.points,
                    counted=s.counted,
                )
                for a, attr, s in zip(activities, attributes, result.activities, strict=True)
            ],
        )


def to_activity(a: ActivityInput, read: _ReadActivity | None) -> Activity:
    """Ghép thuộc tính LLM đọc với số liệu học sinh khai; LLM bỏ sót hoạt động này thì dùng từ khóa."""
    if read is None:
        # Không khai, LLM cũng không đọc được: lấy mức thấp nhất, không đoán cao hơn
        level = a.impact_level or UNKNOWN_LEVEL
        return Activity(impact_level=level, role=role_from_text(a.role), months=a.months)
    # Lan can: LLM chỉ được giữ hoặc hạ phạm vi, không được nâng cao hơn mức học sinh khai
    ceiling = a.impact_level or MAX_LEVEL
    level = max(1, min(read.impact_level, ceiling))
    return Activity(impact_level=level, role=read.role, months=a.months, reputable_org=read.reputable_org)


def _messages(activities: list[ActivityInput]) -> list[dict[str, str]]:
    data = [
        {
            "id": a.id,
            "ten": a.name,
            "vai_tro": a.role,
            "to_chuc": a.organization,
            "mo_ta": a.description,
            "muc_khai": a.impact_level,
        }
        for a in activities
    ]
    return [
        {"role": "system", "content": SYSTEM_PROMPT},
        {"role": "user", "content": "Các hoạt động (JSON):\n" + json.dumps(data, ensure_ascii=False)},
    ]


def _output_schema(activities: list[ActivityInput]) -> dict[str, Any]:
    ids = [a.id for a in activities]
    return {
        "type": "object",
        "properties": {
            "activities": {
                "type": "array",
                "minItems": len(ids),
                "maxItems": len(ids),
                "items": {
                    "type": "object",
                    "properties": {
                        "id": {"type": "string", "enum": ids},
                        "role": {"type": "string", "enum": ["member", "deputy", "head", "founder"]},
                        "reputable_org": {"type": "boolean"},
                        "impact_level": {"type": "integer", "minimum": 1, "maximum": 5},
                    },
                    "required": ["id", "role", "reputable_org", "impact_level"],
                },
            }
        },
        "required": ["activities"],
    }
