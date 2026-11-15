"use client";

import { useState, type ReactNode } from "react";
import { ArrowDownIcon, ArrowUpIcon, MinusIcon, TableIcon, BarChart3Icon } from "lucide-react";
import { cn } from "cn";
import { Button } from "@/components/ui/button";
import { Card, CardAction, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { formatPct } from "@/lib/ai-usage-format";

// ------------------------------------------------------------------ thẻ biểu đồ có bảng song song
export interface TableView {
  columns: string[];
  rows: (string | number)[][];
}

interface ChartCardProps {
  title: string;
  description?: string;
  /** Bảng cùng dữ liệu: cách đọc không cần chuột, và là nơi giữ mọi giá trị tooltip có. */
  table: TableView;
  loading?: boolean;
  /** Điều khiển nhỏ của riêng biểu đồ (đổi chỉ số...). */
  controls?: ReactNode;
  className?: string;
  children: ReactNode;
}

export function ChartCard({ title, description, table, loading, controls, className, children }: ChartCardProps) {
  const [view, setView] = useState<"chart" | "table">("chart");
  const showingTable = view === "table";
  return (
    <Card className={cn("min-w-0", className)}>
      <CardHeader>
        <CardTitle>
          <h2 className="text-base font-semibold text-ink">{title}</h2>
        </CardTitle>
        {description ? <CardDescription>{description}</CardDescription> : null}
        <CardAction>
          <Button
            type="button"
            variant="ghost"
            size="sm"
            aria-pressed={showingTable}
            onClick={() => setView(showingTable ? "chart" : "table")}
          >
            {showingTable ? <BarChart3Icon /> : <TableIcon />}
            {showingTable ? "Xem biểu đồ" : "Xem bảng"}
          </Button>
        </CardAction>
      </CardHeader>
      <CardContent className="flex flex-col gap-3">
        {controls && !showingTable ? controls : null}
        <div data-loading={loading ? "true" : "false"} className="min-w-0">
          {showingTable ? <DataTable table={table} /> : children}
        </div>
      </CardContent>
    </Card>
  );
}

export function DataTable({ table }: { table: TableView }) {
  if (table.rows.length === 0) return <EmptyNote />;
  return (
    <div className="max-h-80 overflow-auto rounded-md border border-line">
      <Table>
        <TableHeader>
          <TableRow>
            {table.columns.map((column, index) => (
              <TableHead key={column} className={index === 0 ? undefined : "text-right"}>{column}</TableHead>
            ))}
          </TableRow>
        </TableHeader>
        <TableBody>
          {table.rows.map((row, rowIndex) => (
            <TableRow key={rowIndex}>
              {row.map((cell, index) => (
                <TableCell key={index} className={cn("tabular", index === 0 ? "font-medium" : "text-right")}>{cell}</TableCell>
              ))}
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </div>
  );
}

export function EmptyNote({ children = "Chưa có lần gọi AI nào trong khoảng này." }: { children?: ReactNode }) {
  return <p className="rounded-md bg-ground px-3 py-8 text-center text-sm text-ink-2">{children}</p>;
}

// ------------------------------------------------------------------ chú giải
export function Legend({ items }: { items: { label: string; color: string }[] }) {
  return (
    <ul className="flex flex-wrap items-center gap-x-4 gap-y-1 text-xs text-ink-2">
      {items.map((item) => (
        <li key={item.label} className="flex items-center gap-1.5">
          <span aria-hidden className="size-2.5 rounded-[3px]" style={{ background: item.color }} />
          {item.label}
        </li>
      ))}
    </ul>
  );
}

// ------------------------------------------------------------------ chọn một trong vài chỉ số
export function Segmented<T extends string>({
  label, value, options, onChange,
}: {
  label: string;
  value: T;
  options: { id: T; label: string }[];
  onChange: (next: T) => void;
}) {
  return (
    <div role="group" aria-label={label} className="inline-flex w-fit rounded-lg border border-line bg-ground p-0.5">
      {options.map((option) => (
        <button
          key={option.id}
          type="button"
          aria-pressed={option.id === value}
          onClick={() => onChange(option.id)}
          className={cn(
            "rounded-md px-3 py-1 text-xs font-medium transition-colors outline-none focus-visible:ring-2 focus-visible:ring-ring",
            option.id === value ? "bg-surface text-ink shadow-xs" : "text-ink-2 hover:text-ink",
          )}
        >
          {option.label}
        </button>
      ))}
    </div>
  );
}

// ------------------------------------------------------------------ thẻ số
interface StatTileProps {
  label: string;
  value: string;
  /** Dòng nhỏ dưới giá trị (ví dụ "cloud · local"). */
  note?: ReactNode;
  /** Thay đổi so với kỳ trước (tỉ lệ), null = không có gốc để so. undefined = không hiện. */
  change?: number | null;
  /** Tăng là tốt, xấu hay chỉ để biết: quyết định màu của mức thay đổi. */
  upIs?: "good" | "bad" | "neutral";
  trend?: number[];
  hero?: boolean;
  loading?: boolean;
}

export function StatTile({ label, value, note, change, upIs = "neutral", trend, hero, loading }: StatTileProps) {
  return (
    <Card className="min-w-0">
      <CardContent data-loading={loading ? "true" : "false"} className="flex h-full flex-col gap-1">
        <p className="text-sm text-ink-2">{label}</p>
        <p className={cn("font-semibold leading-none text-ink", hero ? "text-5xl" : "text-3xl")}>{value}</p>
        <div className="flex min-h-5 flex-wrap items-center gap-x-2 text-xs text-ink-2">
          {change !== undefined ? <Change change={change} upIs={upIs} /> : null}
          {note}
        </div>
        {trend && trend.length > 1 ? <Sparkline values={trend} /> : null}
      </CardContent>
    </Card>
  );
}

function Change({ change, upIs }: { change: number | null; upIs: "good" | "bad" | "neutral" }) {
  if (change === null) return <span className="text-ink-3">chưa có kỳ trước để so</span>;
  const flat = Math.abs(change) < 0.005;
  const up = change > 0;
  // Màu = hướng thay đổi × tăng có tốt không; luôn kèm mũi tên và chữ, không chỉ màu
  const tone = flat || upIs === "neutral" ? "text-ink-2" : (up === (upIs === "good")) ? "text-ok" : "text-risk";
  const Icon = flat ? MinusIcon : up ? ArrowUpIcon : ArrowDownIcon;
  return (
    <span className={cn("inline-flex items-center gap-0.5 font-medium", tone)}>
      <Icon aria-hidden className="size-3" />
      {flat ? "không đổi" : `${up ? "+" : "−"}${formatPct(Math.abs(change), 0)}`}
      <span className="font-normal text-ink-3">so với kỳ trước</span>
    </span>
  );
}

/** Đường xu hướng: màu trung tính, chỉ điểm cuối (kỳ hiện tại) mang màu nhấn. */
function Sparkline({ values }: { values: number[] }) {
  const width = 160;
  const height = 28;
  const pad = 4;
  const max = Math.max(...values, 0);
  const x = (index: number) => pad + (index / (values.length - 1)) * (width - pad * 2);
  const y = (value: number) => height - pad - (max > 0 ? (value / max) * (height - pad * 2) : 0);
  const points = values.map((value, index) => `${x(index)},${y(value)}`).join(" ");
  const last = values.length - 1;
  return (
    <svg aria-hidden width={width} height={height} viewBox={`0 0 ${width} ${height}`} className="mt-1 max-w-full">
      <polyline points={points} fill="none" stroke="var(--viz-muted)" strokeWidth={2} strokeLinejoin="round" strokeLinecap="round" />
      <circle cx={x(last)} cy={y(values[last])} r={4} fill="var(--viz-1)" stroke="var(--viz-surface)" strokeWidth={2} />
    </svg>
  );
}
