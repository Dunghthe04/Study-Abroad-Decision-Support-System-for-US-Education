"use client";

import { useState, type ReactNode } from "react";
import Image from "next/image";
import { Building2Icon, ExternalLinkIcon } from "lucide-react";
import { cn } from "cn";
import { universityHost, universityNumber, universityPercent, universityUrl } from "@/lib/university-display";
import { Button } from "@/components/ui/button";
import { Table, TableBody, TableCell, TableHead, TableRow } from "@/components/ui/table";

export function SchoolImage({ url, alt, className, priority = false, contain = false }: {
  url: string | null; alt: string; className: string; priority?: boolean; contain?: boolean;
}) {
  const [failed, setFailed] = useState<string | null>(null);
  const source = universityUrl(url);
  return (
    <div className={`relative overflow-hidden ${className}`}>
      {source && failed !== source ? (
        <Image src={source} alt={alt} fill unoptimized priority={priority}
          sizes={contain ? "96px" : "(max-width: 768px) 100vw, 1200px"}
          className={contain ? "object-contain p-2" : "object-cover"} onError={() => setFailed(source)} />
      ) : (
        <div className="absolute inset-0 flex items-center justify-center bg-brand-wash text-brand/50">
          <Building2Icon className={contain ? "size-9" : "size-16"} aria-label={alt} />
        </div>
      )}
    </div>
  );
}

export function DetailSection({
  id,
  title,
  description,
  children,
  className,
}: {
  id?: string;
  title: string;
  description?: string;
  children: ReactNode;
  className?: string;
}) {
  return (
    <section id={id} aria-label={title} className={cn("scroll-mt-32 space-y-4", className)}>
      <div className="space-y-1">
        <div className="flex items-center gap-4">
          <h2 className="font-sans text-xl font-semibold tracking-tight text-ink">{title}</h2>
          <div className="h-px flex-1 bg-line" aria-hidden="true" />
        </div>
        {description && <p className="text-sm leading-relaxed text-ink-2">{description}</p>}
      </div>
      {children}
    </section>
  );
}

export function DetailRows({ rows }: { rows: { label: string; value: ReactNode }[] }) {
  return (
    <Table>
      <TableBody>
        {rows.map((row) => {
          const isMissing = typeof row.value === "string" && (row.value === "—" || row.value === "-");
          return (
            <TableRow key={row.label} className="hover:bg-transparent">
              <TableHead scope="row" className="h-auto w-1/2 py-3 pr-4 pl-0 align-top font-normal whitespace-normal text-muted-foreground">{row.label}</TableHead>
              <TableCell className={cn("py-3 pr-0 text-right align-top font-medium whitespace-normal", isMissing && "text-muted-foreground/40 font-normal")}>{row.value}</TableCell>
            </TableRow>
          );
        })}
      </TableBody>
    </Table>
  );
}

export function GridDataItem({
  icon: Icon,
  label,
  value,
}: {
  icon: React.ComponentType<{ className?: string }>;
  label: string;
  value: React.ReactNode;
}) {
  const isMissing = value == null || value === "" || value === "—" || value === "-";
  return (
    <div className="flex items-center gap-3.5 py-1">
      <Icon className="size-6 shrink-0 text-muted-foreground/80" />
      <div className="min-w-0 space-y-0.5">
        <p className="text-xs text-muted-foreground">{label}</p>
        <p className={cn("text-base font-semibold tracking-tight", isMissing ? "text-muted-foreground/40 font-normal" : "text-foreground")}>
          {isMissing ? "—" : value}
        </p>
      </div>
    </div>
  );
}

export function SourceLink({ url, label }: { url: string | null; label?: string }) {
  const safe = universityUrl(url);
  if (!safe) return null;
  return (
    <Button variant="link" nativeButton={false} role="link" render={<a href={safe} target="_blank" rel="noopener noreferrer" />} className="h-auto max-w-full justify-start gap-1.5 p-0 text-sm whitespace-normal text-brand">
      <span className="min-w-0 break-all">{label ?? universityHost(safe)}</span><ExternalLinkIcon className="size-3.5 shrink-0" />
    </Button>
  );
}

export type GaugeTone = "brand" | "reach" | "warn" | "ok";

export function acceptanceRateTone(rate: number): GaugeTone {
  const pct = rate > 1 ? rate : rate * 100;
  if (pct < 15) return "reach";
  if (pct <= 40) return "warn";
  return "ok";
}

export function Gauge({
  value,
  label,
  tone = "brand",
  colorClassName,
  className,
}: {
  value: number;
  label: string;
  tone?: GaugeTone;
  colorClassName?: string;
  className?: string;
}) {
  const rawFraction = Math.max(0, Math.min(1, value > 1 ? value / 100 : value));
  // Đảm bảo cung quét tối thiểu (ít nhất 7%) để số nhỏ như 4.6% vẫn hiện rõ vệt màu và đầu bo tròn
  const fraction = rawFraction > 0 ? Math.max(0.07, rawFraction) : 0;
  const radius = 42;
  const strokeWidth = 8;
  const arcLength = Math.PI * radius;
  const offset = arcLength * (1 - fraction);
  const formattedPct = universityPercent(rawFraction);

  const toneClasses = {
    brand: { track: "text-line", fill: "text-brand" },
    reach: { track: "text-reach-wash", fill: "text-reach" },
    warn: { track: "text-warn-wash", fill: "text-warn" },
    ok: { track: "text-ok-wash", fill: "text-ok" },
  }[tone];

  const fillClass = colorClassName ?? toneClasses.fill;

  return (
    <svg
      role="img"
      aria-label={`${label}: ${formattedPct}`}
      viewBox="0 0 104 58"
      className={cn("h-auto w-24 shrink-0 overflow-visible sm:w-28", className)}
    >
      <path
        d="M 10 50 A 42 42 0 0 1 94 50"
        fill="none"
        stroke="currentColor"
        strokeWidth={strokeWidth}
        strokeLinecap="round"
        className={toneClasses.track}
      />
      {fraction > 0 && (
        <path
          d="M 10 50 A 42 42 0 0 1 94 50"
          fill="none"
          stroke="currentColor"
          strokeWidth={strokeWidth}
          strokeLinecap="round"
          strokeDasharray={arcLength}
          strokeDashoffset={offset}
          className={fillClass}
        />
      )}
    </svg>
  );
}

export type TrackDifficulty = "risk" | "warn" | "ok";

export function difficultyTone(val: number, min: number, max: number): TrackDifficulty {
  const range = max - min;
  if (range <= 0) return "warn";
  const p = (val - min) / range;
  if (p >= 0.8) return "risk";
  if (p <= 0.25) return "ok";
  return "warn";
}

export function RangeTrack({
  min,
  max,
  low,
  high,
  point,
  fillToMax = false,
  tone,
  showScaleEnds = true,
  ariaLabel,
  className,
}: {
  min: number;
  max: number;
  low?: number | null;
  high?: number | null;
  point?: number | null;
  fillToMax?: boolean;
  tone?: TrackDifficulty;
  showScaleEnds?: boolean;
  ariaLabel: string;
  className?: string;
}) {
  const range = max - min;
  if (range <= 0) return null;

  const toPct = (val: number) => Math.max(0, Math.min(100, ((val - min) / range) * 100));

  const hasRange = low != null && high != null && high >= low;
  const rangeLeft = hasRange ? toPct(low!) : 0;
  const rangeWidth = hasRange ? Math.max(1, toPct(high!) - rangeLeft) : 0;

  const hasPoint = point != null && Number.isFinite(point);
  const pointPct = hasPoint ? toPct(point!) : 0;

  // Xác định độ khó và màu tương ứng
  const refValue = hasRange ? (low! + high!) / 2 : hasPoint ? point! : min;
  const resolvedTone: TrackDifficulty = tone ?? difficultyTone(refValue, min, max);
  const difficultyLabel = resolvedTone === "risk" ? "cao" : resolvedTone === "ok" ? "thấp" : "trung bình";

  const colorStyles = {
    risk: { band: "bg-risk/40", dot: "bg-risk" },
    warn: { band: "bg-warn/40", dot: "bg-warn" },
    ok: { band: "bg-ok/40", dot: "bg-ok" },
  }[resolvedTone];

  const isFillToMax = fillToMax || (hasPoint && !hasRange);

  return (
    <div className={cn("w-full space-y-1", className)}>
      <div
        role="img"
        aria-label={`${ariaLabel} (mức khó: ${difficultyLabel})`}
        className="relative h-2.5 w-full min-w-16 rounded-full bg-line"
      >
        {hasRange && (
          <div
            className={cn("absolute inset-y-0 rounded-full", colorStyles.band)}
            style={{ left: `${rangeLeft}%`, width: `${rangeWidth}%` }}
          />
        )}
        {isFillToMax && hasPoint && (
          <div
            className={cn("absolute inset-y-0 rounded-full", colorStyles.band)}
            style={{ left: `${pointPct}%`, width: `${100 - pointPct}%` }}
          />
        )}
        {hasPoint && (
          <div
            className={cn(
              "absolute top-1/2 size-3.5 -translate-x-1/2 -translate-y-1/2 rounded-full ring-2 ring-surface",
              colorStyles.dot
            )}
            style={{ left: `${pointPct}%` }}
          />
        )}
      </div>
      {showScaleEnds && (
        <div className="flex items-center justify-between text-xs text-ink-3 tabular-nums">
          <span>{universityNumber(min)}</span>
          <span>{universityNumber(max)}</span>
        </div>
      )}
    </div>
  );
}

export function GaugeDataItem({
  icon: Icon,
  label,
  value,
  formattedValue,
  tone = "brand",
  colorClassName,
  subtitle,
  gaugeClassName,
  className,
  wrapValue,
}: {
  icon?: React.ComponentType<{ className?: string }>;
  label: string;
  value: number;
  formattedValue?: string;
  tone?: GaugeTone;
  colorClassName?: string;
  subtitle?: string;
  gaugeClassName?: string;
  className?: string;
  wrapValue?: (value: ReactNode) => ReactNode;
}) {
  const display = formattedValue ?? universityPercent(value);
  return (
    <div className={cn("flex items-center justify-between gap-3.5 py-1", className)}>
      <div className="flex items-center gap-3.5 min-w-0">
        {Icon && <Icon className="size-6 shrink-0 text-muted-foreground/80" />}
        <div className="min-w-0 space-y-0.5">
          <p className="text-xs text-ink-3">{label}</p>
          <p className="text-base font-semibold tracking-tight text-ink">{wrapValue ? wrapValue(display) : display}</p>
          {subtitle && <p className="text-xs text-ink-3">{subtitle}</p>}
        </div>
      </div>
      <Gauge
        value={value}
        label={label}
        tone={tone}
        colorClassName={colorClassName}
        className={gaugeClassName}
      />
    </div>
  );
}
