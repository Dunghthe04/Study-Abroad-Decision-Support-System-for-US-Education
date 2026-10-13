// [USAS-640] API Client cho Luồng nhập hồ sơ một cửa 6 bước

import { apiFetch, ApiError } from "@/lib/api";
import type {
  ProfileMetadataResponse,
  ProfileFlowState,
  ProfileFlowDraft,
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

    const offeringMajors: string[] = (raw.usMajors ?? raw.offeringMajors ?? []).map((m) =>
      typeof m === "string" ? m : `${m.name}${m.englishName ? ` (${m.englishName})` : ""}`
    );

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
      return await apiFetch<ProfileFlowState>("/api/v1/profile/flow/state");
    } catch (err: unknown) {
      if (err instanceof ApiError && (err.status === 401 || err.status === 404)) {
        return null;
      }
      throw err;
    }
  },

  // Tự động lưu nháp
  saveDraft: async (currentStep: number, draft: ProfileFlowDraft): Promise<ProfileFlowState> => {
    return await apiFetch<ProfileFlowState>("/api/v1/profile/flow/draft", {
      method: "PUT",
      body: JSON.stringify({
        currentStep,
        draft,
        flowState: draft,
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
    return await apiFetch<CommitProfileFlowResponse>("/api/v1/profile/flow/commit", {
      method: "POST",
      body: finalDraft ? JSON.stringify({ finalDraft }) : "{}",
    });
  },

  // Xóa bản nháp làm lại từ đầu
  clearDraft: async (): Promise<void> => {
    await apiFetch<void>("/api/v1/profile/flow/draft", {
      method: "DELETE",
    });
  },
};
