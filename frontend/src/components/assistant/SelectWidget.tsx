"use client";

import { useEffect, useState } from "react";
import { ChevronDownIcon, SendIcon, XIcon } from "lucide-react";
import { Label } from "@/components/ui/label";
import { profileFlowApi } from "@/lib/profile-flow-api";
import type { UsStateOption } from "@/types/profile-flow";
import { TranscriptUpload } from "./TranscriptUpload";

interface Option {
  value: string;
  label: string;
}

/** Một ô trong widget "select" (advisor buttons.select_fields) */
interface Field {
  name: string;
  /** Nhãn trên ô như form; " " giữ chỗ cho ô đứng cạnh (vd ô năm cạnh ô tháng) */
  label?: string;
  placeholder?: string;
  /** select (mặc định) | number | text | check (ô tích) | states (danh sách bang lấy từ metadata giống form) */
  kind?: "select" | "number" | "text" | "check" | "states";
  options?: Option[];
  /** Ô phụ thuộc ô khác (lớp theo cấp học): lựa chọn lấy theo giá trị ô dependsOn */
  dependsOn?: string;
  optionsBy?: Record<string, Option[]>;
  multiple?: boolean;
  min?: number;
  max?: number;
  /** min / max / step theo giá trị ô khác, vd GPA thang 4 thì tối đa 4, IELTS bước 0.5 */
  minBy?: Record<string, Record<string, number>>;
  maxBy?: Record<string, Record<string, number>>;
  stepBy?: Record<string, Record<string, number>>;
  step?: number;
  /** Ẩn (và không cần điền) khi ô check này được tích, vd tháng kết thúc khi "vẫn đang tham gia" */
  hiddenBy?: string;
  /** Chỉ hiện khi ô `field` chọn một trong `values`, vd ô môn chỉ hiện cho AP, A-Level */
  shownWhen?: { field: string; values: string[] };
  /** Chữ thêm trước giá trị khi ghép câu, vd ", môn " */
  prefix?: string;
}

interface SelectData {
  fields?: Field[];
  /** Ghép giá trị các ô thành tin nhắn, vd "{term} {year}" */
  format?: string;
  /** Câu ghép khác khi ô check `field` được tích */
  altFormat?: { field: string; format: string };
  /** "transcript": thêm nút tải học bạ (câu D2) */
  upload?: string;
}

// Giống ô chọn / ô nhập của form hồ sơ (Step1Goal, Step4Financial)
const controlClass =
  "w-full min-w-0 rounded-lg border border-line bg-surface px-3 py-2 text-sm text-foreground shadow-2xs focus:border-brand focus:outline-hidden focus:ring-1 focus:ring-brand disabled:cursor-not-allowed disabled:opacity-60";
const labelClass = "text-xs font-medium text-foreground";

/** Ô chọn theo mẫu form cho câu đang hỏi; chọn xong gửi câu ghép như tin nhắn. Vẫn gõ chat được bình thường */
export function SelectWidget({ data, onPick }: { data?: Record<string, unknown> | null; onPick?: (text: string) => void }) {
  const { fields = [], format = "", altFormat, upload } = (data ?? {}) as SelectData;
  const [values, setValues] = useState<Record<string, string>>({});
  const [multi, setMulti] = useState<Record<string, string[]>>({});
  const [states, setStates] = useState<UsStateOption[]>([]);
  const needStates = fields.some((f) => f.kind === "states");

  useEffect(() => {
    if (!needStates) return;
    let alive = true;
    profileFlowApi
      .getMetadata()
      .then((m) => alive && setStates(m.states ?? []))
      .catch(() => alive && setStates([]));
    return () => {
      alive = false;
    };
  }, [needStates]);

  if (fields.length === 0) return null;

  const optionsOf = (f: Field): Option[] => {
    if (f.kind === "states") return states.map((s) => ({ value: s.name, label: s.name }));
    if (f.dependsOn) return f.optionsBy?.[values[f.dependsOn] ?? ""] ?? [];
    return f.options ?? [];
  };

  const numberBy = (by: Field["maxBy"], fallback?: number): number | undefined => {
    for (const [other, byValue] of Object.entries(by ?? {})) {
      const n = byValue[values[other] ?? ""];
      if (n !== undefined) return n;
    }
    return fallback;
  };
  const minOf = (f: Field) => numberBy(f.minBy, f.min);
  const maxOf = (f: Field) => numberBy(f.maxBy, f.max);
  const stepOf = (f: Field) => numberBy(f.stepBy, f.step);

  const isChecked = (name: string) => values[name] === "1";
  const isShown = (f: Field) =>
    !(f.hiddenBy && isChecked(f.hiddenBy)) &&
    (!f.shownWhen || f.shownWhen.values.includes(values[f.shownWhen.field] ?? ""));
  const shown = fields.filter(isShown);

  const valueOf = (f: Field): string => (f.multiple ? (multi[f.name] ?? []).join(", ") : (values[f.name] ?? "").trim());

  const isValid = (f: Field): boolean => {
    if (f.kind === "check") return true;
    const v = valueOf(f);
    if (!v) return false;
    if (f.kind !== "number") return true;
    const n = Number(v);
    const min = minOf(f);
    const max = maxOf(f);
    return Number.isFinite(n) && (min === undefined || n >= min) && (max === undefined || n <= max);
  };

  const ready = shown.every(isValid);

  const setValue = (name: string, value: string) =>
    setValues((prev) => {
      const next = { ...prev, [name]: value };
      // Đổi ô cha thì xóa ô phụ thuộc (đổi cấp học thì chọn lại lớp, đổi loại chứng chỉ thì nhập lại điểm)
      for (const f of fields) {
        const parents = [f.dependsOn, f.shownWhen?.field, ...Object.keys({ ...f.minBy, ...f.maxBy, ...f.stepBy })];
        if (parents.includes(name)) next[f.name] = "";
      }
      return next;
    });

  const submit = () => {
    if (!ready || !onPick) return;
    const template = altFormat && isChecked(altFormat.field) ? altFormat.format : format;
    // Ô đang ẩn ghép thành chữ rỗng; chỗ trống của ô không có trong câu này cũng bỏ
    const part = (f: Field) => (isShown(f) && valueOf(f) ? `${f.prefix ?? ""}${valueOf(f)}` : "");
    onPick(fields.reduce((text, f) => text.replace(`{${f.name}}`, part(f)), template).replace(/\{\w+\}/g, ""));
  };

  const control = (f: Field) => {
    if (f.kind === "number" || f.kind === "text")
      return (
        <input
          type={f.kind === "number" ? "number" : "text"}
          inputMode={f.kind === "number" ? "decimal" : undefined}
          id={`sw-${f.name}`}
          placeholder={f.placeholder}
          min={minOf(f)}
          max={maxOf(f)}
          step={stepOf(f)}
          disabled={!onPick}
          value={values[f.name] ?? ""}
          onChange={(e) => setValue(f.name, e.target.value)}
          onKeyDown={(e) => e.key === "Enter" && submit()}
          className={controlClass}
        />
      );
    if (f.multiple)
      return (
        <div className="space-y-1.5">
          <div className="relative">
            <select
              id={`sw-${f.name}`}
              disabled={!onPick}
              value=""
              onChange={(e) => {
                const picked = e.target.value;
                if (picked) setMulti((prev) => ({ ...prev, [f.name]: [...new Set([...(prev[f.name] ?? []), picked])] }));
              }}
              className={`${controlClass} cursor-pointer appearance-none pr-9`}
            >
              <option value="">{states.length || f.kind !== "states" ? f.placeholder : "Đang tải…"}</option>
              {optionsOf(f)
                .filter((o) => !(multi[f.name] ?? []).includes(o.value))
                .map((o) => (
                  <option key={o.value} value={o.value}>
                    {o.label}
                  </option>
                ))}
            </select>
            <ChevronDownIcon className="pointer-events-none absolute top-2.5 right-3 size-4 text-muted-foreground" />
          </div>
          {(multi[f.name] ?? []).length > 0 && (
            <div className="flex flex-wrap gap-1.5">
              {(multi[f.name] ?? []).map((v) => (
                <span
                  key={v}
                  className="inline-flex items-center gap-1 rounded-full bg-brand-wash px-2 py-0.5 text-xs text-brand-ink"
                >
                  {v}
                  <button
                    type="button"
                    aria-label={`Bỏ ${v}`}
                    disabled={!onPick}
                    onClick={() => setMulti((prev) => ({ ...prev, [f.name]: (prev[f.name] ?? []).filter((x) => x !== v) }))}
                    className="cursor-pointer disabled:cursor-not-allowed"
                  >
                    <XIcon className="size-3" />
                  </button>
                </span>
              ))}
            </div>
          )}
        </div>
      );
    return (
      <div className="relative">
        <select
          id={`sw-${f.name}`}
          disabled={!onPick || (f.dependsOn !== undefined && !values[f.dependsOn])}
          value={values[f.name] ?? ""}
          onChange={(e) => setValue(f.name, e.target.value)}
          className={`${controlClass} cursor-pointer appearance-none pr-9`}
        >
          <option value="">{f.kind === "states" && !states.length ? "Đang tải…" : f.placeholder}</option>
          {optionsOf(f).map((o) => (
            <option key={o.value} value={o.value}>
              {o.label}
            </option>
          ))}
        </select>
        <ChevronDownIcon className="pointer-events-none absolute top-2.5 right-3 size-4 text-muted-foreground" />
      </div>
    );
  };

  return (
    <div className="mt-2.5 space-y-3 rounded-xl border border-line bg-surface p-3">
      <div className="grid gap-3 sm:grid-cols-2">
        {shown.map((f) =>
          f.kind === "check" ? (
            <label
              key={f.name}
              className="flex cursor-pointer items-center gap-2 text-sm text-foreground sm:col-span-2"
            >
              <input
                type="checkbox"
                disabled={!onPick}
                checked={isChecked(f.name)}
                onChange={(e) => setValue(f.name, e.target.checked ? "1" : "")}
                className="size-4 cursor-pointer accent-brand"
              />
              {f.label}
            </label>
          ) : (
            <div
              key={f.name}
              className={`space-y-1.5 ${f.multiple || (fields.length === 1 && !f.label?.trim()) || f.kind === "text" ? "sm:col-span-2" : ""}`}
            >
              {f.label && (
                <Label htmlFor={`sw-${f.name}`} className={labelClass}>
                  {f.label.trim() || " "}
                </Label>
              )}
              {control(f)}
            </div>
          )
        )}
      </div>
      {upload === "transcript" && <TranscriptUpload onPick={onPick} />}
      <div className="flex items-center justify-between gap-2">
        <p className="text-[11px] text-muted-foreground">Chọn cho nhanh, hoặc gõ trả lời vào ô chat.</p>
        <button
          type="button"
          disabled={!onPick || !ready}
          onClick={submit}
          className="inline-flex h-8 shrink-0 cursor-pointer items-center gap-1 rounded-full bg-brand px-3.5 text-xs font-semibold text-white transition-colors hover:bg-brand-ink disabled:cursor-not-allowed disabled:opacity-50"
        >
          <SendIcon className="size-3.5" />
          Gửi
        </button>
      </div>
    </div>
  );
}
