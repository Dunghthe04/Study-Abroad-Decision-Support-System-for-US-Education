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

export interface CertificationTypeOption {
  key: string;
  label: string;
  maxScore: number;
  minScore: number;
  scoreType: string;
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

export interface GoalDraft {
  currentEducationLevel?: CurrentEducationLevel | null;
  currentGrade?: string | null;
  currentSchool?: string | null;
  expectedGraduationYear?: number | null;
  targetDegreeLevel?: string | null;
  targetMajor?: string | null;
  targetStates?: string[] | null;
  targetSchoolType?: TargetSchoolType | null;
}

export interface SubjectDraft {
  subjectName: string;
  semester1Score?: number | null;
  semester2Score?: number | null;
}

export interface AcademicYearDraft {
  gradeName: string; // "Lớp 10", "Năm 1", ...
  subjects: SubjectDraft[];
}

/** File bảng điểm nháp — nằm ngoài draft (BE: DraftTranscriptInfoDto). */
export interface DraftTranscriptFile {
  originalFileName: string;
  storedFileName: string;
  relativePath: string;
  fileSizeBytes: number;
  contentType: string;
  uploadedAt: string;
}

export interface TranscriptDraft {
  academicYears: AcademicYearDraft[];
  calculatedGpa4Scale?: number | null;
  manualGpa10Scale?: number | null;
}

export interface OtherTestDraft {
  testType: "SAT" | "ACT" | "AP";
  score?: number | null;
}

export interface CertificationDraft {
  englishTestType?: EnglishTestType | null;
  englishOverallScore?: number | null;
  englishTestDate?: string | null; // "yyyy-MM"
  otherTests: OtherTestDraft[];
}

export interface FinancialDraft {
  annualBudgetTier?: BudgetTierId | null;
  needFinancialAid?: ScholarshipNeed | null;
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
