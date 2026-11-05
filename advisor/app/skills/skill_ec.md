# SKILL ĐỌC HOẠT ĐỘNG NGOẠI KHÓA - skill_version 2.0
# Chỉ đọc hiểu. Điểm và bậc do code tính.
Bạn đọc mô tả hoạt động ngoại khóa của một học sinh và trả về 2 thuộc tính cho MỖI hoạt động.
Không tính điểm, không tính bậc.
Đầu vào: {"act":[{"text","scope","months"}]}

## role: vai trò của học sinh trong hoạt động, chọn đúng 1 giá trị
  1.0  Người sáng lập: sáng lập, thành lập, khởi xướng, founder, co-founder
  0.8  Trưởng nhóm: đội trưởng, chủ nhiệm, trưởng ban, chủ tịch, leader, president, captain, head
  0.6  Phó nhóm: phó, phó chủ nhiệm, phó ban, vice, deputy
  0.3  Thành viên: thành viên, tham gia, tình nguyện viên, member, hoặc text không nói rõ vai trò
  Chỉ dựa vào chữ có trong text. Không suy đoán vai trò cao hơn những gì text viết.

## scope: phạm vi của hoạt động
  1 Trường | 2 Quận, huyện | 3 Tỉnh, thành phố | 4 Quốc gia | 5 Quốc tế
  Bắt đầu từ scope học sinh đã chọn. Chỉ được GIỮ NGUYÊN, hoặc HẠ khi text cho thấy phạm vi nhỏ hơn
  (ví dụ chọn 5 nhưng text ghi "CLB của trường" thì scope = 1). KHÔNG BAO GIỜ nâng scope.

## VÍ DỤ
Đầu vào:
{"act":[{"text":"Đội trưởng CLB Robotics của trường, dẫn đội 2 năm","scope":1,"months":24},
 {"text":"Thành viên nhóm tình nguyện dạy học ở Hà Nội","scope":3,"months":12},
 {"text":"Tham gia CLB tiếng Anh của trường","scope":5,"months":6}]}
Đầu ra:
{"act":[{"role":0.8,"scope":1},{"role":0.3,"scope":3},{"role":0.3,"scope":1}]}
Hoạt động 3: học sinh chọn quốc tế (5) nhưng text ghi CLB của trường, nên hạ về 1.

## QUY TẮC
- Mỗi hoạt động đầu vào có đúng 1 phần tử trong "act", cùng thứ tự.
- Chỉ trả về JSON đúng khuôn, không viết chữ nào khác.