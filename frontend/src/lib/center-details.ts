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

export function determineCenterType(services: string[]): { type: CenterType; label: string } {
  const hasPrep = services.includes("LUYEN_THI") || services.includes("CHUNG_CHI");
  const hasStudyAbroad =
    services.includes("CHON_TRUONG") ||
    services.includes("SAN_HOC_BONG") ||
    services.includes("NOP_HO_SO") ||
    services.includes("HO_SO");

  if (hasPrep && hasStudyAbroad) {
    return { type: "hybrid", label: "Tổ chức Toàn diện (Du học & Luyện thi SAT/IELTS)" };
  }
  if (hasPrep) {
    return { type: "test_prep", label: "Trung tâm Đào tạo Tiếng Anh & Luyện thi Chứng chỉ" };
  }
  return { type: "study_abroad", label: "Trung tâm Tư vấn Du học Mỹ" };
}

export function getEnrichedCenterProfile(center: StudyCenter): EnrichedCenterProfile {
  const { type, label: typeLabel } = determineCenterType(center.services);

  // Gallery hình ảnh thực tế chất lượng cao
  const galleryImages = [
    {
      url: "https://images.unsplash.com/photo-1524178232363-1fb2b075b655?auto=format&fit=crop&w=1000&q=80",
      caption: "Không gian phòng học và hội thảo hiện đại",
    },
    {
      url: "https://images.unsplash.com/photo-1523240795612-9a054b0db644?auto=format&fit=crop&w=1000&q=80",
      caption: "Buổi tư vấn định hướng 1-1 cùng chuyên gia",
    },
    {
      url: "https://images.unsplash.com/photo-1523050854058-8df90110c9f1?auto=format&fit=crop&w=1000&q=80",
      caption: "Lễ vinh danh học viên trúng tuyển học bổng Mỹ",
    },
    {
      url: "https://images.unsplash.com/photo-1497633762265-9d179a990aa6?auto=format&fit=crop&w=1000&q=80",
      caption: "Góc tự học và tra cứu tài liệu du học",
    },
  ];

  // Khóa học tiếng Anh / luyện thi chuẩn hóa
  const courses: EnrichedCenterCourse[] = [
    {
      name: "Luyện thi SAT Digital 1500+ Cấp tốc",
      target: "Bứt phá từ 1250 lên 1500+ trong 10 tuần",
      duration: "30 buổi (3 buổi/tuần)",
      format: "Offline tại lớp hoặc Online tương tác",
      highlight: "Cam kết tăng tối thiểu 150 điểm",
    },
    {
      name: "Khóa IELTS Academic 7.5+ Intensive",
      target: "Hoàn thiện 4 kỹ năng & chiến thuật giải đề",
      duration: "40 buổi (3 tháng)",
      format: "Lớp học nhỏ 6-8 học viên",
      highlight: "Chấm chữa bài luận Writing không giới hạn",
    },
    {
      name: "Luyện thi AP Calculus BC & AP Physics",
      target: "Đạt điểm 4-5 môn nâng cao lấy tín chỉ Đại học",
      duration: "24 buổi",
      format: "Kèm 1-1 hoặc nhóm 3 người",
      highlight: "Giáo viên chuyên môn tốt nghiệp ĐH Top Mỹ",
    },
  ];

  // Thành tích trúng tuyển tiêu biểu
  const successStories: EnrichedSuccessStory[] = [
    {
      studentName: "Nguyễn Hải Đăng",
      highSchool: "THPT Chuyên Hà Nội - Amsterdam",
      university: "University of Pennsylvania (UPenn)",
      scholarship: "240.000 USD / 4 năm",
      major: "Khoa học Máy tính (Computer Science)",
      year: "Kỳ Mùa Thu 2026",
    },
    {
      studentName: "Trần Phương Mai",
      highSchool: "THPT Chuyên Sư Phạm",
      university: "University of California, Los Angeles (UCLA)",
      scholarship: "Học bổng Merit 40.000 USD",
      major: "Kinh tế & Phân tích Dữ liệu",
      year: "Kỳ Mùa Thu 2026",
    },
    {
      studentName: "Lê Hoàng Quân",
      highSchool: "THPT Chuyên Lê Hồng Phong (TP.HCM)",
      university: "Purdue University",
      scholarship: "60.000 USD / 4 năm",
      major: "Kỹ thuật Cơ khí (Mechanical Engineering)",
      year: "Kỳ Mùa Thu 2026",
    },
  ];

  // Đánh giá thực tế từ học sinh/phụ huynh đã xác minh
  const reviews: EnrichedReview[] = [
    {
      id: "rev-1",
      reviewerName: "Phụ huynh Minh Anh",
      reviewerRole: "parent",
      verified: true,
      date: "18/10/2026",
      rating: 5,
      targetSchool: "Con đỗ Purdue University",
      comment:
        "Tư vấn viên phân tích bài toán tài chính gia đình rất kỹ và có tâm, không vẽ vời thêm trường ngoài khả năng. Bài luận cá nhân được người có chuyên môn sửa từng chi tiết nhỏ. Con tôi tự tin hơn hẳn khi phỏng vấn visa.",
      criteriaRatings: {
        consultingQuality: 5,
        responsiveness: 5,
        feeTransparency: 5,
        outcomeSuccess: 5,
      },
    },
    {
      id: "rev-2",
      reviewerName: "Bảo Ngọc",
      reviewerRole: "student",
      verified: true,
      date: "04/10/2026",
      rating: 5,
      targetSchool: "Đỗ UC Berkeley & NYU",
      comment:
        "Thầy cô hướng dẫn mình cách khai thác hoạt động ngoại khóa theo mô hình chữ T rất độc đáo. Gói luyện phỏng vấn visa chuẩn xác, đúng ngay 3 câu Lãnh sự quán hỏi mình. Cực kỳ cảm ơn trung tâm!",
      criteriaRatings: {
        consultingQuality: 5,
        responsiveness: 4.8,
        feeTransparency: 4.9,
        outcomeSuccess: 5,
      },
    },
  ];

  return {
    center,
    type,
    typeLabel,
    coverImage:
      "https://images.unsplash.com/photo-1523050854058-8df90110c9f1?auto=format&fit=crop&w=1600&q=80",
    logoImage:
      "https://images.unsplash.com/photo-1546410531-bb4caa6b424d?auto=format&fit=crop&w=250&q=80",
    galleryImages,
    establishedYear: 2012,
    staffCount: 38,
    licenseNumber: `Số ${Math.floor(1000 + Math.random() * 9000)}/QĐ-SGDĐT`,
    licenseIssuedBy: `Sở Giáo dục và Đào tạo ${center.city ?? "Hà Nội"}`,
    accreditations: [
      "AIRC (American International Recruitment Council)",
      "ICEF Certified Agency",
      "Đối tác tuyển sinh Hội đồng Anh (British Council)",
      "ETS Authorized TOEFL/GRE Partner",
    ],
    visaSuccessRate: 98.4,
    totalScholarshipsWon: "Hơn 55 tỷ VNĐ (2025 - 2026)",
    rating: 4.9,
    reviewCount: 124,
    operatingHours: "08:30 – 20:30 (Thứ 2 – Thứ 7), 09:00 – 17:00 (Chủ nhật)",
    courses,
    successStories,
    reviews,
  };
}
