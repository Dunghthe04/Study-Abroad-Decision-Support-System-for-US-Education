"""UC-153 1a: lời giải thích cách tính điểm phù hợp do Claude viết từ tham số công bố, kiểm chặt, sai thì dùng câu cố định."""

import httpx
import pytest

from app.services.fit_method_writer import FitMethodWriter, check_answer, fit_parameters

PAYLOAD = {
    "algorithmVersion": "x",
    "parameters": [
        {
            "key": "fit_weights",
            "step": "B10",
            "name": "Trọng số điểm phù hợp",
            "value": "Vừa tầm 0.3 · Ngân sách 0.25 · Chất lượng 0.2 · Học bổng 0.15 · Tiếng Anh 0.1",
            "meaning": "Sắp thứ tự trong nhóm.",
        },
        {"key": "lower_cut", "step": "B9", "name": "Ngưỡng", "value": "0.25", "meaning": "Khó vào."},
    ],
}
GOOD = (
    "Điểm phù hợp gồm vừa tầm 0.3, ngân sách 0.25, chất lượng 0.2, học bổng 0.15 và tiếng Anh 0.1, mỗi phần chấm trên thang 100. "
    "Điểm chỉ sắp thứ tự trong cùng nhóm."
)


class FakeLlm:
    def __init__(self, answers):
        self.answers = list(answers)

    async def complete_json(self, *args, **kwargs):  # chữ ký thật do SkillCall quyết định, xem test dưới
        return {"answer": self.answers.pop(0)}


def user():
    return {"parameters": fit_parameters(PAYLOAD)}


def test_only_the_fit_step_parameters_are_sent():
    assert [p["name"] for p in user()["parameters"]] == ["Trọng số điểm phù hợp"]


def test_a_good_answer_passes():
    assert check_answer({"answer": GOOD}, user()) == GOOD


@pytest.mark.parametrize(
    "bad",
    [
        GOOD.replace("tiếng Anh 0.1", "0.1"),  # thiếu thành phần
        GOOD.replace("0.3", "0.4"),  # số không có trong tham số
        GOOD + " Chắc chắn đậu nhé bạn.",  # hứa kết quả
    ],
)
def test_a_bad_answer_is_rejected(bad):
    with pytest.raises(ValueError):
        check_answer({"answer": bad}, user())


@pytest.mark.asyncio
async def test_backend_down_gives_none_so_the_fixed_text_is_used():
    transport = httpx.MockTransport(lambda request: httpx.Response(500))
    async with httpx.AsyncClient(transport=transport, base_url="http://x") as http:
        writer = FitMethodWriter(FakeLlm([]), "m", http)
        assert await writer.write() is None
