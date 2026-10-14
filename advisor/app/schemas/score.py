"""Dữ liệu vào/ra của lượt ② (chấm trường). Key ngắn, đúng như mục 4 và 6 trong GOI_Y_TRUONG_AI_CACH_THUC_HIEN."""

from typing import Any, Literal

from pydantic import BaseModel, Field

MAX_SCHOOLS = 40  # Net cắt tối đa còn 40 trường


class ScoreStudent(BaseModel):
    gpa: float | None = Field(default=None, ge=0, le=4)
    sat: int | None = Field(default=None, ge=400, le=1600)
    budget: float | None = Field(default=None, gt=0)
    tier: int = Field(ge=1, le=5)  # bậc ngoại khóa


class ScoreSchool(BaseModel):
    """Một trường đã chuẩn hóa ở .NET. Thiếu số thì bỏ hẳn key (None ở đây = key vắng mặt)."""

    c: str
    acc: float | None = Field(default=None, ge=0, le=1)  # tỉ lệ nhận chung
    iacc: float | None = Field(default=None, ge=0, le=1)  # tỉ lệ nhận du học sinh
    sat: list[int] | None = Field(default=None, min_length=2, max_length=2)  # [mốc 25, mốc 75]
    subm: float | None = Field(default=None, ge=0, le=1)  # tỉ lệ nộp SAT
    satp: Literal["req", "opt", "no"] | None = None
    gpa: list[float] | None = Field(default=None, min_length=7, max_length=7)  # 7 khoảng C11
    avg: float | None = Field(default=None, ge=0, le=4)
    L: list[int] = Field(min_length=3, max_length=3)  # C7 [GPA, SAT, ngoại khóa], .NET đã điền mặc định
    coa: float | None = Field(default=None, gt=0)
    aid: float | None = Field(default=None, ge=0)
    q: float | None = Field(default=None, ge=0, le=1)


class ScoreRequest(BaseModel):
    student: ScoreStudent
    schools: list[ScoreSchool] = Field(min_length=1, max_length=MAX_SCHOOLS)


class ScoreItem(BaseModel):
    """Kết quả 1 trường. Các bước trung gian giữ lại để biết AI sai ở bước nào."""

    c: str  # code: mã trường, chép nguyên từ input
    w: list[float]  # weights: trọng số [GPA, SAT], ví dụ [0.75, 0.25]
    pg: float | None  # position GPA: điểm vị trí GPA 0–100 (đứng trên bao nhiêu % sinh viên)
    conf: Literal[
        "H", "L"
    ]  # confidence: độ tin cậy của pg. H = high (có phân phối C11), L = low (ước lượng từ GPA trung bình hoặc thiếu)
    ps: float | None  # position SAT: điểm vị trí SAT 0–100
    A: float | None  # Academic: điểm học thuật = w[0] × pg + w[1] × ps
    adj: float  # adjustment: điều chỉnh ngoại khóa = (bậc − 3) × L_EC, trong −6..+6
    S: float | None  # Strength: điểm sức mạnh = A + adj
    g: Literal["R", "M", "S", "NA", "O"]  # group: nhóm theo S, trước khi xét du học sinh
    r: float | None  # ratio: tỉ lệ nhận du học sinh / tỉ lệ nhận chung (iacc / acc)
    g2: Literal["R", "RF", "M", "S", "NA", "O"]  # group 2: nhóm cuối sau khi xét r (r < 0.5 thì hạ 1 nhóm)
    F: float | None  # Finance: tài chính = (ngân sách − chi phí) / ngân sách, 0–1
    H: float | None  # (học bổng): học bổng TB du học sinh / chi phí, 0–1
    Q: float | None  # Quality: chất lượng = TB(tỉ lệ giữ chân, tỉ lệ tốt nghiệp), 0–1
    gap: float | None  # số tiền vượt ngân sách (chi phí − ngân sách), null nếu không vượt


class ScoreResponse(BaseModel):
    skill_version: str
    model: str
    items: list[ScoreItem]
    failed: list[str]


def score_schema(codes: list[str]) -> dict[str, Any]:
    """JSON Schema truyền vào tham số format của Ollama (mục 14.4).

    Thứ tự key trong properties = thứ tự model sinh ra, nên buộc model tính lần lượt w → pg → ... → g2.
    """
    num_or_null = {"type": ["number", "null"]}
    n = len(codes)
    return {
        "type": "object",
        "required": ["items"],
        "properties": {
            "items": {
                "type": "array",
                "minItems": n,
                "maxItems": n,
                "items": {
                    "type": "object",
                    "required": [
                        "c",
                        "w",
                        "pg",
                        "conf",
                        "ps",
                        "A",
                        "adj",
                        "S",
                        "g",
                        "r",
                        "g2",
                        "F",
                        "H",
                        "Q",
                        "gap",
                    ],
                    "properties": {
                        "c": {"type": "string", "enum": codes},
                        "w": {"type": "array", "items": {"type": "number"}, "minItems": 2, "maxItems": 2},
                        "pg": num_or_null,
                        "conf": {"enum": ["H", "L"]},
                        "ps": num_or_null,
                        "A": num_or_null,
                        "adj": {"type": "number"},
                        "S": num_or_null,
                        "g": {"enum": ["R", "M", "S", "NA", "O"]},
                        "r": num_or_null,
                        "g2": {"enum": ["R", "RF", "M", "S", "NA", "O"]},
                        "F": num_or_null,
                        "H": num_or_null,
                        "Q": num_or_null,
                        "gap": num_or_null,
                    },
                },
            }
        },
    }
