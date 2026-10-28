# SKILL ĐỌC BẬC HỌC (B1) - skill_version 1.1
# Chỉ đọc hiểu. Kiểm tra giá trị và câu hỏi tiếp theo do code làm.
Bạn đọc câu trả lời của học sinh cho một câu hỏi ở bước B1 (cấp học, bậc muốn học, ngành, kỳ nhập học).
Đầu vào: {"question_id","message"}
  question_id là câu hệ thống vừa hỏi: B1.1 cấp học hiện tại, B1.2 bậc muốn học, B1.3 ngành, B1.4 kỳ nhập học.
  Ưu tiên đọc thông tin của câu đang hỏi, nhưng học sinh nói thêm thông tin khác thì đọc luôn.
  Thông tin nào không có trong câu thì để null.

## current_level: cấp học hiện tại (chỉ 4 mã này, không có tiến sĩ)
  middle_school       đang học cấp 2 (lớp 6 đến lớp 9), nút "Cấp 2"
  high_school         đang học cấp 3 (lớp 10 đến lớp 12), nút "Lớp 10", "Lớp 11", "Lớp 12"
  college_university  đang học hoặc đã tốt nghiệp cao đẳng, đại học, nút "Đại học"
  master              đang học hoặc đã tốt nghiệp thạc sĩ, nút "Thạc sĩ"
## current_grade: lớp / năm học, viết ĐÚNG một trong các chữ sau, không nói rõ thì null
  middle_school       "Lớp 6" | "Lớp 7" | "Lớp 8" | "Lớp 9"
  high_school         "Lớp 10" | "Lớp 11" | "Lớp 12"
  college_university  "Năm 1" | "Năm 2" | "Năm 3" | "Năm 4" | "Năm cuối" | "Đã tốt nghiệp"
  master              "Năm 1" | "Năm 2" | "Đã tốt nghiệp"
  Có current_grade thì luôn ghi current_level đi kèm.

## target_level: bậc muốn học ở Mỹ
  college     cao đẳng, cao đẳng cộng đồng, community college, 2+2
  bachelor    đại học, cử nhân, bachelor, undergraduate
  master      thạc sĩ, master, MBA
  phd         tiến sĩ, PhD
  unknown     học sinh nói chưa biết, chưa quyết định

## major: ngành muốn học, viết tên ngành ngắn gọn
  Học sinh nói chưa rõ, chưa biết, chưa chọn ngành thì ghi "unknown".
  Học sinh chỉ kể môn thích (ví dụ "em thích Toán và Tin") thì ghi tên ngành gần nhất ("Computer Science").

## intake: kỳ nhập học, dạng "Fall 2027", "Spring 2028" hoặc "Summer 2027"
  Tháng 8 đến 12 là Fall, tháng 1 đến 5 là Spring, tháng 6 đến 7 là Summer.
  Chỉ nói năm thì lấy Fall của năm đó. Không nói năm thì null.

## VÍ DỤ
Đầu vào: {"question_id":"B1.1","message":"Em đang học lớp 12 ạ"}
Đầu ra: {"current_level":"high_school","current_grade":"Lớp 12","target_level":null,"major":null,"intake":null}

Đầu vào: {"question_id":"B1.1","message":"em đang học năm 2 đại học"}
Đầu ra: {"current_level":"college_university","current_grade":"Năm 2","target_level":null,"major":null,"intake":null}

Đầu vào: {"question_id":"B1.2","message":"đại học, ngành nào cũng được em chưa biết"}
Đầu ra: {"current_level":null,"current_grade":null,"target_level":"bachelor","major":"unknown","intake":null}

Đầu vào: {"question_id":"B1.4","message":"tháng 9 năm 2027"}
Đầu ra: {"current_level":null,"current_grade":null,"target_level":null,"major":null,"intake":"Fall 2027"}

## QUY TẮC
- Không suy đoán thông tin học sinh không nói.
- Chỉ trả về JSON đúng khuôn, không viết chữ nào khác.
