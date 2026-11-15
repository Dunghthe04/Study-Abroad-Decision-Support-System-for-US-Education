# Luồng hồ sơ CollegeVine – Tổng hợp chi tiết

Nov 2, 2026 · @vite

## Tổng quan luồng

Người dùng đi qua 3 giai đoạn liên tiếp: onboarding 7 màn, hồ sơ tính cơ hội 7 màn (5 bước có thanh tiến trình cùng màn bổ sung và màn Network), rồi tới trang kết quả. Các bước phải làm theo thứ tự; truy cập thẳng URL một bước sau sẽ bị chuyển về bước đang dở.

&#91;embedded content: luồng CollegeVine · 3 giai đoạn, 18 màn\]

| # | Màn | URL |
| --- | --- | --- |
| 1 | Chọn sản phẩm khi đăng ký | (trang đăng ký) |
| 2 | Welcome | /onboarding/wizard/welcome |
| 3 | Let's start with the basics | /onboarding/wizard/identity |
| 4 | How did you hear about CollegeVine? | /onboarding/wizard/how-found-cv |
| 5 | What schools are you considering? | /onboarding/wizard/schools |
| 6 | What's most important? | /onboarding/wizard/preferences |
| 7 | Let's calculate your chances! | /chancing/profile/start |
| 8 | 1. Demographics | /chancing/profile/demographics |
| 9 | Demographics continued | /chancing/profile/demographics-continued |
| 10 | 2. Test scores | /chancing/profile/test-scores |
| 11 | 3. Grades | /chancing/profile/grades |
| 12 | 4. Coursework | /chancing/profile/coursework |
| 13 | Extracurriculars | /chancing/profile/extracurriculars |
| 14 | LAST STEP · Network | /chancing/profile/network |
| 15 | Danh sách trường + Profile simulator | /schools/hub/all?simulator=open |
| 16 | Trang chi tiết trường | /schools/hub/all/d/\<tên-trường> |
| 17 | Tab Chancing của trường | /schools/hub/all/d/\<tên-trường>/chancing |

Ghi chú nguồn: thu thập ngày 2/11/2026 bằng cách đi qua luồng với dữ liệu mẫu (học sinh lớp 12, Việt Nam, du học sinh) và đọc ảnh chụp màn hình; một số danh sách (Home country = Mỹ, các vai trò khác) được mở thử để ghi lại.

## 1. Đăng ký và onboarding

Trước phần hồ sơ tính cơ hội, người dùng đi qua 6 màn: chọn sản phẩm, chào mừng, thông tin cơ bản, nguồn biết đến, trường quan tâm và ưu tiên khi chọn trường.

### 1.1 Chọn sản phẩm khi đăng ký

Tiêu đề: "Which site are you signing up for?" — "Please choose your CollegeVine destination:". Ba thẻ, chọn một:

| Lựa chọn | Mô tả trên màn | Dành cho |
| --- | --- | --- |
| Agent platform | Nền tảng AI cho mọi phòng ban trong trường đại học | Trường đại học |
| CollegeVine network | Đối tác và tổ chức truy cập mạng lưới học sinh CollegeVine | Đối tác, tổ chức |
| CollegeVine for students | Lập kế hoạch chọn trường, bài luận, tra cứu trường; counselor và advisor cũng đăng nhập ở đây | Học sinh, cố vấn |

Dưới cùng có dòng "Not sure? Contact support".

### 1.2 Màn chào (/onboarding/wizard/welcome)

- Logo trợ lý AI "Sage", lời chào "Welcome to CollegeVine!" và "I'm Sage, your college and career mentor."
- Mục "I can help" liệt kê 3 việc: Calculate your chances of admissions · Review essays · Connect you with admissions officers and current students.
- Nút: Get started.

### 1.3 "Let's start with the basics" (/onboarding/wizard/identity)

Phụ đề: "Knowing a little bit about you helps me personalize your experience". Ban đầu chỉ hiện Tên và Vai trò; các câu sau hiện ra theo vai trò được chọn.

| Trường | Kiểu nhập | Lựa chọn / định dạng | Bắt buộc |
| --- | --- | --- | --- |
| My name is… | 2 ô chữ: First, Last | Không chứa số hoặc ký tự đặc biệt | Có |
| I am a… | Nút chọn 1 | High school student · Parent · Transfer student · High school counselor · Independent Educational Counselor (IEC) | Không đánh dấu |
| Date of birth | Chọn ngày (mm/dd/yyyy), có biểu tượng (?) | Gợi ý (?): phải từ 13 tuổi trở lên | Không đánh dấu |
| When will you graduate high school? | Nút chọn 1 | 2031 · 2030 · 2029 · 2028 · 2027 | Không đánh dấu |
| When do you plan to start your first semester of college? | Dropdown | Winter 2027 → Spring 2030 (xem Phụ lục) | Không đánh dấu |
| Parent or guardian email | Ô email (mẫu parent@example.com) | Ghi chú: giúp phụ huynh theo dõi hồ sơ và truy cập tài nguyên | Không |

Câu hỏi hiện thêm theo vai trò:

| Vai trò | Câu hỏi hiện thêm |
| --- | --- |
| High school student | Date of birth · When will you graduate high school? (2027–2031) · When do you plan to start your first semester of college? · Parent or guardian email |
| Parent | When will your high schooler graduate? (2027–2031) · When does your high schooler plan to start their first semester of college? |
| Transfer student | Date of birth · When did you graduate high school? (2022–2026) · When do you plan to start your first semester at your new college? |
| High school counselor | Không có câu hỏi thêm |
| Independent Educational Counselor (IEC) | Không có câu hỏi thêm |

Nút: Continue.

### 1.4 "How did you hear about CollegeVine?" (/onboarding/wizard/how-found-cv)

Danh sách nút, bấm một nút là chuyển sang màn sau, không có nút Continue:

- School counselor
- Social media — bấm để mở thêm: Instagram · TikTok · YouTube · Facebook
- Google search
- Friend or family member
- Advertisement
- Other

### 1.5 "What schools are you considering?" (/onboarding/wizard/schools)

- Phụ đề: "It's ok if you're not sure yet. I can help you find schools later!"
- 3 ô "Search a school" giống nhau, dạng tìm kiếm theo gõ: bấm vào ô không hiện danh sách, gõ từ 1 ký tự thì hiện các trường có trong cơ sở dữ liệu để chọn.
- Các trường đã chọn được gửi đi dưới dạng danh sách mã trường (school\_ids).
- Có thể để trống cả 3 ô và bấm Continue.

### 1.6 "What's most important?" (/onboarding/wizard/preferences)

Phụ đề: "Pick your top 5 college preferences to improve my recommendations." Nút chip chọn nhiều, bấm lần nữa để bỏ chọn, chia 3 nhóm, 16 lựa chọn:

| Nhóm | Lựa chọn |
| --- | --- |
| ACADEMICS (5) | Experiential and practical learning · College prestige (highly ranked) · Building relationships with professors · Ease of switching majors · Top program for my major |
| STUDENT LIFE (7) | Clubs and extracurriculars · Emotional and mental health resources · Friendly to LGBTQIA+ students · Racial and cultural diversity · Welcoming and supportive campus · Lots of opportunity to socialize · Big sporting events |
| COST AND OUTCOMES (4) | Affordable tuition · Getting a good job in my field · Support to find internships · Making a positive impact on the world |

- Nút chính hiển thị số lựa chọn còn thiếu ("Pick 5 more") và bị khóa cho tới khi đủ 5.
- Link "Skip" bỏ qua bước này, dẫn tới /onboarding/wizard/wicked\_finish.

### 1.7 "Let's calculate your chances!" (/chancing/profile/start)

- Hình đồng hồ đo nửa vòng kèm logo Sage.
- Mục "After that, I can help you": Understand and improve your admissions chances · Connect with admissions officers · Find early acceptance offers and scholarships.
- Nút Continue mở phần hồ sơ tính cơ hội.

## 2. Hồ sơ – Bước 1: Demographics

Phần hồ sơ có thanh tiến trình 5 bước ở đầu trang: Demographics → Test scores → Grades → Coursework → Extracurriculars. Mỗi bước mở đầu bằng một khung nền be giải thích giai đoạn xét tuyển tương ứng.

### 2.1 "1. Demographics" (/chancing/profile/demographics)

Khung giới thiệu: giai đoạn đầu của xét tuyển phân tích bối cảnh của học sinh như địa lý, môi trường trường cấp 3, tài chính và giới tính.

Các câu hỏi hiện ra phụ thuộc vào ô Home country. Câu "international / US residence rights" không làm thay đổi các câu hỏi khác.

| Câu hỏi | Kiểu nhập | Lựa chọn | Bắt buộc | Hiện khi |
| --- | --- | --- | --- | --- |
| Mailing address – Home country | Dropdown tìm kiếm | Danh sách quốc gia (United States of America đứng đầu, sau đó theo bảng chữ cái; Việt Nam ghi "Viet Nam") | Có | Luôn hiện |
| Zip code | Ô chữ | — | Có | Home country = Mỹ hoặc chưa chọn |
| State | Ô chọn | Các bang | Không | Home country = Mỹ |
| City | Ô chữ | — | Có | Home country ≠ Mỹ |
| Applying as international or do you have US residence rights? | Radio | I have US residence rights · I plan to apply as international | Không | Luôn hiện |
| Gender | Radio, có biểu tượng (?) | Male · Female · Non-binary / third gender · Prefer not to say · Prefer to self-describe | Có | Luôn hiện |
| Self-describe here | Ô chữ | — | Không | Gender = Prefer to self-describe |
| Race or ethnicity | Radio | American Indian or Alaska Native · Asian · Black or African American · Hispanic or Latino · Native Hawaiian or Other Pacific Islander · White · Other ethnicity, biracial, or multiracial · Prefer not to say | Không | Home country = Mỹ |
| Are you planning to apply for financial aid? | Radio | I plan to apply for financial aid · I do not plan to apply for financial aid · I don't know yet | Không | Home country = Mỹ |
| What is the maximum amount that your family can spend on college each year? | Thanh trượt 0–100.000 USD + lựa chọn Prefer not to say | — | Không | Home country = Mỹ |
| What is your family's household income? | Dropdown | 8 mức (bảng dưới) | Không | Home country = Mỹ |
| Nationality | Dropdown tìm kiếm | Danh sách tên quốc gia (dùng chung danh sách với Home country) | Có | Home country ≠ Mỹ |
| How much can your family spend per year for college in the United States? | Radio | More than $80,000 USD · Less than $80,000 USD · I'm not sure | Không | Home country ≠ Mỹ |
| What is the maximum amount that your family can spend on college each year? | Thanh trượt 0–80.000 USD, mặc định 40.000 + Prefer not to say | — | Không | Câu trên = Less than $80,000 USD |
| Are you seeking scholarships? | Radio | I'm interested in scholarships · I'm not interested in scholarships · I don't know yet | Không | Home country ≠ Mỹ |

Dòng thông tin dưới câu financial aid (luồng Mỹ): 86,4% sinh viên năm nhất lần đầu nộp đơn và nhận một hình thức hỗ trợ tài chính.

Câu dẫn trước phần tài chính (luồng quốc tế): các trường Mỹ thường yêu cầu chứng minh đủ tài chính cho một năm học.

Các mức thu nhập gia đình (luồng Mỹ):

| # | Mức |
| --- | --- |
| 1 | Less than $60,000 per year |
| 2 | $60,001 - $100,000 per year |
| 3 | $100,001 - $150,000 per year |
| 4 | $150,001 - $200,000 per year |
| 5 | $200,001 - $300,000 per year |
| 6 | $300,001 - $400,000 per year |
| 7 | More than $400,000 per year |
| 8 | I'm not sure |

Nút: Continue.

### 2.2 "Demographics continued" (/chancing/profile/demographics-continued)

Khung giới thiệu: "Just a few more questions about your background for better accuracy."

Phần đầu (được dùng để tính cơ hội):

| Câu hỏi | Kiểu nhập | Lựa chọn / hành vi | Bắt buộc |
| --- | --- | --- | --- |
| High school | Ô tìm kiếm "Type to search high school" | Ghi chú: học nhiều trường thì nhập trường gần nhất | Không (optional) |
| Homeschool | Checkbox | Tích vào thì ẩn ô tìm trường cấp 3 | Không |
| My high school is not listed or I have a non-traditional educational background | Checkbox | Tích vào thì ô tìm kiếm đổi thành ô nhập tự do "Type in your high school or non-traditional education" | Không |
| Intended major | Dropdown tìm kiếm, mặc định "Undecided" | 277 ngành (xem Phụ lục); ghi chú: một số ngành đòi hỏi hồ sơ mạnh hơn | Không đánh dấu |

Dòng phân cách: các câu dưới đây chưa ảnh hưởng tới kết quả tính cơ hội trên CollegeVine, nhưng sẽ được dùng trong thời gian tới.

| Câu hỏi | Kiểu nhập | Lựa chọn | Bắt buộc |
| --- | --- | --- | --- |
| Will you be a first-generation college student? (?) | Radio | Yes, I'm a first-generation college student · No, I'm not a first-generation college student · I do not wish to identify | Không |
| Legacy relationship (?) | Ô tìm kiếm trường, chọn được nhiều trường | Hỏi trường bậc cử nhân mà phụ huynh/người giám hộ đã tốt nghiệp; chỉ chọn trường bậc đại học | Không |
| What is your religious affiliation? | Dropdown | 20 lựa chọn (xem Phụ lục) | Không |
| Are you a recruited athlete? | Radio | Yes, I'm being recruited by D1/D2/D3 schools · Maybe, I'm considering getting recruited for athletics · No | Không |
| Are you considering special school types? | Chip chọn nhiều | HBCU (Historically Black College and University) · All-Women's · Military · Liberal Arts · In My Home State · Other | Không |

Nút: Continue.

## 3. Hồ sơ – Bước 2 đến 4: Test scores, Grades, Coursework

### 3.1 "2. Test scores" (/chancing/profile/test-scores)

Khung giới thiệu: Sage có thể khuyên nên nộp điểm hay nộp theo diện test-optional ở các trường test-optional.

**Chọn loại bài thi** — "Which standardized test scores do you have?", "Select all that apply", checkbox chọn nhiều:

- I have SAT scores
- I have ACT scores
- I have PSAT scores
- I don't have any test scores — tích ô này thì 3 ô trên tự bỏ tích; tích lại một bài thi thì ô này tự bỏ tích

**Khối nhập điểm** (chỉ hiện khối của bài thi đã tích; nhập từng phần, không nhập tổng):

| Bài thi | Ô điểm | Giới hạn | Số lần thi |
| --- | --- | --- | --- |
| SAT | Math · Reading and Writing | 200–800, bước 10 | "How many times have you taken the SAT": số nguyên ≥ 1, nút − / + |
| ACT | Math · Reading · English · Science | 1–36, bước 1 | "How many times have you taken the ACT": số nguyên ≥ 1, nút − / + |
| PSAT | Math · Reading and Writing | 200–800, bước 10 | Không hỏi |

**AP Exams (Optional)** — ghi chú: ở một số trường, điểm thi AP cao có thể cải thiện cơ hội.

- Nút "Add AP exam" mở một thẻ gồm Subject (dropdown "Please select a subject…", 38 môn, xem Phụ lục) và Score (1–5), nút Cancel / Add.
- Thêm được nhiều môn, mỗi môn một dòng.

**English language proficiency test** — câu dẫn: học sinh đến từ nước có ngôn ngữ giảng dạy không phải tiếng Anh phải chứng minh năng lực tiếng Anh để đủ điều kiện nhập học trực tiếp.

| Câu hỏi | Kiểu nhập | Lựa chọn | Hiện khi |
| --- | --- | --- | --- |
| Do you have a TOEFL, IELTS, Duolingo, or PTE test (planned or completed) to report? | Radio | Yes · No | Luôn hiện |
| Is this test completed or planned? | Radio | Completed · Planned | Câu trên = Yes |
| Which test did you take? | Dropdown | Duolingo English Test · TOEFL Paper · TOEFL iBT · IELTS · PTE | Câu đầu = Yes |
| When did you take the test? (Completed) / When do you plan to take the test? (Planned) | 2 dropdown: tháng + năm | Tháng: January–December. Năm: Completed 2023–2026; Planned 2026–2027 | Sau khi chọn bài thi |
| Mã số dự thi | Ô chữ | TOEFL: "Registration number"; IELTS: "Test report form number"; Duolingo: không hỏi | Sau khi chọn bài thi |
| Have you received your scores yet? | Radio | Yes · No (No thì ẩn phần điểm) | Sau khi chọn bài thi, ở cả Completed và Planned |
| Scores | Dropdown cho mỗi thành phần | Bảng dưới | Câu trên = Yes |

| Bài thi | Thành phần điểm | Thang mỗi thành phần | Điểm tổng |
| --- | --- | --- | --- |
| Duolingo English Test | Literacy · Conversation · Comprehension · Production | 0–160, bước 5 | Overall score (chọn) |
| TOEFL Paper | Listening · Reading · Writing | 0–30 | "Total:" tự tính |
| TOEFL iBT | Listening · Reading · Writing · Speaking | 0–30 | "Total:" tự tính |
| IELTS | Listening · Reading · Writing · Speaking | 0,5–9,0, bước 0,5 | Overall band score (chọn) |
| PTE | Listening · Reading · Writing · Speaking · Grammar · Oral fluency · Pronunciation · Spelling · Vocabulary · Written discourse | 10–90 | Overall score (chọn) |

Nút: Continue.

### 3.2 "3. Grades" (/chancing/profile/grades)

Khung giới thiệu: cán bộ tuyển sinh đánh giá hồ sơ học thuật, bắt đầu từ GPA và các lớp đã học.

| Câu hỏi | Kiểu nhập | Lựa chọn / giới hạn | Bắt buộc |
| --- | --- | --- | --- |
| High school's grading system | Dropdown, mặc định 0-4 | 0-4 · 0-5 · 0-7 · 0-10 · 0-15 · 0-20 · 0-100 | Không đánh dấu |
| Unweighted GPA | Ô số "Enter GPA" | Giới hạn theo thang đã chọn (bảng dưới) | Có |
| I'm not sure | Nút mở công cụ ước tính GPA | Chỉ hiện ở thang 0-4 | — |
| Class rank (optional) | Ô số | 1–100 | Không |
| Class size (optional) | Ô số | 1–100 | Không |
| My high school doesn't do class rank | Checkbox | — | Không |

| Thang | Giới hạn ô GPA | Bước |
| --- | --- | --- |
| 0-4 | 0–4,0 | 0,01 |
| 0-5 | 0–5,0 | 0,01 |
| 0-7 | 0–7,0 | 0,01 |
| 0-10 | 0–10,0 | 0,01 |
| 0-15 | 0–15,0 | 0,1 |
| 0-20 | 0–20,0 | 0,1 |
| 0-100 | 0–100,0 | 0,1 |

**Công cụ ước tính GPA** — tiêu đề "Not sure about your unweighted GPA? Let us help you estimate it", câu hỏi "What grades do you usually get?". Bấm một mức thì hiện dòng "Your estimated GPA: …". Link "Never mind, I'd rather input it myself" quay về ô nhập GPA. Chọn một mức là đủ để qua bước; trang kết quả sau đó hiển thị GPA hồ sơ là giá trị giữa khoảng (ví dụ 3,8 cho mức A, A-).

| Điểm thường đạt | GPA ước tính |
| --- | --- |
| A+, A | 3,9–4,0 |
| A, A- | 3,7–3,9 |
| A-, B+ | 3,4–3,6 |
| B+, B | 3,1–3,3 |
| B, B- | 2,8–3,0 |
| B-, C+ | 2,4–2,6 |
| C+, C | 2,1–2,3 |
| C, C- | 1,8–2,0 |
| C-, D+ | 1,4–1,6 |
| D+, D, D- | 1,1–1,3 |

Nút: Continue.

### 3.3 "4. Coursework" (/chancing/profile/coursework)

Khung giới thiệu: cán bộ tuyển sinh đánh giá các lớp đã học dựa trên những lớp mà trường cấp 3 có mở.

Câu chính: "How many classes do you plan to complete by the end of senior year (12th grade)?" — đếm tất cả các lớp từ lớp 9 đến lớp 12.

| Bộ đếm | Kiểu nhập | Giới hạn | Hiện khi |
| --- | --- | --- | --- |
| Honors classes | Ô số + nút − / + | 0–50 | Luôn hiện |
| AP | Ô số + nút − / + | 0–50 | Luôn hiện |
| College courses taken at a local four-year or community college | Ô số + nút − / + | 0–50 | Luôn hiện |
| IB HL · IB SL | 2 ô số + nút − / + | 0–50 | Sau khi bấm "Add IB" |
| GCSE/IGCSE | Ô số + nút − / + | 0–50 | Sau khi bấm "Add GCSE, IGCSE" |
| Cambridge AICE | Ô số + nút − / + | 0–50 | Sau khi bấm "Add Cambridge AICE" |
| CBSE | Ô số + nút − / + | 0–50 | Sau khi bấm "Add CBSE" |
| Other | Ô số + nút − / + | 0–50 | Sau khi bấm "Add Other" |
| How many AP/IB classes does your high school offer? (optional) (?) | Ô số | ≥ 1 | Luôn hiện |

- Mỗi nút "Add …" biến mất sau khi bấm và để lại bộ đếm tương ứng.
- Bộ đếm để trống không qua được bước; phải nhập 0 nếu không có.

Nút: Continue.

## 4. Hồ sơ – Bước 5: Extracurriculars

Mỗi hoạt động được gán một loại (18 loại) và một nấc thành tích trên thang tối đa 12 nấc; mỗi loại dùng một tập nấc riêng với tiêu chí riêng.

### 4.1 Trang danh sách (/chancing/profile/extracurriculars)

- Khung giới thiệu: sau học thuật, cán bộ tuyển sinh đánh giá độ mạnh của hồ sơ ngoại khóa.
- Tiêu đề "Add up to 10 unique extracurricular activities" — tối đa 10 hoạt động.
- Thẻ "+ Add new activity" kèm dòng đếm ngược "You can add N more activities".
- Mỗi hoạt động đã lưu hiện thành một dòng: tên, "\<Loại> | Tier \<nấc>: \<mức>", nút Edit, Remove.
- Dòng nhắc: kết quả tính cơ hội có thể thấp hơn nếu không nhập đủ hoạt động; có thể thêm, sửa hoạt động bất cứ lúc nào trong Profile.
- Nút: Continue.

### 4.2 Hộp thoại "Add activity"

| Thành phần | Kiểu nhập | Hành vi | Bắt buộc |
| --- | --- | --- | --- |
| Activity name | Ô tìm kiếm, có nút xóa × | Gõ chữ thì gọi tìm kiếm trong danh mục hoạt động; chọn một mục thì hiện dòng "Category: \<loại>" ngay dưới ô | Có |
| Level of achievement (?) | Kim tự tháp 4 tầng (1 Exceptional ở đỉnh, 4 Standard ở đáy) + nút ↑ ↓ | Hiện "Tier \<nấc>: \<mức>", câu "I did the equivalent of ANY of the following:" và các tiêu chí của nấc; ↑ lên nấc cao hơn, ↓ xuống nấc thấp hơn; nút bị khóa ở hai đầu thang của loại đó; nấc mặc định khi mới chọn hoạt động là nấc thấp nhất của loại | Có |
| Description (optional) | Ô văn bản nhiều dòng | Gợi ý: tóm tắt vai trò, nêu thành tích cụ thể, kết quả có số liệu và kỹ năng có được | Không |
| Write description for me | Nút (biểu tượng lấp lánh) | AI viết phần mô tả | — |

Tooltip (?) cạnh Level of achievement: chọn mô tả gần đúng với việc mình làm, dùng mũi tên để lên xuống; phân vân giữa hai nấc thì chọn nấc cao hơn.

Nút: Save activity · Cancel · × đóng hộp thoại.

Danh mục hoạt động được tìm qua API: mỗi lần gõ gửi từ khóa (search\_term), nhận về danh sách gồm mã số, tên hoạt động và mã loại. Danh mục gồm hàng trăm mục, kể cả tên chương trình và tổ chức cụ thể.

### 4.3 Thang nấc

12 nấc theo thứ tự tăng dần: 4c < 4b < 4a < 3c < 3b < 3a < 2c < 2b < 2a < 1c < 1b < 1a. Tên mức: bậc 4 = Standard, bậc 3 = Strong, bậc 2 = Very strong, bậc 1 = Exceptional.

### 4.4 18 loại hoạt động

| Mã nội bộ | Tên hiển thị | Số nấc | Nấc thấp nhất → cao nhất | Ví dụ hoạt động trong danh mục |
| --- | --- | --- | --- | --- |
| Writing | Writing | 10 | 4c → 1a | School Newspaper, Journalism, Yearbook, Blogging |
| NonArtComps | Non-Art Competitions | 9 | 4b → 1a | Math Team, Science Olympiad, Math Olympiad, Debate, Speech and Debate |
| CommService | Community Service | 9 | 4c → 1b | Volunteering, Hospital Volunteer, Key Club, Red Cross Volunteer |
| Sports | Sports | 9 | 4c → 1a | Soccer, Basketball, Martial Arts, Competitive Dance, Esports |
| Music | Music | 9 | 4c → 1a | Band, Marching Band, Jazz Band, Church Choir, Music Production |
| VisualArts | Visual Arts | 9 | 4c → 1a | Art, Digital Art, Filmmaking, Photography |
| Research | Research | 8 | 3c → 1a | Research, Independent Research, Research Assistant, Biology Research |
| Drama | Drama | 8 | 4b → 1c | Theater, Musical Theatre, School Musical, Theater Tech |
| HonorSocieties | Honor Societies | 7 | 3c → 1b | National Honor Society, Math / Science / Computer Science Honor Society |
| Politics | Politics | 7 | 4b → 1b | Campaign Volunteer, Campaign Intern, Youth Council |
| Employment | Employment | 6 | 3c → 1b | Part-Time Job, Summer Job, Internship, Congressional Internship |
| OwnInitiative | Own Initiative | 6 | 3b → 1a | Startup, Small Business, Online Business, YouTube Channel |
| SummerPrograms | Summer Programs | 6 | 4a → 1b | RSI, Summer Science Program, Governor's School, NASA Internship |
| Software | Software | 5 | 4a → 1b | Programming, App Development, Website Development, Hackathon |
| AffinityGroups | Affinity Groups | 5 | 4a → 1b | Diversity Council, International Student Alliance |
| InterestClubs | Interest Clubs | 5 | 4a → 1b | Robotics Club, Chess Club, Coding Club, Debate Club, Art Club |
| StudentGov | Student Government | 4 | 3b → 2a | Student Council, Class Council, Honor Council |
| Hobbies | Hobbies | 4 | 4a → 1b | Woodworking, Working Out, Dance, Family Responsibilities |

### 4.5 Tiêu chí từng nấc theo loại (diễn giải)

Mỗi nấc liệt kê một hoặc vài tiêu chí; đạt một tiêu chí bất kỳ là được nấc đó.

**Non-Art Competitions**

| Nấc | Tiêu chí |
| --- | --- |
| 4b | Thi ở cấp trường hoặc địa phương |
| 3c | Thi cấp bang/vùng; hoặc đoạt giải cấp trường |
| 3a | Thuộc đội vào tới vòng cấp bang qua quá trình cạnh tranh |
| 2c | Giữ vai trò lãnh đạo cấp trường; hoặc lập CLB thi đấu ở trường đạt 15+ thành viên |
| 2b | Giải cá nhân cấp bang; hoặc giữ vị trí lãnh đạo cao nhất cấp trường |
| 2a | Lập CLB thi đấu đạt 50+ thành viên hoặc làm chủ tịch; đội vào vòng quốc gia; lãnh đạo cấp bang; giải cá nhân cao nhất cấp bang |
| 1c | Giải cá nhân cấp quốc gia; hoặc lãnh đạo cao nhất cấp bang của một tổ chức |
| 1b | Top 20 toàn quốc ở cuộc thi cá nhân cao nhất của lĩnh vực |
| 1a | Thắng cuộc thi cá nhân quốc gia danh giá; hoặc vào kỳ thi quốc tế rất chọn lọc (khoảng 2.000 người tranh 50 suất trở xuống) |

**Community Service**

| Nấc | Tiêu chí |
| --- | --- |
| 4c | Dưới 30 giờ tình nguyện |
| 4a | Trên 30 giờ; thành viên CLB thiện nguyện ở trường; hoặc dẫn dắt sáng kiến gây quỹ trên 100 USD |
| 3c | Thành viên nòng cốt CLB thiện nguyện; sáng kiến gây quỹ 1.000+ USD hoặc 10+ người tham gia; 100+ giờ ở một hoạt động trong 6+ tháng |
| 3b | Giải thưởng phục vụ cộng đồng (trong hoặc ngoài trường) |
| 2c | Lãnh đạo CLB thiện nguyện lớn (>15 người); sáng kiến 100+ người tham gia; hoặc sáng lập/chủ tịch tổ chức nhỏ (<15 người) |
| 2b | Lãnh đạo cao nhất CLB thiện nguyện lớn; hoặc sáng kiến gây quỹ trên 10.000 USD |
| 2a | Lãnh đạo cấp bang; gây quỹ trên 15.000 USD; 500+ người tham gia; hoặc sáng lập tổ chức lớn (>15 người) |
| 1c | Lãnh đạo cao nhất cấp bang; gây quỹ trên 35.000 USD; hoặc 1.000+ người tham gia |
| 1b | Ban lãnh đạo toàn quốc của tổ chức; gây quỹ trên 100.000 USD; hoặc 3.500+ người tham gia |

**Employment**

| Nấc | Tiêu chí |
| --- | --- |
| 3c | Việc làm theo giờ ở doanh nghiệp địa phương (thu ngân, phục vụ, thư ký) |
| 3a | Thực tập không lương ở doanh nghiệp nhỏ hoặc cơ quan nhà nước địa phương |
| 2c | Việc có lương với trách nhiệm đáng kể (ví dụ trợ lý quản lý); hoặc thực tập cho chính trị gia cấp bang |
| 2a | Thực tập không lương ở tổ chức phi lợi nhuận hoặc doanh nghiệp vừa/lớn, có thành tích cụ thể được công ty dùng |
| 1c | Thực tập cho thượng nghị sĩ/hạ nghị sĩ Mỹ; hoặc thực tập có lương ở tập đoàn lớn |
| 1b | Thực tập có lương, cạnh tranh ngang sinh viên đại học, ở tập đoàn hàng đầu |

**Research**

| Nấc | Tiêu chí |
| --- | --- |
| 3c | Trả phí tham gia chương trình nghiên cứu |
| 3a | Nghiên cứu độc lập với cố vấn bậc đại học (không công bố); hoặc thực tập nghiên cứu ở công ty tư nhân |
| 2c | Nghiên cứu đã hoặc sẽ được công bố |
| 2b | Nghiên cứu qua chương trình có xét tuyển của trường đại học được kiểm định |
| 2a | Đồng tác giả bài trên tạp chí khoa học top 250 |
| 1c | Được chọn vào chương trình nghiên cứu danh dự có xét tuyển |
| 1b | Đồng chủ trì nghiên cứu đăng trên tạp chí top 30 |
| 1a | Chủ trì đột phá nghiên cứu đăng trên tạp chí top 10 |

**Own Initiative**

| Nấc | Tiêu chí |
| --- | --- |
| 3b | Kinh doanh 2.000 USD/năm; tổ chức tự lập 10+ người; hoặc tổ chức phi lợi nhuận gây quỹ 1.000+ USD |
| 2c | 10.000 USD/năm; 25+ người; hoặc gây quỹ 5.000+ USD |
| 2a | 20.000 USD/năm; 100+ người; hoặc gây quỹ 10.000+ USD |
| 1c | 50.000 USD/năm; gây quỹ 25.000+ USD; hoặc 200+ người với nhiều người điều hành |
| 1b | 100.000 USD/năm với 1–2 nhân viên; thắng cuộc thi khởi nghiệp; tổ chức 1.000+ người; hoặc gây quỹ 100.000+ USD |
| 1a | 300.000 USD/năm hoặc 10+ nhân viên; thắng cuộc thi khởi nghiệp; startup có vốn đầu tư mạo hiểm hoặc từ chương trình kiểu Shark Tank; hoặc gây quỹ 250.000+ USD |

**Student Government**

| Nấc | Tiêu chí |
| --- | --- |
| 3b | Thành viên hội học sinh |
| 2c | Cán bộ lớp; hoặc lãnh đạo trong hội học sinh |
| 2b | Chủ tịch hội học sinh; hoặc lớp trưởng |
| 2a | Chủ tịch học sinh toàn trường (School President / Head Boy/Girl) |

**Sports**

| Nấc | Tiêu chí |
| --- | --- |
| 4c | Chơi giải trí |
| 4a | Vận động viên đội dự bị (JV) |
| 3b | VĐV đội dự bị có giải; hoặc đội trưởng đội dự bị |
| 2c | VĐV đội chính (varsity) |
| 2b | VĐV đội chính có giải khu vực/liên đoàn; hoặc đội trưởng đội chính |
| 2a | VĐV đội chính có giải cao nhất khu vực/liên đoàn |
| 1c | Được công nhận cấp bang (ví dụ All-State) |
| 1b | Đội tuyển cấp bang; top 100 toàn quốc theo lứa tuổi; hoặc VĐV xuất sắc nhất năm của bang |
| 1a | Đội tuyển quốc gia trẻ; hoặc vô địch quốc gia |

**Music**

| Nấc | Tiêu chí |
| --- | --- |
| 4c | Chơi nhạc/hát giải trí |
| 4a | Thành viên một nhóm nhạc trong hoặc ngoài trường |
| 3a | Thành viên nhóm nhạc cao nhất của trường |
| 2c | Người chơi chính/trưởng bè ở nhóm cao nhất trong trường hoặc thị trấn |
| 2b | Vào nhóm tuyển chọn cấp vùng/quận; chỉ huy ban nhạc diễu hành; hoặc trưởng nhóm nhạc |
| 2a | Vào nhóm tuyển chọn cấp bang; chứng chỉ cao nhất cho nhạc cụ (ví dụ ABRSM); hoặc bài hát có khoảng 10.000 lượt nghe trực tuyến |
| 1c | Ghế số 1 dàn nhạc cấp bang; nhóm tuyển chọn cấp quốc gia; hoặc khoảng 25.000 lượt nghe |
| 1b | Thuộc 10–20 người giỏi nhất nước về nhạc cụ; hợp đồng thu âm uy tín; hoặc khoảng 75.000 lượt nghe |
| 1a | Top 2–3 toàn quốc theo lứa tuổi; hợp đồng thu âm 100.000+ USD; hoặc khoảng 500.000 lượt nghe một bài |

**Software**

| Nấc | Tiêu chí |
| --- | --- |
| 4a | Viết chương trình cho vui |
| 3b | Đăng ứng dụng lên App Store/Google Play; hoặc đóng góp dự án mã nguồn mở |
| 2b | Ứng dụng đạt 10.000 lượt tải |
| 1c | Phần mềm được dùng thương mại; hoặc ứng dụng trên 100.000 lượt tải |
| 1b | Ứng dụng trên 500.000 lượt tải; hoặc web app trên 1 triệu lượt truy cập/năm |

**Drama**

| Nấc | Tiêu chí |
| --- | --- |
| 4b | Vai quần chúng trong kịch của trường |
| 3c | Vai có thoại trong kịch trường; hoặc thành viên hậu đài / dàn nhạc kịch |
| 3b | Vai diễn trong vở kịch không phải của học sinh ở nhà hát thành phố |
| 3a | Vai quan trọng trong kịch trường |
| 2c | Vai không phải chính nhưng rất quan trọng |
| 2b | Vai chính; kịch tự viết được trường dựng; trưởng hậu đài; hoặc vào chương trình sân khấu có tuyển chọn |
| 2a | Giải diễn xuất/viết cấp bang; chương trình sân khấu tuyển chọn danh giá; hoặc kịch tự viết được nhà hát chuyên nghiệp dựng |
| 1c | Giải diễn xuất/viết cấp quốc gia; hoặc vai chính ở nhà hát lớn của thành phố |

**Visual Arts**

| Nấc | Tiêu chí |
| --- | --- |
| 4c | Vẽ, sơn hoặc làm video cho vui |
| 4a | Thành viên CLB mỹ thuật/phim của trường |
| 3b | 20+ video chất lượng, mỗi video 1.000+ lượt xem; hoặc giải mỹ thuật cấp trường |
| 2c | Lãnh đạo CLB mỹ thuật/phim; hoặc giải Silver Key (Scholastic) quốc gia |
| 2b | Giải mỹ thuật cấp thành phố/vùng; hoặc phim chiếu ở liên hoan phim thành phố/vùng |
| 2a | Kênh video thường xuyên 10.000+ lượt xem/video; giải cấp bang; chủ tịch CLB; hoặc công ty sản xuất video làm cho doanh nghiệp địa phương |
| 1c | Giải Gold Key (Scholastic); hoặc phim chiếu ở liên hoan phim vùng được công nhận |
| 1b | Bài dự thi đứng đầu hạng mục Scholastic; phim chiếu ở liên hoan phim hàng đầu; hoặc 100.000+ lượt xem/video |
| 1a | Bán tác phẩm 5.000+ USD; phim chiếu ở liên hoan phim quốc tế hàng đầu; hoặc 1 triệu+ lượt xem/video |

**Writing**

| Nấc | Tiêu chí |
| --- | --- |
| 4c | Viết cho vui |
| 4a | Đóng góp 1–2 bài cho báo/ấn phẩm trường |
| 3c | 2–10 bài cho báo trường; bài trên tạp chí văn học của trường; hoặc blog dưới 10.000 chữ |
| 3b | Phóng viên báo trường hoặc trên 10 bài; hoặc blog từ 10.000 chữ |
| 2c | Phó/biên tập viên ấn phẩm trường; hoặc blog từ 50.000 chữ, thiết kế chuyên nghiệp |
| 2b | Tự xuất bản sách dưới 100 trang (Amazon hoặc tự xuất bản) |
| 2a | Tự xuất bản sách trên 100 trang; viết cho ấn phẩm hạng 2; blog trên 250.000 lượt xem; giải viết luận hạng 2; đăng tạp chí văn học; hoặc sáng lập/tổng biên tập ấn phẩm trường |
| 1c | Bài đăng ở ấn phẩm quốc gia; viết cho ấn phẩm hạng 2; giải viết luận hàng đầu; đăng tạp chí văn học top 100; hoặc lập ấn phẩm riêng có cây bút ngoài địa phương, 100.000+ chữ |
| 1b | Sách in bởi NXB có tên tuổi; nhiều bài cho ấn phẩm quốc gia hàng đầu; hoặc tác phẩm đăng ở tạp chí văn học top 20 |
| 1a | Hợp đồng sách với một trong 4 NXB lớn; cây bút của ấn phẩm quốc gia hàng đầu; hoặc truyện/kịch đăng ở tạp chí văn học hạng nhất |

**Honor Societies**

| Nấc | Tiêu chí |
| --- | --- |
| 3c | Thành viên hội danh dự theo môn ở trường |
| 3b | Thành viên hội danh dự cấp trường (ví dụ National Honor Society) |
| 3a | Thành viên có đóng góp đáng kể (ví dụ 25 giờ xây dựng chương trình gia sư của hội) |
| 2c | Lãnh đạo trong hội danh dự cấp trường/địa phương |
| 2a | Chủ tịch hội danh dự cấp trường/địa phương |
| 1c | Lãnh đạo cấp bang; hội quốc gia dưới 1.000 suất với hơn 20.000 người nộp; hoặc hội cấp bang công nhận dưới 5% người dự |
| 1b | Hội quốc gia 50–100 suất với hơn 10.000 người nộp; hoặc lãnh đạo cao nhất cấp bang |

**Summer Programs**

| Nấc | Tiêu chí |
| --- | --- |
| 4a | Chương trình hè trả phí, không xét tuyển |
| 3c | Du học qua trường; hoặc chương trình hè trả phí có xét tuyển |
| 2c | Chương trình hè miễn phí có xét tuyển, gắn với trường đại học hoặc tổ chức uy tín |
| 2b | Một trong: SIP, EFL, SAMS, HCSSIM, JCamp, MathILy, HSHSP, Clark Scholars, Notre Dame Summer Scholars |
| 1c | Một trong: RISE, MITES, SuMaC, Boys/Girls State, Governor's School |
| 1b | Một trong: TASP, RSI, PROMYS, SSP, Boys/Girls Nation |

**Hobbies**

| Nấc | Tiêu chí |
| --- | --- |
| 4a | Theo đuổi sở thích trong thời gian dài |
| 2c | Có sản phẩm cụ thể chứng minh (ví dụ phục chế xe, làm game đăng trực tuyến, kênh video hướng dẫn) |
| 1c | Chủ động lớn với sở thích, có thành tựu ghi nhận được (ví dụ quản trị diễn đàn 500 thành viên) |
| 1b | Được công nhận rộng rãi hoặc quy mô lớn (ví dụ game trực tuyến 15.000 người dùng) |

**Affinity Groups**

| Nấc | Tiêu chí |
| --- | --- |
| 4a | Thành viên nhóm cộng đồng/bản sắc cấp trường |
| 2c | Lãnh đạo nhóm cấp trường |
| 2a | Chủ tịch hoặc sáng lập nhóm cấp trường; hoặc lãnh đạo cấp bang |
| 1c | Lãnh đạo cao nhất cấp bang |
| 1b | Lãnh đạo hoặc thành viên ban điều hành toàn quốc |

**Interest Clubs**

| Nấc | Tiêu chí |
| --- | --- |
| 4a | Thành viên CLB sở thích cấp trường |
| 2c | Lãnh đạo CLB sở thích cấp trường |
| 2a | Chủ tịch hoặc sáng lập CLB; hoặc tổ chức sự kiện/hội nghị cấp thành phố/vùng |
| 1c | Tổ chức sự kiện cấp bang; hoặc đứng đầu nhóm sở thích cấp bang |
| 1b | Tổ chức sự kiện/hội nghị toàn quốc |

**Politics**

| Nấc | Tiêu chí |
| --- | --- |
| 4b | Vận động/gọi điện cho chiến dịch chính trị địa phương |
| 4a | Vận động cho chiến dịch cấp bang/quốc gia; hoặc thành viên CLB chính trị ở trường |
| 2c | Vai trò lãnh đạo (không phải chủ tịch) nhóm vận động chính sách ở trường; hoặc chủ tịch nhóm dưới 25 người |
| 2b | Chủ tịch nhóm vận động ở trường trên 25 người; hoặc sáng lập nhóm dưới 25 người |
| 2a | Sáng lập nhóm trên 25 người; hoặc vai trò chiến lược trong chiến dịch địa phương |
| 1c | Vai trò chiến lược có lương trong chiến dịch cấp bang |
| 1b | Vai trò chiến lược có lương trong chiến dịch cấp quốc gia |

## 5. Màn Network và trang kết quả

Sau bước ngoại khóa là màn Network, rồi tới danh sách khoảng 2.900 trường, mỗi trường có % cơ hội và một trong 4 nhóm Safety, Target, Hard Target, Reach.

### 5.1 "LAST STEP · Network" (/chancing/profile/network)

- Tiêu đề "Network", phụ đề: gặp các trường đã xác minh, nhận thư mời nhập học và học bổng.
- Checkbox "Join the Network" — mặc định đã tích; mô tả: cho các trường đã xác minh tìm thấy hồ sơ và tuyển học sinh.
- Checkbox "Auto-accept connection requests from schools on my list" (nhãn Recommended) — mặc định đã tích; chỉ hiện khi "Join the Network" đang tích.
- Mục "HOW IT WORKS": Connect with colleges (chia sẻ hồ sơ, trò chuyện với cán bộ tuyển sinh và sinh viên) · Get acceptances and scholarships (trường trong mạng lưới gửi thư mời nhập học và học bổng; link Learn more).
- Nút: See my chances → chuyển tới /schools/hub/all?simulator=open.

### 5.2 Trang danh sách trường (/schools/hub/all)

**Thanh điều hướng**: Home · Schools · My list · Connections · Apply · trợ lý AI (nhãn Beta) · thông báo · tin nhắn · ảnh đại diện.

**Bộ lọc và sắp xếp**:

- Ô "Search a school".
- All filters · Admissions difficulty (Safety · Target · Hard Target · Reach · Not available; Clear All) · Major/program · State · Public / private.
- Chọn phạm vi "All colleges", nút "Simulate chances", dòng đếm ("2,917 Colleges"), "Sort by: Name".

**Thẻ trường** gồm: tên trường, nhãn nhóm, thành phố/bang, công lập/tư thục, "Your chances" (%), Avg GPA, Cost, Undergrads, link "Calculate my net cost", nút "Add to list". Trường nghệ thuật/âm nhạc hiện "N/A (art or music school)" thay cho %.

**Profile simulator** (khung bên trái, nhãn Beta) — "Explore different profiles to see how they impact your chances." Mỗi mục hiện hai cột PROFILE (giá trị thật) và SIMULATED (giá trị giả lập) kèm chênh lệch:

| Mục | Cách chỉnh | Giới hạn |
| --- | --- | --- |
| GPA | Thanh trượt | 0,0–4,0 |
| SAT | Tổng + thanh trượt SAT Math, SAT Reading | 200–800 mỗi phần |
| ACT | Nút "Add simulated ACT" | — |
| Coursework | AP/IB classes, Honors classes, College courses (nút + / −) | — |
| Extracurriculars | Danh sách hoạt động | — |

Mỗi mục có "Reset simulated"; cuối khung có "Reset changes". Khi giả lập, thẻ trường hiện thêm chênh lệch % cạnh % cơ hội (ví dụ "89%  -1%").

### 5.3 Ngưỡng phân nhóm

Ngưỡng suy ra từ dữ liệu cơ hội trả về cho 2.911 trường với hồ sơ mẫu:

| Nhóm | Khoảng % cơ hội quan sát được | Số trường (hồ sơ mẫu) |
| --- | --- | --- |
| Safety | 80–100 | 2.506 |
| Target | 50–79 | 111 |
| Hard Target | 27–49 | 42 |
| Reach | 2–25 | 72 |
| Không phân nhóm | — | 180 |

### 5.4 Dữ liệu cơ hội của mỗi trường

Trang lấy dữ liệu từ /schools/hub/data/chances-and-financials. Mỗi trường có các trường dữ liệu sau:

| Trường dữ liệu | Giá trị |
| --- | --- |
| classification | safety · target · hard\_target · reach · null |
| score | % cơ hội |
| applicationStrategy | ApplyWithSat · ApplyTestOptional — tính cho cả hai chiến lược, danh sách "all" giữ cả hai, "top" là chiến lược được đề xuất |
| isRecommendedStrategy | true / false |
| shouldApplyTestOptional | true / false |
| academicThresholdStatus | AboveAcademicThreshold · BelowAcademicThreshold |
| courseRigorStrength | PSWeak · PSFair · PSStrong |
| ecStrength | PSWeak · PSFair |
| guidance | GuidanceApplyWithTest (AWTStrongest, AWTWithCloseStrategies) · GuidanceApplyTestOptional (ATOStrongest) |
| specialCase | OpenAdmission · ArtOrMusicSchool · DisqualifiedGender · SchoolAutoAdmission · null |
| sameDemographicStats | Thống kê nhóm cùng bối cảnh (null với hồ sơ mẫu) |
| financials | Dữ liệu tài chính (null với hồ sơ mẫu) |

Ví dụ % cơ hội với hồ sơ mẫu (GPA 3,8; SAT 1380; 2 AP + 4 Honors; 1 hoạt động nấc 2c): Caltech 2%, Brown 3%, Amherst 6%, Bowdoin 8%, Bates 11%, Boston College 16%, Babson 18%, Boston University 19%.

### 5.5 Trang chi tiết trường (/schools/hub/all/d/\<tên-trường>)

Các tab: Overview · Chancing · Cost & scholarships · Majors. Nút "Add to my list".

**Overview** gồm: Your chances (%) và nhóm, Acceptance rate, mô tả ngắn (loại trường, bang, số sinh viên), thẻ đặc điểm (khu vực, vùng, liên đoàn thể thao), website, địa chỉ, điện thoại, các khối:

- Chancing: chiến lược nộp ("Apply with SAT", link "See why"), câu "\<Trường> is a Reach if you apply with SAT", Your chances, Overall acceptance rate, link "See chancing explanation".
- Admissions: tỷ lệ nhận chung, yield rate, tỷ lệ nhận theo giới, tổng số hồ sơ và tỷ lệ theo giới.
- Cost & scholarships: tổng chi phí trong bang / ngoài bang, nút "Estimate my personalized net cost".
- Applications: How to apply (Common App, test optional, thư giới thiệu, xếp hạng lớp, TOEFL cho du học sinh…), tỷ lệ nộp SAT/ACT.
- Students: số sinh viên năm nhất, mức đa dạng, tỷ lệ tốt nghiệp 4 năm và 6 năm, tỷ lệ giữ chân, chính sách giới tính, tỷ lệ du học sinh, cơ cấu giới tính, cơ cấu chủng tộc.
- Academics: tỷ lệ sinh viên/giảng viên, hệ học kỳ, chương trình đặc biệt.
- My custom data: ghi chú riêng (tối đa 500 ký tự).
- Similar schools: trường tương tự kèm tỷ lệ nhận.

**Tab Chancing** (/schools/hub/all/d/\<tên-trường>/chancing) — "Detailed profile breakdown" chia hai pha:

| Pha | Mục | Nội dung hiển thị |
| --- | --- | --- |
| Phase 1 | GPA | Nhãn so sánh (ví dụ "below average"), GPA của học sinh, GPA điển hình của sinh viên trúng tuyển kèm khoảng, mức quan trọng, mức GPA nên đạt, link bài viết |
| Phase 1 | Coursework | Số AP/IB + Honors + College courses, nhãn so sánh với trường (ví dụ "about average for this school"), ghi chú về đánh giá theo lớp trường có mở |
| Phase 1 | SAT | Tổng và từng phần (Math, Reading and Writing) của học sinh so với điển hình kèm khoảng, mức SAT nên đạt |
| Phase 2 | Extracurriculars | Bảng ACTIVITY · TIER · STRENGTH; câu so sánh với học sinh điển hình cùng bối cảnh tại trường |
| Phase 2 | Recommendation letters | Ghi chú về thư giới thiệu |

Mỗi mục có nút "edit" và "show less". Cuối trang có nút "Simulate chances" và mục "LEARN ABOUT CHANCING": các yếu tố được xét gồm coursework, GPA, extracurriculars, demographics, intended major, class rank và điểm thi (nếu có).

## 6. Kiểm tra dữ liệu và thông báo lỗi

Lỗi hiện ngay dưới ô sai khi bấm Continue và giữ người dùng ở lại bước đó.

| Bước | Ô | Điều kiện | Thông báo |
| --- | --- | --- | --- |
| Basics | Họ, tên | Không chứa số hoặc ký tự đặc biệt | "Last name: Name cannot contain numbers or special characters" |
| Basics | Date of birth | Từ 13 tuổi | Tooltip: "You must be at least 13 years old to use CollegeVine" |
| Demographics | Home country, City / Zip code, Gender, Nationality | Bắt buộc (dấu \*) | — |
| Test scores | SAT, PSAT từng phần | Bội số của 10, từ 200 đến 800 | "Please enter each score as a multiple of ten, from 200 to 800" |
| Test scores | ACT từng phần | 1–36 | Giới hạn của ô số |
| Test scores | Số lần thi | Số nguyên ≥ 1 | Giới hạn của ô số |
| Test scores | Điểm AP | 1–5 | Giới hạn của ô số |
| Grades | Unweighted GPA | Bắt buộc, trong giới hạn của thang đã chọn | — |
| Grades | Class rank, Class size | Hạng từ 1 tới sĩ số | "Please enter class rank from 1 to class size" |
| Coursework | Các bộ đếm lớp | Số từ 0 đến 50, không để trống | "Please enter a number between 0 and 50" |
| Coursework | Số lớp AP/IB trường có mở | ≥ 1 hoặc để trống | Giới hạn của ô số |
| Extracurriculars | Activity name | Bắt buộc, chọn từ danh mục | — |
| Extracurriculars | Số hoạt động | Tối đa 10 | Dòng đếm "You can add N more activities" |
| Preferences | Lựa chọn ưu tiên | Đúng 5 hoặc Skip | Nút "Pick 5 more" bị khóa và đếm số còn thiếu |

Các ràng buộc loại trừ và phụ thuộc:

- "I don't have any test scores" loại trừ SAT, ACT, PSAT.
- "Homeschool" ẩn ô tìm trường cấp 3; "not listed" đổi sang ô nhập tự do.
- Câu hỏi Demographics phụ thuộc Home country (Mỹ / nước khác).
- Thanh trượt ngân sách luồng quốc tế chỉ hiện khi chọn "Less than $80,000 USD".
- Phần điểm chứng chỉ tiếng Anh chỉ hiện khi "Have you received your scores yet?" = Yes; câu này hiện ở cả Completed và Planned.
- Công cụ ước tính GPA chỉ có ở thang 0-4.
- "Auto-accept connection requests" chỉ hiện khi "Join the Network" đang tích.

## 7. Phụ lục: danh sách lựa chọn đầy đủ

### 7.1 Kỳ nhập học (Basics)

Winter 2027 · Spring 2027 · Fall 2027 · Winter 2028 · Spring 2028 · Fall 2028 · Winter 2029 · Spring 2029 · Fall 2029 · Winter 2030 · Spring 2030

### 7.2 Thang điểm GPA (Grades)

0-4 · 0-5 · 0-7 · 0-10 · 0-15 · 0-20 · 0-100

### 7.3 Tôn giáo (Demographics continued) — 20 lựa chọn

Adventist · Baptist · Buddhist · Catholic · Christian (Orthodox) · Christian (Other) · Episcopalian · Hindu · Jewish · Lutheran · Methodist · Latter-day Saints · Muslim · None · Pentecostal · Presbyterian · Protestant · Unitarian · Other · Prefer not to answer

### 7.4 Môn AP (Test scores) — 38 môn

| Nhóm | Môn |
| --- | --- |
| Ngoại ngữ | AP Chinese Language and Culture · AP French Language and Culture · AP German Language and Culture · AP Italian Language and Culture · AP Japanese Language and Culture · AP Latin · AP Spanish Language and Culture · AP Spanish Literature and Culture |
| Khoa học | AP Biology · AP Chemistry · AP Environmental Science · AP Physics 1: Algebra-Based · AP Physics 2: Algebra-Based · AP Physics C: Electricity and Magnetism · AP Physics C: Mechanics |
| Toán, tin | AP Calculus AB · AP Calculus BC · AP Computer Science A · AP Computer Science Principles · AP Statistics |
| Xã hội | AP Comparative Government and Politics · AP European History · AP Human Geography · AP Macroeconomics · AP Microeconomics · AP Psychology · AP United States Government and Politics · AP United States History · AP World History: Modern |
| Tiếng Anh | AP English Language and Composition · AP English Literature and Composition |
| Nghệ thuật | AP Art History · AP Music Theory · AP Drawing · AP 2-D Art and Design · AP 3-D Art and Design |
| Capstone | AP Seminar · AP Research |

### 7.5 Chứng chỉ tiếng Anh

Duolingo English Test · TOEFL Paper · TOEFL iBT · IELTS · PTE

### 7.6 Quốc gia / quốc tịch

Danh sách tên quốc gia dùng chung cho Home country và Nationality: United States of America đứng đầu, sau đó theo bảng chữ cái (Afghanistan, Albania, Algeria, American Samoa, Andorra, Angola, Anguilla, Antarctica, Antigua and Barbuda, Argentina, …); Việt Nam ghi "Viet Nam".

### 7.7 Ngành học (Intended major) — 277 lựa chọn

Tên ngành theo hệ phân loại chương trình đào tạo CIP của Mỹ, theo đúng thứ tự trong dropdown:

Undecided · Architectural Technology/Technician · Clinical/Medical Laboratory Science and Allied Professions · Ethnic, Cultural Minority, and Gender Studies · Materials Science · Legal Research and Advanced Professional Studies (Post-LL.B./J.D.) · Homeland Security, Other · Fine and Studio Art · Library Science/Librarianship · International Relations and Affairs · Computer Engineering, General · Biomathematics and Bioinformatics · Agricultural/Biological Engineering and Bioengineering · Communication and Media Studies · Journalism · Agriculture, General · Agricultural Business and Management · Agricultural Mechanization · Agricultural Production Operations · Agricultural and Food Products Processing · Agricultural and Domestic Animal Services · Applied Horticulture and Horticultural Business Services · International Agriculture · Agricultural Public Services · Animal Sciences · Food Science and Technology · Plant Sciences · Soil Sciences · Agriculture, Agriculture Operations, and Related Sciences, Other · Natural Resources Conservation and Research · Natural Resources Management and Policy · Radio, Television, and Digital Communication · Public Relations, Advertising, and Applied Communication · Communication, Journalism, and Related Programs, Other · Communications Technology/Technician · Audiovisual Communications Technologies/Technicians · Graphic Communications · Biological/Biosystems Engineering · Engineering, Other · Engineering Technology, General · Architectural Engineering Technologies/Technicians · Civil Engineering Technologies/Technicians · Electrical Engineering Technologies/Technicians · Electromechanical Instrumentation and Maintenance Technologies/Technicians · Environmental Control Technologies/Technicians · Fishing and Fisheries Sciences and Management · Forestry · Wildlife and Wildlands Science and Management · Natural Resources and Conservation, Other · Architecture · City/Urban, Community and Regional Planning · Environmental Design · Interior Architecture · Landscape Architecture · Area Studies · Communications Technologies/Technicians and Support Services, Other · Computer and Information Sciences, General · Computer Programming · Data Processing · Information Science/Studies · Computer Systems Analysis · Computer Science · Computer Software and Media Applications · Computer Systems Networking and Telecommunications · Computer/Information Technology Administration and Management · Computer and Information Sciences and Support Services, Other · Funeral Service and Mortuary Science · Culinary Arts and Related Services · Education, General · Curriculum and Instruction · Educational Administration and Supervision · Educational Assessment, Evaluation, and Research · International and Comparative Education · Special Education and Teaching · Teacher Education and Professional Development, Specific Levels and Methods · Teacher Education and Professional Development, Specific Subject Areas · Teaching English or French as a Second or Foreign Language · Education, Other · Engineering, General · Aerospace, Aeronautical and Astronautical Engineering · Architectural Engineering · Biomedical/Medical Engineering · Chemical Engineering · Civil Engineering · Electrical, Electronics and Communications Engineering · Engineering Mechanics · Engineering Physics · Engineering Science · Environmental/Environmental Health Engineering · Materials Engineering · Mechanical Engineering · Metallurgical Engineering · Mining and Mineral Engineering · Naval Architecture and Marine Engineering · Nuclear Engineering · Ocean Engineering · Petroleum Engineering · Systems Engineering · Textile Sciences and Engineering · Polymer/Plastics Engineering · Construction Engineering · Forest Engineering · Industrial Engineering · Manufacturing Engineering · Operations Research · Geological/Geophysical Engineering · Electromechanical Engineering · Biochemical Engineering · Industrial Production Technologies/Technicians · Quality Control and Safety Technologies/Technicians · Mechanical Engineering Related Technologies/Technicians · Mining and Petroleum Technologies/Technicians · Construction Engineering Technologies · Computer Engineering Technologies/Technicians · Drafting/Design Engineering Technologies/Technicians · Nuclear Engineering Technologies/Technicians · Engineering-Related Fields · Engineering Technologies/Technicians, Other · Linguistic, Comparative, and Related Language Studies and Services · East Asian Languages, Literatures, and Linguistics · Slavic, Baltic and Albanian Languages, Literatures, and Linguistics · Germanic Languages, Literatures, and Linguistics · Romance Languages, Literatures, and Linguistics · Middle/Near Eastern and Semitic Languages, Literatures, and Linguistics · American Sign Language · Foreign Languages, Literatures, and Linguistics, Other · Family and Consumer Sciences/Human Sciences, General · Family and Consumer Sciences/Human Sciences Business Services · Family and Consumer Economics and Related Studies · Foods, Nutrition, and Related Services · Housing and Human Environments · Human Development, Family Studies, and Related Services · Apparel and Textiles · Non-Professional General Legal Studies (Undergraduate) · Legal Support Services · Legal Professions and Studies, Other · English Language and Literature, General · Rhetoric and Composition/Writing Studies · Literature · English Language and Literature/Letters, Other · Liberal Arts and Sciences, General Studies and Humanities · Biology, General · Biochemistry, Biophysics and Molecular Biology · Cell/Cellular Biology and Anatomical Sciences · Microbiological Sciences and Immunology · Zoology/Animal Biology · Genetics · Physiology, Pathology and Related Sciences · Pharmacology and Toxicology · Biotechnology · Ecology, Evolution, Systematics, and Population Biology · Neurobiology and Neurosciences · Biological and Biomedical Sciences, Other · Mathematics · Applied Mathematics · Statistics · Mathematics and Statistics, Other · Intelligence, Command Control and Information Operations · Multi-/Interdisciplinary Studies, General · Biological and Physical Sciences · Peace Studies and Conflict Resolution · Systems Science and Theory · Mathematics and Computer Science · Biopsychology · Gerontology · Historic Preservation and Conservation · Science, Technology and Society · Behavioral Sciences · Natural Sciences · Nutrition Sciences · International/Global Studies · Intercultural/Multicultural and Diversity Studies · Cognitive Science · Cultural Studies/Critical Theory and Analysis · Human Biology · Marine Sciences · Sustainability Studies · Multi/Interdisciplinary Studies, Other · Parks, Recreation and Leisure Studies · Parks, Recreation and Leisure Facilities Management · Health and Physical Education/Fitness · Parks, Recreation, Leisure, and Fitness Studies, Other · Philosophy · Religion/Religious Studies · Philosophy and Religious Studies, Other · Bible/Biblical Studies · Missions/Missionary Studies and Missiology · Religious Education · Religious/Sacred Music · Theological and Ministerial Studies · Pastoral Counseling and Specialized Ministries · Theology and Religious Vocations, Other · Physical Sciences · Astronomy and Astrophysics · Atmospheric Sciences and Meteorology · Chemistry · Geological and Earth Sciences/Geosciences · Physics · Physical Sciences, Other · Science Technologies/Technicians, Other · Psychology, General · Research and Experimental Psychology · Clinical, Counseling and Applied Psychology · Psychology, Other · Criminal Justice and Corrections · Fire Protection · Homeland Security · Human Services, General · Community Organization and Advocacy · Public Administration · Public Policy Analysis · Social Work · Public Administration and Social Service Professions, Other · Social Sciences, General · Anthropology · Archeology · Criminology · Economics · Geography and Cartography · Political Science and Government · Sociology · Urban Studies/Affairs · Sociology and Anthropology · Rural Sociology · Social Sciences, Other · Building/Construction Finishing, Management, and Inspection · Heavy/Industrial Equipment Maintenance Technologies · Vehicle Maintenance and Repair Technologies · Air Transportation · Marine Transportation · Visual and Performing Arts, General · Crafts/Craft Design, Folk Art and Artisanry · Dance · Design and Applied Arts · Drama/Theatre Arts and Stagecraft · Film/Video and Photographic Arts · Music · Arts, Entertainment,and Media Management · Visual and Performing Arts, Other · Health Services/Allied Health/Health Sciences, General · Communication Disorders Sciences and Services · Dental Support Services and Allied Professions · Health and Medical Administrative Services · Allied Health and Medical Assisting Services · Allied Health Diagnostic, Intervention, and Treatment Professions · Health/Medical Preparatory Programs · Mental and Social Health Services and Allied Professions · Pharmacy, Pharmaceutical Sciences, and Administration · Public Health · Rehabilitation and Therapeutic Professions · Medical Illustration and Informatics · Dietetics and Clinical Nutrition Services · Alternative and Complementary Medicine and Medical Systems · Registered Nursing, Nursing Administration, Nursing Research and Clinical Nursing · Practical Nursing, Vocational Nursing and Nursing Assistants · Health Professions and Related Clinical Sciences, Other · Business/Commerce, General · Business Administration, Management and Operations · Accounting and Related Services · Business Operations Support and Assistant Services · Business/Corporate Communications · Business/Managerial Economics · Entrepreneurial and Small Business Operations · Finance and Financial Management Services · Hospitality Administration/Management · Human Resources Management and Services · International Business · Management Information Systems and Services · Management Sciences and Quantitative Methods · Marketing · Real Estate · Insurance · General Sales, Merchandising and Related Marketing Operations · Specialized Sales, Merchandising and Marketing Operations · Construction Management · Business, Management, Marketing, and Related Support Services, Other · History
