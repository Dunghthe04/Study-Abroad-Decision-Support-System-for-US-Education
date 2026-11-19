# slm: vòng đời mô hình SLM tách ý

Mọi thứ liên quan đến SLM (dữ liệu, huấn luyện, đo, kết quả, model) nằm ở đây. Chạy mọi lệnh trong thư mục `advisor`.

| Thư mục | Nội dung |
|---|---|
| `data/` | Dữ liệu gán nhãn: `du_lieu_slm.xlsx` (nguồn), `du_lieu_slm.jsonl` (sinh ra), `finetune/` (train.jsonl, val.jsonl mang lên Colab), `HUONG_DAN_GAN_NHAN.md` |
| `prepare/` | Chuẩn bị dữ liệu: `build_slm_jsonl`, `check_slm_data`, `resplit_slm`, `export_slm_chat` |
| `train/` | Huấn luyện: notebook Colab `train_slm.ipynb` và `HUONG_DAN_FINETUNE.md` |
| `eval/` | Đo: `eval_slm` (chạy model), `slm_metrics` (chỉ số), `slm_charts` (biểu đồ), `slm_report` (báo cáo) |
| `results/` | Kết quả đo: `ket_qua/` (dự đoán từng câu), `bao_cao/` (báo cáo mỗi lần đo). Không lên git, số chính thức thì `git add -f` |
| `models/` | Mỗi phiên bản một thư mục: `Modelfile`, `training_log.csv`, file `.gguf` (không lên git, sao lưu Drive) |

## Quy trình
```
python -m slm.prepare.build_slm_jsonl                 # Excel -> JSONL
python -m slm.prepare.check_slm_data                  # kiểm dữ liệu
python -m slm.prepare.export_slm_chat                 # ra train.jsonl, val.jsonl cho Colab
# huấn luyện trên Colab (slm/train), lưu training_log.csv vào slm/models/<phiên bản>/
python -m slm.eval.eval_slm --model usas-slm-v5 --prompt slm_intents --split val
python -m slm.eval.eval_slm --model usas-slm-v5 --prompt slm_intents --split test   # test: đo một lần
python -m slm.eval.slm_report --run "Model gốc=slm/results/ket_qua/qwen3_4b-instruct_shots8_test.jsonl" \
    --run "v5=slm/results/ket_qua/usas-slm-v5_test.jsonl" \
    --out slm/results/bao_cao/<ngày>_v5_test_ban_thu --log slm/models/usas-slm-v5/training_log.csv
```
