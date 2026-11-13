"""Việc B6: skill_chuyen_trung_tam gợi ý trung tâm kèm giải thích; AI không chọn trung tâm, không xin liên hệ, không nêu giá."""

import json

import pytest

from app.schemas.chat import ChatContext, ChatState
from app.services.centers import SERVICES, CenterHit, CityCount
from app.services.skills.skill_chuyen_trung_tam import (
    ASK_SERVICE,
    DISCLAIMER,
    NO_CENTER,
    NO_CENTER_IN_CITY,
    NOT_PROVIDED,
    TASK_NOTES,
    UNAVAILABLE,
    ChuyenTrungTamHandler,
    Need,
    centers_widget,
    check_grounded,
    check_opening,
    check_reasons,
    check_text,
    fallback_opening,
    read_need,
    template_reason,
)
from tests.fakes import FakeLLM

HCM = CenterHit(
    "Trung tâm A", "TP.HCM", "https://a.vn", ["LUYEN_PHONG_VAN", "CHON_TRUONG"], ["undergraduate"]
)
HN = CenterHit("Trung tâm B", "Hà Nội", None, ["LUYEN_PHONG_VAN"], ["undergraduate", "master"])
CITIES = ["Hà Nội", "TP.HCM"]


def need(service="VISA_INTERVIEW_PREP", city="HCM", **more):
    """Entities của ý center_referral do SLM tách (mã dịch vụ tiếng Anh, thành phố đúng chữ học sinh viết)."""
    entities = {"service": service, "city": city, **more}
    return {key: value for key, value in entities.items() if value is not None}


OPENING = "Bạn cần luyện phỏng vấn visa ở HCM. Dưới đây là các trung tâm đã xác minh:"


def reasons(*texts, opening=OPENING):
    """Câu trả lời của Claude: đoạn mở đầu và một câu cho mỗi trung tâm."""
    return {"opening": opening, "reasons": [{"id": i, "reason": t} for i, t in enumerate(texts, start=1)]}


class FakeFinder:
    def __init__(
        self,
        centers=(),
        cities=CITIES,
        error: Exception | None = None,
        find_error=None,
        counts=(),
        counts_error=None,
    ) -> None:
        self._centers, self._cities = list(centers), cities
        self._error, self._find_error = error, find_error
        self._counts, self._counts_error = list(counts), counts_error
        self.find_calls: list[tuple] = []
        self.count_calls: list[tuple] = []

    async def cities(self):
        if self._error:
            raise self._error
        return self._cities

    async def city_counts(self, service, study_level=None):
        self.count_calls.append((service, study_level))
        if self._counts_error:
            raise self._counts_error
        return self._counts

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


def test_read_need_turns_the_model_service_into_the_db_code_and_the_written_city_into_the_db_city():
    result = read_need(need(), ChatContext(), CITIES)

    assert result == Need("LUYEN_PHONG_VAN", "VISA_INTERVIEW_PREP", "TP.HCM", "HCM")


def test_read_need_treats_an_unknown_service_as_not_said():
    assert read_need(need(service="LAM_VISA_HO"), ChatContext(), CITIES) == Need(None, None, "TP.HCM", "HCM")
    assert read_need(need(service=None), ChatContext(), CITIES).service is None


def test_read_need_treats_a_city_without_centers_as_not_said():
    assert read_need(need(city="Đà Nẵng"), ChatContext(), CITIES).city is None


@pytest.mark.parametrize("written", ["Sài Gòn", "HCM", "TP.HCM", "Sai Gon", "tp hcm"])
def test_read_need_matches_the_ways_students_write_the_same_city(written):
    assert read_need(need(city=written), ChatContext(), CITIES).city == "TP.HCM"


def test_a_follow_up_takes_the_missing_service_and_city_from_the_context():
    context = ChatContext(service="ESSAY_EDITING", city="Hà Nội")

    result = read_need(need(service=None, city=None, follow_up=True), context, CITIES)

    assert result == Need("SUA_BAI_LUAN", "ESSAY_EDITING", "Hà Nội", "Hà Nội")


def test_a_follow_up_keeps_what_the_student_said_over_the_context():
    context = ChatContext(service="ESSAY_EDITING", city="Hà Nội")

    result = read_need(need(service="TEST_PREP", city="HCM", follow_up=True), context, CITIES)

    assert result == Need("LUYEN_THI", "TEST_PREP", "TP.HCM", "HCM")


def test_a_message_that_is_not_a_follow_up_ignores_the_context():
    context = ChatContext(service="ESSAY_EDITING", city="Hà Nội")

    assert read_need(need(service=None, city=None), context, CITIES) == Need(None, None, None)


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


async def test_suggests_centers_with_reasons_and_disclaimer():
    llm = FakeLLM(
        reasons(
            "Có luyện phỏng vấn visa, nhận học sinh bậc đại học, cùng thành phố với bạn.",
            "Có luyện phỏng vấn visa, nhận học sinh bậc đại học.",
        ),
    )
    finder = FakeFinder([HCM, HN])

    out = await handler(llm, finder).run(
        ChatState(study_level="undergraduate"), "em ở hcm cần luyện phỏng vấn visa", need()
    )

    assert out.answer == (
        f"{OPENING}\n"
        "- Trung tâm A (TP.HCM): Có luyện phỏng vấn visa, nhận học sinh bậc đại học, cùng thành phố với bạn."
        " Website: https://a.vn\n"
        "- Trung tâm B (Hà Nội): Có luyện phỏng vấn visa, nhận học sinh bậc đại học."
    )
    assert out.disclaimer == DISCLAIMER
    assert finder.find_calls == [("LUYEN_PHONG_VAN", "undergraduate", "TP.HCM")]
    assert len(llm.calls) == 1  # chỉ một lượt AI (mở đầu và lý do), không còn lượt đọc nhu cầu


async def test_the_ai_never_sees_the_center_names():
    llm = FakeLLM(reasons("Có luyện phỏng vấn visa.", "Có luyện phỏng vấn visa."))

    await handler(llm, FakeFinder([HCM, HN])).run(ChatState(), "cần luyện phỏng vấn", need())

    explain_input = json.loads(llm.calls[0]["messages"][1]["content"])
    assert "Trung tâm A" not in json.dumps(explain_input, ensure_ascii=False)
    # code tự so thành phố, bậc học và đưa kết quả đúng/sai cho AI
    assert explain_input["centers"][0] == {
        "id": 1,
        "same_city": True,
        "accepts_level": False,  # ChatState() chưa có bậc học
        "other_services": ["chọn trường"],
    }
    assert explain_input["centers"][1]["same_city"] is False


async def test_the_service_and_city_are_remembered_for_the_next_follow_up():
    state = ChatState()

    await handler(FakeLLM(reasons("Có luyện phỏng vấn visa.")), FakeFinder([HCM])).run(state, "q", need())

    assert (state.context.service, state.context.city) == ("VISA_INTERVIEW_PREP", "TP.HCM")


async def test_a_follow_up_without_service_and_city_searches_with_the_remembered_ones():
    state = ChatState(context=ChatContext(service="ESSAY_EDITING", city="Hà Nội"))
    essay_center = CenterHit("Trung tâm C", "Hà Nội", None, ["SUA_BAI_LUAN"], ["undergraduate"])
    finder = FakeFinder([essay_center])

    await handler(FakeLLM(reasons("Có sửa bài luận, cùng thành phố với bạn.")), finder).run(
        state, "thế còn sửa bài luận", {"follow_up": True}
    )

    assert finder.find_calls == [("SUA_BAI_LUAN", None, "Hà Nội")]


async def test_the_city_is_remembered_even_when_the_service_is_still_unclear():
    state = ChatState()

    await handler(FakeLLM(), FakeFinder([HCM])).run(
        state, "tìm trung tâm ở Hà Nội", need(service=None, city="Hà Nội")
    )

    assert state.context.city == "Hà Nội"
    assert state.context.service is None  # chưa rõ dịch vụ nên chưa nhớ dịch vụ


async def test_a_handoff_request_with_a_remembered_service_and_city_lists_centers_without_asking():
    state = ChatState(context=ChatContext(service="ESSAY_EDITING", city="Hà Nội"))
    essay_center = CenterHit("Trung tâm C", "Hà Nội", None, ["SUA_BAI_LUAN"], ["undergraduate"])
    finder = FakeFinder([essay_center])

    out = await handler(FakeLLM(reasons("Có sửa bài luận, cùng thành phố với bạn.")), finder).run(
        state, "cho em gặp tư vấn viên", {"follow_up": True}
    )

    assert finder.find_calls == [("SUA_BAI_LUAN", None, "Hà Nội")]
    assert ASK_SERVICE not in out.answer


async def test_a_handoff_request_with_nothing_remembered_still_asks_for_the_service():
    out = await handler(FakeLLM(), FakeFinder([HCM])).run(
        ChatState(), "cho em gặp tư vấn viên", {"follow_up": True}
    )

    assert out.answer == ASK_SERVICE
    assert out.quick_replies


def test_center_hit_reads_the_center_id_when_the_backend_sends_it():
    hit = CenterHit.from_json({"id": "4b1f", "name": "Trung tâm A", "services": [], "studyLevels": []})

    assert hit.id == "4b1f"
    assert CenterHit.from_json({"name": "Trung tâm B"}).id is None


def test_the_widget_carries_one_card_per_center_and_the_city_options():
    summit = CenterHit("Summit Education Services", "Hà Nội", "https://s.vn", [], [], id="c1")
    no_id = CenterHit("Trung tâm không mã", "TP.HCM", None, [], [])

    widget = centers_widget(
        [summit, no_id],
        {1: "Có luyện thi SAT.", 2: "Có chọn trường."},
        [CityCount("Hải Phòng", 2)],
        "LUYEN_THI",
    )

    assert widget.type == "centers"
    assert widget.data["firstPage"] == 3
    assert widget.data["centers"] == [
        {
            "id": "c1",
            "name": "Summit Education Services",
            "city": "Hà Nội",
            "website": "https://s.vn",
            "reason": "Có luyện thi SAT.",
        },
        {
            "id": None,
            "name": "Trung tâm không mã",
            "city": "TP.HCM",
            "website": None,
            "reason": "Có chọn trường.",
        },
    ]
    assert widget.data["cities"] == [
        {"city": "Hải Phòng", "count": 2, "ask": "Cho mình xem trung tâm luyện thi IELTS, SAT ở Hải Phòng"}
    ]


async def test_the_list_answer_carries_the_cards_in_the_order_of_the_text_list():
    centers = [
        CenterHit("Trung tâm A", "TP.HCM", None, ["LUYEN_PHONG_VAN"], ["undergraduate"], id="a1"),
        CenterHit("Trung tâm B", "Hà Nội", None, ["LUYEN_PHONG_VAN"], ["undergraduate"], id="b2"),
    ]
    llm = FakeLLM(reasons("Có luyện phỏng vấn visa.", "Có luyện phỏng vấn visa."))

    out = await handler(llm, FakeFinder(centers)).run(ChatState(), "cần luyện phỏng vấn", need())

    assert out.widget is not None and out.widget.type == "centers"
    assert [c["id"] for c in out.widget.data["centers"]] == ["a1", "b2"]
    assert "- Trung tâm A" in out.answer and "- Trung tâm B" in out.answer  # danh sách chữ vẫn đủ
    assert not out.quick_replies


async def test_no_card_when_there_is_no_center_or_the_service_is_unclear():
    none_found = await handler(FakeLLM(), FakeFinder([])).run(ChatState(), "q", need())
    asking = await handler(FakeLLM(), FakeFinder([HCM])).run(ChatState(), "q", need(service=None, city=None))

    assert none_found.widget is None and asking.widget is None


async def test_asks_which_service_with_buttons_when_unclear():
    finder = FakeFinder([HCM])
    llm = FakeLLM()

    out = await handler(llm, finder).run(ChatState(), "em cần người giúp em", need(service=None, city=None))

    assert out.answer == ASK_SERVICE
    assert [b.label for b in out.quick_replies][:2] == ["Chọn trường", "Sửa bài luận"]
    assert "Luyện thi IELTS, SAT" in [b.label for b in out.quick_replies]  # giữ chữ hoa của tên riêng
    assert out.quick_replies[1].value == "Mình cần hỗ trợ sửa bài luận"  # bấm là gửi lại một câu rõ dịch vụ
    assert finder.find_calls == [] and llm.calls == []


async def test_asks_which_service_when_the_model_gave_no_entities_at_all():
    out = await handler(FakeLLM(), FakeFinder([HCM])).run(ChatState(), "cần luyện phỏng vấn")

    assert out.answer == ASK_SERVICE


async def test_says_there_is_no_verified_center_when_none_matches():
    # TH-63
    llm = FakeLLM()

    out = await handler(llm, FakeFinder([])).run(
        ChatState(), "ai nộp hồ sơ hộ em được không", need(service="APPLICATION_SUBMISSION")
    )

    assert out.answer == NO_CENTER.format(label="nộp hồ sơ")
    assert out.disclaimer is None
    assert llm.calls == []  # không có trung tâm thì không gọi AI


async def test_uses_template_reasons_when_the_ai_writes_bad_ones_twice():
    # TH-64: AI nêu tên, xin liên hệ hai lần -> dùng câu dựng từ dữ liệu, không để lời cấm tới học sinh
    llm = FakeLLM(reasons("Trung tâm A uy tín nhất."), reasons("Gọi hotline để nhận ưu đãi."))

    out = await handler(llm, FakeFinder([HCM])).run(
        ChatState(study_level="undergraduate"), "cần luyện phỏng vấn", need()
    )

    assert (
        "- Trung tâm A (TP.HCM): Có luyện phỏng vấn visa, nhận học sinh bậc đại học, cùng thành phố với bạn."
        in (out.answer)
    )
    assert "uy tín" not in out.answer and "hotline" not in out.answer


async def test_says_unavailable_when_the_backend_is_down():
    llm = FakeLLM()

    out = await handler(llm, FakeFinder(error=ConnectionError("backend tắt"))).run(ChatState(), "q", need())

    assert out.answer == UNAVAILABLE
    assert llm.calls == []


async def test_says_unavailable_when_finding_centers_fails():
    out = await handler(FakeLLM(), FakeFinder(find_error=ConnectionError("lỗi"))).run(
        ChatState(), "q", need()
    )

    assert out.answer == UNAVAILABLE


# ---------- đoạn mở đầu của Claude ----------

HUE_OPENING = (
    "Bạn cần sửa bài luận ở Huế. Hiện chưa có trung tâm đã xác minh ở Huế, các trung tâm dưới đây ở nơi khác:"
)
ESSAY_CENTER = CenterHit("Trung tâm C", "Hà Nội", None, ["SUA_BAI_LUAN"], ["undergraduate"])


def test_the_opening_must_not_be_empty_long_named_or_carry_banned_words_or_numbers():
    for bad in (
        "",
        "từ " * 46,
        "Trung tâm A có luyện phỏng vấn, bạn xem nhé.",
        "Gọi hotline nhé.",
        "Có 3 trung tâm.",
    ):
        with pytest.raises(ValueError):
            check_opening(bad, [HCM], None, None)


def test_a_city_with_a_number_in_its_name_is_not_a_banned_number():
    check_opening("Bạn ở Quận 7, mình ưu tiên trung tâm cùng thành phố.", [HCM], "Quận 7", True)


def test_when_the_city_has_no_centers_the_opening_must_say_so_with_the_city_name():
    check_opening(HUE_OPENING, [ESSAY_CENTER], "Huế", False)
    with pytest.raises(ValueError, match="thành phố học sinh nêu"):
        check_opening("Bạn cần sửa bài luận. Dưới đây là các trung tâm:", [ESSAY_CENTER], "Huế", False)


def test_a_short_center_name_is_only_forbidden_as_a_whole_word():
    check_text("Mình ưu tiên trung tâm cùng thành phố với bạn.", ["Trung tâm C"])
    with pytest.raises(ValueError):
        check_text("Hãy chọn Trung tâm C nhé.", ["Trung tâm C"])


def test_when_the_city_has_no_centers_the_opening_must_not_say_they_are_near():
    with pytest.raises(ValueError, match="ở gần"):
        check_opening(
            "Bạn ở Huế, mình ưu tiên trung tâm cùng thành phố với bạn.", [ESSAY_CENTER], "Huế", False
        )


def test_the_fallback_opening_says_there_is_no_center_in_the_city_or_names_the_service():
    assert fallback_opening("sửa bài luận", "Huế", False).startswith(
        "Hiện chưa có trung tâm đã xác minh ở Huế"
    )
    assert fallback_opening("sửa bài luận", "Hà Nội", True).endswith("hỗ trợ sửa bài luận:")
    assert fallback_opening("sửa bài luận", None, None).endswith("hỗ trợ sửa bài luận:")


def city_options(out):
    return [(c["city"], c["count"]) for c in out.widget.data["cities"]]


async def test_a_city_with_no_center_gets_no_list_but_the_cities_that_have_some():
    llm = FakeLLM()
    finder = FakeFinder([ESSAY_CENTER], counts=[CityCount("Hà Nội", 6), CityCount("TP.HCM", 1)])

    out = await handler(llm, finder).run(
        ChatState(), "em ở Huế sửa bài luận", need(service="ESSAY_EDITING", city="Huế")
    )

    assert out.answer == NO_CENTER_IN_CITY.format(city="Huế", label="sửa bài luận")
    assert (
        out.widget.data["centers"] == [] and out.disclaimer is None
    )  # không có trung tâm để liệt kê, không lấp nơi khác
    assert (
        finder.find_calls == [] and llm.calls == []
    )  # thành phố không khớp thành phố nào trong DB: không tìm, không gọi AI
    assert city_options(out) == [("Hà Nội", 6), ("TP.HCM", 1)]
    assert out.widget.data["cities"][0]["ask"] == "Cho mình xem trung tâm sửa bài luận ở Hà Nội"


async def test_a_known_city_with_no_center_for_the_service_lists_only_the_other_cities():
    finder = FakeFinder([], counts=[CityCount("Hà Nội", 6)])

    out = await handler(FakeLLM(), finder).run(
        ChatState(), "q", need(service="ESSAY_EDITING", city="Sài Gòn")
    )

    assert out.answer == NO_CENTER_IN_CITY.format(city="Sài Gòn", label="sửa bài luận")
    assert city_options(out) == [
        ("Hà Nội", 6)
    ]  # TP.HCM không có trung tâm cho dịch vụ này nên không có trong thanh


async def test_no_center_anywhere_for_the_service_keeps_the_plain_notice():
    out = await handler(FakeLLM(), FakeFinder([], counts=[])).run(ChatState(), "q", need(city="Huế"))

    assert out.answer == NO_CENTER.format(label="luyện phỏng vấn visa")
    assert out.widget is None


async def test_a_city_with_centers_lists_only_them_and_offers_the_other_cities():
    llm = FakeLLM(reasons("Có luyện phỏng vấn visa, cùng thành phố với bạn."))
    finder = FakeFinder([HN], counts=[CityCount("Hà Nội", 1), CityCount("TP.HCM", 2)])

    out = await handler(llm, finder).run(ChatState(), "q", need(city="Hà Nội"))

    assert [c["city"] for c in out.widget.data["centers"]] == ["Hà Nội"]  # đúng số trung tâm có ở Hà Nội
    assert city_options(out) == [("TP.HCM", 2)]  # chỉ nơi khác, không lặp lại Hà Nội
    assert finder.count_calls == [("LUYEN_PHONG_VAN", None)]


async def test_without_a_city_the_widget_offers_every_city_when_there_are_two_or_more():
    llm = FakeLLM(reasons("Có luyện phỏng vấn visa.", "Có luyện phỏng vấn visa."))
    finder = FakeFinder([HCM, HN], counts=[CityCount("Hà Nội", 1), CityCount("TP.HCM", 1)])

    out = await handler(llm, finder).run(ChatState(), "q", need(city=None))

    assert city_options(out) == [("Hà Nội", 1), ("TP.HCM", 1)]


async def test_without_a_city_and_with_a_single_city_there_is_nothing_to_choose():
    llm = FakeLLM(reasons("Có luyện phỏng vấn visa."))

    out = await handler(llm, FakeFinder([HN], counts=[CityCount("Hà Nội", 1)])).run(
        ChatState(), "q", need(city=None)
    )

    assert city_options(out) == []


async def test_the_list_still_shows_when_the_city_counts_cannot_be_loaded():
    llm = FakeLLM(reasons("Có luyện phỏng vấn visa."))
    finder = FakeFinder([HN], counts_error=RuntimeError("down"))

    out = await handler(llm, finder).run(ChatState(), "q", need(city="Hà Nội"))

    assert "- Trung tâm B (Hà Nội)" in out.answer and city_options(out) == []


async def test_more_than_three_centers_are_all_in_the_widget_but_only_the_first_page_goes_to_the_ai():
    many = [
        CenterHit(f"Trung tâm {n}", "Hà Nội", None, ["LUYEN_PHONG_VAN"], ["undergraduate"], id=f"id{n}")
        for n in "ABCDE"
    ]
    llm = FakeLLM(reasons("Có luyện phỏng vấn visa.", "Có luyện phỏng vấn visa.", "Có luyện phỏng vấn visa."))

    out = await handler(llm, FakeFinder(many)).run(ChatState(), "q", need(city="Hà Nội"))

    sent = json.loads(llm.calls[0]["messages"][1]["content"])
    assert len(sent["centers"]) == 3 and len(llm.calls) == 1  # AI chỉ viết cho trang đầu
    cards = out.widget.data["centers"]
    assert [c["name"] for c in cards] == [f"Trung tâm {n}" for n in "ABCDE"]
    assert (
        cards[3]["reason"] == "Có luyện phỏng vấn visa, cùng thành phố với bạn."
    )  # trung tâm sau trang đầu: câu dựng bằng code
    assert (
        "- Trung tâm C" in out.answer and "- Trung tâm D" not in out.answer
    )  # danh sách chữ chỉ có trang đầu
    assert "Còn 2 trung tâm khác" in out.answer


async def test_a_known_city_whose_centers_are_all_elsewhere_is_also_told():
    # học sinh ở Sài Gòn (TP.HCM có trong DB) nhưng danh sách chỉ có trung tâm ở Hà Nội
    opening = "Bạn cần luyện phỏng vấn visa ở Sài Gòn. Hiện chưa có trung tâm đã xác minh ở Sài Gòn, các trung tâm dưới đây ở nơi khác:"
    llm = FakeLLM(reasons("Có luyện phỏng vấn visa.", opening=opening))

    out = await handler(llm, FakeFinder([HN])).run(ChatState(), "q", need(city="Sài Gòn"))

    sent = json.loads(llm.calls[0]["messages"][1]["content"])
    assert sent["student_city"] == "Sài Gòn" and sent["city_has_centers"] is False
    assert out.answer.startswith(opening + "\n- Trung tâm B (Hà Nội)")


async def test_without_a_city_the_ai_is_told_there_is_nothing_to_say_about_it():
    llm = FakeLLM(
        reasons(
            "Có luyện phỏng vấn visa.", opening="Bạn cần luyện phỏng vấn visa. Dưới đây là các trung tâm:"
        )
    )

    await handler(llm, FakeFinder([HN])).run(ChatState(), "q", need(city=None))

    sent = json.loads(llm.calls[0]["messages"][1]["content"])
    assert sent["student_city"] is None and sent["city_has_centers"] is None


# ---------- việc hệ thống chưa làm được ----------


@pytest.mark.parametrize("task", ["booking", "compare"])
async def test_booking_and_comparing_are_said_not_to_be_ready_but_the_centers_are_still_suggested(task):
    llm = FakeLLM(reasons("Có luyện phỏng vấn visa."))

    out = await handler(llm, FakeFinder([HN])).run(ChatState(), "q", need(tasks=[task]))

    assert out.answer.startswith(TASK_NOTES[task] + "\n\n")
    assert "Trung tâm B" in out.answer


async def test_booking_without_a_service_still_says_it_is_not_ready_before_asking_the_service():
    out = await handler(FakeLLM(), FakeFinder([])).run(
        ChatState(), "q", need(service=None, tasks=["booking"])
    )

    assert out.answer == f"{TASK_NOTES['booking']}\n\n{ASK_SERVICE}"


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
    llm = FakeLLM(reasons("Có luyện phỏng vấn visa."))

    out = await handler(llm, FakeFinder([HN])).run(ChatState(), message, need())

    assert out.answer.startswith(NOT_PROVIDED + "\n\n")
    assert "Trung tâm B" in out.answer  # vẫn có danh sách đã xác minh
    assert out.disclaimer == DISCLAIMER


async def test_a_plain_service_request_has_no_not_provided_notice():
    llm = FakeLLM(reasons("Có luyện phỏng vấn visa."))

    out = await handler(llm, FakeFinder([HN])).run(ChatState(), "em cần luyện phỏng vấn visa", need())

    assert NOT_PROVIDED not in out.answer


async def test_the_not_provided_notice_also_comes_before_the_service_question():
    out = await handler(FakeLLM(), FakeFinder([])).run(
        ChatState(), "cho em số điện thoại của trung tâm", need(service=None)
    )

    assert out.answer == f"{NOT_PROVIDED}\n\n{ASK_SERVICE}"
    assert len(out.quick_replies) == len(SERVICES)
