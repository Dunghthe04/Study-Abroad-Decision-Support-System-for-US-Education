// [USAS-640] API Client cho Luồng nhập hồ sơ một cửa 6 bước

import { apiFetch, ApiError } from "@/lib/api";
import type {
  ProfileMetadataResponse,
  ProfileFlowState,
  ProfileFlowDraft,
  GoalDraft,
  TranscriptDraft,
  CertificationDraft,
  FinancialDraft,
  ActivityDraft,
  UploadDraftTranscriptResponse,
  CommitProfileFlowResponse,
} from "@/types/profile-flow";

export const profileFlowApi = {
  // Lấy siêu dữ liệu động (bậc học, mức tiền, 51 bang, 366 ngành)
  getMetadata: async (): Promise<ProfileMetadataResponse> => {
    interface RawMajorItem {
      id?: string;
      name?: string;
      englishName?: string;
      category?: string;
    }

    interface RawStateItem {
      code: string;
      name: string;
      region?: string;
      isPopular?: boolean;
    }

    interface RawStudyLevelItem {
      key?: string;
      id?: string;
      label?: string;
      name?: string;
      description?: string;
      subName?: string;
      durationYears?: number;
      defaultGradeScale?: string;
      allowedGradingScales?: string[];
    }

    interface RawBudgetTierItem {
      key?: string;
      id?: string;
      label: string;
      minUsd?: number | null;
      maxUsd?: number | null;
      description?: string;
    }

    interface RawMetadataPayload {
      popularStateCodes?: string[];
      usStates?: RawStateItem[];
      states?: RawStateItem[];
      usMajors?: (RawMajorItem | string)[];
      offeringMajors?: (RawMajorItem | string)[];
      studyLevels?: RawStudyLevelItem[];
      budgetTiers?: RawBudgetTierItem[];
      financialNeedLevels?: ProfileMetadataResponse["financialNeedLevels"];
      scholarshipNeeds?: ProfileMetadataResponse["financialNeedLevels"];
      impactLevels?: ProfileMetadataResponse["impactLevels"];
      certificationTypes?: ProfileMetadataResponse["certificationTypes"];
      certifications?: ProfileMetadataResponse["certificationTypes"];
    }

    const raw = await apiFetch<RawMetadataPayload>("/api/v1/profile/metadata");
    const popularCodes = new Set(raw.popularStateCodes ?? []);

    const states = (raw.usStates ?? raw.states ?? []).map((s) => ({
      code: s.code,
      name: s.name,
      region: s.region ?? "",
      isPopular: Boolean(s.isPopular || popularCodes.has(s.code)),
    }));

    const offeringMajorsSet = new Set<string>();
    for (const m of (raw.usMajors ?? raw.offeringMajors ?? [])) {
      const name = typeof m === "string" ? m : (m.englishName || m.name || "");
      if (name.trim()) offeringMajorsSet.add(name.trim());
    }
    const offeringMajors = Array.from(offeringMajorsSet).sort((a, b) => a.localeCompare(b));

    const studyLevels = (raw.studyLevels ?? []).map((l) => ({
      key: l.key ?? l.id ?? "undergraduate",
      label: l.label ?? l.name ?? "Đại học",
      description: l.description ?? l.subName ?? "",
      durationYears: l.durationYears ?? 4,
      defaultGradingScale: l.defaultGradeScale ?? "scale_10",
      allowedGradingScales: l.allowedGradingScales ?? [],
    }));

    const budgetTiers = (raw.budgetTiers ?? []).map((b) => ({
      key: b.key ?? b.id ?? "35k_50k",
      label: b.label,
      minUsd: b.minUsd ?? null,
      maxUsd: b.maxUsd ?? null,
      currency: "USD",
      description: b.description ?? "",
    }));

    return {
      studyLevels,
      budgetTiers,
      financialNeedLevels: raw.financialNeedLevels ?? raw.scholarshipNeeds ?? [],
      impactLevels: raw.impactLevels ?? [],
      certificationTypes: raw.certificationTypes ?? raw.certifications ?? [],
      states,
      offeringMajors,
    };
  },

  // Lấy trạng thái nháp hiện tại của người dùng
  getFlowState: async (): Promise<ProfileFlowState | null> => {
    try {
      const state = await apiFetch<ProfileFlowState>("/api/v1/profile/flow/state");
      if (state) {
        if (!state.draft && state.stepData) {
          state.draft = mapStepDataToDraft(state.stepData) as ProfileFlowDraft;
        } else if (state.draft && state.stepData) {
          const fallback = mapStepDataToDraft(state.stepData);
          state.draft = {
            ...state.draft,
            step1: { ...fallback.step1, ...state.draft.step1 },
            step2: { ...fallback.step2, ...state.draft.step2 },
            step3: { ...fallback.step3, ...state.draft.step3 },
            step4: { ...fallback.step4, ...state.draft.step4 },
            step5: { ...fallback.step5, ...state.draft.step5 },
          };
        }
      }
      return state;
    } catch (err: unknown) {
      if (err instanceof ApiError && (err.status === 401 || err.status === 404)) {
        return null;
      }
      throw err;
    }
  },

  // Tự động lưu nháp (đồng bộ cả draft và stepData chuẩn)
  saveDraft: async (currentStep: number, draft: ProfileFlowDraft): Promise<ProfileFlowState> => {
    const stepData = mapDraftToStepData(draft);
    return await apiFetch<ProfileFlowState>("/api/v1/profile/flow/draft", {
      method: "PUT",
      body: JSON.stringify({
        currentStep,
        draft,
        flowState: draft,
        stepData,
      }),
    });
  },

  // Tải lên ảnh / file PDF bảng điểm và tự động chạy Ollama Vision OCR
  uploadDraftTranscript: async (files: File[]): Promise<UploadDraftTranscriptResponse> => {
    const formData = new FormData();
    for (const f of files) {
      formData.append("files", f);
    }
    const res = await fetch("/api/v1/profile/flow/draft/upload-transcript", {
      method: "POST",
      body: formData,
      credentials: "include",
    });

    if (!res.ok) {
      const err = await res.json().catch(() => null);
      throw new Error(err?.error ?? err?.message ?? "Không thể tải lên bảng điểm.");
    }

    return (await res.json()) as UploadDraftTranscriptResponse;
  },

  // Xác nhận lưu chính thức (Commit) ở bước 6
  commitFlow: async (finalDraft?: ProfileFlowDraft): Promise<CommitProfileFlowResponse> => {
    const stepData = finalDraft ? mapDraftToStepData(finalDraft) : undefined;
    return await apiFetch<CommitProfileFlowResponse>("/api/v1/profile/flow/commit", {
      method: "POST",
      body: JSON.stringify({
        finalDraft,
        stepData,
      }),
    });
  },

  // Xóa bản nháp làm lại từ đầu
  clearDraft: async (): Promise<void> => {
    await apiFetch<void>("/api/v1/profile/flow/draft", {
      method: "DELETE",
    });
  },
};

// ==========================================
// Chuyển đổi dữ liệu 2 chiều Draft <-> StepData
// ==========================================

export function mapDraftToStepData(draft: ProfileFlowDraft) {
  // 1. Goal
  const step1 = {
    targetLevel: draft.step1?.targetDegreeLevel ?? "undergraduate",
    currentEducationLevel: draft.step1?.currentEducationLevel ?? null,
    currentSchool: draft.step1?.currentSchool ?? null,
    currentGrade: draft.step1?.currentGrade ?? null,
    graduationYear: draft.step1?.expectedGraduationYear ?? null,
    intendedMajor: draft.step1?.targetMajor ?? null,
    preferredStates: draft.step1?.targetStates ?? [],
    institutionType: draft.step1?.targetSchoolType ?? null,
  };

  // 2. Transcript
  const terms: Array<{
    termOrder: number;
    termName: string;
    scores: Array<{
      subject: string;
      score: number;
      rawScore: string;
      credits: number;
    }>;
  }> = [];

  let termOrder = 1;
  for (const year of (draft.step2?.academicYears ?? [])) {
    const sem1Scores = (year.subjects ?? [])
      .filter((s) => s.semester1Score !== null && s.semester1Score !== undefined && !isNaN(Number(s.semester1Score)))
      .map((s) => ({
        subject: s.subjectName,
        score: Number(s.semester1Score),
        rawScore: s.semester1Score!.toString(),
        credits: 1,
      }));

    if (sem1Scores.length > 0) {
      terms.push({
        termOrder: termOrder++,
        termName: `${year.gradeName} - HK1`,
        scores: sem1Scores,
      });
    }

    const sem2Scores = (year.subjects ?? [])
      .filter((s) => s.semester2Score !== null && s.semester2Score !== undefined && !isNaN(Number(s.semester2Score)))
      .map((s) => ({
        subject: s.subjectName,
        score: Number(s.semester2Score),
        rawScore: s.semester2Score!.toString(),
        credits: 1,
      }));

    if (sem2Scores.length > 0) {
      terms.push({
        termOrder: termOrder++,
        termName: `${year.gradeName} - HK2`,
        scores: sem2Scores,
      });
    }
  }

  const step2 = {
    educationSystem: draft.step1?.currentEducationLevel ?? "Việt Nam",
    gradeScale: "10",
    calculatedGpa4: draft.step2?.calculatedGpa4Scale ?? null,
    terms,
  };

  // 3. Certifications
  const engType = draft.step3?.englishTestType;
  const engScore = draft.step3?.englishOverallScore ?? null;
  const otherTests = draft.step3?.otherTests ?? [];

  const satTest = otherTests.find((t) => t.testType === "SAT");
  const actTest = otherTests.find((t) => t.testType === "ACT");
  const greTest = otherTests.find((t) => t.testType === "GRE");
  const gmatTest = otherTests.find((t) => t.testType === "GMAT");
  const apTest = otherTests.find((t) => t.testType === "AP");

  const step3 = {
    englishTestType: engType && engType !== "none" ? engType : null,
    ielts: engType === "IELTS" ? engScore : null,
    toefl: engType === "TOEFL" ? engScore : null,
    duolingo: engType === "Duolingo" ? engScore : null,
    englishExamDate: draft.step3?.englishTestDate ?? null,
    selectedStandardizedTests: otherTests.map((t) => t.testType.toLowerCase()),
    sat: satTest?.score ?? null,
    act: actTest?.score ?? null,
    ap: apTest?.score ?? null,
    gre: greTest?.score ?? null,
    gmat: gmatTest?.score ?? null,
  };

  // 4. Financial
  let budgetTierId = draft.step4?.annualBudgetTier ?? null;
  if (budgetTierId && !budgetTierId.startsWith("tier_")) {
    budgetTierId = `tier_${budgetTierId}`;
  }

  const step4 = {
    budgetTierId,
    annualBudgetUsd: draft.step4?.annualBudgetUsd ?? null,
    scholarshipNeedId: draft.step4?.needFinancialAid ?? null,
    financialNotes: draft.step4?.financialNotes ?? null,
  };

  // 5. Extracurricular
  const impactLevelMap: Record<string, number> = {
    school: 1,
    province: 2,
    national: 3,
    international: 4,
  };

  const items = (draft.step5?.activities ?? []).map((act) => ({
    type: act.category === "achievement" ? "award" : "extracurricular",
    title: act.name,
    role: act.role ?? null,
    impactLevel: impactLevelMap[act.impactLevel] ?? 1,
    durationMonths: act.durationYears ? Math.round(act.durationYears * 12) : 12,
    year: act.year ? act.year.toString() : null,
    description: act.role ? `Vai trò: ${act.role}` : null,
  }));

  const step5 = {
    items,
  };

  return {
    step1,
    step2,
    step3,
    step4,
    step5,
  };
}

export interface FlowStep1Payload {
  targetLevel?: string;
  currentEducationLevel?: string;
  currentSchool?: string;
  currentGrade?: string;
  graduationYear?: number;
  intendedMajor?: string;
  preferredStates?: string[];
  institutionType?: string;
}

export interface FlowStep2Payload {
  educationSystem?: string;
  gradeScale?: string;
  calculatedGpa4?: number | null;
  terms?: Array<{
    termOrder: number;
    termName: string;
    scores: Array<{
      subject: string;
      score: number;
      rawScore: string;
      credits: number;
    }>;
  }>;
}

export interface FlowStep3Payload {
  englishTestType?: string | null;
  ielts?: number | null;
  toefl?: number | null;
  duolingo?: number | null;
  englishExamDate?: string | null;
  selectedStandardizedTests?: string[];
  sat?: number | null;
  act?: number | null;
  ap?: number | null;
  gre?: number | null;
  gmat?: number | null;
}

export interface FlowStep4Payload {
  budgetTierId?: string | null;
  annualBudgetUsd?: number | null;
  scholarshipNeedId?: string | null;
  financialNotes?: string | null;
}

export interface FlowActivityItemPayload {
  id?: string;
  type?: string;
  title?: string;
  role?: string;
  organization?: string;
  impactLevel?: number;
  durationMonths?: number;
  year?: string;
  description?: string;
}

export interface FlowStep5Payload {
  items?: FlowActivityItemPayload[];
}

export interface ProfileFlowStepDataPayload {
  step1?: FlowStep1Payload;
  step2?: FlowStep2Payload;
  step3?: FlowStep3Payload;
  step4?: FlowStep4Payload;
  step5?: FlowStep5Payload;
}

export function mapStepDataToDraft(stepData?: ProfileFlowStepDataPayload | null): Partial<ProfileFlowDraft> {
  if (!stepData) return {};

  const step1: Partial<GoalDraft> = {};
  if (stepData.step1) {
    step1.targetDegreeLevel = stepData.step1.targetLevel ?? "undergraduate";
    step1.currentEducationLevel = stepData.step1.currentEducationLevel;
    step1.currentSchool = stepData.step1.currentSchool;
    step1.currentGrade = stepData.step1.currentGrade;
    step1.expectedGraduationYear = stepData.step1.graduationYear;
    step1.targetMajor = stepData.step1.intendedMajor;
    step1.targetStates = stepData.step1.preferredStates ?? [];
    step1.targetSchoolType = stepData.step1.institutionType ?? "all";
  }

  const step2: Partial<TranscriptDraft> = {};
  if (stepData.step2) {
    step2.calculatedGpa4Scale = stepData.step2.calculatedGpa4 ?? null;
    if (stepData.step2.calculatedGpa4) {
      step2.manualGpa10Scale = Math.round(((Number(stepData.step2.calculatedGpa4) / 4.0) * 10) * 100) / 100;
    }
  }

  const step3: Partial<CertificationDraft> = { otherTests: [] };
  if (stepData.step3) {
    if (stepData.step3.ielts) {
      step3.englishTestType = "IELTS";
      step3.englishOverallScore = Number(stepData.step3.ielts);
    } else if (stepData.step3.toefl) {
      step3.englishTestType = "TOEFL";
      step3.englishOverallScore = Number(stepData.step3.toefl);
    } else if (stepData.step3.duolingo) {
      step3.englishTestType = "Duolingo";
      step3.englishOverallScore = Number(stepData.step3.duolingo);
    }

    if (stepData.step3.sat) step3.otherTests!.push({ testType: "SAT", score: Number(stepData.step3.sat) });
    if (stepData.step3.act) step3.otherTests!.push({ testType: "ACT", score: Number(stepData.step3.act) });
    if (stepData.step3.ap) step3.otherTests!.push({ testType: "AP", score: Number(stepData.step3.ap) });
    if (stepData.step3.gre) step3.otherTests!.push({ testType: "GRE", score: Number(stepData.step3.gre) });
    if (stepData.step3.gmat) step3.otherTests!.push({ testType: "GMAT", score: Number(stepData.step3.gmat) });
  }

  const step4: Partial<FinancialDraft> = {};
  if (stepData.step4) {
    step4.annualBudgetTier = stepData.step4.budgetTierId?.replace(/^tier_/, "") ?? undefined;
    step4.annualBudgetUsd = stepData.step4.annualBudgetUsd ?? null;
    step4.needFinancialAid = stepData.step4.scholarshipNeedId ?? undefined;
    step4.financialNotes = stepData.step4.financialNotes ?? undefined;
  }

  const step5: { activities: ActivityDraft[] } = { activities: [] };
  if (stepData.step5?.items) {
    const reverseImpactMap: Record<number, string> = {
      1: "school",
      2: "province",
      3: "national",
      4: "international",
    };

    step5.activities = stepData.step5.items.map((item) => ({
      id: item.id ? String(item.id) : undefined,
      name: item.title ?? "",
      category: item.type === "award" ? "achievement" : "activity",
      role: item.role ?? undefined,
      durationYears: item.durationMonths ? Math.round(item.durationMonths / 12) : 1,
      impactLevel: reverseImpactMap[item.impactLevel ?? 1] ?? "school",
      year: item.year ? Number(item.year) : undefined,
    }));
  }

  return {
    step1: step1 as GoalDraft,
    step2: step2 as TranscriptDraft,
    step3: step3 as CertificationDraft,
    step4: step4 as FinancialDraft,
    step5,
  };
}
