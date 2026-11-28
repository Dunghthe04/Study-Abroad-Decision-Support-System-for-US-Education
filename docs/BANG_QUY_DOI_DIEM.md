# Bảng quy đổi điểm `grade_scale.10` (thang 10 → GPA thang 4)

Người làm: Việt. Liên quan: #4 (hồ sơ), #13 (màn hình quản trị), chat tư vấn (advisor).

## 1. Công thức chuẩn (một công thức duy nhất cho backend, frontend, advisor)

Nguồn mốc: bảng bậc THPT của EducationUSA Vietnam 2025 (Đại sứ quán Hoa Kỳ): https://vn.usembassy.gov/wp-content/uploads/sites/124/2023/01/Basic-Information-on-Grading-in-Vietnam-2025.pdf

1. **Trung bình cả năm từng môn** theo cách tính Việt Nam: `(HK1 + 2×HK2) / 3`. Chỉ có một kỳ thì lấy kỳ đó.
2. **Trung bình thang 10** = trung bình các môn ở bước 1, làm tròn 2 số (nửa lên, 8,125 → 8,13).
3. **Quy đổi một lần** số ở bước 2 sang thang 4 bằng **nội suy tuyến tính** giữa hai mốc kề nhau. Không đổi từng môn rồi mới lấy trung bình.
4. Kết quả làm tròn 2 số, nửa lên (3,625 → 3,63).
5. Chỉ tính **unweighted**. Weighted đã bỏ từ 13/11 (xem `docs/KE_HOACH_GPA_WEIGHTED.md`).

- OCR và bảng điểm giữ **điểm từng môn đúng thang 10**. Chỉ **điểm trung bình tổng** mới đổi sang GPA thang 4.
- Dưới mốc đầu lấy GPA của mốc đầu, trên mốc cuối lấy GPA của mốc cuối.
- DB chưa có dòng `grade_scale.10` (hoặc dòng đó sai luật) thì dùng **bảng mặc định** trong code (`AcademicAnalysisService.GetDefaultScaleConfig`).

Bảng mốc mặc định:

| Mốc (thang 10) | GPA 4 | Điểm chữ |
|---|---|---|
| 0,0 | 0,0 | F |
| 5,0 | 2,0 | C |
| 6,0 | 3,0 | B |
| 8,0 | 3,5 | A |
| 9,5 | 4,0 | A+ |
| 10 | 4,0 | A+ |

Ghi chú: bảng gốc cho F = 1,0; hệ thống để F = 0 theo cách Mỹ. Mốc 0 là sàn để điểm nào cũng có khoảng.

Ví dụ nội suy: 7,0 → 3,0 + (7,0 − 6,0) × (3,5 − 3,0) / (8,0 − 6,0) = **3,25**; 8,5 → **3,63**; 8,7 → **3,73**; 9,0 → **3,83**.

Ví dụ đủ các bước: Toán HK1 8, HK2 9 → (8 + 18)/3 = 8,67; Văn chỉ HK1 7 → 7; Anh 9, 9 → 9. Trung bình = 8,22 → giữa mốc 8,0 và 9,5 → 3,5 + 0,22 × 0,5 / 1,5 = **3,57**.

Code (cùng một luật):

| Nơi | Hàm |
|---|---|
| Backend | `GradeScale.Interpolate`, `AcademicAnalysisService.ConvertScoreToGpa4` / `AverageToGpa4`, `TranscriptGpa.YearAverage` / `SubjectAverage10` / `To4` |
| Frontend | `DEFAULT_GPA10_TO_4`, `toGpa4` (`frontend/src/lib/profile-flow-options.ts`) |
| Advisor | `gpa10_to_4` (`skill_danh_gia.py`), nhận bảng từ backend; không có bảng thì không đoán GPA |

Các công thức cũ **không còn dùng**, đừng chép lại vào tài liệu mới:

- bảng WES tra bậc thang theo cận dưới (9,0 → 4,0; 8,5 → 3,7; 8,0 → 3,5…), bỏ khi đổi sang nội suy;
- `điểm × 0,4`;
- trung bình môn bằng trung bình cộng hai kỳ (HK2 không nhân 2);
- GPA weighted.

## 2. Dữ liệu trong DB

Bảng `app_settings`, key `grade_scale.10`, `value_json`:

```json
{
  "scaleKey": "grade_scale.10",
  "sourceName": "EducationUSA Vietnam 2025 (bậc THPT), nội suy tuyến tính: https://vn.usembassy.gov/wp-content/uploads/sites/124/2023/01/Basic-Information-on-Grading-in-Vietnam-2025.pdf",
  "method": "linear",
  "rules": [
    { "minScore": 0, "maxScore": 4.99, "gpa4": 0, "letterGrade": "F" },
    { "minScore": 5, "maxScore": 5.99, "gpa4": 2, "letterGrade": "C" },
    { "minScore": 6, "maxScore": 7.99, "gpa4": 3, "letterGrade": "B" },
    { "minScore": 8, "maxScore": 9.49, "gpa4": 3.5, "letterGrade": "A" },
    { "minScore": 9.5, "maxScore": 10, "gpa4": 4, "letterGrade": "A+" }
  ]
}
```

- Nội suy chỉ dùng `minScore` → `gpa4`. `maxScore` (mốc kế tiếp − 0,01) và `letterGrade` chỉ để hiển thị.
- `method = "table"` là bảng WES cũ. Chỉ còn dùng để nhận ra hồ sơ đã tính theo bảng cũ khi tính lại (`GradeScale.Lookup`).
- `DbSeeder` thêm bảng mặc định khi DB chưa có, không ghi đè bảng đã sửa.
- Mỗi lần lưu ghi `audit_logs`:
  - `action = "grade_scale.update"`;
  - `entity_type = "app_setting"`;
  - `entity_id = "grade_scale.10"`;
  - `detail_json = {old, new, recomputedProfiles}`.

## 3. Quyền và API

Controller: `AdminSettingsController`, route gốc `api/v1/admin/settings`.

| Phương thức | Đường dẫn | Ai gọi được | Kết quả |
|---|---|---|---|
| GET | `/grade-scale` | admin, moderator | `{config, version, isDefault}`. `isDefault = true` khi DB chưa có bảng, đang dùng bảng mặc định |
| GET | `/grade-scale/history?limit=20` | admin, moderator | Danh sách `{changedAt, actorUserId, actorName, old, new}`, `limit` kẹp trong 1–100 |
| PUT | `/grade-scale` | **chỉ moderator** | Body `{rules:[{minScore, gpa4, letterGrade}], sourceName, version}` |

Kết quả PUT:

- **200** `{setting, recomputedProfiles}` khi lưu xong.
- **400** `{errors:[...]}` khi bảng sai luật (`GradeScale.Validate`), hoặc điểm chữ rỗng hay dài quá 4 ký tự.
- **409** `{errors, current}` khi `version` gửi lên khác bản trong DB, tức là có người vừa sửa. Màn hình hiện cảnh báo và nút "Tải bảng mới nhất".

Ghi chú:

- Server tự tính `maxScore` bằng mốc kế tiếp trừ 0.01, khoảng cao nhất tới 10.
- Quyết định của nhóm: moderator giữ chuẩn, admin giám sát (chỉ xem bảng và lịch sử).

Ngoài ra, người dùng đã đăng nhập đọc bảng đang dùng qua `GET /api/v1/profile/academic/scale-config`.

## 4. Lưu bảng mới thì chuyện gì xảy ra (`GradeScaleSettingService.UpdateAsync`)

1. Kiểm tra luật, rồi so `version` (lệch thì trả 409).
2. Tính lại `overall_gpa` các hồ sơ **lấy GPA từ học bạ**:
   - lấy điểm trung bình từ `transcript_scores`;
   - nếu hồ sơ ghi `gpa_mode = transcript`, hoặc `overall_gpa` hiện tại đúng bằng số nội suy theo bảng cũ (bảng cũ kiểu `table` thì nhận cả số tra bậc thang), thì nội suy lại theo bảng mới.
3. Hồ sơ tự nhập GPA giữ nguyên.
4. Lưu bảng kèm audit log trong cùng một lần.
5. Xoá cache của `IGradeScaleProvider` (cache 10 phút) để mọi chỗ dùng bảng mới ngay.

## 5. Bảng được dùng ở đâu

| Nơi | Cách dùng |
|---|---|
| Backend: GPA từ bảng điểm (`TranscriptGpa`, `AcademicAnalysisService`) | Đọc qua `IGradeScaleProvider` |
| Backend: GPA từ OCR (`ProfileFlowService.OcrGpa4Async`) | Lấy `AverageScoreScale10` OCR trả về, quy đổi theo bảng trong DB. Advisor OCR chỉ trả điểm thang 10, không tự quy đổi |
| Backend → advisor khi chat (`ChatService`) | Gửi `gradeScale` (danh sách rules) mỗi lượt. Bảng lấy từ server, bỏ bảng trình duyệt gửi lên |
| Advisor (`chat_flow`, `skill_danh_gia`, `skill_lo_trinh`, `profile_calc`, `skill_input`) | Cất vào `state.grade_scale`. Lượt không gửi bảng thì giữ bảng cũ. `gpa_table_of` đổi sang danh sách mốc, `gpa10_to_4` nội suy; chỉ GPA tổng thang 10 bị đổi, GPA thang 4 giữ nguyên. Không có bảng thì không đoán GPA |
| Frontend (`useGradeScale`, `toGpa4`) | Tải từ `scale-config`, lỗi thì dùng bảng mặc định `DEFAULT_GPA10_TO_4`. Moderator lưu xong thì `setGradeScaleRules` cập nhật bảng ngay. Đang dùng ở `ProfileFlowStepper`, `Step2Transcript`, `Step6Review`, `TranscriptUpload` |
| Frontend trang quản trị (`/admin/parameters`, `GradeScaleSettings`) | Moderator sửa, thêm hoặc bớt khoảng, lưu có hộp xác nhận. Admin xem. Có thẻ lịch sử thay đổi |

## 6. Chỗ khác còn ghi công thức cũ (không phải file của Việt, chưa sửa, cần báo chủ file)

Rà soát ngày 28/11 (A9 trong `docs/KE_HOACH_SUA_THEO_NHAN_XET_THAY_28_11.md`). Code đã đúng công thức ở mục 1; các chỗ dưới chỉ là chữ.

- Đức:
  - `docs/RA_SOAT_V0.1.md` dòng 31, 47, 122: "chuẩn WES 8.0→3.5, 9.0→4.0", "bảng WES cố định";
  - `docs/report3_sections_USAS-364-365_official.md`, `docs/report3_sections_USAS-365.md`: phần quy đổi kiểu WES;
  - `docs/LICH_SU_TRO_CHUYEN_SPRINT1.md` dòng 38: "chuẩn WES ... fallback raw × 0.4" (nhật ký cũ);
  - comment trong code: `AcademicAnalysisController.cs` dòng 13, 103; `AcademicAnalysisService.cs` dòng 11, 285; `AcademicDtos.cs` dòng 71 còn chữ "WES". `AcademicAnalysisService.cs` dòng 270: chữ dự phòng `"WES Standard Scale"` khi cache phân tích cũ thiếu `scaleSource`; chữ này hiện ra ở `GpaSummaryCard` ("Quy đổi tham khảo theo …");
  - `docs/DEVELOPMENT_GUIDELINE.md` dòng 82 (ví dụ comment).
- `docs/REPORT3_SRS_DUCNX_COMPLETE.md` (ducmkac): phần quy đổi kiểu WES.
- Dũng: comment `advisor/app/services/chat_flow.py` dòng 225 "chưa từng gửi thì dùng bảng WES" (thực tế không có bảng thì không đoán GPA).

## 7. Việc còn lại cho Dũng

- `frontend/src/components/recommendation/ProfileSummary.tsx` (Việt và Dũng cùng làm) tính GPA bằng `draftGpa`, tức là gián tiếp gọi `toGpa4`, nhưng chưa gọi `useGradeScale()`. Vì vậy GPA thang 4 ở đó hiện theo bảng mặc định tới khi bảng tải xong (chỉ lệch khi moderator đã sửa bảng).
- Cần thêm một dòng `useGradeScale();` trong component, giống `Step6Review`. Chưa sửa vì cần Dũng đồng ý.
