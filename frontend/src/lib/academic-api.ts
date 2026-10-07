import { apiFetch, ApiError } from "@/lib/api";
import type {
  AcademicAnalysisResponse,
  BatchTranscriptScoresRequest,
  GradeScaleConfig,
  TranscriptScore,
  UpsertTranscriptScoreItem,
} from "@/types/academic";

const DEV_FALLBACK_USER_ID = "00000000-0000-0000-0000-000000000001";

/**
 * [USAS-365] Lấy danh sách điểm các môn học trong bảng điểm của học sinh.
 */
export async function getTranscriptScores(): Promise<TranscriptScore[]> {
  try {
    return await apiFetch<TranscriptScore[]>("/api/v1/profile/academic/scores", {
      headers: {
        "X-User-Id": DEV_FALLBACK_USER_ID,
      },
    });
  } catch {
    throw new Error("Không thể tải bảng điểm. Vui lòng thử lại sau.");
  }
}

/**
 * [USAS-365] Lưu danh sách bảng điểm chi tiết (Batch Upsert).
 */
export async function saveTranscriptScores(
  items: UpsertTranscriptScoreItem[]
): Promise<TranscriptScore[]> {
  const payload: BatchTranscriptScoresRequest = { scores: items };

  try {
    return await apiFetch<TranscriptScore[]>("/api/v1/profile/academic/scores", {
      method: "POST",
      headers: {
        "X-User-Id": DEV_FALLBACK_USER_ID,
      },
      body: JSON.stringify(payload),
    });
  } catch (err) {
    const msg = err instanceof ApiError ? err.message : "Không thể lưu bảng điểm.";
    throw new Error(msg);
  }
}

/**
 * [USAS-365] Xóa một môn học trong bảng điểm.
 */
export async function deleteTranscriptScore(scoreId: string): Promise<void> {
  try {
    await apiFetch<void>(`/api/v1/profile/academic/scores/${scoreId}`, {
      method: "DELETE",
      headers: {
        "X-User-Id": DEV_FALLBACK_USER_ID,
      },
    });
  } catch {
    throw new Error("Không thể xóa môn học.");
  }
}

/**
 * [USAS-365] Chạy thuật toán phân tích điểm học thuật:
 * - Quy đổi GPA 4.0 chuẩn WES
 * - Tính Unweighted & Weighted GPA
 * - Gom nhóm STEM, Xã hội, Ngoại ngữ
 * - Phân tích xu hướng 3 năm
 */
export async function triggerAcademicAnalysis(): Promise<AcademicAnalysisResponse> {
  try {
    return await apiFetch<AcademicAnalysisResponse>("/api/v1/profile/academic/analyze", {
      method: "POST",
      headers: {
        "X-User-Id": DEV_FALLBACK_USER_ID,
      },
    });
  } catch (err) {
    const msg = err instanceof ApiError ? err.message : "Phân tích học thuật thất bại.";
    throw new Error(msg);
  }
}

/**
 * [USAS-365] Lấy kết quả phân tích học thuật mới nhất của học sinh.
 */
export async function getLatestAcademicAnalysis(): Promise<AcademicAnalysisResponse | null> {
  try {
    return await apiFetch<AcademicAnalysisResponse>("/api/v1/profile/academic/analysis", {
      headers: {
        "X-User-Id": DEV_FALLBACK_USER_ID,
      },
    });
  } catch (err) {
    if (err instanceof ApiError && err.status === 404) {
      return null;
    }
    throw new Error("Không thể lấy kết quả phân tích học thuật.");
  }
}

/**
 * [USAS-365] Lấy bảng cấu hình quy đổi điểm thang 10 sang thang 4.0 hiện hành.
 */
export async function getGradeScaleConfig(): Promise<GradeScaleConfig> {
  try {
    return await apiFetch<GradeScaleConfig>("/api/v1/profile/academic/scale-config");
  } catch {
    throw new Error("Không thể lấy cấu hình thang quy đổi điểm.");
  }
}
