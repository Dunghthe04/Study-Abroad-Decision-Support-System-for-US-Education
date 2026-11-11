"use client";

import { useState, type ReactNode } from "react";
import Image from "next/image";
import { Building2Icon, ExternalLinkIcon } from "lucide-react";
import { cn } from "cn";
import { universityHost, universityPercent, universityUrl } from "@/lib/university-display";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
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

export function DetailSection({ id, title, description, children }: { id?: string; title: string; description?: string; children: ReactNode }) {
  return (
    <section id={id} aria-label={title} className="scroll-mt-32">
      <Card className=" [--card-spacing:--spacing(5)] sm:[--card-spacing:--spacing(6)]">
        <CardHeader>
          <CardTitle><h2 className="font-sans text-lg font-semibold tracking-tight">{title}</h2></CardTitle>
          {description && <CardDescription className="leading-relaxed">{description}</CardDescription>}
        </CardHeader>
        <CardContent>{children}</CardContent>
      </Card>
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

export function Gauge({
  value,
  label,
  colorClassName = "text-brand",
  className,
}: {
  value: number;
  label: string;
  colorClassName?: string;
  className?: string;
}) {
  const fraction = Math.max(0, Math.min(1, value > 1 ? value / 100 : value));
  const radius = 42;
  const strokeWidth = 8;
  const arcLength = Math.PI * radius;
  const offset = arcLength * (1 - fraction);
  const formattedPct = universityPercent(fraction);

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
        className="text-line"
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
          className={colorClassName}
        />
      )}
    </svg>
  );
}

export function RangeTrack({
  min,
  max,
  low,
  high,
  point,
  ariaLabel,
  className,
}: {
  min: number;
  max: number;
  low?: number | null;
  high?: number | null;
  point?: number | null;
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

  return (
    <div
      role="img"
      aria-label={ariaLabel}
      className={cn("relative h-2 w-full min-w-16 rounded-full bg-line", className)}
    >
      {hasRange && (
        <div
          className="absolute inset-y-0 rounded-full bg-brand/40"
          style={{ left: `${rangeLeft}%`, width: `${rangeWidth}%` }}
        />
      )}
      {hasPoint && (
        <div
          className="absolute top-1/2 size-3 -translate-x-1/2 -translate-y-1/2 rounded-full bg-brand ring-2 ring-surface"
          style={{ left: `${pointPct}%` }}
        />
      )}
    </div>
  );
}

export function GaugeDataItem({
  label,
  value,
  formattedValue,
  colorClassName = "text-brand",
  subtitle,
}: {
  label: string;
  value: number;
  formattedValue?: string;
  colorClassName?: string;
  subtitle?: string;
}) {
  const display = formattedValue ?? universityPercent(value);
  return (
    <div className="flex items-center justify-between gap-3.5 py-1">
      <div className="min-w-0 space-y-0.5">
        <p className="text-xs text-ink-3">{label}</p>
        <p className="text-base font-semibold tracking-tight text-ink">{display}</p>
        {subtitle && <p className="text-xs text-ink-3">{subtitle}</p>}
      </div>
      <Gauge value={value} label={label} colorClassName={colorClassName} />
    </div>
  );
}
