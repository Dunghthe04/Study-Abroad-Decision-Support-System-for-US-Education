"""SLM lần 2: nhận câu hỏi + hồ sơ (SkillRequest), sinh skill riêng cho học sinh, kiểm rồi cất cache.

Dùng chung một SLM với lượt tách ý (SLM lần 1), chỉ khác prompt và lấy chữ Markdown thay vì JSON.
Sai thì gọi lại 1 lần; vẫn sai thì trả None để nhánh tư vấn báo chưa sẵn sàng. Không dùng skill tĩnh thay thế.
Có log_dir thì mỗi lần sinh (không tính lần lấy từ cache) ghi một dòng JSON vào log_dir/skill_<ngày>.jsonl:
đầu vào, skill cuối cùng, chữ SLM trả từng lần kèm lý do bị loại và số giây, để người làm đọc lại skill thật.
"""

import datetime
import json
import logging
import time
from collections import OrderedDict
from pathlib import Path
from typing import Any

from app.schemas.slm import SkillRequest, check_skill, skill_key
from app.services.ollama import OllamaError
from app.services.skill_call import MAX_ATTEMPTS, SKILLS_DIR, load_skill

# Prompt sinh skill của từng loại. Loại chưa có prompt thì chưa sinh được
PROMPTS = {"ho_so": SKILLS_DIR / "slm_sinh_skill_ho_so.md"}
CACHE_SIZE = 256  # số skill giữ trong bộ nhớ; đầy thì bỏ skill dùng lâu nhất

logger = logging.getLogger(__name__)


class SkillGenerator:
    def __init__(self, slm: Any, cache_size: int = CACHE_SIZE, log_dir: Path | None = None) -> None:
        self._slm = slm
        self._log_dir = log_dir  # None = không ghi nhật ký skill
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
        attempts: list[dict[str, Any]] = []  # từng lần gọi: chữ SLM trả, lý do bị loại (None = qua), số giây
        result = None
        for _ in range(MAX_ATTEMPTS):
            start = time.perf_counter()
            text = None
            try:
                text = (await self._slm.chat_text(messages)).strip()
                check_skill(text, req)
            except OllamaError as e:
                error = f"gọi model: {e or type(e).__name__}"
            except ValueError as e:
                error = str(e)
            else:
                error = None
            attempts.append({"chu_slm": text, "loi": error, "giay": round(time.perf_counter() - start, 2)})
            if error is None and text is not None:
                self._remember(key, text)
                result = text
                break
        self._log(key, req, result, attempts)
        return result

    # Ghi nhật ký một lần sinh skill. Lỗi ghi file chỉ log cảnh báo, không làm hỏng câu trả lời cho học sinh
    def _log(self, key: str, req: SkillRequest, skill: str | None, attempts: list[dict[str, Any]]) -> None:
        if self._log_dir is None:
            return
        now = datetime.datetime.now()
        row = {
            "thoi_gian": now.isoformat(timespec="seconds"),
            "skill_key": key,
            "loai": req.loai,
            "cau_hoi": req.cau_hoi,
            "ho_so": req.ho_so,
            "tinh_san": req.tinh_san,
            "skill": skill,
            "tung_lan": attempts,
        }
        try:
            self._log_dir.mkdir(parents=True, exist_ok=True)
            path = self._log_dir / f"skill_{now:%Y-%m-%d}.jsonl"
            with path.open("a", encoding="utf-8") as f:
                f.write(json.dumps(row, ensure_ascii=False) + "\n")
        except OSError:
            logger.warning("Không ghi được nhật ký skill vào %s", self._log_dir, exc_info=True)

    def _remember(self, key: str, text: str) -> None:
        self._cache[key] = text
        if len(self._cache) > self._cache_size:
            self._cache.popitem(last=False)
