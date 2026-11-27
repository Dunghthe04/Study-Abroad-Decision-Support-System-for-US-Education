"""Địa danh học sinh nêu cho trung tâm rơi vào một trong ba trường hợp: có trung tâm; nhận ra tỉnh nhưng chưa có trung tâm;
không rõ là đâu. Hai trường hợp sau nói khác nhau và đều kèm các nơi đang có trung tâm để chọn hoặc tìm.

Lỗi thật 27/11/2026: gõ "SG" bị đáp "chưa có trung tâm đã xác minh ở SG" trong khi TP.HCM đang có trung tâm."""

import re
from pathlib import Path

import pytest

from app.schemas.chat import ChatState
from app.services.centers import CityCount, match_city
from app.services.provinces import ALIASES, PROVINCES, province_of
from tests.fakes import FakeLLM
from tests.test_skill_chuyen_trung_tam import FakeFinder, handler

DB_CITIES = ["Hà Nội", "TP.HCM", "Hải Phòng"]


@pytest.mark.parametrize("text", ["SG", "sg", "HCMC", "TP.HCM", "Sài Gòn", "tp hồ chí minh"])
def test_common_names_of_ho_chi_minh_city_find_its_centers(text):
    assert match_city(text, DB_CITIES) == "TP.HCM"


def test_hp_finds_hai_phong():
    assert match_city("HP", DB_CITIES) == "Hải Phòng"


@pytest.mark.parametrize(
    ("text", "province"),
    [("Đà Nẵng", "Đà Nẵng"), ("danang", "Đà Nẵng"), ("tỉnh Nghệ An", "Nghệ An"), ("SG", "TP. Hồ Chí Minh")],
)
def test_a_real_province_is_recognised_even_without_centers(text, province):
    assert province_of(text) == province


@pytest.mark.parametrize("text", ["ĐN", "quận 1", "thủ đô", "Quảng Bình", "Hải Dương", "xyz", "", None])
def test_an_unclear_or_unknown_place_is_not_guessed(text):
    # "ĐN" có thể là Đà Nẵng, Đồng Nai hay Đắk Nông. Tên tỉnh cũ gần giống tên khác không được đoán sang tỉnh đó
    assert province_of(text) is None


def csharp_source() -> str:
    path = "backend/src/StudyAbroad.Domain/Constants/VietnamProvinces.cs"
    return (Path(__file__).resolve().parents[2] / path).read_text(encoding="utf-8")


def test_the_province_list_matches_the_backend():
    block = re.search(r"All\s*=\s*\[(.*?)\];", csharp_source(), re.DOTALL)[1]

    assert list(PROVINCES) == re.findall(r'"([^"]+)"', block)


def test_the_other_names_match_the_backend():
    block = re.search(r"Aliases = new\(\)\s*\{(.*?)\};", csharp_source(), re.DOTALL)[1]

    assert ALIASES == dict(re.findall(r'\["([^"]+)"\]\s*=\s*"([^"]+)"', block))


COUNTS = [CityCount("Hà Nội", 5), CityCount("TP.HCM", 2)]


async def ask(city: str, counts=COUNTS):
    finder = FakeFinder(centers=(), cities=["Hà Nội", "TP.HCM"], counts=counts)
    entities = {"service": "VISA_INTERVIEW_PREP", "city": city}
    return await handler(FakeLLM(), finder).run(ChatState(), "câu hỏi", entities), finder


def picks(output) -> list[str]:
    return [option["city"] for option in output.widget.data["cities"]]


async def test_a_known_province_without_centers_says_there_are_none_there_yet():
    output, _ = await ask("danang")

    assert output.answer.startswith(
        "Tạm thời chưa có trung tâm đã xác minh nào ở Đà Nẵng"
    )  # tên chuẩn, không phải chữ gõ
    assert picks(output) == ["Hà Nội", "TP.HCM"]


async def test_an_unclear_place_is_not_reported_as_having_no_centers():
    output, finder = await ask("ĐN")

    assert "chưa rõ “ĐN” là tỉnh, thành nào" in output.answer
    assert "chưa có trung tâm" not in output.answer.lower()
    assert picks(output) == ["Hà Nội", "TP.HCM"]  # giao diện có ô tìm trên các nút này
    assert finder.find_calls == []  # chưa rõ nơi nào thì không lấy trung tâm nơi khác cho đủ


async def test_a_province_that_has_centers_but_not_for_this_service_is_named_properly():
    output, _ = await ask("SG", counts=[CityCount("Hà Nội", 5)])

    assert "ở TP.HCM" in output.answer or "ở TP. Hồ Chí Minh" in output.answer
    assert "ở SG" not in output.answer and "chưa rõ" not in output.answer
