import { apiFetch, ApiError } from "@/lib/api";
import type { GradeScaleConfig } from "@/types/academic";

/** Bảng đang dùng. version gửi lại khi lưu để biết có ai sửa trước không; isDefault: DB chưa có bảng, đang dùng WES. */
export interface GradeScaleSetting {
  config: GradeScaleConfig;
  version: string | null;
  isDefault: boolean;
}

/** Một khoảng moderator nhập; maxScore server tự tính từ mốc kế tiếp. */
export interface GradeScaleRuleInput {
  minScore: number;
  gpa4: number;
  letterGrade: string;
}

export interface GradeScaleHistoryItem {
  changedAt: string;
  actorUserId: string | null;
  actorName: string | null;
  old: GradeScaleConfig | null;
  new: GradeScaleConfig | null;
}

export type SaveGradeScaleResult =
  | { status: "ok"; setting: GradeScaleSetting; recomputedProfiles: number }
  | { status: "invalid"; errors: string[] }
  | { status: "conflict"; errors: string[]; current: GradeScaleSetting };

const BASE = "/api/v1/admin/settings/grade-scale";

export function getAdminGradeScale(): Promise<GradeScaleSetting> {
  return apiFetch<GradeScaleSetting>(BASE);
}

export function getGradeScaleHistory(limit = 20): Promise<GradeScaleHistoryItem[]> {
  return apiFetch<GradeScaleHistoryItem[]>(`${BASE}/history?limit=${limit}`);
}

/** Chỉ moderator lưu được. 400 (sai luật) và 409 (người khác vừa sửa) trả về kết quả để màn hình hiện, lỗi khác ném ra. */
export async function saveGradeScale(
  rules: GradeScaleRuleInput[],
  sourceName: string,
  version: string | null,
): Promise<SaveGradeScaleResult> {
  try {
    const saved = await apiFetch<{ setting: GradeScaleSetting; recomputedProfiles: number }>(BASE, {
      method: "PUT",
      body: JSON.stringify({ rules, sourceName, version }),
    });
    return { status: "ok", ...saved };
  } catch (err) {
    if (err instanceof ApiError && (err.status === 400 || err.status === 409)) {
      const body = err.body as { errors?: unknown; current?: GradeScaleSetting } | null;
      const errors = Array.isArray(body?.errors) ? body.errors.map(String) : [err.message];
      if (err.status === 409 && body?.current) return { status: "conflict", errors, current: body.current };
      return { status: "invalid", errors };
    }
    throw err;
  }
}
