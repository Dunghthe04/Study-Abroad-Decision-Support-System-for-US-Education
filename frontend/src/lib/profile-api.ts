// [USAS-364] Client API gọi các endpoint Quản lý Hồ sơ tài chính và Hoạt động ngoại khóa

import { apiFetch } from "@/lib/api";
import type {
  FinancialProfileDto,
  SaveFinancialProfileRequest,
  ExtracurricularActivityDto,
  CreateExtracurricularRequest,
  UpdateExtracurricularRequest,
  StudentAchievementDto,
  CreateAchievementRequest,
  StudentProfileSummaryDto,
} from "@/types/profile";

// Helper lấy headers chứa UserId (chống IDOR & hỗ trợ session phiên)
function getAuthHeaders(): HeadersInit {
  if (typeof window === "undefined") return {};
  const storedUser = localStorage.getItem("usas_user");
  if (storedUser) {
    try {
      const user = JSON.parse(storedUser);
      if (user?.id) {
        return { "X-User-Id": user.id };
      }
    } catch {
      // ignore
    }
  }
  return {};
}

// 1. TÀI CHÍNH
export async function getFinancialProfile(): Promise<FinancialProfileDto | null> {
  try {
    return await apiFetch<FinancialProfileDto>("/api/v1/profile/financial", {
      headers: getAuthHeaders(),
    });
  } catch (err: any) {
    if (err?.status === 404 || err?.status === 401) return null;
    throw err;
  }
}

export async function saveFinancialProfile(
  data: SaveFinancialProfileRequest
): Promise<FinancialProfileDto> {
  return await apiFetch<FinancialProfileDto>("/api/v1/profile/financial", {
    method: "PUT",
    headers: getAuthHeaders(),
    body: JSON.stringify(data),
  });
}

// 2. NGOẠI KHÓA
export async function getExtracurricularActivities(): Promise<ExtracurricularActivityDto[]> {
  try {
    return await apiFetch<ExtracurricularActivityDto[]>("/api/v1/profile/extracurricular", {
      headers: getAuthHeaders(),
    });
  } catch (err: any) {
    if (err?.status === 404 || err?.status === 401) return [];
    throw err;
  }
}

export async function addExtracurricularActivity(
  data: CreateExtracurricularRequest
): Promise<ExtracurricularActivityDto> {
  return await apiFetch<ExtracurricularActivityDto>("/api/v1/profile/extracurricular", {
    method: "POST",
    headers: getAuthHeaders(),
    body: JSON.stringify(data),
  });
}

export async function updateExtracurricularActivity(
  id: string,
  data: UpdateExtracurricularRequest
): Promise<ExtracurricularActivityDto> {
  return await apiFetch<ExtracurricularActivityDto>(`/api/v1/profile/extracurricular/${id}`, {
    method: "PUT",
    headers: getAuthHeaders(),
    body: JSON.stringify(data),
  });
}

export async function deleteExtracurricularActivity(id: string): Promise<void> {
  await apiFetch<void>(`/api/v1/profile/extracurricular/${id}`, {
    method: "DELETE",
    headers: getAuthHeaders(),
  });
}

// 3. THÀNH TÍCH / GIẢI THƯỞNG
export async function getAchievements(): Promise<StudentAchievementDto[]> {
  try {
    return await apiFetch<StudentAchievementDto[]>("/api/v1/profile/achievements", {
      headers: getAuthHeaders(),
    });
  } catch (err: any) {
    if (err?.status === 404 || err?.status === 401) return [];
    throw err;
  }
}

export async function addAchievement(
  data: CreateAchievementRequest
): Promise<StudentAchievementDto> {
  return await apiFetch<StudentAchievementDto>("/api/v1/profile/achievements", {
    method: "POST",
    headers: getAuthHeaders(),
    body: JSON.stringify(data),
  });
}

export async function deleteAchievement(id: string): Promise<void> {
  await apiFetch<void>(`/api/v1/profile/achievements/${id}`, {
    method: "DELETE",
    headers: getAuthHeaders(),
  });
}

// 4. TỔNG QUAN HỒ SƠ
export async function getProfileSummary(): Promise<StudentProfileSummaryDto | null> {
  try {
    return await apiFetch<StudentProfileSummaryDto>("/api/v1/profile/summary", {
      headers: getAuthHeaders(),
    });
  } catch (err: any) {
    if (err?.status === 404 || err?.status === 401) return null;
    throw err;
  }
}
