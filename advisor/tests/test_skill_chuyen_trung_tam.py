"""Việc B6: skill_chuyen_trung_tam gợi ý trung tâm kèm giải thích; AI không chọn trung tâm, không xin liên hệ, không nêu giá."""

import json

import pytest

from app.schemas.chat import ChatState
from app.services.centers import SERVICES, CenterHit
from app.services.ollama import OllamaError
from app.services.skills.skill_chuyen_trung_tam import (
    ASK_SERVICE,
    DISCLAIMER,
    NO_CENTER,
    NOT_PROVIDED,
    UNAVAILABLE,
    ChuyenTrungTamHandler,
    check_grounded,
    check_need,
    check_reasons,
    check_text,
    template_reason,
)
from tests.fakes import FakeLLM

HELP = "Phỏng vấn visa thường hỏi về lý do chọn trường và kế hoạch học. Bạn nên tự luyện trả lời ngắn gọn, trung thực."
HCM = CenterHit(
    "Trung tâm A", "TP.HCM", "https://a.vn", ["LUYEN_PHONG_VAN", "CHON_TRUONG"], ["undergraduate"]
)
HN = CenterHit("Trung tâm B", "Hà Nội", None, ["LUYEN_PHONG_VAN"], ["undergraduate", "master"])
CITIES = ["Hà Nội", "TP.HCM"]


def need(service="LUYEN_PHONG_VAN", city="TP.HCM", general_help=HELP):
    return {"service": service, "city": city, "general_help": general_help}


def reasons(*texts):
    return {"reasons": [{"id": i, "reason": t} for i, t in enumerate(texts, start=1)]}


class FakeFinder:
    def __init__(self, centers=(), cities=CITIES, error: Exception | None = None, find_error=None) -> None:
        self._centers, self._cities = list(centers), cities
        self._error, self._find_error = error, find_error
        self.find_calls: list[tuple] = []

    async def cities(self):
        if self._error:
            raise self._error
        return self._cities

    async def find(self, service, study_level=None, city=None):
        self.find_calls.append((service, study_level, city))
        if self._find_error:
            raise self._find_error
        return self._centers


def handler(llm, finder):
    return ChuyenTrungTamHandler(llm, "qwen3:8b", finder)


# ---------- kiểm tra lời của AI ----------


@pytest.mark.parametrize(
    "text",
    [
        "Bạn để lại số điện thoại nhé.",
        "Liên hệ qua Zalo để được tư vấn.",
        "Đây là trung tâm uy tín nhất.",
        "Đăng ký ngay để nhận ưu đãi.",
        "Mức giá khoảng vài triệu.",
        "Khóa học kéo dài 3 tháng.",  # con số
    ],
)
def test_check_text_rejects_contact_requests_prices_praise_and_numbers(text):
    with pytest.raises(ValueError):
        check_text(text)


def test_check_text_rejects_a_center_name_written_by_the_ai():
    with pytest.raises(ValueError):
        check_text("Có luyện phỏng vấn, giống trung tâm a.", ["Trung tâm A"])


def test_check_need_accepts_a_known_service_and_city():
    assert check_need(need(), CITIES).service == "LUYEN_PHONG_VAN"
    assert check_need(need(), CITIES).city == "TP.HCM"


def test_check_need_rejects_an_unknown_service_code():
    with pytest.raises(ValueError):
        check_need(need(service="LAM_VISA_HO"), CITIES)


def test_check_need_treats_an_unknown_city_as_not_said():
    assert check_need(need(city="Đà Nẵng"), CITIES).city is None


def test_check_need_drops_bad_advice_but_keeps_the_service():
    result = check_need(need(general_help="Bạn liên hệ hotline để được hỗ trợ."), CITIES)

    assert result.service == "LUYEN_PHONG_VAN" and result.general_help == ""


def test_check_need_without_service_has_no_advice():
    assert check_need(need(service=None), CITIES).general_help == ""


def test_check_reasons_needs_exactly_one_reason_per_center():
    assert check_reasons(
        reasons("Có luyện phỏng vấn visa.", "Có luyện phỏng vấn visa."), [HCM, HN], None
    ) == {
        1: "Có luyện phỏng vấn visa.",
        2: "Có luyện phỏng vấn visa.",
    }
    with pytest.raises(ValueError):
        check_reasons(reasons("Có luyện phỏng vấn visa."), [HCM, HN], None)


@pytest.mark.parametrize(
    "text",
    [
        "",
        "Có luyện phỏng vấn visa, Trung tâm A rất tốt.",
        "Có luyện phỏng vấn visa, " + "rất " * 40,
        "Có 2 cơ sở.",
    ],
)
def test_check_reasons_rejects_empty_long_named_or_numbered_reasons(text):
    with pytest.raises(ValueError):
        check_reasons(reasons(text), [HCM], None)


@pytest.mark.parametrize(
    "reason, student_city",
    [
        ("Có luyện phỏng vấn visa, cùng thành phố với bạn.", "TP.HCM"),  # Hà Nội không cùng TP.HCM
        ("Có luyện phỏng vấn visa, cùng thành phố với bạn.", None),  # học sinh không nói thành phố
        ("Có luyện phỏng vấn visa và sửa bài luận.", None),  # trung tâm không có sửa bài luận
        ("Có luyện phỏng vấn visa, nhận học sinh bậc tiến sĩ.", None),  # trung tâm không nhận tiến sĩ
    ],
)
def test_check_grounded_rejects_claims_the_center_data_does_not_support(reason, student_city):
    with pytest.raises(ValueError):
        check_grounded(reason, HN, student_city)


def test_check_grounded_accepts_true_claims():
    check_grounded(
        "Có luyện phỏng vấn visa, nhận học sinh bậc thạc sĩ, cùng thành phố với bạn.", HN, "hà nội"
    )


def test_template_reason_uses_only_the_center_data():
    assert template_reason(HCM, "LUYEN_PHONG_VAN", "undergraduate", "tp.hcm") == (
        "Có luyện phỏng vấn visa, nhận học sinh bậc đại học, cùng thành phố với bạn."
    )
    assert template_reason(HN, "LUYEN_PHONG_VAN", "phd", "TP.HCM") == "Có luyện phỏng vấn visa."


# ---------- handler ----------


async def test_suggests_centers_with_advice_reasons_and_disclaimer():
    llm = FakeLLM(
        need(),
        reasons(
            "Có luyện phỏng vấn visa, nhận học sinh bậc đại học, cùng thành phố với bạn.",
            "Có luyện phỏng vấn visa, nhận học sinh bậc đại học.",
        ),
    )
    finder = FakeFinder([HCM, HN])

    out = await handler(llm, finder).run(
        ChatState(study_level="undergraduate"), "em ở hcm cần luyện phỏng vấn visa"
    )

    assert out.answer == (
        f"{HELP}\n\n"
        "Dưới đây là một số trung tâm đã xác minh có hỗ trợ luyện phỏng vấn visa:\n"
        "- Trung tâm A (TP.HCM): Có luyện phỏng vấn visa, nhận học sinh bậc đại học, cùng thành phố với bạn."
        " Website: https://a.vn\n"
        "- Trung tâm B (Hà Nội): Có luyện phỏng vấn visa, nhận học sinh bậc đại học."
    )
    assert out.disclaimer == DISCLAIMER
    assert finder.find_calls == [("LUYEN_PHONG_VAN", "undergraduate", "TP.HCM")]


async def test_the_ai_never_sees_the_center_names():
    llm = FakeLLM(need(), reasons("Có luyện phỏng vấn visa.", "Có luyện phỏng vấn visa."))

    await handler(llm, FakeFinder([HCM, HN])).run(ChatState(), "cần luyện phỏng vấn")

    explain_input = json.loads(llm.calls[1]["messages"][1]["content"])
    assert "Trung tâm A" not in json.dumps(explain_input, ensure_ascii=False)
    # code tự so thành phố, bậc học và đưa kết quả đúng/sai cho AI
    assert explain_input["centers"][0] == {
        "id": 1,
        "same_city": True,
        "accepts_level": False,  # ChatState() chưa có bậc học
        "other_services": ["chọn trường"],
    }
    assert explain_input["centers"][1]["same_city"] is False


async def test_the_first_call_gets_the_service_catalog_and_the_cities():
    llm = FakeLLM(need(), reasons("Có luyện phỏng vấn visa."))

    await handler(llm, FakeFinder([HCM])).run(ChatState(), "cần luyện phỏng vấn")

    need_input = json.loads(llm.calls[0]["messages"][1]["content"])
    assert need_input["cities"] == CITIES
    assert {s["code"] for s in need_input["services"]} == set(SERVICES)


async def test_asks_which_service_with_buttons_when_unclear():
    llm = FakeLLM(need(service=None, city=None, general_help=""))
    finder = FakeFinder([HCM])

    out = await handler(llm, finder).run(ChatState(), "em cần người giúp em")

    assert out.answer == ASK_SERVICE
    assert [b.label for b in out.quick_replies][:2] == ["Chọn trường", "Sửa bài luận"]
    assert "Luyện thi IELTS, SAT" in [b.label for b in out.quick_replies]  # giữ chữ hoa của tên riêng
    assert out.quick_replies[1].value == "Mình cần hỗ trợ sửa bài luận"  # bấm là gửi lại một câu rõ dịch vụ
    assert finder.find_calls == []


async def test_asks_which_service_when_the_ai_fails_twice():
    llm = FakeLLM(OllamaError("tắt"), OllamaError("tắt"))

    out = await handler(llm, FakeFinder([HCM])).run(ChatState(), "cần luyện phỏng vấn")

    assert out.answer == ASK_SERVICE


async def test_says_there_is_no_verified_center_when_none_matches():
    # TH-63
    llm = FakeLLM(need(service="NOP_HO_SO"))

    out = await handler(llm, FakeFinder([])).run(ChatState(), "ai nộp hồ sơ hộ em được không")

    assert out.answer == f"{HELP}\n\n" + NO_CENTER.format(label="nộp hồ sơ")
    assert out.disclaimer is None
    assert len(llm.calls) == 1  # không có trung tâm thì không gọi AI lượt 2


async def test_uses_template_reasons_when_the_ai_writes_bad_ones_twice():
    # TH-64: AI nêu tên, xin liên hệ hai lần -> dùng câu dựng từ dữ liệu, không để lời cấm tới học sinh
    llm = FakeLLM(need(), reasons("Trung tâm A uy tín nhất."), reasons("Gọi hotline để nhận ưu đãi."))

    out = await handler(llm, FakeFinder([HCM])).run(
        ChatState(study_level="undergraduate"), "cần luyện phỏng vấn"
    )

    assert (
        "- Trung tâm A (TP.HCM): Có luyện phỏng vấn visa, nhận học sinh bậc đại học, cùng thành phố với bạn."
        in (out.answer)
    )
    assert "uy tín" not in out.answer and "hotline" not in out.answer


async def test_says_unavailable_when_the_backend_is_down():
    llm = FakeLLM()

    out = await handler(llm, FakeFinder(error=ConnectionError("backend tắt"))).run(ChatState(), "q")

    assert out.answer == UNAVAILABLE
    assert llm.calls == []


async def test_says_unavailable_when_finding_centers_fails():
    out = await handler(FakeLLM(need()), FakeFinder(find_error=ConnectionError("lỗi"))).run(ChatState(), "q")

    assert out.answer == UNAVAILABLE


# ---------- học sinh xin điều hệ thống không cung cấp ----------


@pytest.mark.parametrize(
    "message",
    [
        "cho em số điện thoại của trung tâm luyện phỏng vấn",
        "trung tâm luyện phỏng vấn nào uy tín nhất",
        "giá luyện phỏng vấn bao nhiêu",
        "cho em xin hotline trung tâm",
        "trung tâm nào xếp hạng cao nhất",
    ],
)
async def test_asking_for_phone_price_or_ranking_says_it_is_not_provided_then_still_suggests_centers(message):
    llm = FakeLLM(need(general_help=""), reasons("Có luyện phỏng vấn visa."))

    out = await handler(llm, FakeFinder([HN])).run(ChatState(), message)

    assert out.answer.startswith(NOT_PROVIDED + "\n\n")
    assert "Trung tâm B" in out.answer  # vẫn có danh sách đã xác minh
    assert out.disclaimer == DISCLAIMER


async def test_a_plain_service_request_has_no_not_provided_notice():
    llm = FakeLLM(need(), reasons("Có luyện phỏng vấn visa."))

    out = await handler(llm, FakeFinder([HN])).run(ChatState(), "em cần luyện phỏng vấn visa")

    assert NOT_PROVIDED not in out.answer


async def test_the_not_provided_notice_also_comes_before_the_service_question():
    out = await handler(FakeLLM(need(service=None, general_help="")), FakeFinder([])).run(
        ChatState(), "cho em số điện thoại của trung tâm"
    )

    assert out.answer == f"{NOT_PROVIDED}\n\n{ASK_SERVICE}"
    assert len(out.quick_replies) == len(SERVICES)
