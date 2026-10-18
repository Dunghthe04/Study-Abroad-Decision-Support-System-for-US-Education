from app.schemas.ec import EcRequest
from app.services.ec_reader import EcReader
from tests.fakes import FakeLLM

AN = {
    "act": [
        {"text": "Đội trưởng CLB Robotics của trường", "scope": 1, "months": 24, "role": "leader"},
        {"text": "Tham gia CLB tiếng Anh của trường", "scope": 5, "months": 6, "role": "member"},
    ],
    "awards": [],
}


async def test_lowers_school_club_that_student_marked_international():
    llm = FakeLLM({"act": [{"role": 0.8, "scope": 1}, {"role": 0.3, "scope": 1}]})
    res = await EcReader(llm, "qwen3:8b").read(EcRequest.model_validate(AN))

    assert res.ai_used is True
    assert res.act[1].scope == 1  # chọn quốc tế nhưng câu chữ là CLB trường
    assert 1 <= res.tier <= 5


async def test_falls_back_to_form_role_when_qwen_raises_scope_twice():
    ho_so = {
        "act": [
            {"text": "Đội trưởng CLB Robotics của trường", "scope": 1, "months": 24, "role": "leader"},
            {"text": "Tham gia CLB tiếng Anh của trường", "scope": 1, "months": 6, "role": "member"},
        ],
        "awards": [],
    }
    llm = FakeLLM(
        {"act": [{"role": 0.8, "scope": 1}, {"role": 0.3, "scope": 5}]},
        {"act": [{"role": 0.8, "scope": 1}, {"role": 0.3, "scope": 5}]},
    )
    res = await EcReader(llm, "qwen3:8b").read(EcRequest.model_validate(ho_so))

    assert res.ai_used is False
    assert res.act[1].scope == 1  # giữ phạm vi học sinh khai, không nhận scope 5
    assert res.act[1].role == 0.3  # member trên form
    assert len(llm.calls) == 2