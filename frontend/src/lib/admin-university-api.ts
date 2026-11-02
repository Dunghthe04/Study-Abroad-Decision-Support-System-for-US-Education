import { apiFetch, ApiError } from "@/lib/api";
import type { PagedResult } from "@/types/api";
import type {
  AdminFieldErrors,
  AdminMajorOption,
  AdminUniversityDetail,
  AdminUniversityFilters,
  AdminUniversityListItem,
  AdminUniversityOffering,
  UpdateOfferingRequest,
  UpdateUniversityRequest,
} from "@/types/admin-university";

export const ADMIN_UNIVERSITY_PAGE_SIZE = 20;

/** Lỗi khi lưu: message chung để hiện đầu form, fieldErrors để tô đúng ô. */
export class AdminFormError extends Error {
  constructor(
    message: string,
    public fieldErrors: AdminFieldErrors = {},
  ) {
    super(message);
    this.name = "AdminFormError";
  }
}

function readFieldErrors(body: unknown): AdminFieldErrors {
  if (!body || typeof body !== "object" || !("errors" in body)) {
    return {};
  }
  const errors = (body as { errors: unknown }).errors;
  if (!errors || typeof errors !== "object") {
    return {};
  }
  const result: AdminFieldErrors = {};
  for (const [field, value] of Object.entries(errors)) {
    result[field] = Array.isArray(value) ? value.join(" ") : String(value);
  }
  return result;
}

function toSaveError(err: unknown): AdminFormError {
  if (err instanceof ApiError) {
    if (err.status === 400) {
      return new AdminFormError("Dữ liệu chưa hợp lệ, xem các ô được đánh dấu.", readFieldErrors(err.body));
    }
    if (err.status === 403) {
      return new AdminFormError("Bạn không có quyền sửa dữ liệu trường.");
    }
    if (err.status === 404) {
      return new AdminFormError("Không tìm thấy trường hoặc bậc học này, có thể đã bị xóa.");
    }
  }
  return new AdminFormError("Không thể lưu. Vui lòng thử lại sau.");
}

export async function getAdminUniversity(universityId: string): Promise<AdminUniversityDetail> {
  try {
    return await apiFetch<AdminUniversityDetail>(`/api/v1/admin/universities/${universityId}`);
  } catch (err) {
    if (err instanceof ApiError && err.status === 404) {
      throw new Error("Không tìm thấy trường này.");
    }
    throw new Error("Không thể tải thông tin trường. Vui lòng thử lại sau.");
  }
}

export async function getAdminMajors(): Promise<AdminMajorOption[]> {
  try {
    return await apiFetch<AdminMajorOption[]>("/api/v1/admin/universities/majors");
  } catch {
    throw new Error("Không thể tải danh mục ngành. Vui lòng thử lại sau.");
  }
}

export async function updateAdminUniversity(
  universityId: string,
  body: UpdateUniversityRequest,
): Promise<AdminUniversityDetail> {
  try {
    return await apiFetch<AdminUniversityDetail>(`/api/v1/admin/universities/${universityId}`, {
      method: "PUT",
      body: JSON.stringify(body),
    });
  } catch (err) {
    throw toSaveError(err);
  }
}

export async function updateAdminOffering(
  universityId: string,
  offeringId: string,
  body: UpdateOfferingRequest,
): Promise<AdminUniversityOffering> {
  try {
    return await apiFetch<AdminUniversityOffering>(
      `/api/v1/admin/universities/${universityId}/offerings/${offeringId}`,
      { method: "PUT", body: JSON.stringify(body) },
    );
  } catch (err) {
    throw toSaveError(err);
  }
}

export async function setAdminUniversityVisibility(
  universityId: string,
  isActive: boolean,
): Promise<AdminUniversityDetail> {
  try {
    return await apiFetch<AdminUniversityDetail>(`/api/v1/admin/universities/${universityId}/visibility`, {
      method: "PATCH",
      body: JSON.stringify({ isActive }),
    });
  } catch (err) {
    throw toSaveError(err);
  }
}

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
