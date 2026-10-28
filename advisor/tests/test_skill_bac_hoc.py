import pytest

from app.services.ollama import OllamaError
from app.services.skills.skill_bac_hoc import UNKNOWN, BacHocRead, BacHocReader, check_read
from tests.fakes import FakeLLM

TRONG = {"current_level": None, "current_grade": None, "target_level": None, "major": None, "intake": None}


async def test_reads_current_level_and_grade():
    llm = FakeLLM({**TRONG, "current_level": "high_school", "current_grade": 12})
    res = await BacHocReader(llm, "qwen3:8b").read("B1.1", "Em đang học lớp 12 ạ")

    assert res == BacHocRead(current_level="high_school", current_grade=12, ai_used=True)


@pytest.mark.parametrize(
    ("said", "code"),
    [
        ("bachelor", "undergraduate"),
        ("college", "community_college"),
        ("master", "master"),
        ("unknown", UNKNOWN),
    ],
)
def test_target_level_mapped_to_study_level(said, code):
    assert check_read({**TRONG, "target_level": said}).target_level == code


@pytest.mark.parametrize("major", ["unknown", "Undecided", " chưa rõ "])
def test_major_not_sure_is_unknown(major):
    assert check_read({**TRONG, "major": major}).major == UNKNOWN


def test_major_kept_and_blank_is_none():
    assert check_read({**TRONG, "major": " Computer Science "}).major == "Computer Science"
    assert check_read({**TRONG, "major": "  "}).major is None


@pytest.mark.parametrize(
    "wrong",
    [
        {"current_level": "grade_12"},
        {"current_grade": 13},
        {"target_level": "đại học"},
        {"intake": "tháng 9/2027"},
    ],
)
def test_wrong_value_raises(wrong):
    with pytest.raises(ValueError):
        check_read({**TRONG, **wrong})


async def test_wrong_twice_returns_empty_read():
    llm = FakeLLM({**TRONG, "current_level": "lop_12"}, {**TRONG, "current_level": "lop_12"})
    res = await BacHocReader(llm, "qwen3:8b").read("B1.1", "Em lớp 12")

    assert res == BacHocRead()
    assert len(llm.calls) == 2


async def test_ollama_error_returns_empty_read():
    llm = FakeLLM(OllamaError("tắt máy"), OllamaError("tắt máy"))
    res = await BacHocReader(llm, "qwen3:8b").read("B1.4", "tháng 9 năm 2027")

    assert res == BacHocRead()


async def test_sends_question_id_and_message():
    llm = FakeLLM({**TRONG, "intake": "Fall 2027"})
    res = await BacHocReader(llm, "qwen3:8b").read("B1.4", "tháng 9 năm 2027")

    assert res.intake == "Fall 2027"
    assert '"question_id":"B1.4"' in llm.calls[0]["messages"][1]["content"]
