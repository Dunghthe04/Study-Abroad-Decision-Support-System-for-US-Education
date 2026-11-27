"""Chữ gửi sang Claude (dịch vụ bên ngoài) phải được che thông tin cá nhân nhận ra được theo mẫu: email, số điện thoại,
số CCCD, giá trị sau nhãn định danh, ngày sinh. Tiền, điểm, ngày thi phải giữ nguyên vì Claude cần để tư vấn.
Kiểm ngay ở nội dung gửi đi, để mọi lượt gọi (đọc hồ sơ, kho kiến thức, viết lời) đều đi qua cùng một cửa.

Giới hạn đã biết: họ tên viết tự do trong câu ("em là An") không nhận ra được theo mẫu.
"""

import json

import httpx
import pytest

from app.services.claude import ClaudeClient
from app.services.skill_call import SKILLS_DIR


async def sent_to_claude(system: str, user: str) -> tuple[str, str]:
    bodies: list[dict] = []

    def handle(request: httpx.Request) -> httpx.Response:
        bodies.append(json.loads(request.content))
        return httpx.Response(200, json={"content": [{"type": "tool_use", "name": "tra_loi", "input": {}}]})

    async with httpx.AsyncClient(transport=httpx.MockTransport(handle), base_url="http://claude") as http:
        messages = [{"role": "system", "content": system}, {"role": "user", "content": user}]
        await ClaudeClient(http, "model", "key").chat_json(messages, {"type": "object"})
    return bodies[0]["system"], bodies[0]["messages"][0]["content"]


async def user_sent(text: str) -> str:
    return (await sent_to_claude("skill", text))[1]


@pytest.mark.parametrize(
    ("text", "secret"),
    [
        ("mail em là an.nguyen+hs@gmail.com nha", "an.nguyen+hs@gmail.com"),
        ("sđt em 0912345678", "0912345678"),
        ("gọi em số 0912 345 678 nhé", "0912 345 678"),
        ("zalo 091.234.5678", "091.234.5678"),
        ("số mẹ em +84 912 345 678", "912 345 678"),
        ("máy bàn 02438 123 456", "02438 123 456"),
        ("số em (+84) 912 345 678", "912 345 678"),
        ("so em 84912345678", "84912345678"),
        ("em sinh 05/03/2008", "05/03/2008"),
        ("cccd em 001208012345", "001208012345"),
        ("CMND: 012345678", "012345678"),
        ("Họ tên: Nguyễn Văn An, em muốn học CNTT", "Nguyễn Văn An"),
        ("ho va ten: Tran Thi Binh", "Tran Thi Binh"),
        ("Địa chỉ: 12 Lê Lợi", "12 Lê Lợi"),
        ("em sinh ngày 05/03/2008", "05/03/2008"),
        ("Ngày sinh: 5-3-2008", "5-3-2008"),
        ("ngay sinh cua em la 05/03/2008", "05/03/2008"),
        ("em sinh ngày 5 tháng 3 năm 2008", "5 tháng 3 năm 2008"),
    ],
)
async def test_personal_information_is_not_sent(text, secret):
    assert secret not in await user_sent(text)


@pytest.mark.parametrize(
    "text",
    [
        "nhà em lo được 800.000.000 đồng một năm",
        "ngân sách 900000000 VND",
        "ngân sách khoảng 1000000000",
        "tầm 800 triệu đến 1 tỷ 2",
        "khoảng 35000 đến 50000 USD",
        "em thi IELTS 7.5 ngày 12/10/2026",
        "dự định thi SAT 05/12/2026, mục tiêu 1500",
        "học sinh giỏi: giải nhất tỉnh năm 2024",
        "điểm từng kỳ 8.5 9.0 8.7 9.2",
        "năm học 2023 2024 2025",
        "GPA 3.8 thang 4, SAT 1500, TOEFL 100",
        "hỏi về mẫu I-20, DS-160, visa F-1",
        "hoạt động từ 09/2025 đến nay",
        "Trường mục tiêu: Purdue University, name: CLB Tin học",
        "em sinh năm 2008, đang học lớp 12",
    ],
)
async def test_information_needed_for_advice_is_kept(text):
    assert await user_sent(text) == text


# Skill của bài tư vấn do code dựng có chứa dữ liệu hồ sơ (tên hoạt động...), nên phần skill cũng phải che
async def test_profile_text_inside_the_skill_is_masked_too():
    system, _ = await sent_to_claude("Hoạt động: gia sư, liên hệ 0912345678", "{}")

    assert "0912345678" not in system


# File skill cố định không được đổi một chữ nào khi qua lớp che, nếu không Claude nhận hướng dẫn bị hỏng
@pytest.mark.parametrize("path", sorted(SKILLS_DIR.rglob("*.md")), ids=lambda p: p.name)
async def test_the_skill_files_pass_through_unchanged(path):
    text = path.read_text(encoding="utf-8")

    system, _ = await sent_to_claude(text, "{}")

    assert system == text


# Dữ liệu gửi đi là JSON: che xong vẫn đọc được, chỉ đổi đúng phần cá nhân
async def test_json_stays_readable_and_numbers_stay():
    data = {
        "message": "Họ tên: Nguyễn An, sđt 0912345678, mail a@b.vn",
        "known": {"budget_vnd": 900000000, "sat": 1500, "gpa": {"value": 3.8, "scale": 4}},
        "activities": [
            {"name": "CLB Tin", "description": "liên hệ: cccd 001208012345", "startDate": "09/2025"}
        ],
    }

    sent = json.loads(await user_sent(json.dumps(data, ensure_ascii=False, separators=(",", ":"))))

    assert sent["known"] == data["known"]
    assert sent["activities"][0]["startDate"] == "09/2025"
    flat = json.dumps(sent, ensure_ascii=False)
    for secret in ("Nguyễn An", "0912345678", "a@b.vn", "001208012345"):
        assert secret not in flat
