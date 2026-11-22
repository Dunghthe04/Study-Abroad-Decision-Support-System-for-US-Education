import type {
  SensitivityComparisonWarning,
  SensitivityKind,
  SensitivityLabel,
  SensitivityParameterResult,
  SensitivityPoint,
} from "@/types/sensitivity";

export const STEP_OPTIONS = [
  { value: 0.1, label: "±10%" },
  { value: 0.2, label: "±20%" },
] as const;

/** Thứ tự cột của bảng kết quả. */
export const STEP_COLUMNS = [-0.2, -0.1, 0.1, 0.2] as const;

export const DEFAULT_STABLE_BELOW = 0.02;
export const DEFAULT_WATCH_UP_TO = 0.1;

export const LABELS: Record<SensitivityLabel, { name: string; variant: "ok" | "warn" | "risk" }> = {
  stable: { name: "Ổn định", variant: "ok" },
  watch: { name: "Cần theo dõi", variant: "warn" },
  sensitive: { name: "Nhạy", variant: "risk" },
};

// measure: câu ngắn cho form chọn tham số. explain: ghi chú đầy đủ dưới tiêu đề bảng kết quả, nói rõ cái gì được đếm và cái gì không.
export const KIND_TITLES: Record<SensitivityKind, { title: string; measure: string; explain: string }> = {
  grouping: {
    title: "Tham số xếp nhóm",
    measure: "% cặp (hồ sơ, trường) đổi nhóm Khó vào / Vừa sức / An toàn",
    explain:
      "Các tham số này quyết định trường thuộc nhóm nào. Mỗi ô là % cặp (hồ sơ, trường) bị đổi nhóm, ví dụ từ Vừa sức sang An toàn, " +
      "trên tổng số cặp. Dòng nhỏ là giá trị đã thử.",
  },
  fit_weight: {
    title: "Trọng số điểm phù hợp",
    measure: "% trường bị thay trong 4 trường hiện sẵn của mỗi nhóm",
    explain:
      "Các trọng số này KHÔNG đổi nhóm của trường, chỉ đổi thứ tự trường trong nhóm. Mỗi ô là % chỗ trong 4 trường hiện sẵn đầu mỗi " +
      "nhóm bị thay bằng trường khác. Không tính: các trường chỉ đổi chỗ cho nhau trong 4 trường đó, và thay đổi thứ tự ở phần " +
      "\"Xem thêm\" phía sau. Dòng nhỏ là giá trị đã thử.",
  },
};

export const WARNING_TEXT: Record<SensitivityComparisonWarning, string> = {
  algorithm_version_differs: "Hai lần chạy dùng phiên bản thuật toán khác nhau.",
  profile_set_differs: "Hai lần chạy dùng bộ hồ sơ giả lập khác nhau.",
  school_data_differs: "Dữ liệu trường đã thay đổi giữa hai lần chạy, chênh lệch có thể do dữ liệu chứ không do tham số.",
  thresholds_differ: "Hai lần chạy dùng ngưỡng nhãn khác nhau: nhãn không so được, con số % vẫn so được.",
};

const SAME = 1e-9;

/** −10% · +20% */
export function stepLabel(step: number): string {
  const pct = Math.round(Math.abs(step) * 100);
  return `${step < 0 ? "−" : "+"}${pct}%`;
}

/** 0,0329 → "3,29%" */
export function pct(ratio: number | null | undefined, digits = 2): string {
  if (ratio == null) return "—";
  return `${(Math.round(ratio * 100 * 10 ** digits) / 10 ** digits).toFixed(digits).replace(".", ",")}%`;
}

/** Chênh lệch theo điểm phần trăm, có dấu: "+0,42 điểm %" */
export function deltaPoints(delta: number | null | undefined): string {
  if (delta == null) return "—";
  const value = Math.round(delta * 100 * 100) / 100;
  if (Math.abs(value) < 0.005) return "0";
  return `${value > 0 ? "+" : "−"}${Math.abs(value).toFixed(2).replace(".", ",")} điểm %`;
}

/** Giá trị tham số: 0,825 · 0,055 (tối đa 4 chữ số thập phân, bỏ đuôi lẻ của số thực). */
export function paramValue(value: number | null | undefined): string {
  if (value == null) return "—";
  return (Math.round(value * 10_000) / 10_000).toString().replace(".", ",");
}

export function pointAt(result: SensitivityParameterResult, step: number): SensitivityPoint | undefined {
  return result.points.find((p) => Math.abs(p.step - step) < SAME);
}

/** Tỉ lệ thay đổi của một mức nhích, theo đúng cách đo của loại tham số. */
export function rateOf(kind: SensitivityKind, point: SensitivityPoint): number {
  return kind === "grouping" ? point.groupChangeRate : point.topChangeRate;
}

export function formatRunTime(iso: string): string {
  const date = new Date(iso);
  return Number.isNaN(date.getTime())
    ? iso
    : date.toLocaleString("vi-VN", { day: "2-digit", month: "2-digit", year: "numeric", hour: "2-digit", minute: "2-digit" });
}

/** Kiểm tra trước khi gửi (backend vẫn kiểm tra lại). Trả câu lỗi hoặc null. */
export function validateRequest(keys: string[], steps: number[], stableBelowPct: number, watchUpToPct: number): string | null {
  if (keys.length === 0) return "Chọn ít nhất một tham số để phân tích.";
  if (steps.length === 0) return "Chọn ít nhất một mức nhích (±10% hoặc ±20%).";
  if (!(stableBelowPct > 0 && stableBelowPct < watchUpToPct && watchUpToPct < 100))
    return "Ngưỡng nhãn cần thỏa: 0 < ngưỡng Ổn định < ngưỡng Cần theo dõi < 100%.";
  return null;
}
