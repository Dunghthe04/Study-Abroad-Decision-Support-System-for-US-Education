# Hướng dẫn train model (Windows, RTX 4070 Ti Super)

Làm lần lượt từ trên xuống. **Có lỗi thì dừng, chụp hoặc copy toàn bộ chữ trong cửa sổ gửi lại.**

## 0. Xếp file

```
D:\usas_train\
├── train_slm.py
└── data\
    ├── train.jsonl
    └── val.jsonl
```

Trống ổ đĩa khoảng 50 GB. Khi train thì tắt game, Ollama và chế độ ngủ của máy.

## 1. Cài đặt (làm 1 lần)

Cập nhật driver NVIDIA, cài **Miniconda** và **Git**. Mở **Anaconda Prompt**, gõ:

```
nvidia-smi
```

Góc trên bên phải, `CUDA Version` phải từ **13.0** trở lên (thấp hơn thì cập nhật driver).

```
conda create --name usas_train python==3.12 -y
conda activate usas_train
pip3 install torch torchvision torchaudio --index-url https://download.pytorch.org/whl/cu130
python -c "import torch; print(torch.cuda.is_available(), torch.cuda.get_device_name(0))"
```

Phải in **True** và tên card. Đúng rồi mới chạy tiếp:

```
pip install unsloth
```

## 2. Train

```
cd /d D:\usas_train
python train_slm.py --version v7
```

Bị tắt giữa chừng (mất điện, lỡ đóng cửa sổ) thì chạy lại 3 lệnh, nó tự chạy tiếp:

```
conda activate usas_train
cd /d D:\usas_train
python train_slm.py --version v7
```

## 3. Gộp model

```
python train_slm.py --version v7 --merge
```

## 4. Đổi sang file .gguf

```
cd /d D:\usas_train
git clone --depth 1 https://github.com/ggml-org/llama.cpp
conda create --name usas_convert python==3.12 -y
conda activate usas_convert
pip install -r llama.cpp\requirements\requirements-convert_hf_to_gguf.txt
python llama.cpp\convert_hf_to_gguf.py ket_qua\v7\merged --outfile usas-slm-v7-f16.gguf --outtype f16
```

Vào https://github.com/ggml-org/llama.cpp/releases, tải file zip có tên chứa **bin-win-cpu-x64**, giải nén vào `D:\usas_train\llama_bin`. Rồi chạy:

```
llama_bin\llama-quantize.exe usas-slm-v7-f16.gguf usas-slm-v7.gguf Q4_K_M
```

## 5. Xong thì gửi file cho Dũng (qua Google Drive)

**Làm xong bước 4, gửi file `usas-slm-v7.gguf` cho Dũng.** File nằm ở `D:\usas_train\usas-slm-v7.gguf` (không gửi nhầm file `usas-slm-v7-f16.gguf`).

Gửi kèm:

- `usas-slm-v7.gguf` (khoảng 2,5 GB)
- Trong `ket_qua\v7`: `training_log.csv`, `trainer_state.json`, `thong_tin_train.json`
- Thư mục `ket_qua\v7\lora` (nén zip)

Chưa xóa gì cho tới khi được báo model chạy được.

## Lỗi hay gặp

| Lỗi | Cách sửa |
|---|---|
| In ra `False` ở bước 1 | Cập nhật driver, chạy lại lệnh cài torch |
| `CUDA out of memory` | Thêm `--batch 2 --grad-accum 8` vào lệnh, và **giữ ở mọi lệnh train sau** |
| `Có hội thoại dài quá --max-seq` | Thêm `--max-seq 4096` |
