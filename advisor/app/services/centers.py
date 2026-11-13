"""Lấy trung tâm tư vấn cho B6 (skill_chuyen_trung_tam) từ backend .NET.

Luật chọn trung tâm nằm ở backend (CenterReferral): đã xác minh, đang hoạt động, đúng dịch vụ và bậc học,
cùng thành phố xếp trước, tối đa 3. Advisor chỉ gọi API, không đọc bảng study_centers,
giống phần gợi ý trường: lọc theo luật ở backend, advisor chỉ làm phần AI.
"""

import difflib
import re
from dataclasses import dataclass
from typing import Any

import httpx

from app.services.priority import unaccent

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

# TẠM: mã dịch vụ model trả (SERVICE_CODES trong schemas/slm.py, tiếng Anh) -> mã lưu trong DB (SERVICES, tiếng Việt).
# Chỉ đổi tên ở đây, nơi duy nhất gọi backend. Bỏ bảng này khi DB và backend đổi mã sang tiếng Anh (có test kiểm đủ 6 mã)
MODEL_SERVICE_TO_DB = {
    "SCHOOL_SELECTION": "CHON_TRUONG",
    "ESSAY_EDITING": "SUA_BAI_LUAN",
    "APPLICATION_SUBMISSION": "NOP_HO_SO",
    "VISA_INTERVIEW_PREP": "LUYEN_PHONG_VAN",
    "TEST_PREP": "LUYEN_THI",
    "SCHOLARSHIP_SEARCH": "SAN_HOC_BONG",
}


def db_service(model_code: str | None) -> str | None:
    """Mã dịch vụ trong DB ứng với mã model trả; None khi model không nêu dịch vụ hoặc trả mã lạ."""
    return MODEL_SERVICE_TO_DB.get(model_code or "")


# Thành phố học sinh viết ("Sài Gòn", "HCM", "TP.HCM", "HN", "Hanoi") khác tên chuẩn trong DB ("TP.HCM", "Hà Nội").
# So sau khi bỏ dấu, bỏ ký tự lạ và bỏ "TP", "thành phố" ở đầu; các cách viết tắt hay gặp quy về một tên
_CITY_ALIASES = {
    "hcm": "ho chi minh",
    "hcmc": "ho chi minh",
    "tphcm": "ho chi minh",
    "sai gon": "ho chi minh",
    "saigon": "ho chi minh",
    "hn": "ha noi",
    "hanoi": "ha noi",
}


def _city_key(text: str) -> str:
    folded = re.sub(r"[^a-z0-9]+", " ", unaccent(text)).strip()
    folded = re.sub(r"^(?:thanh pho|tp) ", "", folded)
    folded = re.sub(r" city$", "", folded)  # "Ho Chi Minh city"
    return _CITY_ALIASES.get(folded, folded)


# Giống nhau từ mức này trở lên thì coi là gõ sai chính tả của một thành phố trong DB. Mức 0,82 nằm giữa "ha noy" / "ha noi" (0,83)
# và hai tỉnh khác nhau như "hai duong" / "hai phong" (0,78): nhầm sang thành phố khác tệ hơn là không khớp
CITY_SIMILARITY = 0.82


def match_city(text: str | None, cities: list[str]) -> str | None:
    """Tên thành phố trong DB (đúng chữ của `cities`) ứng với chữ học sinh viết; None khi không nói hoặc không khớp thành phố nào."""
    if not text or not text.strip():
        return None
    key = _city_key(text)
    by_key = {_city_key(city): city for city in cities}
    if key in by_key:
        return by_key[key]
    close = difflib.get_close_matches(
        key, list(by_key), n=1, cutoff=CITY_SIMILARITY
    )  # gõ sai nhẹ: "ho chinh minh"
    return by_key[close[0]] if close else None


REFERRALS_PATH = "api/v1/study-centers/referrals"
CITIES_PATH = "api/v1/study-centers/referral-cities"
CITY_COUNTS_PATH = "api/v1/study-centers/referral-city-counts"


@dataclass(frozen=True)
class CenterHit:
    name: str
    city: str | None
    website: str | None
    services: list[str]  # mã dịch vụ trung tâm có: AI dựa vào đây để giải thích vì sao trung tâm hợp
    study_levels: list[str]  # bậc học trung tâm nhận
    id: str | None = (
        None  # mã trung tâm (Guid) để giao diện mở /centers/{id}; None thì không có nút chuyển trang
    )

    # Một phần tử JSON của API referrals (StudyCenterDto bên .NET, camelCase)
    @classmethod
    def from_json(cls, data: dict[str, Any]) -> "CenterHit":
        return cls(
            name=data["name"],
            city=data.get("city"),
            website=data.get("website"),
            services=list(data.get("services") or []),
            study_levels=list(data.get("studyLevels") or []),
            id=str(data["id"]) if data.get("id") else None,
        )


# Một thành phố và số trung tâm đã xác minh ở đó có đúng dịch vụ (và bậc học) học sinh cần
@dataclass(frozen=True)
class CityCount:
    city: str
    count: int


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

    # Thành phố có trung tâm cho một dịch vụ, kèm số trung tâm: để học sinh chọn nơi đang có, không bao giờ dẫn tới danh sách trống
    async def city_counts(self, service: str, study_level: str | None = None) -> list[CityCount]:
        params = {"service": service}
        if study_level:
            params["studyLevel"] = study_level
        res = await self._http.get(CITY_COUNTS_PATH, params=params)
        res.raise_for_status()
        return [CityCount(city=str(item["city"]), count=int(item["count"])) for item in res.json()]

    # Các thành phố đang có trung tâm: AI chọn thành phố của học sinh trong danh sách này, để so khớp đúng chữ
    async def cities(self) -> list[str]:
        res = await self._http.get(CITIES_PATH)
        res.raise_for_status()
        return [str(city) for city in res.json()]
