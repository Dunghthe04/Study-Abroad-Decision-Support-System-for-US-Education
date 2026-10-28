"use client";

import { cn } from "@/lib/utils";
import {
  DEFAULT_BUDGET_TIERS,
  EDUCATION_LEVEL_OPTIONS,
  EDUCATION_SYSTEM_OPTIONS,
  FUNDING_SOURCE_OPTIONS,
  SCHOLARSHIP_NEED_OPTIONS,
  SCHOOL_TYPE_OPTIONS,
  labelOf,
} from "@/lib/profile-flow-options";
import { formatYearMonth } from "@/lib/certification-utils";
import { studyLevelLabel } from "@/lib/study-levels";

/** Một ô hồ sơ chat: giá trị + trạng thái (confirmed, said_in_B0, none, unknown, declined) */
type Field = { value?: unknown; status?: string };

// Thứ tự và nhãn các dòng trong bảng; khóa giống PROFILE_KEYS bên advisor
const ROWS: { key: string; label: string }[] = [
  { key: "current_level", label: "Đang học" },
  { key: "current_grade", label: "Lớp / năm" },
  { key: "education_system", label: "Hệ học" },
  { key: "target_level", label: "Bậc muốn học" },
  { key: "major", label: "Ngành" },
  { key: "intake", label: "Nhập học" },
  { key: "gpa", label: "GPA" },
  { key: "sat", label: "SAT" },
  { key: "act", label: "ACT" },
  { key: "english", label: "Tiếng Anh" },
  { key: "english_plan", label: "Kế hoạch thi" },
  { key: "budget", label: "Ngân sách / năm" },
  { key: "scholarship", label: "Học bổng" },
  { key: "funding_source", label: "Nguồn tài chính" },
  { key: "activities", label: "Hoạt động" },
  { key: "awards", label: "Giải thưởng" },
  { key: "preferred_states", label: "Bang muốn học" },
  { key: "relatives_state", label: "Người thân ở bang" },
  { key: "school_type", label: "Loại trường" },
  { key: "visa_refused", label: "Từng trượt visa" },
];

const STATUS_TEXT: Record<string, string> = {
  none: "Không có",
  unknown: "Chưa rõ",
  declined: "Không muốn nói",
};

// Học bổng: chat dùng required/preferred/no, form dùng mandatory/preferred/optional
const SCHOLARSHIP_ALIASES: Record<string, string> = { required: "mandatory", no: "optional" };

const CERT_NAMES: Record<string, string> = {
  ielts: "IELTS",
  toefl: "TOEFL",
  duolingo: "Duolingo",
  pte: "PTE",
};

function obj(v: unknown): Record<string, unknown> {
  return v && typeof v === "object" && !Array.isArray(v) ? (v as Record<string, unknown>) : {};
}

function str(v: unknown): string {
  return typeof v === "string" || typeof v === "number" ? String(v) : "";
}

function names(v: unknown): string {
  if (!Array.isArray(v) || v.length === 0) return "";
  return v.map((item) => str(obj(item).name) || str(item)).filter(Boolean).join(", ");
}

/** Đổi giá trị thô của một ô thành chữ dễ đọc; rỗng nếu không đọc được */
function formatValue(key: string, value: unknown): string {
  switch (key) {
    case "current_level":
      return labelOf(EDUCATION_LEVEL_OPTIONS, str(value) as never) || str(value);
    case "education_system":
      return labelOf(EDUCATION_SYSTEM_OPTIONS, str(value) as never) || str(value);
    case "target_level":
      return str(value) && studyLevelLabel(str(value));
    case "gpa": {
      const g = obj(value);
      return str(g.value) && `${str(g.value)}${g.scale ? ` (thang ${str(g.scale)})` : ""}`;
    }
    case "english": {
      const e = obj(value);
      const type = str(e.type).toLowerCase();
      const date = formatYearMonth(str(e.date));
      return [CERT_NAMES[type] ?? str(e.type), str(e.value), date && `· thi ${date}`].filter(Boolean).join(" ");
    }
    case "english_plan": {
      const p = obj(value);
      if (p.planned === false) return "Chưa có kế hoạch";
      const date = formatYearMonth(str(p.date));
      return date ? `Dự định thi ${date}` : p.planned ? "Có dự định thi" : "";
    }
    case "budget":
      return DEFAULT_BUDGET_TIERS.find((t) => t.key === value)?.label ?? str(value);
    case "scholarship": {
      const need = SCHOLARSHIP_ALIASES[str(value)] ?? str(value);
      return labelOf(SCHOLARSHIP_NEED_OPTIONS, need as never) || str(value);
    }
    case "funding_source":
      return labelOf(FUNDING_SOURCE_OPTIONS, str(value) as never) || str(value);
    case "activities":
    case "awards":
      return names(value);
    case "preferred_states":
      return Array.isArray(value) ? value.map(str).filter(Boolean).join(", ") : str(value);
    case "school_type":
      return labelOf(SCHOOL_TYPE_OPTIONS, str(value) as never) || str(value);
    case "visa_refused":
      return value === true ? "Có" : value === false ? "Không" : "";
    default:
      return str(value);
  }
}

/** D4: bảng rà soát hồ sơ trong chat (widget "review" từ skill_ho_so ở câu X1) */
export function ReviewWidget({ data }: { data?: Record<string, unknown> | null }) {
  const profile = obj(data?.profile) as Record<string, Field>;
  const missing = new Set(Array.isArray(data?.missing) ? data.missing.map(str) : []);

  const rows = ROWS.flatMap(({ key, label }) => {
    const field = obj(profile[key]) as Field;
    const isMissing = missing.has(key);
    const text = STATUS_TEXT[field.status ?? ""] ?? formatValue(key, field.value);
    // Bỏ dòng chưa nói gì, trừ dòng bắt buộc còn thiếu
    if (!text && !isMissing) return [];
    return [{ key, label, text: text || "Còn thiếu", isMissing, muted: !isMissing && Boolean(STATUS_TEXT[field.status ?? ""]) }];
  });

  if (rows.length === 0) return null;

  return (
    <div className="mt-2.5 overflow-hidden rounded-xl border border-line bg-surface">
      <div className="border-b border-line bg-ground px-3 py-2 text-[11px] font-semibold tracking-wide text-muted-foreground uppercase">
        Hồ sơ của bạn
      </div>
      <dl className="divide-y divide-line text-[13px]">
        {rows.map((r) => (
          <div key={r.key} className="flex gap-3 px-3 py-1.5">
            <dt className="w-32 shrink-0 text-muted-foreground">{r.label}</dt>
            <dd
              className={cn(
                "min-w-0 flex-1 break-words text-foreground",
                r.muted && "text-muted-foreground italic",
                r.isMissing && "font-medium text-warn"
              )}
            >
              {r.text}
            </dd>
          </div>
        ))}
      </dl>
    </div>
  );
}
