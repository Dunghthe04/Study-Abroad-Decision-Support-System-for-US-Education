"""Tìm trung tâm tư vấn cho B6 (skill_chuyen_trung_tam) trong bảng app.study_centers.

Code chọn trung tâm, AI không chọn: chỉ lấy trung tâm đã xác minh, đang hoạt động, có đúng dịch vụ học sinh cần
và nhận bậc học của học sinh; tối đa 3. Trung tâm cùng thành phố với học sinh xếp trước, còn lại sắp theo tên
để không trung tâm nào được đẩy lên đầu (không xếp hạng chất lượng như gợi ý trường).
"""

import asyncio
from dataclasses import dataclass
from typing import Any

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

MAX_CENTERS = 3

SEARCH_SQL = """
SELECT name, city, website, services, study_levels
FROM app.study_centers
WHERE is_active
  AND verification_status = ANY(%(statuses)s)
  AND %(service)s = ANY(services)
  AND (%(level)s::text IS NULL OR %(level)s = ANY(study_levels))
ORDER BY (city IS NOT DISTINCT FROM %(city)s) DESC, name
LIMIT %(limit)s
"""
NAMES_SQL = "SELECT name FROM app.study_centers"
CITIES_SQL = "SELECT DISTINCT city FROM app.study_centers WHERE is_active AND city IS NOT NULL ORDER BY city"


@dataclass(frozen=True)
class CenterHit:
    name: str
    city: str | None
    website: str | None
    services: list[str]  # mã dịch vụ trung tâm có: AI dựa vào đây để giải thích vì sao trung tâm hợp
    study_levels: list[str]  # bậc học trung tâm nhận


class CenterFinder:
    def __init__(self, pool: Any, statuses: list[str]) -> None:
        self._pool = pool
        self._statuses = statuses

    # Bản đồng bộ: chạy trong luồng riêng (xem find) vì hồ chứa kết nối là bản sync
    # city: thành phố học sinh nói (đúng chữ trong danh sách cities); None = không ưu tiên thành phố nào
    def find_sync(
        self, service: str, study_level: str | None = None, city: str | None = None
    ) -> list[CenterHit]:
        params = {
            "statuses": self._statuses,
            "service": service,
            "level": study_level,
            "city": city,
            "limit": MAX_CENTERS,
        }
        with self._pool.connection() as conn:
            rows = conn.execute(SEARCH_SQL, params).fetchall()
        return [CenterHit(*row) for row in rows]

    # Tên mọi trung tâm trong bảng: để chặn AI tự nêu tên trung tâm trong lời khuyên (TH-64)
    def names_sync(self) -> list[str]:
        with self._pool.connection() as conn:
            return [row[0] for row in conn.execute(NAMES_SQL).fetchall()]

    # Các thành phố đang có trung tâm: AI chọn thành phố của học sinh trong danh sách này, để so khớp đúng chữ
    def cities_sync(self) -> list[str]:
        with self._pool.connection() as conn:
            return [row[0] for row in conn.execute(CITIES_SQL).fetchall()]

    async def find(
        self, service: str, study_level: str | None = None, city: str | None = None
    ) -> list[CenterHit]:
        return await asyncio.to_thread(self.find_sync, service, study_level, city)

    async def names(self) -> list[str]:
        return await asyncio.to_thread(self.names_sync)

    async def cities(self) -> list[str]:
        return await asyncio.to_thread(self.cities_sync)
