"""Lời cho câu "nếu đổi X" khi X không thử được: Claude viết, code chặn thiếu tên mục, nói đã thử, số lạ."""

import pytest

from app.services.ollama import OllamaError
from app.services.scenario import SUPPORTED_TEXT, parse_what_if
from app.services.skill_call import load_skill
from app.services.what_if_writer import SKILL_PATH, WhatIfWriter, build_user, check_answer
from tests.fakes import FakeLLM

CHANGES = [
    {"factor": "major", "value": "Khoa học máy tính"},
    {"factor": "intake", "value": "Fall 2027"},
    {"factor": "activities", "value": "x"},
]
GOOD = {
    "answer": "Ngành là điều kiện lọc trường nên mình không thử tạm được, bạn sửa trong hồ sơ rồi nhờ mình "
    "gợi ý lại nhé. Kỳ nhập học Fall 2027 không làm thay đổi danh sách trường gợi ý."
}


def user() -> dict:
    return build_user(parse_what_if(CHANGES), CHANGES)


def test_skill_file_has_version():
    assert load_skill(SKILL_PATH)[1] == "1.0"


# Gửi Claude tên mục theo nhóm và giá trị muốn đổi, không gửi nguyên tin nhắn
def test_build_user_groups_labels_and_values():
    assert user() == {
        "filters": ["ngành"],
        "noEffect": ["kỳ nhập học"],
        "others": ["hoạt động ngoại khóa"],
        "values": {"ngành": "Khoa học máy tính", "kỳ nhập học": "Fall 2027"},
        "canTry": SUPPORTED_TEXT,
    }


def test_build_user_joins_list_values():
    changes = [{"factor": "preferred_states", "value": ["CA", "NY"]}]
    assert build_user(parse_what_if(changes), changes)["values"] == {"bang muốn học": "CA, NY"}


def test_good_answer_passes():
    assert check_answer(GOOD, user()) == GOOD["answer"]


@pytest.mark.parametrize(
    "answer",
    [
        "Ngành là điều kiện lọc trường nên mình không thử tạm được nhé bạn.",  # thiếu kỳ nhập học
        "Mình đã thử đổi ngành và kỳ nhập học, danh sách vẫn vậy bạn nhé.",  # nói đã thử
        "Ngành và kỳ nhập học không đổi được, có 12 trường vẫn giữ nguyên.",  # số lạ
        "Ngắn",  # quá ngắn
    ],
)
def test_bad_answer_is_rejected(answer):
    with pytest.raises(ValueError):
        check_answer({"answer": answer}, user())


async def test_writer_retries_once_then_returns_answer():
    llm = FakeLLM({"answer": "Ngành không thử được bạn nhé, sửa hồ sơ là được."}, GOOD)
    answer = await WhatIfWriter(llm, "claude-test").write(parse_what_if(CHANGES), CHANGES)

    assert answer == GOOD["answer"]
    assert len(llm.calls) == 2
    assert llm.calls[0]["messages"][0]["content"].startswith("# SKILL NẾU ĐỔI")


async def test_writer_returns_none_when_claude_keeps_failing():
    llm = FakeLLM(OllamaError("hết hạn"), {"answer": "Mình đã thử rồi, ngành và kỳ nhập học không đổi."})
    assert await WhatIfWriter(llm, "claude-test").write(parse_what_if(CHANGES), CHANGES) is None
