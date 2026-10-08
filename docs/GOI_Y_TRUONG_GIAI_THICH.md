# Gợi ý trường: phương pháp và công thức

Tài liệu này giải thích cách USAS chọn trường cho học sinh khi bấm nút **"Lọc trường theo hồ sơ"**: lọc thế nào, chấm điểm bằng công thức gì, trọng số lấy từ đâu và LLM tham gia ở bước nào. Mọi con số ở đây khớp với code và test hiện tại; sửa công thức thì sửa cả tài liệu này.

## Đọc nhanh

- **Thuật toán chọn trường là của nhóm**: lọc cứng, phân nhóm reach/match/safety, rồi xếp hạng bằng **SAW** (Simple Additive Weighting). Trọng số SAW được chứng minh bằng **AHP**.
- **LLM chỉ là công cụ**, làm 2 việc:
  1. **Đọc chữ thành thuộc tính**: đọc mô tả hoạt động ngoại khóa thành vai trò, tổ chức uy tín, phạm vi thực tế. LLM không chấm điểm; điểm do công thức của nhóm tính.
  2. **Viết lời giải thích** cho danh sách trường đã chọn. Lời giải thích được code kiểm tra lại trước khi hiển thị.
- **AI lỗi thì hệ thống vẫn chạy**: mọi bước dùng LLM đều có phương án dự phòng không cần LLM.

| Bước | Ai làm | Code |
|---|---|---|
| Đọc hoạt động ngoại khóa thành thuộc tính | LLM (Qwen3 8B) | `advisor/app/services/activity_reader.py` |
| Tính điểm ngoại khóa 0–4 | Công thức của nhóm | `advisor/app/services/extracurricular.py` |
| Lọc cứng, phân nhóm, SAW | Công thức của nhóm | `backend/.../Recommendations/RecommendationScorer.cs`, `AdmissionCategorizer.cs` |
| Viết lời giải thích | LLM | `advisor/app/services/recommender.py` |
| Kiểm tra lời giải thích | Code | `advisor/app/services/fact_check.py`, `backend/.../Recommendations/AiOutputGuard.cs` |

## Luồng một lần bấm nút

```
Hồ sơ học sinh (học thuật, tài chính, tiếng Anh, hoạt động ngoại khóa)
   │
   ├─ 1. Điểm ngoại khóa: LLM đọc chữ → thuộc tính → công thức → điểm 0–4
   │     (chỉ tính lại khi danh sách hoạt động thay đổi)
   │
   ├─ 2. Lọc cứng: ngành, bang mong muốn, ngân sách
   ├─ 3. Phân nhóm: reach / match / safety theo GPA và SAT
   ├─ 4. Chuẩn hóa 3 tiêu chí về [0, 1] (tiếng Anh nằm trong học thuật)
   ├─ 5. SAW với trọng số AHP → xếp hạng trong từng nhóm, lấy tối đa 12 trường
   │
   ├─ 6. LLM viết giải thích 3 câu cho từng trường: học thuật, tài chính, ngoại khóa
   └─ 7. Code kiểm tra giải thích; sai thì thay bằng câu soạn sẵn
```

## 1. Lọc cứng

Trường bị loại nếu vi phạm một trong ba điều kiện. Học sinh để trống điều kiện nào thì bỏ qua điều kiện đó.

| Điều kiện | Quy tắc |
|---|---|
| Ngành | Trường có ngành học sinh chọn |
| Bang | Trường nằm trong các bang học sinh muốn |
| Ngân sách | Tổng chi phí/năm ≤ ngân sách × 1.1 (cho vượt 10%). Trường không công bố chi phí thì giữ lại |

## 2. Phân nhóm reach / match / safety

So hồ sơ học sinh với số liệu tuyển sinh của trường:

| | Thấp (reach) | Ngang (match) | Cao (safety) |
|---|---|---|---|
| **GPA** (thang 4) | GPA ≤ GPA TB của trường − 0.3 | ở giữa | GPA ≥ GPA TB + 0.3 |
| **SAT** | SAT < mốc 25% của trường | ở giữa | SAT ≥ mốc 75% |

- Có cả GPA và SAT thì **lấy mức thấp hơn** (thận trọng).
- Không có cả hai thì nhóm là **"chưa đủ dữ liệu"**. Riêng bậc cao đẳng cộng đồng (tuyển sinh mở) thì xếp **safety**.
- Mỗi nhóm lấy tối đa: reach 3, match 5, safety 4; tổng tối đa 12 trường.

## 3. Chuẩn hóa 3 tiêu chí về [0, 1]

| Tiêu chí | Công thức | Thiếu dữ liệu |
|---|---|---|
| **Học thuật** | Trung bình có trọng số: 0.4 × GPA fit + 0.4 × SAT fit + 0.2 × Tiếng Anh fit (xem bảng dưới) | Học sinh không có GPA, SAT lẫn điểm tiếng Anh: bỏ tiêu chí. Có điểm nhưng trường không có số liệu nào: 0.5 |
| **Tài chính** | (Ngân sách − Chi phí) / Ngân sách, kẹp trong [0, 1] | Học sinh chưa nhập ngân sách: bỏ tiêu chí. Trường không công bố chi phí: 0.5 |
| **Ngoại khóa** | Điểm ngoại khóa / 4 (xem mục 6) | Chưa có điểm: bỏ tiêu chí |

Ba phần của học thuật, mỗi phần kẹp trong [0, 1]:

| Phần | Trọng số | Công thức |
|---|---|---|
| GPA fit | 0.4 | (GPA − GPA TB của trường + 0.3) / 0.6 |
| SAT fit | 0.4 | (SAT − SAT25) / (SAT75 − SAT25) |
| Tiếng Anh fit | 0.2 | min(1, điểm / mức tối thiểu của trường), lấy bài thi tốt nhất (IELTS, TOEFL, Duolingo) |

- Chỉ tính phần mà **cả học sinh lẫn trường** đều có số liệu, rồi chia lại theo tổng trọng số của các phần còn lại. Ví dụ trường không công bố mức IELTS: học thuật = (0.4 × GPA fit + 0.4 × SAT fit) / 0.8.
- Tiếng Anh nhẹ hơn GPA, SAT vì bản chất là điều kiện đạt/không đạt (đạt = 1, cao hơn mức tối thiểu không được thêm điểm). Học sinh chưa đạt vẫn có cảnh báo riêng trên kết quả. Tỷ lệ 0.2 lấy từ AHP 4 tiêu chí trước đây: tiếng Anh 0.098 / (học thuật 0.437 + tiếng Anh 0.098) ≈ 0.18.

**Bỏ tiêu chí** nghĩa là SAW chia lại cho tổng trọng số của các tiêu chí còn lại, để học sinh thiếu một loại điểm không bị kéo điểm xuống 0.

## 4. Trọng số: chứng minh bằng AHP

AHP (Analytic Hierarchy Process, Saaty) biến các câu so sánh "tiêu chí A quan trọng hơn B bao nhiêu lần" thành trọng số, và kiểm tra người so sánh có tự mâu thuẫn không.

### Ma trận so sánh cặp

Ô (hàng, cột) = hàng quan trọng hơn cột bao nhiêu lần, theo thang Saaty 1–9. Ô đối xứng là nghịch đảo.

| | Học thuật | Tài chính | Ngoại khóa |
|---|---|---|---|
| **Học thuật** | 1 | 2 | 2 |
| **Tài chính** | 1/2 | 1 | 3/2 |
| **Ngoại khóa** | 1/2 | 2/3 | 1 |

| So sánh | Mức | Lý do |
|---|---|---|
| Học thuật / Tài chính | 2 | Học thuật quyết định nhóm reach/match/safety và khả năng được nhận; ngân sách đã lọc cứng ở bước 1, trong xếp hạng chỉ còn để ưu tiên trường rẻ hơn |
| Học thuật / Ngoại khóa | 2 | Trường Mỹ xét hồ sơ toàn diện, nhưng không nặng bằng học thuật |
| Tài chính / Ngoại khóa | 3/2 | Chi phí quyết định gia đình có theo học được không |

### Tính trọng số (trung bình nhân theo hàng)

```
Học thuật : (1 × 2 × 2)^(1/3)       = 1.587
Tài chính : (1/2 × 1 × 3/2)^(1/3)   = 0.909
Ngoại khóa: (1/2 × 2/3 × 1)^(1/3)   = 0.693
Tổng = 3.189

→ Học thuật 0.498 · Tài chính 0.285 · Ngoại khóa 0.217
```

Tính bằng vector riêng cho cùng kết quả: 0.4977 · 0.2849 · 0.2174.

### Kiểm tra nhất quán

```
λmax = 3.018
CI   = (λmax − n) / (n − 1) = (3.018 − 3) / 2 = 0.0091
CR   = CI / RI = 0.0091 / 0.58 = 0.016        (RI = 0.58 với n = 3)
```

**CR = 0.016 < 0.1**: các phép so sánh nhất quán, trọng số dùng được. Kết quả cũng khớp AHP 4 tiêu chí trước đây khi gộp tiếng Anh vào học thuật: 0.437 + 0.098 = 0.536 · 0.268 · 0.197.

### Trọng số dùng trong hệ thống

Làm tròn kết quả AHP: **Học thuật 0.5 · Tài chính 0.3 · Ngoại khóa 0.2** (tổng = 1). Đây là giá trị mặc định trong `RecommendSettings.cs`, đồng thời được ghi vào bảng `app_settings` (key `recommend.weights`). Admin sửa dòng này là đổi được mọi hệ số mà không phải sửa code hay khởi động lại API:

```json
{"weights": {"academic": 0.5, "finance": 0.3, "extracurricular": 0.2},
 "academicParts": {"gpa": 0.4, "sat": 0.4, "english": 0.2},
 "gpaBand": 0.3, "budgetTolerance": 0.10, "missingValue": 0.5,
 "maxResults": 12, "perCategory": {"reach": 3, "match": 5, "safety": 4},
 "openAdmissionLevels": ["community_college"],
 "aiEnabled": true, "aiTimeoutSeconds": 120, "extracurricularTimeoutSeconds": 60}
```

Key nào thiếu thì dùng giá trị mặc định trong code.

## 5. SAW: xếp hạng

```
Điểm trường = Σ (trọng số_k × giá trị chuẩn hóa_k) / Σ trọng số_k     (k = các tiêu chí có dữ liệu)
```

Mọi trường dùng **chung một bộ trọng số**. Tỷ lệ nhận của trường chỉ để hiển thị, không tham gia chấm điểm.

### Ví dụ tính tay: Oregon State University

Học sinh: GPA 3.5, SAT 1300, IELTS 6.5, ngoại khóa 3.2/4, ngân sách 60.000 USD.
Trường: GPA TB 3.70, SAT 1140–1400, tổng chi phí 57.168 USD, IELTS tối thiểu 6.0.

| Tiêu chí | Giá trị [0, 1] | Trọng số | Tích |
|---|---|---|---|
| Học thuật | GPA (3.5 − 3.7 + 0.3) / 0.6 = 0.167; SAT (1300 − 1140) / 260 = 0.615; tiếng Anh min(1, 6.5 / 6.0) = 1; 0.4 × 0.167 + 0.4 × 0.615 + 0.2 × 1 = 0.513 | 0.5 | 0.256 |
| Tài chính | (60000 − 57168) / 60000 = 0.047 | 0.3 | 0.014 |
| Ngoại khóa | 3.2 / 4 = 0.8 | 0.2 | 0.160 |
| **Tổng** | | | **0.431** |

Nhóm: GPA thấp hơn TB 0.2 (< 0.3) → ngang; SAT nằm giữa 25%–75% → ngang. Kết quả: **match**. Tiếng Anh không dùng để phân nhóm. Ví dụ này có test tự động `Score_OregonStateExample_MatchesHandCalculation`.

## 6. Điểm ngoại khóa 0–4

### Công thức

Mỗi hoạt động i có chất lượng q_i trong [0, 1]:

```
q_i = 0.40 × (phạm vi / 5) + 0.35 × vai trò + 0.25 × min(số tháng, 24) / 24
      + 0.10 nếu tổ chức uy tín   (q_i tối đa 1)

Điểm ngoại khóa = min(4, Σ (0.5 + 0.5 × q_i))   trên tối đa 4 hoạt động có q cao nhất
```

| Thuộc tính | Giá trị | Nguồn |
|---|---|---|
| Phạm vi | 1 trường · 2 quận/huyện · 3 tỉnh/thành · 4 quốc gia · 5 quốc tế | Học sinh khai trên form, LLM đối chiếu mô tả |
| Vai trò | thành viên 0.3 · phó/trưởng ban 0.6 · chủ nhiệm/đội trưởng 0.8 · sáng lập 1.0 | LLM đọc từ chữ học sinh nhập |
| Số tháng | Từ 24 tháng trở lên được tối đa. Không khai: lấy 0.5 | Ngày bắt đầu, kết thúc trên form |
| Tổ chức uy tín | UNICEF, cơ quan cấp quốc gia, cuộc thi chính thức, đại học… | LLM đọc từ tên tổ chức |

Mỗi hoạt động được tính đóng góp **từ 0.5 đến 1.0** điểm, nên 4 hoạt động tốt nhất cho tối đa 4 điểm. Có hoạt động thì ít nhất được 0.5, tránh trường hợp hoạt động yếu bị tính bằng 0.

Hoạt động gồm loại `extracurricular` (CLB, tình nguyện, thể thao) và `experience` (việc làm, thực tập), giống mục Activities của Common App. Thành tích loại "thực tập" cũng tính như hoạt động.

### Điểm thưởng giải thưởng (đề xuất, chờ thầy duyệt)

Giải thưởng không có vai trò hay thời gian tham gia nên không dùng công thức q. LLM chỉ đọc **cấp giải** (1 trường … 5 quốc tế) từ tên giải, đơn vị trao và mô tả:

```
Thưởng giải = min(0.5, Σ 0.05 × cấp giải)   trên tối đa 3 giải cấp cao nhất
Điểm ngoại khóa = min(4, Σ điểm hoạt động + thưởng giải)
```

Ví dụ: sáng lập CLB cấp tỉnh 24 tháng (0.92) + giải cấp tỉnh (0.15) + giải quốc gia (0.20) = **1.27**. Thưởng giải tối đa 0.5 để giải thưởng chỉ bổ sung, không thay được hoạt động dài hạn. Bỏ phần này chỉ cần đặt hệ số 0.05 về 0.

Code quy điểm thành mức rồi đưa mức cho LLM; `fact_check` bỏ giải thích nào nói sai mức:

| Mức | Điểm |
|---|---|
| Mạnh | ≥ 3 |
| Trung bình | ≥ 2 |
| Còn mỏng | < 2 |

### Ví dụ tính tay

| Hồ sơ | Tính | Điểm |
|---|---|---|
| Không có hoạt động | | **0** |
| Thành viên CLB trường, 3 tháng | q = 0.08 + 0.105 + 0.031 = 0.216 → 0.5 + 0.108 | **0.61** |
| Sáng lập CLB cấp tỉnh, 24 tháng | q = 0.24 + 0.35 + 0.25 = 0.84 → 0.5 + 0.42 | **0.92** |
| 3 hoạt động thành viên cấp trường, 12 tháng | q = 0.08 + 0.105 + 0.125 = 0.31 → 3 × 0.655 | **1.97** |
| 4 hoạt động chủ nhiệm cấp quốc gia, 24 tháng | q = 0.32 + 0.28 + 0.25 = 0.85 → 4 × 0.925 | **3.70** |
| 4 hoạt động sáng lập cấp quốc tế, tổ chức uy tín | q = 1 (đã chặn) → 4 × 1.0 | **4.00** |

Các ví dụ này có test tự động `test_score_matches_hand_calculation`.

### LLM đọc thuộc tính, code giữ lan can

LLM chỉ **phân loại** (chọn trong danh sách có sẵn qua JSON schema), không chấm điểm. Code giữ 2 lan can:

1. **Phạm vi chỉ được giữ hoặc hạ, không được nâng**: phạm vi dùng để tính = min(LLM đọc, học sinh khai). Ví dụ thật khi chạy Qwen3 8B: học sinh khai "CLB tiếng Anh của lớp 11A1" ở phạm vi 5 (quốc tế), LLM đọc mô tả và hạ xuống 1 (trường).
2. **LLM lỗi hoặc bỏ sót hoạt động**: đọc vai trò bằng từ khóa ("sáng lập", "phó", "chủ nhiệm"…) và giữ phạm vi học sinh khai. Điểm vẫn được tính.

## 7. LLM giải thích và kiểm tra giải thích

LLM nhận số liệu đã tính sẵn (nhóm, so sánh GPA/SAT, chi phí rẻ hay đắt nhất nhóm, mức ngoại khóa) và viết 3 câu: học thuật, tài chính, ngoại khóa. LLM không đổi được danh sách trường, nhóm hay thứ tự: .NET luôn giữ thứ tự SAW và chỉ lấy lời giải thích của LLM.

Qwen3 8B hay so sánh số sai (ví dụ nói "SAT 1380 dưới mốc 25%" khi khoảng là 1320–1480), nên giải thích được kiểm tra 2 lớp:

| Lớp | Kiểm tra | Sai thì |
|---|---|---|
| `fact_check.py` (advisor) | Câu nói về SAT, GPA, ngân sách, tiếng Anh, nhóm, mức ngoại khóa, độ rẻ trong nhóm có khớp số liệu không; có nêu đúng **tiêu chí quyết định nhóm** không (vd. thử sức vì GPA thì phải nhắc GPA); có hứa hẹn "chắc chắn", "đảm bảo" không | Bỏ giải thích của LLM |
| `AiOutputGuard.cs` (.NET) | Mã trường có trong danh sách không; mọi con số có trong dữ liệu không; có nêu tỷ lệ đậu không | Thay bằng câu soạn sẵn từ số liệu |

## 8. Khi AI lỗi

| Tình huống | Hệ thống làm gì |
|---|---|
| LLM không đọc được hoạt động | Đọc vai trò bằng từ khóa, vẫn tính điểm |
| Advisor tắt hoặc quá 60 giây khi tính ngoại khóa | Dùng điểm ngoại khóa lần trước; chưa có thì bỏ tiêu chí ngoại khóa, kèm cảnh báo |
| LLM không viết được giải thích hoặc quá 120 giây | Danh sách vẫn theo SAW, giải thích soạn sẵn, kèm cảnh báo |
| Giải thích sai số liệu | Thay bằng giải thích soạn sẵn cho đúng trường đó |

Thuật toán chọn trường (bước 2–5) không phụ thuộc LLM, nên **cùng hồ sơ luôn ra cùng danh sách trường**.

## Trạng thái hiện tại

| Phần | Trạng thái |
|---|---|
| Lọc, phân nhóm, SAW, giải thích 3 câu, kiểm tra giải thích | Đã chạy trong nút "Lọc trường" |
| Điểm ngoại khóa trong nút: hoạt động `extracurricular`, `experience` và giải thưởng `award`, chỉ tính lại khi hoạt động đổi, trả bảng chi tiết | Đã chạy (điểm thưởng giải chờ thầy duyệt) |
| Phạm vi và số tháng học sinh tự khai (lan can "chỉ được giữ hoặc hạ") | Chờ cột `impact_level`, `duration_months` trong bảng `profile_activities`; hiện LLM tự đánh giá phạm vi từ mô tả, số tháng lấy mức trung tính |
