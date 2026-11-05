"""SLM lần 2: nhận câu hỏi + hồ sơ (SkillRequest), sinh skill riêng cho học sinh, kiểm rồi cất cache.

Dùng chung một SLM với lượt tách ý (SLM lần 1), chỉ khác prompt và lấy chữ Markdown thay vì JSON.
Sai thì gọi lại 1 lần; vẫn sai thì trả None để nhánh tư vấn báo chưa sẵn sàng. Không dùng skill tĩnh thay thế.
"""

import json
from collections import OrderedDict
from typing import Any

from app.schemas.slm import SkillRequest, check_skill, skill_key
from app.services.ollama import OllamaError
from app.services.skill_call import MAX_ATTEMPTS, SKILLS_DIR, load_skill

# Prompt sinh skill của từng loại. Loại chưa có prompt thì chưa sinh được
PROMPTS = {"ho_so": SKILLS_DIR / "slm_sinh_skill_ho_so.md"}
CACHE_SIZE = 256  # số skill giữ trong bộ nhớ; đầy thì bỏ skill dùng lâu nhất


class SkillGenerator:
    def __init__(self, slm: Any, cache_size: int = CACHE_SIZE) -> None:
        self._slm = slm
        # đọc prompt 1 lần lúc khởi động
        self._prompts = {loai: load_skill(path)[0] for loai, path in PROMPTS.items()}
        self._cache: OrderedDict[str, str] = OrderedDict()
        self._cache_size = cache_size

    async def generate(self, req: SkillRequest) -> str | None:
        """Trả skill đã kiểm (Markdown), hoặc None nếu SLM sai cả MAX_ATTEMPTS lần."""
        prompt = self._prompts.get(req.loai)
        if prompt is None:
            raise ValueError(f"chưa có prompt sinh skill cho loại {req.loai}")

        key = skill_key(req)
        if key in self._cache:  # cùng câu hỏi, cùng hồ sơ: dùng lại, không gọi SLM
            self._cache.move_to_end(key)
            return self._cache[key]

        # Đúng 4 khóa như dữ liệu train (export_slm_chat.skill_request), kể cả tinh_san rỗng
        user = {"loai": req.loai, "cau_hoi": req.cau_hoi, "ho_so": req.ho_so, "tinh_san": req.tinh_san}
        messages = [
            {"role": "system", "content": prompt},
            {"role": "user", "content": json.dumps(user, ensure_ascii=False, separators=(",", ":"))},
        ]
        for _ in range(MAX_ATTEMPTS):
            try:
                text = (await self._slm.chat_text(messages)).strip()
                check_skill(text, req)
            except (OllamaError, ValueError):
                continue
            self._remember(key, text)
            return text
        return None

    def _remember(self, key: str, text: str) -> None:
        self._cache[key] = text
        if len(self._cache) > self._cache_size:
            self._cache.popitem(last=False)
