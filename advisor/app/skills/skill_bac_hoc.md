# SKILL ĐỌC BẬC HỌC (B1) - skill_version 1.0
# Chỉ đọc hiểu. Kiểm tra giá trị và câu hỏi tiếp theo do code làm.
Bạn đọc câu trả lời của học sinh cho một câu hỏi ở bước B1 (cấp học, bậc muốn học, ngành, kỳ nhập học).
Đầu vào: {"question_id","message"}
  question_id là câu hệ thống vừa hỏi: B1.1 cấp học hiện tại, B1.2 bậc muốn học, B1.3 ngành, B1.4 kỳ nhập học.
  Ưu tiên đọc thông tin của câu đang hỏi, nhưng học sinh nói thêm thông tin khác thì đọc luôn.
  Thông tin nào không có trong câu thì để null.

## current_level: cấp học hiện tại
  middle_school       đang học cấp 2 (lớp 6 đến lớp 9)
  high_school         đang học cấp 3 (lớp 10 đến lớp 12), vừa tốt nghiệp cấp 3
  college_university  đang học hoặc đã tốt nghiệp cao đẳng, đại học
  master              đang học hoặc đã tốt nghiệp thạc sĩ
## current_grade: số lớp nếu học sinh nói rõ (6 đến 12), không thì null

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
Đầu ra: {"current_level":"high_school","current_grade":12,"target_level":null,"major":null,"intake":null}

Đầu vào: {"question_id":"B1.2","message":"đại học, ngành nào cũng được em chưa biết"}
Đầu ra: {"current_level":null,"current_grade":null,"target_level":"bachelor","major":"unknown","intake":null}

Đầu vào: {"question_id":"B1.4","message":"tháng 9 năm 2027"}
Đầu ra: {"current_level":null,"current_grade":null,"target_level":null,"major":null,"intake":"Fall 2027"}

## QUY TẮC
- Không suy đoán thông tin học sinh không nói.
- Chỉ trả về JSON đúng khuôn, không viết chữ nào khác.
