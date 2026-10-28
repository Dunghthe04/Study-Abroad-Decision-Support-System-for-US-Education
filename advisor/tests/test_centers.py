"""Việc B6: advisor lấy trung tâm từ backend .NET (API referrals). Dùng transport giả của httpx nên không cần backend."""

import httpx
import pytest

from app.services.centers import CITIES_PATH, REFERRALS_PATH, SERVICES, CenterFinder, CenterHit
from app.services.skill_rules import ORDER

# Đúng dạng JSON backend trả (StudyCenterDto, camelCase)
CENTER_HCM = {
    "id": "777daf6f-d1d1-4559-b5d8-0f1bce075cd2",
    "code": "DEMO_002",
    "name": "Demo Center TP.HCM",
    "website": "https://demo.vn",
    "address": None,
    "city": "TP.HCM",
    "studyLevels": ["undergraduate", "master"],
    "services": ["CHON_TRUONG", "SUA_BAI_LUAN"],
    "surveyedAt": None,
}


def finder_with(handler) -> tuple[CenterFinder, list[httpx.Request]]:
    """CenterFinder dùng backend giả: handler nhận request, trả response; ghi lại các request đã gửi."""
    sent: list[httpx.Request] = []

    def record(request: httpx.Request) -> httpx.Response:
        sent.append(request)
        return handler(request)

    http = httpx.AsyncClient(base_url="http://backend.test", transport=httpx.MockTransport(record))
    return CenterFinder(http), sent


def test_service_codes_are_upper_snake_case_with_a_vietnamese_label():
    for code, label in SERVICES.items():
        assert code == code.upper() and " " not in code
        assert label.strip()  # tên hiển thị không được rỗng


def test_service_catalog_covers_the_codes_used_by_the_demo_centers():
    # mã trong DbSeeder (CHON_TRUONG, LUYEN_THI, SUA_BAI_LUAN, SAN_HOC_BONG, LUYEN_PHONG_VAN) phải có tên hiển thị
    assert {"CHON_TRUONG", "LUYEN_THI", "SUA_BAI_LUAN", "SAN_HOC_BONG", "LUYEN_PHONG_VAN"} <= set(SERVICES)


def test_b6_skill_is_a_known_skill_label():
    assert "skill_chuyen_trung_tam" in ORDER


async def test_find_calls_the_referrals_api_with_the_filters():
    finder, sent = finder_with(lambda request: httpx.Response(200, json=[CENTER_HCM]))

    hits = await finder.find("SUA_BAI_LUAN", "undergraduate", "TP.HCM")

    assert sent[0].url.path == "/" + REFERRALS_PATH
    assert dict(sent[0].url.params) == {
        "service": "SUA_BAI_LUAN",
        "studyLevel": "undergraduate",
        "city": "TP.HCM",
    }
    assert hits == [
        CenterHit(
            name="Demo Center TP.HCM",
            city="TP.HCM",
            website="https://demo.vn",
            services=["CHON_TRUONG", "SUA_BAI_LUAN"],
            study_levels=["undergraduate", "master"],
        )
    ]


async def test_find_leaves_out_the_filters_that_are_not_known():
    finder, sent = finder_with(lambda request: httpx.Response(200, json=[]))

    assert await finder.find("CHON_TRUONG") == []
    assert dict(sent[0].url.params) == {"service": "CHON_TRUONG"}  # không gửi studyLevel, city rỗng


async def test_find_tolerates_missing_optional_fields():
    minimal = {"name": "Trung tâm A", "city": None, "website": None, "services": None, "studyLevels": None}
    finder, _ = finder_with(lambda request: httpx.Response(200, json=[minimal]))

    [hit] = await finder.find("CHON_TRUONG")

    assert hit == CenterHit(name="Trung tâm A", city=None, website=None, services=[], study_levels=[])


async def test_find_raises_when_the_backend_fails():
    # handler bắt lỗi này và báo "đang gián đoạn", không bịa danh sách
    finder, _ = finder_with(lambda request: httpx.Response(500))

    with pytest.raises(httpx.HTTPStatusError):
        await finder.find("CHON_TRUONG")


async def test_cities_calls_the_referral_cities_api():
    finder, sent = finder_with(lambda request: httpx.Response(200, json=["Hà Nội", "TP.HCM"]))

    assert await finder.cities() == ["Hà Nội", "TP.HCM"]
    assert sent[0].url.path == "/" + CITIES_PATH
