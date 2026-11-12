"use client";

import Link from "next/link";
import { ArrowLeftIcon, TriangleAlertIcon } from "lucide-react";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { StatTile } from "@/components/admin/ai-usage/parts";
import { formatInt, formatPct } from "@/lib/ai-usage-format";
import { adminReviewBatchApi } from "@/lib/review-batch-api";
import {
  causeLabel, countText, intentOrUnknownLabel, intervalText, rateText, reportNotices, stratumLabel,
} from "@/lib/review-batch-format";
import { useFetched } from "@/lib/use-fetched";
import type { RateEstimate } from "@/types/review-batch";

/**
 * Báo cáo của một đợt duyệt mẫu: hệ thống trả lời sai bao nhiêu (từng nhóm và chung), sai vì sao, SLM hay nhầm nhãn nào.
 * Chỉ là số tổng hợp, không có nội dung chat. Số liệu từ mẫu luôn kèm khoảng tin cậy; nhóm chấm hết thì ghi là số chính xác.
 */
export function ReviewReportView({ batchId }: { batchId: string }) {
  const fetched = useFetched(`report|${batchId}`, () => adminReviewBatchApi.report(batchId));
  const data = fetched.data;
  const report = data?.report;
  const notices = data ? reportNotices(data) : [];
  const totalWrong = report?.causes.reduce((sum, cause) => sum + cause.count, 0) ?? 0;

  return (
    <div className="flex flex-col gap-4">
      <header className="flex flex-col gap-2">
        <Link href="/admin/review-batches" className="inline-flex w-fit items-center gap-1 text-sm text-ink-2 hover:text-ink">
          <ArrowLeftIcon aria-hidden className="size-4" /> Các đợt
        </Link>
        <h1 className="text-h2">{data ? `Báo cáo: ${data.batchName}` : "Báo cáo duyệt mẫu"}</h1>
        {data ? (
          <p className="text-sm text-ink-2">
            {formatInt(data.itemsGraded)} / {formatInt(data.totalItems)} câu đã chấm · seed{" "}
            <code className="rounded bg-ground px-1 text-[13px]">{data.seed}</code>
          </p>
        ) : null}
      </header>

      {fetched.error ? (
        <Alert variant="destructive">
          <AlertDescription>{fetched.error} Có thể đợt không tồn tại hoặc bạn không phải người tạo hay người được giao đợt này.</AlertDescription>
        </Alert>
      ) : null}
      {!data && !fetched.error ? <p className="text-sm text-ink-2">Đang tải...</p> : null}

      {notices.length > 0 ? (
        <Alert>
          <TriangleAlertIcon aria-hidden />
          <AlertDescription>
            <ul className="flex flex-col gap-1">
              {notices.map((notice) => <li key={notice}>{notice}</li>)}
            </ul>
          </AlertDescription>
        </Alert>
      ) : null}

      {report && data && data.itemsGraded > 0 ? (
        <>
          <section aria-label="Số liệu chung" className="grid gap-3 sm:grid-cols-3">
            <StatTile label="Đã chấm" value={`${formatInt(data.itemsGraded)} / ${formatInt(data.totalItems)}`} note="câu của đợt" />
            <RateTile label="Câu trả lời sai (chung)" estimate={report.answerWrong} />
            <RateTile label="Nhãn SLM gán sai (chung)" estimate={report.labelWrong} />
          </section>

          <Card>
            <CardHeader>
              <CardTitle><h2 className="text-base font-semibold text-ink">Theo nhóm</h2></CardTitle>
              <CardDescription>
                Nhóm là skill hệ thống đã chạy. Tỷ lệ sai là số câu trả lời sai chia số câu đã chấm trong nhóm, kèm khoảng tin cậy 95%.
              </CardDescription>
            </CardHeader>
            <CardContent className="overflow-x-auto">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Nhóm</TableHead>
                    <TableHead className="text-right">Có trong nhóm</TableHead>
                    <TableHead className="text-right">Đã chấm</TableHead>
                    <TableHead className="text-right">Đúng</TableHead>
                    <TableHead className="text-right">Sai</TableHead>
                    <TableHead className="text-right">Thiếu dữ liệu</TableHead>
                    <TableHead className="text-right">Ngoài phạm vi</TableHead>
                    <TableHead>Tỷ lệ sai</TableHead>
                    <TableHead>Nhãn SLM sai</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {report.strata.map((stratum) => (
                    <TableRow key={stratum.stratum}>
                      <TableCell className="font-medium text-ink">{stratumLabel(stratum.stratum)}</TableCell>
                      <TableCell className="tabular text-right">{formatInt(stratum.population)}</TableCell>
                      <TableCell className="tabular text-right">{formatInt(stratum.graded)}</TableCell>
                      <TableCell className="tabular text-right">{formatInt(stratum.answers.correct)}</TableCell>
                      <TableCell className="tabular text-right">{formatInt(stratum.answers.wrong)}</TableCell>
                      <TableCell className="tabular text-right">{formatInt(stratum.answers.missingData)}</TableCell>
                      <TableCell className="tabular text-right">{formatInt(stratum.answers.outOfScope)}</TableCell>
                      <TableCell><RateCell estimate={stratum.answerWrongRate} /></TableCell>
                      <TableCell><RateCell estimate={stratum.labelWrongRate} /></TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </CardContent>
          </Card>

          <div className="grid gap-4 lg:grid-cols-2">
            <Card>
              <CardHeader>
                <CardTitle><h2 className="text-base font-semibold text-ink">Vì sao câu trả lời sai</h2></CardTitle>
                <CardDescription>Chỉ đếm các câu được chấm là sai. Số đếm thô trong mẫu, chưa nhân trọng số.</CardDescription>
              </CardHeader>
              <CardContent>
                {report.causes.length === 0 ? (
                  <p className="text-sm text-ink-2">Chưa có câu nào được chấm là sai.</p>
                ) : (
                  <ul className="flex flex-col gap-1.5 text-sm">
                    {report.causes.map((cause) => (
                      <li key={cause.cause} className="flex justify-between gap-2">
                        <span className="text-ink">{causeLabel(cause.cause)}</span>
                        <span className="tabular text-ink-2">
                          {formatInt(cause.count)} câu ({formatPct(totalWrong > 0 ? cause.count / totalWrong : 0, 0)})
                        </span>
                      </li>
                    ))}
                  </ul>
                )}
              </CardContent>
            </Card>

            <Card>
              <CardHeader>
                <CardTitle><h2 className="text-base font-semibold text-ink">Khi SLM gán nhãn sai, nhãn đúng là gì</h2></CardTitle>
                <CardDescription>
                  Cho biết SLM hay nhầm sang loại nào. &ldquo;Không chắc&rdquo; là người chấm không chọn nhãn đúng.
                </CardDescription>
              </CardHeader>
              <CardContent>
                {report.correctIntents.length === 0 ? (
                  <p className="text-sm text-ink-2">Chưa có câu nào được chấm là gán nhãn sai.</p>
                ) : (
                  <ul className="flex flex-col gap-1.5 text-sm">
                    {report.correctIntents.map((item) => (
                      <li key={item.intent} className="flex justify-between gap-2">
                        <span className="text-ink">{intentOrUnknownLabel(item.intent)}</span>
                        <span className="tabular text-ink-2">{formatInt(item.count)} câu</span>
                      </li>
                    ))}
                  </ul>
                )}
              </CardContent>
            </Card>
          </div>

          <details className="rounded-md border border-line">
            <summary className="cursor-pointer px-3 py-2 text-sm font-medium text-ink">Cách đọc các con số</summary>
            <div className="flex flex-col gap-2 border-t border-line p-3 text-sm text-ink-2">
              <p>
                Mỗi nhóm được bốc số câu bằng nhau chứ không theo kích thước nhóm, nên tỷ lệ chung được tính có trọng số theo số câu thật của từng nhóm
                lúc bốc, để nhóm nhỏ không bị tính nặng hơn thực tế.
              </p>
              <p>
                Khoảng tin cậy 95% cho biết mẫu nhỏ thì con số còn rộng đến đâu: &ldquo;khoảng 15% đến 50%&rdquo; nghĩa là tỷ lệ thật của cả nhóm
                nhiều khả năng nằm trong khoảng đó. Nhóm đã chấm hết các câu có trong nhóm thì là số chính xác, không còn khoảng.
              </p>
              <p>
                Câu được chấm &ldquo;Thiếu dữ liệu&rdquo; hoặc &ldquo;Ngoài phạm vi&rdquo; được đếm riêng, không tính là sai nhưng vẫn nằm trong số câu đã chấm.
              </p>
            </div>
          </details>
        </>
      ) : null}
    </div>
  );
}

function RateTile({ label, estimate }: { label: string; estimate: RateEstimate | null }) {
  if (!estimate) return <StatTile label={label} value="Chưa có" />;
  return <StatTile label={label} value={rateText(estimate)} note={`${intervalText(estimate)} · ${countText(estimate)}`} />;
}

function RateCell({ estimate }: { estimate: RateEstimate | null }) {
  if (!estimate) return <span className="text-ink-3">Chưa chấm</span>;
  return (
    <span className="flex flex-col">
      <span className="tabular font-medium text-ink">{rateText(estimate)}</span>
      <span className="text-xs text-ink-3">{intervalText(estimate)}</span>
    </span>
  );
}
