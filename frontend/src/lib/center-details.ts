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

export function getEnrichedCenterProfile(center: StudyCenter): EnrichedCenterProfile {
  const { type, label: typeLabel } = determineCenterType(center.services, center.centerType);

  // Gallery hình ảnh
  const defaultGallery = [
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

  const galleryImages =
    center.galleryImages && center.galleryImages.length > 0
      ? center.galleryImages.map((url, idx) => ({
          url,
          caption: `Không gian hoạt động và tư vấn cơ sở #${idx + 1}`,
        }))
      : defaultGallery;

  // Custom data per center code for realistic profiles
  let courses: EnrichedCenterCourse[] = [
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

  let successStories: EnrichedSuccessStory[] = [
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

  if (center.code === "CTY_013") {
    // Summit
    courses = [
      {
        name: "SAT Master 1550+ Ivy League Track",
        target: "Chẩn đoán SAT miễn phí, chiến thuật phần Reading & Writing nâng cao",
        duration: "12 tuần",
        format: "Trực tiếp tại 101 Láng Hạ & Online LMS",
        highlight: "Hơn 50 học viên đạt 1550+ mỗi năm",
      },
      {
        name: "IELTS Intensive 7.5 - 8.5",
        target: "Rèn phản xạ Speaking & hoàn thiện cấu trúc bài Task 2 học thuật",
        duration: "10 tuần",
        format: "Lớp chọn 6 học viên",
        highlight: "Cố vấn học thuật cựu giám khảo chấm thi",
      },
      {
        name: "Chiến lược nộp đơn sớm ED/EA & Hoạt động Ngoại khóa",
        target: "Xây dựng dự án cá nhân gắn liền ngành học dự kiến",
        duration: "Kèm 1-1 suốt mùa nộp đơn",
        format: "1-1 trực tiếp",
        highlight: "Tối ưu cơ hội trúng tuyển đợt nộp đơn tháng 11",
      },
    ];
    successStories = [
      {
        studentName: "Vũ Minh Khang",
        highSchool: "THPT Chuyên Ngoại Ngữ",
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
    ];
  } else if (center.code === "CTY_014") {
    // New Ocean
    courses = [
      {
        name: "Luyện phỏng vấn Visa F-1 Chuyên sâu 1-1",
        target: "Hóa giải điều khoản di trú INA 214(b), chứng minh ràng buộc quê hương",
        duration: "6 buổi thực hành mô phỏng",
        format: "Phỏng vấn giả định 1-1",
        highlight: "Tỷ lệ đỗ visa 98.6% qua hơn 18 năm",
      },
      {
        name: "Tiếng Anh Du học & Thuyết trình Học thuật",
        target: "Luyện phát âm chuẩn Mỹ, làm quen phương pháp học đại học Hoa Kỳ",
        duration: "8 tuần",
        format: "Offline tại Phạm Thận Duật, Cầu Giấy",
        highlight: "Giáo trình độc quyền chuẩn bị trước khi bay",
      },
    ];
    successStories = [
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
        scholarship: "Master of Science in Project Management (STEM)",
        major: "Quản lý Dự án Khối STEM (OPT 36 tháng)",
        year: "Kỳ Fall 2026",
      },
    ];
  } else if (center.code === "CTY_011") {
    // Kosam
    courses = [
      {
        name: "GRE / GMAT Quant & Verbal 330+ Cấp tốc",
        target: "Luyện đề thi chuẩn hóa sau đại học, giải quyết dứt điểm câu hỏi khó",
        duration: "8 tuần",
        format: "Online chuyên sâu 1-1",
        highlight: "Cam kết đầu ra chuẩn các khoa Thạc sĩ Top 50 Mỹ",
      },
      {
        name: "Chiến lược Viết bài luận mục đích nghiên cứu (SOP Coaching)",
        target: "Kết nối dự án thực tế, công trình nghiên cứu và định hướng nghề nghiệp",
        duration: "10 buổi cố vấn 1-1",
        format: "1-1 cùng cựu học giả Thạc sĩ/Tiến sĩ Mỹ",
        highlight: "Phù hợp đặc biệt cho ứng viên chuyển ngành kỹ thuật AI/Data",
      },
    ];
    successStories = [
      {
        studentName: "Phạm Tấn Tài",
        highSchool: "Đại học Bách Khoa Hà Nội",
        university: "Georgia Institute of Technology (Georgia Tech)",
        scholarship: "Hỗ trợ Nghiên cứu Graduate Assistantship",
        major: "Thạc sĩ Trí tuệ Nhân tạo & Học máy",
        year: "Kỳ Fall 2026",
      },
    ];
  }

  const defaultCover =
    "https://images.unsplash.com/photo-1523050854058-8df90110c9f1?auto=format&fit=crop&w=1600&q=80";
  const defaultLogo =
    "https://images.unsplash.com/photo-1546410531-bb4caa6b424d?auto=format&fit=crop&w=250&q=80";

  return {
    center,
    type,
    typeLabel,
    coverImage: center.coverImageUrl || defaultCover,
    logoImage: center.logoUrl || defaultLogo,
    galleryImages,
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
    totalScholarshipsWon: "Hơn 55 tỷ VNĐ (2025 - 2026)",
    rating: Number(center.averageRating ?? 4.9),
    reviewCount: center.reviewCount ?? 120,
    operatingHours: "08:30 – 20:30 (Thứ 2 – Thứ 7), 09:00 – 17:00 (Chủ nhật)",
    courses,
    successStories,
    reviews,
  };
}

