import { apiFetch } from "@/lib/api";
import type { PagedResult } from "@/types/api";
import type { ScholarshipItem, ScholarshipSearchParams } from "@/types/scholarship";

export async function searchScholarships(params: ScholarshipSearchParams): Promise<PagedResult<ScholarshipItem>> {
  const query = new URLSearchParams();
  for (const [key, value] of Object.entries(params)) {
    if (value !== undefined && value !== "") query.set(key, String(value));
  }
  const qs = query.toString();
  return await apiFetch<PagedResult<ScholarshipItem>>(`/api/v1/scholarships${qs ? `?${qs}` : ""}`, {
    signal: AbortSignal.timeout(15_000),
  });
}
