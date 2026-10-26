"""Bước 2.8: SkillPicker chọn skill từ xác suất của model. Dùng model giả nên không cần Ollama hay model.pt."""

import numpy as np
import torch

from ml.skill_picker.data import LABELS
from ml.skill_picker.predict import SkillPicker


class FakeModel:
    """Model giả: luôn trả điểm (logit) soạn sẵn, giống SkillCAE (trả về điểm và vector dựng lại)."""

    def __init__(self, scores: dict[str, float]) -> None:
        self._logits = torch.tensor([[scores.get(label, -10.0) for label in LABELS]])

    def __call__(self, x: torch.Tensor):
        return self._logits, x


async def never_called(text: str) -> np.ndarray:
    raise AssertionError("predict_vector không được gọi embedder")


def test_picks_labels_above_threshold_highest_first():
    # điểm 3 ~ xác suất 0,95; điểm 1 ~ 0,73; điểm -1 ~ 0,27
    picker = SkillPicker(
        FakeModel({"skill_tra_loi": 1.0, "skill_ho_so": 3.0, "ket_thuc": -1.0}), LABELS, 0.5, never_called
    )

    result = picker.predict_vector(np.zeros(1024))

    assert result.skills == ["skill_ho_so", "skill_tra_loi"]  # xác suất cao xếp trước, ket_thuc bị loại
    assert 0.94 < result.probs["skill_ho_so"] < 0.96
    assert len(result.probs) == len(LABELS)
    assert not result.unsure


def test_unsure_when_nothing_reaches_threshold():
    picker = SkillPicker(FakeModel({"skill_ho_so": -1.0}), LABELS, 0.5, never_called)

    result = picker.predict_vector(np.zeros(1024))

    assert result.skills == []
    assert result.unsure


async def test_predict_builds_same_text_as_training_and_embeds_it():
    seen = []

    async def embedder(text: str) -> np.ndarray:
        seen.append(text)
        return np.zeros(1024, dtype=np.float32)

    picker = SkillPicker(FakeModel({"skill_ho_so": 3.0}), LABELS, 0.5, embedder)

    result = await picker.predict("B2", "Bạn đã thi SAT chưa?", "em được 1420", ["english", "budget"])

    assert seen == ["[BƯỚC] B2 [HỎI] Bạn đã thi SAT chưa? [TIN] em được 1420 [THIẾU] english, budget"]
    assert result.skills == ["skill_ho_so"]
