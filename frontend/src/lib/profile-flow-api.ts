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
      scholarshipNeeds?: { id: string; label: string; subLabel?: string }[];
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
      key: b.key ?? b.id ?? "",
      label: b.label,
      minUsd: b.minUsd ?? null,
      maxUsd: b.maxUsd ?? null,
      currency: "USD",
      description: b.description ?? "",
    }));

    return {
      studyLevels,
      budgetTiers,
      financialNeedLevels: (raw.scholarshipNeeds ?? []).map((n) => ({
        key: n.id,
        label: n.label,
        description: n.subLabel ?? "",
      })),
      impactLevels: raw.impactLevels ?? [],
      certificationTypes: raw.certificationTypes ?? raw.certifications ?? [],
      states,
      offeringMajors,
    };
  },

  // Lấy trạng thái luồng: nháp đang làm dở, hoặc draft dựng lại từ hồ sơ đã lưu
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

  // Lưu nháp — gửi nguyên draft, BE lưu đúng định dạng này
  saveDraft: async (
    currentStep: number,
    completedSteps: number[],
    draft: ProfileFlowDraft
  ): Promise<ProfileFlowState> => {
    return await apiFetch<ProfileFlowState>("/api/v1/profile/flow/draft", {
      method: "PUT",
      body: JSON.stringify({ currentStep, completedSteps, draft }),
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
      // 413/429 có thể do Kestrel hoặc nginx trả, không kèm JSON
      const fallback =
        res.status === 429
          ? "Bạn tải bảng điểm quá nhiều lần hoặc đang có một tệp được xử lý. Vui lòng đợi vài phút rồi thử lại."
          : res.status === 413
            ? "Tệp quá lớn. Bảng điểm tối đa 10MB."
            : "Không thể tải lên bảng điểm.";
      throw new Error(err?.error ?? err?.message ?? fallback);
    }

    return (await res.json()) as UploadDraftTranscriptResponse;
  },

  // Xác nhận lưu chính thức (Commit) ở bước 6 — BE validate rồi ghi vào các bảng hồ sơ
  commitFlow: async (draft: ProfileFlowDraft): Promise<CommitProfileFlowResponse> => {
    return await apiFetch<CommitProfileFlowResponse>("/api/v1/profile/flow/commit", {
      method: "POST",
      body: JSON.stringify({ draft }),
    });
  },

  // Xóa bản nháp làm lại từ đầu
  clearDraft: async (): Promise<void> => {
    await apiFetch<void>("/api/v1/profile/flow/draft", {
      method: "DELETE",
    });
  },
};
