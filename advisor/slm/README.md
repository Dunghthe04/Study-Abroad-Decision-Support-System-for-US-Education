# slm: vòng đời mô hình SLM tách ý

SLM (Qwen3-4B) đọc một tin nhắn của học sinh và tách thành các ý `{"type","span","entities"}`. Thư mục này chứa mọi thứ của model đó: dữ liệu, mã chuẩn bị, huấn luyện, đo, kết quả và model. **Chạy mọi lệnh trong thư mục `advisor`.**

## Cây thư mục

```
slm/
├── data/                         dữ liệu
│   ├── du_lieu_slm.xlsx          nguồn duy nhất: mỗi loại ý một sheet, người gán nhãn sửa ở đây
│   ├── du_lieu_slm.jsonl         sinh từ Excel, không sửa tay
│   ├── finetune/
│   │   ├── train.jsonl           hội thoại để huấn luyện (mang lên Colab)
│   │   └── val.jsonl             hội thoại để theo dõi lúc huấn luyện
│   ├── HUONG_DAN_GAN_NHAN.md     luật gán nhãn, các ca dễ lệch
│   └── ENTITIES_REFERENCE.md     từ điển loại ý và khóa entities (sinh tự động)
├── prepare/                      chuẩn bị dữ liệu
│   ├── build_slm_jsonl.py        Excel → JSONL, kiểm từng dòng
│   ├── check_slm_data.py         8 kiểm tra chất lượng và rò rỉ
│   ├── resplit_slm.py            chia lại train/val/test theo nhóm
│   ├── export_slm_chat.py        JSONL → hội thoại train/val
│   └── build_entities_reference.py   sinh ENTITIES_REFERENCE.md
├── train/                        huấn luyện
│   ├── train_slm.ipynb           notebook Colab (14 bước)
│   └── HUONG_DAN_FINETUNE.md     quy trình, giải thích lệnh, thuật ngữ
├── eval/                         đo và dựng báo cáo
│   ├── eval_slm.py               chạy model trên từng tin, ghi dự đoán
│   ├── slm_metrics.py            tính thước đo (F1, khoảng tin cậy, kiểm định cặp, hiệu chuẩn)
│   ├── slm_audit.py              kiểm tra dữ liệu, ví dụ lỗi, nhãn F1 = 0
│   ├── baseline_tfidf.py         hai mốc tham chiếu đơn giản
│   ├── slm_charts.py             vẽ 12 biểu đồ
│   ├── slm_report.py             dựng REPORT.md, bảng CSV, metrics.json
│   └── model_info.json           thông số công bố của các model (bảng tiêu chí chọn model)
├── results/                      kết quả đo (không lên git, số chính thức dùng `git add -f`)
│   ├── ket_qua/                  dự đoán từng câu: <model>[_shotsN][_limitN]_<split>.jsonl
│   └── bao_cao/                  mỗi lần dựng báo cáo một thư mục tự chứa
├── models/                       mỗi phiên bản một thư mục
│   └── usas-slm-vN/              Modelfile, training_log.csv, file .gguf (không lên git)
└── drive_package/                gói tạm để kéo lên Google Drive (không lên git, xóa sau khi tải lên)
```

## Quy trình và lệnh

| Bước | Lệnh | Ra |
|---|---|---|
| 1. Gán nhãn | Sửa `data/du_lieu_slm.xlsx` theo `HUONG_DAN_GAN_NHAN.md` | Dữ liệu nguồn |
| 2. Sinh JSONL | `python -m slm.prepare.build_slm_jsonl` | `data/du_lieu_slm.jsonl` |
| 3. Kiểm dữ liệu | `python -m slm.prepare.check_slm_data` (phải 0 lỗi) | Báo cáo kiểm |
| 4. Xuất hội thoại | `python -m slm.prepare.export_slm_chat` | `data/finetune/train.jsonl`, `val.jsonl` |
| 5. Fine-tune | Chạy `train/train_slm.ipynb` trên Colab | `.gguf`, `training_log.csv` |
| 6. Nạp model | `ollama create usas-slm-v5 -f Modelfile` | Model trong Ollama |
| 7. Đo | `python -m slm.eval.eval_slm --model usas-slm-v5 --prompt slm_intents --split test` | `results/ket_qua/*.jsonl` |
| 8. Báo cáo | `python -m slm.eval.slm_report --run "Tên=file" --out results/bao_cao/<ngày>_<tên>` | Thư mục báo cáo |

Chi tiết từng lệnh và tham số: `train/HUONG_DAN_FINETUNE.md`. Mốc tham chiếu: `python -m slm.eval.baseline_tfidf --split test`, rồi thêm `--baseline "Tên=file"` vào lệnh `slm_report`. Từ điển khóa entities: `python -m slm.prepare.build_entities_reference`.

## Báo cáo đo (`slm_report`)
Biểu đồ, bảng và nhận xét bằng tiếng Anh (yêu cầu của thầy ở MOM-10). Mỗi báo cáo có:
- **Tách lỗi định dạng khỏi lỗi hiểu ý:** tin model trả sai luật hai lần (lùi về `out_of_scope/ambiguous`) không tính là dự đoán của model; ma trận nhầm lẫn có cột `(invalid)`; có bảng riêng cho tin hợp lệ và cho tập tin mà **mọi** model đều hợp lệ (so công bằng).
- **Thống kê:** khoảng tin cậy 95% (bootstrap theo tin) cho mọi chỉ số, khoảng Wilson cho nhóm nhỏ, so cặp (McNemar chính xác và bootstrap cặp), hiệu chuẩn confidence (ECE).
- **Mô tả thí nghiệm:** bảng thiết lập, kiểm tra dữ liệu (số tin, câu trùng giữa các tập, file huấn luyện thật có trùng test không), ví dụ lỗi chọn theo mã băm, nhãn F1 = 0, tin chậm nhất, bảng tiêu chí chọn model.
- **Mốc tham chiếu** (chỉ so được ở mức loại ý) khi có `--baseline`.
- **Lý do tin hỏng và cài đặt chạy** chỉ có khi file dự đoán tạo bằng `eval_slm` bản mới; file cũ báo rõ là chưa ghi. Phần cứng, lượng tử hóa, phiên bản Ollama ghi bằng `--config "Phần cứng: ..."`.
- So entities **không phân biệt hoa thường** (phía sau cũng so như vậy); dòng "exact match phân biệt hoa thường" cho thấy hoa thường làm đổi bao nhiêu.

## Tài liệu

| File | Nội dung |
|---|---|
| `data/HUONG_DAN_GAN_NHAN.md` | Luật gán nhãn: tách ý, chọn loại, ca dễ lệch, mã `fields` |
| `data/ENTITIES_REFERENCE.md` | Mỗi loại ý có khóa nào, giá trị hợp lệ, ví dụ thật, số lần gặp |
| `train/HUONG_DAN_FINETUNE.md` | Quy trình, giải thích từng lệnh, thông số, thuật ngữ, lỗi hay gặp |
| `../app/skills/slm_intents.md` | Prompt mà model được huấn luyện và chạy thật |
