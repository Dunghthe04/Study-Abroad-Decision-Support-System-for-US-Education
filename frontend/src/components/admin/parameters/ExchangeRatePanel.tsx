"use client";

import { useEffect, useState } from "react";
import { TriangleAlertIcon } from "lucide-react";
import { getAdminExchangeRate, type ExchangeRateStatus } from "@/lib/admin-exchange-rate-api";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Spinner } from "@/components/ui/spinner";

const VND = new Intl.NumberFormat("vi-VN", { maximumFractionDigits: 2 });
const PERCENT = new Intl.NumberFormat("vi-VN", { maximumFractionDigits: 2 });

function formatDate(iso: string): string {
  return new Date(iso).toLocaleString("vi-VN", { dateStyle: "short", timeStyle: "short" });
}

/**
 * [UC-65] Theo dõi tỉ giá USD sang VNĐ dùng cho ước tính chi phí: tỉ giá đang dùng, thời điểm cập nhật gần nhất, nguồn, số ngày từ lần cập nhật.
 * Chỉ xem (tỉ giá tự cập nhật mỗi ngày). Cảnh báo khi quá 2 ngày chưa cập nhật được, hoặc khi chưa từng lấy được và đang dùng tỉ giá dự phòng.
 */
export function ExchangeRatePanel() {
  const [status, setStatus] = useState<ExchangeRateStatus | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let active = true;
    getAdminExchangeRate()
      .then((value) => active && setStatus(value))
      .catch((err: unknown) => active && setError(err instanceof Error ? err.message : "Chưa tải được tỉ giá."));
    return () => {
      active = false;
    };
  }, []);

  return (
    <Card>
      <CardHeader>
        <CardTitle>Tỉ giá USD sang VNĐ</CardTitle>
        <CardDescription>
          Tự cập nhật mỗi ngày từ nguồn công khai. Chỉ xem, Moderator không sửa tỉ giá. Khi quy đổi chi phí, hệ thống cộng thêm biên độ giá bán ra của ngân hàng.
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        {error && <p className="text-sm text-destructive">{error}</p>}
        {!status && !error && <Spinner className="size-4 text-brand" />}
        {status?.isFallback && (
          <Alert variant="destructive">
            <TriangleAlertIcon className="size-4" />
            <AlertTitle>Đang dùng tỉ giá dự phòng</AlertTitle>
            <AlertDescription>
              Chưa từng lấy được tỉ giá từ nguồn công khai nên hệ thống dùng tỉ giá dự phòng {VND.format(status.fallbackVndPerUsd)} VNĐ mỗi USD. Số VNĐ trên các trang chi phí chỉ mang tính tham khảo.
            </AlertDescription>
          </Alert>
        )}
        {status?.isStale && (
          <Alert variant="destructive">
            <TriangleAlertIcon className="size-4" />
            <AlertTitle>Tỉ giá đã cũ</AlertTitle>
            <AlertDescription>
              Đã {status.daysSinceUpdate} ngày chưa cập nhật được tỉ giá mới. Hệ thống giữ tỉ giá gần nhất đã lưu.
            </AlertDescription>
          </Alert>
        )}
        {status && (
          <dl className="grid gap-x-8 gap-y-3 text-sm sm:grid-cols-2">
            <div>
              <dt className="text-xs font-medium text-muted-foreground">Tỉ giá trung bình thị trường (đang lưu)</dt>
              <dd className="text-lg font-semibold text-foreground">{VND.format(status.midVndPerUsd)} VNĐ/USD</dd>
            </div>
            <div>
              <dt className="text-xs font-medium text-muted-foreground">
                Tỉ giá dùng quy đổi chi phí (cộng biên độ ngân hàng {PERCENT.format(status.bankSellRateSpread * 100)}%)
              </dt>
              <dd className="text-lg font-semibold text-foreground">{VND.format(status.sellVndPerUsd)} VNĐ/USD</dd>
            </div>
            <div>
              <dt className="text-xs font-medium text-muted-foreground">Cập nhật gần nhất</dt>
              <dd className="text-foreground">{status.asOf ? formatDate(status.asOf) : "Chưa từng lấy được"}</dd>
            </div>
            <div>
              <dt className="text-xs font-medium text-muted-foreground">Số ngày từ lần cập nhật gần nhất</dt>
              <dd className="text-foreground">{status.daysSinceUpdate ?? "Chưa có"}</dd>
            </div>
            <div className="sm:col-span-2">
              <dt className="text-xs font-medium text-muted-foreground">Nguồn</dt>
              <dd className="text-foreground">{status.source}</dd>
            </div>
          </dl>
        )}
      </CardContent>
    </Card>
  );
}
