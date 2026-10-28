"""Nạp model chọn skill lúc khởi động. Không nạp được (chưa train, thiếu torch, thiếu thư mục ml) thì trả None."""

import logging

import httpx

logger = logging.getLogger("advisor.chat")


# ml/ là phần học máy, nằm ngoài app/ nên chỉ import khi cần; lỗi gì cũng không được làm sập cả dịch vụ
def load_skill_picker(client: httpx.AsyncClient):
    try:
        from ml.skill_picker.predict import OllamaEmbedder, SkillPicker

        picker = SkillPicker.load(OllamaEmbedder(client))
        logger.info("Đã nạp model chọn skill")
        return picker
    except Exception as ex:
        logger.warning("Chưa nạp được model chọn skill (%s), chat dùng skill của bước hiện tại", ex)
        return None
