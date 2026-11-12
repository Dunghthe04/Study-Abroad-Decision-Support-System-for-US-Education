"use client";

import { useState } from "react";
import Link from "next/link";
import { ArrowLeftIcon, CheckIcon, ShieldAlertIcon } from "lucide-react";
import { cn } from "cn";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Block, ModelLabels, Text } from "@/components/admin/chat-review/ReviewDetailSheet";
import { ProgressBar } from "@/components/admin/review-batches/ProgressBar";
import { VerdictForm } from "@/components/admin/review-batches/VerdictForm";
import { formatDateTime } from "@/lib/ai-usage-format";
import { adminReviewBatchApi } from "@/lib/review-batch-api";
import { stratumLabel } from "@/lib/review-batch-format";
import { useFetched } from "@/lib/use-fetched";
import type { ReviewItemRow } from "@/types/review-batch";

const ROLE_LABELS: Record<string, string> = { user: "Học sinh", assistant: "Hệ thống" };

/**
 * Màn chấm một đợt: danh sách câu bên trái, câu đang chấm bên phải. Nội dung chỉ được tải khi người duyệt chủ động chọn một câu
 * (bấm vào câu, "Bắt đầu chấm" hoặc "Lưu và sang câu kế"), vì mỗi lần tải được ghi nhật ký. Không tải trước các câu sau.
 */
export function ReviewGradingView({ batchId }: { batchId: string }) {
  const [version, setVersion] = useState(0);   // tăng sau mỗi lần lưu để tải lại danh sách câu và tiến độ
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const items = useFetched(`items|${batchId}|${version}`, () => adminReviewBatchApi.items(batchId));
  const list = items.data;

  const firstUngraded = list?.items.find((item) => !item.gradedByMe) ?? null;

  // Câu chưa chấm kế tiếp sau câu vừa lưu: ưu tiên câu đứng sau nó, hết thì quay lại câu chưa chấm đầu tiên (không tính câu vừa lưu)
  const nextAfter = (savedId: string): ReviewItemRow | null => {
    if (!list) return null;
    const open = list.items.filter((item) => !item.gradedByMe && item.itemId !== savedId);
    const saved = list.items.find((item) => item.itemId === savedId);
    return open.find((item) => item.position > (saved?.position ?? 0)) ?? open[0] ?? null;
  };

  const handleSaved = (savedId: string) => {
    const next = nextAfter(savedId);
    setSelectedId(next?.itemId ?? null);
    setVersion((current) => current + 1);
  };

  const selected = list?.items.find((item) => item.itemId === selectedId) ?? null;

  return (
    <div className="flex flex-col gap-4">
      <header className="flex flex-col gap-2">
        <Link href="/admin/review-batches" className="inline-flex w-fit items-center gap-1 text-sm text-ink-2 hover:text-ink">
          <ArrowLeftIcon aria-hidden className="size-4" /> Các đợt
        </Link>
        <h1 className="text-h2">{list?.batchName ?? "Chấm đợt duyệt mẫu"}</h1>
        {list ? <ProgressBar graded={list.myGraded} total={list.totalItems} label="Bạn đã chấm" /> : null}
        <p className="flex items-center gap-1.5 text-xs text-ink-3">
          <ShieldAlertIcon aria-hidden className="size-3.5" />
          Đây là nội dung chat của học sinh. Mỗi lần mở một câu được ghi nhật ký. Màn này không hiển thị học sinh là ai.
        </p>
      </header>

      {items.error ? (
        <Alert variant="destructive">
          <AlertDescription>
            {items.error} Có thể đợt không tồn tại hoặc bạn không được giao duyệt đợt này.
          </AlertDescription>
        </Alert>
      ) : null}
      {!list && !items.error ? <p className="text-sm text-ink-2">Đang tải...</p> : null}

      {list ? (
        <div className="grid gap-4 lg:grid-cols-[280px_minmax(0,1fr)]">
          <nav aria-label="Các câu trong đợt" data-loading={items.loading ? "true" : "false"} className="data-[loading=true]:opacity-60">
            <ul className="flex max-h-[70vh] flex-col gap-1 overflow-y-auto rounded-md border border-line p-1">
              {list.items.map((item) => (
                <li key={item.itemId}>
                  <button
                    type="button"
                    aria-current={item.itemId === selectedId ? "true" : undefined}
                    onClick={() => setSelectedId(item.itemId)}
                    className={cn(
                      "flex w-full items-center justify-between gap-2 rounded px-2.5 py-2 text-left text-sm outline-none focus-visible:ring-2 focus-visible:ring-ring",
                      item.itemId === selectedId ? "bg-brand-wash text-brand-ink" : "text-ink hover:bg-ground",
                    )}
                  >
                    <span className="min-w-0">
                      <span className="tabular block font-medium">Câu {item.position}</span>
                      <span className="block truncate text-xs text-ink-3">{stratumLabel(item.stratum)}</span>
                    </span>
                    {item.gradedByMe ? (
                      <span className="inline-flex shrink-0 items-center gap-1 text-xs text-ok"><CheckIcon aria-hidden className="size-3.5" /> Đã chấm</span>
                    ) : null}
                  </button>
                </li>
              ))}
            </ul>
          </nav>

          <div className="min-w-0">
            {selected ? (
              <ItemPanel
                key={selected.itemId}
                batchId={batchId}
                item={selected}
                isLastToGrade={nextAfter(selected.itemId) === null}
                onSaved={() => handleSaved(selected.itemId)}
              />
            ) : (
              <Card>
                <CardContent className="flex flex-col items-start gap-3 py-6">
                  {firstUngraded ? (
                    <>
                      <p className="text-ink">Chọn một câu ở bên trái, hoặc bắt đầu từ câu chưa chấm đầu tiên.</p>
                      <Button type="button" onClick={() => setSelectedId(firstUngraded.itemId)}>
                        {list.myGraded > 0 ? "Chấm tiếp" : "Bắt đầu chấm"}
                      </Button>
                    </>
                  ) : (
                    <p className="text-ink">Bạn đã chấm hết các câu của đợt này. Chọn một câu ở bên trái nếu muốn xem lại hoặc sửa.</p>
                  )}
                </CardContent>
              </Card>
            )}
          </div>
        </div>
      ) : null}
    </div>
  );
}

function ItemPanel({
  batchId, item, isLastToGrade, onSaved,
}: { batchId: string; item: ReviewItemRow; isLastToGrade: boolean; onSaved: () => void }) {
  // Nội dung chat chỉ nằm trong bộ nhớ của khung này, mất khi đổi câu: không ghi vào localStorage hay URL
  const detail = useFetched(`item|${batchId}|${item.itemId}`, () => adminReviewBatchApi.item(batchId, item.itemId));
  const data = detail.data;

  return (
    <Card>
      <CardContent className="flex flex-col gap-4 py-4">
        <div className="flex flex-wrap items-center gap-2 text-sm">
          <span className="tabular font-semibold text-ink">Câu {item.position}</span>
          <span className="text-ink-3">· {stratumLabel(item.stratum)}</span>
        </div>

        {detail.error ? <Alert variant="destructive"><AlertDescription>{detail.error}</AlertDescription></Alert> : null}
        {!data && !detail.error ? <p className="text-sm text-ink-2">Đang tải...</p> : null}

        {data ? (
          <>
            <Block title="Câu hỏi của học sinh">
              {data.question ? (
                <>
                  <Text>{data.question.content}</Text>
                  {data.question.model ? <ModelLabels model={data.question.model} /> : <p className="text-xs text-ink-3">Lượt này không qua SLM nên không có nhãn.</p>}
                </>
              ) : (
                <p className="text-sm text-ink-2">Không có tin hỏi đi kèm.</p>
              )}
            </Block>

            <Block title="Hệ thống đã trả lời">
              <Text>{data.reply.content}</Text>
            </Block>

            {data.context.length > 0 ? (
              <details className="rounded-md border border-line">
                <summary className="cursor-pointer px-3 py-2 text-sm font-medium text-ink">Các tin trước đó ({data.context.length})</summary>
                <ul className="flex flex-col gap-2 border-t border-line p-3">
                  {data.context.map((message) => (
                    <li key={message.messageId} className="text-sm">
                      <span className="text-xs font-medium text-ink-3">{ROLE_LABELS[message.role] ?? message.role} · {formatDateTime(message.createdAt)}</span>
                      <Text>{message.content}</Text>
                    </li>
                  ))}
                </ul>
              </details>
            ) : null}

            <VerdictForm
              key={item.itemId}
              batchId={batchId}
              itemId={item.itemId}
              initial={data.myVerdict}
              isLastToGrade={isLastToGrade}
              onSaved={onSaved}
            />
          </>
        ) : null}
      </CardContent>
    </Card>
  );
}
