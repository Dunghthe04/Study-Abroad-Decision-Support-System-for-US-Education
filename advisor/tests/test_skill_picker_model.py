"""Bước 2.6: hình dạng đầu vào, đầu ra của CAE. Không cần Ollama, không cần dữ liệu thật."""

import torch

from ml.skill_picker.data import LABELS
from ml.skill_picker.model import INPUT_DIM, LATENT_DIM, SkillCAE
from ml.skill_picker.train_cae import predict_prob


def test_shapes_of_encoder_decoder_and_head():
    model = SkillCAE()
    x = torch.randn(4, INPUT_DIM)

    logits, recon = model(x)

    assert model.encode(x).shape == (4, LATENT_DIM)  # nén 1024 số còn 128 số
    assert logits.shape == (4, len(LABELS))  # 11 điểm, mỗi nhãn một điểm
    assert recon.shape == x.shape  # dựng lại đúng 1024 số


def test_predict_prob_is_between_0_and_1():
    prob = predict_prob(SkillCAE(), torch.randn(3, INPUT_DIM))

    assert prob.shape == (3, len(LABELS))
    assert ((prob >= 0) & (prob <= 1)).all()


def test_loss_decreases_when_overfitting_tiny_batch():
    torch.manual_seed(0)
    model = SkillCAE()
    x = torch.randn(8, INPUT_DIM) / 32
    y = torch.zeros(8, len(LABELS))
    y[:, 2] = 1
    optimizer = torch.optim.Adam(model.parameters(), lr=1e-2)
    bce = torch.nn.BCEWithLogitsLoss()

    first = bce(model(x)[0], y).item()
    for _ in range(30):
        loss = bce(model(x)[0], y)
        optimizer.zero_grad()
        loss.backward()
        optimizer.step()

    assert loss.item() < first  # học được: loss giảm
