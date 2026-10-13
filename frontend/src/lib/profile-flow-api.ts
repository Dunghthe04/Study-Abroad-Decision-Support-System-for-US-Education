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
    return await apiFetch<ProfileMetadataResponse>("/api/v1/profile/metadata");
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
