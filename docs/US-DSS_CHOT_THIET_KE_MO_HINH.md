# Chốt thiết kế mô hình quyết định US-DSS

Tài liệu chốt cho cả nhóm SEP490_G119, dùng để sửa hai file (Quy trình phát triển, Danh sách chức năng và use case) và làm theo. Cập nhật 20/10/2026. Mục đánh dấu **[đề xuất]** là giá trị nhóm cần xác nhận; mục **[mở]** là việc chưa có đáp án.

## 1. Các quyết định đã chốt

| # | Vấn đề | Quyết định |
|---|---|---|
| D1 | Ai ra kết quả gợi ý lần đầu | **AI (Gemini) đề xuất**, **mô hình luật (code) tính lại cùng công thức để kiểm tra** |
| D2 | Khi AI và luật lệch | **Dùng kết quả của luật** cho trường lệch (bất kỳ mức nào), ghi nhật ký |
| D3 | Khi AI lỗi, hết hạn mức, quá tải, sai khuôn | Dùng kết quả của luật cho cả danh sách |
| D4 | Khi AI lệch nhiều | **Ngắt mạch**: tỉ lệ lệch vượt ngưỡng thì chỉ dùng luật, báo nhóm |
| D5 | What-if, goal-seeking, độ nhạy, đổi nút ưu tiên | **Chỉ chạy bằng mô hình luật**, không gọi AI |
| D6 | Lưu kết quả | Tính một lần rồi lưu; chỉ tính lại khi hồ sơ hoặc dữ liệu đổi |
| D7 | AI làm gì ngoài việc chấm | Đọc ngoại khóa và bảng điểm, giải thích và tư vấn, hỏi đáp có nguồn, phỏng vấn thu thập hồ sơ |
| D8 | Nơi đặt công thức | **Backend C#**, cạnh `HardFilter` (backend giữ luồng, what-if và lưu kết quả) **[đề xuất]** |

Cách nói thống nhất trong mọi tài liệu: **"AI đề xuất, mô hình luật kiểm tra và quyết định khi lệch. Mọi phân tích what-if, goal-seeking, độ nhạy chạy bằng mô hình luật."**

Nói trung thực khi hội đồng hỏi "AI đóng góp gì ở bước chấm": khi khớp thì kết quả giống luật, khi lệch dùng luật, nên đóng góp của AI ở bước chấm là **ý kiến độc lập có đo độ khớp**. Giá trị thật của AI nằm ở D7.

## 2. Luồng và phân vai

```
Hồ sơ
 → Lọc cứng (code)
 → Qwen đọc hoạt động ngoại khóa → Python tính bậc 1–5
 → Gemini chấm (skill_score) rồi chọn danh sách (skill_pick)
 → Mô hình luật tính lại, so từng trường
      khớp: giữ | lệch: lấy giá trị của luật + cờ + nhật ký
 → LƯU kết quả, kèm nhật ký đo (token, thời gian, khớp hay lệch)
 → Màn kết quả
      đổi nút ưu tiên, what-if, goal-seeking, độ nhạy → mô hình luật (không gọi AI)
      "Chi tiết" → Qwen viết lời từ số đã có
```

| Thao tác | Ai tính | Gọi AI |
|---|---|---|
| Gợi ý lần đầu, hoặc khi hồ sơ đổi | AI đề xuất, luật kiểm tra | Có |
| Xem lại, tải lại trang | Đọc kết quả đã lưu | Không |
| Đổi nút ưu tiên, what-if, goal-seeking, độ nhạy | Luật | Không |
| Lời giải thích, hỏi đáp, phỏng vấn | AI, từ số do luật đưa | Có |

## 3. Quy tắc dự phòng (backup)

**Thế nào là "khớp"** (so từng trường, lấy luật làm chuẩn):
- Nhóm cuối `g2` giống nhau.
- Điểm Strength `S` chênh không quá 1 điểm (đúng ngưỡng SRS GBR-REC-09).
- F, H, Q chênh không quá 0,01.
- Lượt chọn: không đổi nhóm, đúng khối, đúng thứ tự theo điểm X (bằng X thì theo mã trường), đúng hạn mức 3/5/4 và khối học bổng.
- Mã lý do (`GPA_LOW`, `SAT_BELOW`...) đúng theo quy tắc P5 tính bằng code từ `pg`, `ps`, `blk`, `r`, `adj`.

| Tình huống | Hiển thị | Ghi lại |
|---|---|---|
| Khớp | Kết quả (giống nhau), nguồn "AI" | `khớp` |
| Lệch ở một trường | Giá trị của luật cho trường đó, thẻ gắn cờ, nguồn "luật" | Trường, giá trị AI, giá trị luật, bước sai đầu tiên |
| AI lỗi, 429, 503, hết giờ, sai khuôn | Luật cho cả danh sách, nguồn "luật" | Lý do lỗi |
| Tỉ lệ lệch vượt ngưỡng | Chỉ luật, báo nhóm | Sự kiện ngắt mạch |

Giá trị cấu hình **[đề xuất]**, lưu trong cơ sở dữ liệu để chỉnh được:
- Ngưỡng ngắt mạch: lệch trên 10% số trường trong 20 lần chạy gần nhất.
- Tổng thời gian tối đa cho một lần gợi ý: 90 giây, quá thì chuyển luật.
- Lỗi 429: không thử lại liên tục; chuyển luật ngay hoặc chờ theo thời gian nhà cung cấp gợi ý.
- Mỗi kết quả ghi rõ nguồn (AI hay luật) và hiện trên thẻ trường.

## 4. Đo lường (token, tốc độ, độ chính xác)

**Ghi mỗi lần gọi AI** (bảng `AiCall` hoặc bảng mới; hiện chưa có code ghi): loại lượt (đọc ngoại khóa, chấm, chọn, tư vấn), model, `skill_version`, token vào, ra, suy nghĩ, thời gian, thành công hay lỗi (mã lỗi).
**Ghi mỗi trường sau kiểm tra:** nhóm AI và luật, S của AI và luật, khớp hay lệch, bước sai đầu tiên.

| Chỉ số | Cách tính | Dùng để |
|---|---|---|
| Token mỗi lần gọi, mỗi trường | Tổng token chia số lần gọi, số trường | Chi phí, hạn mức gói |
| Thời gian mỗi lần gọi (trung vị, p95) | Từ nhật ký | Thiết kế chờ, hạn thời gian |
| Chi phí ước tính mỗi lần gợi ý | Token × giá nhà cung cấp | Giá gói trả phí (UC-83) |
| **Tỉ lệ khớp** (AI so với luật) | Số trường khớp chia tổng | Chỉ số "đo được AI đúng hay sai" |
| Bước sai nhiều nhất | Đếm bước sai đầu tiên | Chỗ cần sửa skill |
| Tỉ lệ lỗi gọi, số lần ngắt mạch | Từ nhật ký | Độ tin cậy dịch vụ |

**Đo ngoại tuyến:** bộ hồ sơ mẫu có đáp án (6 học sinh, mỗi học sinh 40 trường, 240 cặp; có đủ trường hợp thiếu dữ liệu, trường nhận gần hết, du học sinh ít) và chương trình chạy ra các chỉ số trên. Tiêu chí chấp nhận theo SRS: tỉ lệ nhóm đúng từ 90%; mỗi lần chạy sau không thấp hơn lần trước.

**Chạy bóng** trước khi cho AI hiển thị kết quả: AI chạy song song nhưng chỉ ghi số liệu, hiển thị kết quả của luật, đến khi tỉ lệ khớp ổn định.

**Số đo hiện có** (lần chạy 18/10, `gemini-3.8-flash`, mức suy nghĩ `low`, 1 học sinh, 40 cặp, lô 8 trường): 95% đúng nhóm (38/40, khoảng tin cậy khoảng 83–99% nên **chưa đủ để kết luận đạt 90%**); 5 lần gọi, 10–22 giây mỗi lần (trung bình khoảng 14 giây); 16.217 token vào, 6.298 token ra, 0 token suy nghĩ; ước khoảng 900 đồng mỗi lần gợi ý 40 trường (giá Standard đến hết 31/12/2026, tỉ giá giả định 25.000 đồng/USD). Qwen 8B tự chấm trước đó chỉ đúng 19–48%. Gói miễn phí của Gemini chỉ 5 yêu cầu mỗi phút và **20 yêu cầu mỗi ngày**, nên đo đủ bộ cần Tier 1 hoặc chia nhiều ngày.

## 5. What-if, goal-seeking, độ nhạy (yêu cầu của hội đồng)

Cả ba chạy trên **bản sao tạm** của hồ sơ, bằng mô hình luật, kết quả tức thì và lặp lại được, **không ghi đè hồ sơ thật**, nhãn "mô phỏng theo mô hình luật". "Trước" và "sau" đều do cùng một mô hình luật tính nên so sánh được.

**What-if (UC-82).** Đầu vào được phép đổi: SAT, GPA, IELTS hoặc TOEFL, mức tài chính, học bổng, tỷ giá, nộp hay không nộp SAT (với trường test-optional), vòng nộp (ED, EA, RD), bang, ngành, nút ưu tiên. Kết quả: danh sách trường **đổi nhóm** (từ nhóm nào sang nhóm nào), thứ hạng mới, trường vào hoặc ra khỏi danh sách.

**Goal-seeking (UC-81).** Chọn một trường mục tiêu và nhóm muốn đạt (ví dụ Vừa sức). Với từng yếu tố điều chỉnh được (SAT, GPA, bậc ngoại khóa, điểm tiếng Anh, ngân sách), hệ thống tìm **mức tối thiểu** để trường đạt nhóm đó, giữ các yếu tố khác nguyên:
- Điểm Strength tăng theo SAT, GPA, bậc ngoại khóa, nên chỉ cần quét tăng dần (SAT bước 10 điểm đến 1600, GPA bước 0,05 đến 4,0, bậc ngoại khóa 1 đến 5) hoặc tìm kiếm nhị phân, rồi dừng ở mức đầu tiên đạt.
- Không yếu tố nào đạt được một mình thì báo "không đạt chỉ bằng yếu tố này", kèm tổ hợp thử (ví dụ SAT và ngoại khóa cùng tăng).
- Trường ở trạng thái "Khó vào xa" (tỉ lệ nhận quốc tế dưới một nửa tỉ lệ chung) vẫn giữ nhãn dù điểm tăng: nói rõ lý do.
- Kết quả: mức cần đạt và mức còn thiếu theo từng yếu tố (cũng là suy diễn lùi của hợp đồng DSS).

**Độ nhạy (UC-85).** Chạy lại nhiều lần, mỗi lần đổi **một** đầu vào trong khoảng cho phép **[đề xuất]**: SAT ±50 và ±100; GPA ±0,1 và ±0,2; ngân sách ±10% và ±20%; bậc ngoại khóa ±1; mức quan trọng C7 của từng trường ±1; bốn nút ưu tiên. Kết quả:
- Số trường đổi nhóm theo từng mức đổi, và danh sách trường đó.
- Mỗi trường gắn nhãn **ổn định** (không đổi nhóm trong khoảng) hay **nhạy** (đổi nhóm khi lệch nhỏ).
- Với thứ hạng: mức trùng của top 5 và top 10 giữa các nút ưu tiên hoặc khi đổi trọng số.

Tính toán rất nhẹ (vài trăm lần chạy công thức trên vài trăm trường), nên không cần AI.

**Phần chưa có luật [mở]:** hội đồng nêu "nộp Early Decision hay Regular Decision" sẽ đổi kết quả thế nào, nhưng nhóm **chưa có công thức** cho ảnh hưởng của vòng nộp. Cần luật từ chuyên gia (UC-88) hoặc dữ liệu CDS về ED, EA. Cho đến khi có, what-if vòng nộp chỉ hiện hạn nộp theo vòng, **không đổi nhóm**, và ghi rõ như vậy.

## 6. Hạn mức và chi phí (liên quan thanh toán)
- Chỉ ba loại việc tốn token: gợi ý lần đầu (kể cả khi hồ sơ đổi), lời tư vấn từng trường, chat. What-if, goal-seeking, độ nhạy, đổi nút ưu tiên, xem lại **không tốn**.
- Kết quả được lưu nên chi phí tính theo **số lần tính lại**, không theo số lần xem.
- Con số hạn mức gói (UC mục 6) điền từ số đo ở mục 4; hiện đã có số đo lần chạy 18/10 nhưng **chưa đủ để chốt giá**.

## 7. Việc phải sửa trong hai file

**Quy trình phát triển**
1. Giai đoạn 0, mục Mô hình: thay đoạn "Quy tắc khi hai mô hình lệch" bằng bảng ở mục 3.
2. Bảng rủi ro: dòng "AI trả kết quả sai hoặc hết hạn mức", đổi "luật thắng khi lệch 2 bậc" thành "luật thắng khi lệch (bất kỳ mức nào); chuyển sang chỉ dùng luật khi vượt ngưỡng".
3. Đưa **mô hình luật lên giai đoạn 0 hoặc v0.1** (hiện ở v0.3), và thêm chạy bóng vào v0.1. v0.4 đổi câu "đo độ lệch" thành "tổng hợp độ lệch đã đo từ v0.1, phân tích độ nhạy".
4. Xóa dòng rủi ro "Thanh toán phức tạp hơn dự kiến" bị lặp; điền tên Dev A và Dev B.
5. Thêm một câu giải thích **Strength thay cho Fit score**; chỉnh câu trích dẫn Arnott (2004) cho đúng ý bài sau khi đối chiếu toàn văn.

**Danh sách chức năng và use case**
1. Mục 7: thay bảng "Tình huống / Hiện kết quả" bằng bảng ở mục 3, sửa câu mở đầu mục cho khớp.
2. UC-22 (tạo gợi ý): ghi AI đề xuất, luật kiểm tra, lưu kết quả, ghi nhật ký.
3. UC-81, UC-82, UC-85: bổ sung đầu vào, cách tìm, đầu ra như mục 5.
4. UC-83 (admin xem mức dùng AI): bổ sung **tỉ lệ khớp, thời gian, lỗi gọi, sự kiện ngắt mạch** ngoài token và chi phí.
5. UC-23: gắn nguồn kết quả (AI hay luật) và cờ lệch trên thẻ trường (đã có "nguồn kết quả").
6. **Thêm use case dashboard KPI** của Advisee (UC-90 hoặc gộp UC-23/24): chỉ số, biểu đồ, giải thích số liệu. Bộ use case hiện không có.
7. Mục 5: có **bảng quy đổi điểm Việt Nam** thật (UC-68), đặc tả ETL và tiêu chí chọn 50 trường (tài liệu riêng, phải nằm trong bộ nộp).
8. Mục 6: thêm số đo thật ở mục 4, ghi ngày đo và model.

## 8. Phân việc đề xuất
| Việc | Người | Ghi chú |
|---|---|---|
| Công thức bằng code (C#) kèm test | Dũng viết | Dùng chung cho kiểm tra, dự phòng, what-if, đo |
| Kiểm tra kết quả AI, nhật ký đo, ngắt mạch | Dũng | Thay phần `CloudResultCheck` chỉ kiểm khuôn hiện nay |
| Bộ hồ sơ mẫu, chương trình đo, báo cáo số đo | Dũng cùng bạn kiểm thử | Cần Tier 1 hoặc chia ngày để chạy đủ bộ |
| What-if, goal-seeking, độ nhạy (API và giao diện) | Dev backend, dev giao diện | Chạy trên công thức ở trên |
| Bảng quy đổi điểm, đặc tả ETL, danh sách luật chuyên gia | BA (Phương Anh) | Cần chuyên gia xác nhận |
| Sửa hai file theo mục 7 | BA | Mục 1–4 làm trước |

## 9. Việc còn mở
1. Xác nhận các giá trị **[đề xuất]** (ngưỡng 10%, hạn 90 giây, khoảng đổi của độ nhạy).
2. Luật cho vòng nộp ED, EA ảnh hưởng nhóm thế nào (cần chuyên gia hoặc dữ liệu CDS).
3. Bảng quy đổi điểm Việt Nam.
4. Chạy đủ bộ 240 cặp để có tỉ lệ khớp đáng tin (hiện chỉ có lần thử 40 cặp).
5. Có chuyên gia, tư vấn viên xác nhận luật và skill (nhờ giáo viên hướng dẫn giới thiệu).
