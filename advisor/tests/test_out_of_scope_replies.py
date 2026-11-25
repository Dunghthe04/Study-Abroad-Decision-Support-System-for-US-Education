"""UC-157: câu ngoài phạm vi trả bằng câu cố định (không gọi mô hình trên đám mây), nút liên kết, và "cho em gặp người thật" theo gói."""

import pytest

from app.schemas.chat import ChatMessage, ChatRequest, ChatState
from app.schemas.slm import SlmY
from app.services import replies
from app.services.branches import build_branches
from app.services.chat_flow import SkillOutput, free_handoff
from tests.fakes import flow_with, tin

FREE = ["knowledge", "out_of_scope"]
MENU_LABELS = [b.label for b in replies.menu_buttons()]


def ask(text: str, allowed: list[str] | None) -> ChatRequest:
    req = ChatRequest(messages=[ChatMessage(role="user", content=text)], state=ChatState())
    req.allowed_intents = allowed
    return req


class CenterSkill:
    """Skill trung tâm giả: ghi lại lần được gọi."""

    def __init__(self) -> None:
        self.calls: list[object] = []

    async def run(self, state, message, entities=None):
        self.calls.append(entities)
        return SkillOutput("Bạn cần hỗ trợ phần nào?")


# ---- exception: chỉ hỗ trợ bậc đại học


async def test_the_out_of_topic_sentence_says_only_undergraduate_study_in_the_us_is_supported():
    out = await build_branches({})["out_of_scope"].run(
        ChatState(), "em muốn học thạc sĩ", tin(("out_of_scope", {"subtype": "exception"}))
    )

    assert out.answer == replies.OUT_OF_SCOPE["exception"]
    assert "bậc đại học" in out.answer
    assert "thạc sĩ" in out.answer and "tiến sĩ" in out.answer


# ---- about_system: nguồn dữ liệu, ngày cập nhật, độ tin cậy, kèm liên kết tới trang Căn cứ tham số


async def test_the_about_system_answer_covers_sources_dates_and_reliability_and_links_to_the_parameters_page():
    out = await build_branches({})["out_of_scope"].run(
        ChatState(), "dữ liệu lấy từ đâu", tin(("out_of_scope", {"subtype": "about_system"}))
    )

    assert out.answer == replies.ABOUT_SYSTEM
    assert "nguồn" in out.answer and "ngày thu thập" in out.answer and "tham khảo" in out.answer
    first, *rest = out.quick_replies
    assert (first.label, first.href) == ("Xem căn cứ tham số", "/methodology")
    assert [b.label for b in rest] == MENU_LABELS  # vẫn đủ nút menu, không cụt đường


async def test_the_parameters_page_button_is_a_fresh_copy_each_time():
    branch = build_branches({})["out_of_scope"]
    slm = tin(("out_of_scope", {"subtype": "about_system"}))

    first = await branch.run(ChatState(), "x", slm)
    first.quick_replies[0].label = "đã sửa"
    second = await branch.run(ChatState(), "x", slm)

    assert second.quick_replies[0].label == "Xem căn cứ tham số"


# ---- human_handoff: Premium vào luồng trung tâm, Miễn phí tới trang danh bạ Trung tâm


def handoff() -> SlmY:
    return SlmY(type="out_of_scope", span="cho em gặp tư vấn viên", entities={"subtype": "human_handoff"})


def test_only_a_human_handoff_is_turned_into_the_free_account_kind():
    converted = free_handoff(handoff())

    assert converted.entities["subtype"] == replies.HANDOFF_FREE
    assert handoff().entities["subtype"] == "human_handoff"  # ý gốc không bị đổi (log giữ nhãn model gán)
    for other in ("exception", "greeting", "about_system", "sensitive"):
        y = SlmY(type="out_of_scope", span="x", entities={"subtype": other})
        assert free_handoff(y) == y
    knowledge = SlmY(type="knowledge", span="x")
    assert free_handoff(knowledge) == knowledge  # chỉ ý ngoài phạm vi


async def test_a_free_account_asking_for_a_person_is_sent_to_the_centers_page_without_the_center_flow():
    center = CenterSkill()
    flow = flow_with({"skill_chuyen_trung_tam": center}, tin(("out_of_scope", {"subtype": "human_handoff"})))

    res = await flow.reply(ask("cho em gặp tư vấn viên", FREE))

    assert center.calls == []  # gợi ý trung tâm trong chat là chức năng Premium
    assert res.answer == replies.HANDOFF_FREE_REPLY
    assert (res.quick_replies[0].label, res.quick_replies[0].href) == ("Trang Trung tâm", "/centers")
    # nút Tìm trung tâm tư vấn cũng bị khóa với gói Miễn phí nên không được hiện
    assert "Tìm trung tâm tư vấn" not in [b.label for b in res.quick_replies]
    assert res.locked_intents == []  # ngoài phạm vi vẫn là ý được phép: không hiện thẻ nâng cấp
    assert res.slm.out_of_scope == "human_handoff"  # tóm tắt SLM vẫn ghi nhãn model gán


async def test_a_premium_account_asking_for_a_person_goes_into_the_center_flow():
    center = CenterSkill()
    flow = flow_with({"skill_chuyen_trung_tam": center}, tin(("out_of_scope", {"subtype": "human_handoff"})))

    res = await flow.reply(ask("cho em gặp tư vấn viên", None))

    assert center.calls == [{"follow_up": True}]
    assert res.answer.startswith(replies.HANDOFF_INTRO)


@pytest.mark.parametrize("allowed", [None, FREE])
async def test_the_other_out_of_scope_answers_do_not_depend_on_the_plan(allowed):
    flow = flow_with({}, tin(("out_of_scope", {"subtype": "exception"})))

    res = await flow.reply(ask("thời tiết hôm nay", allowed))

    assert res.answer == replies.OUT_OF_SCOPE["exception"]
