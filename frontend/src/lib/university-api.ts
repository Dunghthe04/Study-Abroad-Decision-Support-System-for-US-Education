import { cache } from "react";
import { apiFetch } from "@/lib/api";
import type { PagedResult } from "@/types/api";
import type {
  UniversityDetail,
  UniversityFilterOptions,
  UniversityListItem,
  UniversitySearchParams,
} from "@/types/university";

export const getUniversityDetail = cache(async (code: string): Promise<UniversityDetail> => {
  return await apiFetch<UniversityDetail>(`/api/v1/universities/${encodeURIComponent(code)}`, {
    signal: AbortSignal.timeout(15_000),
  });
});

export async function searchUniversities(
  params?: UniversitySearchParams,
  signal?: AbortSignal
): Promise<PagedResult<UniversityListItem>> {
  const query = new URLSearchParams();
  if (params?.search?.trim()) query.set("search", params.search.trim());
  if (params?.state && params.state !== "all") query.set("state", params.state);
  if (params?.control && params.control !== "all") query.set("control", params.control);
  if (params?.major && params.major !== "all") query.set("major", params.major);
  if (params?.maxTuitionUsd !== undefined && params.maxTuitionUsd > 0)
    query.set("maxTuitionUsd", params.maxTuitionUsd.toString());
  if (params?.maxTotalCostUsd !== undefined && params.maxTotalCostUsd > 0)
    query.set("maxTotalCostUsd", params.maxTotalCostUsd.toString());
  if (params?.maxMinIelts !== undefined && params.maxMinIelts > 0)
    query.set("maxMinIelts", params.maxMinIelts.toString());
  if (params?.maxMinToefl !== undefined && params.maxMinToefl > 0)
    query.set("maxMinToefl", params.maxMinToefl.toString());
  if (params?.maxMinDuolingo !== undefined && params.maxMinDuolingo > 0)
    query.set("maxMinDuolingo", params.maxMinDuolingo.toString());
  if (params?.minAcceptanceRate !== undefined)
    query.set("minAcceptanceRate", params.minAcceptanceRate.toString());
  if (params?.maxAcceptanceRate !== undefined)
    query.set("maxAcceptanceRate", params.maxAcceptanceRate.toString());
  if (params?.isIvy !== undefined) query.set("isIvy", params.isIvy.toString());
  if (params?.sortBy) query.set("sortBy", params.sortBy);
  if (params?.studyLevel) query.set("studyLevel", params.studyLevel);
  if (params?.page) query.set("page", params.page.toString());
  if (params?.pageSize) query.set("pageSize", params.pageSize.toString());

  const qs = query.toString();
  const endpoint = `/api/v1/universities${qs ? `?${qs}` : ""}`;
  return await apiFetch<PagedResult<UniversityListItem>>(endpoint, {
    signal: signal ?? AbortSignal.timeout(15_000),
  });
}

export const getUniversityFilterOptions = cache(
  async (signal?: AbortSignal): Promise<UniversityFilterOptions> => {
    return await apiFetch<UniversityFilterOptions>("/api/v1/universities/filter-options", {
      signal: signal ?? AbortSignal.timeout(15_000),
    });
  }
);
