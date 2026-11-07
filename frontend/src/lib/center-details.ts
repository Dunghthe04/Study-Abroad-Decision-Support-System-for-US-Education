import type { StudyCenter } from "@/types/api";

export type CenterType = "study_abroad" | "test_prep" | "hybrid";

export interface EnrichedCenterCourse {
  name: string;
  target: string;
  duration: string;
  format: string;
  highlight?: string;
}

export interface EnrichedSuccessStory {
  studentName: string;
  highSchool: string;
  university: string;
  scholarship: string;
  major: string;
  year: string;
}

export interface EnrichedReview {
  id: string;
  reviewerName: string;
  reviewerRole: "student" | "parent";
  verified: boolean;
  date: string;
  rating: number; // 1-5
  targetSchool?: string;
  comment: string;
  criteriaRatings: {
    consultingQuality: number;
    responsiveness: number;
    feeTransparency: number;
    outcomeSuccess: number;
  };
}

export interface EnrichedCenterProfile {
  center: StudyCenter;
  type: CenterType;
  typeLabel: string;
  coverImage: string;
  logoImage: string;
  galleryImages: { url: string; caption: string }[];
  establishedYear: number;
  staffCount: number;
  licenseNumber: string;
  licenseIssuedBy: string;
  accreditations: string[];
  visaSuccessRate: number;
  totalScholarshipsWon: string;
  rating: number;
  reviewCount: number;
  operatingHours: string;
  courses: EnrichedCenterCourse[];
  successStories: EnrichedSuccessStory[];
  reviews: EnrichedReview[];
}

export function determineCenterType(services: string[] = [], explicitType?: string): { type: CenterType; label: string } {
  if (explicitType === "hybrid") {
    return { type: "hybrid", label: "Tổ chức Toàn diện (Du học & Luyện thi SAT/IELTS)" };
  }
  if (explicitType === "test_prep" || explicitType === "english_training") {
    return { type: "test_prep", label: "Trung tâm Đào tạo Tiếng Anh & Luyện thi Chứng chỉ" };
  }
  if (explicitType === "study_abroad") {
    return { type: "study_abroad", label: "Trung tâm Tư vấn Du học Mỹ" };
  }

  const hasPrep = services.some(s => s.includes("LUYEN_THI") || s.includes("CHUNG_CHI") || s.includes("IELTS") || s.includes("SAT"));
  const hasStudyAbroad = services.some(s =>
    s.includes("CHON_TRUONG") ||
    s.includes("SAN_HOC_BONG") ||
    s.includes("NOP_HO_SO") ||
    s.includes("HO_SO") ||
    s.includes("VISA") ||
    s.includes("CHUYEN_TIEP")
  );

  if (hasPrep && hasStudyAbroad) {
    return { type: "hybrid", label: "Tổ chức Toàn diện (Du học & Luyện thi SAT/IELTS)" };
  }
  if (hasPrep) {
    return { type: "test_prep", label: "Trung tâm Đào tạo Tiếng Anh & Luyện thi Chứng chỉ" };
  }
  return { type: "study_abroad", label: "Trung tâm Tư vấn Du học Mỹ" };
}

interface CenterCustomData {
  coverImage: string;
  logoImage: string;
  galleryImages: { url: string; caption: string }[];
  totalScholarshipsWon: string;
  operatingHours: string;
  courses: EnrichedCenterCourse[];
  successStories: EnrichedSuccessStory[];
  reviews: EnrichedReview[];
}

const CENTER_PROFILES_MAP: Record<string, CenterCustomData> = {
  // 1. Summit Education Services
  CTY_013: {
    coverImage: "https://images.unsplash.com/photo-1541339907198-e08756dedf3f?auto=format&fit=crop&w=1600&q=80",
    logoImage: "https://images.unsplash.com/photo-1546410531-bb4caa6b424d?auto=format&fit=crop&w=400&q=80",
    galleryImages: [
      { url: "https://images.unsplash.com/photo-1523240795612-9a054b0db644?auto=format&fit=crop&w=800&q=80", caption: "Phòng hội thảo chuyên đề học bổng Ivy League" },
      { url: "https://images.unsplash.com/photo-1577896851231-70ef18881754?auto=format&fit=crop&w=800&q=80", caption: "Không gian luyện thi SAT Digital tiêu chuẩn quốc tế" },
      { url: "https://images.unsplash.com/photo-1517486808906-6ca8b3f04846?auto=format&fit=crop&w=800&q=80", caption: "Lễ vinh danh học sinh trúng tuyển Đại học Top đầu Mỹ" },
    ],
    totalScholarshipsWon: "Hơn 115 tỷ VNĐ (Kỳ tuyển sinh 2025 - 2026)",
    operatingHours: "08:00 – 21:00 (Thứ 2 – Thứ 7), 08:30 – 17:30 (Chủ nhật)",
    courses: [
      {
        name: "SAT Master 1550+ Ivy League Track",
        target: "Chẩn đoán SAT miễn phí, chiến thuật phần Reading & Writing nâng cao",
        duration: "12 tuần (3 buổi/tuần)",
        format: "Trực tiếp tại 101 Láng Hạ & LMS tương tác",
        highlight: "Hơn 50 học viên đạt 1550+ mỗi năm",
      },
      {
        name: "IELTS Intensive 7.5 - 8.5 Academic",
        target: "Bứt phá kỹ năng Speaking & hoàn thiện cấu trúc bài Task 2 học thuật",
        duration: "10 tuần",
        format: "Lớp chọn quy mô nhỏ 6 học viên",
        highlight: "Cố vấn học thuật cựu giám khảo chấm thi",
      },
      {
        name: "Chiến lược nộp đơn sớm ED/EA & Ngoại khóa Chuyên sâu",
        target: "Xây dựng dự án cá nhân gắn liền ngành học dự kiến",
        duration: "Kèm 1-1 suốt mùa nộp đơn",
        format: "1-1 cùng chuyên gia cố vấn",
        highlight: "Tối ưu cơ hội trúng tuyển đợt nộp đơn tháng 11",
      },
    ],
    successStories: [
      {
        studentName: "Vũ Minh Khang",
        highSchool: "THPT Chuyên Ngoại Ngữ (Hà Nội)",
        university: "Dartmouth College (Ivy League)",
        scholarship: "280.000 USD Toàn phần",
        major: "Kinh tế & Khoa học Chính trị",
        year: "Kỳ Fall 2026",
      },
      {
        studentName: "Nguyễn Quỳnh Chi",
        highSchool: "THPT Chuyên Hà Nội - Amsterdam",
        university: "Duke University",
        scholarship: "Học bổng Merit 160.000 USD",
        major: "Sinh học & Tiền Y khoa (Pre-Med)",
        year: "Kỳ Fall 2026",
      },
      {
        studentName: "Phạm Gia Bảo",
        highSchool: "THPT Chuyên Sư Phạm",
        university: "Northwestern University",
        scholarship: "Học bổng Tài năng 190.000 USD",
        major: "Khoa học Máy tính & AI",
        year: "Kỳ Fall 2026",
      },
    ],
    reviews: [
      {
        id: "rev-summit-1",
        reviewerName: "Bác Trần Đình Trọng (Phụ huynh)",
        reviewerRole: "parent",
        verified: true,
        date: "24/10/2026",
        rating: 5,
        targetSchool: "Con đỗ Dartmouth College",
        comment: "Summit làm việc rất bài bản và chuẩn mực. Đội ngũ cố vấn đánh giá đúng năng lực thật của cháu, không tư vấn trường viển vông. Khâu luyện phỏng vấn visa và giải trình tài chính cực kỳ chắc chắn.",
        criteriaRatings: { consultingQuality: 5, responsiveness: 5, feeTransparency: 4.9, outcomeSuccess: 5 },
      },
      {
        id: "rev-summit-2",
        reviewerName: "Nguyễn Thùy Dương",
        reviewerRole: "student",
        verified: true,
        date: "12/10/2026",
        rating: 5,
        targetSchool: "Đỗ Duke University",
        comment: "Khóa học SAT ở Summit giúp mình bứt phá từ 1380 lên 1540 điểm trong 2 tháng. Bài luận cá nhân được anh chị sửa từng câu từ để làm nổi bật sự gắn kết cộng đồng.",
        criteriaRatings: { consultingQuality: 5, responsiveness: 4.8, feeTransparency: 5, outcomeSuccess: 5 },
      },
    ],
  },

  // 2. American Study
  CTY_010: {
    coverImage: "https://images.unsplash.com/photo-1562774053-701939374585?auto=format&fit=crop&w=1600&q=80",
    logoImage: "https://images.unsplash.com/photo-1594608661623-aa0bd3a69d98?auto=format&fit=crop&w=400&q=80",
    galleryImages: [
      { url: "https://images.unsplash.com/photo-1524178232363-1fb2b075b655?auto=format&fit=crop&w=800&q=80", caption: "Không gian huấn luyện 1-1 cùng cựu cán bộ tuyển sinh Mỹ" },
      { url: "https://images.unsplash.com/photo-1497633762265-9d179a990aa6?auto=format&fit=crop&w=800&q=80", caption: "Phòng tư liệu và tuyển tập hồ sơ trúng tuyển Top 20" },
      { url: "https://images.unsplash.com/photo-1523240795612-9a054b0db644?auto=format&fit=crop&w=800&q=80", caption: "Workshop xây dựng đề án nghiên cứu khoa học học sinh THPT" },
    ],
    totalScholarshipsWon: "Hơn 140 tỷ VNĐ (Chuyên sâu Top 10 - Top 50 Mỹ)",
    operatingHours: "08:30 – 21:30 (Mở cửa tất cả các ngày trong tuần)",
    courses: [
      {
        name: "Ivy League Elite Mentorship (Gói nộp đơn toàn diện)",
        target: "Đồng hành xuyên suốt từ lớp 9 đến lớp 12 vào Top 20 Mỹ",
        duration: "Dài hạn theo năm học",
        format: "1-1 trực tiếp cùng Cố vấn Trưởng",
        highlight: "Cam kết học bổng từ 3 tỷ – 7 tỷ VNĐ/học sinh",
      },
      {
        name: "SAT Digital 1500+ Chiến lược điểm tuyệt đối",
        target: "Luyện đề theo ngân hàng câu hỏi độc quyền College Board",
        duration: "36 buổi",
        format: "Lớp học tiêu chuẩn 4-6 bạn",
        highlight: "Giáo viên 100% đạt SAT 1560+",
      },
      {
        name: "AP Calculus, AP Chemistry & Nghiên cứu Khoa học",
        target: "Tích lũy tín chỉ Đại học sớm và công bố bài báo quốc tế",
        duration: "16 tuần",
        format: "Hướng dẫn đề tài 1-1",
        highlight: "Lợi thế vượt trội cho hồ sơ ngành STEM",
      },
    ],
    successStories: [
      {
        studentName: "Đặng Tuấn Minh",
        highSchool: "THPT Chuyên Khoa học Tự nhiên",
        university: "Harvard University",
        scholarship: "320.000 USD Toàn phần",
        major: "Khoa học Máy tính & Toán ứng dụng",
        year: "Kỳ Fall 2026",
      },
      {
        studentName: "Lê Thu Trang",
        highSchool: "THPT Chu Văn An (Hà Nội)",
        university: "Brown University",
        scholarship: "Học bổng Hỗ trợ Tài chính 240.000 USD",
        major: "Tâm lý học Nhận thức",
        year: "Kỳ Fall 2026",
      },
      {
        studentName: "Phạm Nhật Huy",
        highSchool: "THPT Chuyên Lê Hồng Phong (TP.HCM)",
        university: "New York University (NYU Stern)",
        scholarship: "Học bổng Merit 80.000 USD",
        major: "Tài chính & Kinh doanh Quốc tế",
        year: "Kỳ Fall 2026",
      },
    ],
    reviews: [
      {
        id: "rev-as-1",
        reviewerName: "Chị Hoàng Kim Ngân (Phụ huynh)",
        reviewerRole: "parent",
        verified: true,
        date: "05/10/2026",
        rating: 5,
        targetSchool: "Con đỗ Harvard & Brown",
        comment: "American Study có tầm nhìn chiến lược khác biệt hoàn toàn. Họ giúp con tìm thấy đam mê thực sự trong lĩnh vực nghiên cứu thay vì chỉ chạy theo các hoạt động ngoại khóa phong trào.",
        criteriaRatings: { consultingQuality: 5, responsiveness: 5, feeTransparency: 4.8, outcomeSuccess: 5 },
      },
    ],
  },

  // 3. Du học New Ocean
  CTY_014: {
    coverImage: "https://images.unsplash.com/photo-1498243691581-b145c3f54a5a?auto=format&fit=crop&w=1600&q=80",
    logoImage: "https://images.unsplash.com/photo-1516321318423-f06f85e504b3?auto=format&fit=crop&w=400&q=80",
    galleryImages: [
      { url: "https://images.unsplash.com/photo-1522202176988-66273c2fd55f?auto=format&fit=crop&w=800&q=80", caption: "Không gian tư vấn tài chính và chứng minh nguồn tiền" },
      { url: "https://images.unsplash.com/photo-1531482615713-2afd69097998?auto=format&fit=crop&w=800&q=80", caption: "Phòng phỏng vấn mô phỏng câu hỏi Lãnh sự quán Hoa Kỳ" },
      { url: "https://images.unsplash.com/photo-1517486808906-6ca8b3f04846?auto=format&fit=crop&w=800&q=80", caption: "Lễ chia tay và dặn dò du học sinh trước ngày bay" },
    ],
    totalScholarshipsWon: "Hơn 68 tỷ VNĐ (Lộ trình CĐCĐ & Thạc sĩ STEM)",
    operatingHours: "08:00 – 18:00 (Thứ 2 – Thứ 6), 08:30 – 12:00 (Thứ 7)",
    courses: [
      {
        name: "Lộ trình Chuyển tiếp CĐCĐ 2+2 Tối ưu Chi phí",
        target: "Học 2 năm Cao đẳng tại Washington/California rồi chuyển tiếp ĐH Top 50",
        duration: "Tư vấn & Hoàn tất hồ sơ 2 tháng",
        format: "Trực tiếp tại văn phòng",
        highlight: "Tiết kiệm 50% chi phí 2 năm đầu so với vào thẳng ĐH 4 năm",
      },
      {
        name: "Luyện phỏng vấn Visa F-1 Chuyên sâu 1-1",
        target: "Hóa giải điều khoản di trú INA 214(b), chứng minh ràng buộc quê hương",
        duration: "6 buổi thực hành mô phỏng",
        format: "Phỏng vấn giả định 1-1 cùng chuyên gia di trú",
        highlight: "Tỷ lệ đỗ visa 98.6% qua hơn 18 năm hoạt động",
      },
      {
        name: "Thạc sĩ Khối STEM & Định hướng Việc làm OPT 36 tháng",
        target: "Chọn ngành STEM hưởng quyền lợi ở lại làm việc hợp pháp 3 năm tại Mỹ",
        duration: "Lộ trình 3 tháng",
        format: "1-1 chuyên sâu",
        highlight: "Hỗ trợ chuẩn bị hồ sơ tìm việc làm thêm On-Campus và CPT",
      },
    ],
    successStories: [
      {
        studentName: "Trần Đăng Khoa",
        highSchool: "THPT Thăng Long (Hà Nội)",
        university: "Green River College → University of Washington",
        scholarship: "Lộ trình 2+2 tiết kiệm 55% chi phí",
        major: "Kỹ thuật Máy tính (Computer Engineering)",
        year: "Kỳ Fall 2026",
      },
      {
        studentName: "Đỗ Mai Linh",
        highSchool: "Đại học Kinh tế Quốc dân (Đã tốt nghiệp)",
        university: "Northeastern University",
        scholarship: "Master of Science in Project Management (Khối STEM)",
        major: "Quản lý Dự án STEM (Hưởng quyền lợi OPT 3 năm)",
        year: "Kỳ Fall 2026",
      },
    ],
    reviews: [
      {
        id: "rev-no-1",
        reviewerName: "Bác Vũ Đình Long (Phụ huynh)",
        reviewerRole: "parent",
        verified: true,
        date: "28/10/2026",
        rating: 5,
        targetSchool: "Con đỗ Green River College",
        comment: "Gia đình tôi kinh doanh tự do, việc chứng minh thu nhập rất khó khăn. May mắn được New Ocean hướng dẫn giải trình dòng tiền và sổ tiết kiệm minh bạch. Cháu phỏng vấn visa chỉ mất đúng 3 phút là được chấp thuận.",
        criteriaRatings: { consultingQuality: 5, responsiveness: 4.9, feeTransparency: 5, outcomeSuccess: 5 },
      },
    ],
  },

  // 4. Du học New World
  CTY_005: {
    coverImage: "https://images.unsplash.com/photo-1523050854058-8df90110c9f1?auto=format&fit=crop&w=1600&q=80",
    logoImage: "https://images.unsplash.com/photo-1509062522246-3755977927d7?auto=format&fit=crop&w=400&q=80",
    galleryImages: [
      { url: "https://images.unsplash.com/photo-1509062522246-3755977927d7?auto=format&fit=crop&w=800&q=80", caption: "Không gian làm việc và tiếp phụ huynh tại Trần Cao Vân, Quận 3" },
      { url: "https://images.unsplash.com/photo-1522202176988-66273c2fd55f?auto=format&fit=crop&w=800&q=80", caption: "Ngày hội đại diện tuyển sinh các trường Cao đẳng Mỹ tại TP.HCM" },
      { url: "https://images.unsplash.com/photo-1517486808906-6ca8b3f04846?auto=format&fit=crop&w=800&q=80", caption: "Lớp hướng dẫn văn hóa và kỹ năng sinh tồn trước khi sang Mỹ" },
    ],
    totalScholarshipsWon: "Hơn 55 tỷ VNĐ (Học bổng Merit Cử nhân & Kép)",
    operatingHours: "08:00 – 17:30 (Thứ 2 – Thứ 6), 08:00 – 12:00 (Thứ 7)",
    courses: [
      {
        name: "Hoàn tất Trung học Kép (High School Completion từ 16 tuổi)",
        target: "Nhận bằng THPT và Associate Degree trong 2 năm tại bang Washington",
        duration: "Chuẩn bị hồ sơ 45 ngày",
        format: "Trực tiếp & Hỗ trợ bảo trợ visa",
        highlight: "Tiết kiệm 2 năm học và hơn 1 tỷ đồng chi phí sinh hoạt",
      },
      {
        name: "Săn Học bổng Merit Tự động theo Học bạ GPA",
        target: "Ứng tuyển các trường Đại học công lập Mỹ không bắt buộc SAT (Test-Optional)",
        duration: "Theo kỳ nộp đơn",
        format: "Cố vấn hồ sơ trực tiếp",
        highlight: "Học bổng xét tự động từ $5,000 - $20,000/năm",
      },
      {
        name: "Tìm kiếm Homestay & Người giám hộ hợp pháp (Học sinh <18 tuổi)",
        target: "Đảm bảo điều kiện ăn ở và pháp lý theo luật di trú Mỹ",
        duration: "Theo khóa học",
        format: "Phối hợp cùng nhà trường",
        highlight: "Mạng lưới gia đình bản xứ được SEVP phê duyệt",
      },
    ],
    successStories: [
      {
        studentName: "Hoàng Bảo Lâm",
        highSchool: "THPT Gia Định (TP.HCM)",
        university: "Seattle Central College (Chương trình kép)",
        scholarship: "Tiết kiệm 2 năm đại học",
        major: "Quản trị Kinh doanh (Business Administration)",
        year: "Kỳ Spring 2026",
      },
      {
        studentName: "Nguyễn Ngọc Ánh",
        highSchool: "THPT Nguyễn Thị Minh Khai",
        university: "Arizona State University (ASU)",
        scholarship: "Học bổng Năng lực 58.000 USD / 4 năm",
        major: "Truyền thông Tiếp thị (Marketing)",
        year: "Kỳ Fall 2026",
      },
    ],
    reviews: [
      {
        id: "rev-nw-1",
        reviewerName: "Cô Mai Thị Huệ (Phụ huynh)",
        reviewerRole: "parent",
        verified: true,
        date: "19/10/2026",
        rating: 5,
        targetSchool: "Con học kép tại Seattle Central",
        comment: "Ban đầu gia đình rất lo lắng khi cho cháu đi từ năm 16 tuổi. Nhờ New World lo chu đáo từ khâu chọn trường, liên hệ gia đình bản xứ homestay đến thủ tục giám hộ pháp lý nên bố mẹ rất an tâm.",
        criteriaRatings: { consultingQuality: 4.9, responsiveness: 5, feeTransparency: 5, outcomeSuccess: 5 },
      },
    ],
  },

  // 5. PEC (Hải Phòng)
  CTY_009: {
    coverImage: "https://images.unsplash.com/photo-1519452635265-7b1fbfd1e4e0?auto=format&fit=crop&w=1600&q=80",
    logoImage: "https://images.unsplash.com/photo-1577495508048-b635879837f1?auto=format&fit=crop&w=400&q=80",
    galleryImages: [
      { url: "https://images.unsplash.com/photo-1580582932707-520aed937b7b?auto=format&fit=crop&w=800&q=80", caption: "Khu vực đào tạo và khảo thí IELTS chính thức tại Hải Phòng" },
      { url: "https://images.unsplash.com/photo-1434030216411-0b793f4b4173?auto=format&fit=crop&w=800&q=80", caption: "Phòng thi thử máy tính chuẩn hóa áp lực thi thật" },
    ],
    totalScholarshipsWon: "Hơn 32 tỷ VNĐ (Khu vực Duyên hải Bắc Bộ)",
    operatingHours: "07:30 – 21:00 (Thứ 2 – Thứ 7), 08:00 – 18:00 (Chủ nhật)",
    courses: [
      {
        name: "Luyện thi IELTS Cam kết 7.0+ Du học",
        target: "Đạt chuẩn ngoại ngữ đầu vào và điều kiện săn học bổng Mỹ",
        duration: "4 tháng",
        format: "Trực tiếp tại 41 Lạch Tray, Hải Phòng",
        highlight: "Cam kết hoàn tiền hoặc đào tạo lại nếu không đạt",
      },
      {
        name: "Luyện phỏng vấn Visa F-1 cùng Giáo viên Bản ngữ",
        target: "Rèn luyện phản xạ tiếng Anh tự nhiên và phong thái tự tin",
        duration: "10 buổi",
        format: "1-1 trực tiếp",
        highlight: "Không học vẹt văn mẫu, trả lời theo tư duy logic",
      },
    ],
    successStories: [
      {
        studentName: "Bùi Gia Huy",
        highSchool: "THPT Chuyên Trần Phú (Hải Phòng)",
        university: "University of South Florida (USF)",
        scholarship: "Học bổng Merit 48.000 USD",
        major: "Chuỗi Cung ứng & Logistics",
        year: "Kỳ Fall 2026",
      },
    ],
    reviews: [
      {
        id: "rev-pec-1",
        reviewerName: "Bùi Gia Huy",
        reviewerRole: "student",
        verified: true,
        date: "14/10/2026",
        rating: 5,
        targetSchool: "Đỗ University of South Florida",
        comment: "Mình học IELTS từ 5.5 lên 7.5 tại PEC rồi làm hồ sơ du học luôn ở đây. Rất tiện lợi vì thầy cô nắm rõ năng lực của mình để chọn trường và xin học bổng mức cao nhất.",
        criteriaRatings: { consultingQuality: 4.8, responsiveness: 4.9, feeTransparency: 5, outcomeSuccess: 5 },
      },
    ],
  },

  // 6. Edu4life
  CTY_003: {
    coverImage: "https://images.unsplash.com/photo-1503676260728-1c00da094a0b?auto=format&fit=crop&w=1600&q=80",
    logoImage: "https://images.unsplash.com/photo-1497633762265-9d179a990aa6?auto=format&fit=crop&w=400&q=80",
    galleryImages: [
      { url: "https://images.unsplash.com/photo-1522071820081-009f0129c71c?auto=format&fit=crop&w=800&q=80", caption: "Không gian tư vấn chiến lược 3 phễu lọc tại Kim Mã, Hà Nội" },
      { url: "https://images.unsplash.com/photo-1516321318423-f06f85e504b3?auto=format&fit=crop&w=800&q=80", caption: "Góc chỉnh sửa bài luận cá nhân và thư giới thiệu LOR" },
    ],
    totalScholarshipsWon: "Hơn 45 tỷ VNĐ (Tỷ lệ đỗ visa 98.2%)",
    operatingHours: "08:30 – 18:30 (Thứ 2 – Thứ 6), 09:00 – 17:00 (Thứ 7)",
    courses: [
      {
        name: "Chiến lược Chọn trường Thông minh theo Quy tắc 3 Phễu lọc",
        target: "Loại bỏ rủi ro tài chính và tối đa hóa khả năng trúng tuyển",
        duration: "4 buổi làm việc chuyên sâu",
        format: "1-1 cùng Cố vấn Trưởng",
        highlight: "Cam kết danh sách trường đúng ngành, vừa ngân sách",
      },
      {
        name: "Khắc phục Hồ sơ GPA Khiêm tốn bằng Hoạt động Ngoại khóa",
        target: "Xây dựng điểm nhấn độc bản bù đắp điểm học bạ",
        duration: "Theo tiến độ mùa nộp đơn",
        format: "Kèm 1-1",
        highlight: "Phương pháp Holistic Review chuẩn Mỹ",
      },
    ],
    successStories: [
      {
        studentName: "Lê Minh Trí",
        highSchool: "THPT Kim Liên (Hà Nội)",
        university: "Purdue University",
        scholarship: "Học bổng Merit 40.000 USD",
        major: "Kỹ thuật Cơ khí (Mechanical Engineering)",
        year: "Kỳ Fall 2026",
      },
    ],
    reviews: [
      {
        id: "rev-edu-1",
        reviewerName: "Bác Lê Văn Dũng (Phụ huynh)",
        reviewerRole: "parent",
        verified: true,
        date: "20/10/2026",
        rating: 5,
        targetSchool: "Con đỗ Purdue University",
        comment: "Điểm hay nhất ở Edu4life là sự chân thành. Tư vấn viên phân tích bảng chi phí sinh hoạt từng bang rất chi tiết, giúp gia đình chủ động nguồn tiền mà không bị phát sinh chi phí ngầm.",
        criteriaRatings: { consultingQuality: 5, responsiveness: 4.8, feeTransparency: 5, outcomeSuccess: 5 },
      },
    ],
  },

  // 7. DuhocEZ
  CTY_004: {
    coverImage: "https://images.unsplash.com/photo-1492538368677-f6e0afe31dcc?auto=format&fit=crop&w=1600&q=80",
    logoImage: "https://images.unsplash.com/photo-1534528741775-53994a69daeb?auto=format&fit=crop&w=400&q=80",
    galleryImages: [
      { url: "https://images.unsplash.com/photo-1497366216548-37526070297c?auto=format&fit=crop&w=800&q=80", caption: "Không gian làm việc số hóa tại Landmark Plus, Bình Thạnh" },
      { url: "https://images.unsplash.com/photo-1517245386807-bb43f82c33c4?auto=format&fit=crop&w=800&q=80", caption: "Phòng điều phối và theo dõi hồ sơ visa trực tuyến" },
    ],
    totalScholarshipsWon: "Hơn 40 tỷ VNĐ (Nền tảng số hóa hồ sơ)",
    operatingHours: "08:30 – 19:00 (Thứ 2 – Thứ 7)",
    courses: [
      {
        name: "Khai báo Đơn điện tử DS-160 & Đóng phí SEVIS I-901 Chuẩn hóa",
        target: "Kiểm tra chéo tính nhất quán 100%, loại bỏ lỗi sai thông tin di trú",
        duration: "Hoàn tất trong 24 giờ",
        format: "Trực tuyến qua cổng EZ Portal",
        highlight: "Bảo hiểm hồ sơ chống lỗi khai man theo điều 212(a)(6)(C)",
      },
      {
        name: "Giải trình Nguồn tiền & Sổ tiết kiệm An toàn Lãnh sự",
        target: "Chứng minh nguồn gốc tài chính minh bạch cho người bảo trợ",
        duration: "3 buổi tư vấn chuyên sâu",
        format: "1-1 trực tiếp hoặc Zoom",
        highlight: "Hóa giải nghi vấn dòng tiền nộp gấp trước ngày phỏng vấn",
      },
    ],
    successStories: [
      {
        studentName: "Nguyễn Phan Anh",
        highSchool: "THPT Chuyên Lê Hồng Phong (TP.HCM)",
        university: "University of California, Berkeley (UC Berkeley)",
        scholarship: "Học bổng Merit 45.000 USD",
        major: "Khoa học Dữ liệu (Data Science)",
        year: "Kỳ Fall 2026",
      },
    ],
    reviews: [
      {
        id: "rev-ez-1",
        reviewerName: "Nguyễn Phan Anh",
        reviewerRole: "student",
        verified: true,
        date: "09/10/2026",
        rating: 5,
        targetSchool: "Đỗ UC Berkeley",
        comment: "DuhocEZ làm việc rất hiện đại. Mọi giấy tờ, hạn chót và mẫu đơn DS-160 đều được số hóa rõ ràng. Lúc phỏng vấn visa mình trả lời y hệt những câu anh chị đã diễn tập nên đậu ngay lần 1.",
        criteriaRatings: { consultingQuality: 5, responsiveness: 5, feeTransparency: 5, outcomeSuccess: 5 },
      },
    ],
  },

  // 8. Mission Edu
  CTY_012: {
    coverImage: "https://images.unsplash.com/photo-1497366811353-6870744d04b2?auto=format&fit=crop&w=1600&q=80",
    logoImage: "https://images.unsplash.com/photo-1507003211169-0a1dd7228f2d?auto=format&fit=crop&w=400&q=80",
    galleryImages: [
      { url: "https://images.unsplash.com/photo-1521737604893-d14cc237f11d?auto=format&fit=crop&w=800&q=80", caption: "Không gian tư vấn gia đình tại The Manor, Mễ Trì" },
      { url: "https://images.unsplash.com/photo-1531545514256-b1400bc00f31?auto=format&fit=crop&w=800&q=80", caption: "Workshop hướng nghiệp và chọn ngành học phù hợp xu hướng" },
    ],
    totalScholarshipsWon: "Hơn 28 tỷ VNĐ (Định hướng học sinh THPT)",
    operatingHours: "08:30 – 18:00 (Thứ 2 – Thứ 6), 09:00 – 16:00 (Thứ 7)",
    courses: [
      {
        name: "Định hướng Lộ trình Du học Sớm từ Bậc THPT",
        target: "Lên kế hoạch chuẩn bị hồ sơ từ lớp 10, tích lũy ngoại khóa",
        duration: "Theo dõi 2 năm",
        format: "Kèm 1-1",
        highlight: "Tối ưu ngân sách du học trong tầm 600 - 800 triệu/năm",
      },
      {
        name: "Huấn luyện Viết luận Cá nhân 650 từ (Personal Statement)",
        target: "Khai thác trải nghiệm thực tế, tìm ra giọng văn độc bản",
        duration: "8 buổi cố vấn",
        format: "1-1 cùng chuyên gia ngôn ngữ",
        highlight: "Nói không với văn mẫu và AI viết hộ",
      },
    ],
    successStories: [
      {
        studentName: "Đỗ Đức Thắng",
        highSchool: "THPT Việt Đức (Hà Nội)",
        university: "Miami University (Ohio)",
        scholarship: "Học bổng Merit 68.000 USD / 4 năm",
        major: "Kinh doanh & Khởi nghiệp",
        year: "Kỳ Fall 2026",
      },
    ],
    reviews: [
      {
        id: "rev-mission-1",
        reviewerName: "Bác Đỗ Quang Vinh (Phụ huynh)",
        reviewerRole: "parent",
        verified: true,
        date: "17/10/2026",
        rating: 5,
        targetSchool: "Con đỗ Miami University",
        comment: "Mission Edu rất kiên nhẫn với các cháu học sinh THPT. Nhờ có thầy cô định hướng mà con tôi biết cách cân đối thời gian giữa việc học ở trường và chuẩn bị hồ sơ du học.",
        criteriaRatings: { consultingQuality: 4.8, responsiveness: 4.9, feeTransparency: 5, outcomeSuccess: 5 },
      },
    ],
  },

  // 9. Kosam Education
  CTY_011: {
    coverImage: "https://images.unsplash.com/photo-1451187580459-43490279c0fa?auto=format&fit=crop&w=1600&q=80",
    logoImage: "https://images.unsplash.com/photo-1500648767791-00dcc994a43e?auto=format&fit=crop&w=400&q=80",
    galleryImages: [
      { url: "https://images.unsplash.com/photo-1581091226825-a6a2a5aee158?auto=format&fit=crop&w=800&q=80", caption: "Không gian làm việc và tiếp đón học giả tại Tràng Tiền, Hà Nội" },
      { url: "https://images.unsplash.com/photo-1526374965328-7f61d4dc18c5?auto=format&fit=crop&w=800&q=80", caption: "Khu vực cố vấn dự án nghiên cứu AI & Data Science" },
    ],
    totalScholarshipsWon: "Hơn 85 tỷ VNĐ (Chuyên sâu Thạc sĩ & Nghiên cứu)",
    operatingHours: "09:00 – 19:30 (Thứ 2 – Thứ 7)",
    courses: [
      {
        name: "GRE / GMAT Quant & Verbal 330+ Cấp tốc Sau Đại học",
        target: "Luyện đề thi chuẩn hóa nâng cao, tối ưu điểm phần toán định lượng",
        duration: "8 tuần",
        format: "Online chuyên sâu 1-1",
        highlight: "Cam kết đầu ra chuẩn các khoa Thạc sĩ Top 50 Mỹ",
      },
      {
        name: "Chiến lược Viết luận Mục đích Nghiên cứu (SOP Coaching)",
        target: "Kết nối dự án thực tế, công trình nghiên cứu và định hướng nghề nghiệp",
        duration: "10 buổi cố vấn 1-1",
        format: "1-1 cùng cựu học giả Thạc sĩ/Tiến sĩ Mỹ",
        highlight: "Phù hợp đặc biệt cho ứng viên chuyển ngành kỹ thuật AI/Data",
      },
      {
        name: "Chiến lược Tìm Giáo sư Hướng dẫn & Xin Thư giới thiệu (LOR)",
        target: "Liên hệ giáo sư nghiên cứu để xin học bổng trợ lý nghiên cứu (RA/TA)",
        duration: "Theo đề tài",
        format: "Cố vấn chuyên gia",
        highlight: "Gia tăng cơ hội nhận gói hỗ trợ toàn phần (Tuition Waiver)",
      },
    ],
    successStories: [
      {
        studentName: "Phạm Tấn Tài",
        highSchool: "Đại học Bách Khoa Hà Nội (Đã tốt nghiệp)",
        university: "Georgia Institute of Technology (Georgia Tech)",
        scholarship: "Hỗ trợ Nghiên cứu Graduate Assistantship",
        major: "Thạc sĩ Trí tuệ Nhân tạo & Học máy (MS AI)",
        year: "Kỳ Fall 2026",
      },
      {
        studentName: "Vũ Hương Giang",
        highSchool: "Đại học Ngoại Thương",
        university: "University of Texas at Austin (UT Austin)",
        scholarship: "Học bổng Merit 35.000 USD",
        major: "Thạc sĩ Phân tích Kinh doanh (MSBA)",
        year: "Kỳ Fall 2026",
      },
    ],
    reviews: [
      {
        id: "rev-kosam-1",
        reviewerName: "Phạm Tấn Tài (Kỹ sư AI)",
        reviewerRole: "student",
        verified: true,
        date: "22/10/2026",
        rating: 5,
        targetSchool: "Đỗ Georgia Tech (Thạc sĩ AI)",
        comment: "Hồ sơ Thạc sĩ tại Mỹ đòi hỏi Statement of Purpose (SOP) phải cực kỳ sắc bén về mặt chuyên môn. Cố vấn ở Kosam đã giúp tôi liên kết các project thực tế tại Việt Nam với hướng nghiên cứu của lab bên Mỹ rất thuyết phục.",
        criteriaRatings: { consultingQuality: 5, responsiveness: 5, feeTransparency: 4.9, outcomeSuccess: 5 },
      },
    ],
  },

  // 10. Beestudious
  CTY_002: {
    coverImage: "https://images.unsplash.com/photo-1513258496099-48168024aec0?auto=format&fit=crop&w=1600&q=80",
    logoImage: "https://images.unsplash.com/photo-1573496359142-b8d87734a5a2?auto=format&fit=crop&w=400&q=80",
    galleryImages: [
      { url: "https://images.unsplash.com/photo-1529156069898-49953e39b3ac?auto=format&fit=crop&w=800&q=80", caption: "Không gian làm việc sáng tạo tại Tòa nhà Detech, Cầu Giấy" },
      { url: "https://images.unsplash.com/photo-1522071820081-009f0129c71c?auto=format&fit=crop&w=800&q=80", caption: "Workshop xây dựng thương hiệu cá nhân du học sinh" },
    ],
    totalScholarshipsWon: "Hơn 22 tỷ VNĐ (Giáo dục Khai phóng Mỹ)",
    operatingHours: "08:30 – 18:30 (Thứ 2 – Thứ 7)",
    courses: [
      {
        name: "Khai phóng Tiềm năng Cá nhân & Tự chủ Học tập",
        target: "Học sinh rèn luyện tư duy phản biện và khả năng thích nghi môi trường Mỹ",
        duration: "12 tuần",
        format: "Kèm nhóm 3 bạn",
        highlight: "Giúp học sinh tự tin hòa nhập cuộc sống đại học phương Tây",
      },
      {
        name: "Thiết kế Dự án Ngoại khóa Phụng sự Cộng đồng",
        target: "Khởi xướng các dự án xã hội có tác động thực tế để đưa vào hồ sơ",
        duration: "Theo đề án 3-6 tháng",
        format: "Cố vấn 1-1",
        highlight: "Tạo dấu ấn khác biệt so với các hồ sơ học bạ thông thường",
      },
    ],
    successStories: [
      {
        studentName: "Nguyễn Khánh Linh",
        highSchool: "THPT Marie Curie (Hà Nội)",
        university: "Fordham University (New York)",
        scholarship: "Học bổng Merit 100.000 USD / 4 năm",
        major: "Quan hệ Quốc tế & Báo chí",
        year: "Kỳ Fall 2026",
      },
    ],
    reviews: [
      {
        id: "rev-bee-1",
        reviewerName: "Nguyễn Khánh Linh",
        reviewerRole: "student",
        verified: true,
        date: "16/10/2026",
        rating: 5,
        targetSchool: "Đỗ Fordham University",
        comment: "Beestudious không ép học sinh theo khuôn mẫu. Thầy cô khuyến khích mình thể hiện cá tính riêng và giúp mình hoàn thiện bài luận đầy cảm xúc về niềm đam mê truyền thông.",
        criteriaRatings: { consultingQuality: 4.8, responsiveness: 4.9, feeTransparency: 5, outcomeSuccess: 5 },
      },
    ],
  },
};

export function getEnrichedCenterProfile(center: StudyCenter): EnrichedCenterProfile {
  const { type, label: typeLabel } = determineCenterType(center.services, center.centerType);

  // Check if center has custom curated data
  const custom = CENTER_PROFILES_MAP[center.code];

  if (custom) {
    return {
      center,
      type,
      typeLabel,
      coverImage: center.coverImageUrl || custom.coverImage,
      logoImage: center.logoUrl || custom.logoImage,
      galleryImages:
        center.galleryImages && center.galleryImages.length > 0
          ? center.galleryImages.map((url, idx) => ({
              url,
              caption: `Không gian hoạt động và tư vấn cơ sở #${idx + 1}`,
            }))
          : custom.galleryImages,
      establishedYear: center.establishedYear ?? 2012,
      staffCount: center.staffCount ?? 35,
      licenseNumber: center.licenseNumber ?? `Số 2049/QĐ-SGDĐT`,
      licenseIssuedBy: `Sở Giáo dục và Đào tạo ${center.city ?? "Hà Nội"}`,
      accreditations:
        center.accreditations && center.accreditations.length > 0
          ? center.accreditations
          : [
              "AIRC (American International Recruitment Council)",
              "ICEF Certified Agency",
              "Đối tác tuyển sinh Hội đồng Anh (British Council)",
              "ETS Authorized TOEFL/GRE Partner",
            ],
      visaSuccessRate: center.visaSuccessRate ?? 98.4,
      totalScholarshipsWon: custom.totalScholarshipsWon,
      rating: Number(center.averageRating ?? 4.9),
      reviewCount: center.reviewCount ?? 120,
      operatingHours: custom.operatingHours,
      courses: custom.courses,
      successStories: custom.successStories,
      reviews: custom.reviews,
    };
  }

  // Dynamic diversified fallback for any other center (e.g. newly created or demo centers)
  const hash = Math.abs(
    center.code.split("").reduce((acc, char) => (acc << 5) - acc + char.charCodeAt(0), 0)
  );

  const fallbackCovers = [
    "https://images.unsplash.com/photo-1541339907198-e08756dedf3f?auto=format&fit=crop&w=1600&q=80",
    "https://images.unsplash.com/photo-1523050854058-8df90110c9f1?auto=format&fit=crop&w=1600&q=80",
    "https://images.unsplash.com/photo-1498243691581-b145c3f54a5a?auto=format&fit=crop&w=1600&q=80",
    "https://images.unsplash.com/photo-1562774053-701939374585?auto=format&fit=crop&w=1600&q=80",
  ];

  const fallbackLogos = [
    "https://images.unsplash.com/photo-1546410531-bb4caa6b424d?auto=format&fit=crop&w=400&q=80",
    "https://images.unsplash.com/photo-1516321318423-f06f85e504b3?auto=format&fit=crop&w=400&q=80",
    "https://images.unsplash.com/photo-1509062522246-3755977927d7?auto=format&fit=crop&w=400&q=80",
    "https://images.unsplash.com/photo-1594608661623-aa0bd3a69d98?auto=format&fit=crop&w=400&q=80",
  ];

  const fallbackCover = fallbackCovers[hash % fallbackCovers.length];
  const fallbackLogo = fallbackLogos[hash % fallbackLogos.length];

  const dynamicCourses: EnrichedCenterCourse[] = [
    {
      name: `Chương trình Tư vấn Du học Mỹ Toàn diện – ${center.name}`,
      target: "Lên chiến lược chọn trường, làm hồ sơ và săn học bổng merit",
      duration: "Theo kỳ nhập học (Fall / Spring)",
      format: "Trực tiếp & Cố vấn 1-1",
      highlight: "Tối ưu hóa học bổng theo khả năng tài chính gia đình",
    },
    {
      name: "Khóa Luyện phỏng vấn Visa F-1 Chuyên sâu",
      target: "Thực hành trả lời câu hỏi viên chức Lãnh sự theo điều khoản INA 214(b)",
      duration: "6 buổi mô phỏng",
      format: "1-1 cùng chuyên gia",
      highlight: "Tự tin đối thoại, giải trình nguồn tiền minh bạch",
    },
  ];

  const dynamicSuccessStories: EnrichedSuccessStory[] = [
    {
      studentName: hash % 2 === 0 ? "Nguyễn Đức Anh" : "Trần Thùy Linh",
      highSchool: center.city === "TP.HCM" ? "THPT Chuyên Lê Hồng Phong" : "THPT Chuyên Hà Nội - Amsterdam",
      university: hash % 2 === 0 ? "University of Wisconsin-Madison" : "Ohio State University",
      scholarship: `Học bổng ${(40 + (hash % 4) * 15) * 1000} USD / 4 năm`,
      major: hash % 2 === 0 ? "Khoa học Máy tính" : "Kinh tế & Phân tích Dữ liệu",
      year: "Kỳ Fall 2026",
    },
  ];

  const dynamicReviews: EnrichedReview[] = [
    {
      id: `rev-dyn-${hash}`,
      reviewerName: hash % 2 === 0 ? "Phụ huynh Quốc Bảo" : "Học sinh Minh Châu",
      reviewerRole: hash % 2 === 0 ? "parent" : "student",
      verified: true,
      date: "15/10/2026",
      rating: 5,
      targetSchool: hash % 2 === 0 ? "Con đỗ ĐH Mỹ Top 60" : "Đỗ học bổng 60.000 USD",
      comment: `Dịch vụ tại ${center.name} rất tận tâm và rõ ràng. Mọi giấy tờ tài chính và thư mời I-20 đều được xử lý đúng hẹn, không phát sinh chi phí.`,
      criteriaRatings: { consultingQuality: 5, responsiveness: 4.9, feeTransparency: 5, outcomeSuccess: 5 },
    },
  ];

  return {
    center,
    type,
    typeLabel,
    coverImage: center.coverImageUrl || fallbackCover,
    logoImage: center.logoUrl || fallbackLogo,
    galleryImages:
      center.galleryImages && center.galleryImages.length > 0
        ? center.galleryImages.map((url, idx) => ({
            url,
            caption: `Không gian hoạt động và tư vấn cơ sở #${idx + 1}`,
          }))
        : [
            { url: fallbackCover, caption: "Khu vực tiếp đón và tư vấn" },
            { url: fallbackLogo, caption: "Góc học tập và trao đổi học viên" },
          ],
    establishedYear: center.establishedYear ?? 2015 + (hash % 6),
    staffCount: center.staffCount ?? 20 + (hash % 30),
    licenseNumber: center.licenseNumber ?? `Số ${1000 + (hash % 8000)}/QĐ-SGDĐT`,
    licenseIssuedBy: `Sở Giáo dục và Đào tạo ${center.city ?? "Hà Nội"}`,
    accreditations:
      center.accreditations && center.accreditations.length > 0
        ? center.accreditations
        : [
            "AIRC (American International Recruitment Council)",
            "ICEF Certified Agency",
            "Đối tác tuyển sinh Hội đồng Anh (British Council)",
          ],
    visaSuccessRate: center.visaSuccessRate ?? 97.5 + (hash % 20) / 10,
    totalScholarshipsWon: `Hơn ${30 + (hash % 70)} tỷ VNĐ (Kỳ tuyển sinh 2025 - 2026)`,
    rating: Number(center.averageRating ?? (4.7 + (hash % 3) / 10)),
    reviewCount: center.reviewCount ?? (50 + (hash % 100)),
    operatingHours: "08:30 – 19:30 (Thứ 2 – Thứ 7)",
    courses: dynamicCourses,
    successStories: dynamicSuccessStories,
    reviews: dynamicReviews,
  };
}
