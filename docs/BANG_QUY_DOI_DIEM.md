# Bảng quy đổi điểm `grade_scale.10` (thang 10 → GPA thang 4)

Người làm: Việt. Liên quan: #4 (hồ sơ), #13 (màn hình quản trị), chat tư vấn (advisor).

## 1. Nguyên tắc

- OCR và bảng điểm giữ **điểm từng môn đúng thang 10**. Chỉ **điểm trung bình tổng** mới đổi sang GPA thang 4 kiểu Mỹ.
- Tra theo **cận dưới** `minScore`: lấy khoảng có `minScore` lớn nhất mà điểm ≥ `minScore`. `maxScore` chỉ để hiển thị, nên 8.995 vẫn ra 3.7 (không rơi vào khe 8.99–9.0).
- Điểm thấp hơn mọi mốc thì GPA = 0.
- DB chưa có dòng `grade_scale.10` thì mọi nơi dùng **bảng WES mặc định** trong code (`AcademicAnalysisService.GetDefaultWesScaleConfig`).

Bảng WES mặc định:

| Từ điểm (thang 10) | GPA 4 | Điểm chữ |
|---|---|---|
| 9.0 | 4.0 | A |
| 8.5 | 3.7 | A- |
| 8.0 | 3.5 | B+ |
| 7.0 | 3.0 | B |
| 6.5 | 2.5 | C+ |
| 5.5 | 2.0 | C |
| 5.0 | 1.5 | D+ |
| 4.0 | 1.0 | D |
| 0.0 | 0.0 | F |

## 2. Dữ liệu trong DB

Bảng `app_settings`, key `grade_scale.10`, `value_json`:

```json
{
  "scaleKey": "grade_scale.10",
  "sourceName": "Chuẩn thẩm định WES (World Education Services - Hoa Kỳ)",
  "method": "table",
  "rules": [
    { "minScore": 9.0, "maxScore": 10, "gpa4": 4.0, "letterGrade": "A" },
    { "minScore": 8.5, "maxScore": 8.99, "gpa4": 3.7, "letterGrade": "A-" }
  ]
}
```

- `DbSeeder` thêm bảng WES khi DB chưa có, không ghi đè bảng đã sửa.
- Mỗi lần lưu ghi `audit_logs`:
  - `action = "grade_scale.update"`;
  - `entity_type = "app_setting"`;
  - `entity_id = "grade_scale.10"`;
  - `detail_json = {old, new, recomputedProfiles}`.

## 3. Quyền và API

Controller: `AdminSettingsController`, route gốc `api/v1/admin/settings`.

| Phương thức | Đường dẫn | Ai gọi được | Kết quả |
|---|---|---|---|
| GET | `/grade-scale` | admin, moderator | `{config, version, isDefault}`. `isDefault = true` khi DB chưa có bảng, đang dùng WES |
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
   - nếu `overall_gpa` hiện tại đúng bằng số tra theo bảng cũ thì tra lại theo bảng mới.
3. Hồ sơ tự nhập GPA giữ nguyên, vì DB không lưu cách nhập.
4. Lưu bảng kèm audit log trong cùng một lần.
5. Xoá cache của `IGradeScaleProvider` (cache 10 phút) để mọi chỗ dùng bảng mới ngay.

## 5. Bảng được dùng ở đâu

| Nơi | Cách dùng |
|---|---|
| Backend: GPA từ bảng điểm (`TranscriptGpa`, `AcademicAnalysisService`) | Đọc qua `IGradeScaleProvider` |
| Backend: GPA từ OCR (`ProfileFlowService.OcrGpa4Async`) | Lấy `AverageScoreScale10` OCR trả về, tra lại theo bảng trong DB. Advisor OCR vẫn tra WES, nhưng kết quả đó không được dùng làm GPA cuối |
| Backend → advisor khi chat (`ChatService`) | Gửi `gradeScale` (danh sách rules) mỗi lượt. Bảng lấy từ server, bỏ bảng trình duyệt gửi lên |
| Advisor (`chat_flow`, `skill_danh_gia`, `skill_lo_trinh`, `profile_calc`, `skill_input`) | Cất vào `state.grade_scale`. Lượt không gửi bảng thì giữ bảng cũ. `gpa_table_of` đổi sang bảng tra; chỉ GPA tổng thang 10 bị đổi, GPA thang 4 giữ nguyên. Không có bảng thì dùng WES |
| Frontend (`useGradeScale`, `toGpa4`) | Tải từ `scale-config`, lỗi thì dùng WES. Moderator lưu xong thì `setGradeScaleRules` cập nhật bảng ngay. Đang dùng ở `ProfileFlowStepper`, `Step2Transcript`, `Step6Review`, `TranscriptUpload` |
| Frontend trang quản trị (`/admin/parameters`, `GradeScaleSettings`) | Moderator sửa, thêm hoặc bớt khoảng, lưu có hộp xác nhận. Admin xem. Có thẻ lịch sử thay đổi |

## 6. Chỗ tài liệu khác còn lệch (chưa sửa, cần báo chủ file)

- `docs/RA_SOAT_V0.1.md` dòng 31 và 122 (Đức):
  - mới ghi "bảng WES cố định";
  - nay bảng nằm trong DB, moderator sửa được, chưa có thì mới dùng WES.
- SRS (`REPORT3_SRS_DUCNX_COMPLETE.md`, Đức):
  - chưa tìm thấy đoạn lệch cụ thể;
  - cần Đức kiểm tra phần quy đổi điểm và quyền sửa: moderator sửa, admin chỉ xem.

## 7. Việc còn lại cho Dũng

- `frontend/src/components/recommendation/ProfileSummary.tsx` (Việt và Dũng cùng làm) tính GPA bằng `draftGpa`, tức là gián tiếp gọi `toGpa4`, nhưng chưa gọi `useGradeScale()`. Vì vậy GPA thang 4 ở đó có thể hiện theo WES tới khi bảng tải xong.
- Cần thêm một dòng `useGradeScale();` trong component, giống `Step6Review`. Chưa sửa vì cần Dũng đồng ý.
