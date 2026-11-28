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
