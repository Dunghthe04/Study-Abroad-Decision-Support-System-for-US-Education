"""Câu hỏi kiểu học sinh gõ tự nhiên để tìm chỗ SLM hiểu sai.

Claude soạn câu và nhãn TRƯỚC khi chạy model, theo slm/data/HUONG_DAN_GAN_NHAN.md. Chưa có người thứ hai duyệt.
KHÔNG chép nguyên các câu này vào dữ liệu train: muốn đo lại sau khi train thì chúng phải còn là câu model chưa học.

Mỗi dòng: (nhóm, câu, [đáp án chấp nhận được]). Một đáp án là các ý cách nhau bằng " | ";
mỗi ý viết "loại" hoặc "loại:chi tiết" (nhiệm vụ, mode hoặc subtype; nhiều nhiệm vụ nối bằng "+").
"""

# Câu học sinh tự mở lời (không trả lời câu hỏi hồ sơ nào)
TU_DO: list[tuple[str, str, list[str]]] = [
    # ----- bắt đầu, lộ trình
    ("lộ trình", "em muốn đi mỹ học mà chưa biết gì hết, giờ phải làm sao ạ", ["profile_review:roadmap"]),
    ("lộ trình", "cho em hỏi muốn du học mỹ thì mình nên làm gì đầu tiên", ["profile_review:roadmap"]),
    ("lộ trình", "em đang lớp 11, từ giờ tới lúc nộp đơn cần chuẩn bị những gì", ["collect_profile | profile_review:roadmap"]),
    ("lộ trình", "len ke hoach giup em voi", ["profile_review:roadmap"]),
    ("lộ trình", "mốc thời gian của em nên sắp xếp thế nào cho kịp kỳ fall", ["profile_review:roadmap"]),
    # ----- gợi ý trường
    ("gợi ý trường", "với hồ sơ của em thì nộp trường nào được ạ", ["profile_review:school_list"]),
    ("gợi ý trường", "tư vấn trường cho em đi", ["profile_review:school_list"]),
    ("gợi ý trường", "em nên apply đâu bây giờ", ["profile_review:school_list"]),
    ("gợi ý trường", "có trường nào hợp với em không ạ", ["profile_review:school_list"]),
    ("gợi ý trường", "kiem truong vua tui tien cho em", ["profile_review:school_list", "knowledge:search"]),
    # ----- đánh giá hồ sơ
    ("đánh giá", "hồ sơ em vậy ổn chưa ạ", ["profile_review:assessment"]),
    ("đánh giá", "em yếu chỗ nào nhất vậy", ["profile_review:assessment"]),
    ("đánh giá", "liệu em có cửa đi du học không", ["profile_review:assessment"]),
    ("đánh giá", "xem giùm em profile coi được chưa", ["profile_review:assessment"]),
    # ----- trường cụ thể có hợp mình không
    ("hợp trường", "em nộp UCLA được không ạ", ["profile_review:explain_school"]),
    ("hợp trường", "sức em thì Purdue có với tới không", ["profile_review:explain_school"]),
    ("hợp trường", "sao trường Boston University lại xếp vào nhóm khó vào vậy", ["profile_review:explain_fit"]),
    # ----- giả định
    ("giả định", "lỡ em thi ielts được có 6.0 thôi thì sao", ["scenario:what_if"]),
    ("giả định", "giả sử nhà em ráng thêm được 10k nữa thì có thêm trường nào không", ["scenario:what_if"]),
    ("giả định", "neu sat em len 1450 thi danh sach co khac ko", ["scenario:what_if"]),
    ("giả định", "mà em đổi qua học kinh tế thì sao ta", ["scenario:what_if"]),
    ("giả định", "muốn đậu NYU thì em phải cày SAT lên mấy", ["scenario:goal_seeking"]),
    ("giả định", "làm sao để em vào được Cornell", ["scenario:goal_seeking"]),
    ("giả định", "GPA bao nhiêu thì em mới có cửa vô UC Berkeley", ["scenario:goal_seeking"]),
    # ----- số liệu một trường
    ("số liệu trường", "học phí Arizona State bao nhiêu 1 năm vậy ạ", ["knowledge:school_info"]),
    ("số liệu trường", "trường Drexel có cho tiền du học sinh không", ["knowledge:school_info"]),
    ("số liệu trường", "deadline nộp hồ sơ của UIUC là khi nào", ["knowledge:school_info"]),
    ("số liệu trường", "ktx ở Purdue mắc ko ạ", ["knowledge:school_info"]),
    ("số liệu trường", "Stanford khó vào cỡ nào", ["knowledge:school_info"]),
    ("số liệu trường", "A&M Texas với UT Austin cái nào rẻ hơn", ["knowledge:compare_facts"]),
    # ----- tìm trường theo điều kiện
    ("tìm trường", "có trường nào ở Cali học phí dưới 30k không", ["knowledge:search"]),
    ("tìm trường", "truong nao ko can sat vay", ["knowledge:search"]),
    ("tìm trường", "kể vài trường rẻ rẻ dạy computer science", ["knowledge:search"]),
    ("tìm trường", "ielts 5.5 thì học được trường nào", ["knowledge:search", "collect_profile | profile_review:school_list"]),
    # ----- kiến thức chung
    ("kiến thức", "du học mỹ tốn khoảng bao nhiêu tiền 1 năm", ["knowledge"]),
    ("kiến thức", "visa f1 phỏng vấn người ta hay hỏi gì", ["knowledge"]),
    ("kiến thức", "học bổng toàn phần có thật không hay chỉ quảng cáo", ["knowledge"]),
    ("kiến thức", "sang đó có được đi làm thêm ko ạ", ["knowledge"]),
    ("kiến thức", "community college là sao, học xong chuyển lên đại học được hả", ["knowledge"]),
    ("kiến thức", "bài luận nên viết về cái gì cho hay", ["knowledge"]),
    ("kiến thức", "sat với act thi cái nào dễ hơn", ["knowledge"]),
    ("kiến thức", "chứng minh tài chính cần bao nhiêu", ["knowledge"]),
    ("kiến thức", "ED với EA khác nhau chỗ nào", ["knowledge"]),
    # ----- trung tâm
    ("trung tâm", "có chỗ nào ở đà nẵng luyện phỏng vấn visa không ạ", ["center_referral"]),
    ("trung tâm", "em cần người coi lại bài luận giùm", ["center_referral"]),
    ("trung tâm", "giới thiệu em trung tâm nào uy tín đi", ["center_referral"]),
    ("trung tâm", "em muốn hẹn gặp bên tư vấn chọn trường", ["center_referral:booking"]),
    # ----- khai hồ sơ tự nhiên
    ("khai hồ sơ", "em gpa 8.7, ielts 6.5, nhà lo được tầm 800 triệu 1 năm", ["collect_profile"]),
    ("khai hồ sơ", "mình học 12 chuyên anh, thích marketing", ["collect_profile"]),
    ("khai hồ sơ", "con tôi đang học lớp 10, cháu muốn học y", ["collect_profile"]),
    ("khai hồ sơ", "à em ghi nhầm, gpa em 8.2 chứ không phải 8.7", ["collect_profile:edit", "collect_profile"]),
    # ----- nhiều ý
    ("nhiều ý", "em gpa 3.4, có trường nào hợp không, với lại visa khó không", ["collect_profile | profile_review:school_list | knowledge"]),
    ("nhiều ý", "toefl là gì và em nên thi cái đó hay ielts", ["knowledge", "knowledge | knowledge"]),
    ("nhiều ý", "cho em hỏi học phí của UMass và chỗ nào luyện sat ở hà nội", ["knowledge:school_info | center_referral"]),
    # ----- phụ huynh
    ("phụ huynh", "gia đình tôi không dư dả lắm, liệu cho cháu đi mỹ được không", ["profile_review:assessment", "collect_profile | profile_review:assessment", "knowledge"]),
    ("phụ huynh", "tôi muốn biết với sức học của cháu thì nên chọn trường nào", ["profile_review:school_list"]),
    # ----- ngoài phạm vi, xã giao
    ("ngoài phạm vi", "alo có ai ko", ["out_of_scope:greeting"]),
    ("ngoài phạm vi", "ok cảm ơn nha bye", ["out_of_scope:farewell"]),
    ("ngoài phạm vi", "bạn là người hay máy vậy", ["out_of_scope:about_system"]),
    ("ngoài phạm vi", "cho em nói chuyện với người thật được không", ["out_of_scope:human_handoff"]),
    ("ngoài phạm vi", "nay trời nóng quá ha", ["out_of_scope:exception"]),
    ("ngoài phạm vi", "làm giùm em cái bảng điểm đẹp hơn được ko", ["out_of_scope:sensitive"]),
    ("ngoài phạm vi", "du học Úc thì sao", ["out_of_scope:exception"]),
    ("ngoài phạm vi", "asdf hmm", ["out_of_scope:ambiguous"]),
]

# Câu trả lời cho một câu hỏi hồ sơ đang chờ: (nhóm, mã câu đang chờ, câu, [đáp án])
TRA_LOI: list[tuple[str, str, str, list[str]]] = [
    ("trả lời câu chờ", "B1.1", "lớp 11 á", ["collect_profile"]),
    ("trả lời câu chờ", "B1.3", "chắc IT, em cũng chưa chắc nữa", ["collect_profile"]),
    ("trả lời câu chờ", "D3", "chưa thi luôn á", ["collect_profile"]),
    ("trả lời câu chờ", "D3", "1380", ["collect_profile"]),
    ("trả lời câu chờ", "E1", "mới có ielts 6 hồi tháng 5", ["collect_profile"]),
    ("trả lời câu chờ", "F1", "tầm 1 tỷ đổ lại", ["collect_profile"]),
    ("trả lời câu chờ", "F1", "cái này em phải hỏi lại ba mẹ", ["collect_profile"]),
    ("trả lời câu chờ", "F2", "có thì tốt ko có cũng ráng được", ["collect_profile"]),
    ("trả lời câu chờ", "F3", "ba má em lo hết", ["collect_profile"]),
    ("trả lời câu chờ", "D4", "em đá banh cho trường với đi tình nguyện hè", ["collect_profile"]),
    ("trả lời câu chờ", "N1", "dạ có dì ở bên đó", ["collect_profile"]),
    ("trả lời câu chờ", "N3", "sao cũng được ạ", ["collect_profile"]),
    # đang chờ trả lời nhưng học sinh hỏi chuyện khác
    ("hỏi ngang", "F1", "mà đi mỹ trung bình tốn nhiêu vậy ạ", ["knowledge"]),
    ("hỏi ngang", "D3", "sat là cái gì vậy", ["knowledge"]),
    ("hỏi ngang", "E1", "không có ielts thì nộp được không", ["knowledge"]),
]
