const CONTROL_LABELS: Record<string, string> = {
  public: "Công lập",
  private: "Tư thục",
};

export function controlLabel(control: string | null): string {
  if (!control) {
    return "—";
  }
  return CONTROL_LABELS[control] ?? control;
}

export function formatAcceptanceRate(rate: number | null): string {
  if (rate === null) {
    return "—";
  }
  const percent = rate * 100;
  return `${percent.toFixed(1)}%`;
}

export function formatRetrievedAt(isoDate: string | null): string {
  if (!isoDate) {
    return "—";
  }
  const parts = isoDate.split("-");
  if (parts.length !== 3) {
    return isoDate;
  }
  const [year, month, day] = parts;
  return `${day}/${month}/${year}`;
}

const SAT_POLICY_LABELS: Record<string, string> = {
  required: "Bắt buộc",
  optional: "Không bắt buộc (test-optional)",
  not_accepted: "Không nhận SAT",
};

export function satPolicyLabel(policy: string | null): string {
  if (!policy) {
    return "—";
  }
  return SAT_POLICY_LABELS[policy] ?? policy;
}

const ACT_POLICY_LABELS: Record<string, string> = {
  required: "Bắt buộc",
  optional: "Không bắt buộc (test-optional)",
  not_accepted: "Không nhận ACT",
};

export function actPolicyLabel(policy: string | null): string {
  if (!policy) {
    return "—";
  }
  return ACT_POLICY_LABELS[policy] ?? policy;
}

const SOURCE_RELIABILITY_LABELS: Record<string, string> = {
  official: "Chính thức",
  aggregated: "Tổng hợp",
  self_reported: "Tự khai",
};

export function sourceReliabilityLabel(reliability: string | null): string {
  if (!reliability) {
    return "Chưa rõ";
  }
  return SOURCE_RELIABILITY_LABELS[reliability] ?? reliability;
}

export function formatLastEdited(isoDateTime: string | null, editorName: string | null): string {
  if (!isoDateTime) {
    return "—";
  }
  const editedAt = new Date(isoDateTime).toLocaleString("vi-VN");
  if (!editorName) {
    return editedAt;
  }
  return `${editorName} · ${editedAt}`;
}

export function formatUsd(amount: number | null): string {
  if (amount === null) {
    return "—";
  }
  return `$${amount.toLocaleString("en-US")}`;
}

export function formatNumber(value: number | null): string {
  if (value === null) {
    return "—";
  }
  return String(value);
}

export function formatSatRange(low: number | null, high: number | null): string {
  if (low === null && high === null) {
    return "—";
  }
  return `${low ?? "?"} – ${high ?? "?"}`;
}

export interface DeadlineEntry {
  round: string;
  date: string;
}

export function parseDeadlines(deadlinesJson: string | null): DeadlineEntry[] {
  if (!deadlinesJson) {
    return [];
  }
  try {
    const parsed: unknown = JSON.parse(deadlinesJson);
    if (parsed === null || typeof parsed !== "object" || Array.isArray(parsed)) {
      return [];
    }
    const entries: DeadlineEntry[] = [];
    for (const [round, value] of Object.entries(parsed as Record<string, unknown>)) {
      if (typeof value === "string") {
        entries.push({ round, date: value });
      }
    }
    return entries;
  } catch {
    return [];
  }
}

export interface LabeledOption {
  value: string;
  label: string;
}

function buildOptions(emptyLabel: string, labels: Record<string, string>): LabeledOption[] {
  const options: LabeledOption[] = [{ value: "", label: emptyLabel }];
  for (const [value, label] of Object.entries(labels)) {
    options.push({ value, label });
  }
  return options;
}

export const CONTROL_OPTIONS = buildOptions("Chưa rõ", CONTROL_LABELS);
export const CONTROL_FILTER_OPTIONS = buildOptions("Mọi loại trường", CONTROL_LABELS);
export const SAT_POLICY_OPTIONS = buildOptions("Chưa rõ", SAT_POLICY_LABELS);
export const ACT_POLICY_OPTIONS = buildOptions("Chưa rõ", ACT_POLICY_LABELS);
export const SOURCE_RELIABILITY_OPTIONS = buildOptions("Chưa rõ", SOURCE_RELIABILITY_LABELS);

// Các trường lấy từ CDS: mức coi trọng (C7), thang GPA, chính sách hỗ trợ, bối cảnh khuôn viên
const IMPORTANCE_LABELS: Record<string, string> = {
  very_important: "Rất quan trọng",
  important: "Quan trọng",
  considered: "Có xem xét",
  not_considered: "Không xem xét",
};
const GPA_SCALE_LABELS: Record<string, string> = {
  weighted: "Có trọng số (weighted)",
  unweighted: "Không trọng số (unweighted)",
  both: "Cả hai thang",
};
const AID_POLICY_LABELS: Record<string, string> = {
  need_and_merit: "Theo nhu cầu & thành tích",
  merit_only: "Chỉ theo thành tích",
  need_only: "Chỉ theo nhu cầu",
  none: "Không hỗ trợ",
};
const CAMPUS_SETTING_LABELS: Record<string, string> = {
  "City: Large": "Đô thị lớn",
  "City: Midsize": "Đô thị vừa",
  "City: Small": "Đô thị nhỏ",
  "Suburb: Large": "Ngoại ô lớn",
  "Suburb: Midsize": "Ngoại ô vừa",
  "Suburb: Small": "Ngoại ô nhỏ",
  "Town: Fringe": "Thị trấn gần đô thị",
  "Town: Distant": "Thị trấn xa đô thị",
  "Town: Remote": "Thị trấn biệt lập",
  "Rural: Fringe": "Nông thôn gần đô thị",
  "Rural: Distant": "Nông thôn xa đô thị",
  "Rural: Remote": "Nông thôn biệt lập",
};
const INTL_HOUSING_LABELS: Record<string, string> = { true: "Có", false: "Không" };

function labelOf(labels: Record<string, string>, value: string | null): string {
  return value ? (labels[value] ?? value) : "—";
}
export const importanceLabel = (v: string | null) => labelOf(IMPORTANCE_LABELS, v);
export const gpaScaleLabel = (v: string | null) => labelOf(GPA_SCALE_LABELS, v);
export const aidPolicyLabel = (v: string | null) => labelOf(AID_POLICY_LABELS, v);
export const campusSettingLabel = (v: string | null) => labelOf(CAMPUS_SETTING_LABELS, v);
export const intlHousingLabel = (v: boolean | null) => (v === null ? "—" : v ? "Có" : "Không");

export const IMPORTANCE_OPTIONS = buildOptions("Chưa rõ", IMPORTANCE_LABELS);
export const GPA_SCALE_OPTIONS = buildOptions("Chưa rõ", GPA_SCALE_LABELS);
export const AID_POLICY_OPTIONS = buildOptions("Chưa rõ", AID_POLICY_LABELS);
export const CAMPUS_SETTING_OPTIONS = buildOptions("Chưa rõ", CAMPUS_SETTING_LABELS);
export const INTL_HOUSING_OPTIONS = buildOptions("Chưa rõ", INTL_HOUSING_LABELS);

/** Tỉ lệ 0–1 hiển thị dạng phần trăm; không có thì "—". */
export function formatFraction(value: number | null): string {
  return value === null ? "—" : `${(value * 100).toFixed(1)}%`;
}
