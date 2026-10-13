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
  key: string;
  label: string;
  weight: number;
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

export interface GoalDraft {
  currentEducationLevel?: string;
  currentGrade?: string;
  currentSchool?: string;
  expectedGraduationYear?: number;
  targetDegreeLevel?: string;
  targetMajor?: string;
  targetStates?: string[];
  targetSchoolType?: string; // "all" | "public" | "private"
}

export interface SubjectDraft {
  subjectName: string;
  semester1Score?: number | null;
  semester2Score?: number | null;
  finalScore?: number | null;
}

export interface AcademicYearDraft {
  gradeName: string; // "Lớp 10", "Lớp 11", "Lớp 12", etc.
  yearName?: string;
  subjects: SubjectDraft[];
}

export interface DraftTranscriptFile {
  fileId: string;
  originalFileName: string;
  fileUrl: string;
  fileSize: number;
  uploadedAtUtc: string;
}

export interface TranscriptDraft {
  draftTranscriptFile?: DraftTranscriptFile | null;
  academicYears: AcademicYearDraft[];
  calculatedGpa4Scale?: number | null;
  manualGpa10Scale?: number | null;
}

export interface OtherTestDraft {
  testType: string; // "SAT" | "ACT" | "AP"
  score?: number | null;
  details?: string;
}

export interface CertificationDraft {
  englishTestType?: string; // "IELTS" | "TOEFL" | "Duolingo" | "none"
  englishOverallScore?: number | null;
  englishTestDate?: string;
  otherTests: OtherTestDraft[];
}

export interface FinancialDraft {
  annualBudgetTier?: string;
  annualBudgetUsd?: number | null;
  needFinancialAid?: string; // "mandatory" | "preferred" | "not_needed"
  financialNotes?: string;
}

export interface ActivityDraft {
  id?: string;
  name: string;
  category: "activity" | "achievement";
  role?: string;
  durationYears?: number;
  startDate?: string;
  endDate?: string;
  isOngoing?: boolean;
  impactLevel: string; // "school" | "province" | "national" | "international"
  year?: number;
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
  lastCompletedStep: number;
  updatedAtUtc?: string | null;
  draft?: ProfileFlowDraft | null;
  stepData?: Record<string, unknown>;
  completedSteps?: number[];
  hasCommittedProfile?: boolean;
}

export interface OcrExtractedSubject {
  subjectName: string;
  semester1Score?: number | null;
  semester2Score?: number | null;
  finalScore?: number | null;
}

export interface OcrExtractionResult {
  detectedGrade?: string | null;
  detectedTerm?: string | null;
  detectedGradingScale?: string | null;
  subjects: OcrExtractedSubject[];
  summaryGpa10?: number | null;
  calculatedGpa4?: number | null;
  confidenceScore: number;
  warnings?: string[] | null;
}

export interface UploadDraftTranscriptResponse {
  draftFile: DraftTranscriptFile;
  ocrExtraction?: OcrExtractionResult | null;
}

export interface CommitProfileFlowResponse {
  success?: boolean;
  succeeded?: boolean;
  message: string;
  profileId: string;
  committedAtUtc?: string;
}
