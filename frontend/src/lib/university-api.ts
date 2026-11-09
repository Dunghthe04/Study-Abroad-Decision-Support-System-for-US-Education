import { cache } from "react";
import { apiFetch } from "@/lib/api";
import type { UniversityDetail } from "@/types/university";

export const getUniversityDetail = cache(async (code: string): Promise<UniversityDetail> => {
  return await apiFetch<UniversityDetail>(`/api/v1/universities/${encodeURIComponent(code)}`, {
    signal: AbortSignal.timeout(15_000),
  });
});
