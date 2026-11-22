import { ApiError, apiFetch } from "@/lib/api";
import type { PolicyKind, PublicPolicy } from "@/types/policy";

/**
 * Bản đang hiệu lực của một loại tài liệu (UC-103). Công khai, không cần đăng nhập.
 * Trả null khi loại này chưa có bản nào được công bố (404); lỗi khác (mất kết nối...) vẫn ném lên để trang báo "không tải được".
 */
export async function getCurrentPolicy(kind: PolicyKind): Promise<PublicPolicy | null> {
  try {
    return await apiFetch<PublicPolicy>(`/api/v1/policies/${kind}`, { signal: AbortSignal.timeout(10_000) });
  } catch (error) {
    if (error instanceof ApiError && error.status === 404) return null;
    throw error;
  }
}

/** 22/11/2026, theo giờ Việt Nam (ngày công bố lưu giờ UTC nên tính lại cho đúng ngày người dùng thấy). */
export function formatPolicyDate(iso: string): string {
  const date = new Date(iso);
  return Number.isNaN(date.getTime())
    ? iso
    : date.toLocaleDateString("vi-VN", { day: "2-digit", month: "2-digit", year: "numeric", timeZone: "Asia/Ho_Chi_Minh" });
}
