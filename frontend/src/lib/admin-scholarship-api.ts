import { apiFetch, ApiError } from "@/lib/api";
import { toSaveError } from "@/lib/admin-university-api";
import type { PagedResult } from "@/types/api";
import type { AdminScholarship, AdminScholarshipFilters, SaveScholarshipRequest } from "@/types/admin-scholarship";

const BASE = "/api/v1/admin/scholarships";

export async function getAdminScholarships(filters: AdminScholarshipFilters): Promise<PagedResult<AdminScholarship>> {
  const params = new URLSearchParams({ page: String(filters.page), pageSize: String(filters.pageSize ?? 20) });
  if (filters.universityId) params.set("universityId", filters.universityId);
  if (filters.search?.trim()) params.set("search", filters.search.trim());
  if (filters.studyLevel) params.set("studyLevel", filters.studyLevel);
  if (filters.active === "active") params.set("isActive", "true");
  if (filters.active === "hidden") params.set("isActive", "false");

  try {
    return await apiFetch<PagedResult<AdminScholarship>>(`${BASE}?${params}`);
  } catch (err) {
    if (err instanceof ApiError && err.status === 403) {
      throw new Error("Bạn không có quyền xem danh sách học bổng.");
    }
    throw new Error("Không thể tải danh sách học bổng. Vui lòng thử lại sau.");
  }
}

export async function createAdminScholarship(body: SaveScholarshipRequest): Promise<AdminScholarship> {
  try {
    return await apiFetch<AdminScholarship>(BASE, { method: "POST", body: JSON.stringify(body) });
  } catch (err) {
    throw toSaveError(err);
  }
}

export async function updateAdminScholarship(id: string, body: SaveScholarshipRequest): Promise<AdminScholarship> {
  try {
    return await apiFetch<AdminScholarship>(`${BASE}/${id}`, { method: "PUT", body: JSON.stringify(body) });
  } catch (err) {
    throw toSaveError(err);
  }
}

export async function setAdminScholarshipVisibility(
  id: string,
  isActive: boolean,
  reason: string | null,
): Promise<AdminScholarship> {
  try {
    return await apiFetch<AdminScholarship>(`${BASE}/${id}/visibility`, {
      method: "PATCH",
      body: JSON.stringify({ isActive, reason }),
    });
  } catch (err) {
    throw toSaveError(err);
  }
}
