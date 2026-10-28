"""Lấy trung tâm tư vấn cho B6 (skill_chuyen_trung_tam) từ backend .NET.

Luật chọn trung tâm nằm ở backend (CenterReferral): đã xác minh, đang hoạt động, đúng dịch vụ và bậc học,
cùng thành phố xếp trước, tối đa 3. Advisor chỉ gọi API, không đọc bảng study_centers,
giống phần gợi ý trường: lọc theo luật ở backend, advisor chỉ làm phần AI.
"""

from dataclasses import dataclass
from typing import Any

import httpx

# Danh mục dịch vụ: mã lưu trong cột services -> tên hiển thị cho học sinh.
# Một nguồn duy nhất cho lời nhắc của AI, nút gợi ý và câu trả lời. Thêm dịch vụ mới thì thêm ở đây.
SERVICES = {
    "CHON_TRUONG": "chọn trường",
    "SUA_BAI_LUAN": "sửa bài luận",
    "NOP_HO_SO": "nộp hồ sơ",
    "LUYEN_PHONG_VAN": "luyện phỏng vấn visa",
    "LUYEN_THI": "luyện thi IELTS, SAT",
    "SAN_HOC_BONG": "săn học bổng",
}

REFERRALS_PATH = "api/v1/study-centers/referrals"
CITIES_PATH = "api/v1/study-centers/referral-cities"


@dataclass(frozen=True)
class CenterHit:
    name: str
    city: str | None
    website: str | None
    services: list[str]  # mã dịch vụ trung tâm có: AI dựa vào đây để giải thích vì sao trung tâm hợp
    study_levels: list[str]  # bậc học trung tâm nhận

    # Một phần tử JSON của API referrals (StudyCenterDto bên .NET, camelCase)
    @classmethod
    def from_json(cls, data: dict[str, Any]) -> "CenterHit":
        return cls(
            name=data["name"],
            city=data.get("city"),
            website=data.get("website"),
            services=list(data.get("services") or []),
            study_levels=list(data.get("studyLevels") or []),
        )


class CenterFinder:
    # http: client đã cấu hình base_url là địa chỉ backend .NET (lifespan trong main.py tạo)
    def __init__(self, http: httpx.AsyncClient) -> None:
        self._http = http

    # Tối đa 3 trung tâm cho một dịch vụ; study_level, city để None thì backend không xét
    async def find(
        self, service: str, study_level: str | None = None, city: str | None = None
    ) -> list[CenterHit]:
        params = {"service": service}
        if study_level:
            params["studyLevel"] = study_level
        if city:
            params["city"] = city
        res = await self._http.get(REFERRALS_PATH, params=params)
        res.raise_for_status()
        return [CenterHit.from_json(item) for item in res.json()]

    # Các thành phố đang có trung tâm: AI chọn thành phố của học sinh trong danh sách này, để so khớp đúng chữ
    async def cities(self) -> list[str]:
        res = await self._http.get(CITIES_PATH)
        res.raise_for_status()
        return [str(city) for city in res.json()]
