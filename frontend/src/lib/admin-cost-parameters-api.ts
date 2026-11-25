import { apiFetch, ApiError } from "@/lib/api";

export interface CostParameters {
  visaIntegrityFeeUsd: number;
  annualEscalationRate: number;
  bankSellRateSpread: number;
  undergraduateYears: number;
}

export interface CostParametersSetting {
  values: CostParameters;
  version: string | null;
  isDefault: boolean;
  defaults: CostParameters;
}

export type SaveCostParametersResult =
  | { status: "ok"; setting: CostParametersSetting }
  | { status: "invalid"; errors: string[] }
  | { status: "conflict"; errors: string[]; current: CostParametersSetting };

const BASE = "/api/v1/admin/settings/cost-parameters";

export function getAdminCostParameters(): Promise<CostParametersSetting> {
  return apiFetch<CostParametersSetting>(BASE);
}

export async function saveCostParameters(
  values: CostParameters,
  version: string | null,
): Promise<SaveCostParametersResult> {
  try {
    const setting = await apiFetch<CostParametersSetting>(BASE, {
      method: "PUT",
      body: JSON.stringify({ ...values, version }),
    });
    return { status: "ok", setting };
  } catch (err) {
    if (err instanceof ApiError && (err.status === 400 || err.status === 409)) {
      const body = err.body as { errors?: unknown; current?: CostParametersSetting } | null;
      const errors = Array.isArray(body?.errors) ? body.errors.map(String) : [err.message];
      if (err.status === 409 && body?.current) return { status: "conflict", errors, current: body.current };
      return { status: "invalid", errors };
    }
    throw err;
  }
}
