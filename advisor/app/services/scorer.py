"""Lượt ② (chấm trường): chia lô, gọi AI theo skill_score.md, kiểm tra JSON, sai thì gọi lại 1 lần."""
import json
import re
from pathlib import Path
from typing import Any

from app.schemas.score import ScoreItem, ScoreRequest, ScoreResponse, ScoreSchool, score_schema
from app.services.ollama import OllamaError

SKILL_PATH = Path(__file__).resolve().parent.parent / "skills" / "skill_score.md"
BATCH_SIZE =8 # 8 trường/lô: vừa num_ctx 6144, model 8B còn tính chính xác
MAX_ATTEMPTS=2 # lô sai định dạng thì gọi lại đúng 1 lần

# Đọc skill
def load_skill(path: Path =SKILL_PATH) -> tuple[str, str]:
    """Đọc file skill, lấy số phiên bản ở dòng đầu ("skill_version 1.1")."""
    text = path.read_text(encoding="utf-8")
    match = re.search(r"skill_version\s+([\w.]+)", text)
    return text, match.group(1) if match else "unknown"

#kiểm tra kết quả JSON do AI trả về có đúng cấu trúc, đúng mã trường, đúng thứ tự và nằm trong các giới hạn hợp lệ hay không.
#raw dữ liệu AI trả về, codes là mã trường ban đầu 
def check_batch(raw: dict[str, Any], codes: list[str]) -> list[ScoreItem]:
    """Kiểm tra JSON AI trả về cho 1 lô (mục 7). Sai thì ném ValueError."""
    
    items = []
    try:
        # Duyệt qua kết quả từng trường
        for x in raw["items"]:
            item = ScoreItem.model_validate(x)
            items.append(item)
    except (KeyError, TypeError) as ex:
        raise ValueError("JSON thiếu danh sách items") from ex

    actual_codes =[]
    for item in items:
        actual_codes.append(item.c)
    #Nếu danh sách mã trường khác danh sách ban đầu
    if actual_codes != codes:
        raise ValueError("Mã trường hoặc thứ tự trường không khớp")

    for i in items:
        for name in ("pg", "ps", "A"):
            v = getattr(i, name)
            if v is not None and not 0 <= v <= 100:
                raise ValueError(f"{i.c}: {name} = {v} ngoài 0–100")
        if not -6 <= i.adj <= 6:
            raise ValueError(f"{i.c}: adj = {i.adj} ngoài −6..+6")
        for name in ("F", "H", "Q"):
            v = getattr(i, name)
            if v is not None and not 0 <= v <= 1:
                raise ValueError(f"{i.c}: {name} = {v} ngoài 0–1")
    return items

class Scorer:
    def __init__(self, llm: Any, model: str, skill_path: Path=SKILL_PATH, batch_size: int=BATCH_SIZE)-> None:
         # Lưu đối tượng giao tiếp với AI
        self._llm = llm

        # Lưu tên mô hình AI, ví dụ qwen3:8b
        self._model = model

        # Đọc hướng dẫn chấm điểm và phiên bản hướng dẫn
        self._skill, self.skill_version = load_skill(skill_path)

        # Số trường được chấm trong mỗi lần gọi AI
        self._batch_size = batch_size

    async def score(self, req: ScoreRequest) -> ScoreResponse:
        #Chuyển hồ sơ học sinh thành dictionary
        student = req.student.model_dump(exclude_none=True)

        #Chuẩn bị danh sách kq
        items =[]
        failed = []

        #Chia danh sách trường thành nhiều lô
        for start in range(0, len(req.schools), self._batch_size):
            batch = req.schools[
                start: start+self._batch_size
            ]

            #Gửi lô trường cho AI chấm
            result = await self._score_batch(student, batch)

            #Xử lý kết quả của lô
            if result is None:
                #Ghi nhận mã trường thuộc lô thất bại
                for school in batch:
                    failed.append(school.c)
            else:
                #Thêm kết quả hợp lệ vào danh sách chung
                for item in result:
                    items.append(item)
        return ScoreResponse(
            skill_version = self.skill_version,
            model = self._model,
            items= items,
            failed= failed

        )


    async def _score_batch(
        self,
        student: dict[str, Any],
        batch: list[ScoreSchool]
    ) -> list[ScoreItem] | None:

        # Bước 1: Lấy mã các trường trong lô
        codes = []
        for school in batch:
            codes.append(school.c)

        # Bước 2: Chuẩn bị dữ liệu gửi AI
        user = {
            "student": student,
            # duyệt từng trường trong lô, bỏ các tt = none
            "schools": [
                school.model_dump(exclude_none=True)
                for school in batch
            ]
        }

        # Bước 3: Tạo nội dung hội thoại gửi AI
        messages = [
            # Hướng dẫn + quy tắc cần tuân theo
            {
                "role": "system",
                "content": self._skill
            },
            # Dữ liệu cần đánh giá
            {
                "role": "user",
                "content": json.dumps( # Chuyển thành chuỗi json
                    user,
                    ensure_ascii=False,
                    separators=(",", ":")
                )
            }
        ]

        # Bước 4: Thử gọi AI tối đa MAX_ATTEMPTS lần
        for _ in range(MAX_ATTEMPTS):
            try:
                # Gửi yêu cầu và nhận JSON từ AI
                raw = await self._llm.chat_json(
                    messages,
                    score_schema(codes),
                    temperature=0.0
                )

                # Kiểm tra kết quả AI trả về
                checked_items = check_batch(raw, codes)

                # Hợp lệ thì trả kết quả ngay
                return checked_items

            except (OllamaError, ValueError):
                # Gặp lỗi thuộc nhóm trên thì thử lại
                continue

        # Tất cả các lần thử đều thất bại
        return None
