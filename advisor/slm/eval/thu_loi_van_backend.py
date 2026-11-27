"""Cho Claude thật viết 5 loại lời văn mà backend nhờ advisor viết, bằng dữ liệu mẫu có sẵn trong test của từng bộ viết.

Chỉ kiểm LỜI VĂN (đúng trọng tâm, dài ngắn, chữ lạ), không kiểm số liệu backend tính. Gọi Claude nên tốn tiền API.
Chạy trong thư mục advisor:  .venv/Scripts/python.exe -m slm.eval.thu_loi_van_backend
"""

import asyncio
import json
import sys

import httpx

from app.api.routes.recommend import (
    CompareNoteRequest,
    ExplainRequest,
    GoalNoteRequest,
    RecommendNoteRequest,
    ScenarioNoteRequest,
)
from app.core.config import get_settings
from app.services import usage_meter
from app.services.claude import ClaudeClient
from app.services.compare_writer import CompareWriter
from app.services.explainer import Explainer
from app.services.goal_writer import GoalWriter
from app.services.recommend_writer import RecommendWriter
from app.services.scenario_writer import ScenarioWriter
from tests import (
    test_compare_writer,
    test_explainer,
    test_goal_writer,
    test_recommend_writer,
    test_scenario_writer,
)


async def main() -> None:
    s = get_settings()
    if not s.anthropic_api_key:
        raise SystemExit("Chưa có ANTHROPIC_API_KEY trong advisor/.env")
    cases = [
        ("so sánh trường", CompareWriter, "write", CompareNoteRequest, test_compare_writer.REQUEST),
        ("gợi ý danh sách", RecommendWriter, "write", RecommendNoteRequest, test_recommend_writer.REQUEST),
        ("cần gì để vào trường", GoalWriter, "write", GoalNoteRequest, test_goal_writer.REQUEST),
        ("thử giả định", ScenarioWriter, "write", ScenarioNoteRequest, test_scenario_writer.REQUEST),
        (
            "thử giả định không đổi gì",
            ScenarioWriter,
            "write",
            ScenarioNoteRequest,
            test_scenario_writer.NONE_REQUEST,
        ),
        ("giải thích trường", Explainer, "explain", ExplainRequest, test_explainer.COLGATE),
        ("giải thích điểm phù hợp", Explainer, "explain", ExplainRequest, test_explainer.FIT),
    ]
    total_in = total_out = 0
    async with httpx.AsyncClient(base_url=s.anthropic_base_url, timeout=s.anthropic_timeout_seconds) as http:
        claude = ClaudeClient(http, s.anthropic_model, s.anthropic_api_key, max_retries=2)
        for name, writer, method, model, payload in cases:
            print(f"\n######## {name}")
            try:
                request = model.model_validate(payload)
            except ValueError as ex:
                print("  (dữ liệu mẫu không đủ để dựng yêu cầu)", str(ex).splitlines()[:3])
                continue
            calls = usage_meter.start()
            try:
                response = await getattr(writer(claude, s.anthropic_model), method)(request)
            except Exception as ex:
                print("  LỖI:", repr(ex)[:300])
                continue
            total_in += sum(c.input_tokens for c in calls if c.source == "claude")
            total_out += sum(c.output_tokens for c in calls if c.source == "claude")
            data = response.model_dump() if hasattr(response, "model_dump") else response
            print(f"  số lần gọi: {len(calls)}")
            for k, v in data.items():
                if k in ("skill_version", "model") or v in (None, "", [], {}):
                    continue
                text = v if isinstance(v, str) else json.dumps(v, ensure_ascii=False)
                print(f"  {k} ({len(text.split())} từ): {text[:900]}")
    print(
        f"\nToken Claude: vào {total_in}, ra {total_out}, khoảng {total_in * 0.1e-6 + total_out * 0.5e-6:.4f} USD"
    )


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    asyncio.run(main())
