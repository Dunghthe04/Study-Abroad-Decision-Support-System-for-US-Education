"""Hợp đồng đầu ra của SLM (Qwen3 4B): lần gọi 1 tách tin thành danh sách ý, lần gọi 2 sinh skill cá nhân hóa.

Đây là chỗ nối giữa hai phần: bộ phân loại (Dũng) tạo ra SlmTinResult; handler hồ sơ (Việt) và các nhánh còn lại đọc nó.
Khuôn một ý trùng với phần tử của cột cac_y trong data/slm/du_lieu_slm.xlsx (type, doan, thuc_the).
Sửa file này thì báo người kia trước, và sửa dữ liệu cho khớp.
"""

import hashlib
import json
import re
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
    "standardized_plan",
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
# Trần kỹ thuật cho số ý của một tin, để model sinh lặp vô hạn không làm hệ thống chạy mãi. Không phải giới hạn
# sản phẩm: tin có bao nhiêu câu hỏi thì trả lời bấy nhiêu. Vượt trần thì các ý sau bị cắt
MAX_RAW_Y = 20


# Thực thể phải đúng khóa của loại (cộng khóa hồ sơ) và đúng giá trị hệ thống biết. Sai thì ném ValueError
def check_entities(intent: str, entities: dict[str, Any]) -> None:
    allowed = ENTITY_KEYS[intent] | set(PROFILE_KEYS)
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


# Một ý trong tin nhắn. Tin có nhiều ý thì SLM trả một danh sách SlmY, mỗi ý có đoạn chữ và thông tin riêng.
# doan: kien_thuc và trung_tam được viết lại thành câu hỏi đầy đủ ("I-20 và SAT là gì" tách thành hai ý);
# mac_dinh phải nguyên văn (kiểm ở bộ phân loại vì cần tin gốc)
class SlmY(BaseModel):
    type: Intent
    doan: str = Field(min_length=1)
    thuc_the: dict[str, Any] = Field(default_factory=dict)

    @model_validator(mode="after")
    def _check(self) -> "SlmY":
        check_entities(self.type, self.thuc_the)
        return self

    def profile_values(self) -> dict[str, str]:
        return {key: value for key, value in self.thuc_the.items() if key in PROFILE_KEYS}


# Ba loại này chỉ chạy một lần mỗi tin: SLM trả nhiều ý cùng loại thì gộp thành một
ONCE = ("mac_dinh", "ho_so", "kich_ban")


# Gộp thông tin của hai ý cùng loại: danh sách thì nối thêm (thay_doi, yeu_to), còn lại giá trị sau thay giá trị trước
def _gop_thuc_the(a: dict[str, Any], b: dict[str, Any]) -> dict[str, Any]:
    out = dict(a)
    for key, value in b.items():
        out[key] = out[key] + value if isinstance(value, list) and isinstance(out.get(key), list) else value
    return out


# Đưa danh sách ý SLM trả về về dạng hệ thống chạy được
def chuan_hoa_y(cac_y: list[SlmY]) -> list[SlmY]:
    # 1. Ngoài phạm vi: có ý khác thì bỏ hết ý ngoài phạm vi (chào kèm câu hỏi), không thì chỉ giữ ý đầu
    khac = [y for y in cac_y if y.type != "ngoai_pham_vi"]
    cac_y = khac or cac_y[:1]
    # 2. Gộp các ts cùng loại thuộc ONCE
    gop: list[SlmY] = []
    for y in cac_y:
        # Tìm xem trong gop có ý nào cùng loại y chưa
        truoc = next((g for g in gop if g.type == y.type and y.type in ONCE), None)
        if truoc is None:
            gop.append(y)
        else:
            gop[gop.index(truoc)] = SlmY(
                type=y.type,
                doan=f"{truoc.doan}, {y.doan}",
                thuc_the=_gop_thuc_the(truoc.thuc_the, y.thuc_the),
            )
    # 3. Giữ thứ tự học sinh hỏi; chỉ mac_dinh lên đầu vì ghi hồ sơ phải xong trước các ý khác
    gop = [y for y in gop if y.type == "mac_dinh"] + [y for y in gop if y.type != "mac_dinh"]
    # 4. Không giới hạn số ý trả lời; chỉ cắt khi vượt trần kỹ thuật (mac_dinh đứng đầu nên không bao giờ bị cắt)
    return gop[:MAX_RAW_Y]


# Cả tin nhắn: các ý cần trả lời (đã qua chuan_hoa_y). Luật dưới đây là lưới an toàn, sai thì ném ValueError
class SlmTin(BaseModel):
    cac_y: list[SlmY] = Field(min_length=1)

    @model_validator(mode="after")
    def _check(self) -> "SlmTin":
        loai = [y.type for y in self.cac_y]
        if "ngoai_pham_vi" in loai and len(loai) > 1:
            raise ValueError("ngoai_pham_vi phải đứng một mình")
        for t in ONCE:
            if loai.count(t) > 1:
                raise ValueError(f"loại '{t}' chỉ được một ý, hãy gộp lại")
        if "mac_dinh" in loai and loai[0] != "mac_dinh":
            raise ValueError("mac_dinh phải đứng đầu")
        if len(loai) > MAX_RAW_Y:
            raise ValueError(f"quá {MAX_RAW_Y} ý trong một tin")
        return self


# SlmTin cộng độ tin cậy (confidence). Độ tin cậy do code tính (từ xác suất của model), không để SLM tự khai.
# Đây là kết quả bộ phân loại trả cho ChatFlow và các nhánh
class SlmTinResult(SlmTin):
    confidence: float = Field(default=1.0, ge=0, le=1)

    # Dựng từ JSON SLM trả ({"cac_y": [...]}): kiểm từng ý, dọn (gộp, sắp xếp, cắt ý quá trần kỹ thuật) rồi kiểm cả tin.
    # JSON sai thì ném ValueError (ValidationError của pydantic cũng là ValueError)
    @classmethod
    def from_raw(cls, raw: dict[str, Any], confidence: float = 1.0) -> "SlmTinResult":
        items = raw.get("cac_y")
        if not isinstance(items, list):
            raise ValueError("thiếu danh sách cac_y")
        cac_y = chuan_hoa_y([SlmY.model_validate(item) for item in items])
        return cls(cac_y=cac_y, confidence=confidence)

    # Các loại của những ý trả lời ngay, theo thứ tự (để ghi log)
    def types(self) -> list[str]:
        return [y.type for y in self.cac_y]

    # Giá trị hồ sơ trong cả tin, gom từ mọi ý (ý loại nào cũng có thể kèm giá trị hồ sơ). Giá trị sau thay giá trị trước.
    # Handler hồ sơ (Việt) tự chuẩn hóa và kiểm rồi mới lưu
    def profile_values(self) -> dict[str, str]:
        values: dict[str, str] = {}
        for y in self.cac_y:
            values.update(y.profile_values())
        return values

    # Tin có cần chuyển qua handler hồ sơ không: có ý mac_dinh (trả lời câu đang chờ), hoặc có kèm giá trị hồ sơ
    def touches_profile(self) -> bool:
        return "mac_dinh" in self.types() or bool(self.profile_values())


# ---- SLM lần 2: sinh skill cá nhân hóa ----
# Chỉ hai loại cần hồ sơ mới có skill riêng của học sinh; các loại còn lại dùng skill chung
SKILL_KINDS = ("ho_so", "kich_ban")
# Các mục bắt buộc của một skill, đúng thứ tự. Mỗi mục là một dòng "## TÊN MỤC" rồi nội dung
SKILL_SECTIONS = {
    "ho_so": (
        "0. Câu hỏi kích hoạt",
        "1. Nhiệm vụ",
        "2. Dữ liệu dùng để tính",
        "3. Cách hiểu hoạt động và giải thưởng",
        "4. Góc nhìn về hồ sơ",
        "5. Đề xuất trường",
        "6. Lộ trình",
        "7. Yêu cầu câu trả lời",
    ),
    "kich_ban": (
        "0. Câu hỏi kích hoạt",
        "1. Nhiệm vụ",
        "2. Hồ sơ gốc",
        "3. Cách làm",
        "4. Yêu cầu câu trả lời",
    ),
}


# Dòng code tính sẵn cho skill ho_so (profile_calc.calc_profile): mức GPA / tiếng Anh so với mốc dữ liệu trường,
# điểm TB từng năm, mốc thời gian... Tách khỏi ho_so để SLM phân biệt dữ kiện học sinh khai với phần code so sánh;
# không nằm trong PROFILE_KEYS vì SLM lần 1 không được tách ra các khóa này từ câu chat
CALC_KEYS = (
    "grade_by_year",
    "gpa_level",
    "english_level",
    "sat_level",
    "budget_level",
    "ec_level",
    "scholarship_chance",
    "timeline",
)


# Đầu vào của SLM lần 2: câu hỏi và hồ sơ đã đủ (giá trị đã được nhánh hồ sơ chuẩn hóa và kiểm)
class SkillRequest(BaseModel):
    loai: Literal["ho_so", "kich_ban"]
    cau_hoi: str = Field(min_length=1)
    ho_so: dict[str, str]
    # Chỉ ho_so: các dòng code tính sẵn (khóa trong CALC_KEYS); kịch bản đổi hồ sơ nên các dòng này không còn đúng
    tinh_san: dict[str, str] = Field(default_factory=dict)
    # Chỉ kịch bản: giá trị muốn thử (áp lên bản sao hồ sơ) hoặc mục tiêu cần đạt, lấy từ thuc_the của SLM lần 1
    thay_doi: list[dict[str, str]] = Field(default_factory=list)
    muc_tieu: str | None = None

    @model_validator(mode="after")
    def _check(self) -> "SkillRequest":
        if not self.ho_so:
            raise ValueError("hồ sơ rỗng, chưa đủ để sinh skill")
        unknown = set(self.ho_so) - set(PROFILE_KEYS)
        if unknown:
            raise ValueError(f"khóa hồ sơ lạ: {sorted(unknown)}")
        unknown_calc = set(self.tinh_san) - set(CALC_KEYS)
        if unknown_calc:
            raise ValueError(f"khóa tính sẵn lạ: {sorted(unknown_calc)}")
        if self.tinh_san and self.loai != "ho_so":
            raise ValueError("chỉ skill ho_so có tinh_san")
        if self.loai == "kich_ban" and not (self.thay_doi or self.muc_tieu):
            raise ValueError("kich_ban cần thay_doi hoặc muc_tieu")
        return self


# Khóa lưu skill: cùng loại, câu hỏi, hồ sơ (và thay đổi) thì dùng lại skill đã sinh; khác một giá trị là skill khác
def skill_key(req: SkillRequest) -> str:
    raw = json.dumps(req.model_dump(), ensure_ascii=False, sort_keys=True)
    return hashlib.sha256(raw.encode("utf-8")).hexdigest()[:16]


_SO = re.compile(r"\d+(?:[.,]\d+)*")


# Các con số trong một đoạn chữ ("4.000" và "4000" là một số, "6.0" và "6" là một số;
# bỏ số thứ tự đầu dòng "1. ", "2) ")
def _so_trong(text: str) -> set[str]:
    out = set()
    for n in _SO.findall(re.sub(r"^\s*\d+[.)]\s+", "", text, flags=re.M)):
        n = n.replace(",", ".")
        n = n.replace(".", "") if re.fullmatch(r"\d{1,3}(\.\d{3})+", n) else n
        out.add(re.sub(r"\.0+$", "", n))
    return out


# Kiểm skill SLM sinh ra (văn bản .md). Sai thì ném ValueError để bộ gọi dùng skill mặc định của loại.
# Chỉ kiểm cấu trúc và việc không bỏ số của hồ sơ và dòng tính sẵn. Số suy ra (GPA thang 4, hạn nộp) và dòng Mốc sẽ kiểm bằng luật riêng
def check_skill(text: str, req: SkillRequest) -> None:
    wanted = SKILL_SECTIONS[req.loai]
    parts = re.split(
        r"^## (.+?)\s*$", text, flags=re.M
    )  # [chữ trước, tên mục, nội dung, tên mục, nội dung, ...]
    muc = dict(zip(parts[1::2], parts[2::2], strict=True))
    if tuple(name for name in parts[1::2] if name in wanted) != wanted:
        raise ValueError(f"skill {req.loai} phải có đủ các mục theo thứ tự: {', '.join(wanted)}")
    empty = [name for name in wanted if not muc[name].strip()]
    if empty:
        raise ValueError(f"mục rỗng: {empty}")
    dropped = _so_trong(" ".join([*req.ho_so.values(), *req.tinh_san.values()])) - _so_trong(text)
    if dropped:
        raise ValueError(f"skill bỏ mất số của hồ sơ: {sorted(dropped)}")


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
            "properties": {"yeu_to": _TEXT, "gia_tri": _TEXT, "don_vi": _TEXT, "hanh_dong": _TEXT},
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


# Khuôn cho một ý của loại intent: thuc_the chỉ có khóa của loại đó cộng khóa hồ sơ
def _y_branch(intent: str) -> dict[str, Any]:
    return {
        "type": "object",
        "additionalProperties": False,
        "required": ["type", "doan", "thuc_the"],
        "properties": {
            "type": {"const": intent},
            "doan": _TEXT,
            "thuc_the": {
                "type": "object",
                "additionalProperties": False,
                "required": _REQUIRED_KEYS.get(intent, []),
                "properties": {
                    **dict.fromkeys(PROFILE_KEYS, _TEXT),
                    **{key: _ENTITY_SCHEMA[key] for key in sorted(ENTITY_KEYS[intent])},
                },
            },
        },
    }


# Khuôn JSON ép SLM khi sinh một tin có nhiều ý: {"cac_y": [ý, ý, ...]}. Mỗi ý là một nhánh của anyOf theo type,
# model chọn type nào thì chỉ sinh được khóa của loại đó. Chỉ dùng khi slm_format = "schema" (model chưa fine-tune);
# model đã fine-tune dùng format "json" vì Ollama xếp khóa theo bảng chữ cái, lệch thứ tự lúc train
SLM_TIN_SCHEMA = {
    "type": "object",
    "additionalProperties": False,
    "required": ["cac_y"],
    "properties": {
        "cac_y": {
            "type": "array",
            "minItems": 1,
            "maxItems": MAX_RAW_Y,
            "items": {"anyOf": [_y_branch(intent) for intent in INTENTS]},
        }
    },
}
