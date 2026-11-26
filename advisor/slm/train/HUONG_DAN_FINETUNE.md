# Hướng dẫn fine-tune usas-slm

Dùng cùng `train_slm.ipynb` (cùng thư mục). Mục tiêu: dạy model Qwen3-4B-Instruct-2507 **một việc**: đọc tin nhắn của học sinh và trả JSON tách ý `{"intents":[{"type","span","entities"}]}`. Mỗi loại ý và khóa `entities` được giải thích ở `slm/data/ENTITIES_REFERENCE.md`.

## 1. Quy trình tổng thể

```
Excel gán nhãn → JSONL → hội thoại train/val → [Colab: fine-tune] → .gguf → Ollama → đo trên test → hệ thống dùng
```

| # | Giai đoạn | Làm ở đâu | Công cụ | Ra gì |
|---|---|---|---|---|
| 1 | Gán nhãn | Máy bạn | `du_lieu_slm.xlsx`, `HUONG_DAN_GAN_NHAN.md` | Dữ liệu nguồn |
| 2 | Chuẩn bị dữ liệu | Máy bạn | 3 lệnh `slm.prepare.*` (mục 3.1) | `train.jsonl`, `val.jsonl` |
| 3 | Fine-tune | Google Colab (GPU) | `train_slm.ipynb`, Unsloth, TRL | Checkpoint, LoRA, `.gguf`, `training_log.csv` |
| 4 | Nạp model | Máy bạn | `ollama create` (mục 3.3) | Model `usas-slm-<VERSION>` trong Ollama |
| 5 | Đo | Máy bạn | `eval_slm`, `slm_report` (mục 3.4) | `REPORT.md`, biểu đồ |
| 6 | Dùng trong hệ thống | Máy bạn | `advisor/.env` | Advisor gọi model mới |

## 2. Khái niệm và thuật ngữ

**Fine-tune** là lấy một model đã biết nhiều thứ rồi cho học thêm một việc cụ thể bằng các ví dụ của mình (mỗi ví dụ gồm đề và đáp án đúng). Rẻ hơn train từ đầu rất nhiều: vài nghìn ví dụ; thời gian chạy từ vài giờ đến hơn một ngày tùy GPU.

| Thuật ngữ | Nghĩa | Trong dự án này |
|---|---|---|
| **Qwen3-4B-Instruct-2507** | Model gốc khoảng 4 tỷ tham số, bản Instruct (đã biết làm theo chỉ dẫn, không có chế độ suy luận) | `MODEL_NAME` |
| **SFT** (supervised fine-tuning) | Học có giám sát: cho xem (đề, đáp án đúng) và chấm lỗi theo đáp án | Cách huấn luyện ở đây |
| **Unsloth** | Thư viện làm fine-tune nhanh hơn và tốn ít bộ nhớ | `FastLanguageModel` |
| **TRL / SFTTrainer** | Thư viện (Hugging Face) chạy vòng huấn luyện SFT | `SFTTrainer`, `SFTConfig` |
| **LoRA** | Không sửa cả 4 tỷ tham số, chỉ gắn thêm các "miếng vá" nhỏ rồi học các miếng đó: nhanh, nhẹ, ít quên kiến thức cũ | `LORA_R = 16` |
| `r` (LoRA) | Độ rộng miếng vá: càng lớn học được càng nhiều nhưng càng nặng | 16 |
| `lora_alpha` | Hệ số nhân của miếng vá, thường đặt bằng `r` | 16 (bằng `r`) |
| `target_modules` | Các lớp được gắn LoRA: attention (`q_proj`, `k_proj`, `v_proj`, `o_proj`) và MLP (`gate_proj`, `up_proj`, `down_proj`) | Cả 7 lớp |
| **Nén 4-bit** (lượng tử hóa) | Lưu mỗi tham số bằng 4 bit thay vì 16 bit để model vừa GPU, chất lượng giảm rất ít | `load_in_4bit=True` |
| **Token / tokenizer** | Token là mảnh chữ model đọc; tokenizer là bộ chia chữ thành token. Độ dài đo bằng token | Prompt khoảng 1.500 token |
| `MAX_SEQ` | Số token tối đa của một hội thoại; dài hơn bị cắt mất đáp án | 4096 |
| **Chat template** (ChatML) | Khuôn ghép system, user, assistant bằng `<|im_start|>vai trò ... <|im_end|>` mà Qwen quen | `get_chat_template(..., "qwen3-instruct")` |
| **Epoch** | Một lượt đi qua hết dữ liệu train | `EPOCHS = 1` |
| **Batch** | Số hội thoại xử lý cùng lúc | `BATCH = 2` |
| **Gradient accumulation** | Gom nhiều batch rồi mới cập nhật một lần | `GRAD_ACCUM = 8`, nên mỗi lần cập nhật dùng 2 × 8 = 16 hội thoại |
| **Bước** (step) | Một lần cập nhật. Số bước = số mẫu ÷ 16 mỗi epoch | 7.507 mẫu: 470 bước/epoch, 940 bước hai epoch |
| **Learning rate** | Mỗi lần cập nhật model "nhích" bao xa: quá lớn học hỏng, quá nhỏ học chậm | `LR = 2e-4` |
| `warmup_ratio` | 5% bước đầu tăng dần learning rate từ 0 cho đỡ sốc | 0,05 |
| `lr_scheduler_type = linear` | Learning rate giảm đều về 0 đến cuối | linear |
| `optim = adamw_8bit` | Thuật toán cập nhật tham số, lưu trạng thái 8-bit để tiết kiệm bộ nhớ | adamw_8bit |
| `weight_decay` | Phạt nhẹ tham số quá lớn để giảm học thuộc | 0,01 |
| **Loss** | Model sai bao nhiêu trên dữ liệu đang học | In mỗi 10 bước |
| **Val loss** (`eval_loss`) | Model sai bao nhiêu trên **dữ liệu chưa học** (tập val). Đây là con số quan trọng | In mỗi 100 bước |
| **Overfitting** (học thuộc) | Loss giảm nhưng val loss tăng: model nhớ ví dụ chứ không hiểu | Dừng sớm tự cắt |
| **Early stopping** | Dừng khi val loss không tốt hơn sau `PATIENCE` lần kiểm liên tiếp, và giữ điểm tốt nhất | `PATIENCE = 4` (400 bước) |
| **Checkpoint** | Bản lưu giữa chừng, để chạy tiếp khi bị ngắt | Lưu lên Drive mỗi 100 bước, giữ 2 bản gần nhất |
| `train_on_responses_only` | Chỉ chấm lỗi trên phần đáp án (assistant), phần prompt và tin học sinh bị che (nhãn -100) | Tránh model tốn sức học thuộc prompt giống hệt ở mọi mẫu |
| **Gradient checkpointing** | Tính lại một số giá trị thay vì lưu để tiết kiệm bộ nhớ, đổi lại chậm hơn | `"unsloth"` |
| **Seed** | Số khởi tạo ngẫu nhiên cố định để chạy lại ra kết quả như cũ | 3407 |
| **GGUF, `q4_k_m`** | Định dạng file model nén cho Ollama; `q4_k_m` nén còn khoảng 2,5 GB | Bước 14 |
| **Modelfile** | Tệp mô tả cách Ollama nạp một model (mục 3.3) | `FROM`, `TEMPLATE` |
| **Ollama** | Phần mềm chạy model ngay trên máy | `ollama create`, `ollama list` |
| **Colab, T4, Drive** | Colab là máy ảo có GPU miễn phí, T4 là loại GPU phổ biến của Colab, Drive dùng để lưu kết quả | Bước 1 và 3 |

**Vì sao train lâu dù đáp án ngắn:** prompt (khoảng 1.500 token) nằm trong từng hội thoại nên chiếm khoảng 92% khối lượng. **Train và lúc chạy phải khớp:** model học đúng *prompt này, dạng tin user này, dạng đáp án này*; lúc chạy thật phải gửi đúng prompt `slm_intents.md` và đúng dạng tin user, nếu không kết quả lệch.

## 3. Các bước

### 3.1 Chuẩn bị dữ liệu (máy bạn, trong thư mục `advisor`)

| Lệnh | Làm gì | Ra gì |
|---|---|---|
| `python -m slm.prepare.build_slm_jsonl` | Đổi Excel thành `du_lieu_slm.jsonl`, kiểm từng dòng, sai thì báo tên sheet và số dòng | `slm/data/du_lieu_slm.jsonl` |
| `python -m slm.prepare.check_slm_data` | Kiểm dữ liệu (chỉ đọc): span nguyên văn; một ý `collect_profile`; dòng test không trùng đầu vào với train hoặc val; một hồ sơ chỉ ở một tập; đủ dòng test cho mỗi nhãn, mỗi mục `fields`, câu từ 3 ý và mỗi khóa hồ sơ | Phải báo `Tổng số lỗi: 0` |
| `python -m slm.prepare.resplit_slm` | Chia lại train, val, test **theo nhóm** (cùng hồ sơ hoặc cùng khuôn câu vào một tập) theo tỉ lệ 70/15/15. Mặc định chỉ in thử; thêm `--apply` mới ghi Excel | Chỉ chạy khi cần chia lại. Test đổi thì không so được với số đo cũ |
| `python -m slm.prepare.export_slm_chat` | Đổi JSONL thành hội thoại `[system = prompt, user = JSON đầu vào, assistant = JSON đáp án]`. Chỉ xuất train và val, bỏ câu trùng chữ với test | `slm/data/finetune/train.jsonl`, `val.jsonl` (không nằm trong git, xem bên dưới) |

Mỗi dòng của `train.jsonl` có dạng `{"messages": [system, user, assistant]}`. Tập **test không bao giờ đưa vào** hai file này: model thấy trước thì điểm đo vô nghĩa.

**`train.jsonl` và `val.jsonl` không nằm trong git** (quá 100 MB, GitHub không nhận; đã khai trong `.gitignore`). Chúng chỉ là bản "ghép sẵn" sinh ra từ `du_lieu_slm.jsonl` (có trong git). Người mới kéo code về hoặc đổi máy thì sinh lại, trong thư mục `advisor`:

```
python -m slm.prepare.check_slm_data      # phải 0 lỗi
python -m slm.prepare.export_slm_chat     # ra slm/data/finetune/train.jsonl và val.jsonl
```

Chỉ khi sửa `du_lieu_slm.xlsx` mới cần chạy thêm `python -m slm.prepare.build_slm_jsonl` trước `export_slm_chat`. Muốn chạy tiếp một lần train dở ở máy khác thì phải tải lên **đúng** hai file lúc bắt đầu (chép chúng sang máy kia), đừng sinh lại.

Nếu đã kích hoạt môi trường ảo (dòng nhắc có `(.venv)`) thì gõ `python`; nếu chưa, gọi `.venv\Scripts\python.exe` (PowerShell) hoặc `.venv/Scripts/python.exe` (Git Bash).

### 3.2 Fine-tune trên Colab (`train_slm.ipynb`)

Chạy lần lượt từng ô từ trên xuống. Không cần tạo gì trên Drive: ô Bước 3 hiện hộp thoại để bạn chọn `train.jsonl` và `val.jsonl` từ máy.

| Bước | Việc | Code chính | Cần thấy |
|---|---|---|---|
| 1 | Bật GPU: Runtime, Change runtime type, GPU (T4 trở lên) | `!nvidia-smi` | In tên GPU. Lỗi nghĩa là chưa bật |
| 2 | Cài thư viện | `pip install unsloth` | Vài phút. Colab bảo khởi động lại thì làm rồi chạy tiếp |
| 3 | Tải 2 file dữ liệu lên, nối Drive, đặt `VERSION` | `files.upload()`, `drive.mount()`, `OUT_DIR = .../usas_slm/<VERSION>` | Thấy `train.jsonl`, `val.jsonl`; ô tự kiểm có khóa `intents` |
| 4 | Đặt thông số | Các hằng số (bảng dưới) | |
| 5 | Tải model gốc | `FastLanguageModel.from_pretrained(..., load_in_4bit=True)` | |
| 6 | Gắn LoRA | `get_peft_model(r=16, lora_alpha=16, target_modules=[...], use_gradient_checkpointing="unsloth")` | |
| 7 | Đọc dữ liệu và ghép chữ theo khuôn Qwen | `load_dataset("json", ...)`, `apply_chat_template(...)`; val rút còn 300 mẫu cho nhanh | In một mẫu để nhìn bằng mắt |
| 8 | Kiểm độ dài | Đếm token của toàn bộ train | Số token lớn nhất **dưới 4096** |
| 9 | Cấu hình huấn luyện | `SFTTrainer` + `SFTConfig`, `EarlyStoppingCallback`, `train_on_responses_only` | |
| 10 | Kiểm phần bị che | In nhãn của một mẫu | Chỉ còn đáp án JSON. Thấy cả prompt thì **dừng, sửa Bước 9** |
| 11 | Huấn luyện | `trainer.train(resume_from_checkpoint=...)` | `loss` mỗi 10 bước, `eval_loss` mỗi `EVAL_EVERY` bước (mặc định 250) |
| 12 | Lưu nhật ký và bản LoRA | `training_log.csv`, `trainer_state.json`, thư mục `lora/` | File trong `OUT_DIR` |
| 13 | Thử nhanh 4 câu | `model.generate(...)` | Ra JSON đúng khuôn. Chỉ để nhìn, **không phải phép đo** |
| 14 | Xuất `.gguf` | `save_pretrained_gguf(RUN_NAME, tokenizer, quantization_method="q4_k_m")` | `.gguf` (khoảng 2,5 GB) và `Modelfile` chép vào `OUT_DIR` |

**Thông số ở Bước 4** (mặc định đã hợp lý, chỉ đổi khi có lý do):

| Tham số | Mặc định | Nghĩa | Khi nào đổi |
|---|---|---|---|
| `VERSION` | `v7` | Tên lần train, đặt tên thư mục lưu | **Đổi mỗi lần train bằng dữ liệu mới** (v8, v9...). Giữ nguyên khi chạy tiếp một lần bị ngắt |
| `MODEL_NAME` | `unsloth/Qwen3-4B-Instruct-2507` | Model gốc | Khi thử model khác (phải train lại) |
| `MAX_SEQ` | 2048 | Độ dài tối đa (token). Hạ xuống chỉ tiết kiệm bộ nhớ, ít làm nhanh hơn | Ô kiểm tra độ dài báo có hội thoại vượt (dữ liệu mới dài hơn thì tăng lên 2560, 3072) |
| `EPOCHS` | 2 | Số lượt tối đa (có dừng sớm). 2 để có đường cong val loss đủ dài chứng minh model đã hội tụ | Gấp thì đặt 1 (tiết kiệm khoảng một nửa giờ GPU, dừng sớm gần như không kích hoạt); thêm lượt dễ học thuộc |
| `LR` | 2e-4 | Learning rate | Mức quen dùng cho LoRA |
| `LORA_R` | 16 | Độ rộng LoRA | Tăng nếu học chưa đủ |
| `BATCH`, `GRAD_ACCUM` | 4, 4 | Batch hiệu dụng 4 × 4 = 16 | Báo CUDA out of memory: đổi thành 2 và 8 (giữ 16) |
| `EVAL_EVERY` | 250 | Cứ 250 bước kiểm val loss và lưu checkpoint (mỗi lần kiểm 300 mẫu val khoảng 4 phút, lưu lên Drive cũng chậm) | Đặt nhỏ hơn thì an toàn hơn khi hay bị ngắt nhưng chậm hơn |
| `VAL_MAX` | 300 | Số mẫu val dùng khi kiểm giữa chừng | Val đầy đủ dùng khi đo |
| `PATIENCE` | 2 | Dừng sau 2 lần kiểm không tốt hơn (= 500 bước với `EVAL_EVERY` 250) | Đổi cùng lúc với `EVAL_EVERY`: số bước chờ = `PATIENCE` × `EVAL_EVERY` |
| `BENCH_STEPS` | 0 | **Chạy thử:** đặt 40 để chỉ chạy 40 bước, in giây mỗi bước, bộ nhớ GPU đỉnh và ước lượng tổng giờ. Không kiểm val, không lưu gì | Trước mỗi lần train dài, hoặc khi muốn thử `BATCH` 4 và `GRAD_ACCUM` 4. Xong **đặt lại 0** |
| `SEED` | 3407, rồi 42 | Hạt ngẫu nhiên. **Train 2 lần**: lần 1 để 3407, lần 2 đổi thành 42 (giữ nguyên mọi thông số khác), chạy lại từ Bước 1. Kết quả lưu ở `usas_slm/<VERSION>/` (seed 3407) và `usas_slm/<VERSION>_seed42/` | Hai seed cho biết kết quả có ổn định không; seed cũng đổi tập 300 mẫu val dùng khi kiểm giữa chừng |
| `RUN_NAME` | `usas-slm-<VERSION>` (seed 42: thêm `_seed42`) | Tên model xuất ra và nạp vào Ollama | |

Cố định trong Bước 9: `warmup_ratio=0.05`, `lr_scheduler_type="linear"`, `optim="adamw_8bit"`, `weight_decay=0.01`, `group_by_length=True` (gom hội thoại gần độ dài nhau vào chung batch cho bớt phần đệm thừa), `save_total_limit=2`, `load_best_model_at_end=True` (theo `eval_loss`).

**Ví dụ lần train v5** (`training_log.csv`): 940 bước, `eval_loss` giảm đều từ 0,0356 (bước 100) xuống 0,0092 (bước 940) và không tăng lại, nên dừng sớm không kích hoạt. **Thời gian thực tế mất hơn một ngày** (đã gồm các lần bị ngắt rồi chạy tiếp). Cột `train_runtime` trong log (6.624 giây, khoảng 1,8 giờ) **không phải tổng thời gian**: Hugging Face tính lại từ đầu mỗi lần gọi `trainer.train()`, nên khi chạy tiếp từ checkpoint thì con số đó chỉ là lần chạy cuối. Mỗi lần kiểm `eval_loss` trên 300 mẫu tốn khoảng 4 phút.

**Đọc kết quả lúc train:**

| Thấy | Nghĩa là | Làm gì |
|---|---|---|
| `eval_loss` giảm rồi đi ngang | Học xong | Bình thường |
| `eval_loss` tăng lại, `loss` vẫn giảm | Đang học thuộc | Để dừng sớm tự cắt, hoặc giảm epoch |
| `loss` không giảm | Học không vào | Kiểm tra Bước 10 (phần bị che) và dữ liệu |
| Hết bộ nhớ GPU | Quá nặng | `BATCH = 1`, `GRAD_ACCUM = 16` |

**Bị ngắt kết nối giữa chừng:** không mất gì vì checkpoint nằm trên Drive. Kết nối lại, chạy lại Bước 1 đến 10 (chọn lại 2 file ở Bước 3, **giữ nguyên `VERSION`**), rồi chạy Bước 11: nó tự tiếp tục từ checkpoint mới nhất.

### 3.3 Nạp model vào máy và cho hệ thống dùng

1. Tải `.gguf` và `Modelfile` từ `MyDrive/usas_slm/<VERSION>/` về `advisor/slm/models/usas-slm-<VERSION>/` (cần khoảng 2,5 GB trống).
2. Nạp vào Ollama, chạy **trong thư mục chứa `Modelfile` và `.gguf`**:

| Lệnh | Làm gì |
|---|---|
| `ollama create usas-slm-v5 -f Modelfile` | Tạo model tên `usas-slm-v5` từ `Modelfile` |
| `ollama list` | Liệt kê model đã có (kiểm tra vừa tạo) |
| `ollama run usas-slm-v5` | Chạy thử tương tác. Chỉ để nhìn; đo thật dùng `eval_slm` |

`Modelfile` có hai phần: `FROM` là đường dẫn **tương đối** tới file `.gguf` (nên `.gguf` phải cùng thư mục), `TEMPLATE` là khuôn chat của Qwen viết dạng Go template, ghép system, user, assistant bằng `<|im_start|>` giống lúc train.

3. Cho hệ thống dùng: trong `advisor/.env` đặt `SLM_MODEL=usas-slm-v5` và `SLM_PROMPT=slm_intents`, rồi chạy lại advisor (`.env` chỉ được đọc lúc khởi động). Model cũ (v3) trả khóa tiếng Việt nên **không dùng được** với code hiện tại.

### 3.4 Đo model (tập test chỉ đo một lần)

| Lệnh | Làm gì |
|---|---|
| `python -m slm.eval.eval_slm --model usas-slm-v5 --prompt slm_intents --split test` | Chạy model trên từng tin của tập test, ghi dự đoán |
| `python -m slm.eval.eval_slm --model qwen3:4b-instruct --prompt slm_intents --shots 8 --split test` | Đo model gốc (chưa fine-tune) với 8 ví dụ trong prompt |
| `python -m slm.eval.baseline_tfidf --split test` | Tạo hai mốc tham chiếu đơn giản (không cần GPU) |
| `python -m slm.eval.slm_report --run "Tên=file" --out slm/results/bao_cao/<ngày>_<tên>` | Dựng báo cáo, bảng, biểu đồ từ các file dự đoán |

Tham số `eval_slm`: `--model` (tên trong Ollama), `--prompt` (luôn `slm_intents`), `--split` (`val` khi phát triển, `test` để đo thật), `--shots` (số ví dụ lấy từ train đưa vào prompt: model gốc dùng 8, model đã fine-tune dùng 0), `--limit N` (chạy thử N tin đầu, ghi file riêng `_limitN`, không đè file chạy đủ). Dự đoán ghi vào `slm/results/ket_qua/<model>[_shotsN][_limitN]_<split>.jsonl`.

Tham số `slm_report`: `--run` (dùng nhiều lần, **lần đầu là model tham chiếu**), `--out`, `--log training_log.csv` (vẽ đường cong huấn luyện), `--baseline "Tên=file"` (mốc tham chiếu), `--config "..."` (ghi điều kiện chạy như phần cứng vào báo cáo). Cách đọc một báo cáo: `slm/results` hoặc `05_results/README.md` của gói Drive.

## 4. Lỗi hay gặp

| Triệu chứng | Nguyên nhân | Cách xử lý |
|---|---|---|
| Ô Bước 3 báo thiếu file | Chỉ chọn một trong hai file | Chạy lại ô, chọn đủ `train.jsonl` và `val.jsonl` |
| Ô Bước 3 báo file train cũ | Dùng `train.jsonl` của model v3 (khóa tiếng Việt `cac_y`) | Tạo lại bằng `export_slm_chat`; bản đúng có chữ `"intents"` |
| Bước 8 báo vượt `MAX_SEQ` | Có hội thoại quá dài, đáp án bị cắt | Tăng `MAX_SEQ` hoặc rút ngắn dữ liệu |
| Bước 10 thấy cả prompt | `instruction_part` hoặc `response_part` sai | Sửa Bước 9 trước khi train |
| Hết bộ nhớ GPU | Batch quá lớn | `BATCH = 1`, `GRAD_ACCUM = 16` |
| Train từ đầu dù đã có checkpoint | Đổi `VERSION` giữa chừng | Giữ nguyên `VERSION` khi chạy tiếp |
| `ollama create` báo không thấy `.gguf` | `.gguf` không cùng thư mục với `Modelfile`, hoặc tên khác dòng `FROM` | Đặt cùng thư mục, sửa `FROM` cho đúng tên |
| Điểm đo cao bất thường | Tập test lọt vào train | Kiểm bằng `check_slm_data` |
| Kết quả thật lệch kết quả đo | Chạy với prompt khác lúc train | Dùng đúng `slm_intents.md` và dạng tin user của `export_slm_chat` |
| Số đo của bản thử gửi cho thầy | Bản thử dùng dữ liệu chưa duyệt | Chỉ gửi số đo trên tập test của bản chính thức |
