import { apiFetch, ApiError } from "@/lib/api";
import type { PagedResult } from "@/types/api";
import type {
  AdminUniversityFilters,
  AdminUniversityListItem,
  AdminUniversityOffering,
} from "@/types/admin-university";

export const ADMIN_UNIVERSITY_PAGE_SIZE = 20;

export function buildAdminUniversityQuery(filters: AdminUniversityFilters): string {
  const params = new URLSearchParams();
  params.set("page", String(filters.page));
  params.set("pageSize", String(ADMIN_UNIVERSITY_PAGE_SIZE));

  const search = filters.search.trim();
  if (search) {
    params.set("search", search);
  }
  const state = filters.state.trim();
  if (state) {
    params.set("state", state);
  }
  if (filters.control) {
    params.set("control", filters.control);
  }
  if (filters.active === "active") {
    params.set("isActive", "true");
  }
  if (filters.active === "hidden") {
    params.set("isActive", "false");
  }

  return params.toString();
}

export async function getAdminUniversityOfferings(universityId: string): Promise<AdminUniversityOffering[]> {
  try {
    return await apiFetch<AdminUniversityOffering[]>(`/api/v1/admin/universities/${universityId}/offerings`);
  } catch (err) {
    if (err instanceof ApiError && err.status === 404) {
      throw new Error("Không tìm thấy trường này.");
    }
    throw new Error("Không thể tải danh sách bậc học của trường. Vui lòng thử lại sau.");
  }
}

export async function getAdminUniversities(
  filters: AdminUniversityFilters
): Promise<PagedResult<AdminUniversityListItem>> {
  try {
    return await apiFetch<PagedResult<AdminUniversityListItem>>(
      `/api/v1/admin/universities?${buildAdminUniversityQuery(filters)}`
    );
  } catch (err) {
    if (err instanceof ApiError && err.status === 403) {
      throw new Error("Bạn không có quyền xem danh sách trường.");
    }
    throw new Error("Không thể tải danh sách trường. Vui lòng thử lại sau.");
  }
}
