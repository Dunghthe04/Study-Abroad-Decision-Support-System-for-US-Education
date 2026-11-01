"""Chỗ đăng ký các skill chạy trong chat và lớp xử lý tương ứng.

Mỗi skill là một lớp có hàm `async run(state, message) -> SkillOutput` (hợp đồng SkillHandler trong chat_flow.py).
Thêm một skill mới:
  1. Viết lớp skill trong một file riêng ở thư mục này.
  2. Import lớp đó ở đầu file này.
  3. Thêm MỘT dòng vào build_handlers.
Khóa của dict là tên skill, đúng nhãn của model chọn skill (skill_nhu_cau, skill_bac_hoc, ...).
Không đăng ký ngoai_le và ket_thuc: hai skill tạm đó đã có sẵn trong ChatFlow.
"""

from typing import Any

from app.services.centers import CenterFinder
from app.services.chat_flow import SkillHandler
from app.services.knowledge import KnowledgeRetriever
from app.services.skills.skill_bac_hoc import BacHocHandler
from app.services.skills.skill_chuyen_trung_tam import ChuyenTrungTamHandler
from app.services.skills.skill_danh_gia import DanhGiaHandler
from app.services.skills.skill_explain import ExplainHandler
from app.services.skills.skill_ho_so import HoSoHandler
from app.services.skills.skill_lo_trinh import LoTrinhHandler
from app.services.skills.skill_nhu_cau import NhuCauHandler
from app.services.skills.skill_tra_loi import TraLoiHandler


# llm: OllamaClient (Qwen) dùng chung cho các skill; model: tên model, truyền vào SkillCall hoặc để ghi log
# knowledge: bộ tìm kiến thức; None (chưa có DB) thì không đăng ký skill_tra_loi
def build_handlers(
    llm: Any,
    model: str,
    knowledge: KnowledgeRetriever | None = None,
    min_score: float = 0.72,
    centers: CenterFinder | None = None,
) -> dict[str, SkillHandler]:
    handlers: dict[str, SkillHandler] = {}
    # Mỗi skill một dòng, ví dụ:
    # handlers["skill_nhu_cau"] = NhuCauHandler(llm, model)
    handlers["skill_nhu_cau"] = NhuCauHandler(llm, model)
    handlers["skill_bac_hoc"] = BacHocHandler(llm, model)
    handlers["skill_ho_so"] = HoSoHandler(llm, model)
    handlers["skill_danh_gia"] = DanhGiaHandler(llm, model)
    handlers["skill_lo_trinh"] = LoTrinhHandler(llm, model)
    handlers["skill_explain"] = ExplainHandler(llm, model)
    if knowledge is not None:
        handlers["skill_tra_loi"] = TraLoiHandler(llm, model, knowledge, min_score)
    if centers is not None:
        handlers["skill_chuyen_trung_tam"] = ChuyenTrungTamHandler(llm, model, centers)
    return handlers
