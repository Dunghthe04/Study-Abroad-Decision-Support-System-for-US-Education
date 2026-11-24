import { apiFetch } from "@/lib/api";
import type { CostEstimate, CostEstimateParams, StateLivingCost } from "@/types/cost";

export async function getCostEstimate(params: CostEstimateParams, signal?: AbortSignal): Promise<CostEstimate> {
  const query = new URLSearchParams({ codes: params.codes.join(",") });
  if (params.studyLevel) query.set("studyLevel", params.studyLevel);
  if (params.scholarshipUsdPerYear) query.set("scholarshipUsdPerYear", String(params.scholarshipUsdPerYear));
  if (params.budgetUsd) query.set("budgetUsd", String(params.budgetUsd));
  return await apiFetch<CostEstimate>(`/api/v1/costs/estimate?${query}`, { signal: signal ?? AbortSignal.timeout(15_000) });
}

export async function getStateLivingCosts(): Promise<StateLivingCost[]> {
  return await apiFetch<StateLivingCost[]>("/api/v1/costs/state-living-costs", { signal: AbortSignal.timeout(15_000) });
}
