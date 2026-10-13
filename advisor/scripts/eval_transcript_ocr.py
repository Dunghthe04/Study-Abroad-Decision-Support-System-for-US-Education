"""Chạy thử quy trình đọc bảng điểm với từng model đọc ảnh trên các tệp thật, để chọn model mặc định.

Cách chạy (Ollama đang bật, trong thư mục advisor):
    python -m scripts.eval_transcript_ocr ../backend/src/StudyAbroad.Api/uploads --models glm-ocr qwen3-vl:2b --out ../.ocr-eval

In ra mỗi tệp: số kỳ, số điểm đọc được, số điểm bị hậu kiểm loại, thời gian. Văn bản bước A và kết quả lưu vào --out để đối chiếu với ảnh gốc.
"""

from __future__ import annotations

import argparse
import asyncio
import base64
import json
import re
import time
from pathlib import Path

import httpx

from app.core.config import get_settings
from app.schemas.transcript_ocr import TranscriptFilePayload, TranscriptOcrRequest
from app.services.transcript_reader import TranscriptReader

EXTENSIONS = {".jpg", ".jpeg", ".png", ".webp", ".pdf"}


class RecordingReader(TranscriptReader):
    """Ghi lại văn bản bước A để xem model đọc ảnh chép được gì."""

    def __init__(self, *args, **kwargs) -> None:
        super().__init__(*args, **kwargs)
        self.transcripts: list[str] = []

    async def _transcribe(self, model: str, image_b64: str) -> str:
        text = await super()._transcribe(model, image_b64)
        self.transcripts.append(text)
        return text


async def run(files: list[Path], models: list[str], out_dir: Path) -> None:
    settings = get_settings()
    out_dir.mkdir(parents=True, exist_ok=True)
    rows: list[tuple[str, str, int, int, int, float, str]] = []

    async with httpx.AsyncClient(base_url=settings.ollama_base_url) as http:
        for model in models:
            reader = RecordingReader(http, settings.model_copy(update={"vision_model": model}))
            for path in files:
                reader.transcripts.clear()
                payload = TranscriptFilePayload(
                    image_base64=base64.b64encode(path.read_bytes()).decode(),
                    file_name=path.name,
                    content_type="application/pdf" if path.suffix.lower() == ".pdf" else "image/*",
                )
                started = time.perf_counter()
                result = await reader.read_transcript(TranscriptOcrRequest(files=[payload]))
                elapsed = time.perf_counter() - started

                scores = sum(len(t.scores) for t in result.terms)
                dropped = sum(int(m.group(1)) for w in result.warnings if (m := re.search(r"bỏ (\d+) điểm", w)))
                rows.append((model, path.name, len(result.terms), scores, dropped, elapsed, result.model_used or ""))

                stem = f"{re.sub(r'[^a-z0-9]+', '_', model)}__{path.stem}"
                (out_dir / f"{stem}.md").write_text("\n\n".join(reader.transcripts), encoding="utf-8")
                (out_dir / f"{stem}.json").write_text(
                    json.dumps(result.model_dump(by_alias=True), ensure_ascii=False, indent=1), encoding="utf-8"
                )
                print(f"{model:14} {path.name:42} kỳ={len(result.terms):2} điểm={scores:3} loại={dropped:2} {elapsed:6.1f}s")
                for w in result.warnings:
                    print(f"{'':16}! {w}")

    print("\nTổng hợp theo model:")
    for model in models:
        mine = [r for r in rows if r[0] == model]
        print(
            f"  {model:14} tệp có điểm={sum(1 for r in mine if r[3]):2}/{len(mine)} "
            f"điểm={sum(r[3] for r in mine):4} bị loại={sum(r[4] for r in mine):3} "
            f"thời gian TB={sum(r[5] for r in mine) / max(len(mine), 1):6.1f}s"
        )


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("root", type=Path)
    parser.add_argument("--models", nargs="+", required=True)
    parser.add_argument("--out", type=Path, default=Path(".ocr-eval"))
    args = parser.parse_args()
    files = sorted(p for p in args.root.rglob("*") if p.suffix.lower() in EXTENSIONS)
    asyncio.run(run(files, args.models, args.out))


if __name__ == "__main__":
    main()
