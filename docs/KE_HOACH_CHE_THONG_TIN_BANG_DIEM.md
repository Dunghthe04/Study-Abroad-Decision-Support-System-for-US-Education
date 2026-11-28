# Kế hoạch: che chắc chắn thông tin cá nhân trong bảng điểm trước khi gửi Claude

Ngày viết: 28/11/2026 · Trạng thái: **kế hoạch, chưa sửa code**

## 1. Vấn đề

Luồng tải bảng điểm hiện tại:

```
.NET ProfileFlowService.UploadDraftTranscriptAsync
  → POST advisor /api/v1/profile/transcript-ocr (chỉ có file base64)
      Bước A: Ollama trong máy chép ảnh → chữ (ảnh không rời máy)
      Bước B: _parse_source
          mask_personal_info(source)          ← che bằng regex theo NHÃN + hình dạng
          read_term_tables(source)            ← đọc bảng bằng code, đọc được thì KHÔNG gọi Claude
          _parse_with_parser → Claude         ← chỉ khi bảng lộn xộn
```

Regex chỉ che được giá trị đứng sau nhãn ("Họ và tên: …"), ngày, email, dãy số dài.
**Lỗ hổng:** họ tên **đứng riêng một dòng, không có nhãn** (ví dụ OCR trả về "NGUYỄN VĂN A" ở đầu trang hay ở lời phê), hoặc nhãn bị OCR đọc sai ("Ho va tèn"), thì vẫn bị gửi sang Claude.

Mục tiêu: bịt lỗ này **mà không thêm model nào**.

## 2. Hướng làm (2 lớp mới, regex cũ giữ nguyên làm lớp giữa)

```
chữ OCR
  → [MỚI 1] mask_known_values   : che đúng họ tên / ngày sinh / email / SĐT mà hệ thống ĐÃ BIẾT của chủ hồ sơ
  → [CŨ]    mask_personal_info  : regex theo nhãn + hình dạng (như hiện nay)
  → [MỚI 2] keep_score_lines    : chỉ giữ dòng có ích cho việc đọc điểm, bỏ phần còn lại
  → read_term_tables / Claude
```

- **Lớp 1, che theo giá trị đã biết:** bảng điểm là của chính chủ hồ sơ. DB đã có họ tên, ngày sinh, email, số điện thoại của người đó, nên không cần đoán chữ nào là tên, chỉ cần tìm đúng chuỗi đó mà che.
- **Lớp 2, danh sách trắng:** thay vì "gửi hết rồi che cái nguy hiểm", ta "chỉ gửi cái cần". Bước B chỉ cần dòng môn – điểm – lớp – kỳ. Dòng nào không gửi thì không thể lộ.

## 3. Phần 1: che theo giá trị đã biết

### 3.1 .NET (file của Việt)

**`backend/src/StudyAbroad.Application/Advisor/ITranscriptOcrClient.cs`**
- Thêm record:
  ```csharp
  /// Thông tin cá nhân đã biết của chủ hồ sơ, advisor chỉ dùng để che trước khi gửi LLM, không lưu, không log
  public record TranscriptKnownPersonalInfo(
      IReadOnlyList<string> Names,     // họ tên chủ hồ sơ (+ phụ huynh đã liên kết, xem câu hỏi Q1)
      DateOnly? DateOfBirth,
      string? Email,
      string? Phone);
  ```
- Thêm tham số cuối, có mặc định, vào `TranscriptOcrRequest`: `TranscriptKnownPersonalInfo? KnownPersonalInfo = null`. Có mặc định nên không vỡ chỗ gọi cũ và test cũ.

**`backend/src/StudyAbroad.Application/Profile/Flow/ProfileFlowService.cs` → `UploadDraftTranscriptAsync`**
- Trước khi gọi OCR, dựng `KnownPersonalInfo` từ `users.GetUserById(userId)`: `FullName`, `DateOfBirth`, `Email`, `Phone`.
- `userId` ở đây là **chủ hồ sơ**. Khi phụ huynh làm thay con, `ActingIdentityMiddleware` đã đổi thành id của con, nên thông tin gửi đi là của **con**, đúng người có tên trên bảng điểm.
- `users` null (test cũ) thì gửi `null`, advisor chạy như hiện nay.
- Không ghi log các giá trị này.

**`backend/src/StudyAbroad.Infrastructure/Advisor/TranscriptOcrClient.cs`**: không cần sửa. Client gửi nguyên record bằng `PostAsJsonAsync`, nên trường mới tự lên JSON dạng camelCase (`knownPersonalInfo`, `dateOfBirth` ở dạng `"2008-03-05"`).

Upload trong chat cũng đi qua `UploadDraftTranscriptAsync`, nên **form và chat cùng được che**, không phải sửa riêng bên chat.

### 3.2 Advisor

**`advisor/app/schemas/transcript_ocr.py`**
- Thêm model `KnownPersonalInfo(CamelModel)` gồm:
  - `names: list[str]`: tối đa 5 tên, mỗi tên ≤ 200 ký tự;
  - `date_of_birth: date | None`;
  - `email: str | None` (≤ 320);
  - `phone: str | None` (≤ 30).
- Thêm vào `TranscriptOcrRequest`: `known_personal_info: KnownPersonalInfo | None = None`.

**`advisor/app/services/pii_mask.py`**: thêm hàm thuần `mask_known_values(text, known) -> str`. **Không đụng `mask_outgoing` của Dũng.**

| Loại | Cách so | Thay bằng |
|---|---|---|
| Họ tên | So trên bản **bỏ dấu, chữ thường** (dùng lại `_plain_keep_length`), khoảng trắng giữa các chữ linh hoạt. Che **cả họ tên** và **2 chữ cuối** (tên gọi, ví dụ "Minh Anh"). **Không che một chữ đơn lẻ** (xem dưới) | `[HỌ TÊN]` |
| Ngày sinh | Mọi cách viết của đúng ngày đó: `5/3/2008`, `05/03/2008`, `05-03-2008`, `05.03.2008`, `ngày 5 tháng 3 năm 2008` | `[NGÀY SINH]` |
| Số điện thoại | So **chỉ trên chữ số** (bỏ cách, chấm, gạch); `+84` và `84` coi như `0` | `[LIÊN HỆ]` |
| Email | Không phân biệt hoa thường | `[EMAIL]` |

- **Vì sao không che từng chữ:** tên "Nguyễn Văn Anh" mà che chữ "Anh" thì môn "Tiếng **Anh**" thành "Tiếng [HỌ TÊN]", mất điểm môn đó. Tương tự với "Văn" (Ngữ văn), "Hóa", "Sử". Chỉ che chuỗi từ 2 chữ liền nhau trở lên, khớp trọn ranh giới chữ.
- Tên chỉ có 1 chữ (dữ liệu lạ) thì bỏ qua, không che.

**`advisor/app/services/transcript_reader.py`**
- `read_transcript` truyền `request.known_personal_info` xuống `_read_file` → `_read_pdf` / `_read_images` → `_parse_source`. Đi qua cả 2 đường: PDF có chữ (dòng 461) và ảnh (dòng 533).
- Thứ tự trong `_parse_source`:
  ```python
  source = mask_known_values(source, known)          # MỚI 1
  source = mask_personal_info(source)                # như cũ
  source = keep_score_lines(source)[:MAX_SOURCE_CHARS]  # MỚI 2
  ```
- Hậu kiểm `_check_parsed` / `validate_parsed` dùng đúng `source` đã lọc. Ta chỉ bỏ dòng không có điểm, nên tập số để đối chiếu không đổi.

## 4. Phần 2: chỉ gửi dòng có ích (danh sách trắng)

**`advisor/app/services/transcript_text.py`** (file của Việt): thêm `keep_score_lines(text) -> str`.

**Giữ** một dòng nếu thỏa một trong các điều kiện:
1. **Dòng bảng (`|`)** có số kiểu điểm (`8`, `8.5`, `7,25`, `Đ`, `CĐ`; dùng lại `_VALUE_RE`), **hoặc** là dòng tiêu đề bảng (ô đầu là "Môn"/"Subject", dùng lại `_header_lead`), **hoặc** dòng kẻ `|---|`.
2. **Dòng có lớp / kỳ / năm học:** "Lớp 10", "HK1", "Học kỳ II", "Cả năm", "Năm học 2023-2024", "Semester". Dùng lại `_GRADE_LINE_RE`, `_TERM_LABEL_RE`, nhưng tìm **ở bất kỳ đâu trong dòng**, vì có dòng kiểu "Họ và tên: [HỌ TÊN]   Lớp: 10A1".
3. **Dòng chữ có số kiểu điểm** (ví dụ "Toán 8.5   Văn 7.0" khi OCR không dựng bảng).

**Bỏ** mọi dòng còn lại: tên trường, quốc hiệu, họ tên đứng riêng, địa chỉ, lời phê của giáo viên, "Hiệu trưởng", chữ ký, dấu.

- **Dòng bảng không có số** như `| Họ và tên | [HỌ TÊN] |` cũng bị bỏ.
- **Lưu ý:** một dòng có số mà vẫn chứa tên (ví dụ "Nguyễn Văn A  8.5") thì vẫn giữ. Đó là việc của lớp 1 và lớp regex: dòng nào giữ lại thì đã đi qua hai lớp che rồi.
- **Lọc xong không còn dòng điểm nào:** trả cảnh báo "không thấy bảng điểm" như hiện nay. Không có gì để gửi Claude thì không gọi Claude.

## 5. Kiểm thử

| Nơi | Ca test |
|---|---|
| `advisor/tests/test_pii_mask.py` | `mask_known_values`: tên đứng riêng viết HOA; tên không dấu; tên gọi "Minh Anh"; **"Tiếng Anh" không bị che khi tên là "Nguyễn Văn Anh"**; tên 1 chữ thì không che; ngày sinh dạng `05-03-2008` và "ngày 5 tháng 3 năm 2008"; SĐT `+84 912.345.678` khớp `0912345678`; email hoa thường; **điểm và năm học không đổi**; `known=None` trả nguyên văn |
| `advisor/tests/test_transcript_text.py` | `keep_score_lines`: giữ tiêu đề bảng, dòng kỳ, dòng "Lớp 10", dòng điểm; bỏ quốc hiệu, tên trường, họ tên đứng riêng, lời phê, chữ ký, `\| Họ và tên \| … \|`; đầu vào chỉ có tiêu đề thì ra rỗng |
| `advisor/tests/test_transcript_ocr_api.py` | Request có `knownPersonalInfo` dạng camelCase được nhận; parser Claude giả **ghi lại nội dung nhận được** → không chứa tên, ngày sinh, SĐT; request cũ không có trường mới vẫn chạy |
| `backend/tests/.../ProfileFlowServiceTests.cs` | OCR client giả ghi lại request → `KnownPersonalInfo` đúng họ tên, ngày sinh, email, SĐT của chủ hồ sơ; `users` null thì gửi null |
| Đo trên bảng điểm thật | Chạy bộ ảnh `G:\DO_AN\học_ba` (script ở `advisor/.ocr-eval`) **trước và sau**. Điều kiện qua: **số ô điểm đúng không giảm**, và chữ gửi sang Claude không còn họ tên học sinh |

## 6. File sẽ sửa

| File | Việc | Chủ file |
|---|---|---|
| `backend/src/StudyAbroad.Application/Advisor/ITranscriptOcrClient.cs` | thêm record + tham số | Việt |
| `backend/src/StudyAbroad.Application/Profile/Flow/ProfileFlowService.cs` | dựng và gửi `KnownPersonalInfo` | Việt |
| `advisor/app/schemas/transcript_ocr.py` | thêm model | Việt |
| `advisor/app/services/pii_mask.py` | thêm `mask_known_values` (không đụng `mask_outgoing` của Dũng) | Việt |
| `advisor/app/services/transcript_text.py` | thêm `keep_score_lines` | Việt |
| `advisor/app/services/transcript_reader.py` | truyền `known` xuống, đổi thứ tự che trong `_parse_source` | Việt |
| 4 file test ở trên | thêm ca | Việt |

Không thêm bảng, không thêm cột, không thêm model, không thêm thư viện.

## 7. Câu hỏi cần Việt chốt trước khi làm

- **Q1.** Có che thêm **họ tên phụ huynh đã liên kết** không? Học bạ THPT thường có dòng "Họ tên cha/mẹ". Regex đã che khi có nhãn, nhưng lỡ OCR làm mất nhãn thì lọt. Lấy qua `parentLinks.GetByStudentAsync` (liên kết còn hiệu lực, phụ huynh có tài khoản). **Đề xuất: có.**
- **Q2.** Lớp 1 có áp luôn cho **chat** không? Tức là che họ tên trong câu chat trước khi gửi Claude, ở `mask_outgoing`. Phần này nằm trong file Dũng vừa sửa và cần .NET gửi thêm thông tin vào request chat. **Đề xuất: để sau, hỏi Dũng.**

## 8. Thứ tự làm và commit (mỗi phần một commit, không push)

1. Advisor: `mask_known_values` + test → commit.
2. Advisor: `keep_score_lines` + test → commit.
3. Advisor: schema + `transcript_reader` nối hai lớp + test API → commit.
4. .NET: record + `ProfileFlowService` + test → commit.
5. Chạy bộ ảnh `học_ba` trước/sau, ghi kết quả vào cuối file này.
6. Cập nhật mục 5.7 trong `docs/TONG_HOP_BAO_MAT_VIET.md`.

## 9. Kết quả (28/11)

Việt chốt: **Q1 có** (che cả họ tên phụ huynh đã liên kết), **Q2 để sau** (chưa áp cho chat).

| Phần | Commit |
|---|---|
| `mask_known_values` + test | `00daed5e` |
| `keep_score_lines` + test | `31cd05f8` |
| Schema + `transcript_reader` nối các lớp + test API | `1996c9fb` |
| .NET gửi `KnownPersonalInfo` (chủ hồ sơ + phụ huynh liên kết Active) + test | `ce8288d5` |
| Sửa sau khi đo: giữ ô của bảng chép dọc | `8c4dc1da` |

**Cách đo.** Không tìm thấy script benchmark cũ: chỉ còn file `.pyc`, không còn nhãn đúng. Vì vậy đo không gọi LLM, trên 30 bản chép bước A đã lưu (`.ocr-eval/bench_check/raw/replay_ollama_glm_ocr`, 10 ảnh × 3 lần chạy). Lấy những ô điểm mà Claude đã đọc ra trước đây (file `.json`) và có mặt trong chữ gốc, rồi kiểm tra ô đó còn trong chữ sau khi lọc hay không. So thêm `read_term_tables` trước và sau lọc.

| | Ô điểm còn lại | Ký tự gửi đi |
|---|---|---|
| Bản đầu (`31cd05f8`) | **534/741**: mất trắng 5 ảnh | 77.697 → 31.077 |
| Sau khi sửa (`8c4dc1da`) | **741/741** | 77.697 → 37.614 |

- **Lỗi của bản đầu:** glm-ocr hay chép bảng theo kiểu **dọc**, mỗi ô một dòng ("Toán" / "8,2" / "8,2" / "8,2"). Luật lọc theo dòng bỏ mất những dòng chỉ có một con số.
- **Đã sửa:** giữ dòng chỉ có điểm; giữ dòng tiêu đề cột ngắn (HKý I, CN, ĐTB…); giữ dòng nhãn ngắn (≤ 6 chữ) đứng ngay trước dòng điểm, tức tên môn. Tên giáo viên đứng sau điểm vẫn bị bỏ. Đã thêm test.
- `read_term_tables` cho kết quả giống hệt nhau trước và sau lọc ở cả 30 bản.
- Toàn bộ test advisor pass (2495). Toàn bộ unit test backend pass.
- **Chưa đo lại F1 đầu cuối có gọi Claude:** việc này cần nhãn đúng và tốn token. Phép đo trên chỉ chứng minh không ô nào bị cắt khỏi chữ gửi đi.

### 9.1 Chạy thật trên ảnh `học_ba` (28/11)

- **Cách chạy:** chạy bước A thật bằng glm-ocr trên 11 ảnh, rồi chặn ngay trước chỗ gửi Claude để lấy đúng chữ sẽ gửi đi. Lần này không gọi Claude.
- **Báo cáo:** `advisor/.ocr-eval/che_ten_28_11/BAO_CAO.md`, gồm chữ gốc, chữ gửi Claude và các dòng bị bỏ của từng ảnh.
- **Trong chữ gốc có:** họ tên học sinh ("Họ tên học sinh: …", "Họ và tên: …", "Học sinh: …"), ngày sinh, mã học sinh, tên giáo viên bộ môn, tên giáo viên chủ nhiệm.
- **Lỗ hổng tìm ra:** OCR chép lại tiêu đề trang nhưng làm mất nhãn, còn "Nguyễn Thanh Trā Lớp: 12Tg Nám học 2010. 2011". Lớp 2 không bắt được vì không có nhãn. Lớp 3 lại giữ cả dòng vì có "Lớp".
- **Đã sửa (commit kế tiếp sau `75d9945d`):** dòng không có điểm, chỉ có lớp/kỳ/năm học thì chỉ giữ từ chữ "Lớp…" trở đi. Dòng bảng `| … |` vẫn giữ nguyên.
- **Sau khi sửa:** không còn họ tên, ngày sinh hay mã học sinh nào trong chữ gửi đi của cả 11 ảnh. Các cụm chữ viết hoa còn lại chỉ là tên môn ("Tiếng Anh", "Tiếng Việt").
- **Lớp 1 (họ tên .NET gửi kèm):** che được cả khi OCR chép sai dấu ("Trā" so với "Trà").
- **Số ô điểm:** vẫn đủ 741/741. Toàn bộ test advisor pass (2497).

### 9.2 Gửi Claude thật để kiểm điểm đọc ra (28/11)

- **Cách chạy:** lấy chữ bước A đã lưu của 11 ảnh (mục 9.1), đưa qua đúng `_parse_source` (che 3 lớp rồi gửi `claude-haiku-5-5`). Ghi lại nguyên văn mọi request gửi Claude vào `advisor/.ocr-eval/che_ten_28_11/<ảnh>.claude.json`.
- **Đem so với:** kết quả cũ trước khi có lớp che (`bench_check/.../<ảnh>__r1.json`, chạy ngày 22/11, cùng model).
- **Lọt thông tin:** không request nào chứa họ tên, ngày sinh hay mã học sinh đã biết trong ảnh.

| Ảnh | Ô cũ | Ô mới | Trùng | Ghi chú |
|---|---|---|---|---|
| 500289857, diem-so-hoc-ba, hoc-ba-8848, Hoc-ba-dep, images, images (1) | 208 | 208 | 208 | giống hệt |
| k1-155194… | 23 | 24 | 23 | mới đọc thêm GDQP HK2 |
| 20200701 | 12 | 10 | 10 | mới bỏ GDCD, Tin học. Bản cũ ghi Tin học = 8.5, sai: đó là ĐTB, Tin đúng là 9.6 |
| 3794687… | 26 | 36 | 24 | mới bỏ dòng "Điểm TB" (đúng), nhưng tạo thêm kỳ **"HK3" bịa từ cột CN** (sai, xem dưới) |
| 499489625… | 16 | 16 | 5 | bảng dọc nhiều cột, cả bản cũ lẫn bản mới đều đọc lệch cột |
| 500289857_v1 | — | 18 | — | không có bản cũ để so |
| **Tổng** | 285 | 312 | **270** | |

- **Độ dao động sẵn có:** chính bản cũ chạy 2 lần (r1 với r2) cũng chỉ trùng 269/285 ô. Mức trùng giữa bản mới (đã che) và bản cũ là 270/285, ngang mức đó.
- **Ở 2 ảnh lệch nhiều (20200701, 499489625):** phần bảng điểm trong chữ gửi đi giống hệt chữ gốc. Chỗ lệch đến từ Claude đọc bảng khó, không đến từ việc che.
- **Kết luận:** che thông tin không làm sai điểm đọc ra.
- **Lỗi có từ trước, không do che:** từ commit `27d22586` (kiểm tra kết quả bước B, sau ngày chạy bộ cũ), `_resolve_term` chỉ nhận "Cả năm / tổng kết", không nhận **"CN"**.
  - Khi Claude trả về kỳ "CN", bước kiểm tra báo "tên kỳ không hiểu được" và bắt thử lại.
  - Lần thử lại, Claude hoặc giữ "CN" (k1, hoc-ba-8848, diem-so: tốn thêm 1 lần gọi), hoặc **đổi thành "HK3"** (3794687: 12 ô điểm cả năm bị nhận là một kỳ).
  - Nên sửa: coi "CN" là dòng cả năm và bỏ đi.
- **OCR mất dấu thập phân** ("85" thay cho "8.5", diem-so, Hoc-ba-dep): bước kiểm tra báo ngoài thang 10. Kết quả cuối giống bản cũ.

### 9.3 Cơ chế hiểu tên kỳ khi mỗi trường ghi một kiểu (28/11)

Không sửa kiểu "gặp chữ nào thêm chữ đó" (thêm "CN" rồi mai lại "TBcn", "T.Kết"...). Làm 3 lớp, lớp sau đỡ lớp trước:

- **Lớp 1, Claude phân loại theo nghĩa:** mỗi kỳ Claude trả thêm `kind` = `hk1` / `hk2` / `ca_nam` / `khac`. Code tin `kind` trước, chữ tên kỳ chỉ là dự phòng. Tên lạ như "Đợt I", "Giai đoạn cuối", "HKý I" vẫn vào đúng HK1/HK2. Kỳ `ca_nam` bỏ đi dù tên viết thế nào.
- **Lớp 2, R7 không ép đổi tên:** trước đây R7 bắt Claude "ghi rõ HK1/HK2", ép quá nên Claude bịa ra "HK3". Giờ R7 chỉ báo lỗi khi `kind` sai giá trị hoặc trái với chữ ghi rõ (ví dụ tên "HK1" mà `kind` = `hk2`). Kỳ `khac` được giữ lại kèm cảnh báo, học sinh tự chọn tên kỳ ở bước 2 (màn hình đã có sẵn chỗ chọn).
- **Lớp 3, kiểm bằng số:** trong cùng lớp/năm học có từ 3 kỳ trở lên, kỳ đáng ngờ (không phải HK1/HK2, hoặc trùng học kỳ) mà ít nhất 80% môn (tối thiểu 3 môn) có điểm ≈ (HK1 + 2·HK2)/3 hoặc ≈ (HK1 + HK2)/2 (lệch ≤ 0,1) thì là cột cả năm, bỏ đi. Lớp này không cần biết tên cột.
- **Dự phòng khi không có `kind`** (bảng code tự đọc, không qua Claude): chữ nhận thêm CN, TBcn, TB năm, Tổng kết, Annual... Học bạ THPT không có học kỳ 3, nên "HK3" không còn bị đổi thành HK1 (trước đây ghi đè HK1 thật). Học kỳ 3, 4 của ĐH vẫn tính thành Năm 2 như cũ.
- **Luật gộp cùng học kỳ:** cùng một tệp, học bạ không ghi lớp/năm, Claude trả cùng một học kỳ hai lần dưới hai tên ("Kết quả học tập học kỳ 1" và "HK1") thì gộp thành một kỳ nếu không môn nào mâu thuẫn điểm. Có mâu thuẫn thì để riêng.

Commit: `cf34c42d` (3 lớp), `71b57255` (luật gộp). Test mới `advisor/tests/test_transcript_term_kind.py` (15 test), toàn bộ advisor 2512 test pass.

**Chạy lại 11 ảnh với `claude-haiku-5-5`** (chữ bước A như mục 9.2, so với bộ cũ chưa che, 285 ô):

| Lần chạy | Trùng | Khác điểm | Chỉ có ở bản cũ |
|---|---|---|---|
| Trước khi sửa (mục 9.2) | 270/285 | 9 | 6 |
| Sau sửa, lần 1 (chưa có luật gộp) | 265 | 14 | — |
| Sau sửa, lần 2 (`che_ten_28_11/lan2/`) | **274/285** | 7 | 4 |

- Mức dao động sẵn có (bộ cũ chạy 2 lần) là 269/285, lần 2 cao hơn mức đó.
- **3794687:** hết kỳ "Lớp 12 HK3" bịa ra, HK1 không còn bị ghi đè, chỉ gọi Claude 1 lần (trước 2 lần). Cả 2 lần chạy trùng 24/24 ô với bản cũ.
- **hoc-ba-8848, k1:** "CN" được nhận là cả năm, không còn bị R7 bắt thử lại, giảm từ 2 xuống 1 lần gọi. hoc-ba-8848 24/24; k1 lần 2 23/23 (lần 1 Claude bỏ dòng OCR rác "Ngoại ngũ / Gung...inh", còn 21).
- **20200701:** lần 1 bị tách HK1 thành 2 kỳ, có luật gộp thì lần 2 còn 1 kỳ, 12 ô.
- **Không lọt thông tin cá nhân** ở cả 3 lần chạy.
- **Còn lệch, không do thay đổi này:** 499489625 (bảng dọc nhiều cột, Claude dao động 5 / 3 / 8 ô trùng qua các lần); diem-so, Hoc-ba-dep (OCR mất dấu thập phân "85" thay "8.5", R5 bắt như cũ).

### 9.4 OCR mất dấu thập phân và chạy với ảnh mới (28/11)

Không vá theo từng con số. Dùng một luật chung theo thang điểm:

- **Luật:** trên thang 10, số nguyên từ 11 đến 100 không thể là điểm đúng. Gặp số như vậy thì hiểu là OCR làm mất dấu thập phân: "85" thành 8.5, "100" thành 10.0 (`lost_decimal_score` trong `transcript_validate.py`). Số 10 trở xuống, số trên 100 (năm học "2010"), số có số 0 đứng đầu thì giữ nguyên.
- **Chỉ áp dụng khi chắc là thang 10** (`decimal_may_be_lost`): học bạ THPT, hoặc bảng có ô điểm thập phân thật ≤ 10. Bảng ĐH hay bảng chưa rõ cấp học mà toàn số nguyên thì có thể là thang 100, nên không tự chia 10.
- **R5 vẫn giữ nguyên:** số phải có trong chữ OCR. Claude chép "85" hay "8.5" đều được chấp nhận. Số bịa ra (không có trong chữ) vẫn bị bắt.
- **Báo học sinh:** khi đã đọc lại ô nào, kết quả có cảnh báo "ảnh bị mất dấu thập phân ở N ô, đã đọc lại theo thang 10 (ví dụ…). Bạn so lại với học bạ giúp nhé." Ở bước 2, học sinh sửa được từng ô.
- **Lớp 3 (nhận cột cả năm bằng số)** chạy sau khi đã đọc lại, nên cột CN bị mất dấu vẫn được nhận ra và bỏ.
- **Prompt:** dặn Claude cứ chép nguyên văn "85", hệ thống tự hiểu, để Claude không đoán.
- **Dòng tiêu đề kỳ bị OCR chép sai** ("Hoc kg I Hoc kg II Ca nám"): danh sách trắng nhận "hoc k" cộng tối đa 1 chữ, không liệt kê từng kiểu sai. Trước đây dòng này bị bỏ nên Claude mất cột HK1, HK2.

Commit: `ae4f8273`, `84084d31` (luật + điều kiện THPT), `67b159ac` (tiêu đề kỳ). Test mới `advisor/tests/test_transcript_lost_decimal.py` (7 test) và 1 test trong `test_transcript_text.py`. Toàn bộ advisor 2520 test pass, ruff sạch.

**Chạy lại 11 ảnh cũ** (`che_ten_28_11/lan3/`, `claude-haiku-5-5`, so với bộ cũ 285 ô):

| Lần chạy | Trùng | Khác điểm | Chỉ có ở bản cũ |
|---|---|---|---|
| Mục 9.3, lần 2 | 274/285 | 7 | 4 |
| Sau sửa mất dấu (lan3) | **278/285** | 5 | 2 |

- **diem-so-hoc-ba:** 24 ô (trước 18–19). Đọc lại 6 ô, ví dụ "Công nghệ 95 → 9.5", "Sinh 100 → 10".
- **Hoc-ba-dep:** 24 ô (trước 4). Đọc lại 20 ô.
- Cả hai chỉ gọi Claude 1 lần (trước đó R5 bắt thử lại).
- **Còn lệch như cũ:** 499489625 (bảng dọc); 20200701 Tin học (bản cũ ghi 8.5 là sai).

**4 ảnh mới chưa từng chạy** (lấy từ `G:\DO_AN\học_ba`, bước A bằng glm-ocr):

| Ảnh | Kết quả | Ghi chú |
|---|---|---|
| 2452048341 (học bạ Lớp 8) | 2 kỳ, 18 ô | đọc ổn |
| anh-chup-man-hinh… (ảnh chụp bài đăng, học bạ THPT) | 2 kỳ, 20 ô | mất dấu ở **mọi** ô, đọc lại 22 ô. Có 1 lỗi gán môn: OCR chép tên môn thành "Böl 63 64 66" (có lẽ là Sử), Claude gán thành Toán, rồi dòng "Toàn 42 47 45" ghi đè. Lỗi do tên môn OCR hỏng, không do luật mất dấu |
| 1654860257 | 1 kỳ chưa rõ tên, 10 ô | OCR không chép dòng tiêu đề cột, Claude không biết cột nào là HK1, HK2. Học sinh phải tự chọn kỳ ở bước 2 |
| 740314228 | 0 ô | OCR hỏng, chỉ ra 7 dòng chữ, không có bảng điểm |

**Vấn đề còn lại, phát hiện ở ảnh mới (chưa sửa, cần Việt quyết):**

- **Lọt tên qua lớp lọc dòng (lớp 3):**
  - anh-chup: câu bài đăng Facebook "cô Di Li post toàn bố so diêm" bị giữ.
  - 1654860257: dòng "Vũ Thị Hà 8" bị giữ, vì có tên viết hoa đứng cạnh một con số.
  - Cả hai tên không có nhãn ("Họ tên:"), không có trong dữ liệu .NET gửi kèm, nên lớp 1, 2 không che được. Hướng sửa chung: dòng có chuỗi chữ viết hoa giống họ tên người mà không phải tên môn, chỉ đi kèm 1 số, thì bỏ hoặc che phần tên.
- **OCR mất tiêu đề cột:** có thể suy cột bằng số (cột 3 ≈ (cột 1 + 2·cột 2)/3 thì cột 1, 2 là HK1, HK2), dùng lại cách tính của lớp 3.
