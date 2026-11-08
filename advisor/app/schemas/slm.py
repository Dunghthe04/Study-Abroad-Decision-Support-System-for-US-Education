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
    "mac_dinh": {"nhiem_vu", "tiep_noi"},
    "ho_so": {"truong", "cac_truong", "cac_nhiem_vu", "tiep_noi"},
    "kich_ban": {"che_do", "thay_doi", "truong", "muc_tieu", "yeu_to", "tiep_noi"},
    "kien_thuc": {"truong", "cac_truong", "nhiem_vu", "muc", "bo_loc", "sap_xep", "tiep_noi"},
    "trung_tam": {"dich_vu", "thanh_pho", "nhiem_vu", "cac_trung_tam", "tiep_noi"},
    "ngoai_pham_vi": {"loai_con"},
}
# Câu kiến thức hỏi số liệu một trường ("học phí Harvard bao nhiêu"): SLM trả truong (tên như học sinh gõ), nhiem_vu = tra_cuu và
# muc = các mục hỏi. Hệ thống tra cơ sở dữ liệu theo đúng điều model trả, không đoán lại bằng từ khóa.
# so_sanh: hỏi sự thật của từ hai trường trở lên ("học phí BU và NYU chênh nhau bao nhiêu"): SLM trả cac_truong (tên như học sinh gõ,
# theo thứ tự trong câu) và muc; khác tư vấn hồ sơ "BU hay NYU hợp với em hơn" (loại ho_so, dùng hồ sơ học sinh).
# tim_kiem: tìm trường theo điều kiện, không nêu trường nào ("trường nào ở California có học bổng"): SLM trả bo_loc (các điều kiện học sinh nói,
# điều kiện không nói thì bỏ trống) và sap_xep nếu học sinh nói muốn xếp theo gì ("rẻ nhất", "dễ vào nhất").
# Không dùng hồ sơ học sinh: ai hỏi cùng câu cũng nhận cùng danh sách (khác "gợi ý trường cho em").
# Mã mục khớp InfoField bên .NET (ChatSchoolInfo); "khac" là điều hệ thống không lưu (ký túc xá, việc làm...).
KNOWLEDGE_TASKS = ("tra_cuu", "so_sanh", "tim_kiem")
INFO_FIELDS = (
    "chi_phi",
    "hoc_bong",
    "tieng_anh",
    "diem_thi",
    "gpa",
    "ty_le_nhan",
    "han_nop",
    "tot_nghiep",
    "nganh",
    "xep_hang",
    "vi_tri",
    "website",
    "khac",
)
# Điều kiện lọc trong bo_loc. Giá trị dạng chữ là CHỮ HỌC SINH GÕ ("texas", "30 nghìn đô", "top 50"), code tự khớp và đổi đơn vị.
# Các điều kiện cùng có hiệu lực (trường phải thỏa tất cả). Mỗi khóa nói rõ trường phải thỏa gì:
#   bang (danh sách), thanh_pho, nganh: trường ở bang đó (một trong các bang), ở thành phố đó, có đào tạo ngành đó
#   loai_truong: cong_lap | tu_thuc
#   hoc_bong: co (có học bổng cho du học sinh) | theo_nhu_cau | theo_thanh_tich
#   chinh_sach_thi: khong_bat_buoc (test-optional hoặc không nhận SAT, ACT) | bat_buoc
#   chi_phi_toi_da: tổng chi phí cả năm không vượt mức này
#   ielts_toi_da, toefl_toi_da, duolingo_toi_da: mức tối thiểu trường yêu cầu không vượt mức này (học sinh có điểm đó vẫn nộp được)
#   ty_le_nhan_toi_thieu, ty_le_nhan_toi_da: tỷ lệ nhận từ mức này trở lên (dễ vào) hoặc không quá mức này (chọn lọc)
#   xep_hang_toi_da: có hạng trong các bảng xếp hạng chung từ hạng này trở lên ("top 50")
#   ty_le_tot_nghiep_toi_thieu: tỷ lệ tốt nghiệp trong 6 năm từ mức này trở lên
#   ivy: co (trường Ivy League)
FILTER_TEXT_KEYS = (
    "thanh_pho",
    "nganh",
    "chi_phi_toi_da",
    "ielts_toi_da",
    "toefl_toi_da",
    "duolingo_toi_da",
    "ty_le_nhan_toi_thieu",
    "ty_le_nhan_toi_da",
    "xep_hang_toi_da",
    "ty_le_tot_nghiep_toi_thieu",
)
FILTER_ENUMS = {
    "loai_truong": ("cong_lap", "tu_thuc"),
    "hoc_bong": ("co", "theo_nhu_cau", "theo_thanh_tich"),
    "chinh_sach_thi": ("khong_bat_buoc", "bat_buoc"),
    "ivy": ("co",),
}
FILTER_KEYS = ("bang", *FILTER_TEXT_KEYS, *FILTER_ENUMS)
# Cách xếp danh sách học sinh muốn; không nói thì xếp theo xếp hạng (điểm chất lượng của trường)
SORTS = ("xep_hang", "chi_phi_thap", "de_vao", "chon_loc")
# Chế độ trong kịch bản
SCENARIO_MODES = ("what_if", "goal_seeking")
# giá trị của loai_con trong ngoài phạm vi. ve_he_thong: hỏi về chính hệ thống ("bạn là ai", "bạn làm được gì");
# gap_nguoi: muốn nói chuyện với người thật
SUBTYPES = ("ngoai_le", "nhay_cam", "tam_biet", "mo_ho", "ve_he_thong", "gap_nguoi")
# Nhiệm vụ của ý nhập hồ sơ (mac_dinh), khóa nhiem_vu: tra_loi (mặc định, trả lời câu hệ thống đang hỏi), sua (sửa một mục đã nhập),
# xem_ho_so (hỏi đã nhập gì, còn thiếu gì), lam_lai (nhập lại từ đầu)
PROFILE_TASKS = ("tra_loi", "sua", "xem_ho_so", "lam_lai")
# Nhiệm vụ của ý tư vấn hồ sơ (ho_so), khóa cac_nhiem_vu là danh sách vì một câu có thể hỏi nhiều việc ("đánh giá hồ sơ và cho em lộ trình").
# Vắng hoặc rỗng = chưa rõ muốn gì, hệ thống hỏi lại bằng nút. so_sanh cần cac_truong; giai_thich_truong có truong (hoặc lấy từ ngữ cảnh)
REVIEW_TASKS = ("danh_gia", "chon_truong", "lo_trinh", "so_sanh", "giai_thich_truong")
# Nhiệm vụ của ý trung tâm (trung_tam), khóa nhiem_vu: tim (mặc định), dat_lich, so_sanh (cần cac_trung_tam, từ hai tên trở lên)
CENTER_TASKS = ("tim", "dat_lich", "so_sanh")
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
    task = entities.get("nhiem_vu")
    task_values = {"kien_thuc": KNOWLEDGE_TASKS, "mac_dinh": PROFILE_TASKS, "trung_tam": CENTER_TASKS}.get(
        intent, ()
    )
    if task is not None and task not in task_values:
        raise ValueError(f"nhiem_vu của loại {intent} phải là một trong {task_values}")
    # tiep_noi: câu thiếu thông tin và lấy phần thiếu từ ngữ cảnh (dang_noi_ve trong đầu vào). false hoặc vắng = câu tự đứng được
    if "tiep_noi" in entities and not isinstance(entities["tiep_noi"], bool):
        raise ValueError("tiep_noi phải là true hoặc false")
    bo_loc = entities.get("bo_loc")
    if bo_loc is not None:
        _check_filters(bo_loc)
    if entities.get("sap_xep") is not None and entities["sap_xep"] not in SORTS:
        raise ValueError(f"sap_xep phải là một trong {SORTS}")
    # Câu tiếp nối chỉ đổi cách xếp ("rẻ nhất đi") không có điều kiện mới: điều kiện lấy từ ngữ cảnh
    if (
        entities.get("nhiem_vu") == "tim_kiem"
        and not bo_loc
        and not (entities.get("tiep_noi") is True and entities.get("sap_xep") is not None)
    ):
        raise ValueError("tim_kiem cần bo_loc có ít nhất một điều kiện")
    if (bo_loc is not None or entities.get("sap_xep") is not None) and entities.get("nhiem_vu") != "tim_kiem":
        raise ValueError("bo_loc và sap_xep chỉ đi với nhiem_vu = tim_kiem")
    cac_truong = entities.get("cac_truong")
    if cac_truong is not None and not (
        isinstance(cac_truong, list)
        and len(cac_truong) >= 2
        and all(isinstance(t, str) and t.strip() for t in cac_truong)
    ):
        raise ValueError("cac_truong phải là danh sách từ hai tên trường trở lên")
    if intent == "kien_thuc" and task == "so_sanh" and cac_truong is None:
        raise ValueError("so_sanh cần cac_truong")
    _check_task_entities(intent, entities, task, cac_truong)
    muc = entities.get("muc")
    if muc is not None and not (isinstance(muc, list) and muc and all(m in INFO_FIELDS for m in muc)):
        raise ValueError(f"muc phải là danh sách mã trong {INFO_FIELDS}")
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


# Quy tắc nhiệm vụ của nhập hồ sơ, tư vấn hồ sơ và trung tâm. Sai thì ném ValueError
def _check_task_entities(intent: str, entities: dict[str, Any], task: Any, cac_truong: Any) -> None:
    if intent == "mac_dinh":
        has_profile = bool(set(entities) & set(PROFILE_KEYS))
        if task == "sua" and not has_profile:
            raise ValueError("sua cần ít nhất một mục hồ sơ (sửa mục nào)")
        if task in ("xem_ho_so", "lam_lai") and has_profile:
            raise ValueError(f"{task} không kèm giá trị hồ sơ")
    elif intent == "ho_so":
        tasks = entities.get("cac_nhiem_vu")
        if tasks is not None and not (
            isinstance(tasks, list)
            and len(set(tasks)) == len(tasks)
            and all(t in REVIEW_TASKS for t in tasks)
        ):
            raise ValueError(f"cac_nhiem_vu phải là danh sách không lặp các mã trong {REVIEW_TASKS}")
        if tasks and "so_sanh" in tasks and cac_truong is None:
            raise ValueError("so_sanh cần cac_truong")
    elif intent == "trung_tam":
        centers = entities.get("cac_trung_tam")
        if centers is not None and not (
            isinstance(centers, list)
            and len(centers) >= 2
            and all(isinstance(c, str) and c.strip() for c in centers)
        ):
            raise ValueError("cac_trung_tam phải là danh sách từ hai tên trung tâm trở lên")
        if task == "so_sanh" and centers is None:
            raise ValueError("so_sanh cần cac_trung_tam")
        if centers is not None and task != "so_sanh":
            raise ValueError("cac_trung_tam chỉ đi với nhiem_vu = so_sanh")


# bo_loc: object có ít nhất một điều kiện, đúng khóa và đúng giá trị. Sai thì ném ValueError
def _check_filters(bo_loc: Any) -> None:
    if not isinstance(bo_loc, dict) or not bo_loc:
        raise ValueError("bo_loc phải là object có ít nhất một điều kiện")
    unknown = set(bo_loc) - set(FILTER_KEYS)
    if unknown:
        raise ValueError(f"điều kiện lọc lạ: {sorted(unknown)}")
    bang = bo_loc.get("bang")
    if bang is not None and not (
        isinstance(bang, list) and bang and all(isinstance(b, str) and b.strip() for b in bang)
    ):
        raise ValueError("bang phải là danh sách tên bang")
    for key in FILTER_TEXT_KEYS:
        if key in bo_loc and not (isinstance(bo_loc[key], str) and bo_loc[key].strip()):
            raise ValueError(f"{key} phải là đoạn chữ học sinh gõ")
    for key, allowed in FILTER_ENUMS.items():
        if key in bo_loc and bo_loc[key] not in allowed:
            raise ValueError(f"{key} phải là một trong {allowed}")


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
        out[key] = (
            out[key] + [x for x in value if x not in out[key]]
            if isinstance(value, list) and isinstance(out.get(key), list)
            else value
        )
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
    # Khuôn 7 mục: không còn mục "Đề xuất trường" vì danh sách trường do hệ thống tính riêng (skill chỉ đánh giá hồ sơ và lộ trình)
    "ho_so": (
        "0. Câu hỏi kích hoạt",
        "1. Nhiệm vụ",
        "2. Dữ liệu dùng để tính",
        "3. Cách hiểu hoạt động và giải thưởng",
        "4. Góc nhìn về hồ sơ",
        "5. Lộ trình",
        "6. Yêu cầu câu trả lời",
    ),
    "kich_ban": (
        "0. Câu hỏi kích hoạt",
        "1. Nhiệm vụ",
        "2. Hồ sơ gốc",
        "3. Cách làm",
        "4. Yêu cầu câu trả lời",
    ),
}
# Khuôn cũ 8 mục (có "5. Đề xuất trường"). Vẫn nhận trong lúc chuyển sang khuôn 7 mục, vì model usas-slm-v2 đang chạy và dữ liệu cũ
# còn viết theo khuôn này. Bỏ khi model huấn luyện lại bằng khuôn 7 mục đã chạy thật
LEGACY_SKILL_SECTIONS = {
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


# Kiểm skill SLM sinh ra (văn bản .md). Sai thì ném ValueError để bộ sinh gọi lại; vẫn sai thì báo chưa có skill (không dùng skill tĩnh).
# Chỉ kiểm cấu trúc và việc không bỏ số của hồ sơ và dòng tính sẵn. Số suy ra (GPA thang 4, hạn nộp) và dòng Mốc sẽ kiểm bằng luật riêng
def check_skill(text: str, req: SkillRequest) -> None:
    layouts = [SKILL_SECTIONS[req.loai]]  # khuôn chuẩn đứng đầu, khuôn cũ (nếu còn nhận) đứng sau
    if req.loai in LEGACY_SKILL_SECTIONS:
        layouts.append(LEGACY_SKILL_SECTIONS[req.loai])
    parts = re.split(
        r"^## (.+?)\s*$", text, flags=re.M
    )  # [chữ trước, tên mục, nội dung, tên mục, nội dung, ...]
    muc = dict(zip(parts[1::2], parts[2::2], strict=True))
    wanted = next(
        (layout for layout in layouts if tuple(name for name in parts[1::2] if name in layout) == layout),
        None,
    )
    if wanted is None:
        raise ValueError(f"skill {req.loai} phải có đủ các mục theo thứ tự: {', '.join(layouts[0])}")
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
    "cac_truong": {"type": "array", "minItems": 2, "items": _TEXT},
    "bo_loc": {
        "type": "object",
        "additionalProperties": False,
        "minProperties": 1,
        "properties": {
            "bang": {"type": "array", "minItems": 1, "items": _TEXT},
            **dict.fromkeys(FILTER_TEXT_KEYS, _TEXT),
            **{key: {"enum": list(values)} for key, values in FILTER_ENUMS.items()},
        },
    },
    "sap_xep": {"enum": list(SORTS)},
    "tiep_noi": {"type": "boolean"},
    "nhiem_vu": {
        "enum": list(KNOWLEDGE_TASKS)
    },  # mặc định cho kien_thuc; mac_dinh và trung_tam đổi ở _entity_schema
    "cac_nhiem_vu": {"type": "array", "uniqueItems": True, "items": {"enum": list(REVIEW_TASKS)}},
    "cac_trung_tam": {"type": "array", "minItems": 2, "items": _TEXT},
    "muc": {"type": "array", "minItems": 1, "items": {"enum": list(INFO_FIELDS)}},
}
# nhiem_vu có tập giá trị riêng theo loại
_TASK_ENUMS = {"kien_thuc": KNOWLEDGE_TASKS, "mac_dinh": PROFILE_TASKS, "trung_tam": CENTER_TASKS}


def _entity_schema(intent: str, key: str) -> dict[str, Any]:
    return {"enum": list(_TASK_ENUMS[intent])} if key == "nhiem_vu" else _ENTITY_SCHEMA[key]


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
                    **{key: _entity_schema(intent, key) for key in sorted(ENTITY_KEYS[intent])},
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
