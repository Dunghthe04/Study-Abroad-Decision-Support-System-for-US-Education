"""34 tỉnh, thành và tên gọi khác, khớp VietnamProvinces bên backend (test so hai bên).

Chỉ dùng để đổi địa danh SLM đã tách ra thành tên chuẩn, không đọc lại câu chat.
Chỉ khớp đúng tên hoặc tên gọi khác, không đoán gần đúng: đoán thì "Quảng Bình" thành "Quảng Ninh".
Tên có thể chỉ nhiều nơi (vd "ĐN": Đà Nẵng, Đồng Nai, Đắk Nông) không đưa vào tên gọi khác, để hệ thống hỏi lại.
"""

import re

from app.services.priority import unaccent

PROVINCES = (
    "Hà Nội", "Huế", "Lai Châu", "Điện Biên", "Sơn La", "Lạng Sơn", "Quảng Ninh", "Thanh Hóa",
    "Nghệ An", "Hà Tĩnh", "Cao Bằng", "Tuyên Quang", "Lào Cai", "Thái Nguyên", "Phú Thọ", "Bắc Ninh",
    "Hưng Yên", "Hải Phòng", "Ninh Bình", "Quảng Trị", "Đà Nẵng", "Quảng Ngãi", "Gia Lai", "Khánh Hòa",
    "Lâm Đồng", "Đắk Lắk", "TP. Hồ Chí Minh", "Đồng Nai", "Tây Ninh", "Cần Thơ", "Vĩnh Long",
    "Đồng Tháp", "Cà Mau", "An Giang",
)  # fmt: skip

# tên gọi khác (đã bỏ dấu) -> tên chuẩn đã bỏ dấu
ALIASES = {
    "hcm": "ho chi minh",
    "hcmc": "ho chi minh",
    "tphcm": "ho chi minh",
    "sg": "ho chi minh",
    "sai gon": "ho chi minh",
    "saigon": "ho chi minh",
    "hn": "ha noi",
    "hanoi": "ha noi",
    "hp": "hai phong",
    "thua thien hue": "hue",
    "danang": "da nang",
}


def city_key(text: str) -> str:
    """Bỏ dấu, ký tự lạ, tiền tố "TP", "thành phố", "tỉnh", hậu tố "city", rồi đổi tên gọi khác về tên chuẩn."""
    folded = re.sub(r"[^a-z0-9]+", " ", unaccent(text)).strip()
    folded = re.sub(r"^(?:thanh pho|tp|tinh) ", "", folded)
    folded = re.sub(r" city$", "", folded)  # "Ho Chi Minh city"
    return ALIASES.get(folded, folded)


_BY_KEY = {city_key(name): name for name in PROVINCES}


def province_of(text: str | None) -> str | None:
    """Tên tỉnh, thành ứng với địa danh học sinh nêu; None khi không rõ là đâu."""
    return _BY_KEY.get(city_key(text)) if text and text.strip() else None
