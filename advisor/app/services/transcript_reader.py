"""Dịch vụ OCR đọc bảng điểm (học bạ THPT Việt Nam) qua Ollama Vision / LLM."""

from __future__ import annotations

import base64
import io
import json
import logging
from typing import Any

import httpx
import pypdf

from app.core.config import Settings
from app.schemas.transcript_ocr import (
    TranscriptOcrRequest,
    TranscriptOcrResponse,
    TranscriptSubjectScore,
    TranscriptTerm,
)

logger = logging.getLogger(__name__)

STANDARD_SUBJECTS = [
    "Toán",
    "Ngữ văn",
    "Tiếng Anh",
    "Vật lí",
    "Hóa học",
    "Sinh học",
    "Lịch sử",
    "Địa lí",
    "Tin học",
    "Giáo dục công dân",
]

OCR_SYSTEM_PROMPT = """Bạn là trợ lý AI chuyên đọc và bóc tách bảng điểm học bạ THPT của học sinh Việt Nam.
Nhiệm vụ của bạn là đọc các con điểm trung bình môn từ ảnh bảng điểm học bạ được cung cấp.

Yêu cầu xuất ra định dạng JSON duy nhất khớp với cấu trúc sau:
{
  "terms": [
    {
      "termOrder": 1,
      "termName": "Lớp 10 HK1",
      "scores": [
        { "subject": "Toán", "score": 8.5 },
        { "subject": "Ngữ văn", "score": 7.8 }
      ]
    }
  ]
}

Quy tắc bóc tách:
1. Nhận diện các môn học chính: Toán, Ngữ văn, Tiếng Anh (hoặc Ngoại ngữ), Vật lí, Hóa học, Sinh học, Lịch sử, Địa lí, Tin học, Giáo dục công dân.
2. Lấy điểm trung bình học kỳ (ĐTBmhk) hoặc điểm tổng kết của từng môn. Điểm nằm trong thang 0.0 đến 10.0.
3. Chia theo học kỳ nếu có: Lớp 10 HK1, Lớp 10 HK2, Lớp 11 HK1, Lớp 11 HK2, Lớp 12 HK1. Nếu ảnh chỉ chụp 1 năm hoặc 1 kỳ, chỉ xuất ra các kỳ nhận diện được.
4. Chỉ trả về chuỗi JSON thuần túy, không có văn bản giải thích nào khác.
"""


class TranscriptReader:
    def __init__(self, http: httpx.AsyncClient, settings: Settings) -> None:
        self._http = http
        self._settings = settings

    async def read_transcript(self, request: TranscriptOcrRequest) -> TranscriptOcrResponse:
        """Đọc và bóc tách bảng điểm từ ảnh hoặc PDF qua Ollama Vision hoặc LLM."""
        raw_base64 = request.image_base64.strip()
        # Loại bỏ prefix data url nếu có (vd: data:image/png;base64,...)
        if "," in raw_base64:
            raw_base64 = raw_base64.split(",", 1)[1].strip()

        file_bytes = b""
        try:
            file_bytes = base64.b64decode(raw_base64)
        except Exception as ex:
            logger.warning("Không thể giải mã base64: %s", ex)

        # 1. Kiểm tra nếu là file PDF: thử trích xuất text trước
        extracted_text = ""
        is_pdf = (
            (request.content_type and "pdf" in request.content_type.lower())
            or (request.file_name and request.file_name.lower().endswith(".pdf"))
            or file_bytes.startswith(b"%PDF")
        )

        image_base64_to_send = raw_base64

        if is_pdf and file_bytes:
            try:
                reader = pypdf.PdfReader(io.BytesIO(file_bytes))
                for page in reader.pages:
                    text = page.extract_text()
                    if text:
                        extracted_text += text + "\n"
                
                # Nếu PDF có ảnh nhúng, lấy ảnh đầu tiên làm base64 gửi sang Vision
                if not extracted_text.strip():
                    for page in reader.pages:
                        if hasattr(page, "images") and len(page.images) > 0:
                            first_img = page.images[0]
                            image_base64_to_send = base64.b64encode(first_img.data).decode("utf-8")
                            break
            except Exception as ex:
                logger.warning("Lỗi trích xuất PDF: %s", ex)

        # 2. Nếu trích xuất được text từ PDF, dùng Qwen3 (text LLM) sẵn có để parse
        if extracted_text.strip() and len(extracted_text.strip()) > 50:
            parsed = await self._parse_with_text_llm(extracted_text)
            if parsed:
                return parsed

        # 3. Thử gọi mô hình Vision trên Ollama
        vision_result = await self._try_call_ollama_vision(image_base64_to_send)
        if vision_result:
            return vision_result

        # 4. Fallback: Nếu mô hình Vision chưa sẵn sàng hoặc lỗi, nạp khung mẫu môn THPT tiêu chuẩn
        return self._create_fallback_response("Hệ thống đã chuẩn bị sẵn khung môn học THPT tiêu chuẩn để bạn kiểm tra và nhập nhanh.")

    async def _try_call_ollama_vision(self, image_base64: str) -> TranscriptOcrResponse | None:
        """Thử gọi Ollama với mô hình Vision (minicpm-v hoặc model tương đương)."""
        # Kiểm tra xem mô hình vision có trong ollama không
        available_models = await self._get_available_ollama_models()
        target_model = self._settings.vision_model

        matched_model = None
        for m in available_models:
            if target_model in m or "vision" in m or "llava" in m or "minicpm" in m:
                matched_model = m
                break

        if not matched_model:
            logger.info("Chưa tìm thấy mô hình Vision %s trong Ollama (có sẵn: %s)", target_model, available_models)
            return None

        payload = {
            "model": matched_model,
            "messages": [
                {
                    "role": "user",
                    "content": OCR_SYSTEM_PROMPT,
                    "images": [image_base64],
                }
            ],
            "format": "json",
            "stream": False,
            "options": {"temperature": 0.0},
        }

        try:
            resp = await self._http.post(
                "/api/chat",
                json=payload,
                timeout=self._settings.vision_timeout_seconds,
            )
            resp.raise_for_status()
            data = resp.json()
            content = data.get("message", {}).get("content", "")
            return self._parse_json_to_response(content, matched_model)
        except Exception as ex:
            logger.warning("Gọi Ollama Vision %s thất bại: %s", matched_model, ex)
            return None

    async def _parse_with_text_llm(self, text: str) -> TranscriptOcrResponse | None:
        """Sử dụng Qwen3 để bóc tách text PDF học bạ thành JSON."""
        prompt = f"""Dưới đây là văn bản trích xuất từ bảng điểm học bạ:\n\n{text[:4000]}\n\n{OCR_SYSTEM_PROMPT}"""
        payload = {
            "model": self._settings.llm_model,
            "messages": [{"role": "user", "content": prompt}],
            "format": "json",
            "stream": False,
            "options": {"temperature": 0.0},
        }
        try:
            resp = await self._http.post("/api/chat", json=payload, timeout=self._settings.llm_timeout_seconds)
            resp.raise_for_status()
            content = resp.json().get("message", {}).get("content", "")
            return self._parse_json_to_response(content, self._settings.llm_model)
        except Exception as ex:
            logger.warning("Gọi Qwen3 bóc tách text PDF thất bại: %s", ex)
            return None

    async def _get_available_ollama_models(self) -> list[str]:
        """Lấy danh sách các mô hình hiện có trong Ollama."""
        try:
            resp = await self._http.get("/api/tags", timeout=5.0)
            if resp.status_code == 200:
                data = resp.json()
                return [m.get("name", "") for m in data.get("models", [])]
        except Exception:
            pass
        return []

    def _parse_json_to_response(self, json_str: str, model_name: str) -> TranscriptOcrResponse | None:
        """Chuyển đổi chuỗi JSON từ LLM thành TranscriptOcrResponse chuẩn."""
        try:
            data = json.loads(json_str)
            raw_terms = data.get("terms", [])
            if not isinstance(raw_terms, list) or len(raw_terms) == 0:
                return None

            terms: list[TranscriptTerm] = []
            all_scores: list[float] = []

            for t in raw_terms:
                term_order = int(t.get("termOrder", len(terms) + 1))
                term_name = str(t.get("termName", f"Học kỳ {term_order}"))
                raw_scores = t.get("scores", [])
                score_items: list[TranscriptSubjectScore] = []

                if isinstance(raw_scores, list):
                    for s in raw_scores:
                        subj = str(s.get("subject", "")).strip()
                        if not subj:
                            continue
                        try:
                            sc_val = float(s.get("score", 0.0))
                            sc_val = max(0.0, min(10.0, sc_val))
                            score_items.append(
                                TranscriptSubjectScore(
                                    subject=subj,
                                    score=round(sc_val, 2),
                                    rawScore=str(s.get("score", sc_val)),
                                    credits=1.0,
                                )
                            )
                            all_scores.append(sc_val)
                        except (ValueError, TypeError):
                            continue

                if score_items:
                    terms.append(TranscriptTerm(term_order=term_order, term_name=term_name, scores=score_items))

            if not terms:
                return None

            gpa4 = self._calculate_gpa4_scale(all_scores)

            return TranscriptOcrResponse(
                success=True,
                model_used=model_name,
                terms=terms,
                calculated_gpa4=gpa4,
                message=f"Đã tự động đọc và bóc tách thành công điểm {len(all_scores)} môn học bằng mô hình {model_name}.",
            )
        except Exception as ex:
            logger.warning("Không thể parse JSON từ mô hình: %s. Raw: %s", ex, json_str[:200])
            return None

    def _create_fallback_response(self, message: str) -> TranscriptOcrResponse:
        """Tạo khung học bạ 3 năm THPT chuẩn Việt Nam (Lớp 10, 11, 12; HK1, HK2)."""
        terms: list[TranscriptTerm] = [
            TranscriptTerm(
                term_order=1,
                term_name="Lớp 10 Học kỳ 1",
                scores=[TranscriptSubjectScore(subject=s, score=8.0, rawScore="8.0", credits=1.0) for s in STANDARD_SUBJECTS[:6]],
            ),
            TranscriptTerm(
                term_order=2,
                term_name="Lớp 10 Học kỳ 2",
                scores=[TranscriptSubjectScore(subject=s, score=8.2, rawScore="8.2", credits=1.0) for s in STANDARD_SUBJECTS[:6]],
            ),
            TranscriptTerm(
                term_order=3,
                term_name="Lớp 11 Học kỳ 1",
                scores=[TranscriptSubjectScore(subject=s, score=8.5, rawScore="8.5", credits=1.0) for s in STANDARD_SUBJECTS[:6]],
            ),
            TranscriptTerm(
                term_order=4,
                term_name="Lớp 11 Học kỳ 2",
                scores=[TranscriptSubjectScore(subject=s, score=8.6, rawScore="8.6", credits=1.0) for s in STANDARD_SUBJECTS[:6]],
            ),
            TranscriptTerm(
                term_order=5,
                term_name="Lớp 12 Học kỳ 1",
                scores=[TranscriptSubjectScore(subject=s, score=8.8, rawScore="8.8", credits=1.0) for s in STANDARD_SUBJECTS[:6]],
            ),
        ]

        all_scores = [s.score for t in terms for s in t.scores]
        gpa4 = self._calculate_gpa4_scale(all_scores)

        return TranscriptOcrResponse(
            success=False,
            model_used="standard_highschool_template",
            terms=terms,
            calculated_gpa4=gpa4,
            message=message,
        )

    @staticmethod
    def _calculate_gpa4_scale(scores: list[float]) -> float | None:
        """Quy đổi chuẩn điểm thang 10 sang thang 4.0."""
        if not scores:
            return None
        total_gpa4 = 0.0
        for s in scores:
            if s >= 8.5:
                val = 4.0
            elif s >= 8.0:
                val = 3.5
            elif s >= 7.0:
                val = 3.0
            elif s >= 6.0:
                val = 2.5
            elif s >= 5.0:
                val = 2.0
            else:
                val = 1.0
            total_gpa4 += val
        return round(total_gpa4 / len(scores), 2)
