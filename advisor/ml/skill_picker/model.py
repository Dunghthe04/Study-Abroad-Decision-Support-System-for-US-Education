"""
MÔ HÌNH SKILLCAE
----------------
Mô hình nhận đầu vào là vector BGE-M3 gồm 1024 số và thực hiện 2 nhiệm vụ:

1. Phân loại:
   1024 số -> nén thành 128 số -> dự đoán 11 nhãn.
   Ví dụ: xác định câu người dùng thuộc nhóm nhu cầu, hồ sơ, đánh giá...

2. Tái tạo vector:
   Từ 128 số đã nén, dựng lại vector gồm 1024 số.
   Nhánh này giúp mô hình học cách giữ lại thông tin của vector đầu vào.

Luồng xử lý:
Vector 1024 số
       |
       v
BỘ MÃ HÓA (Encoder): nén 1024 số thành 128 số
       |
       +--------------------------+
       |                          |
       v                          v
BỘ PHÂN LOẠI (Head)          BỘ GIẢI MÃ (Decoder)
128 số -> 11 điểm            128 số -> 1024 số
       |                          |
       v                          v
Dùng để dự đoán nhãn         Dùng để học tái tạo vector
"""

# Thư viện Pytorch để xử lý tensor và tính tonas
import torch
from torch import nn  # Cung cấp các lớp để xây dựng mạng nơ-ron.

from ml.skill_picker.data import LABELS

# Số lượng giá trị của vector đầu vào BGE-M3.
INPUT_DIM = 1024

# Sau khi nén, vector chỉ còn 128 giá trị.
LATENT_DIM = 128

# Nhân vector với 32 để các giá trị nhỏ trở nên lớn hơn,
# giúp điều chỉnh thang giá trị khi mô hình xử lý.
# Khi dựng lại vector, chia cho 32 để đưa về thang giá trị ban đầu.
INPUT_SCALE = 32.0


# Xây dựng mô hình, sử dụng cơ chế của pytorch để xây dựng và quản lý mạng nơ-ron
class SkillCAE(nn.Module):
    """
    Định nghĩa mô hình gồm:
    - Encoder: nén vector.
    - Head: dự đoán 11 nhãn.
    - Decoder: dựng lại vector ban đầu.
    """

    def __init__(
        self,
        n_labels: int = len(LABELS),
        dropout: float = 0.3,  # tỷ lệ Dropout được sử dụng trong Head.
    ) -> None:
        super().__init__()

        # =====================================================
        # 1. BỘ MÃ HÓA (ENCODER)
        # =====================================================
        # Nén vector đầu vào qua 3 lớp Conv1D:
        #
        # Ban đầu: 1 kênh x 1024 vị trí
        # Lớp 1:  16 kênh x 512 vị trí
        # Lớp 2:  32 kênh x 256 vị trí
        # Lớp 3:  64 kênh x 128 vị trí
        #
        # Mỗi lớp Conv1D vừa trích xuất đặc trưng,
        # vừa giảm số vị trí xuống còn một nửa.
        # ReLU(): thêm tính phi tuyến để mô hình học các quan hệ phức tạp hơn.
        self.encoder_conv = nn.Sequential(
            # 1 kênh -> 16 kênh; 1024 -> 512 vị trí.
            nn.Conv1d(1, 16, kernel_size=7, stride=2, padding=3),
            nn.ReLU(),
            # 16 kênh -> 32 kênh; 512 -> 256 vị trí.
            nn.Conv1d(16, 32, kernel_size=5, stride=2, padding=2),
            nn.ReLU(),
            # 32 kênh -> 64 kênh; 256 -> 128 vị trí.
            nn.Conv1d(32, 64, kernel_size=3, stride=2, padding=1),
            nn.ReLU(),
        )

        # Sau 3 lớp Conv1D, dữ liệu có 64 x 128 = 8192 giá trị.
        # Linear nén 8192 giá trị xuống còn 128 giá trị.cố gắng giữ thông tin quan trọng
        self.encoder_fc = nn.Linear(
            64 * (INPUT_DIM // 8),
            LATENT_DIM,
        )

        # =====================================================
        # 2. BỘ GIẢI MÃ (DECODER)
        # =====================================================
        # Làm ngược lại việc nén:
        # 128 giá trị -> 8192 giá trị -> vector 1024 số.
        # Mục tiêu là học cách dựng lại vector đầu vào.

        # Mở rộng 128 giá trị thành 8192 giá trị.
        self.decoder_fc = nn.Linear(
            LATENT_DIM,
            64 * (INPUT_DIM // 8),
        )

        # Tăng dần số vị trí:
        # 128 -> 256 -> 512 -> 1024.
        # ConvTranspose1D học cách tạo lại các giá trị ở mỗi bước.
        # lớp kết nối đầy đủ (Fully Connected Layer) dùng để mở rộng vector latent từ 128 số thành 8192 số
        self.decoder_conv = nn.Sequential(
            nn.ConvTranspose1d(
                64,
                32,
                kernel_size=3,
                stride=2,
                padding=1,
                output_padding=1,
            ),
            nn.ReLU(),
            nn.ConvTranspose1d(
                32,
                16,
                kernel_size=5,
                stride=2,
                padding=2,
                output_padding=1,
            ),
            nn.ReLU(),
            nn.ConvTranspose1d(
                16,
                1,
                kernel_size=7,
                stride=2,
                padding=3,
                output_padding=1,
            ),
        )

        # =====================================================
        # 3. BỘ PHÂN LOẠI (HEAD)
        # =====================================================
        # Nhận 128 giá trị đã nén và tạo ra 11 điểm dự đoán.
        self.head = nn.Sequential(
            nn.Linear(LATENT_DIM, 64),  # 128 -> 64 giá trị.
            nn.ReLU(),  # Giúp mô hình học quan hệ phi tuyến.
            nn.Dropout(dropout),  # Ngẫu nhiên tắt một số nút khi huấn luyện.
            nn.Linear(64, n_labels),  # 64 -> 11 điểm dự đoán.
        )

    def encode(self, x: torch.Tensor) -> torch.Tensor:
        """
        Nén vector đầu vào từ 1024 giá trị xuống 128 giá trị.

        x: (B, 1024)
        Kết quả: (B, 128)

        B là số mẫu được xử lý cùng lúc.
        """

        # Nhân 32 để điều chỉnh thang giá trị.
        # Conv1D yêu cầu 3 chiều (B, số_kênh, chiều_dài)
        # unsqueeze(1) thêm chiều kênh:
        # (B, 1024) -> (B, 1, 1024).
        h = self.encoder_conv((x * INPUT_SCALE).unsqueeze(1))

        # Sau Conv1D: (B, 64, 128).
        # flatten(1) giữ nguyên chiều đầu tiên là B = 3, rồi gộp hai chiều còn lại thành một chiều.
        # Gộp 64 kênh và 128 vị trí thành 8192 giá trị:
        # (B, 64, 128) -> (B, 8192).
        h = h.flatten(1)

        # Nén 8192 giá trị thành 128 giá trị:
        # (B, 8192) -> (B, 128).
        return self.encoder_fc(h)

    def decode(self, z: torch.Tensor) -> torch.Tensor:
        """
        Dựng lại vector 1024 giá trị từ vector đã nén 128 giá trị.

        z: (B, 128)
        Kết quả: (B, 1024)
        """

        # Mở rộng 128 -> 8192 giá trị.
        h = self.decoder_fc(z)

        # Sắp xếp lại 8192 giá trị thành:
        # B mẫu, mỗi mẫu có 64 kênh và 128 vị trí.
        h = h.view(-1, 64, INPUT_DIM // 8)

        # Dựng lại 1024 vị trí:
        # (B, 64, 128) -> (B, 1, 1024).
        h = self.decoder_conv(h)

        # Bỏ chiều kênh có kích thước 1:
        # (B, 1, 1024) -> (B, 1024).
        # Chia 32 để đưa giá trị về thang ban đầu.
        return h.squeeze(1) / INPUT_SCALE

    def forward(
        self,
        x: torch.Tensor,
    ) -> tuple[torch.Tensor, torch.Tensor]:
        """
        Luồng xử lý chính khi gọi model(x).

        Trả về 2 kết quả:
        1. logits: 11 điểm dự đoán nhãn.
        2. reconstructed: vector 1024 giá trị được dựng lại.
        """

        # Bước 1: Nén vector 1024 -> 128.
        z = self.encode(x)

        # Bước 2: Dùng vector 128 giá trị cho cả hai nhiệm vụ.
        logits = self.head(z)  # (B, 128) -> (B, 11).
        reconstructed = self.decode(z)  # (B, 128) -> (B, 1024).

        return logits, reconstructed
