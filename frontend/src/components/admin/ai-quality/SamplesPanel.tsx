"use client";

import { useState } from "react";
import Link from "next/link";
import { ShieldCheckIcon } from "lucide-react";
import { cn } from "cn";
import { TablePagination } from "@/components/admin/shared/TablePagination";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import type { DateRange } from "@/lib/ai-usage-api";
import { formatDateTime, formatInt, formatMs } from "@/lib/ai-usage-format";
import { adminAiQualityApi } from "@/lib/ai-quality-api";
import { featureLabel, SAMPLE_OUTCOMES } from "@/lib/ai-quality-format";
import { useFetched } from "@/lib/use-fetched";
import type { AiQualitySample, OutcomeCounts, SampleOutcome } from "@/types/ai-quality";

const PAGE_SIZE = 10;
const LONG_TEXT = 320; // dài hơn thì thu gọn, bấm để xem hết (danh sách trung tâm, câu trả lời dài)

const ROLE_LABELS: Record<string, string> = { user: "Học sinh", assistant: "Trợ lý", system: "Hệ thống" };

/** Một bong bóng hội thoại: học sinh bên phải (màu thương hiệu), trợ lý và hệ thống bên trái (nền trung tính). */
function Bubble({ who, label, text }: { who: "student" | "reply"; label: string; text: string }) {
  const [open, setOpen] = useState(false);
  const long = text.length > LONG_TEXT;
  return (
    <div className={cn("flex max-w-[88%] flex-col gap-1", who === "student" ? "self-end items-end" : "self-start items-start")}>
      <span className="px-1 text-xs font-medium text-ink-3">{label}</span>
      <div
        className={cn(
          "rounded-2xl px-3.5 py-2.5 text-sm leading-relaxed",
          who === "student" ? "rounded-br-sm bg-brand-wash text-brand-ink" : "rounded-bl-sm border border-line bg-surface text-ink",
        )}
      >
        <p className={cn("whitespace-pre-wrap break-words", long && !open && "line-clamp-5")}>{text}</p>
        {long ? (
          <button type="button" onClick={() => setOpen(!open)} className="mt-1 cursor-pointer text-xs font-medium underline underline-offset-2 outline-none focus-visible:ring-2 focus-visible:ring-ring">
            {open ? "Thu gọn" : "Xem hết"}
          </button>
        ) : null}
      </div>
    </div>
  );
}

function SampleCard({ sample }: { sample: AiQualitySample }) {
  const replyLabel = sample.outcome === "failed" ? "Phản hồi của hệ thống" : "Trợ lý trả lời";
  return (
    <li className="flex flex-col gap-3 rounded-lg border border-line bg-surface p-4">
      <div className="flex flex-wrap items-center gap-x-3 gap-y-1 text-xs text-ink-2">
        <span className="tabular font-medium text-ink">{formatDateTime(sample.createdAt)}</span>
        {sample.feature ? <Badge variant="outline">{featureLabel(sample.feature)}</Badge> : null}
        {sample.turnMs !== null ? <span className="ml-auto">phản hồi sau {formatMs(sample.turnMs)}</span> : null}
      </div>

      <div className="flex flex-col gap-3 rounded-md bg-ground p-3">
        {sample.context.length > 0 ? (
          <details className="group text-sm">
            <summary className="cursor-pointer text-xs font-medium text-ink-3 outline-none hover:text-ink focus-visible:ring-2 focus-visible:ring-ring">
              Xem {sample.context.length} tin trước đó
            </summary>
            <div className="mt-2 flex flex-col gap-2 border-l-2 border-line pl-3 opacity-80">
              {sample.context.map((message, index) => (
                <Bubble
                  key={index}
                  who={message.role === "user" ? "student" : "reply"}
                  label={ROLE_LABELS[message.role] ?? message.role}
                  text={message.content}
                />
              ))}
            </div>
          </details>
        ) : null}
        {sample.question !== null ? <Bubble who="student" label="Học sinh hỏi" text={sample.question} /> : null}
        <Bubble who="reply" label={replyLabel} text={sample.answer} />
      </div>
    </li>
  );
}

/**
 * Hội thoại mẫu của nhóm có vấn đề (UC-90): không trả lời được, lỗi, từ chối. Nội dung do backend che thông tin cá nhân,
 * ẩn hội thoại của người dưới 16 tuổi chưa có đồng ý phù hợp, và ghi nhật ký mỗi lần xem. Nhóm trả lời được dùng Duyệt mẫu.
 * counts: số lượt của từng loại trong khoảng (từ báo cáo) để hiện trên ba thẻ chọn loại. Gắn `key` theo khoảng ngày ở nơi dùng
 * để đổi khoảng thì về trang đầu.
 */
export function SamplesPanel({ range, counts }: { range: DateRange; counts: OutcomeCounts | null }) {
  const [outcome, setOutcome] = useState<SampleOutcome>("no_answer");
  const [page, setPage] = useState(1);
  const samples = useFetched(`samples|${range.from}|${range.to}|${outcome}|${page}`, () =>
    adminAiQualityApi.samples(range, outcome, page, PAGE_SIZE),
  );
  const data = samples.data;
  const totalPages = data ? Math.max(1, Math.ceil(data.totalCount / data.pageSize)) : 1;

  return (
    <Card className="min-w-0">
      <CardHeader>
        <CardTitle>
          <h2 className="text-base font-semibold text-ink">Hội thoại mẫu của nhóm có vấn đề</h2>
        </CardTitle>
        <CardDescription>Chọn một loại để đọc từng lượt và biết vì sao hệ thống không trả lời được hoặc bị lỗi.</CardDescription>
      </CardHeader>
      <CardContent className="flex flex-col gap-4">
        <Alert>
          <ShieldCheckIcon />
          <AlertDescription>
            Nội dung đã che thông tin cá nhân (họ tên, email, số điện thoại, ngày sinh...) nhưng việc che theo quy tắc không đảm bảo tuyệt đối.
            Mỗi lần xem được ghi nhật ký. Hội thoại của học sinh dưới 16 tuổi chưa có đồng ý phù hợp của phụ huynh không hiện nội dung.
          </AlertDescription>
        </Alert>

        {/* Ba thẻ chọn loại: mỗi thẻ có màu của loại đó (khớp biểu đồ), số lượt trong khoảng và mô tả ngắn */}
        <div role="tablist" aria-label="Loại kết quả" className="grid gap-2 sm:grid-cols-3">
          {SAMPLE_OUTCOMES.map((entry) => {
            const active = entry.id === outcome;
            return (
              <button
                key={entry.id}
                type="button"
                role="tab"
                aria-selected={active}
                onClick={() => { setOutcome(entry.id); setPage(1); }}
                className={cn(
                  "flex cursor-pointer flex-col gap-1 rounded-lg border-2 p-3 text-left transition-colors outline-none focus-visible:ring-3 focus-visible:ring-ring/50",
                  active ? "border-brand bg-brand-wash" : "border-line bg-surface hover:bg-ground",
                )}
              >
                <span className="flex items-center gap-2 text-sm font-semibold text-ink">
                  <span aria-hidden className="size-3 rounded-[3px]" style={{ background: entry.color }} />
                  {entry.label}
                </span>
                <span className="tabular text-2xl font-semibold leading-none text-ink">{counts ? formatInt(counts[entry.key]) : "—"}</span>
                <span className="text-xs leading-snug text-ink-2">{entry.hint}</span>
              </button>
            );
          })}
        </div>

        {samples.error ? <Alert variant="destructive"><AlertDescription>{samples.error}</AlertDescription></Alert> : null}

        <div data-loading={samples.loading ? "true" : "false"} className="flex flex-col gap-3">
          {data && data.items.length > 0 ? (
            <ul className="flex flex-col gap-4">
              {data.items.map((sample, index) => <SampleCard key={`${sample.createdAt}|${index}`} sample={sample} />)}
            </ul>
          ) : null}
          {data && data.items.length === 0 ? (
            <p className="rounded-md bg-ground px-3 py-8 text-center text-sm text-ink-2">
              {data.hiddenCount > 0
                ? `Không có lượt nào được xem nội dung. ${formatInt(data.hiddenCount)} lượt bị ẩn vì lý do riêng tư.`
                : "Không có lượt nào thuộc loại này trong khoảng đã chọn."}
            </p>
          ) : null}
          {!data && !samples.error ? <p className="rounded-md bg-ground px-3 py-8 text-center text-sm text-ink-2">Đang tải...</p> : null}
        </div>

        {data && data.hiddenCount > 0 && data.items.length > 0 ? (
          <p className="text-xs text-ink-3">
            Đã ẩn nội dung {formatInt(data.hiddenCount)} lượt cùng loại vì lý do riêng tư. Số liệu của các lượt này vẫn nằm trong báo cáo ở trên.
          </p>
        ) : null}

        {data ? (
          <TablePagination
            page={data.page} totalPages={totalPages} totalCount={data.totalCount} itemLabel="lượt xem được"
            disabled={samples.loading} onPageChange={setPage}
          />
        ) : null}

        <p className="text-xs text-ink-3">
          Muốn kiểm tra các câu hệ thống đã trả lời được có thật sự đúng không? Dùng <Link href="/admin/review-batches" className="underline underline-offset-2">Duyệt mẫu</Link>.
        </p>
      </CardContent>
    </Card>
  );
}
