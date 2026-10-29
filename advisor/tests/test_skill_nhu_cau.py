from app.services.ollama import OllamaError
from app.services.skills.skill_nhu_cau import NhuCauReader
from tests.fakes import FakeLLM

CAU_DAU = "Em lớp 12, muốn học đại học ở Mỹ, GPA 8.5, IELTS 6.5"


async def test_reads_need_and_info_with_profile_keys():
    llm = FakeLLM(
        {
            "need": "profile_advice",
            "branch": "DH",
            "info_given": {"current_level": "lớp 12", "gpa_text": "8.5", "english_text": "IELTS 6.5"},
        }
    )
    res = await NhuCauReader(llm, "qwen3:8b").read(CAU_DAU)

    assert res.ai_used is True
    assert (res.need, res.branch) == ("profile_advice", "DH")
    assert res.info == {"current_level": "lớp 12", "gpa": "8.5", "english": "IELTS 6.5"}
    assert llm.calls[0]["messages"][1]["content"] == '{"message":"' + CAU_DAU + '"}'


async def test_wrong_need_twice_falls_back_to_unclear():
    llm = FakeLLM({"need": "bậy", "branch": None, "info_given": {}}, {"need": "bậy"})
    res = await NhuCauReader(llm, "qwen3:8b").read(CAU_DAU)

    assert res.ai_used is False
    assert (res.need, res.branch, res.info) == ("unclear", None, {})
    assert len(llm.calls) == 2


async def test_wrong_once_then_right_uses_second_answer():
    llm = FakeLLM(
        {"need": "knowledge", "branch": "CH"}, {"need": "knowledge", "branch": None, "info_given": {}}
    )
    res = await NhuCauReader(llm, "qwen3:8b").read("Visa du học Mỹ cần gì?")

    assert (res.need, res.ai_used) == ("knowledge", True)


async def test_ollama_error_falls_back_to_unclear():
    llm = FakeLLM(OllamaError("tắt máy"), OllamaError("tắt máy"))
    res = await NhuCauReader(llm, "qwen3:8b").read(CAU_DAU)

    assert (res.need, res.ai_used) == ("unclear", False)


def test_skill_version_read_from_prompt():
    assert NhuCauReader(FakeLLM(), "qwen3:8b").skill_version == "1.0"
