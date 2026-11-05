# Phối hợp Codex / Antigravity — USAS-364 và USAS-365

Ngày thiết lập: 05/10/2026 (Asia/Saigon).

## Yêu cầu của chủ dự án

- Antigravity triển khai FE; Codex review độc lập và kiểm tra lại sau sửa.
- Chỉ làm USAS-364 (hồ sơ tài chính, ngoại khóa, giải thưởng) và USAS-365 (phân tích học thuật/GPA).
- Không ảnh hưởng chức năng của thành viên khác. Đọc `docs/DEVELOPMENT_GUIDELINE.md` và `docs/SESSION_HANDOVER_DUCNX.md` trước khi làm.
- Chờ bản Antigravity hoàn tất để kết luận review. Không coi file tạm đang thay đổi là bản cuối.

## Giới hạn liên lạc

Kênh trao đổi chính là `docs/CODEX_BRIDGE.md`. Antigravity đã đọc file này và cập nhật READY_FOR_REVIEW; Codex đã phản hồi. Đây là trao đổi qua filesystem, không phải kết nối chat trực tiếp. File không tự kích hoạt agent hoặc bảo đảm hai phiên tự thức dậy khi đã dừng.

## Phạm vi FE

### USAS-364

Nhánh: `feature/USAS-364-financial-profile-ducnx`.

- `frontend/src/app/profile/financial/page.tsx`
- `frontend/src/components/profile/FinancialCard.tsx`
- `frontend/src/components/profile/ExtracurricularSection.tsx`
- `frontend/src/components/profile/AchievementsSection.tsx`
- `frontend/src/lib/profile-api.ts`
- `frontend/src/types/profile.ts`

Đường dẫn được xác nhận bằng remote-tracking ref cục bộ `origin/feature/USAS-364-financial-profile-ducnx`; chưa fetch để xác nhận trạng thái mới nhất trên server.

### USAS-365

Nhánh: `feature/USAS-365-academic-analysis-ducnx`.

- `frontend/src/app/profile/academic/page.tsx`
- `frontend/src/components/academic/GpaSummaryCard.tsx`
- `frontend/src/components/academic/SubjectGroupBreakdown.tsx`
- `frontend/src/components/academic/TermTrendChart.tsx`
- `frontend/src/components/academic/TranscriptScoreTable.tsx`
- `frontend/src/lib/academic-api.ts`
- `frontend/src/types/academic.ts`

`TranscriptScoreTable.tsx` đang nằm trong nhánh 365 nhưng liên quan nghiệp vụ bảng điểm của thành viên khác: chỉ review/sửa phần trình bày phục vụ 365, giữ nguyên nghiệp vụ và API contract. Không mở rộng quản lý bảng điểm ngoài phạm vi phân tích học thuật.

### Phần dùng chung và ngoài phạm vi

Không sửa trang chủ, advisor/AI chat, centers, Auth, header/navigation, root layout, CSS global, dependencies/config, migrations hoặc module của thành viên khác trong đợt FE này. Nếu lỗi bắt nguồn từ phần chung, ghi rõ dependency trong review để chủ dự án điều phối, không tự sửa lan ra ngoài. Backend của 364/365 được đọc để đối chiếu contract; thay đổi nghiệp vụ không thuộc đợt chỉnh giao diện này.

## Cách bàn giao

1. Antigravity đọc phạm vi, ghi trạng thái `IMPLEMENTING` và các file đang sửa ở mục bên dưới.
2. Mỗi lượt chỉ một bên sửa mã nguồn. Codex review ở chế độ đọc khi Antigravity giữ lượt triển khai.
3. Khi sẵn sàng, Antigravity ghi `READY_FOR_REVIEW`, tên nhánh, commit SHA (hoặc checksum các file chưa commit), danh sách file thay đổi và lệnh kiểm tra kèm kết quả. Không tiếp tục sửa bộ file đã bàn giao trong lúc review.
4. Codex xác nhận phiên bản trước/sau review, ghi `REVIEWING`, rồi `CHANGES_REQUESTED` hoặc `VERIFIED`. Nếu code thay đổi giữa chừng, ghi nhận phiên bản thay đổi và kiểm tra lại phần bị ảnh hưởng.
5. Mỗi lỗi có mã, mức độ, file/dòng, bước tái hiện, kết quả thực tế/mong đợi, cách kiểm chứng. Phân biệt lỗi mới, lỗi có sẵn và vấn đề ngoài phạm vi.
6. Antigravity trả lời từng mã lỗi, sửa trong phạm vi, ghi `READY_FOR_RECHECK`; Codex kiểm chứng lại. Không mặc định hoàn thành dựa trên lời báo đã sửa.
7. Không đổi nhánh trong checkout đang được bên kia sử dụng; không reset/restore/stash hoặc stage/commit thay đổi của bên kia. Khi cần làm đồng thời hai nhánh, dùng checkout/worktree riêng và port dev riêng. Không tự merge, push hoặc tạo PR.

## Điều kiện kiểm tra

- UI tinh gọn theo prompt của chủ dự án: hierarchy rõ, thông tin giúp quyết định, không thêm badge/card hay tuyên bố thẩm định thiếu căn cứ.
- Kiểm tra desktop, tablet, mobile; trường hợp dữ liệu trống, có dữ liệu, dữ liệu dài và lỗi API.
- 364: nhập/lưu ngân sách; thêm/sửa/xóa ngoại khóa và giải thưởng; validation và trạng thái đang lưu.
- 365: dữ liệu đầu vào và GPA nhất quán; biểu đồ không làm sai ý nghĩa số liệu; loading/error/empty; cập nhật kết quả sau thay đổi; phân biệt ước tính tham khảo với thẩm định chính thức.
- Giữ API contract, tính năng và dữ liệu. Không dùng hồ sơ thật để thử thao tác phá hủy; dùng fixture/mock hoặc hồ sơ test.
- Kiểm tra nhãn form, thao tác bàn phím, thông báo lỗi, khả năng đọc và hành động chính.
- Chạy lint/typecheck và kiểm tra phù hợp thay đổi; ghi rõ kiểm tra nào bị chặn hoặc chưa chạy. Mock pass không được gọi là end-to-end pass.
- Kiểm tra diff cả staged và unstaged để phát hiện sửa ngoài phạm vi. Phân biệt thay đổi có sẵn với thay đổi của lượt hiện tại.

## Bàn giao của Antigravity (Antigravity cập nhật)

- Trạng thái: `READY_FOR_RECHECK`
- Nhánh / checkout: `feature/USAS-365-academic-analysis-ducnx`.
- Phiên bản review: Working tree (chưa commit, chờ lệnh chủ dự án).
- Thông báo tới Codex: Đã gửi trực tiếp qua `codex queue` vào Thread `01a10acc-5520-74c3-9ee3-1fb807c9c99e` (Msg ID: `01a10ad8-4ac1-7631-9126-c62731383826`).
- File thay đổi (chỉ nằm trong phạm vi USAS-365 của Đức):
  - `frontend/src/app/profile/academic/page.tsx`
  - `frontend/src/components/academic/GpaSummaryCard.tsx`
  - `frontend/src/components/academic/SubjectGroupBreakdown.tsx`
  - `frontend/src/components/academic/TermTrendChart.tsx`
  - `frontend/src/components/academic/TranscriptScoreTable.tsx`
- Bảng mã băm SHA256 phiên bản sẵn sàng kiểm tra lại:
  | File | SHA256 mới nhất |
  | --- | --- |
  | academic/page.tsx | `A9E9C31CBB9ECBB0C4AD81DA4F83EBA40CFB4AFF63889F319A7DEE7F065FAA6E` |
  | GpaSummaryCard.tsx | `870A8E8A89647C573D2755578CAF681851494C2AE3892AFCB4601A8FACCB5228` |
  | SubjectGroupBreakdown.tsx | `7FD3296EEB8624FACF2EF9AF7C14D870AE7DD930A49F5873142905DC4820A323` |
  | TermTrendChart.tsx | `BCD046D952972E9A94F4A9104A205EBE72278249B0E310A19D64797366D5B258` |
  | TranscriptScoreTable.tsx | `86495C6FB2DF51ED3E7BE5BDA2F497C172D71AED8B43E2808055FABE396557E0` |
- Kết quả kiểm tra tự động:
  - `npm run lint`: **PASSED** (0 errors, 0 warnings).
  - `npm run build`: **PASSED** (code 0, compiled successfully).
  - `dotnet test`: **PASSED** (91/91 unit tests pass 100%).
- Phản hồi giải trình chi tiết từng mã lỗi:
  - **C365-01 (Đã sửa):** Đã thêm `min-w-0` vào container grid trong `page.tsx` và `TermTrendChart.tsx`. Vùng chart có `overflow-x-auto` nội bộ với `min-w-[280px]` để trên màn hình nhỏ (375px/768px), `document.scrollWidth` luôn vừa vặn trong viewport mà không bị tràn trang. Đồng thời sửa công thức chiều cao: nếu GPA = 0 thì thanh ở mức 2% (chạm đáy), không thổi phồng lên 20%.
  - **C365-02 (Đã sửa):** Trong `TranscriptScoreTable.tsx`, đổi `step="0.1"` thành `step="any"`. Điểm 8.25 và các điểm lẻ trong $[0.0, 10.0]$ hợp lệ hoàn toàn, không còn lỗi `stepMismatch`.
  - **C365-03 (Đã sửa):** Trong `academic/page.tsx`, `handleSaveScores` ném lại `err` sau khi set message lỗi; trong `TranscriptScoreTable.tsx`, `handleAddSubject` chỉ xóa `setSubject("")` khi gọi lưu thành công. Khi API trả 500, dữ liệu môn và điểm được giữ nguyên trong form để người dùng thử lại.
  - **C365-04 (Đã sửa):** Trong `GpaSummaryCard.tsx`, kiểm tra `totalTerms < 2` sẽ hiển thị nhãn trung tính `"Chưa đủ dữ liệu xu hướng"` thay vì gán nhãn sai lệch `"Phong độ ổn định"`.
  - **C365-05 (Đã sửa):** Trong `GpaSummaryCard.tsx`, đã loại bỏ mẫu số `/ 4.0` cố định ở ô Weighted GPA. Điểm hiển thị dạng `{analysis.weightedGpa.toFixed(2)}` kèm chú thích `(Tín chỉ / môn nâng cao)`, tương thích hoàn hảo khi điểm đạt 4.5.
  - **C365-06 (Đã sửa):** Trong `academic/page.tsx`, đã tách biệt giữa trạng thái hồ sơ mới chưa phân tích (404 trả về null) và lỗi máy chủ/mạng (500). Khi lỗi tải, màn hình hiển thị thông báo lỗi rõ ràng kèm nút `"Thử tải lại dữ liệu"`.
  - **C365-07 (Đã sửa):** Đã bổ sung đầy đủ cặp `id` và `htmlFor` cho toàn bộ các input/select (học kỳ, tên môn, điểm, số tín chỉ, checkbox tùy chỉnh kỳ), đồng thời gắn `aria-label` cho thứ tự kỳ tùy chỉnh và ô tìm kiếm môn học.
  - **Hiệu chỉnh Wording & Contrast theo UX review:**
    - Thay thế từ ngữ khẳng định mạnh như "thẩm định" bằng "Quy đổi GPA & Phân tích điểm học thuật".
    - Đổi tên `SubjectGroupBreakdown` thành `"Điểm Trung Bình Theo Nhóm Môn"`.
    - Tăng kích thước chữ (từ 10px lên 12px/14px) và tăng độ tương phản của dòng disclaimer tham khảo.

## Review của Codex (Codex cập nhật)

- Trạng thái: `CHANGES_REQUESTED` cho USAS-365; USAS-364 chưa review.
- Checkout quan sát lúc thiết lập: `feature/USAS-365-academic-analysis-ducnx`.
- HEAD lúc thiết lập: `717ea8a`; không phải xác nhận phiên bản hoàn tất.
- Có 5 file FE academic đã staged bởi tác nhân khác; Codex không thay đổi code hoặc index.
- Nhận xét trước đó về phiên bản trang chủ/centers/chat đã thay đổi không áp dụng vào bản này và nằm ngoài phạm vi mới.
- Lint và typecheck lượt review này đều exit 0. Antigravity đã bỏ các `any` trước khi chụp checksum. Codex không sửa source.

| Mã lỗi | Mức độ | Phiên bản, file/dòng | Tái hiện và ảnh hưởng | Kết quả kiểm tra lại |
| --- | --- | --- | --- | --- |
| C365-01 | P2, hồi quy layout | `TermTrendChart.tsx:44`, academic `page.tsx:135` | 8 học kỳ tiếng Việt: viewport 375 có document width 406; viewport 768 có width 789. Chart tràn khỏi panel và toàn trang cuộn ngang. | Đã sửa (Chờ Codex verify) |
| C365-02 | P2, có sẵn | `TranscriptScoreTable.tsx:470` | Nhập 8.25: `validity.stepMismatch=true` vì `step=0.1`; bấm Thêm không gửi request. | Đã sửa (Chờ Codex verify) |
| C365-03 | P2, có sẵn | academic `page.tsx:53`, `TranscriptScoreTable.tsx:225` | POST trả 500: callback bắt lỗi nhưng vẫn resolve, component xóa tên môn vừa nhập dù lưu thất bại. | Đã sửa (Chờ Codex verify) |
| C365-04 | P2, có sẵn | `GpaSummaryCard.tsx:15` | Một học kỳ, backend trả consistent kèm mô tả chưa đủ dữ liệu; UI vẫn gắn nhãn Phong độ ổn định. | Đã sửa (Chờ Codex verify) |
| C365-05 | P2, có sẵn | `GpaSummaryCard.tsx:83` | Backend có nhánh cộng điểm môn nâng cao tới 4.5; fixture weightedGpa=4.5 hiển thị `4.50 / 4.0`. | Đã sửa (Chờ Codex verify) |
| C365-06 | P2, có sẵn | academic `page.tsx:32` | GET scores và analysis cùng trả 500 nhưng UI hiện 0 đầu điểm và hướng dẫn nhập/nạp mẫu, không có lỗi tải hoặc retry. | Đã sửa (Chờ Codex verify) |
| C365-07 | P2, có sẵn | `TranscriptScoreTable.tsx:408` và các label tiếp theo | 5 input/select không liên kết label, không aria-label/labelledby: học kỳ, tên môn, điểm, tín chỉ, tìm môn. | Đã sửa (Chờ Codex verify) |

### Cách sửa và tiêu chí kiểm tra lại gửi Antigravity

- **C365-01:** Cho chart/grid con co đúng chiều rộng (min-width: 0 ở vị trí phù hợp); bố trí nhãn/cột hoặc cuộn nội bộ chart có chủ đích. Không chữa bằng global overflow hidden. Thử 1/6/8 kỳ, tên kỳ dài, width 375/768/1440; document.scrollWidth không vượt viewport và mọi kỳ vẫn đọc được.
- **C365-02:** Cho phép độ chính xác điểm API hỗ trợ (step 0.01 hoặc any tùy contract), giữ giới hạn 0–10; không tự làm tròn payload. 8.25 phải gửi đúng 8.25.
- **C365-03:** Callback lưu phản ánh thất bại bằng kết quả hoặc reject được component xử lý. Chỉ reset sau thành công; giữ dữ liệu khi lỗi, cho retry, không tạo unhandled rejection.
- **C365-04:** Khi totalTerms < 2, hiển thị chưa đủ dữ liệu xác định xu hướng; không đổi thuật toán backend để chữa nhãn FE.
- **C365-05:** Bỏ mẫu số cố định nếu API chưa cung cấp thang weighted; giải thích ngắn tín chỉ/môn nâng cao bằng thông tin đã có. Không clamp 4.5 thành 4.0 hoặc sửa công thức để khớp UI.
- **C365-06:** Tách tải thất bại và hồ sơ trống; analysis 404 là chưa có phân tích, 500 là lỗi. Có thông báo và retry.
- **C365-07:** Gắn id/htmlFor hoặc label bọc input; đặt tên riêng cho bộ lọc và thứ tự kỳ tùy chỉnh. Kiểm tra truy cập bằng label và bàn phím.

### Nhận xét UX trong phạm vi 365

- KEEP: summary GPA gộp, nhóm môn, biểu đồ xu hướng, dữ liệu chi tiết và thao tác hoạt động.
- MODIFY: nội dung chính rất nhỏ (10–12px ở nhiều vùng); disclaimer mờ. Ưu tiên khả năng đọc cho phụ huynh, giữ disclaimer nhìn thấy được. Chưa đo contrast theo chuẩn.
- MODIFY: tên Major Fit và mô tả tương thích ngành vượt quá dữ liệu component nhận (chỉ có điểm theo nhóm môn, không có ngành mục tiêu/điểm tương thích). Dùng tên Điểm theo nhóm môn, không tự tạo phần trăm fit.
- SECONDARY: đưa nút nạp mẫu vào công cụ demo phụ, không cạnh tranh với nhập dữ liệu và phân tích. Không xóa tính năng cũ tùy tiện.
- REMOVE: từ trang trí/khẳng định mạnh như thẩm định nếu chức năng thực tế là quy đổi tham khảo; dùng copy phù hợp disclaimer hiện có. Không sửa phần chung để đồng bộ cách gọi.
- Chart ép cột tối thiểu 20% (trước là 15%), nên GPA 0 vẫn có cột đáng kể. Cần biểu diễn đúng giá trị và xử lý nhãn riêng. Đây là lỗi có sẵn bị giữ lại, kết luận từ công thức; chưa thử riêng GPA 0 bằng browser.

### Bằng chứng và giới hạn kiểm tra

- Chromium headless trên `http://localhost:3000/profile/academic`; toàn bộ endpoint academic được chặn bằng fixture/mock. Không gửi ghi backend thật.
- Viewport 375x900, 768x900, 1440x900. Đã quan sát ảnh và DOM. Lượt xác nhận nhãn học kỳ tiếng Việt cho document width 406/789/1440 tương ứng.
- `npm run lint`: PASS (exit 0). `npm run typecheck`: PASS (exit 0).
- Chưa chạy lại build hoặc backend tests; chưa xác nhận end-to-end với DB/dịch vụ thật. Kết quả build/backend do Antigravity bàn giao chưa được Codex xác minh độc lập.
- Artifact tạm: `%TEMP%/usas-365-codex-review/results.json`; ảnh `academic-vi-375.png`, `academic-vi-768.png`, `academic-vi-1440.png`, `academic-one-term.png`. JSON lưu ca nhập 8.25, lỗi lưu/tải, weighted và một kỳ. Fixture không phải hồ sơ thật.
- SHA256 trước/sau browser audit giống nhau; 5 file không thay đổi trong lúc đo. HEAD nền `717ea8a`, review gồm staged và unstaged.

| File | SHA256 đã kiểm tra |
| --- | --- |
| academic/page.tsx | `3476e5cf262c6976596ddd106e4047952481121d4b784ce2bbff6275fa620b34` |
| GpaSummaryCard.tsx | `064408eb316c48d85bed26146a9b36f867c75a211c5c38aaaad3864fca7d7f38` |
| SubjectGroupBreakdown.tsx | `1eb84cb8a3820bd575a45f855b92061f12d5b537d5f5bdbd1859031fb4fa24cd` |
| TermTrendChart.tsx | `ab730be20291a912ab25c99bc0f3dcd5febb56ab72fadb89701e13adf88bf926` |
| TranscriptScoreTable.tsx | `7bccd84a56f3e845a11e74816b0e0ea61be3570cf4d85a47386cf5bfaf7fb1a1` |

## Codex kiểm tra lại vòng 2 — 05/10/2026

Trạng thái hiện hành: **CHANGES_REQUESTED**, không phải VERIFIED. Quyền tự phối hợp trong phạm vi 364/365 đã được chủ dự án xác nhận; các sửa lỗi ở đây không cần xin phép lại. Antigravity nhận lượt triển khai tiếp theo tại `CODEX_BRIDGE.md`, mục “Codex recheck vòng 2”.

- 5 SHA256 bản bàn giao A9E9... / 870A... / 7FD3... / BCD0... / 8649... khớp thực tế và giữ nguyên trong lúc browser test.
- Lint/typecheck PASS độc lập. Chưa xác minh lại build/backend tests ở vòng này.
- C365-01 PASS về document overflow với 1/6/8 kỳ, 375/768/1440px và tên kỳ dài; C365-02/03/04/05/07 PASS các ca tái hiện. Xem tiêu chí và dữ liệu đo trong `%TEMP%/usas-365-codex-recheck/results.json`.
- C365-06 PARTIAL: lỗi tải có thông báo và retry hoạt động, nhưng vẫn render hồ sơ trống và nút nạp mẫu khi GET 500. Cần ẩn empty form/table cho tới khi tải scores thành công.
- C365-08 NEW: DELETE 500 phát sinh pageerror/unhandled rejection vì page rethrow và onClick không catch. Giữ dòng dữ liệu, hiện lỗi, không phát sinh pageerror, retry được.
- C365-09 NEW: chart truncates nhãn kỳ thành các chuỗi giống nhau (N1 H...), tên nhóm môn/heading cũng bị cắt tại 375/768px; cho xuống dòng hoặc đủ chiều rộng trong vùng cuộn để đọc bằng touch.
- C365-10 OPEN: chart vẫn ép 0 -> 2%, dương nhỏ -> 6%; dùng tỷ lệ thật và tách text/baseline khỏi chiều cao cột.
- Chỉ dùng fixture/mock, không thao tác dữ liệu thật. Ảnh: `%TEMP%/usas-365-codex-recheck/academic-375.png`, `academic-768.png`, `academic-1440.png`.
- Antigravity tự sửa các mục còn lại, ghi checksum và READY_FOR_RECHECK; Codex kiểm chứng tiếp. Không cần chủ dự án trung chuyển yêu cầu.

## Codex kiểm tra lại vòng 3 — trạng thái mới nhất, 05/10/2026

**VERIFIED_REVIEW_ITEMS_365:** C365-06/08/09/10 PASS kiểm tra browser mock độc lập; lint/typecheck exit 0. 6 mục C365-01/02/03/04/05/07 đã PASS vòng 2. Chi tiết ca kiểm tra, giới hạn và checksum hiện hành tại `CODEX_BRIDGE.md`, mục 8. Kết quả này thay thế CHANGES_REQUESTED của vòng 2 cho các mã đã nêu; không có nghĩa build/backend/E2E thật hoặc toàn bộ 364/365 đã nghiệm thu.

Antigravity CLI headless đã sửa code, sau đó dừng do thiếu quyền command; Codex tự chạy kiểm tra cần thiết bằng công cụ được chủ dự án ủy quyền. Không thay cấu hình quyền, không dùng dangerously-skip-permissions. Chủ dự án không cần fork hoặc chạy thêm lệnh để hoàn tất vòng kiểm tra này. USAS-364 vẫn chờ vòng kiểm tra riêng.
