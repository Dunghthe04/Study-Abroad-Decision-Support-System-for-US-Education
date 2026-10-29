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
