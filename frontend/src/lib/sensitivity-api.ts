import { ApiError, apiFetch } from "@/lib/api";
import { authFetch } from "@/lib/auth-refresh";
import type {
  SensitivityComparison,
  SensitivityParameterInfo,
  SensitivityRequest,
  SensitivityRunDetail,
  SensitivityRunListRow,
} from "@/types/sensitivity";

const base = "/api/v1/admin/sensitivity";

// Phân tích độ nhạy tham số (UC-85). Chỉ admin. Chạy trên hồ sơ giả lập, không có dữ liệu học sinh thật.
export const adminSensitivityApi = {
  parameters: () => apiFetch<SensitivityParameterInfo[]>(`${base}/parameters`),

  run: (request: SensitivityRequest) =>
    apiFetch<SensitivityRunDetail>(`${base}/runs`, { method: "POST", body: JSON.stringify(request) }),

  list: (limit = 50) => apiFetch<SensitivityRunListRow[]>(`${base}/runs?limit=${limit}`),

  get: (id: string) => apiFetch<SensitivityRunDetail>(`${base}/runs/${id}`),

  /** withId bỏ trống: so với lần chạy ngay trước. */
  compare: (id: string, withId?: string | null) =>
    apiFetch<SensitivityComparison>(`${base}/runs/${id}/compare${withId ? `?with=${withId}` : ""}`),

  /** Tải CSV (chỉ số tổng hợp). Mỗi lần tải được backend ghi nhật ký. */
  downloadCsv: async (id: string) => {
    const res = await authFetch(`${base}/runs/${id}/csv`, { credentials: "include", cache: "no-store" });
    if (!res.ok) {
      const body = await res.json().catch(() => null);
      throw new ApiError(res.status, body?.error ?? body?.title ?? "Không tải được file CSV.", body);
    }
    const header = res.headers.get("content-disposition") ?? "";
    const match = /filename\*?=(?:UTF-8'')?"?([^";]+)"?/i.exec(header);
    const url = URL.createObjectURL(await res.blob());
    const link = document.createElement("a");
    link.href = url;
    link.download = match ? decodeURIComponent(match[1]) : `uc85-sensitivity-${id.slice(0, 8)}.csv`;
    document.body.appendChild(link);
    link.click();
    link.remove();
    window.setTimeout(() => URL.revokeObjectURL(url), 1000);
  },
};
