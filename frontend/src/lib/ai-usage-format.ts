// Định dạng số, ngày và gom dữ liệu cho trang Token và chi phí AI. Toàn hàm thuần, không đụng React.

import type { AiUsageByPurpose, AiUsageMetrics, AiUsageOverview } from "@/types/ai-usage";

// ------------------------------------------------------------------ số
const int = new Intl.NumberFormat("vi-VN");

export function formatInt(n: number): string {
  return int.format(Math.round(n));
}

/** 1.284 · 12,9K · 3,4M. Dùng cho số lớn trên thẻ và trục. */
export function formatCompact(n: number): string {
  const abs = Math.abs(n);
  if (abs < 10_000) return formatInt(n);
  const [value, unit] = abs >= 1e9 ? [n / 1e9, "B"] : abs >= 1e6 ? [n / 1e6, "M"] : [n / 1e3, "K"];
  const text = value >= 100 ? Math.round(value).toString() : (Math.round(value * 10) / 10).toString().replace(".", ",");
  return `${text}${unit}`;
}

/** USD: tiền mỗi lần gọi rất nhỏ nên số lẻ tự co theo độ lớn ($1.234 · $12,34 · $0,0432 · $0,00045). */
export function formatUsd(n: number): string {
  const abs = Math.abs(n);
  const digits = abs >= 100 ? 0 : abs >= 1 ? 2 : abs >= 0.01 ? 3 : abs === 0 ? 0 : 5;
  return `$${n.toLocaleString("vi-VN", { minimumFractionDigits: 0, maximumFractionDigits: digits })}`;
}

export function formatMs(ms: number): string {
  if (ms < 1000) return `${Math.round(ms)} ms`;
  return `${(Math.round(ms / 100) / 10).toString().replace(".", ",")} s`;
}

export function formatPct(ratio: number, digits = 1): string {
  return `${(Math.round(ratio * 100 * 10 ** digits) / 10 ** digits).toString().replace(".", ",")}%`;
}

export function ratio(part: number, whole: number): number {
  return whole > 0 ? part / whole : 0;
}

/** Token vào + ra của một nhóm. */
export function totalTokens(m: AiUsageMetrics): number {
  return m.inputTokens + m.outputTokens;
}

// ------------------------------------------------------------------ ngày (UTC, "yyyy-MM-dd")
const DAY_MS = 86_400_000;

function toTime(iso: string): number {
  return Date.parse(`${iso}T00:00:00Z`);
}

function fromTime(time: number): string {
  return new Date(time).toISOString().slice(0, 10);
}

export function utcToday(): string {
  return new Date().toISOString().slice(0, 10);
}

export function addDays(iso: string, days: number): string {
  return fromTime(toTime(iso) + days * DAY_MS);
}

export function daysBetween(from: string, to: string): number {
  return Math.round((toTime(to) - toTime(from)) / DAY_MS) + 1; // gồm cả hai đầu
}

export function eachDay(from: string, to: string): string[] {
  const out: string[] = [];
  for (let day = from; day <= to; day = addDays(day, 1)) out.push(day);
  return out;
}

/** 0 = Thứ hai … 6 = Chủ nhật. */
export function weekdayMon0(iso: string): number {
  return (new Date(toTime(iso)).getUTCDay() + 6) % 7;
}

export function rangeOfLastDays(days: number): { from: string; to: string } {
  const to = utcToday();
  return { from: addDays(to, -(days - 1)), to };
}

/** Khoảng liền trước, dài bằng khoảng đang xem (để so sánh). */
export function previousRange(range: { from: string; to: string }): { from: string; to: string } {
  const length = daysBetween(range.from, range.to);
  return { from: addDays(range.from, -length), to: addDays(range.from, -1) };
}

export const MAX_RANGE_DAYS = 366; // khớp AiUsageReportService.MaxDays

/** "dd/MM" cho nhãn trục và tooltip. */
export function formatDay(iso: string): string {
  return `${iso.slice(8, 10)}/${iso.slice(5, 7)}`;
}

export function formatDayFull(iso: string): string {
  return `${iso.slice(8, 10)}/${iso.slice(5, 7)}/${iso.slice(0, 4)}`;
}

export function formatDateTime(iso: string): string {
  const date = new Date(iso);
  return Number.isNaN(date.getTime())
    ? iso
    : date.toLocaleString("vi-VN", { day: "2-digit", month: "2-digit", hour: "2-digit", minute: "2-digit" });
}

// ------------------------------------------------------------------ nguồn và tính năng
export const CLOUD_KIND = "claude";
export const LOCAL_KIND = "ollama";

export function kindLabel(kind: string): string {
  if (kind === CLOUD_KIND) return "Cloud (Claude)";
  if (kind === LOCAL_KIND) return "Local (Ollama)";
  return kind;
}

// Tên dễ đọc của các skill đã biết; tên lạ giữ nguyên (bảng vẫn hiện tên gốc)
const PURPOSE_LABELS: Record<string, string> = {
  slm_intents: "Phân loại tin nhắn",
  skill_tra_loi: "Trả lời kiến thức",
  skill_explain: "Giải thích từng trường",
  skill_kich_ban: "Kịch bản what-if",
  skill_goal_seek: "Cần gì để vào trường",
  skill_so_sanh: "So sánh trường",
  skill_ho_so: "Đọc thông tin hồ sơ",
  skill_ho_so_khuon: "Tư vấn hồ sơ",
  viet_tra_loi_ho_so: "Viết tư vấn hồ sơ",
  skill_danh_gia: "Đánh giá hồ sơ",
  skill_lo_trinh: "Lộ trình",
  skill_ec: "Đọc ngoại khóa",
  skill_bac_hoc: "Đọc bậc học",
  skill_nhu_cau: "Đọc nhu cầu",
  skill_chon_truong: "Chọn trường để giải thích",
  skill_giai_thich_trung_tam: "Giải thích trung tâm",
  transcript_ocr_read: "Đọc học bạ (OCR)",
};

export const UNLABELED_PURPOSE = "(chưa gắn nhãn)";

export function purposeLabel(purpose: string | null): string {
  if (!purpose) return UNLABELED_PURPOSE;
  return PURPOSE_LABELS[purpose] ?? purpose;
}

// ------------------------------------------------------------------ gom dữ liệu để vẽ
export interface DailyPoint {
  date: string;
  cloudCalls: number;
  localCalls: number;
  otherCalls: number;
  calls: number;
  failed: number;
  cost: number;
  inputTokens: number;
  outputTokens: number;
}

/** Mỗi ngày trong khoảng một điểm (ngày không có lần gọi nào là 0), để trục thời gian không bị hở. */
export function dailySeries(overview: AiUsageOverview): DailyPoint[] {
  const byDate = new Map<string, DailyPoint>();
  for (const date of eachDay(overview.from, overview.to)) {
    byDate.set(date, {
      date, cloudCalls: 0, localCalls: 0, otherCalls: 0, calls: 0, failed: 0, cost: 0,
      inputTokens: 0, outputTokens: 0,
    });
  }
  for (const row of overview.byDay) {
    const point = byDate.get(row.date.slice(0, 10));
    if (!point) continue;
    const m = row.metrics;
    if (row.kind === CLOUD_KIND) point.cloudCalls += m.calls;
    else if (row.kind === LOCAL_KIND) point.localCalls += m.calls;
    else point.otherCalls += m.calls;
    point.calls += m.calls;
    point.failed += m.failedCalls;
    point.cost += m.costUsd;
    point.inputTokens += m.inputTokens;
    point.outputTokens += m.outputTokens;
  }
  return [...byDate.values()];
}

export interface PurposeRow {
  key: string;
  purpose: string | null;
  label: string;
  kind: string;
  metrics: AiUsageMetrics;
}

export function purposeRows(rows: AiUsageByPurpose[]): PurposeRow[] {
  return rows.map((row) => ({
    key: `${row.kind}:${row.purpose ?? ""}`,
    purpose: row.purpose,
    label: purposeLabel(row.purpose),
    kind: row.kind,
    metrics: row.metrics,
  }));
}

export type PurposeMetric = "cost" | "tokens" | "calls" | "latency";

export const PURPOSE_METRICS: { id: PurposeMetric; label: string }[] = [
  { id: "cost", label: "Chi phí" },
  { id: "tokens", label: "Token" },
  { id: "calls", label: "Lần gọi" },
  { id: "latency", label: "Thời gian" },
];

export function purposeValue(metric: PurposeMetric, m: AiUsageMetrics): number {
  switch (metric) {
    case "cost": return m.costUsd;
    case "tokens": return totalTokens(m);
    case "calls": return m.calls;
    case "latency": return m.avgLatencyMs;
  }
}

export function formatPurposeValue(metric: PurposeMetric, value: number): string {
  switch (metric) {
    case "cost": return formatUsd(value);
    case "tokens": return formatCompact(value);
    case "calls": return formatInt(value);
    case "latency": return formatMs(value);
  }
}

/** Thay đổi so với kỳ trước, tính theo tỉ lệ. null khi kỳ trước bằng 0 (không có gốc để so). */
export function changeRatio(current: number, previous: number): number | null {
  if (previous <= 0) return null;
  return (current - previous) / previous;
}
