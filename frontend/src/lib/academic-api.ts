import type {
  AcademicAnalysisResponse,
  BatchTranscriptScoresRequest,
  GradeScaleConfig,
  TranscriptScore,
  UpsertTranscriptScoreItem,
} from "@/types/academic";

const API_BASE = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5080";

/**
 * [USAS-365] Lấy danh sách điểm các môn học trong bảng điểm của học sinh.
 */
export async function getTranscriptScores(): Promise<TranscriptScore[]> {
  const res = await fetch(`${API_BASE}/api/v1/profile/academic/scores`, {
    headers: {
      "Content-Type": "application/json",
      "X-User-Id": "00000000-0000-0000-0000-000000000001",
    },
    cache: "no-store",
  });

  if (!res.ok) {
    throw new Error("Không thể tải bảng điểm. Vui lòng thử lại sau.");
  }

  return res.json();
}

/**
 * [USAS-365] Lưu danh sách bảng điểm chi tiết (Batch Upsert).
 */
export async function saveTranscriptScores(
  items: UpsertTranscriptScoreItem[]
): Promise<TranscriptScore[]> {
  const payload: BatchTranscriptScoresRequest = { scores: items };

  const res = await fetch(`${API_BASE}/api/v1/profile/academic/scores`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      "X-User-Id": "00000000-0000-0000-0000-000000000001",
    },
    body: JSON.stringify(payload),
  });

  if (!res.ok) {
    const errorData = await res.json().catch(() => null);
    throw new Error(errorData?.detail ?? "Không thể lưu bảng điểm.");
  }

  return res.json();
}

/**
 * [USAS-365] Xóa một môn học trong bảng điểm.
 */
export async function deleteTranscriptScore(scoreId: string): Promise<void> {
  const res = await fetch(`${API_BASE}/api/v1/profile/academic/scores/${scoreId}`, {
    method: "DELETE",
    headers: {
      "X-User-Id": "00000000-0000-0000-0000-000000000001",
    },
  });

  if (!res.ok) {
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
  const res = await fetch(`${API_BASE}/api/v1/profile/academic/analyze`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      "X-User-Id": "00000000-0000-0000-0000-000000000001",
    },
  });

  if (!res.ok) {
    const errorData = await res.json().catch(() => null);
    throw new Error(errorData?.detail ?? "Phân tích học thuật thất bại.");
  }

  return res.json();
}

/**
 * [USAS-365] Lấy kết quả phân tích học thuật mới nhất của học sinh.
 */
export async function getLatestAcademicAnalysis(): Promise<AcademicAnalysisResponse | null> {
  const res = await fetch(`${API_BASE}/api/v1/profile/academic/analysis`, {
    headers: {
      "Content-Type": "application/json",
      "X-User-Id": "00000000-0000-0000-0000-000000000001",
    },
    cache: "no-store",
  });

  if (res.status === 404) {
    return null;
  }

  if (!res.ok) {
    throw new Error("Không thể lấy kết quả phân tích học thuật.");
  }

  return res.json();
}

/**
 * [USAS-365] Lấy bảng cấu hình quy đổi điểm thang 10 sang thang 4.0 hiện hành.
 */
export async function getGradeScaleConfig(): Promise<GradeScaleConfig> {
  const res = await fetch(`${API_BASE}/api/v1/profile/academic/scale-config`, {
    headers: {
      "Content-Type": "application/json",
    },
    cache: "no-store",
  });

  if (!res.ok) {
    throw new Error("Không thể lấy cấu hình thang quy đổi điểm.");
  }

  return res.json();
}
