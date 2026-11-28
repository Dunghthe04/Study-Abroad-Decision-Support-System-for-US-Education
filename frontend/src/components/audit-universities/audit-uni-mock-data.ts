// ponytail: temporary mock fixtures & fetch interceptor for /audit/universities UI redesign preview
import type { PagedResult, TargetSchoolDefaultsDto, TargetSchoolDto, UserRole } from "@/types/api";
import type { BudgetCompare, CostEstimate, SchoolCost, StateLivingCost } from "@/types/cost";
import type { ScholarshipItem } from "@/types/scholarship";
import type {
  UniversityDetail,
  UniversityFilterOptions,
  UniversityListItem,
  UniversitySearchParams,
} from "@/types/university";
import { MOCK_USERS } from "@/components/audit-center/audit-mock-data";

export type UniAuditPageId = "uni_list" | "uni_detail" | "uni_costs";

export interface UniAuditCaseDef {
  id: string;
  label: string;
  description: string;
  defaultRole?: UserRole | "guest";
}

export interface UniAuditPageDef {
  id: UniAuditPageId;
  route: string;
  label: string;
  shortLabel: string;
  defaultRole: UserRole | "guest";
  cases: UniAuditCaseDef[];
}

export const AUDIT_UNI_PAGES: UniAuditPageDef[] = [
  {
    id: "uni_list",
    route: "/universities",
    label: "Danh sách trường ĐH Mỹ",
    shortLabel: "1. /universities",
    defaultRole: "student",
    cases: [
      { id: "populated", label: "Có dữ liệu (10 trường, lọc sống)", description: "Đủ bộ lọc, xếp hạng, Ivy, Test-Optional, Hỗ trợ quốc tế & nút Lưu mục tiêu" },
      { id: "filtered_narrow", label: "Đang bật bộ lọc (2 trường)", description: "Lọc sẵn Ivy League + Học phí ≤ $65k với chip bộ lọc đang bật" },
      { id: "empty", label: "Không có kết quả", description: "Trạng thái tìm kiếm không khớp trường nào + nút đặt lại bộ lọc" },
    ],
  },
  {
    id: "uni_detail",
    route: "/university/[code]",
    label: "Chi tiết trường ĐH",
    shortLabel: "2. /university/[code]",
    defaultRole: "student",
    cases: [
      { id: "full_ivy", label: "Đủ dữ liệu (MIT)", description: "Ảnh bìa, gallery + ghi công, nhiều bảng xếp hạng, 2 bậc học, GPA/SAT/hạn nộp, học bổng" },
      { id: "saved_target", label: "Đã lưu vào trường mục tiêu", description: "Hiển thị trạng thái đã lưu nhóm Khó vào (ED) trong nút hành động" },
      { id: "sparse_missing", label: "Thiếu nhiều dữ liệu", description: "Không ảnh, nhiều trường thiếu (—), không khuôn viên, 0 học bổng" },
      { id: "guest", label: "Chế độ Khách (chưa đăng nhập)", description: "Ẩn nút Lưu trường mục tiêu dành cho học sinh/phụ huynh", defaultRole: "guest" },
    ],
  },
  {
    id: "uni_costs",
    route: "/costs",
    label: "Tra cứu & So sánh chi phí",
    shortLabel: "3. /costs",
    defaultRole: "student",
    cases: [
      { id: "school_empty", label: "Theo trường: chưa chọn trường", description: "Bố cục tìm kiếm căn giữa ban đầu (/costs)" },
      { id: "school_single", label: "Theo trường: 1 trường (MIT)", description: "Ước tính chi tiết năm đầu, cả khóa 4 năm, biểu đồ cấu phần & ngân sách" },
      { id: "school_compare_3", label: "So sánh 3 trường", description: "MIT vs Purdue vs UC Berkeley (ô xanh thấp nhất / đỏ cao nhất)" },
      { id: "school_incomplete", label: "Trường thiếu dữ liệu học phí", description: "Cảnh báo chưa đủ học phí + ẩn tổng của trường thiếu" },
      { id: "school_too_many", label: "Chọn quá 4 trường (5 trường)", description: "Thông báo giới hạn tối đa 4 trường (/costs?codes=...)" },
      { id: "state_living_costs", label: "Theo bang: bản đồ & chỉ số", description: "Chế độ Bang (/costs?mode=state & /universities/living-costs)" },
      { id: "state_error", label: "Theo bang: lỗi tải dữ liệu", description: "Màn hình ErrorFallback khi API bang lỗi" },
    ],
  },
];

export const MOCK_UNI_FILTER_OPTIONS: UniversityFilterOptions = {
  states: [
    { value: "MA", label: "Massachusetts" },
    { value: "CA", label: "California" },
    { value: "IN", label: "Indiana" },
    { value: "MI", label: "Michigan" },
    { value: "NJ", label: "New Jersey" },
    { value: "WA", label: "Washington" },
    { value: "AZ", label: "Arizona" },
  ],
  controls: [
    { value: "private_nonprofit", label: "Tư thục phi lợi nhuận" },
    { value: "public", label: "Công lập" },
  ],
  majors: [
    { value: "Computer Science", label: "Computer Science" },
    { value: "Electrical Engineering", label: "Electrical Engineering" },
    { value: "Economics", label: "Economics" },
    { value: "Data Science", label: "Data Science" },
    { value: "Mechanical Engineering", label: "Mechanical Engineering" },
    { value: "Business Administration", label: "Business Administration" },
  ],
  minTuitionUsd: 10000,
  maxTuitionUsd: 70000,
  minTotalCostUsd: 24000,
  maxTotalCostUsd: 92000,
  rankingLists: [
    { value: "USN27-NU", label: "U.S. News National Universities 2027" },
    { value: "USN27-LAC", label: "U.S. News Liberal Arts Colleges 2027" },
    { value: "QS27-WUR", label: "QS World University Rankings 2027" },
  ],
  ranges: {
    minLivingUsd: 11000,
    maxLivingUsd: 26000,
    minSat: 950,
    maxSat: 1600,
    minAct: 18,
    maxAct: 36,
    minStudentFacultyRatio: 3,
    maxStudentFacultyRatio: 26,
    minIntlStudents: 50,
    maxIntlStudents: 15000,
    maxIntlAidAvgUsd: 75000,
  },
  housingTypes: [
    "Special housing for international students",
    "Living Learning Communities",
    "Cooperative housing",
  ],
  activities: [
    "International Student Organization",
    "Model UN",
    "Symphony orchestra",
    "Student government",
  ],
};

export const MOCK_UNIVERSITIES: UniversityListItem[] = [
  {
    id: "uni-mit",
    code: "MIT",
    name: "Massachusetts Institute of Technology",
    city: "Cambridge",
    state: "MA",
    stateName: "Massachusetts",
    control: "private_nonprofit",
    acceptanceRate: 0.045,
    isIvy: false,
    logoUrl: "/schools/mit.jpg",
    coverUrl: "/schools/mit.jpg",
    tuitionUsd: 61990,
    totalCostUsd: 85960,
    minIelts: 7.5,
    minToefl: 100,
    minDuolingo: 135,
    bestRank: 2,
    bestRankList: "USN27-NU",
    satPolicy: "required",
    actPolicy: "required",
    intlAidPolicy: "need_only",
    majors: ["Computer Science", "Electrical Engineering", "Mechanical Engineering", "Data Science"],
  },
  {
    id: "uni-harvard",
    code: "HARVARD",
    name: "Harvard University",
    city: "Cambridge",
    state: "MA",
    stateName: "Massachusetts",
    control: "private_nonprofit",
    acceptanceRate: 0.036,
    isIvy: true,
    logoUrl: "/schools/harvard.jpg",
    coverUrl: "/schools/harvard.jpg",
    tuitionUsd: 59320,
    totalCostUsd: 86926,
    minIelts: 7.5,
    minToefl: 105,
    minDuolingo: null,
    bestRank: 3,
    bestRankList: "USN27-NU",
    satPolicy: "required",
    actPolicy: "required",
    intlAidPolicy: "need_only",
    majors: ["Economics", "Computer Science", "Data Science"],
  },
  {
    id: "uni-princeton",
    code: "PRINCETON",
    name: "Princeton University",
    city: "Princeton",
    state: "NJ",
    stateName: "New Jersey",
    control: "private_nonprofit",
    acceptanceRate: 0.046,
    isIvy: true,
    logoUrl: "/schools/princeton.jpg",
    coverUrl: "/schools/princeton.jpg",
    tuitionUsd: 62400,
    totalCostUsd: 86700,
    minIelts: 7.0,
    minToefl: 100,
    minDuolingo: null,
    bestRank: 1,
    bestRankList: "USN27-NU",
    satPolicy: "optional",
    actPolicy: "optional",
    intlAidPolicy: "need_only",
    majors: ["Economics", "Computer Science", "Mechanical Engineering"],
  },
  {
    id: "uni-stanford",
    code: "STANFORD",
    name: "Stanford University",
    city: "Stanford",
    state: "CA",
    stateName: "California",
    control: "private_nonprofit",
    acceptanceRate: 0.039,
    isIvy: false,
    logoUrl: "/schools/stanford.jpg",
    coverUrl: "/schools/stanford.jpg",
    tuitionUsd: 65127,
    totalCostUsd: 90480,
    minIelts: 7.0,
    minToefl: 100,
    minDuolingo: 130,
    bestRank: 4,
    bestRankList: "USN27-NU",
    satPolicy: "required",
    actPolicy: "required",
    intlAidPolicy: "need_only",
    majors: ["Computer Science", "Data Science", "Electrical Engineering"],
  },
  {
    id: "uni-berkeley",
    code: "BERKELEY",
    name: "University of California, Berkeley",
    city: "Berkeley",
    state: "CA",
    stateName: "California",
    control: "public",
    acceptanceRate: 0.114,
    isIvy: false,
    logoUrl: "/schools/berkeley.jpg",
    coverUrl: "/schools/berkeley.jpg",
    tuitionUsd: 48465,
    totalCostUsd: 74200,
    minIelts: 6.5,
    minToefl: 80,
    minDuolingo: 115,
    bestRank: 17,
    bestRankList: "USN27-NU",
    satPolicy: "not_accepted",
    actPolicy: "not_accepted",
    intlAidPolicy: "none",
    majors: ["Computer Science", "Data Science", "Economics", "Electrical Engineering"],
  },
  {
    id: "uni-michigan",
    code: "MICHIGAN",
    name: "University of Michigan–Ann Arbor",
    city: "Ann Arbor",
    state: "MI",
    stateName: "Michigan",
    control: "public",
    acceptanceRate: 0.179,
    isIvy: false,
    logoUrl: "/schools/michigan.jpg",
    coverUrl: "/schools/michigan.jpg",
    tuitionUsd: 58072,
    totalCostUsd: 76900,
    minIelts: 7.0,
    minToefl: 100,
    minDuolingo: 130,
    bestRank: 21,
    bestRankList: "USN27-NU",
    satPolicy: "optional",
    actPolicy: "optional",
    intlAidPolicy: "none",
    majors: ["Business Administration", "Computer Science", "Mechanical Engineering"],
  },
  {
    id: "uni-uw",
    code: "UW",
    name: "University of Washington",
    city: "Seattle",
    state: "WA",
    stateName: "Washington",
    control: "public",
    acceptanceRate: 0.425,
    isIvy: false,
    logoUrl: "/schools/uw.jpg",
    coverUrl: "/schools/uw.jpg",
    tuitionUsd: 42423,
    totalCostUsd: 62500,
    minIelts: 7.0,
    minToefl: 92,
    minDuolingo: 120,
    bestRank: 46,
    bestRankList: "USN27-NU",
    satPolicy: "optional",
    actPolicy: "optional",
    intlAidPolicy: "merit_only",
    majors: ["Computer Science", "Data Science", "Business Administration"],
  },
  {
    id: "uni-purdue",
    code: "PURDUE",
    name: "Purdue University",
    city: "West Lafayette",
    state: "IN",
    stateName: "Indiana",
    control: "public",
    acceptanceRate: 0.503,
    isIvy: false,
    logoUrl: "/schools/purdue.jpg",
    coverUrl: "/schools/purdue.jpg",
    tuitionUsd: 28794,
    totalCostUsd: 43900,
    minIelts: 6.5,
    minToefl: 80,
    minDuolingo: 115,
    bestRank: 46,
    bestRankList: "USN27-NU",
    satPolicy: "required",
    actPolicy: "required",
    intlAidPolicy: "merit_only",
    majors: ["Mechanical Engineering", "Computer Science", "Electrical Engineering"],
  },
  {
    id: "uni-asu",
    code: "ASU",
    name: "Arizona State University",
    city: "Tempe",
    state: "AZ",
    stateName: "Arizona",
    control: "public",
    acceptanceRate: 0.898,
    isIvy: false,
    logoUrl: "/schools/asu.jpg",
    coverUrl: "/schools/asu.jpg",
    tuitionUsd: 32193,
    totalCostUsd: 51400,
    minIelts: 6.0,
    minToefl: 61,
    minDuolingo: 95,
    bestRank: 121,
    bestRankList: "USN27-NU",
    satPolicy: "optional",
    actPolicy: "optional",
    intlAidPolicy: "need_and_merit",
    majors: ["Business Administration", "Computer Science", "Mechanical Engineering"],
  },
  {
    id: "uni-sparse",
    code: "SPARSE_CC",
    name: "Shoreline Regional College",
    city: "Shoreline",
    state: "WA",
    stateName: "Washington",
    control: "public",
    acceptanceRate: null,
    isIvy: false,
    logoUrl: null,
    coverUrl: null,
    tuitionUsd: null,
    totalCostUsd: null,
    minIelts: 5.5,
    minToefl: null,
    minDuolingo: null,
    bestRank: null,
    bestRankList: null,
    satPolicy: null,
    actPolicy: null,
    intlAidPolicy: null,
    majors: ["Business Administration"],
  },
];

export const MOCK_MIT_DETAIL: UniversityDetail = {
  code: "MIT",
  name: "Massachusetts Institute of Technology",
  city: "Cambridge",
  state: "MA",
  stateName: "Massachusetts",
  website: "https://www.mit.edu",
  control: "private_nonprofit",
  acceptanceRate: 0.045,
  internationalStudentCount: 3820,
  isIvy: false,
  logoUrl: "/schools/mit.jpg",
  coverUrl: "/schools/mit.jpg",
  galleryUrls: ["/schools/mit.jpg", "/schools/harvard.jpg", "/schools/stanford.jpg"],
  imageCredits: [
    { url: "/schools/mit.jpg", license: "CC BY-SA 4.0", author: "MIT News Archive", sourcePageUrl: "https://commons.wikimedia.org" },
    { url: "/schools/harvard.jpg", license: "CC BY 4.0", author: "Campus Commons", sourcePageUrl: "https://commons.wikimedia.org" },
  ],
  offerings: [
    {
      studyLevel: "undergraduate",
      academicYear: "2025-2026",
      tuitionUsd: 61990,
      livingUsd: 20480,
      feesUsd: 420,
      otherExpensesUsd: 3070,
      healthInsuranceUsd: 3560,
      annualCostUsd: 85960,
      minGpa4: 3.9,
      minIelts: 7.5,
      minToefl: 100,
      minDuolingo: 135,
      avgGpa4: 3.97,
      avgGpaWeighted: 4.19,
      gpaScaleType: "both",
      sat25: 1520,
      sat75: 1580,
      act25: 34,
      act75: 36,
      applicantsTotal: 28232,
      admittedTotal: 1275,
      intlApplicants: 6150,
      intlAdmitted: 124,
      intlAcceptanceRate: 0.02,
      satPolicy: "required",
      actPolicy: "required",
      deadlinesJson: JSON.stringify({ EA: "2026-11-01", RD: "2027-01-05" }),
      c7GpaImportance: "very_important",
      c7TestImportance: "very_important",
      c7EcImportance: "very_important",
      satSubmitPct: 81,
      actSubmitPct: 24,
      gpaSubmitPct: 98,
      gpaPct40: 84.2,
      gpaPct375To399: 11.5,
      gpaPct350To374: 3.1,
      gpaPct325To349: 0.9,
      gpaPct300To324: 0.3,
      gpaPct250To299: 0,
      gpaPctBelow250: 0,
      intlAidPolicy: "need_only",
      intlAidAvgUsd: 64500,
      intlAidRecipients: 412,
      retentionRate1Yr: 0.99,
      gradRate6Yr: 0.96,
      fyOnCampusPct: 99,
      ugOnCampusPct: 92,
      housingTypes: "Special housing for international students; Living Learning Communities; Cooperative housing",
      intlHousing: true,
      housingOnlyUsd: 13200,
      housingDepositUsd: 0,
      studentFacultyRatio: 3,
      activities: "International Student Organization; Model UN; Symphony orchestra; Student government",
      campusSetting: "City: Midsize",
      applicationFeeUsd: 75,
      tuitionInStateUsd: 61990,
      majors: [
        { name: "Computer Science and Engineering", cipCode: "11.0701", isStem: true, isVerified: true },
        { name: "Electrical Engineering", cipCode: "14.1001", isStem: true, isVerified: true },
        { name: "Mechanical Engineering", cipCode: "14.1901", isStem: true, isVerified: true },
        { name: "Mathematics", cipCode: "27.0101", isStem: true, isVerified: true },
        { name: "Economics", cipCode: "45.0601", isStem: true, isVerified: true },
      ],
      sourceUrl: "https://registrar.mit.edu",
      retrievedAt: "2026-10-15T00:00:00Z",
      sourceReliability: "official",
    },
    {
      studyLevel: "graduate",
      academicYear: "2025-2026",
      tuitionUsd: 61990,
      livingUsd: 24800,
      feesUsd: 420,
      otherExpensesUsd: 2900,
      healthInsuranceUsd: 3560,
      annualCostUsd: 90110,
      minGpa4: 3.7,
      minIelts: 7.0,
      minToefl: 100,
      minDuolingo: null,
      avgGpa4: null,
      avgGpaWeighted: null,
      gpaScaleType: null,
      sat25: null,
      sat75: null,
      act25: null,
      act75: null,
      applicantsTotal: null,
      admittedTotal: null,
      intlApplicants: null,
      intlAdmitted: null,
      intlAcceptanceRate: null,
      satPolicy: "not_accepted",
      actPolicy: "not_accepted",
      deadlinesJson: JSON.stringify({ RD: "2026-12-15" }),
      c7GpaImportance: null,
      c7TestImportance: null,
      c7EcImportance: null,
      satSubmitPct: null,
      actSubmitPct: null,
      gpaSubmitPct: null,
      gpaPct40: null,
      gpaPct375To399: null,
      gpaPct350To374: null,
      gpaPct325To349: null,
      gpaPct300To324: null,
      gpaPct250To299: null,
      gpaPctBelow250: null,
      intlAidPolicy: "need_and_merit",
      intlAidAvgUsd: null,
      intlAidRecipients: null,
      retentionRate1Yr: null,
      gradRate6Yr: null,
      fyOnCampusPct: null,
      ugOnCampusPct: null,
      housingTypes: null,
      intlHousing: true,
      housingOnlyUsd: null,
      housingDepositUsd: null,
      studentFacultyRatio: 3,
      activities: null,
      campusSetting: "City: Midsize",
      applicationFeeUsd: 90,
      tuitionInStateUsd: 61990,
      majors: [
        { name: "EECS (SM / PhD)", cipCode: "11.0701", isStem: true, isVerified: true },
        { name: "Operations Research", cipCode: "14.3701", isStem: true, isVerified: true },
      ],
      sourceUrl: "https://gradadmissions.mit.edu",
      retrievedAt: "2026-10-15T00:00:00Z",
      sourceReliability: "official",
    },
  ],
  rankings: [
    { publisher: "U.S. News", name: "National Universities", rank: 2, isTie: false, rankDisplay: "#2", edition: "2027", editionYear: 2027, sourceUrl: "https://www.usnews.com" },
    { publisher: "QS", name: "QS World University Rankings", rank: 1, isTie: false, rankDisplay: "#1", edition: "2027", editionYear: 2027, sourceUrl: "https://www.topuniversities.com" },
    { publisher: "U.S. News", name: "Best Undergraduate Engineering", rank: 1, isTie: false, rankDisplay: "#1", edition: "2027", editionYear: 2027, sourceUrl: "https://www.usnews.com" },
  ],
  sources: [
    { fieldName: "tuitionUsd", fieldValue: "61990", sourceUrl: "https://sfs.mit.edu", sourceSection: "Cost of Attendance", status: "verified", retrievedAt: "2026-10-15T00:00:00Z", dataYear: "2025-2026", appliedToSnapshot: true, normalization: null },
    { fieldName: "acceptanceRate", fieldValue: "0.045", sourceUrl: "https://ir.mit.edu/cds", sourceSection: "CDS C1", status: "verified", retrievedAt: "2026-10-15T00:00:00Z", dataYear: "2025-2026", appliedToSnapshot: true, normalization: null },
  ],
};

export const MOCK_SPARSE_DETAIL: UniversityDetail = {
  code: "SPARSE_CC",
  name: "Shoreline Regional College",
  city: "Shoreline",
  state: "WA",
  stateName: "Washington",
  website: "https://shoreline.example.edu",
  control: "public",
  acceptanceRate: null,
  internationalStudentCount: null,
  isIvy: false,
  logoUrl: null,
  coverUrl: null,
  galleryUrls: [],
  offerings: [
    {
      studyLevel: "undergraduate",
      academicYear: "2025-2026",
      tuitionUsd: null,
      livingUsd: 12800,
      feesUsd: null,
      otherExpensesUsd: null,
      healthInsuranceUsd: null,
      annualCostUsd: null,
      minGpa4: 2.5,
      minIelts: 5.5,
      minToefl: null,
      minDuolingo: null,
      avgGpa4: null,
      avgGpaWeighted: null,
      gpaScaleType: null,
      sat25: null,
      sat75: null,
      act25: null,
      act75: null,
      applicantsTotal: null,
      admittedTotal: null,
      intlApplicants: null,
      intlAdmitted: null,
      intlAcceptanceRate: null,
      satPolicy: "not_accepted",
      actPolicy: "not_accepted",
      deadlinesJson: null,
      c7GpaImportance: null,
      c7TestImportance: null,
      c7EcImportance: null,
      satSubmitPct: null,
      actSubmitPct: null,
      gpaSubmitPct: null,
      gpaPct40: null,
      gpaPct375To399: null,
      gpaPct350To374: null,
      gpaPct325To349: null,
      gpaPct300To324: null,
      gpaPct250To299: null,
      gpaPctBelow250: null,
      intlAidPolicy: null,
      intlAidAvgUsd: null,
      intlAidRecipients: null,
      retentionRate1Yr: null,
      gradRate6Yr: null,
      fyOnCampusPct: null,
      ugOnCampusPct: null,
      housingTypes: null,
      intlHousing: null,
      housingOnlyUsd: null,
      housingDepositUsd: null,
      studentFacultyRatio: null,
      activities: null,
      campusSetting: null,
      applicationFeeUsd: 50,
      tuitionInStateUsd: null,
      majors: [
        { name: "Business Administration Transfer", cipCode: "52.0201", isStem: false, isVerified: false },
      ],
      sourceUrl: null,
      retrievedAt: null,
      sourceReliability: null,
    },
  ],
  rankings: [],
  sources: [],
};

export const MOCK_MIT_SCHOLARSHIPS: ScholarshipItem[] = [
  {
    id: "sch-1",
    universityCode: "MIT",
    universityName: "Massachusetts Institute of Technology",
    name: "MIT Need-Based Financial Aid (International)",
    provider: "MIT Student Financial Services",
    studyLevel: "undergraduate",
    amountUsd: 64500,
    coverageType: "need_based",
    minGpa4: null,
    minIelts: null,
    deadline: "2027-02-15",
    eligibilityNotes: "Xét tự động dựa trên hồ sơ CSS Profile và chứng minh thu nhập gia đình.",
    sourceUrl: "https://sfs.mit.edu",
    retrievedAt: "2026-10-15T00:00:00Z",
    sourceReliability: "official",
    kind: "need_based",
    amountType: "variable",
    amountMinUsd: 20000,
    amountMaxUsd: 85960,
    durationYears: 4,
    renewable: true,
    vnEligible: true,
    applicationMethod: "automatic",
    applyUrl: "https://sfs.mit.edu",
    meritBased: false,
    needBased: true,
  },
  {
    id: "sch-2",
    universityCode: "MIT",
    universityName: "Massachusetts Institute of Technology",
    name: "MIT Undergraduate Research Opportunities Fund",
    provider: "MIT UROP",
    studyLevel: "undergraduate",
    amountUsd: 6000,
    coverageType: "partial",
    minGpa4: 3.5,
    minIelts: 7.5,
    deadline: "2027-03-01",
    eligibilityNotes: "Hỗ trợ sinh viên đại học tham gia nghiên cứu từ năm nhất.",
    sourceUrl: "https://urop.mit.edu",
    retrievedAt: "2026-10-15T00:00:00Z",
    sourceReliability: "official",
    kind: "merit",
    amountType: "fixed",
    amountMinUsd: 6000,
    amountMaxUsd: 6000,
    durationYears: 1,
    renewable: true,
    vnEligible: true,
    applicationMethod: "separate",
    applyUrl: "https://urop.mit.edu",
    meritBased: true,
    needBased: false,
  },
];

export const MOCK_STATE_LIVING_COSTS: StateLivingCost[] = [
  { code: "MA", name: "Massachusetts", region: "northeast", index: 148.4, housingRentsIndex: 186.2, year: 2025, sourceUrl: "https://www.bea.gov", retrievedAt: "2026-09-01T00:00:00Z" },
  { code: "CA", name: "California", region: "west", index: 142.2, housingRentsIndex: 194.5, year: 2025, sourceUrl: "https://www.bea.gov", retrievedAt: "2026-09-01T00:00:00Z" },
  { code: "NY", name: "New York", region: "northeast", index: 134.8, housingRentsIndex: 168.0, year: 2025, sourceUrl: "https://www.bea.gov", retrievedAt: "2026-09-01T00:00:00Z" },
  { code: "WA", name: "Washington", region: "west", index: 118.6, housingRentsIndex: 142.3, year: 2025, sourceUrl: "https://www.bea.gov", retrievedAt: "2026-09-01T00:00:00Z" },
  { code: "NJ", name: "New Jersey", region: "northeast", index: 115.2, housingRentsIndex: 139.4, year: 2025, sourceUrl: "https://www.bea.gov", retrievedAt: "2026-09-01T00:00:00Z" },
  { code: "AZ", name: "Arizona", region: "west", index: 106.4, housingRentsIndex: 118.9, year: 2025, sourceUrl: "https://www.bea.gov", retrievedAt: "2026-09-01T00:00:00Z" },
  { code: "FL", name: "Florida", region: "south", index: 102.9, housingRentsIndex: 116.4, year: 2025, sourceUrl: "https://www.bea.gov", retrievedAt: "2026-09-01T00:00:00Z" },
  { code: "IL", name: "Illinois", region: "midwest", index: 95.8, housingRentsIndex: 99.2, year: 2025, sourceUrl: "https://www.bea.gov", retrievedAt: "2026-09-01T00:00:00Z" },
  { code: "TX", name: "Texas", region: "south", index: 93.4, housingRentsIndex: 96.7, year: 2025, sourceUrl: "https://www.bea.gov", retrievedAt: "2026-09-01T00:00:00Z" },
  { code: "MI", name: "Michigan", region: "midwest", index: 91.2, housingRentsIndex: 88.5, year: 2025, sourceUrl: "https://www.bea.gov", retrievedAt: "2026-09-01T00:00:00Z" },
  { code: "NC", name: "North Carolina", region: "south", index: 92.6, housingRentsIndex: 94.1, year: 2025, sourceUrl: "https://www.bea.gov", retrievedAt: "2026-09-01T00:00:00Z" },
  { code: "IN", name: "Indiana", region: "midwest", index: 88.5, housingRentsIndex: 82.4, year: 2025, sourceUrl: "https://www.bea.gov", retrievedAt: "2026-09-01T00:00:00Z" },
];

const SCHOOL_COSTS_BY_CODE: Record<string, Omit<SchoolCost, "scholarshipUsdPerYear" | "annualAfterScholarshipUsd" | "totalAfterScholarshipUsd" | "budget">> = {
  MIT: {
    code: "MIT",
    name: "Massachusetts Institute of Technology",
    state: "MA",
    studyLevel: "undergraduate",
    academicYear: "2025-2026",
    years: 4,
    lines: [
      { key: "tuition", amountUsd: 61990, isOneTime: false },
      { key: "living", amountUsd: 20480, isOneTime: false },
      { key: "fees", amountUsd: 420, isOneTime: false },
      { key: "other", amountUsd: 3070, isOneTime: false },
      { key: "insurance", amountUsd: 3560, isOneTime: false },
      { key: "visa", amountUsd: 185, isOneTime: true },
      { key: "sevis", amountUsd: 350, isOneTime: true },
      { key: "visa_integrity", amountUsd: 250, isOneTime: true },
    ],
    annualUsd: 89520,
    firstYearUsd: 90305,
    totalUsd: 380880,
    isComplete: true,
    missingKeys: [],
    sourceUrl: "https://sfs.mit.edu",
    retrievedAt: "2026-10-15T00:00:00Z",
  },
  PURDUE: {
    code: "PURDUE",
    name: "Purdue University",
    state: "IN",
    studyLevel: "undergraduate",
    academicYear: "2025-2026",
    years: 4,
    lines: [
      { key: "tuition", amountUsd: 28794, isOneTime: false },
      { key: "living", amountUsd: 11850, isOneTime: false },
      { key: "fees", amountUsd: 1200, isOneTime: false },
      { key: "other", amountUsd: 2100, isOneTime: false },
      { key: "insurance", amountUsd: 1680, isOneTime: false },
      { key: "visa", amountUsd: 185, isOneTime: true },
      { key: "sevis", amountUsd: 350, isOneTime: true },
      { key: "visa_integrity", amountUsd: 250, isOneTime: true },
    ],
    annualUsd: 45624,
    firstYearUsd: 46409,
    totalUsd: 194380,
    isComplete: true,
    missingKeys: [],
    sourceUrl: "https://admissions.purdue.edu",
    retrievedAt: "2026-10-10T00:00:00Z",
  },
  BERKELEY: {
    code: "BERKELEY",
    name: "University of California, Berkeley",
    state: "CA",
    studyLevel: "undergraduate",
    academicYear: "2025-2026",
    years: 4,
    lines: [
      { key: "tuition", amountUsd: 48465, isOneTime: false },
      { key: "living", amountUsd: 22400, isOneTime: false },
      { key: "fees", amountUsd: 2100, isOneTime: false },
      { key: "other", amountUsd: 2450, isOneTime: false },
      { key: "insurance", amountUsd: 4120, isOneTime: false },
      { key: "visa", amountUsd: 185, isOneTime: true },
      { key: "sevis", amountUsd: 350, isOneTime: true },
      { key: "visa_integrity", amountUsd: 250, isOneTime: true },
    ],
    annualUsd: 79535,
    firstYearUsd: 80320,
    totalUsd: 338450,
    isComplete: true,
    missingKeys: [],
    sourceUrl: "https://financialaid.berkeley.edu",
    retrievedAt: "2026-10-12T00:00:00Z",
  },
  HARVARD: {
    code: "HARVARD",
    name: "Harvard University",
    state: "MA",
    studyLevel: "undergraduate",
    academicYear: "2025-2026",
    years: 4,
    lines: [
      { key: "tuition", amountUsd: 59320, isOneTime: false },
      { key: "living", amountUsd: 21450, isOneTime: false },
      { key: "fees", amountUsd: 2500, isOneTime: false },
      { key: "other", amountUsd: 3656, isOneTime: false },
      { key: "insurance", amountUsd: 4280, isOneTime: false },
      { key: "visa", amountUsd: 185, isOneTime: true },
      { key: "sevis", amountUsd: 350, isOneTime: true },
      { key: "visa_integrity", amountUsd: 250, isOneTime: true },
    ],
    annualUsd: 91206,
    firstYearUsd: 91991,
    totalUsd: 388050,
    isComplete: true,
    missingKeys: [],
    sourceUrl: "https://college.harvard.edu",
    retrievedAt: "2026-10-14T00:00:00Z",
  },
  STANFORD: {
    code: "STANFORD",
    name: "Stanford University",
    state: "CA",
    studyLevel: "undergraduate",
    academicYear: "2025-2026",
    years: 4,
    lines: [
      { key: "tuition", amountUsd: 65127, isOneTime: false },
      { key: "living", amountUsd: 21315, isOneTime: false },
      { key: "fees", amountUsd: 950, isOneTime: false },
      { key: "other", amountUsd: 3088, isOneTime: false },
      { key: "insurance", amountUsd: 7344, isOneTime: false },
      { key: "visa", amountUsd: 185, isOneTime: true },
      { key: "sevis", amountUsd: 350, isOneTime: true },
      { key: "visa_integrity", amountUsd: 250, isOneTime: true },
    ],
    annualUsd: 97824,
    firstYearUsd: 98609,
    totalUsd: 416120,
    isComplete: true,
    missingKeys: [],
    sourceUrl: "https://financialaid.stanford.edu",
    retrievedAt: "2026-10-14T00:00:00Z",
  },
  SPARSE_CC: {
    code: "SPARSE_CC",
    name: "Shoreline Regional College",
    state: "WA",
    studyLevel: "undergraduate",
    academicYear: "2025-2026",
    years: 4,
    lines: [
      { key: "tuition", amountUsd: null, isOneTime: false },
      { key: "living", amountUsd: 12800, isOneTime: false },
      { key: "fees", amountUsd: null, isOneTime: false },
      { key: "other", amountUsd: null, isOneTime: false },
      { key: "insurance", amountUsd: null, isOneTime: false },
      { key: "visa", amountUsd: 185, isOneTime: true },
      { key: "sevis", amountUsd: 350, isOneTime: true },
      { key: "visa_integrity", amountUsd: 250, isOneTime: true },
    ],
    annualUsd: null,
    firstYearUsd: null,
    totalUsd: null,
    isComplete: false,
    missingKeys: ["tuition", "fees", "insurance"],
    sourceUrl: null,
    retrievedAt: null,
  },
};

export function filterMockUniversities(params: UniversitySearchParams): PagedResult<UniversityListItem> {
  const q = params.search?.trim().toLowerCase();
  const filtered = MOCK_UNIVERSITIES.filter((u) => {
    if (q && !`${u.name} ${u.code} ${u.city ?? ""} ${u.stateName ?? ""}`.toLowerCase().includes(q)) return false;
    if (params.state && params.state !== "all" && u.state !== params.state) return false;
    if (params.control && params.control !== "all" && u.control !== params.control) return false;
    if (params.major && params.major !== "all" && !u.majors.includes(params.major)) return false;
    if (params.isIvy && !u.isIvy) return false;
    if (params.testOptional && u.satPolicy !== "optional" && u.satPolicy !== "not_accepted") return false;
    if (params.hasIntlAid && (!u.intlAidPolicy || u.intlAidPolicy === "none")) return false;
    if (params.maxTuitionUsd !== undefined && (u.tuitionUsd == null || u.tuitionUsd > params.maxTuitionUsd)) return false;
    if (params.minTuitionUsd !== undefined && (u.tuitionUsd == null || u.tuitionUsd < params.minTuitionUsd)) return false;
    if (params.maxRank !== undefined && (u.bestRank == null || u.bestRank > params.maxRank)) return false;
    if (params.maxMinIelts !== undefined && (u.minIelts == null || u.minIelts > params.maxMinIelts)) return false;
    return true;
  });

  const sortBy = params.sortBy ?? "name";
  const sorted = [...filtered].sort((a, b) => {
    if (sortBy === "rank") return (a.bestRank ?? 9999) - (b.bestRank ?? 9999);
    if (sortBy === "tuition_asc") return (a.tuitionUsd ?? 999999) - (b.tuitionUsd ?? 999999);
    if (sortBy === "tuition_desc") return (b.tuitionUsd ?? -1) - (a.tuitionUsd ?? -1);
    if (sortBy === "acceptance_asc") return (a.acceptanceRate ?? 1) - (b.acceptanceRate ?? 1);
    return a.name.localeCompare(b.name);
  });

  const page = params.page ?? 1;
  const pageSize = params.pageSize ?? 12;
  const start = (page - 1) * pageSize;
  return {
    items: sorted.slice(start, start + pageSize),
    page,
    pageSize,
    totalCount: sorted.length,
    totalPages: Math.max(1, Math.ceil(sorted.length / pageSize)),
  };
}

export interface UniAuditRuntimeConfig {
  pageId: UniAuditPageId;
  caseId: string;
  role: UserRole | "guest";
}

function jsonResponse(body: unknown, status = 200): Response {
  return new Response(body === null ? "" : JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json" },
  });
}

export function installUniAuditFetchInterceptor(getConfig: () => UniAuditRuntimeConfig): () => void {
  if (typeof window === "undefined") return () => undefined;

  const originalFetch = window.fetch.bind(window);
  let savedTarget: TargetSchoolDto | null = null;
  let lastSavedKey = "";

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

    // Reset simulated target state when switching case
    const stateKey = `${pageId}:${caseId}`;
    if (stateKey !== lastSavedKey) {
      lastSavedKey = stateKey;
      savedTarget =
        pageId === "uni_detail" && caseId === "saved_target"
          ? {
              id: "ts-mit",
              userId: "usr-student-1",
              universityId: "uni-mit",
              universityCode: "MIT",
              universityName: "Massachusetts Institute of Technology",
              city: "Cambridge",
              state: "MA",
              website: "https://www.mit.edu",
              control: "private_nonprofit",
              acceptanceRate: 0.045,
              category: "reach",
              status: "applying",
              priority: 1,
              applicationRound: "EA",
              deadline: "2026-11-01",
              createdAt: "2026-10-01T00:00:00Z",
              estimatedAnnualCostUsd: 85960,
              isCategoryUserSelected: false,
              availableRounds: ["EA", "RD"],
            }
          : null;
    }

    // 1. Auth & shell chrome
    if (path === "/api/v1/auth/me") {
      const user = MOCK_USERS[role];
      return user ? jsonResponse(user) : jsonResponse({ message: "Chưa đăng nhập" }, 401);
    }
    if (path === "/api/v1/auth/acting") {
      return jsonResponse({ acting: false, childId: null, childName: null });
    }
    if (path === "/api/v1/policy-acceptance") {
      return jsonResponse({ mustAccept: false, hasUpdates: false, items: [] });
    }
    if (path === "/api/v1/notifications/unread-count") {
      return jsonResponse({ unreadCount: 0 });
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
      return jsonResponse({ totalUnread: 0, items: [] });
    }

    // 2. Universities list / autocomplete search
    if (path === "/api/v1/universities") {
      const search = url.searchParams.get("search") ?? undefined;
      const pageSize = Number(url.searchParams.get("pageSize") ?? "12") || 12;
      const page = Number(url.searchParams.get("page") ?? "1") || 1;
      return jsonResponse(filterMockUniversities({ search, page, pageSize }));
    }

    // 3. Target schools (for AddTargetSchoolButton)
    if (path === "/api/v1/target-schools/check") {
      const code = url.searchParams.get("universityCode");
      return jsonResponse(savedTarget && savedTarget.universityCode === code ? savedTarget : null);
    }
    if (path === "/api/v1/target-schools/defaults") {
      return jsonResponse({
        recommendedCategory: "reach",
        availableRounds: ["EA", "RD"],
        isScholarshipMandatory: false,
      } satisfies TargetSchoolDefaultsDto);
    }
    if (path === "/api/v1/target-schools" && method === "POST") {
      savedTarget = {
        id: "ts-new",
        userId: "usr-student-1",
        universityId: "uni-mit",
        universityCode: "MIT",
        universityName: "Massachusetts Institute of Technology",
        category: "reach",
        status: "considering",
        priority: 1,
        applicationRound: "EA",
        deadline: "2026-11-01",
        createdAt: new Date().toISOString(),
        availableRounds: ["EA", "RD"],
      };
      return jsonResponse(savedTarget, 201);
    }
    if (path.startsWith("/api/v1/target-schools/") && method === "DELETE") {
      savedTarget = null;
      return new Response(null, { status: 204 });
    }

    // 4. Costs API
    if (path === "/api/v1/costs/estimate") {
      const codesParam = url.searchParams.get("codes") ?? "";
      const codes = codesParam.split(",").map((s) => s.trim()).filter(Boolean);
      const scholarshipPerYear = Number(url.searchParams.get("scholarshipUsdPerYear") ?? "0") || 0;
      const budgetInput = Number(url.searchParams.get("budgetUsd") ?? "0") || 0;
      const effectiveBudget = budgetInput > 0 ? budgetInput : 55000;

      const budgetSource: BudgetCompare["source"] = budgetInput > 0 ? "input" : role === "parent" ? "parent" : "student";
      const schools: SchoolCost[] = codes
        .map((c): SchoolCost | null => {
          const base = SCHOOL_COSTS_BY_CODE[c];
          if (!base) return null;
          const annualAfter = base.annualUsd != null ? Math.max(0, base.annualUsd - scholarshipPerYear) : null;
          const totalAfter = base.totalUsd != null ? Math.max(0, base.totalUsd - scholarshipPerYear * 4) : null;
          return {
            ...base,
            scholarshipUsdPerYear: scholarshipPerYear,
            annualAfterScholarshipUsd: annualAfter,
            totalAfterScholarshipUsd: totalAfter,
            budget:
              annualAfter != null
                ? {
                    source: budgetSource,
                    annualBudgetUsd: effectiveBudget,
                    unlimited: false,
                    annualGapUsd: annualAfter - effectiveBudget,
                    totalGapUsd: (totalAfter ?? 0) - effectiveBudget * 4,
                    withinTolerance: Math.abs(annualAfter - effectiveBudget) <= 3000,
                  }
                : null,
          };
        })
        .filter((x): x is SchoolCost => x !== null);

      return jsonResponse({
        rate: { vndPerUsd: 25450, asOf: "2026-11-28", source: "Vietcombank (mô phỏng)", isFallback: false },
        schools,
        missingCodes: codes.filter((c) => !SCHOOL_COSTS_BY_CODE[c]),
        academicYearsDiffer: false,
        annualEscalationRate: 0.04,
        bankSellRateSpread: 0.015,
        undergraduateYears: 4,
      } satisfies CostEstimate);
    }

    if (path === "/api/v1/costs/state-living-costs") {
      return jsonResponse(MOCK_STATE_LIVING_COSTS);
    }

    return jsonResponse({});
  };

  return () => {
    window.fetch = originalFetch;
  };
}
