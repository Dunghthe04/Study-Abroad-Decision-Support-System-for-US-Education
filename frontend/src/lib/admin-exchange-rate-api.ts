import { apiFetch } from "@/lib/api";

// [UC-65] Trạng thái tỉ giá USD sang VNĐ (chỉ xem). Khớp ExchangeRateStatusDto của backend.
export interface ExchangeRateStatus {
  midVndPerUsd: number;
  bankSellRateSpread: number;
  sellVndPerUsd: number;
  asOf: string | null; // null: chưa từng lấy được, đang dùng tỉ giá dự phòng
  source: string;
  daysSinceUpdate: number | null;
  isStale: boolean; // quá 2 ngày chưa cập nhật được
  isFallback: boolean;
  fallbackVndPerUsd: number;
}

export function getAdminExchangeRate(): Promise<ExchangeRateStatus> {
  return apiFetch<ExchangeRateStatus>("/api/v1/admin/settings/exchange-rate");
}
