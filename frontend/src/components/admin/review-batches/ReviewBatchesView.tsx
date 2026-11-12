"use client";

import { useState } from "react";
import Link from "next/link";
import { PlusIcon } from "lucide-react";
import { cn } from "cn";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button, buttonVariants } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { CreateBatchSheet } from "@/components/admin/review-batches/CreateBatchSheet";
import { ProgressBar } from "@/components/admin/review-batches/ProgressBar";
import { formatDateTime, formatInt } from "@/lib/ai-usage-format";
import { adminReviewBatchApi } from "@/lib/review-batch-api";
import { batchProgress, stratumLabel } from "@/lib/review-batch-format";
import { useFetched } from "@/lib/use-fetched";
import type { ReviewBatchListItem } from "@/types/review-batch";

/**
 * Trang admin: các đợt duyệt mẫu. Mỗi đợt là một mẫu ngẫu nhiên các câu hệ thống đã trả lời được, để người duyệt đọc và chấm đúng sai.
 * Danh sách chỉ có số đo, không có nội dung chat; nội dung chỉ hiện ở màn chấm, cho người được giao duyệt đợt đó. Chỉ Admin.
 */
export function ReviewBatchesView() {
  const [version, setVersion] = useState(0);   // tăng sau khi tạo đợt để tải lại danh sách
  const [creating, setCreating] = useState(false);
  const batches = useFetched(`batches|${version}`, () => adminReviewBatchApi.list());
  const list = batches.data;

  return (
    <div className="flex flex-col gap-4">
      <header className="flex flex-wrap items-start justify-between gap-3">
        <div className="flex max-w-2xl flex-col gap-1">
          <h1 className="text-h2">Duyệt mẫu</h1>
          <p className="text-sm text-ink-2">
            Hàng đợi &ldquo;Câu cần xem&rdquo; chỉ có các câu hệ thống tự thừa nhận là không làm được. Ở đây bạn bốc ngẫu nhiên một số câu đã trả lời được,
            đọc và chấm đúng sai, để biết hệ thống sai bao nhiêu kể cả khi nó trả lời trôi chảy.
          </p>
        </div>
        <Button type="button" onClick={() => setCreating(true)}>
          <PlusIcon /> Tạo đợt
        </Button>
      </header>

      {batches.error ? <Alert variant="destructive"><AlertDescription>{batches.error}</AlertDescription></Alert> : null}

      <div data-loading={batches.loading ? "true" : "false"} className="flex flex-col gap-3 data-[loading=true]:opacity-60">
        {list?.map((batch) => <BatchCard key={batch.id} batch={batch} />)}
        {list && list.length === 0 ? (
          <Card>
            <CardContent className="flex flex-col items-start gap-2 py-6">
              <p className="text-ink">Chưa có đợt nào.</p>
              <p className="text-sm text-ink-2">
                Bấm &ldquo;Tạo đợt&rdquo; để bốc mẫu. Cần có câu hệ thống đã trả lời được (kèm câu hỏi) trong khoảng ngày bạn chọn.
              </p>
            </CardContent>
          </Card>
        ) : null}
        {!list && !batches.error ? <p className="text-sm text-ink-2">Đang tải...</p> : null}
      </div>

      <CreateBatchSheet open={creating} onClose={() => setCreating(false)} onCreated={() => setVersion((current) => current + 1)} />
    </div>
  );
}

function BatchCard({ batch }: { batch: ReviewBatchListItem }) {
  const progress = batchProgress(batch);
  const range =
    batch.sampledFromUtc || batch.sampledToUtcExclusive
      ? `${batch.sampledFromUtc ? formatDateTime(batch.sampledFromUtc) : "đầu"} → ${batch.sampledToUtcExclusive ? formatDateTime(batch.sampledToUtcExclusive) : "nay"}`
      : "Toàn bộ thời gian";

  return (
    <Card>
      <CardHeader>
        <CardTitle>
          <h2 className="text-base font-semibold text-ink">{batch.name}</h2>
        </CardTitle>
        <CardDescription>
          Tạo {formatDateTime(batch.createdAt)} · {formatInt(batch.totalItems)} câu · {batch.sizePerStratum} câu mỗi nhóm · seed{" "}
          <code className="rounded bg-ground px-1 text-[13px]">{batch.seed}</code>
        </CardDescription>
      </CardHeader>
      <CardContent className="flex flex-col gap-3">
        {batch.isReviewer ? (
          <ProgressBar graded={progress.graded} total={progress.total} label="Bạn đã chấm" />
        ) : (
          <p className="text-sm text-ink-2">Bạn tạo đợt này nhưng không được giao duyệt, nên chỉ xem được số đo, không xem được nội dung câu.</p>
        )}

        <details className="rounded-md border border-line">
          <summary className="cursor-pointer px-3 py-2 text-sm font-medium text-ink">
            Các nhóm lúc bốc ({batch.strata.length}) · {range}
          </summary>
          <div className="overflow-x-auto border-t border-line">
            <table className="w-full text-sm">
              <thead>
                <tr className="text-left text-xs text-ink-3">
                  <th className="px-3 py-1.5 font-medium">Nhóm</th>
                  <th className="px-3 py-1.5 text-right font-medium">Có trong nhóm</th>
                  <th className="px-3 py-1.5 text-right font-medium">Được chọn</th>
                </tr>
              </thead>
              <tbody>
                {batch.strata.map((stratum) => (
                  <tr key={stratum.stratum} className="border-t border-line">
                    <td className="px-3 py-1.5 text-ink">{stratumLabel(stratum.stratum)}</td>
                    <td className="tabular px-3 py-1.5 text-right">{formatInt(stratum.population)}</td>
                    <td className="tabular px-3 py-1.5 text-right">{formatInt(stratum.picked)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </details>

        <div className="flex flex-wrap items-center gap-2">
          {batch.isReviewer ? (
            <Link href={`/admin/review-batches/${batch.id}`} className={cn(buttonVariants({ variant: progress.done ? "outline" : "default" }))}>
              {progress.done ? "Xem lại" : progress.graded > 0 ? "Chấm tiếp" : "Bắt đầu chấm"}
            </Link>
          ) : null}
          <Link href={`/admin/review-batches/${batch.id}/report`} className={cn(buttonVariants({ variant: "outline" }))}>
            Xem báo cáo
          </Link>
          {batch.isReviewer && progress.done ? <Badge variant="ok">Đã chấm hết</Badge> : null}
          {batch.isCreator ? <Badge variant="neutral">Bạn tạo</Badge> : null}
        </div>
      </CardContent>
    </Card>
  );
}
