# Hướng dẫn đo và vẽ biểu đồ các model SLM

Làm theo thứ tự từ trên xuống. Mọi lệnh chạy trong **PowerShell**, trừ khi ghi khác.
Máy tham chiếu: RTX 3050 6GB. Tập đo là `test` trong `slm/data/du_lieu_slm.jsonl`, không cần sinh thêm file nào.

## 0. Chuẩn bị (làm một lần mỗi lần mở terminal)

```
cd "D:\DoAn\Study Abroad Decision Support System for US Education\advisor"
.\.venv\Scripts\Activate.ps1
```

Đầu dòng phải hiện `(.venv)`. Nếu báo thiếu thư viện (`No module named ...`) thì bạn đang dùng Python hệ thống, hãy kích hoạt lại `.venv`, hoặc `pip install -r requirements.txt`.
Ollama phải đang chạy (biểu tượng ở khay hệ thống, hoặc `ollama list` ra danh sách là được).

## 1. Kiểm tra GPU có bị chiếm không

Đo tốc độ chỉ có nghĩa khi GPU trống. Nếu chương trình khác chiếm VRAM, model có thể tràn sang CPU, chạy chậm và số đo thời gian không còn đúng.

```
nvidia-smi
```
Xem hai chỗ: **Memory-Usage** (nên dưới khoảng 500 MiB khi chưa chạy gì) và danh sách **Processes** ở dưới.

Ollama đang giữ model nào trong GPU:
```
ollama ps
```

### Giải phóng GPU
```
ollama stop qwen3:4b-instruct
ollama stop gemma3:4b
ollama stop phi4-mini
ollama stop usas-slm-v6
```
Dừng hết những model đang liệt kê trong `ollama ps`. Nếu `nvidia-smi` vẫn thấy chương trình khác chiếm (trình duyệt, game, Colab cục bộ...), đóng nó đi. Chỉ khi cần mới dùng `taskkill /PID <số PID> /F` với PID đọc từ `nvidia-smi`, và chỉ với chương trình bạn biết rõ.

Chạy lại `nvidia-smi` cho tới khi VRAM gần trống.

## 2. Tải model gốc (nếu máy chưa có)

```
ollama pull qwen3:4b-instruct
ollama pull gemma3:4b
ollama pull phi4-mini
ollama list
```

## 3. Kiểm tra nhanh: model chạy 100% GPU

```
ollama run qwen3:4b-instruct "Xin chào" --verbose
ollama ps
```
Cột **PROCESSOR** phải ghi `100% GPU`. Nếu ghi `x% CPU/y% GPU` là đã tràn, quay lại mục 1. Xong thì `ollama stop qwen3:4b-instruct`.

## 4. Thử đo 20 câu

```
python -m slm.eval.eval_slm --model qwen3:4b-instruct --split test --shots 8 --limit 20
```
Chạy được và in bảng tóm tắt là ổn. **Xóa file thử** trong `slm/results/ket_qua/` (tên có `limit20`) trước khi đo thật, vì mọi model đưa vào cùng một báo cáo phải đo đúng cùng một tập câu.

## 5. Đo đầy đủ trên tập test

Model gốc dùng `--shots 8` (8 ví dụ minh họa kèm theo). Model đã fine-tune dùng `--shots 0` (mặc định, bỏ tham số).

```
python -m slm.eval.eval_slm --model qwen3:4b-instruct --split test --shots 8
python -m slm.eval.eval_slm --model gemma3:4b --split test --shots 8
python -m slm.eval.eval_slm --model phi4-mini --split test --shots 8
```
Mỗi lệnh chạy lâu (tập test gần 3.900 câu), có thể để qua đêm, mỗi lần một model. Khi lệnh sau chạy, model trước còn nằm trong GPU thì `ollama stop <tên model trước>` cho nhẹ.
Kết quả ra `slm/results/ket_qua/<tên model>_shots8_test.jsonl`. Đo bị ngắt giữa chừng thì chạy lại từ đầu lệnh đó.

Điều kiện đo (temperature 0, ngữ cảnh 6144 token, tất cả lớp lên GPU, seed) lấy từ cài đặt dự án và được ghi vào từng dòng kết quả, nên không cần tự đặt.

### Model đã fine-tune

Sau khi train trên Colab xong, nạp vào Ollama theo mục 3.3 của `slm/train/HUONG_DAN_FINETUNE.md`:
```
cd slm\models\usas-slm-v6
ollama create usas-slm-v6 -f Modelfile
cd ..\..\..
python -m slm.eval.eval_slm --model usas-slm-v6 --split test
```
Thay `v6` bằng đúng tên phiên bản. Phải đo **trên tập test của đúng bản dữ liệu đã dùng để train**; nếu không, so sánh không công bằng.

## 6. (Không bắt buộc) Mốc tham chiếu TF-IDF

Mô hình thống kê đơn giản, chỉ để so mức cơ sở cho việc phân loại ý.
```
python -m slm.eval.baseline_tfidf --split test
```

## 7. Dựng báo cáo và biểu đồ

So 3 model gốc:
```
python -m slm.eval.slm_report `
  --run "Qwen3-4B=slm/results/ket_qua/qwen3_4b-instruct_shots8_test.jsonl" `
  --run "Gemma3-4B=slm/results/ket_qua/gemma3_4b_shots8_test.jsonl" `
  --run "Phi4-mini=slm/results/ket_qua/phi4-mini_shots8_test.jsonl" `
  --out slm/results/bao_cao/2026-11-25_so_sanh_3_model_test_ban_thu `
  --config "Phần cứng: RTX 3050 6GB, Ollama (ghi phiên bản: lệnh ollama --version)"
```

So model gốc với model đã fine-tune (model **gốc đặt đầu tiên**):
```
python -m slm.eval.slm_report `
  --run "Qwen3-4B gốc=slm/results/ket_qua/qwen3_4b-instruct_shots8_test.jsonl" `
  --run "Qwen3-4B fine-tune=slm/results/ket_qua/usas-slm-v6_test.jsonl" `
  --log slm/models/usas-slm-v6/training_log.csv `
  --out slm/results/bao_cao/2026-11-25_hieu_qua_finetune_test_ban_thu `
  --config "Phần cứng: RTX 3050 6GB, Ollama (ghi phiên bản)"
```
(Muốn thêm mốc TF-IDF thì thêm `--baseline "TF-IDF=<file ở mục 6>"`.)

- `--run "Tên=file"` dùng nhiều lần, mỗi model một lần. Dấu `` ` `` (huyền, phím cạnh số 1) cuối dòng là xuống dòng của PowerShell. Nếu gặp lỗi thì gõ tất cả trên **một dòng**.
- Đặt tên thư mục báo cáo theo mẫu `<ngày>_<nội dung>_<tập>_<ban_thu|chinh_thuc>`, ngày mới để không ghi đè báo cáo cũ.
- Lệnh sẽ **dừng và báo lỗi** nếu các file đo khác tập hoặc khác danh sách câu. Khi đó chạy lại cho đồng nhất.

Kết quả trong thư mục `--out`: `REPORT.md` (đọc trước), bảng CSV, `metrics.json` và các biểu đồ PNG.

## Việc dễ làm sai

| Triệu chứng | Nguyên nhân thường gặp |
|---|---|
| `No module named httpx` | Chưa kích hoạt `.venv` (mục 0) |
| Chạy rất chậm, `ollama ps` ghi CPU | GPU đang bị chiếm (mục 1) |
| `slm_report` báo khác tập | Còn lẫn file `limit20`, hoặc một model đo chưa xong |
| Số đo mới khác hẳn số cũ | Đã đổi dữ liệu và tập test; đừng so trực tiếp với báo cáo cũ |
