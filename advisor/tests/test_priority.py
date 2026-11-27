"""Ưu tiên nói trong chat: nhận ra yếu tố và mức, không nhầm với thông tin hồ sơ hay câu hỏi kiến thức."""

from app.schemas.chat import ChatState
from app.schemas.slm import SlmTinResult, SlmY


# ---- ưu tiên do model trả (priority_*) trong changes
def test_priorities_the_model_returned_are_read_and_bad_values_are_dropped():
    from app.services.priority import from_model

    changes = [
        {"factor": "priority_aid", "value": "4"},
        {"factor": "priority_cost", "value": "9"},  # ngoài 0..4
        {"factor": "priority_prestige", "value": "cao"},  # không phải số
        {"factor": "priority_unknown", "value": "2"},  # yếu tố lạ
        {"factor": "sat", "value": "1500"},  # không phải ưu tiên
    ]

    assert from_model(changes) == {"aid": 4}


async def test_a_what_if_with_only_model_priorities_goes_to_dotnet_with_them():
    from app.services.branches import build_branches
    from tests.fakes import tin
    from tests.test_branches import skills

    entities = {
        "mode": "what_if",
        "changes": [{"factor": "priority_cost", "value": "0"}, {"factor": "priority_aid", "value": "4"}],
    }

    out = await build_branches(skills([]))["scenario"].run(
        ChatState(has_results=True), "x", tin(("scenario", entities, "đổi ưu tiên"))
    )

    assert out.action == "simulate"
    assert out.action_data["priorities"] == {"cost": 0, "aid": 4}
    assert out.action_data["unsupported"] == []  # ưu tiên không bị báo là "chưa thử được"


def profile_compare(*schools: str) -> SlmTinResult:
    entities = {"tasks": ["compare"], "schools": list(schools)}
    return SlmTinResult(intents=[SlmY(type="profile_review", span="x", entities=entities)])


# ---- không còn quy tắc từ khóa: câu chữ không tự thành ưu tiên, chỉ điều model trả (priority_*) mới tính
async def test_a_scholarship_need_the_model_read_is_not_also_counted_as_a_priority():
    # Lỗi thật 16/11: "nêu không cần học bổng" model trả đúng mức cần học bổng, nhưng quy tắc từ khóa thêm "Ưu tiên mới: học bổng nhiều 0/4"
    from app.services.branches import build_branches
    from tests.fakes import tin

    slm = tin(("scenario", {"mode": "what_if", "changes": [{"factor": "scholarship", "value": "optional"}]}))
    out = await build_branches({})["scenario"].run(
        ChatState(has_results=True), "nêu không cần học bổng thì sao", slm
    )

    assert out.action == "simulate"
    assert "priorities" not in out.action_data
    assert "Ưu tiên" not in out.answer
