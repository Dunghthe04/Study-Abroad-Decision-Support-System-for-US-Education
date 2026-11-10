# Hướng dẫn fine-tune usas-slm (kèm giải thích để học)

Dùng cùng `train_slm.ipynb` trong thư mục này. Notebook chạy trên Google Colab. Lưu ý: notebook mới được viết lại và **chưa được chạy thử trên Colab**; thư viện Unsloth đổi khá nhanh, nếu một ô báo lỗi thì chụp lỗi gửi lại để sửa.

---

## Phần A. Hiểu trước khi làm

**Fine-tune là gì?** Lấy một model đã biết nhiều thứ (Qwen3-4B-Instruct, 4 tỷ tham số) rồi cho nó học thêm **một việc cụ thể** bằng các ví dụ của mình. Ở đây việc đó là: đọc tin nhắn của học sinh và trả JSON tách các ý. Mỗi ví dụ gồm đề (prompt + tin học sinh) và đáp án đúng.

**Vì sao không train từ đầu?** Quá tốn kém. Fine-tune chỉ cần vài nghìn ví dụ và vài giờ GPU.

**LoRA là gì?** Không sửa cả 4 tỷ tham số, mà gắn thêm các "miếng vá" rất nhỏ vào model rồi chỉ học các miếng này. Ưu điểm: nhanh, ít bộ nhớ, ít bị quên kiến thức cũ. `LORA_R = 16` là độ lớn của miếng vá (số càng lớn càng học được nhiều nhưng càng nặng).

**Nén 4-bit (quantization) là gì?** Lưu mỗi tham số bằng 4 bit thay vì 16 bit để model vừa bộ nhớ GPU của Colab. Chất lượng giảm rất ít. Cuối cùng khi xuất file `.gguf` cũng nén kiểu này (`q4_k_m`) để chạy bằng Ollama.

**Epoch, batch, bước (step):**
- **Epoch**: một lượt đi qua hết dữ liệu train. `EPOCHS = 2` là tối đa 2 lượt (đủ cho dữ liệu này; thêm lượt dễ học thuộc và rất tốn giờ GPU).
- **Batch**: số hội thoại xử lý cùng lúc (`BATCH = 2`). Gom 8 lần rồi mới cập nhật (`GRAD_ACCUM = 8`), nên mỗi lần cập nhật dùng 2 × 8 = **16 hội thoại**.
- **Bước (step)**: một lần cập nhật. Với 4.278 hội thoại thì một epoch khoảng 267 bước.

**Learning rate (`LR = 2e-4`)**: mỗi lần cập nhật, model "nhích" bao xa. Quá lớn thì học hỏng, quá nhỏ thì học chậm. 2e-4 là mức quen dùng cho LoRA.

**Loss và val loss:** loss là "model sai bao nhiêu" trên dữ liệu đang học; val loss là sai bao nhiêu trên **dữ liệu chưa học** (tập val). Mục tiêu là val loss thấp. Loss giảm nhưng val loss tăng nghĩa là model đang **học thuộc** thay vì học hiểu (overfitting).

**Dừng sớm (early stopping):** cứ 50 bước kiểm val loss; nếu 4 lần kiểm liên tiếp không tốt hơn thì dừng, và **giữ lại điểm có val loss thấp nhất**. Bảo vệ khỏi học thuộc.

**`train_on_responses_only`:** model chỉ bị chấm lỗi trên **phần đáp án** (JSON), không bị chấm trên prompt và tin nhắn học sinh. Nếu không, model tốn sức học thuộc đoạn prompt giống hệt ở mọi mẫu.

**Prompt lặp ở mọi mẫu:** prompt (~1.500 token) nằm trong từng hội thoại nên chiếm khoảng 92% khối lượng train. Đó là lý do train lâu dù đáp án ngắn.

**Train và lúc chạy phải khớp:** model học đúng *cái prompt này + dạng tin user này + dạng đáp án này*. Lúc chạy thật phải gửi đúng prompt `slm_intents.md` và đúng dạng tin user, nếu không kết quả sẽ lệch.

---

## Phần B. Các bước

### Bước 0. Chuẩn bị dữ liệu (ở máy bạn)
```
cd advisor
.venv\Scripts\python.exe -m scripts.build_slm_jsonl
.venv\Scripts\python.exe -m scripts.check_slm_data      # phải báo "Tổng số lỗi: 0"
.venv\Scripts\python.exe -m scripts.export_slm_chat
```
- Lệnh 1 sinh `du_lieu_slm.jsonl` từ Excel. Lệnh 2 kiểm dữ liệu. Lệnh 3 sinh hai file `finetune/train.jsonl` và `finetune/val.jsonl` (mỗi dòng là một hội thoại).
- Tập **test không được đưa vào** hai file này. Test chỉ để đo sau, nếu cho model thấy trước thì điểm đo vô nghĩa.

### Bước 1. Chuẩn bị 2 file trên máy bạn
Không cần tạo gì trên Google Drive. Ở bước chạy notebook sẽ có **hộp thoại chọn file** để bạn tải thẳng `train.jsonl` và `val.jsonl` từ máy lên Colab. Drive chỉ dùng để lưu kết quả train (notebook tự tạo thư mục).

### Bước 2. Mở notebook trên Colab và bật GPU
Mở `train_slm.ipynb` (tải lên Colab hoặc mở từ Drive). **Runtime → Change runtime type → GPU (T4 trở lên)**. Không có GPU thì train không nổi.

### Bước 3. Chạy từng ô, từ trên xuống
Mỗi ô có tiêu đề giải thích. Tóm tắt:
1. **Cài Unsloth** (vài phút). Nếu Colab đòi khởi động lại thì khởi động lại rồi chạy tiếp.
2. **Tải 2 file dữ liệu lên Colab** (hộp thoại chọn file, chọn cả `train.jsonl` và `val.jsonl`) **và nối Drive** để lưu kết quả. Ô tự kiểm file đúng phiên bản mới.
3. **Thông số**: giữ mặc định. Tên lần train `VERSION` (ở ô Bước 3) phải **đổi mỗi lần train bằng dữ liệu mới** (v5, v6…); giữ nguyên khi chạy tiếp một lần train bị ngắt.
4. **Tải model gốc** ở dạng nén 4-bit.
5. **Gắn LoRA**: thêm các miếng vá sẽ được học.
6. **Đọc dữ liệu và ghép thành chữ** theo khuôn mà Qwen quen. Ô in ra một mẫu để bạn nhìn bằng mắt.
7. **Kiểm độ dài**: số token lớn nhất phải **dưới 4096**, nếu không đáp án bị cắt.
8. **Cấu hình huấn luyện**: gom các thông số ở trên, thêm dừng sớm, che phần không tính lỗi.
9. **Kiểm phần bị che**: in một mẫu, phải chỉ thấy đáp án JSON. Thấy cả prompt nghĩa là cấu hình sai, **dừng lại sửa trước khi train**.
10. **Chạy train**: ô lâu nhất. Xem `loss` (mỗi 10 bước) và `eval_loss` (mỗi 50 bước).
11. **Lưu nhật ký** `training_log.csv` và `trainer_state.json` vào Drive. Hai file này dùng để vẽ biểu đồ cho thầy.
12. **Thử nhanh 4 câu** bằng mắt: có ra JSON đúng khuôn không. Chỉ là thử, không phải phép đo.
13. **Xuất `.gguf`** (~2,5 GB) và chép vào Drive.

### Cách đọc kết quả lúc train
| Thấy | Nghĩa là | Làm gì |
|---|---|---|
| `eval_loss` giảm rồi đi ngang | Học xong | Bình thường |
| `eval_loss` tăng lại, `loss` vẫn giảm | Đang học thuộc | Để dừng sớm tự cắt, hoặc giảm epoch |
| `loss` không giảm | Học không vào | Kiểm tra bước 9 (phần bị che), kiểm tra dữ liệu |
| Báo hết bộ nhớ GPU | Quá nặng | Đổi `BATCH = 1` và `GRAD_ACCUM = 16` (giữ batch hiệu dụng 16) |

---

## Phần C. Sau khi train

1. **Tải về máy** `.gguf` và `Modelfile` từ `MyDrive/usas_slm/<VERSION>/` (ví dụ `v5`; cần khoảng 2,5 GB trống; **ổ C: đang gần đầy nên đặt vào ổ D:**).
2. Đặt vào `advisor/models/usas-slm-v4/`, mở `Modelfile` kiểm dòng `FROM` trỏ đúng tên file `.gguf`, rồi nạp vào Ollama:
   ```
   ollama create usas-slm-v4 -f Modelfile
   ```
3. **Chưa chạy bằng code hệ thống được**: code hiện tại vẫn đọc tên tiếng Việt, anh em phải đổi theo `data/slm/TEN_KHOA_ANH_VIET.md`. Trong lúc chờ, thử trực tiếp bằng `ollama run usas-slm-v4` với đúng prompt `slm_intents.md` và tin user dạng JSON.
4. **Đo chính thức** (sau khi Đức duyệt dữ liệu, train lại bản cuối): chạy `eval_slm` trên tập **test**, báo F1 theo nhãn, so với baseline Qwen gốc.
5. Nếu muốn chia sẻ cho anh em: đẩy lên Hugging Face như model v3, ghi rõ trong README **prompt và dạng tin user phải dùng**.

## Phần D. Những điều dễ sai
- Chỉ chọn một trong hai file khi tải lên: ô sẽ báo thiếu file, chạy lại và chọn đủ cả `train.jsonl` lẫn `val.jsonl`.
- Tải nhầm `train.jsonl` cũ (của model v3, tên khóa tiếng Việt). Bản đúng có chữ `"intents"` trong đáp án.
- Đưa tập test vào train.
- Chạy thật mà dùng prompt khác prompt lúc train.
- Báo số đo của bản thử cho thầy: bản thử dùng dữ liệu chưa được duyệt, chỉ để anh em code, không phải để đo.
