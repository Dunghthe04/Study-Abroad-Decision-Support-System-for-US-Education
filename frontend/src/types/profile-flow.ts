// [USAS-640] TypeScript Types cho Luồng nhập hồ sơ một cửa (Unified Profile Flow)

export interface StudyLevelOption {
  key: string;
  label: string;
  description: string;
  durationYears: number;
  defaultGradingScale: string;
  allowedGradingScales: string[];
}

export interface BudgetTierOption {
  key: string;
  label: string;
  minUsd?: number | null;
  maxUsd?: number | null;
  currency: string;
  description: string;
}

export interface FinancialNeedLevelOption {
  key: string;
  label: string;
  description: string;
}

export interface ImpactLevelOption {
  id: ImpactLevel;
  level: number;
  label: string;
  desc?: string;
}

/** Điểm thành phần của một loại chứng chỉ (kỹ năng IELTS, phần thi SAT) — từ metadata */
export interface CertificationSectionOption {
  id: string;
  label: string;
  min: number;
  max: number;
  step: number;
}

export type CertificationCategory = "english" | "standardized" | "graduate";

/** Một loại chứng chỉ trong danh mục `certifications` của app_settings profile.metadata */
export interface CertificationTypeOption {
  id: string;
  name: string;
  category: CertificationCategory;
  group: "popular" | "other" | string;
  order: number;
  aliases: string[];
  min: number | null;
  max: number | null;
  step: number | null;
  placeholder: string | null;
  scaleLabel: string | null;
  validityYears: number | null;
  sections: CertificationSectionOption[];
  sectionsHint: string | null;
  requiresSubject: boolean;
  subjectLabel: string | null;
  profileField: string | null;
}

export interface UsStateOption {
  code: string;
  name: string;
  region: string;
  isPopular: boolean;
}

export interface ProfileMetadataResponse {
  studyLevels: StudyLevelOption[];
  budgetTiers: BudgetTierOption[];
  financialNeedLevels: FinancialNeedLevelOption[];
  impactLevels: ImpactLevelOption[];
  certificationTypes: CertificationTypeOption[];
  states: UsStateOption[];
  offeringMajors: string[];
}

// ==========================================
// Draft 6 bước — đúng 1 định dạng, BE lưu nguyên (ProfileFlowDraftDto)
// ==========================================

export type CurrentEducationLevel = "middle_school" | "high_school" | "college_university" | "master";
export type TargetSchoolType = "all" | "public" | "private";
export type EnglishTestType = "IELTS" | "TOEFL" | "Duolingo" | "none";
export type BudgetTierId = "tier_under_20k" | "tier_20k_35k" | "tier_35k_50k" | "tier_50k_70k" | "tier_above_70k";
export type ScholarshipNeed = "mandatory" | "preferred" | "optional";
/** Phạm vi ảnh hưởng — DB lưu impact_level 1..5 theo thứ tự này. */
export type ImpactLevel = "school" | "district" | "province" | "national" | "international";
export type ActivityRole = "founder" | "leader" | "vice_leader" | "member";
/** Hệ chương trình cấp 3 — khớp EducationSystems phía BE */
export type EducationSystem = "standard" | "specialized" | "dual_degree" | "international" | "private" | "other";
/** Ai chi trả chính — khớp ProfileConstants.FundingSources phía BE */
export type FundingSource = "family_support" | "personal_savings" | "bank_loan" | "scholarship" | "other";

export interface GoalDraft {
  currentEducationLevel?: CurrentEducationLevel | null;
  currentGrade?: string | null;
  /** Chỉ hỏi khi currentEducationLevel là high_school */
  educationSystem?: EducationSystem | null;
  currentSchool?: string | null;
  expectedGraduationYear?: number | null;
  targetDegreeLevel?: string | null;
  targetMajor?: string | null;
  /** Kỳ nhập học dạng "Fall 2027" — khớp câu B1.4 bên chat */
  targetIntake?: string | null;
  targetStates?: string[] | null;
  targetSchoolType?: TargetSchoolType | null;
}

export interface SubjectDraft {
  subjectName: string;
  semester1Score?: number | null;
  semester2Score?: number | null;
  /** [UC-12] Lý do ô cần kiểm tra (source_image | outlier | overwrote | duplicate); null = đã kiểm tra / nhập tay */
  semester1Review?: string | null;
  semester2Review?: string | null;
}

export interface AcademicYearDraft {
  gradeName: string; // "Lớp 10", "Năm 1", ...
  subjects: SubjectDraft[];
}

/** File bảng điểm nháp — nằm ngoài draft (BE: DraftTranscriptFileDto, không có đường dẫn lưu trữ). */
export interface DraftTranscriptFile {
  originalFileName: string;
  fileSizeBytes: number;
  contentType: string;
  uploadedAt: string;
}

/** Cách nhập GPA bước 2: GPA tổng hoặc tính từ điểm môn (học bạ). BE: ProfileConstants.GpaModes */
export type GpaMode = "overall" | "transcript";

export interface TranscriptDraft {
  academicYears: AcademicYearDraft[];
  calculatedGpa4Scale?: number | null;
  manualGpa10Scale?: number | null;
  gpaMode?: GpaMode | null;
}

/** Loại "Khác" — học sinh tự nhập tên chứng chỉ */
export const OTHER_CERT_TYPE = "other";

/** Một chứng chỉ học sinh khai (BE: FlowCertificationItemDto). Mỗi môn AP là một mục. */
export interface CertificationItem {
  id: string;
  category: CertificationCategory;
  /** id trong metadata certifications, hoặc "other" */
  type: string;
  customName?: string | null;
  subject?: string | null;
  overall?: number | null;
  /** thang điểm tự nhập (loại "Khác") hoặc điểm dạng chữ (A-Level) */
  scaleText?: string | null;
  /** điểm thành phần theo id section trong metadata */
  sections?: Record<string, number | null> | null;
  testDate?: string | null; // "yyyy-MM"
}

/** Nháp kiểu cũ (trước khi có danh sách thẻ) — chỉ dùng để chuyển đổi */
export interface LegacyOtherTestDraft {
  testType: "SAT" | "ACT" | "AP";
  score?: number | null;
}

export interface CertificationDraft {
  certifications: CertificationItem[];
  englishPlanned: boolean;
  englishPlannedDate?: string | null; // "yyyy-MM"
}

/** Bước 3 kiểu cũ còn nằm trong nháp đã lưu (server hoặc localStorage) */
export interface LegacyCertificationDraft {
  englishTestType?: EnglishTestType | null;
  englishOverallScore?: number | null;
  englishTestDate?: string | null;
  otherTests?: LegacyOtherTestDraft[] | null;
}

export interface FinancialDraft {
  annualBudgetTier?: BudgetTierId | null;
  needFinancialAid?: ScholarshipNeed | null;
  fundingSource?: FundingSource | null;
}

export interface ActivityDraft {
  id?: string | null;
  name: string;
  category: "activity" | "achievement";
  impactLevel?: ImpactLevel | null;
  // Hoạt động
  role?: ActivityRole | null;
  startDate?: string | null; // "MM/yyyy"
  endDate?: string | null; // "MM/yyyy"
  isOngoing?: boolean | null;
  durationMonths?: number | null;
  // Giải thưởng
  year?: number | null;
}

export interface ProfileFlowDraft {
  step1: GoalDraft;
  step2: TranscriptDraft;
  step3: CertificationDraft;
  step4: FinancialDraft;
  step5: {
    activities: ActivityDraft[];
  };
}

export interface ProfileFlowState {
  currentStep: number;
  completedSteps: number[];
  lastCompletedStep: number;
  draft: ProfileFlowDraft;
  draftTranscript?: DraftTranscriptFile | null;
  lastUpdatedAt?: string | null;
  hasCommittedProfile: boolean;
}

export interface ExtractedSubjectScore {
  subject: string;
  score: number;
  rawScore?: string;
  credits?: number;
  needsReview?: boolean;
  reviewReason?: string | null;
}

export interface ExtractedTerm {
  termOrder?: number;
  termName: string;
  scores: ExtractedSubjectScore[];
}

export interface UploadDraftTranscriptResponse {
  draftTranscript: DraftTranscriptFile;
  extractedTerms: ExtractedTerm[];
  calculatedGpa4?: number | null;
  ocrMessage?: string | null;
  warnings?: string[] | null;
}

export interface CommitProfileFlowResponse {
  success?: boolean;
  succeeded?: boolean;
  message: string;
  profileId: string;
  targetLevel?: string;
  intendedMajor?: string | null;
  overallGpa?: number | null;
  extracurricularScore?: number | null;
  budgetMinUsd?: number | null;
  budgetMaxUsd?: number | null;
  targetSchoolType?: string | null;
  transcriptFileUrl?: string | null;
  committedAtUtc?: string;
}
