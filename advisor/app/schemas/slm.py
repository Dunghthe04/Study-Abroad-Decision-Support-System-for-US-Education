"""Hợp đồng đầu ra của SLM (Qwen3 4B) ở lần gọi 1: phân loại câu hỏi và tách thông tin.

Đây là chỗ nối giữa hai phần: bộ phân loại (Dũng) tạo ra SlmResult; handler hồ sơ (Việt) và các nhánh còn lại đọc nó.
Khuôn này trùng với cột đầu ra trong data/slm/du_lieu_slm.xlsx (type = tên sheet, nhan_phu, thuc_the).
Sửa file này thì báo người kia trước, và sửa dữ liệu cho khớp.
"""

from typing import Any, Literal

from pydantic import BaseModel, Field, model_validator

from app.services.centers import SERVICES

# mac_dinh: tin chỉ trả lời câu hồ sơ đang chờ (handler hồ sơ của Việt, không cần skill). Năm loại sau có nhánh riêng
INTENTS = ("mac_dinh", "ho_so", "kich_ban", "kien_thuc", "trung_tam", "ngoai_pham_vi")
Intent = Literal["mac_dinh", "ho_so", "kich_ban", "kien_thuc", "trung_tam", "ngoai_pham_vi"]

# Khóa hồ sơ, phải giống info_to_field.PROFILE_KEYS (có test kiểm).
# Không import vì sẽ kéo cả gói skills và chat_flow theo, chat_flow lại cần file này nên dễ nhập vòng
PROFILE_KEYS = (
    "current_level",
    "current_grade",
    "target_level",
    "major",
    "intake",
    "education_system",
    "gpa",
    "sat",
    "act",
    "english",
    "english_plan",
    "budget",
    "scholarship",
    "funding_source",
    "activities",
    "awards",
    "relatives_state",
    "preferred_states",
    "school_type",
    "visa_refused",
)
# Khóa thực thể riêng của từng loại. Loại nào cũng được kèm thêm khóa hồ sơ khi tin có giá trị hồ sơ
ENTITY_KEYS = {
    "mac_dinh": set(),
    "ho_so": {"truong"},
    "kich_ban": {"che_do", "thay_doi", "truong", "muc_tieu", "yeu_to"},
    "kien_thuc": {"truong"},
    "trung_tam": {"dich_vu", "thanh_pho"},
    "ngoai_pham_vi": {"loai_con"},
}
# Chế độ trong kịch bản
SCENARIO_MODES = ("what_if", "goal_seeking")
# giá trị của loai_con trong ngoài phạm vi
SUBTYPES = ("ngoai_le", "nhay_cam", "tam_biet", "mo_ho")
# thanh_pho không có danh sách cố định: là đoạn chữ nguyên văn trong tin (vd "Sài Gòn", "HN").
# Danh sách thành phố đổi theo trung tâm trong DB, nhánh trung tâm khớp với CenterFinder.cities() lúc chạy
# Tin có hai ý (multi-intent, tối đa 2): ý xếp trước là nhãn chính, ý sau là nhãn phụ, trả lời theo thứ tự đó.
# mac_dinh không làm nhãn phụ (giá trị hồ sơ đã nằm trong thuc_the), ngoai_pham_vi cũng không (câu chào kèm câu hỏi thì bỏ)
PRIORITY = ("ho_so", "kich_ban", "kien_thuc", "trung_tam")
SecondaryIntent = Literal["kich_ban", "kien_thuc", "trung_tam"]


# Các nhãn phụ hợp lệ của một loại: những loại xếp sau nó trong PRIORITY. Loại ngoài PRIORITY không có nhãn phụ
def secondaries(intent: str) -> tuple[str, ...]:
    return PRIORITY[PRIORITY.index(intent) + 1 :] if intent in PRIORITY else ()


# Nhãn phụ phải xếp sau nhãn chính trong PRIORITY. Sai thì ném ValueError
def check_labels(intent: str, sub: str | None) -> None:
    if sub is not None and sub not in secondaries(intent):
        raise ValueError(f"nhan_phu '{sub}' phải xếp sau '{intent}' theo thứ tự {' > '.join(PRIORITY)}")


# Thực thể phải đúng khóa của loại (cộng khóa hồ sơ) và đúng giá trị hệ thống biết. Sai thì ném ValueError
def check_entities(intent: str, sub: str | None, entities: dict[str, Any]) -> None:
    allowed = ENTITY_KEYS[intent] | ENTITY_KEYS.get(sub or "", set()) | set(PROFILE_KEYS)
    unknown = set(entities) - allowed
    if unknown:
        raise ValueError(f"khóa thực thể lạ với loại {intent}: {sorted(unknown)}")
    for key in (set(entities) & set(PROFILE_KEYS)) | ({"thanh_pho"} & set(entities)):
        if not isinstance(entities[key], str) or not entities[key]:
            raise ValueError(f"giá trị '{key}' phải là đoạn chữ trong tin nhắn")
    if entities.get("dich_vu") is not None and entities["dich_vu"] not in SERVICES:
        raise ValueError(f"mã dịch vụ lạ: {entities['dich_vu']}")
    if intent == "ngoai_pham_vi" and entities.get("loai_con") not in SUBTYPES:
        raise ValueError(f"loai_con phải là một trong {SUBTYPES}")
    if intent == "kich_ban":
        if entities.get("che_do") not in SCENARIO_MODES:
            raise ValueError(f"che_do phải là một trong {SCENARIO_MODES}")
        changes = entities.get("thay_doi", [])
        if entities["che_do"] == "what_if" and not changes:
            raise ValueError("what_if cần thay_doi")
        for change in changes:
            if "yeu_to" not in change or not ({"gia_tri", "hanh_dong"} & set(change)):
                raise ValueError("mỗi thay_doi cần yeu_to và gia_tri hoặc hanh_dong")


# Định nghĩa cấu trúc dữ liệu mà SLM cần trả về, ép theo SLM_SCHEMA
class SlmRead(BaseModel):
    type: Intent
    # Ý thứ hai khi tin có hai câu hỏi (vd đánh giá hồ sơ rồi hỏi visa), theo PRIORITY. Thường là None
    nhan_phu: SecondaryIntent | None = None
    # Thông tin tách từ tin nhắn, khóa theo ENTITY_KEYS của loại cộng khóa hồ sơ. Không nhắc tới thì không có khóa
    thuc_the: dict[str, Any] = Field(default_factory=dict)

    @model_validator(mode="after")
    def _check(self) -> "SlmRead":
        check_labels(self.type, self.nhan_phu)
        check_entities(self.type, self.nhan_phu, self.thuc_the)
        return self

    # Giá trị hồ sơ trong tin, nguyên văn đoạn chữ. Handler hồ sơ (Việt) tự chuẩn hóa và kiểm rồi mới lưu
    def profile_values(self) -> dict[str, str]:
        return {key: value for key, value in self.thuc_the.items() if key in PROFILE_KEYS}


# SlmRead cộng độ tin cậy (confidence). Độ tin cậy do code tính (từ xác suất của model), không để SLM tự khai
class SlmResult(SlmRead):
    confidence: float = Field(default=1.0, ge=0, le=1)

    # Tin có cần chuyển qua handler hồ sơ không: trả lời câu đang chờ, hoặc có kèm giá trị hồ sơ
    def touches_profile(self) -> bool:
        return self.type == "mac_dinh" or bool(self.profile_values())


# Chuỗi phải có ít nhất 1 ký tự: chặn model điền "" cho khóa không có thông tin
_TEXT = {"type": "string", "minLength": 1}
# Khuôn của từng khóa thực thể riêng. Khóa hồ sơ dùng chung _TEXT
_ENTITY_SCHEMA = {
    "truong": _TEXT,
    "che_do": {"enum": list(SCENARIO_MODES)},
    "thay_doi": {
        "type": "array",
        "items": {
            "type": "object",
            "additionalProperties": False,
            "required": ["yeu_to"],
            "properties": {"yeu_to": _TEXT, "gia_tri": _TEXT, "hanh_dong": _TEXT},
        },
    },
    "muc_tieu": _TEXT,
    "yeu_to": {"type": "array", "items": _TEXT},
    "dich_vu": {"enum": list(SERVICES)},
    "thanh_pho": _TEXT,
    "loai_con": {"enum": list(SUBTYPES)},
}
# Khóa bắt buộc theo loại, khớp với check_entities
_REQUIRED_KEYS = {"kich_ban": ["che_do"], "ngoai_pham_vi": ["loai_con"]}


# Khuôn cho một cặp (nhãn chính, nhãn phụ): thuc_the chỉ có khóa của hai loại đó cộng khóa hồ sơ
def _branch(intent: str, sub: str | None) -> dict[str, Any]:
    keys = ENTITY_KEYS[intent] | ENTITY_KEYS.get(sub or "", set())
    return {
        "type": "object",
        "required": ["type", "nhan_phu", "thuc_the"],
        "properties": {
            "type": {"const": intent},
            "nhan_phu": {"const": sub},
            "thuc_the": {
                "type": "object",
                "additionalProperties": False,
                "required": _REQUIRED_KEYS.get(intent, []),
                "properties": {
                    **dict.fromkeys(PROFILE_KEYS, _TEXT),
                    **{key: _ENTITY_SCHEMA[key] for key in sorted(keys)},
                },
            },
        },
    }


# Khuôn JSON ép SLM khi sinh (truyền cho Ollama qua tham số format). Mỗi cặp (nhãn chính, nhãn phụ) hợp lệ là một
# nhánh của anyOf: model chọn type nào thì chỉ sinh được nhãn phụ và khóa của nhánh đó. SlmRead vẫn kiểm lại sau
SLM_SCHEMA = {"anyOf": [_branch(intent, sub) for intent in INTENTS for sub in (None, *secondaries(intent))]}
