"use client";

import { Alert, AlertDescription } from "@/components/ui/alert";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { formatInt, formatUsd } from "@/lib/ai-usage-format";
import type { Fetched } from "@/lib/use-fetched";
import type { AiPrice } from "@/types/ai-usage";

/** Bảng giá đang áp dụng, chỉ đọc. Giá đọc từ cấu hình backend (mục AiPricing). */
export function PricingPanel({ prices }: { prices: Fetched<AiPrice[]> }) {
  const threshold = prices.data?.[0]?.longPromptThreshold;
  return (
    <Card>
      <CardHeader>
        <CardTitle>
          <h2 className="text-base font-semibold text-ink">Bảng giá đang áp dụng</h2>
        </CardTitle>
        <CardDescription>
          Giá USD cho một triệu token, dùng để tính chi phí mỗi lần gọi ngay lúc gọi. Giá đọc từ cấu hình backend (appsettings.json,
          mục AiPricing, hoặc biến môi trường AiPricing__Models__...). Đổi giá không làm lệch các lần gọi đã ghi.
        </CardDescription>
      </CardHeader>
      <CardContent className="flex flex-col gap-3">
        {prices.error ? (
          <Alert variant="destructive"><AlertDescription>{prices.error}</AlertDescription></Alert>
        ) : prices.data && prices.data.length > 0 ? (
          <div className="overflow-x-auto rounded-md border border-line">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Model</TableHead>
                  <TableHead className="text-right">Token vào</TableHead>
                  <TableHead className="text-right">Token ra</TableHead>
                  <TableHead className="text-right">Vào (prompt dài)</TableHead>
                  <TableHead className="text-right">Ra (prompt dài)</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {prices.data.map((price) => (
                  <TableRow key={price.model}>
                    <TableCell className="font-medium">{price.model}</TableCell>
                    <TableCell className="tabular text-right">{formatUsd(price.inputPerMTok)}</TableCell>
                    <TableCell className="tabular text-right">{formatUsd(price.outputPerMTok)}</TableCell>
                    <TableCell className="tabular text-right">{formatUsd(price.longInputPerMTok)}</TableCell>
                    <TableCell className="tabular text-right">{formatUsd(price.longOutputPerMTok)}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </div>
        ) : (
          <p className="text-sm text-ink-2">{prices.loading ? "Đang tải bảng giá..." : "Chưa có model nào trong bảng giá."}</p>
        )}
        <ul className="list-disc space-y-0.5 pl-5 text-xs text-ink-3">
          {threshold ? <li>Prompt dài hơn {formatInt(threshold)} token tính giá bậc dài cho cả lần gọi.</li> : null}
          <li>Model Claude không có trong bảng này được ghi là &ldquo;chưa có giá&rdquo; (không phải 0 đồng); số lần như vậy hiện ở cảnh báo đầu trang.</li>
          <li>Ollama chạy trên máy của dự án nên tính 0 đồng.</li>
        </ul>
      </CardContent>
    </Card>
  );
}
