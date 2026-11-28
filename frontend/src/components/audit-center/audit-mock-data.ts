// ponytail: temporary mock fixtures & fetch interceptor for /audit/center UI redesign preview
import type {
  CenterDeletionRequestDto,
  CenterGuidanceCardsDto,
  CenterLeadsSummaryDto,
  CenterReceivedLeadDto,
  CenterReviewDto,
  CenterReviewsDto,
  CenterVerificationStatusDto,
  LeadMessageDto,
  StudyCenter,
  UserDto,
  UserRole,
} from "@/types/api";
import type { PublicPolicy } from "@/types/policy";

export type AuditPageGroup = "public" | "portal" | "register";

export type AuditPageId =
  | "public_list"
  | "public_detail"
  | "public_compare"
  | "portal_overview"
  | "portal_leads"
  | "portal_reviews"
  | "portal_verification"
  | "portal_profile"
  | "register_form"
  | "register_verify";

export interface AuditCaseDef {
  id: string;
  label: string;
  description: string;
  defaultRole?: UserRole | "guest";
}

export interface AuditPageDef {
  id: AuditPageId;
  group: AuditPageGroup;
  route: string;
  label: string;
  shortLabel: string;
  defaultRole: UserRole | "guest";
  cases: AuditCaseDef[];
}

export const AUDIT_PAGES: AuditPageDef[] = [
  {
    id: "public_list",
    group: "public",
    route: "/centers",
    label: "Danh bạ trung tâm",
    shortLabel: "1. /centers",
    defaultRole: "student",
    cases: [
      { id: "populated", label: "Có dữ liệu (6 trung tâm)", description: "Đủ loại hình, trạng thái xác minh, đánh giá & bộ lọc" },
      { id: "empty", label: "Danh sách trống", description: "Chưa có trung tâm nào phù hợp" },
    ],
  },
  {
    id: "public_detail",
    group: "public",
    route: "/centers/[id]",
    label: "Chi tiết trung tâm",
    shortLabel: "2. /centers/[id]",
    defaultRole: "student",
    cases: [
      { id: "verified_full", label: "Đã xác minh (đủ dữ liệu)", description: "Có ảnh bìa, gallery, đánh giá, thẻ hướng dẫn, học sinh gửi được review" },
      { id: "unverified_minimal", label: "Chờ xác minh (thiếu thông tin)", description: "Chưa ảnh, thiếu nhiều trường, 0 đánh giá, mẫu phản hồi < 5" },
      { id: "student_already_reviewed", label: "Học sinh đã đánh giá", description: "Hiện thông báo đã đánh giá trung tâm này" },
      { id: "student_ineligible", label: "Học sinh chưa đủ điều kiện", description: "Chưa gửi lead nên chưa được viết đánh giá" },
      { id: "reviews_error", label: "Lỗi tải đánh giá", description: "API đánh giá trả lỗi 500" },
    ],
  },
  {
    id: "public_compare",
    group: "public",
    route: "/centers/compare",
    label: "So sánh trung tâm",
    shortLabel: "3. /centers/compare",
    defaultRole: "student",
    cases: [
      { id: "compare_3", label: "So sánh 3 trung tâm", description: "Đủ 3 trung tâm khác loại hình & trạng thái" },
      { id: "compare_2", label: "So sánh 2 trung tâm", description: "1 trung tâm đầy đủ vs 1 trung tâm thiếu dữ liệu" },
      { id: "compare_empty", label: "Chưa đủ 2 trung tâm (0/1)", description: "Trạng thái trống / gợi ý chọn thêm trung tâm" },
    ],
  },
  {
    id: "portal_overview",
    group: "portal",
    route: "/center",
    label: "Portal: Tổng quan",
    shortLabel: "4. /center",
    defaultRole: "center",
    cases: [
      { id: "unverified", label: "Chưa xác minh", description: "Email đã xác thực, chưa nộp hồ sơ ĐKKD" },
      { id: "pending_review", label: "Đang chờ duyệt", description: "Đã nộp hồ sơ, chờ Moderator đối chiếu" },
      { id: "changes_requested", label: "Yêu cầu bổ sung", description: "Moderator yêu cầu bổ sung giấy tờ / thông tin" },
      { id: "rejected", label: "Bị từ chối", description: "Hồ sơ xác minh không đạt kèm lý do" },
      { id: "verified", label: "Đã xác minh", description: "4/4 điều kiện hoàn tất, hiển thị công khai" },
      { id: "suspended", label: "Bị tạm ngừng", description: "Tài khoản trung tâm đang tạm khóa" },
    ],
  },
  {
    id: "portal_leads",
    group: "portal",
    route: "/center/leads",
    label: "Portal: Yêu cầu liên hệ",
    shortLabel: "5. /center/leads",
    defaultRole: "center",
    cases: [
      { id: "mixed", label: "Đủ mọi trạng thái lead", description: "Mới nhận (còn hạn), Đã chấp nhận (chat), Từ chối, Đã đóng, Rút hồ sơ / Rút đồng ý" },
      { id: "empty", label: "Chưa có yêu cầu nào", description: "Hộp thư lead trống" },
    ],
  },
  {
    id: "portal_reviews",
    group: "portal",
    route: "/center/reviews",
    label: "Portal: Đánh giá",
    shortLabel: "6. /center/reviews",
    defaultRole: "center",
    cases: [
      { id: "eligible_populated", label: "Đã xác minh (có đánh giá)", description: "Chưa trả lời, đã trả lời <24h (sửa được), >24h (khóa), người dùng đã xóa" },
      { id: "ineligible_unverified", label: "Chưa xác minh (khóa trả lời)", description: "Hiện cảnh báo chưa thể phản hồi đánh giá" },
      { id: "empty", label: "Chưa có đánh giá", description: "Danh sách đánh giá trống" },
      { id: "error", label: "Lỗi tải danh sách", description: "Lỗi kết nối khi tải đánh giá" },
    ],
  },
  {
    id: "portal_verification",
    group: "portal",
    route: "/center/verification",
    label: "Portal: Xác minh pháp lý",
    shortLabel: "7. /center/verification",
    defaultRole: "center",
    cases: [
      { id: "unverified_same_person", label: "Chưa nộp (đại diện PL)", description: "Người đăng ký trùng đại diện pháp luật" },
      { id: "unverified_need_poa", label: "Chưa nộp (cần giấy ủy quyền)", description: "Người đăng ký khác đại diện PL → bắt buộc Giấy ủy quyền" },
      { id: "pending_review", label: "Đang chờ duyệt", description: "Khóa chỉnh sửa, hiện danh sách tài liệu đã nộp" },
      { id: "changes_requested", label: "Cần bổ sung", description: "Banner yêu cầu bổ sung + cho phép sửa & nộp lại" },
      { id: "rejected", label: "Bị từ chối", description: "Banner từ chối kèm lý do + form nộp lại" },
      { id: "verified", label: "Đã xác minh", description: "Banner xanh hoàn tất xác minh" },
    ],
  },
  {
    id: "portal_profile",
    group: "portal",
    route: "/center/profile",
    label: "Portal: Hồ sơ công bố",
    shortLabel: "8. /center/profile",
    defaultRole: "center",
    cases: [
      { id: "unverified_editable", label: "Chưa xác minh (sửa được pháp lý)", description: "Mở khóa MST, người đại diện, giấy phép" },
      { id: "verified_locked", label: "Đã xác minh (khóa pháp lý)", description: "Khóa nhóm thông tin pháp lý + liên hệ + yêu cầu xóa" },
      { id: "deletion_pending", label: "Đang chờ xóa tài khoản", description: "Có yêu cầu xóa tài khoản trạng thái pending" },
      { id: "deletion_approved", label: "Đã duyệt xóa tài khoản", description: "Yêu cầu xóa trạng thái approved (đang chờ hết hạn)" },
    ],
  },
  {
    id: "register_form",
    group: "register",
    route: "/register/center",
    label: "Đăng ký trung tâm",
    shortLabel: "9. /register/center",
    defaultRole: "guest",
    cases: [
      { id: "ready", label: "Sẵn sàng (đã tải điều khoản)", description: "Form đăng ký chuẩn với Điều khoản & Cam kết" },
      { id: "policies_error", label: "Lỗi tải điều khoản", description: "Không tải được phiên bản chính sách → khóa nút đăng ký" },
    ],
  },
  {
    id: "register_verify",
    group: "register",
    route: "/register/center/verify-email",
    label: "Xác thực Email OTP",
    shortLabel: "10. /register/center/verify-email",
    defaultRole: "guest",
    cases: [
      { id: "prefilled", label: "Có sẵn email", description: "Điền sẵn email trung tâm vừa đăng ký + bộ đếm OTP" },
      { id: "empty_email", label: "Chưa có email", description: "Mở trực tiếp không có query ?email=" },
    ],
  },
];

const nowIso = (offsetMinutes = 0) => new Date(Date.now() + offsetMinutes * 60_000).toISOString();
const daysAgoIso = (days: number) => new Date(Date.now() - days * 86_400_000).toISOString();
const daysAheadIso = (days: number) => new Date(Date.now() + days * 86_400_000).toISOString();

const COVER_SVG_1 =
  "data:image/svg+xml;utf8," +
  encodeURIComponent(
    `<svg xmlns="http://www.w3.org/2000/svg" width="1200" height="420" viewBox="0 0 1200 420"><defs><linearGradient id="g" x1="0" y1="0" x2="1" y2="1"><stop offset="0%" stop-color="#0f2942"/><stop offset="60%" stop-color="#1d4d7a"/><stop offset="100%" stop-color="#2c6aa0"/></linearGradient></defs><rect width="1200" height="420" fill="url(#g)"/><circle cx="980" cy="90" r="140" fill="#ffffff" fill-opacity="0.06"/><circle cx="180" cy="340" r="180" fill="#ffffff" fill-opacity="0.05"/></svg>`,
  );

const GALLERY_SVG_2 =
  "data:image/svg+xml;utf8," +
  encodeURIComponent(
    `<svg xmlns="http://www.w3.org/2000/svg" width="1200" height="420" viewBox="0 0 1200 420"><defs><linearGradient id="g2" x1="0" y1="0" x2="1" y2="1"><stop offset="0%" stop-color="#134e4a"/><stop offset="60%" stop-color="#0f766e"/><stop offset="100%" stop-color="#14b8a6"/></linearGradient></defs><rect width="1200" height="420" fill="url(#g2)"/><circle cx="260" cy="110" r="120" fill="#ffffff" fill-opacity="0.08"/><circle cx="960" cy="320" r="160" fill="#ffffff" fill-opacity="0.06"/></svg>`,
  );

const LOGO_SVG = (text: string, bg: string) =>
  "data:image/svg+xml;utf8," +
  encodeURIComponent(
    `<svg xmlns="http://www.w3.org/2000/svg" width="128" height="128" viewBox="0 0 128 128"><rect width="128" height="128" rx="24" fill="${bg}"/><text x="64" y="76" font-family="system-ui,sans-serif" font-size="42" font-weight="700" fill="#fff" text-anchor="middle">${text}</text></svg>`,
  );

export const MOCK_CENTERS: StudyCenter[] = [
  {
    id: "ctr-1",
    code: "CTR_VUS01",
    name: "Tư vấn Du học Minh Khang Global",
    website: "https://minhkhang.edu.vn",
    fanpage: "https://facebook.com/minhkhangglobal",
    address: "Tầng 8, Tòa nhà Lotte Center, 54 Liễu Giai",
    district: "Ba Đình",
    city: "Hà Nội",
    studyLevels: ["DaiHoc", "ThacSi", "PhoThong"],
    services: ["CHON_TRUONG", "SAN_HOC_BONG", "SUA_BAI_LUAN", "LUYEN_PHONG_VAN", "LUYEN_THI"],
    surveyedAt: daysAgoIso(12),
    phone: "0912345678",
    email: "tuvan@minhkhang.edu.vn",
    emailVerifiedAt: daysAgoIso(90),
    description:
      "Đơn vị chuyên tư vấn lộ trình vào các trường Đại học Top 100 Hoa Kỳ, hỗ trợ tối ưu hồ sơ học bổng và luyện phỏng vấn Visa F-1 theo quy trình minh bạch.",
    verificationStatus: "verified",
    isActive: true,
    centerType: "hybrid",
    logoUrl: LOGO_SVG("MK", "#1d4d7a"),
    coverImageUrl: COVER_SVG_1,
    galleryImages: [COVER_SVG_1, GALLERY_SVG_2],
    staffCount: 28,
    establishedYear: 2016,
    licenseNumber: "1428/GCN-SGDĐT",
    licenseIssuedBy: "Sở GD&ĐT TP. Hà Nội",
    licenseIssuedDate: "2022-05-18",
    legalComplianceMode: "licensed",
    trainingLicenseNumber: "910/GP-SGDĐT",
    certifiedCounselorCount: 12,
    taxCode: "0107654321",
    legalRepresentative: "Nguyễn Minh Khang",
    registrantName: "Nguyễn Minh Khang",
    registrantPosition: "Giám đốc điều hành",
    accreditations: ["AIRC Certified", "ICEF Agency", "US News Global Partner"],
    averageRating: 4.8,
    reviewCount: 34,
    verifiedAt: daysAgoIso(60),
    feeMin: 15000000,
    feeMax: 45000000,
    feeDescription: "Tư vấn trọn gói hồ sơ Đại học Mỹ kèm 1:1 mentor bài luận",
    serviceAreas: ["Hà Nội", "Hải Phòng", "Quảng Ninh", "Tư vấn trực tuyến toàn quốc"],
    branches: ["54 Liễu Giai, Ba Đình, Hà Nội", "128 Nguyễn Huệ, Quận 1, TP.HCM"],
    medianResponseHours: 2.4,
    responseRatePercent: 96,
    responseSampleCount: 24,
  },
  {
    id: "ctr-2",
    code: "CTR_SGN02",
    name: "Học viện Du học Ánh Dương",
    website: "https://anhduong.edu.vn",
    fanpage: "https://facebook.com/anhduongstudy",
    address: "215 Nam Kỳ Khởi Nghĩa, Phường Võ Thị Sáu",
    district: "Quận 3",
    city: "TP.HCM",
    studyLevels: ["DaiHoc", "CaoDangCongDong"],
    services: ["CHON_TRUONG", "SAN_HOC_BONG", "LUYEN_PHONG_VAN"],
    surveyedAt: daysAgoIso(20),
    phone: "0988776655",
    email: "contact@anhduong.edu.vn",
    emailVerifiedAt: daysAgoIso(45),
    description: "Đồng hành cùng học sinh Việt Nam chuyển tiếp Cao đẳng cộng đồng và Đại học công lập bang California, Washington.",
    verificationStatus: "verified",
    isActive: true,
    centerType: "study_abroad",
    logoUrl: LOGO_SVG("AD", "#0f766e"),
    coverImageUrl: null,
    galleryImages: [],
    staffCount: 15,
    establishedYear: 2019,
    licenseNumber: "882/GCN-SGDĐT",
    licenseIssuedBy: "Sở GD&ĐT TP.HCM",
    licenseIssuedDate: "2023-01-10",
    legalComplianceMode: "licensed",
    certifiedCounselorCount: 7,
    taxCode: "0315998877",
    legalRepresentative: "Trần Thị Lan Anh",
    accreditations: ["NAFSA Member"],
    averageRating: 4.5,
    reviewCount: 19,
    verifiedAt: daysAgoIso(40),
    feeMin: 10000000,
    feeMax: 28000000,
    serviceAreas: ["TP.HCM", "Bình Dương", "Đồng Nai"],
    branches: ["215 Nam Kỳ Khởi Nghĩa, Quận 3, TP.HCM"],
    medianResponseHours: 6.2,
    responseRatePercent: 89,
    responseSampleCount: 12,
  },
  {
    id: "ctr-3",
    code: "CTR_DNG03",
    name: "Trung tâm Luyện thi & Hồ sơ Horizon",
    website: "https://horizonprep.vn",
    address: "74 Bạch Đằng",
    district: "Hải Châu",
    city: "Đà Nẵng",
    studyLevels: ["PhoThong", "DaiHoc"],
    services: ["LUYEN_THI", "SUA_BAI_LUAN"],
    surveyedAt: null,
    phone: "0905112233",
    email: "hello@horizonprep.vn",
    emailVerifiedAt: daysAgoIso(10),
    description: "Luyện thi SAT/ACT/IELTS tăng tốc kết hợp cố vấn nộp hồ sơ du học Mỹ khu vực miền Trung.",
    verificationStatus: "unverified",
    isActive: true,
    centerType: "test_prep",
    logoUrl: LOGO_SVG("HZ", "#7c3aed"),
    staffCount: 9,
    establishedYear: 2023,
    legalComplianceMode: "notified",
    trainingLicenseNumber: "412/GP-SGDĐT",
    certifiedCounselorCount: 3,
    taxCode: "0402123456",
    legalRepresentative: "Lê Quốc Bảo",
    accreditations: [],
    averageRating: 4.2,
    reviewCount: 5,
    medianResponseHours: null,
    responseRatePercent: null,
    responseSampleCount: 2,
  },
  {
    id: "ctr-4",
    code: "CTR_HPG04",
    name: "Tư vấn Du học Cảng Biển Star",
    website: null,
    address: "19 Trần Phú",
    district: "Ngô Quyền",
    city: "Hải Phòng",
    studyLevels: ["DaiHoc", "ThacSi"],
    services: ["CHON_TRUONG", "LUYEN_PHONG_VAN"],
    surveyedAt: null,
    phone: "0934556677",
    email: "info@cangbienstar.vn",
    description: null,
    verificationStatus: "unverified",
    isActive: true,
    centerType: "study_abroad",
    logoUrl: null,
    staffCount: null,
    establishedYear: null,
    legalComplianceMode: "notified",
    accreditations: [],
    averageRating: 0,
    reviewCount: 0,
    medianResponseHours: null,
    responseRatePercent: null,
    responseSampleCount: 0,
  },
  {
    id: "ctr-5",
    code: "CTR_HNO05",
    name: "Tổ chức Giáo dục IvyPath Việt Nam",
    website: "https://ivypath.vn",
    address: "101 Láng Hạ",
    district: "Đống Đa",
    city: "Hà Nội",
    studyLevels: ["DaiHoc", "ThacSi", "TienSi"],
    services: ["CHON_TRUONG", "SAN_HOC_BONG", "SUA_BAI_LUAN", "LUYEN_PHONG_VAN"],
    surveyedAt: daysAgoIso(30),
    phone: "0977334455",
    email: "admissions@ivypath.vn",
    description: "Chuyên sâu học bổng nghiên cứu Thạc sĩ/Tiến sĩ và hồ sơ Liberal Arts Colleges.",
    verificationStatus: "verified",
    isActive: true,
    centerType: "study_abroad",
    logoUrl: LOGO_SVG("IP", "#b45309"),
    staffCount: 18,
    establishedYear: 2018,
    licenseNumber: "551/GCN-SGDĐT",
    licenseIssuedBy: "Sở GD&ĐT TP. Hà Nội",
    licenseIssuedDate: "2021-11-04",
    legalComplianceMode: "licensed",
    certifiedCounselorCount: 9,
    taxCode: "0108877665",
    legalRepresentative: "Phạm Thu Hà",
    accreditations: ["IECA Associate"],
    averageRating: 4.9,
    reviewCount: 41,
    verifiedAt: daysAgoIso(120),
    feeMin: 25000000,
    feeMax: 70000000,
    medianResponseHours: 1.5,
    responseRatePercent: 98,
    responseSampleCount: 31,
  },
  {
    id: "ctr-6",
    code: "CTR_HCM06",
    name: "Pacific Bridge Education",
    website: "https://pacificbridge.vn",
    address: "33 Lê Duẩn",
    district: "Quận 1",
    city: "TP.HCM",
    studyLevels: ["DaiHoc", "CaoDangCongDong", "PhoThong"],
    services: ["CHON_TRUONG", "SAN_HOC_BONG", "LUYEN_THI", "LUYEN_PHONG_VAN"],
    surveyedAt: daysAgoIso(5),
    phone: "0909881122",
    email: "team@pacificbridge.vn",
    description: "Cầu nối du học Mỹ kết hợp đào tạo tiếng Anh học thuật và chuẩn hóa hồ sơ tài chính.",
    verificationStatus: "verified",
    isActive: true,
    centerType: "hybrid",
    logoUrl: LOGO_SVG("PB", "#1e3a8a"),
    staffCount: 22,
    establishedYear: 2015,
    licenseNumber: "319/GCN-SGDĐT",
    licenseIssuedBy: "Sở GD&ĐT TP.HCM",
    licenseIssuedDate: "2020-08-20",
    legalComplianceMode: "licensed",
    trainingLicenseNumber: "105/GP-SGDĐT",
    certifiedCounselorCount: 10,
    taxCode: "0312233445",
    legalRepresentative: "Đặng Hoàng Nam",
    accreditations: ["ICEF Agency"],
    averageRating: 4.6,
    reviewCount: 27,
    verifiedAt: daysAgoIso(85),
    feeMin: 18000000,
    feeMax: 52000000,
    medianResponseHours: 4.0,
    responseRatePercent: 92,
    responseSampleCount: 18,
  },
];

export const MOCK_GUIDANCE_CARDS: CenterGuidanceCardsDto = {
  askCard: {
    topicCode: "CENTER_ASK",
    docType: "guidance",
    title: "5 câu hỏi nên hỏi trung tâm trước khi ký hợp đồng",
    content:
      "1. Tổng chi phí trọn gói gồm những khoản nào và có hoàn phí nếu trượt Visa?\n2. Ai là người trực tiếp sửa bài luận và theo sát hồ sơ?\n3. Trung tâm có minh bạch tài khoản nộp đơn trường (CommonApp/email trường) hay không?",
    source: "USDSS Knowledge Base",
    sourceUrl: "/methodology",
  },
  warningCard: {
    topicCode: "CENTER_WARNING",
    docType: "warning",
    title: "Dấu hiệu cần thận trọng khi làm việc với trung tâm",
    content:
      "Cảnh giác với cam kết 'đậu 100% Visa/học bổng', yêu cầu chuyển khoản đặt cọc vào tài khoản cá nhân thay vì tài khoản doanh nghiệp, hoặc từ chối cung cấp hợp đồng dịch vụ mẫu.",
    source: "Cục Hợp tác Quốc tế - Tham chiếu quy chuẩn",
    sourceUrl: "/methodology",
  },
};

export const MOCK_REVIEWS: CenterReviewDto[] = [
  {
    id: "rev-1",
    reviewerName: "Nguyễn M***",
    reviewerRole: "student",
    isVerified: true,
    createdAt: daysAgoIso(3),
    rating: 5,
    comment:
      "Anh chị cố vấn hỗ trợ sửa bài luận cực kỳ chi tiết, minh bạch toàn bộ tài khoản CommonApp và lịch nộp hồ sơ. Mình đạt học bổng 60% của Purdue.",
    centerReply: null,
    isOwnReview: false,
  },
  {
    id: "rev-2",
    reviewerName: "Phụ huynh Trần H***",
    reviewerRole: "parent",
    isVerified: true,
    createdAt: daysAgoIso(6),
    rating: 5,
    comment: "Hợp đồng rõ ràng từng đợt thanh toán, báo cáo tiến độ hàng tuần cho gia đình rất yên tâm.",
    centerReply: {
      content: "Cảm ơn gia đình đã tin tưởng đồng hành cùng Minh Khang Global trong suốt mùa hồ sơ vừa qua ạ!",
      createdAt: nowIso(-180), // < 24h → cho phép sửa
      editedAt: null,
    },
    isOwnReview: false,
  },
  {
    id: "rev-3",
    reviewerName: "Lê T***",
    reviewerRole: "student",
    isVerified: false,
    createdAt: daysAgoIso(25),
    rating: 3,
    comment: "Chất lượng tư vấn ổn nhưng phản hồi vào cuối tuần hơi chậm so với kỳ vọng.",
    centerReply: {
      content: "Cảm ơn bạn đã góp ý. Trung tâm đã bổ sung thêm ca trực tư vấn cuối tuần để hỗ trợ kịp thời hơn.",
      createdAt: daysAgoIso(20), // > 24h → khóa nút sửa
      editedAt: daysAgoIso(19),
    },
    isOwnReview: false,
  },
  {
    id: "rev-4",
    reviewerName: "Người dùng đã xóa tài khoản",
    reviewerRole: null,
    isVerified: false,
    createdAt: daysAgoIso(40),
    rating: 4,
    comment: "Luyện phỏng vấn Visa sát thực tế, có buổi mock với chuyên gia.",
    centerReply: null,
    isOwnReview: false,
  },
];

export const MOCK_LEADS: CenterReceivedLeadDto[] = [
  {
    id: "lead-1",
    studyCenterId: "ctr-1",
    studyCenterName: "Tư vấn Du học Minh Khang Global",
    studyLevel: "DaiHoc",
    contactPhone: "0912000111",
    message: "Em đang học lớp 11 chuyên Anh, GPA 9.1, IELTS 7.5 muốn tìm hiểu lộ trình săn học bổng Top 50 ngành CS.",
    sharedProfile: true,
    status: "accepted",
    createdAt: daysAgoIso(4),
    studentName: "Nguyễn Gia Huy",
    studentEmail: "giahuy.nguyen@example.com",
    profileSummary: {
      targetLevel: "DaiHoc",
      overallGpa: 9.1,
      educationSystem: "Vietnam_THPT",
      currentGrade: "Lớp 11",
      englishTestType: "IELTS",
      englishScore: 7.5,
      satScore: 1480,
      intendedMajor: "Computer Science",
      budgetTier: "30k - 45k USD/năm",
    },
  },
  {
    id: "lead-2",
    studyCenterId: "ctr-1",
    studyCenterName: "Tư vấn Du học Minh Khang Global",
    studyLevel: "ThacSi",
    contactPhone: "0987654321",
    message: "Mình muốn tư vấn hồ sơ Thạc sĩ Data Science kỳ Fall 2027 và chiến lược chọn trường có học bổng.",
    sharedProfile: true,
    status: "new",
    createdAt: daysAgoIso(1),
    studentName: "Phạm Thảo Vy",
    studentEmail: "thaovy.pham@example.com",
    expiresAt: daysAheadIso(6),
    daysUntilExpiry: 6,
    profileSummary: {
      targetLevel: "ThacSi",
      overallGpa: 3.65,
      englishTestType: "TOEFL",
      englishScore: 102,
      intendedMajor: "Data Science",
      budgetTier: "25k - 40k USD/năm",
    },
  },
  {
    id: "lead-3",
    studyCenterId: "ctr-1",
    studyCenterName: "Tư vấn Du học Minh Khang Global",
    studyLevel: "PhoThong",
    contactPhone: null,
    message: "Gia đình muốn tìm trường nội trú bang Massachusetts cho cháu vào lớp 10.",
    sharedProfile: false,
    status: "new",
    createdAt: daysAgoIso(6),
    studentName: "Phụ huynh Lê Minh",
    studentEmail: "minhle.parent@example.com",
    expiresAt: daysAheadIso(1),
    daysUntilExpiry: 1,
    profileSummary: null,
  },
  {
    id: "lead-4",
    studyCenterId: "ctr-1",
    studyCenterName: "Tư vấn Du học Minh Khang Global",
    studyLevel: "DaiHoc",
    contactPhone: "0903222333",
    message: "Em đã chọn trường gần nhà nên xin phép dừng trao đổi ạ.",
    sharedProfile: false,
    status: "closed",
    createdAt: daysAgoIso(18),
    studentName: "Vũ Đức Anh",
    studentEmail: "ducanh.vu@example.com",
  },
  {
    id: "lead-5",
    studyCenterId: "ctr-1",
    studyCenterName: "Tư vấn Du học Minh Khang Global",
    studyLevel: "CaoDangCongDong",
    contactPhone: null,
    message: null,
    sharedProfile: false,
    status: "withdrawn",
    dataHidden: true,
    createdAt: daysAgoIso(10),
    studentName: "Học sinh đã thu hồi",
    studentEmail: null,
  },
  {
    id: "lead-6",
    studyCenterId: "ctr-1",
    studyCenterName: "Tư vấn Du học Minh Khang Global",
    studyLevel: "PhoThong",
    contactPhone: null,
    message: null,
    sharedProfile: false,
    status: "closed",
    dataHidden: true,
    consentRevoked: true,
    createdAt: daysAgoIso(12),
    studentName: "Học sinh dưới 16 tuổi",
    studentEmail: null,
  },
  {
    id: "lead-7",
    studyCenterId: "ctr-1",
    studyCenterName: "Tư vấn Du học Minh Khang Global",
    studyLevel: "DaiHoc",
    contactPhone: "0919888777",
    message: "Em muốn hỏi gói luyện SAT cấp tốc 2 tháng.",
    sharedProfile: false,
    status: "rejected",
    createdAt: daysAgoIso(8),
    studentName: "Đỗ Bảo Ngọc",
    studentEmail: "baongoc.do@example.com",
  },
];

export const MOCK_LEAD_MESSAGES: Record<string, LeadMessageDto[]> = {
  "lead-1": [
    {
      id: "msg-1",
      leadId: "lead-1",
      senderUserId: "usr-student-1",
      senderName: "Nguyễn Gia Huy",
      senderRole: "student",
      isFromCenter: false,
      content: "Chào anh chị, em gửi kèm hồ sơ học tập nhờ trung tâm đánh giá giúp cơ hội học bổng ạ.",
      status: "visible",
      createdAt: daysAgoIso(3),
      readAt: daysAgoIso(3),
    },
    {
      id: "msg-2",
      leadId: "lead-1",
      senderUserId: "usr-center-1",
      senderName: "Tư vấn Du học Minh Khang Global",
      senderRole: "center",
      isFromCenter: true,
      content:
        "Chào Gia Huy, hồ sơ GPA 9.1 và IELTS 7.5 của em rất cạnh tranh cho nhóm Purdue, UMass Amherst và ASU. Anh gửi bảng lộ trình và biểu phí tham khảo theo hợp đồng mẫu (STK doanh nghiệp chỉ nhận sau khi ký hợp đồng chính thức).",
      status: "visible",
      createdAt: daysAgoIso(2),
      readAt: daysAgoIso(2),
      showPaymentReminder: true,
    },
    {
      id: "msg-3",
      leadId: "lead-1",
      senderUserId: "usr-student-1",
      senderName: "Nguyễn Gia Huy",
      senderRole: "student",
      isFromCenter: false,
      content: "Dạ em đã xem qua, tối thứ 5 lúc 19h em và bố mẹ có thể trao đổi online 30 phút được không ạ?",
      status: "visible",
      createdAt: nowIso(-45),
      readAt: null,
    },
  ],
};

export const MOCK_USERS: Record<UserRole | "guest", UserDto | null> = {
  guest: null,
  student: {
    id: "usr-student-1",
    email: "giahuy.student@example.com",
    fullName: "Nguyễn Gia Huy",
    role: "student",
    status: "active",
    ageGroup: "adult",
    parentConsentStatus: "not_required",
  },
  parent: {
    id: "usr-parent-1",
    email: "phuhuynh@example.com",
    fullName: "Nguyễn Văn Hùng",
    role: "parent",
    status: "active",
  },
  center: {
    id: "usr-center-1",
    email: "tuvan@minhkhang.edu.vn",
    fullName: "Nguyễn Minh Khang",
    role: "center",
    status: "active",
    phone: "0912345678",
  },
  moderator: {
    id: "usr-mod-1",
    email: "mod@usdss.vn",
    fullName: "Moderator Demo",
    role: "moderator",
    status: "active",
  },
  admin: {
    id: "usr-admin-1",
    email: "admin@usdss.vn",
    fullName: "Admin Demo",
    role: "admin",
    status: "active",
  },
};

const MOCK_POLICIES: Record<string, PublicPolicy> = {
  terms_of_use: {
    kind: "terms_of_use",
    version: "2026-11",
    title: "Điều khoản sử dụng nền tảng USDSS",
    content: "Nội dung điều khoản sử dụng cho trung tâm và học sinh.",
    changeSummary: null,
    requiresReconsent: false,
    publishedAt: "2026-11-01T00:00:00Z",
  },
  privacy_policy: {
    kind: "privacy_policy",
    version: "2026-11",
    title: "Chính sách bảo mật dữ liệu",
    content: "Nội dung chính sách bảo mật dữ liệu USDSS.",
    changeSummary: null,
    requiresReconsent: false,
    publishedAt: "2026-11-01T00:00:00Z",
  },
};

export interface AuditRuntimeConfig {
  pageId: AuditPageId;
  caseId: string;
  role: UserRole | "guest";
}

function buildPortalProfileForCase(pageId: AuditPageId, caseId: string): {
  center: StudyCenter;
  verification: CenterVerificationStatusDto;
  deletionRequest: CenterDeletionRequestDto | null;
} {
  const baseVerified = { ...MOCK_CENTERS[0] };
  const baseUnverified: StudyCenter = {
    ...MOCK_CENTERS[0],
    verificationStatus: "unverified",
    verifiedAt: null,
    licenseNumber: "",
    licenseIssuedBy: "",
    licenseIssuedDate: null,
    trainingLicenseNumber: "",
  };

  let vStatus = "verified";
  if (pageId === "portal_overview" || pageId === "portal_verification") {
    if (caseId === "unverified" || caseId === "unverified_same_person" || caseId === "unverified_need_poa") {
      vStatus = "unverified";
    } else if (caseId === "pending_review") {
      vStatus = "pending_review";
    } else if (caseId === "changes_requested" || caseId === "rejected") {
      vStatus = "rejected";
    } else if (caseId === "suspended") {
      vStatus = "suspended";
    } else {
      vStatus = "verified";
    }
  } else if (pageId === "portal_profile") {
    vStatus = caseId === "unverified_editable" ? "unverified" : "verified";
  } else if (pageId === "portal_reviews") {
    vStatus = caseId === "ineligible_unverified" ? "unverified" : "verified";
  }

  const center: StudyCenter =
    vStatus === "unverified"
      ? {
          ...baseUnverified,
          registrantName:
            pageId === "portal_verification" && caseId === "unverified_need_poa"
              ? "Trần Thị Thu Trang"
              : baseUnverified.legalRepresentative,
        }
      : { ...baseVerified, verificationStatus: vStatus };

  const requestStatus =
    caseId === "changes_requested"
      ? "changes_requested"
      : caseId === "rejected"
        ? "rejected"
        : caseId === "pending_review"
          ? "pending_review"
          : "approved";

  const hasRequest = vStatus !== "unverified" && vStatus !== "suspended";

  const verification: CenterVerificationStatusDto = {
    verificationStatus: vStatus,
    isEmailVerified: true,
    emailVerifiedAt: daysAgoIso(90),
    rejectionReason:
      caseId === "changes_requested"
        ? "Ảnh chụp Giấy chứng nhận ĐKKD trang 2 bị mờ phần mã số doanh nghiệp. Vui lòng tải lại bản rõ nét hơn và bổ sung hợp đồng mẫu có đủ điều khoản hoàn phí."
        : caseId === "rejected"
          ? "Tên doanh nghiệp trên hồ sơ không khớp với thông tin trên Cổng thông tin Quốc gia về đăng ký doanh nghiệp."
          : caseId === "suspended"
            ? "Phát hiện phản ánh về việc thu phí ngoài hợp đồng công bố. Tài khoản tạm khóa để xác minh."
            : null,
    currentRequest: hasRequest
      ? {
          id: "ver-req-1",
          centerId: center.id,
          requestedByUserId: "usr-center-1",
          legalName: center.name,
          taxCode: center.taxCode ?? "0107654321",
          address: center.address ?? "54 Liễu Giai, Ba Đình, Hà Nội",
          legalRepresentative: center.legalRepresentative ?? "Nguyễn Minh Khang",
          website: center.website,
          status: requestStatus,
          submittedAt: daysAgoIso(3),
          reviewedAt: vStatus === "pending_review" ? null : daysAgoIso(1),
          rejectionReason: null,
          isNationalRegistryMatched: vStatus === "verified",
          isBusinessOperating: vStatus === "verified",
          isRepresentativeValid: vStatus === "verified",
          isServiceContractValid: vStatus === "verified",
          documents: [
            {
              id: "doc-1",
              requestId: "ver-req-1",
              documentType: "business_registration",
              fileUrl: "#mock-biz-reg.pdf",
              fileName: "GPKD-MinhKhangGlobal.pdf",
              fileSize: 1_240_000,
              contentType: "application/pdf",
              uploadedAt: daysAgoIso(3),
            },
            {
              id: "doc-2",
              requestId: "ver-req-1",
              documentType: "sample_service_contract",
              fileUrl: "#mock-contract.pdf",
              fileName: "Hop-dong-dich-vu-mau-2026.pdf",
              fileSize: 890_000,
              contentType: "application/pdf",
              uploadedAt: daysAgoIso(3),
            },
          ],
        }
      : null,
  };

  let deletionRequest: CenterDeletionRequestDto | null = null;
  if (pageId === "portal_profile" && caseId === "deletion_pending") {
    deletionRequest = {
      id: "del-1",
      studyCenterId: center.id,
      reason: "Trung tâm tạm dừng hoạt động tư vấn du học Mỹ để tái cấu trúc pháp nhân.",
      status: "pending",
      createdAt: daysAgoIso(2),
      handledAt: null,
      dueAt: daysAheadIso(12),
    };
  } else if (pageId === "portal_profile" && caseId === "deletion_approved") {
    deletionRequest = {
      id: "del-2",
      studyCenterId: center.id,
      reason: "Ngừng hợp tác vận hành trên nền tảng từ tháng 12/2026.",
      status: "approved",
      createdAt: daysAgoIso(5),
      handledAt: daysAgoIso(2),
      dueAt: daysAheadIso(28),
    };
  }

  return { center, verification, deletionRequest };
}

function jsonResponse(body: unknown, status = 200): Response {
  return new Response(body === null ? "" : JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json" },
  });
}

/**
 * Installs a scoped window.fetch interceptor while /audit/center is active so every real
 * production component renders cleanly without requiring a live backend.
 */
export function installAuditFetchInterceptor(getConfig: () => AuditRuntimeConfig): () => void {
  if (typeof window === "undefined") return () => undefined;

  const originalFetch = window.fetch.bind(window);
  let localLeads = structuredClone(MOCK_LEADS);
  let localReviews = structuredClone(MOCK_REVIEWS);

  window.fetch = async (input: RequestInfo | URL, init?: RequestInit): Promise<Response> => {
    const rawUrl = typeof input === "string" ? input : input instanceof Request ? input.url : String(input);
    let url: URL;
    try {
      url = new URL(rawUrl, window.location.origin);
    } catch {
      return originalFetch(input, init);
    }

    if (!url.pathname.startsWith("/api/v1/")) {
      return originalFetch(input, init);
    }

    const { pageId, caseId, role } = getConfig();
    const method = (init?.method ?? (input instanceof Request ? input.method : "GET")).toUpperCase();
    const path = url.pathname;

    // 1. Auth & session
    if (path === "/api/v1/auth/me") {
      const user = MOCK_USERS[role];
      return user ? jsonResponse(user) : jsonResponse({ message: "Chưa đăng nhập" }, 401);
    }
    if (path === "/api/v1/auth/acting") {
      return jsonResponse({ acting: false, childId: null, childName: null });
    }
    if (path === "/api/v1/auth/refresh") {
      return jsonResponse({ expiresAt: daysAheadIso(1) });
    }
    if (path === "/api/v1/auth/resend-otp") {
      return jsonResponse({ message: "Đã gửi lại mã OTP mẫu (123456) tới hộp thư của bạn." });
    }

    // 2. Policies & header chrome
    if (path === "/api/v1/policy-acceptance") {
      return jsonResponse({ mustAccept: false, hasUpdates: false, items: [] });
    }
    if (path.startsWith("/api/v1/policies/")) {
      const kind = path.split("/").pop() ?? "";
      if (pageId === "register_form" && caseId === "policies_error") {
        return jsonResponse({ message: "Không tải được điều khoản (mô phỏng lỗi)." }, 500);
      }
      const policy = MOCK_POLICIES[kind];
      return policy ? jsonResponse(policy) : jsonResponse({ message: "Not found" }, 404);
    }
    if (path === "/api/v1/notifications/unread-count") {
      return jsonResponse({ unreadCount: 2 });
    }
    if (path === "/api/v1/notifications") {
      return jsonResponse({ items: [], page: 1, pageSize: 20, totalCount: 0, totalPages: 1 });
    }
    if (path === "/api/v1/subscription") {
      return jsonResponse({ tier: "free", status: "active" });
    }
    if (path === "/api/v1/subscription/quota") {
      return jsonResponse({ used: 1, limit: 5 });
    }
    if (path === "/api/v1/parent-links/me") {
      return jsonResponse({ items: [] });
    }
    if (path === "/api/v1/leads/sent/conversations" || path === "/api/v1/leads/received/conversations") {
      return jsonResponse({ totalUnread: 1, items: [] });
    }

    // 3. Public study centers
    if (path === "/api/v1/study-centers/guidance-cards") {
      if (pageId === "public_detail" && caseId === "unverified_minimal") {
        return jsonResponse({ askCard: null, warningCard: null });
      }
      return jsonResponse(MOCK_GUIDANCE_CARDS);
    }

    const reviewsEligibilityMatch = path.match(/^\/api\/v1\/study-centers\/([^/]+)\/reviews\/eligibility$/);
    if (reviewsEligibilityMatch) {
      if (caseId === "student_already_reviewed") {
        return jsonResponse({ canReview: false, hasReviewed: true, reason: null });
      }
      if (caseId === "student_ineligible") {
        return jsonResponse({
          canReview: false,
          hasReviewed: false,
          reason: "Bạn cần gửi yêu cầu liên hệ và trao đổi với trung tâm trước khi viết đánh giá.",
        });
      }
      return jsonResponse({ canReview: true, hasReviewed: false, reason: null });
    }

    const publicReviewsMatch = path.match(/^\/api\/v1\/study-centers\/([^/]+)\/reviews$/);
    if (publicReviewsMatch) {
      if (method === "POST") {
        return jsonResponse(
          {
            id: `rev-${Date.now()}`,
            reviewerName: "Nguyễn Gia Huy",
            reviewerRole: "student",
            isVerified: true,
            createdAt: nowIso(),
            rating: 5,
            comment: "Đánh giá mới (mô phỏng).",
            centerReply: null,
            isOwnReview: true,
          },
          201,
        );
      }
      if (pageId === "public_detail" && caseId === "reviews_error") {
        return jsonResponse({ message: "Lỗi tải danh sách đánh giá (mô phỏng)." }, 500);
      }
      if (pageId === "public_detail" && caseId === "unverified_minimal") {
        return jsonResponse({ totalCount: 0, averageRating: null, items: [] } satisfies CenterReviewsDto);
      }
      return jsonResponse({
        totalCount: MOCK_REVIEWS.length,
        averageRating: 4.3,
        items: MOCK_REVIEWS,
      } satisfies CenterReviewsDto);
    }

    // 4. Center Portal APIs
    const portalBundle = buildPortalProfileForCase(pageId, caseId);

    if (path === "/api/v1/center/profile") {
      if (method === "PUT" || method === "PATCH") {
        return jsonResponse({
          center: portalBundle.center,
          outcome: {
            savedFields: ["description", "address"],
            reviewFields: [],
            legalFields: [],
            verificationReset: false,
            message: "Đã lưu thay đổi hồ sơ (mô phỏng /audit/center).",
          },
        });
      }
      return jsonResponse(portalBundle.center);
    }

    if (path === "/api/v1/center/verification/status") {
      return jsonResponse(portalBundle.verification);
    }
    if (path === "/api/v1/center/verification/documents") {
      return jsonResponse({
        documentType: "business_registration",
        fileUrl: "#mock-uploaded-doc.pdf",
        fileName: "tai-lieu-moi-tai-len.pdf",
        fileSize: 512_000,
        contentType: "application/pdf",
      });
    }
    if (path === "/api/v1/center/verification" && method === "POST") {
      return jsonResponse({ ...portalBundle.center, verificationStatus: "pending_review" });
    }

    if (path === "/api/v1/center/deletion-request") {
      if (method === "POST") {
        return jsonResponse({
          id: "del-new",
          studyCenterId: portalBundle.center.id,
          reason: "Yêu cầu xóa mô phỏng từ /audit/center",
          status: "pending",
          createdAt: nowIso(),
          handledAt: null,
          dueAt: daysAheadIso(14),
        } satisfies CenterDeletionRequestDto);
      }
      return jsonResponse(portalBundle.deletionRequest);
    }

    if (path === "/api/v1/center/reviews") {
      if (pageId === "portal_reviews" && caseId === "error") {
        return jsonResponse({ message: "Không thể tải danh sách đánh giá (mô phỏng)." }, 500);
      }
      if (pageId === "portal_reviews" && caseId === "empty") {
        return jsonResponse({ totalCount: 0, averageRating: null, items: [] } satisfies CenterReviewsDto);
      }
      return jsonResponse({
        totalCount: localReviews.length,
        averageRating: 4.3,
        items: localReviews,
      } satisfies CenterReviewsDto);
    }

    const replyMatch = path.match(/^\/api\/v1\/center\/reviews\/([^/]+)\/reply$/);
    if (replyMatch) {
      const reviewId = decodeURIComponent(replyMatch[1]);
      const parsed = init?.body ? (JSON.parse(String(init.body)) as { content?: string }) : {};
      localReviews = localReviews.map((r) =>
        r.id === reviewId
          ? {
              ...r,
              centerReply: {
                content: parsed.content ?? "",
                createdAt: r.centerReply?.createdAt ?? nowIso(),
                editedAt: r.centerReply ? nowIso() : null,
              },
            }
          : r,
      );
      const updated = localReviews.find((r) => r.id === reviewId) ?? localReviews[0];
      return jsonResponse(updated);
    }

    // 5. Center Leads & Chat
    if (path === "/api/v1/leads/received") {
      if (pageId === "portal_leads" && caseId === "empty") {
        localLeads = [];
        return jsonResponse({
          items: [],
          totalCount: 0,
          page: 1,
          pageSize: 10,
          newCount: 0,
          contactedCount: 0,
          closedCount: 0,
          rejectedCount: 0,
        } satisfies CenterLeadsSummaryDto);
      }
      if (localLeads.length === 0) localLeads = structuredClone(MOCK_LEADS);
      const statusFilter = url.searchParams.get("status");
      const search = (url.searchParams.get("search") ?? "").trim().toLowerCase();
      const filtered = localLeads.filter((l) => {
        if (statusFilter && statusFilter !== "all") {
          if (statusFilter === "contacted") {
            if (l.status !== "accepted" && l.status !== "contacted") return false;
          } else if (l.status !== statusFilter) {
            return false;
          }
        }
        if (search) {
          const hay = `${l.studentName ?? ""} ${l.studentEmail ?? ""} ${l.message ?? ""}`.toLowerCase();
          if (!hay.includes(search)) return false;
        }
        return true;
      });
      return jsonResponse({
        items: filtered,
        totalCount: filtered.length,
        page: 1,
        pageSize: 10,
        newCount: localLeads.filter((l) => l.status === "new").length,
        contactedCount: localLeads.filter((l) => l.status === "accepted" || l.status === "contacted").length,
        closedCount: localLeads.filter((l) => l.status === "closed").length,
        rejectedCount: localLeads.filter((l) => l.status === "rejected").length,
      } satisfies CenterLeadsSummaryDto);
    }

    const leadActionMatch = path.match(/^\/api\/v1\/leads\/received\/([^/]+)\/(accept|reject|status)$/);
    if (leadActionMatch) {
      const leadId = decodeURIComponent(leadActionMatch[1]);
      const action = leadActionMatch[2];
      let nextStatus = action === "accept" ? "accepted" : action === "reject" ? "rejected" : "closed";
      if (action === "status" && init?.body) {
        const parsed = JSON.parse(String(init.body)) as { status?: string };
        if (parsed.status) nextStatus = parsed.status;
      }
      localLeads = localLeads.map((l) => (l.id === leadId ? { ...l, status: nextStatus } : l));
      return jsonResponse(localLeads.find((l) => l.id === leadId) ?? localLeads[0]);
    }

    const leadMsgMatch = path.match(/^\/api\/v1\/leads\/received\/([^/]+)\/messages$/);
    if (leadMsgMatch) {
      const leadId = decodeURIComponent(leadMsgMatch[1]);
      const lead = localLeads.find((l) => l.id === leadId);
      const msgs = MOCK_LEAD_MESSAGES[leadId] ?? [];
      if (method === "POST") {
        const parsed = init?.body ? (JSON.parse(String(init.body)) as { content?: string }) : {};
        const newMsg: LeadMessageDto = {
          id: `msg-${Date.now()}`,
          leadId,
          senderUserId: "usr-center-1",
          senderName: "Tư vấn Du học Minh Khang Global",
          senderRole: "center",
          isFromCenter: true,
          content: parsed.content ?? "",
          status: "visible",
          createdAt: nowIso(),
          readAt: null,
        };
        MOCK_LEAD_MESSAGES[leadId] = [...msgs, newMsg];
        return jsonResponse({ message: newMsg, leadStatus: lead?.status ?? "accepted" });
      }
      return jsonResponse({
        leadId,
        leadStatus: lead?.status ?? "accepted",
        messages: msgs,
      });
    }

    // 6. Center Registration
    if (path === "/api/v1/center-accounts/register" && method === "POST") {
      return jsonResponse({ message: "Đăng ký thành công (mô phỏng)", email: "center-moi@example.com" });
    }
    if (path === "/api/v1/center-accounts/verify-email" && method === "POST") {
      return jsonResponse({ expiresAt: daysAheadIso(1), user: MOCK_USERS.center });
    }

    // Fallback safe JSON so background/optional calls don't crash UI
    return jsonResponse({});
  };

  return () => {
    window.fetch = originalFetch;
  };
}
