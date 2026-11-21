# Kế hoạch UC-94 — Xem dashboard KPI

Tác nhân: Học sinh, Phụ huynh. Chỉ đọc, chỉ dùng số đã có, **không tính điểm mới**, **không tạo bảng/cột mới**.

## 1. Mỗi chỉ số lấy từ đâu (đối chiếu code hiện có)

### Học sinh

| Chỉ số | Nguồn có sẵn | Cách tính (dòng giải thích hiện trên thẻ) |
|---|---|---|
| Số trường mỗi nhóm, lần gợi ý mới nhất | `RecommendationHistoryService.ListAsync` → `RecommendationRunSummaryDto.Counts` (reach / match / safety / aid) — bảng `recommendations` | "Đếm số trường trong từng nhóm của lần gợi ý lúc {giờ}. Chưa chạy gợi ý thì trống." |
| Tiến độ hoàn thiện hồ sơ | `ProfileFlowService` state: `completedSteps` / 5 bước nhập + `hasCommittedProfile` | "{x}/5 bước đã xong. Hồ sơ tính là đã gửi khi bấm Lưu ở bước 6." |
| Hạn nộp sắp tới | `target_schools.deadline` + `application_round` (UC-32), đã có `TargetSchoolSummaryDto.NextDeadline` | "Các trường mục tiêu có hạn từ hôm nay trở đi, sắp theo ngày gần nhất (lấy 3–5 trường)." Trường chưa chọn vòng nộp → đếm riêng "chưa có hạn". |
| Mức đầy đủ dữ liệu | **Cần chốt (câu hỏi Q2)** | — |

### Phụ huynh (tính trên danh sách trường mục tiêu của **từng con**)

| Chỉ số | Nguồn có sẵn | Cách tính |
|---|---|---|
| Số trường trong ngân sách | Chi phí/năm = `TotalCost(offering)` = học phí + ăn ở + phí + chi khác (đã có trong `RecommendationService`, `SchoolCostInfo.Total`). Ngân sách: **cần chốt (Q1)** | "Trường có tổng chi phí 1 năm ≤ ngân sách/năm. Trường chưa có học phí thì không tính, ghi riêng 'chưa có số liệu'." |
| Tổng chi phí 4 năm của danh sách mục tiêu | Như trên × 4, cộng các trường có số liệu | "Chi phí năm học {AcademicYear} × 4, chưa tính tăng giá, chưa trừ học bổng." |
| Khoản còn thiếu | Theo từng trường: max(0, chi phí 4 năm − ngân sách/năm × 4) | "Phần chi phí 4 năm vượt ngân sách 4 năm của từng trường." (Không cộng dồn các trường vì con chỉ học 1 trường — hiện trường thiếu nhiều nhất / ít nhất.) |

Phụ huynh đã liên kết **xem thêm** các thẻ của học sinh (gợi ý, tiến độ, hạn nộp, đầy đủ dữ liệu) của con đó.

## 2. Thiết kế

### Backend (file mới, của Việt)
- `Application/Dashboard/DashboardDtos.cs`, `DashboardService.cs`: chỉ **gọi lại** service/repository có sẵn (history, flow state, target schools, offering cost, parent preference) rồi gom số. Mỗi KPI trả `{ value, explanation, missing? }` — giải thích sinh ở backend để chat/form/web nói giống nhau.
- `Api/Controllers/DashboardController.cs`:
  - `GET /api/v1/dashboard/me` — học sinh (và phụ huynh đang acts-as con managed).
  - `GET /api/v1/dashboard/children/{studentId}` — phụ huynh; kiểm liên kết đang hiệu lực giống `RecommendationHistoryService.CanViewAsync`; không có quyền → 404.
- Không migration, không bảng mới.
- Cần thêm 1 hàm đọc chi phí theo danh sách `universityId` (nếu repository chưa có) — đặt trong file mới, không sửa `RecommendationService` (của Dũng).

### Frontend
- Trang học sinh: route mới `/dashboard` (`app/dashboard/page.tsx` + `components/dashboard/*`). Không sửa `app/page.tsx` (của Dương).
- Phụ huynh: thêm thẻ KPI vào tab **Tổng quan** trong `ChildDashboard.tsx` (file của Việt), trên `AcademicSummaryCard`.
- Thẻ KPI dùng chung `KpiCard` (số lớn + dòng giải thích + link sang trang chi tiết: Gợi ý / Trường mục tiêu / Hồ sơ).
- Link vào `/dashboard` trên menu: thêm 1 mục "Tổng quan" vào `SiteHeader.tsx` (Việt đã cho phép, Q4).

### Chat
UC này chỉ là trang xem, không có bước nhập → không cần thay đổi chat (khác với các UC sửa form). Nếu muốn trợ lý trả lời "tiến độ của em thế nào" thì để UC sau.

## 3. Các bước làm (mỗi bước test + commit riêng, không push)
1. DTO + `DashboardService` phần học sinh (gợi ý, tiến độ, hạn nộp) + unit test.
2. Phần phụ huynh (ngân sách, chi phí 4 năm, còn thiếu) + unit test các ca: trường thiếu học phí, chưa có ngân sách, không có trường mục tiêu.
3. Mức đầy đủ dữ liệu (sau khi chốt Q2) + test.
4. `DashboardController` + kiểm quyền (học sinh khác / phụ huynh hết liên kết → 404).
5. FE `/dashboard` + `KpiCard`.
6. FE tab Tổng quan của phụ huynh.
7. Chạy thử api + frontend bằng tài khoản test (học sinh, phụ huynh liên kết, phụ huynh con managed), xong tắt server.

## 4. Quyết định (Việt chốt 20/11/2026)
- **Q1 → (a)**: ngân sách phụ huynh nhập ở UC-76 (`parent_preference_json.BudgetUsd`); chưa nhập thì dùng mức trần hạng ngân sách của con (`BudgetMaxUsd`). Con managed không có tab UC-76 → luôn dùng hạng của con. Thẻ ghi rõ đang dùng nguồn nào.
- **Q2 → (a)**: mức đầy đủ dữ liệu = hồ sơ học sinh đã có bao nhiêu mục mà gợi ý dùng (GPA, chứng chỉ tiếng Anh, SAT/ACT, ngân sách, ngành) — hiện x/N mục và tên mục còn thiếu. Danh sách mục chốt khi code bước 3.
- **Q3 → từng trường**: khoản còn thiếu tính riêng mỗi trường mục tiêu, không cộng dồn.
- **Q4 → thêm vào menu trên**: Việt cho phép sửa `SiteHeader.tsx` (file Đức/Dương), chỉ thêm 1 mục "Tổng quan" → `/dashboard`, hiện với học sinh (và phụ huynh đang acts-as con managed). Không sửa gì khác trong file đó.

## 5. Bổ sung: số tiền tối đa thật của phụ huynh (Việt chốt 20/11/2026, chưa code)

### Quy tắc
| Trường hợp | Số dùng để so | Cách tính |
|---|---|---|
| Con liên kết, phụ huynh đã nhập ở UC-76 | `BudgetUsd` của UC-76 | 1 trường hợp |
| Con liên kết, phụ huynh chưa nhập | hạng của con (`BudgetMaxUsd`) | như hiện tại, ghi rõ nguồn; có nút **"Nhập số tiền tối đa chi trả được/năm"** |
| Con managed, phụ huynh nhập số cụ thể trong form | `BudgetMaxUsd` = số đó (chung cho con và phụ huynh) | 1 trường hợp |
| Con managed cũ chỉ có hạng | hạng của con | như hiện tại, ghi "theo hạng, là ước tính" + link sang hồ sơ để nhập số |

### Phần A — popup cho con liên kết (chỉ lưu tiền)
- Ô nhập **VND** (100 triệu – 10 tỷ, như UC-76), dòng "≈ … USD theo tỷ giá hôm nay".
- **Chỉ lưu tiền**: không lưu ưu tiên, UC-76 vẫn coi là "chưa chọn ưu tiên", **không báo cho con**.
- Backend (file Việt): `ParentPreferenceService` thêm `SaveBudgetAsync` — giữ `Pref` cũ nếu đã có; chưa có thì để trống (`Stored.Pref` cho phép null, cột jsonb sẵn có → **không thêm cột**). `GetAsync` / `CompareForParentAsync` / `ParentPreferencePanel` coi `Pref` null như chưa chọn ưu tiên nhưng **ô ngân sách hiện số đã lưu** (tự fill). Endpoint `PUT .../preference/budget` trong `ParentChildPreferenceController`.
- Frontend: `BudgetDialog` trong `ParentDashboardKpiSection`; lưu xong tải lại khung Tài chính.

### Phần B — ô số tiền thật khi phụ huynh nhập hộ con managed
- Chỉ khi đang acts-as (`ActingSessionGuard.ReadActingParentId` có giá trị). Học sinh tự điền vẫn chọn hạng.
- Form `Step4Financial`: đã có ô nhập số tiền (VND/USD) nhưng hiện chỉ dùng để đoán hạng rồi bỏ số. Khi acting: ô **VND** là câu chính, gửi thêm `annualBudgetVnd` trong `FinancialDraft`.
- Backend `ProfileFlowService` khi commit: có `annualBudgetVnd` → quy đổi theo tỷ giá lúc lưu → `BudgetMaxUsd` = số đó, `BudgetTier` = hạng chứa số đó (để các chỗ hiện hạng không hỏng), `BudgetMinUsd` = mức min của hạng. Lưu lại hồ sơ mà không đổi số thì giữ nguyên, không bị hạng đè. **Không thêm cột**: số VND nằm trong draft json sẵn có của flow.
- Chat (đổi form = đổi chat): `ChatProfileMapper` + `advisor/.../validators.py`, `buttons.py` — khi acting, câu ngân sách hỏi số tiền VND thay vì nút hạng; `check_budget` trả cả số lẫn hạng.
- Gợi ý trường không đổi luật: `HardFilter` (file Dũng, **không sửa**) vẫn so với `BudgetMaxUsd` (+ mức dư), chỉ là giờ nhận số thật.
- Dashboard: con managed có số thật → nhãn nguồn "Số tiền phụ huynh nhập trong hồ sơ".

### Đã chốt thêm
- Câu ngân sách khi nhập hộ con managed **bắt buộc** (không "Bỏ qua") nhưng có 2 cách trả lời: nhập **số VND chính xác**, hoặc bấm **"Chưa biết chính xác"** rồi chọn hạng như học sinh. Chọn hạng → dashboard ghi "theo hạng, là ước tính" + nhắc cập nhật số khi biết. Chat làm giống form.
- Việt cho phép sửa `Step4Financial.tsx`, `ProfileFlowWizard.tsx`.

### Các bước (mỗi bước test + commit riêng, không push)
1. A backend `SaveBudgetAsync` + endpoint + unit test (pref null, giữ pref cũ, không gửi thông báo).
2. A frontend popup + UC-76 panel hiện số khi chưa chọn ưu tiên.
3. B backend commit hồ sơ với `annualBudgetVnd` + test (không bị hạng đè, mức trên 70k).
4. B form `Step4Financial` khi acting.
5. B chat (mapper + advisor) + test python.
6. Dashboard nhãn nguồn + chạy thử 3 tài khoản (học sinh, phụ huynh liên kết, phụ huynh managed), xong tắt server mình mở.
