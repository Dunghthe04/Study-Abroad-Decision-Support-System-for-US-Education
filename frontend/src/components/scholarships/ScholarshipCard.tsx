import Link from "next/link";
import { ArrowUpRightIcon, Building2Icon, CalendarClockIcon } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { buttonVariants } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { AMOUNT_TYPE_LABELS, APPLICATION_METHOD_LABELS, coverageLabel, KIND_LABELS } from "@/lib/admin-scholarship-display";
import { formatUsd } from "@/lib/admin-university-display";
import { httpLink } from "@/lib/university-display";
import { cn } from "@/lib/utils";
import type { ScholarshipItem } from "@/types/scholarship";

const LEVEL_LABELS: Record<string, string> = { undergraduate: "Đại học", graduate: "Sau đại học" };
const DAY_MS = 86_400_000;

/** "2026-12-01" → "01/12/2026"; đọc thẳng chuỗi để không lệch ngày theo múi giờ máy người xem. */
function formatDate(value: string): string {
  const [y, m, d] = value.slice(0, 10).split("-");
  return `${d}/${m}/${y}`;
}

// Toàn phần ghi theo kiểu giá trị; còn lại ưu tiên số cố định, rồi khoảng thấp nhất – cao nhất
function amountText(s: ScholarshipItem): string | null {
  if (s.amountType === "full_tuition" || s.amountType === "full_ride") return AMOUNT_TYPE_LABELS[s.amountType];
  if (s.amountUsd !== null) return formatUsd(s.amountUsd);
  if (s.amountMinUsd !== null && s.amountMaxUsd !== null) return `${formatUsd(s.amountMinUsd)} – ${formatUsd(s.amountMaxUsd)}`;
  if (s.amountMaxUsd !== null) return `Đến ${formatUsd(s.amountMaxUsd)}`;
  if (s.amountMinUsd !== null) return `Từ ${formatUsd(s.amountMinUsd)}`;
  return s.amountType ? (AMOUNT_TYPE_LABELS[s.amountType] ?? null) : null;
}

function deadlineStatus(deadline: string | null, today: string): { label: string; tone: "ok" | "warn" | "risk" | "neutral" } {
  if (!deadline) return { label: "Xét quanh năm", tone: "neutral" };
  const days = Math.round((Date.parse(deadline.slice(0, 10)) - Date.parse(today)) / DAY_MS);
  if (days < 0) return { label: "Đã hết hạn", tone: "risk" };
  if (days === 0) return { label: "Hết hạn hôm nay", tone: "warn" };
  if (days <= 30) return { label: `Còn ${days} ngày`, tone: "warn" };
  return { label: `Còn ${days} ngày`, tone: "ok" };
}

function Fact({ label, value, fallback }: { label: string; value: string | null; fallback: string }) {
  return (
    <div className="min-w-0">
      <dt className="text-xs text-muted-foreground">{label}</dt>
      <dd className={cn("text-sm font-semibold text-pretty",value == null ? "font-normal text-muted-foreground/60" : "text-foreground")}>
        {value ?? fallback}
      </dd>
    </div>
  );
}

/** showSchool=false khi thẻ nằm ngay trong trang của trường đó: bỏ tên trường và nút "Xem trường" cho đỡ lặp. */
export function ScholarshipCard({ scholarship: s, today, showSchool = true }: { scholarship: ScholarshipItem; today: string; showSchool?: boolean }) {
  const amount = amountText(s);
  const status = deadlineStatus(s.deadline, today);
  const applyLink = httpLink(s.applyUrl);
  const sourceLink = httpLink(s.sourceUrl);
  const schoolHref = showSchool && s.universityCode ? `/university/${encodeURIComponent(s.universityCode)}?from=scholarships` : null;
  const duration = [
    s.durationYears !== null ? `${s.durationYears} năm` : null,
    s.renewable === null ? null : s.renewable ? "có gia hạn" : "không gia hạn",
  ]
    .filter(Boolean)
    .join(", ");

  return (
    <Card className="gap-0 py-0 transition-[box-shadow,ring-color] duration-150 hover:shadow-sm hover:ring-brand/45">
      <div className="grid gap-5 p-5 sm:grid-cols-[minmax(0,1fr)_auto] sm:gap-8">
        <div className="min-w-0 space-y-2">
          <div className="flex flex-wrap gap-1.5">
            <Badge variant="neutral">{LEVEL_LABELS[s.studyLevel] ?? s.studyLevel}</Badge>
            {s.kind && <Badge variant="neutral">{KIND_LABELS[s.kind] ?? s.kind}</Badge>}
            {s.vnEligible === true && <Badge variant="brand">Mở cho du học sinh Việt Nam</Badge>}
          </div>
          <h3 className="font-heading text-lg font-bold leading-snug text-balance text-foreground">{s.name}</h3>
          {(showSchool || s.provider) && (
            <p className="flex min-w-0 items-center gap-1.5 text-sm text-muted-foreground">
              <Building2Icon className="size-4 shrink-0" aria-hidden />
              {schoolHref ? (
                <Link href={schoolHref} className="truncate font-medium text-foreground underline-offset-4 hover:text-brand hover:underline">
                  {s.universityName}
                </Link>
              ) : (
                <span className="truncate">{s.provider ?? "Chưa rõ đơn vị cấp"}</span>
              )}
            </p>
          )}
        </div>

        {/* Hai điều người xem cần nhất: được bao nhiêu và còn bao lâu */}
        <div className="flex items-end justify-between gap-4 border-t border-line pt-4 sm:flex-col sm:items-end sm:justify-start sm:gap-2 sm:border-t-0 sm:pt-0 sm:text-right">
          <div>
            {/* Chỉ con số mới in cỡ lớn; giá trị dạng chữ ("Thay đổi theo hồ sơ") để cỡ vừa */}
            <p
              className={cn(
                "font-heading font-bold leading-tight tabular-nums",
                amount?.startsWith("$") ? "text-2xl text-brand" : "text-base text-foreground",
              )}
            >
              {amount ?? "Chưa rõ giá trị"}
            </p>
            <p className="text-xs text-muted-foreground">
              {s.coverageType ? `Hỗ trợ ${coverageLabel(s.coverageType).toLowerCase()}` : "Giá trị học bổng"}
            </p>
          </div>
          <Badge variant={status.tone} className="gap-1">
            <CalendarClockIcon className="size-3.5" aria-hidden />
            {status.label}
          </Badge>
        </div>
      </div>

      <dl className="grid grid-cols-2 gap-x-6 gap-y-3 border-t border-line px-5 py-4 md:grid-cols-5">
        <div className="min-w-0">
          <dt className="text-xs text-muted-foreground">Hạn nộp</dt>
          <dd className="text-sm font-semibold text-foreground tabular-nums">{s.deadline ? formatDate(s.deadline) : "Xét quanh năm"}</dd>
        </div>
        <Fact label="GPA tối thiểu" value={s.minGpa4 !== null ? Number(s.minGpa4).toFixed(1) : null} fallback="Không yêu cầu" />
        <Fact label="IELTS tối thiểu" value={s.minIelts !== null ? Number(s.minIelts).toFixed(1) : null} fallback="Không yêu cầu" />
        <Fact label="Thời gian nhận" value={duration || null} fallback="Chưa rõ" />
        <Fact
          label="Cách nộp"
          value={s.applicationMethod ? (APPLICATION_METHOD_LABELS[s.applicationMethod] ?? s.applicationMethod) : null}
          fallback="Chưa rõ"
        />
      </dl>

      {s.eligibilityNotes && (
        <p className="line-clamp-2 px-5 pb-4 text-sm leading-relaxed text-ink-2" title={s.eligibilityNotes}>
          {s.eligibilityNotes}
        </p>
      )}

      <div className="flex flex-wrap items-center justify-between gap-3 border-t border-line bg-ground/60 px-5 py-3">
        <p className="min-w-0 text-xs text-muted-foreground">
          Nguồn:{" "}
          {sourceLink ? (
            <a href={sourceLink.href} target="_blank" rel="noopener noreferrer" className="font-medium text-foreground underline-offset-4 hover:underline">
              {sourceLink.host}
            </a>
          ) : (
            "chưa có liên kết"
          )}
          {s.retrievedAt && <span className="tabular-nums"> · cập nhật {formatDate(s.retrievedAt)}</span>}
        </p>
        <div className="flex gap-2 max-sm:w-full">
          {schoolHref && (
            <Link href={schoolHref} className={cn(buttonVariants({ variant: "outline" }), "max-sm:flex-1")}>
              Xem trường
            </Link>
          )}
          {applyLink && (
            <a
              href={applyLink.href}
              target="_blank"
              rel="noopener noreferrer"
              className={cn(buttonVariants({ variant: "default" }), "gap-1.5 max-sm:flex-1")}
            >
              Nộp hồ sơ
              <ArrowUpRightIcon className="size-4" aria-hidden />
              <span className="sr-only">(mở trang {applyLink.host})</span>
            </a>
          )}
        </div>
      </div>
    </Card>
  );
}
