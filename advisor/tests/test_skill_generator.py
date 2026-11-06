import json

import pytest

from app.schemas.slm import SKILL_SECTIONS, SkillRequest
from app.services.ollama import OllamaError
from app.services.skill_generator import SkillGenerator

REQ = SkillRequest(
    loai="ho_so",
    cau_hoi="Hồ sơ của em có đủ vào ngành Khoa học máy tính không?",
    ho_so={"gpa": "8.5", "english": "IELTS 6.5", "major": "Khoa học máy tính"},
    tinh_san={"gpa_level": "3.4 thang 4, trung bình (TB các trường 3.6)"},
)


def good_skill() -> str:
    """Skill đúng khuôn: dòng Mốc + 8 mục, giữ đủ số của hồ sơ và dòng tính sẵn."""
    body = {name: "Nội dung." for name in SKILL_SECTIONS["ho_so"]}
    body["2. Dữ liệu dùng để tính"] = "GPA 8.5 (3.4 thang 4, TB các trường 3.6), IELTS 6.5."
    sections = "\n\n".join(f"## {name}\n{text}" for name, text in body.items())
    return f"# Mốc: GPA trung bình, tiếng Anh trung bình, SAT chưa thi, hoạt động chưa có, ngân sách đủ\n\n{sections}\n"


class FakeSlm:
    """SLM giả: trả lần lượt từng câu trả lời (chữ, hoặc lỗi để ném ra), ghi lại message đã nhận."""

    def __init__(self, *replies: str | Exception) -> None:
        self._replies = list(replies)
        self.calls: list[list[dict[str, str]]] = []

    async def chat_text(self, messages: list[dict[str, str]], temperature: float = 0.0) -> str:
        self.calls.append(messages)
        reply = self._replies.pop(0)
        if isinstance(reply, Exception):
            raise reply
        return reply


async def test_valid_skill_is_returned_and_sent_with_prompt_and_request():
    slm = FakeSlm(good_skill())

    skill = await SkillGenerator(slm).generate(REQ)

    assert skill == good_skill().strip()
    system, user = slm.calls[0]
    assert "SINH SKILL HỒ SƠ" in system["content"]
    # Đầu vào đúng dạng ví dụ trong prompt: không kèm khóa rỗng của kịch bản
    assert json.loads(user["content"]) == {
        "loai": "ho_so",
        "cau_hoi": REQ.cau_hoi,
        "ho_so": REQ.ho_so,
        "tinh_san": REQ.tinh_san,
    }


async def test_wrong_skill_is_retried_once():
    slm = FakeSlm("## 1. Nhiệm vụ\nthiếu mục", good_skill())

    assert await SkillGenerator(slm).generate(REQ) == good_skill().strip()
    assert len(slm.calls) == 2


async def test_skill_that_drops_a_profile_number_is_rejected():
    dropped = good_skill().replace("IELTS 6.5", "IELTS")
    slm = FakeSlm(dropped, dropped)

    assert await SkillGenerator(slm).generate(REQ) is None


async def test_wrong_twice_gives_none_instead_of_a_static_skill():
    slm = FakeSlm(OllamaError("down"), "không phải skill")

    assert await SkillGenerator(slm).generate(REQ) is None
    assert len(slm.calls) == 2


async def test_same_question_and_profile_reuses_the_skill_without_calling_slm():
    slm = FakeSlm(good_skill())
    generator = SkillGenerator(slm)

    first = await generator.generate(REQ)
    second = await generator.generate(REQ.model_copy())

    assert first == second
    assert len(slm.calls) == 1


async def test_changed_profile_generates_a_new_skill():
    other = REQ.model_copy(update={"ho_so": {**REQ.ho_so, "english": "IELTS 7.0"}})
    slm = FakeSlm(good_skill(), good_skill().replace("6.5", "7.0"))
    generator = SkillGenerator(slm)

    await generator.generate(REQ)
    await generator.generate(other)

    assert len(slm.calls) == 2


async def test_cache_drops_the_oldest_skill_when_full():
    other = REQ.model_copy(update={"cau_hoi": "Em nên học trường nào?"})
    slm = FakeSlm(good_skill(), good_skill(), good_skill())
    generator = SkillGenerator(slm, cache_size=1)

    await generator.generate(REQ)
    await generator.generate(other)  # đẩy REQ ra khỏi cache
    await generator.generate(REQ)

    assert len(slm.calls) == 3


async def test_kind_without_a_prompt_is_a_programming_error():
    req = SkillRequest(
        loai="kich_ban", cau_hoi="Nếu IELTS 7.0 thì sao?", ho_so={"english": "IELTS 6.5"}, muc_tieu="7.0"
    )

    with pytest.raises(ValueError):
        await SkillGenerator(FakeSlm()).generate(req)


async def test_each_generation_is_logged_with_input_skill_and_every_attempt(tmp_path):
    slm = FakeSlm("## 1. Nhiệm vụ\nthiếu mục", good_skill())
    generator = SkillGenerator(slm, log_dir=tmp_path)

    await generator.generate(REQ)
    await generator.generate(REQ)  # lấy từ cache: không ghi thêm dòng

    (log_file,) = tmp_path.glob("skill_*.jsonl")
    (row,) = [json.loads(line) for line in log_file.read_text(encoding="utf-8").splitlines()]
    assert (row["cau_hoi"], row["ho_so"], row["tinh_san"]) == (REQ.cau_hoi, REQ.ho_so, REQ.tinh_san)
    assert row["skill"] == good_skill().strip()
    first, second = row["tung_lan"]
    assert first["chu_slm"] == "## 1. Nhiệm vụ\nthiếu mục" and first["loi"]
    assert second["loi"] is None and second["giay"] >= 0


async def test_failed_generation_is_logged_with_reasons(tmp_path):
    slm = FakeSlm(OllamaError("hết giờ"), "không phải skill")

    assert await SkillGenerator(slm, log_dir=tmp_path).generate(REQ) is None

    (log_file,) = tmp_path.glob("skill_*.jsonl")
    row = json.loads(log_file.read_text(encoding="utf-8"))
    assert row["skill"] is None
    assert [a["chu_slm"] for a in row["tung_lan"]] == [None, "không phải skill"]
    assert row["tung_lan"][0]["loi"] == "gọi model: hết giờ"
