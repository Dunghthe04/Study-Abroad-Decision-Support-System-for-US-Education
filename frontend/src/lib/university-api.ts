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

const ALLOWED_REGIONS = new Set(["northeast", "midwest", "south", "west"]);
const ALLOWED_CAMPUS = new Set(["city", "suburb", "town", "rural"]);
const ALLOWED_TEST_POLICIES = new Set(["required", "optional", "not_accepted"]);
const ALLOWED_DEADLINE_ROUNDS = new Set(["ED", "EA", "RD", "RollingFinal"]);
const ALLOWED_IMPORTANCE = new Set(["very_important", "important", "considered", "not_considered"]);
const ALLOWED_AID_POLICIES = new Set(["need_and_merit", "merit_only", "need_only", "none"]);

export function buildUniversitySearchQuery(params?: UniversitySearchParams): URLSearchParams {
  const query = new URLSearchParams();
  if (!params) return query;

  const setNum = (key: string, val: number | undefined, allowZero = false) => {
    if (val !== undefined && Number.isFinite(val) && (allowZero ? val >= 0 : val > 0)) {
      query.set(key, val.toString());
    }
  };

  if (params.search?.trim()) query.set("search", params.search.trim());
  if (params.state && params.state !== "all") query.set("state", params.state);
  if (params.region && ALLOWED_REGIONS.has(params.region)) query.set("region", params.region);
  if (params.control && params.control !== "all") query.set("control", params.control);
  if (params.major && params.major !== "all") query.set("major", params.major);

  setNum("minTuitionUsd", params.minTuitionUsd);
  setNum("maxTuitionUsd", params.maxTuitionUsd);
  setNum("minTotalCostUsd", params.minTotalCostUsd);
  setNum("maxTotalCostUsd", params.maxTotalCostUsd);
  setNum("minLivingUsd", params.minLivingUsd);
  setNum("maxLivingUsd", params.maxLivingUsd);
  setNum("maxApplicationFeeUsd", params.maxApplicationFeeUsd, true);

  setNum("maxMinIelts", params.maxMinIelts);
  setNum("maxMinToefl", params.maxMinToefl);
  setNum("maxMinDuolingo", params.maxMinDuolingo);
  setNum("maxMinGpa4", params.maxMinGpa4);
  setNum("minAvgGpa4", params.minAvgGpa4);
  setNum("maxAvgGpa4", params.maxAvgGpa4);
  setNum("minSat", params.minSat);
  setNum("maxSat", params.maxSat);
  setNum("minAct", params.minAct);
  setNum("maxAct", params.maxAct);

  if (params.satPolicy && ALLOWED_TEST_POLICIES.has(params.satPolicy)) query.set("satPolicy", params.satPolicy);
  if (params.actPolicy && ALLOWED_TEST_POLICIES.has(params.actPolicy)) query.set("actPolicy", params.actPolicy);
  if (params.deadlineRound && ALLOWED_DEADLINE_ROUNDS.has(params.deadlineRound))
    query.set("deadlineRound", params.deadlineRound);
  if (params.c7TestImportance && ALLOWED_IMPORTANCE.has(params.c7TestImportance))
    query.set("c7TestImportance", params.c7TestImportance);
  if (params.c7EcImportance && ALLOWED_IMPORTANCE.has(params.c7EcImportance))
    query.set("c7EcImportance", params.c7EcImportance);

  setNum("minAcceptanceRate", params.minAcceptanceRate, true);
  setNum("maxAcceptanceRate", params.maxAcceptanceRate, true);
  setNum("minIntlAcceptanceRate", params.minIntlAcceptanceRate, true);
  setNum("maxIntlAcceptanceRate", params.maxIntlAcceptanceRate, true);

  setNum("minStudentFacultyRatio", params.minStudentFacultyRatio);
  setNum("maxStudentFacultyRatio", params.maxStudentFacultyRatio);
  setNum("minRetentionRate", params.minRetentionRate, true);
  setNum("maxRetentionRate", params.maxRetentionRate, true);
  setNum("minGradRate6Yr", params.minGradRate6Yr, true);
  setNum("maxGradRate6Yr", params.maxGradRate6Yr, true);
  setNum("minIntlStudents", params.minIntlStudents, true);
  setNum("maxIntlStudents", params.maxIntlStudents, true);

  if (params.intlAidPolicy && ALLOWED_AID_POLICIES.has(params.intlAidPolicy))
    query.set("intlAidPolicy", params.intlAidPolicy);
  setNum("minIntlAidAvgUsd", params.minIntlAidAvgUsd, true);
  setNum("maxIntlAidAvgUsd", params.maxIntlAidAvgUsd, true);
  setNum("minFyOnCampusPct", params.minFyOnCampusPct, true);
  setNum("maxFyOnCampusPct", params.maxFyOnCampusPct, true);

  if (params.housingType?.trim()) query.set("housingType", params.housingType.trim());
  if (params.activity?.trim()) query.set("activity", params.activity.trim());

  if (params.isIvy === true) query.set("isIvy", "true");
  if (params.testOptional === true) query.set("testOptional", "true");
  if (params.hasIntlAid === true) query.set("hasIntlAid", "true");
  if (params.hasScholarship === true) query.set("hasScholarship", "true");
  if (params.intlHousing === true) query.set("intlHousing", "true");
  if (params.campusSetting && ALLOWED_CAMPUS.has(params.campusSetting))
    query.set("campusSetting", params.campusSetting);
  setNum("maxRank", params.maxRank);

  if (params.sortBy) query.set("sortBy", params.sortBy);
  if (params.sortBy === "rank" && params.rankListId) query.set("rankListId", params.rankListId);
  if (params.studyLevel) query.set("studyLevel", params.studyLevel);
  if (params.page) query.set("page", params.page.toString());
  if (params.pageSize) query.set("pageSize", params.pageSize.toString());

  return query;
}

export async function searchUniversities(
  params?: UniversitySearchParams,
  signal?: AbortSignal
): Promise<PagedResult<UniversityListItem>> {
  const qs = buildUniversitySearchQuery(params).toString();
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
