"use client";

/**
 * Bộ khung biểu mẫu dùng chung cho luồng hồ sơ (/analysis).
 * Mọi bước dựng từ các mảnh này để giữ một kiểu duy nhất: khối câu hỏi, chọn nhanh, thẻ lựa chọn,
 * danh sách thao tác, trạng thái trống, bảng chọn có tìm kiếm, ô nhập trong bảng.
 */

import { useId, useRef, useState, type ReactNode } from "react";
import { CheckIcon, ChevronDownIcon, SearchIcon } from "lucide-react";
import { Input } from "@/components/ui/input";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { normalizeSearch } from "@/lib/certification-utils";
import { cn } from "@/lib/utils";
import { RequiredMark } from "./RequiredMark";

export interface Option<T extends string = string> {
  value: T;
  label: string;
  description?: string;
  disabled?: boolean;
  /** Từ khoá tìm thêm (tên tiếng Anh, viết tắt...) cho PickerPanel */
  keywords?: string[];
}

/* ───────────── Bố cục ───────────── */

/** Thân bước: cột giữa có bề rộng tối đa; các nhóm thông tin (FormGroup) xếp chồng, xuất hiện lần lượt */
export function FormBody({ lede, children }: { lede?: ReactNode; children: ReactNode }) {
  return (
    <div className="mx-auto w-full max-w-(--flow-form-w) px-4 pt-6 pb-10 sm:px-8 sm:pt-8">
      {lede && <p className="mb-6 text-sm text-ink-2">{lede}</p>}
      <div className="space-y-6">{children}</div>
    </div>
  );
}

/**
 * Một nhóm thông tin (Tình trạng học vấn, Mục tiêu du học...). Khung rộng: tên nhóm cỡ lớn ở cột trái (kèm lời dẫn,
 * "Bắt buộc" khi nhóm chỉ có một câu hỏi và chính nhóm là câu hỏi), các câu hỏi ở cột phải.
 * Khung hẹp: tên nhóm thành dòng đầu, câu hỏi xếp bên dưới.
 * `tabs`: hàng chọn cách nhập nằm trên cùng cột phải; `footer`: dòng kết quả nằm cuối cột phải.
 * Đo theo bề rộng của chính nhóm (container query), nên mở khung chat vẫn tự co đúng.
 * Nền xanh nhạt theo màu nhấn: cột tên nhóm đậm hơn cột câu hỏi, ô nhập trắng nổi lên rõ; không dùng overflow-hidden để danh sách thả xuống bên trong không bị cắt.
 */
export function FormGroup({
  id,
  title,
  description,
  required,
  error,
  tabs,
  footer,
  children,
}: {
  id?: string;
  title: string;
  description?: ReactNode;
  required?: boolean;
  error?: string | null;
  tabs?: ReactNode;
  footer?: ReactNode;
  children?: ReactNode;
}) {
  const headingId = useId();
  return (
    <section id={id} aria-labelledby={headingId} className="@container scroll-mt-6 rounded-2xl border border-line">
      <div className="@3xl:grid @3xl:grid-cols-[15rem_minmax(0,1fr)]">
        <div className="rounded-t-2xl border-b border-line bg-(--surface-3) px-5 py-3 @3xl:rounded-tr-none @3xl:rounded-l-2xl @3xl:border-r @3xl:border-b-0 @3xl:px-6 @3xl:py-7">
          <div className="flex flex-wrap items-start justify-between gap-x-3 gap-y-1.5 @3xl:flex-col @3xl:justify-start">
            <h2 id={headingId} className="text-sm font-semibold text-ink @3xl:font-heading @3xl:text-2xl @3xl:leading-snug @3xl:font-normal">
              {title}
            </h2>
            {required && <RequiredBadge />}
          </div>
          {description && <p className="mt-1 text-sm text-ink-2 @3xl:mt-3">{description}</p>}
        </div>
        <div className="flex min-w-0 flex-col rounded-b-2xl bg-(--surface-2) @3xl:rounded-bl-none @3xl:rounded-r-2xl">
          {tabs}
          {children && <div className="divide-y divide-dashed divide-line px-5 @3xl:px-7">{children}</div>}
          {error && (
            <div className="px-5 pb-5 @3xl:px-7">
              <FieldError>{error}</FieldError>
            </div>
          )}
          {footer && <div className="mt-auto border-t border-line px-5 py-5 @3xl:px-7">{footer}</div>}
        </div>
      </div>
    </section>
  );
}

/** Hàng chọn cách nhập trên đầu nhóm (Nhập GPA tổng / Nhập từng môn): mỗi tab có tên và một dòng mô tả */
export function GroupTabs<T extends string>({
  options,
  value,
  onChange,
  ariaLabel,
}: {
  options: Option<T>[];
  value: string | null | undefined;
  onChange: (value: T) => void;
  ariaLabel: string;
}) {
  return (
    <div role="radiogroup" aria-label={ariaLabel} className="grid auto-cols-fr grid-flow-col">
      {options.map((o, i) => (
        <button
          key={o.value}
          type="button"
          role="radio"
          aria-checked={o.value === value}
          aria-disabled={o.disabled || undefined}
          onClick={() => !o.disabled && onChange(o.value)}
          className={cn(
            // Tab đang chọn cùng màu với phần câu hỏi bên dưới (liền một khối); tab chưa chọn nền trắng (khác cột tên nhóm),
            // rê chuột thì ánh xanh nhạt, không trùng màu tab đang chọn
            "cursor-pointer bg-surface px-4 py-3.5 text-center text-ink-2 hover:bg-brand-wash/45 focus-visible:bg-brand-wash/45 focus-visible:outline-none aria-checked:bg-(--surface-2) aria-checked:hover:bg-(--surface-2) aria-disabled:cursor-not-allowed aria-disabled:opacity-(--cds-opacity-disabled) @3xl:px-6",
            i === options.length - 1 && "@3xl:rounded-tr-2xl",
          )}
        >
          <span className="block text-sm font-semibold text-ink">{o.label}</span>
          {o.description && <span className="mt-0.5 block text-xs text-ink-2">{o.description}</span>}
        </button>
      ))}
    </div>
  );
}

/** Dòng kết quả cuối nhóm: nhãn bên trái, các con số bên phải */
export function GroupResult({ label, items }: { label: ReactNode; items: { label: string; value: ReactNode }[] }) {
  return (
    <div className="flex flex-wrap items-center justify-between gap-4">
      <p className="text-base font-semibold text-ink">{label}</p>
      <dl className="flex gap-8">
        {items.map((it) => (
          <div key={it.label}>
            <dt className="text-xs text-ink-2">{it.label}</dt>
            <dd className="mt-0.5 text-xl font-semibold tabular-nums text-ink">{it.value}</dd>
          </div>
        ))}
      </dl>
    </div>
  );
}

/** Nhãn "Bắt buộc" cạnh câu hỏi */
export function RequiredBadge() {
  return (
    <span className="inline-flex shrink-0 items-center rounded-md bg-risk-wash px-1.5 py-0.5 text-[11px] font-medium text-risk">
      Bắt buộc
    </span>
  );
}

/**
 * Một câu hỏi trong nhóm. Khung rộng: câu hỏi (kèm "Bắt buộc", lời dẫn) bên trái, phần trả lời bên phải.
 * Khung hẹp: câu hỏi và "Bắt buộc" cùng hàng (câu dài thì "Bắt buộc" xuống dòng), trả lời bên dưới.
 * `wide`: phần trả lời cần cả bề ngang (bảng, danh sách dài) thì luôn nằm dưới câu hỏi.
 */
export function FormSection({
  id,
  title,
  required,
  description,
  aside,
  error,
  wide,
  open,
  onToggle,
  children,
}: {
  id?: string;
  title: ReactNode;
  required?: boolean;
  description?: ReactNode;
  /** Ghi chú thêm trải hết bề ngang dưới câu hỏi và phần trả lời, ví dụ cách quy đổi điểm */
  aside?: ReactNode;
  error?: string | null;
  wide?: boolean;
  /** Có onToggle thì câu hỏi thu gọn được: bấm tiêu đề (mũi tên bên phải) để mở / đóng */
  open?: boolean;
  onToggle?: () => void;
  children?: ReactNode;
}) {
  const headingId = useId();
  const collapsible = Boolean(onToggle);
  const stacked = wide || collapsible;
  const showBody = !collapsible || open;
  // Câu hỏi trong nhóm đánh dấu bắt buộc bằng sao đỏ cạnh nhãn (gọn, dễ nhìn); nhãn "Bắt buộc" chỉ dùng khi cả nhóm là một câu hỏi
  const heading = (
    <h3 id={headingId} className="text-base font-semibold text-ink">
      {title} {required && <RequiredMark />}
    </h3>
  );
  return (
    <section id={id} aria-labelledby={headingId} className="scroll-mt-6 py-6">
    <div className={cn(!stacked && "@4xl:grid @4xl:grid-cols-[minmax(0,2fr)_minmax(0,3fr)] @4xl:gap-8")}>
      <div className={cn(showBody && "mb-4", !stacked && "@4xl:mb-0")}>
        {collapsible ? (
          <button
            type="button"
            onClick={onToggle}
            aria-expanded={Boolean(open)}
            className="flex w-full cursor-pointer items-start justify-between gap-3 text-left focus-visible:outline-none focus-visible:ring-3 focus-visible:ring-ring/50"
          >
            <span className="min-w-0">
              {heading}
              {description && <span className="mt-1 block text-sm text-ink-2">{description}</span>}
            </span>
            <ChevronDownIcon aria-hidden className={cn("mt-0.5 size-5 shrink-0 text-ink-2", open && "rotate-180")} />
          </button>
        ) : (
          <>
            {heading}
            {description && <p className="mt-1 text-sm text-ink-2">{description}</p>}
          </>
        )}
      </div>
      {showBody && (
        <div className="min-w-0">
          <div className="space-y-5">{children}</div>
          {error && <FieldError>{error}</FieldError>}
        </div>
      )}
    </div>
      {aside && <div className="mt-4 text-xs text-ink-2">{aside}</div>}
    </section>
  );
}

/**
 * Một câu hỏi con trong thẻ (chứng chỉ, hoạt động): khung rộng thì nhãn (sao đỏ nếu bắt buộc, gợi ý) bên trái,
 * ô nhập bên phải; khung hẹp xếp chồng. Đo theo bề rộng thân thẻ — thân thẻ cần `@container`,
 * các hàng ngăn nhau bằng đường kẻ đứt (divide-y divide-dashed). `wide`: phần trả lời cần cả bề ngang (bảng).
 */
export function FieldRow({
  label,
  htmlFor,
  required,
  hint,
  error,
  wide,
  children,
}: {
  label: ReactNode;
  htmlFor?: string;
  required?: boolean;
  hint?: ReactNode;
  error?: string | null;
  wide?: boolean;
  children: ReactNode;
}) {
  const Label = htmlFor ? "label" : "div";
  return (
    <div className={cn("py-5", !wide && "@2xl:grid @2xl:grid-cols-[minmax(0,2fr)_minmax(0,3fr)] @2xl:gap-8")}>
      <div className={cn("mb-3", !wide && "@2xl:mb-0")}>
        <Label {...(htmlFor && { htmlFor })} className="block text-sm font-medium text-ink">
          {label} {required && <RequiredMark />}
        </Label>
        {hint && <p className="mt-1 text-xs text-ink-2">{hint}</p>}
      </div>
      <div className="min-w-0 space-y-2">
        {children}
        {error && <FieldError>{error}</FieldError>}
      </div>
    </div>
  );
}

/** Một ô trong khối câu hỏi: nhãn, phần nhập, lỗi; mỗi ô một hàng để học sinh đọc từ trên xuống */
export function Field({
  label,
  htmlFor,
  required,
  hint,
  error,
  children,
}: {
  label: ReactNode;
  htmlFor?: string;
  required?: boolean;
  hint?: ReactNode;
  error?: string | null;
  children: ReactNode;
}) {
  const Label = htmlFor ? "label" : "div";
  return (
    <div className="space-y-2">
      <Label {...(htmlFor && { htmlFor })} className="block text-sm font-medium text-ink">
        {label} {required && <RequiredMark />}
      </Label>
      {children}
      {hint && !error && <p className="text-xs text-ink-2">{hint}</p>}
      {error && <FieldError>{error}</FieldError>}
    </div>
  );
}

export function FieldError({ children }: { children: ReactNode }) {
  return (
    <p role="alert" className="mt-2 text-xs font-medium text-destructive">
      {children}
    </p>
  );
}

/* ───────────── Chọn một ───────────── */

/* Nút chọn nhanh: nền trắng nổi trên nền nhóm bằng bóng mờ (không viền), chọn thì nền xanh đậm chữ trắng; đổi trạng thái tức thì.
   Thẻ lựa chọn (CHOICE_CARD): chưa chọn nền trắng nổi; chọn thì viền và nền xanh nhạt.
   Tương phản tạo bằng màu nền, không thêm viền */
export const CHOICE_CHIP =
  "inline-flex min-h-10 cursor-pointer items-center gap-3 rounded-lg bg-surface px-3.5 py-2 text-left text-sm font-medium text-ink shadow-(--elev-1) hover:bg-brand-wash aria-checked:shadow-none focus-visible:outline-none focus-visible:ring-3 focus-visible:ring-ring/50 aria-checked:bg-brand aria-checked:text-brand-foreground aria-checked:[&_.text-ink-2]:text-brand-foreground/75 aria-disabled:cursor-not-allowed aria-disabled:opacity-(--cds-opacity-disabled)";

const OTHER = "__other__";

/**
 * Chọn một giá trị: ít lựa chọn (≤ maxChips) thì bày sẵn thành nút để bấm một lần,
 * nhiều hơn thì gom vào danh sách thả xuống. `other` thêm nút "Khác": bấm thì hiện ô tự nhập,
 * bên trái ô là danh sách để quay về lựa chọn có sẵn.
 */
export function ChoicePicker<T extends string>({
  id,
  options,
  value,
  onChange,
  ariaLabel,
  placeholder = "Chọn",
  maxChips = 6,
  fill,
  other,
}: {
  id?: string;
  options: Option<T>[];
  value: string | null | undefined;
  onChange: (value: string | null) => void;
  ariaLabel?: string;
  placeholder?: string;
  maxChips?: number;
  /** Các nút chia đều, kéo hết bề ngang phần trả lời (ít lựa chọn như Thang 10 / Thang 4) */
  fill?: boolean;
  other?: {
    label?: string;
    placeholder?: string;
    /** Trả về câu lỗi khi giá trị tự nhập chưa hợp lệ */
    validate?: (value: string) => string | null;
  };
}) {
  const inList = options.some((o) => o.value === value);
  const [otherMode, setOtherMode] = useState(Boolean(value && !inList && other));
  const showOther = Boolean(other) && (otherMode || Boolean(value && !inList));
  const otherLabel = other?.label ?? "Khác";

  if (showOther && other) {
    return (
      <OtherInput
        id={id}
        options={options}
        value={inList ? "" : (value ?? "")}
        label={otherLabel}
        placeholder={other.placeholder}
        validate={other.validate}
        ariaLabel={ariaLabel}
        onPick={(v) => {
          setOtherMode(false);
          onChange(v);
        }}
        onType={onChange}
      />
    );
  }

  if (options.length > maxChips) {
    // Danh sách dài: thả xuống, giữ "Khác" ở cuối nếu cho tự nhập
    const items = [...options, ...(other ? [{ value: OTHER, label: otherLabel }] : [])];
    return (
      <Select
        value={inList ? value : null}
        onValueChange={(v) => {
          if (v === OTHER) {
            setOtherMode(true);
            onChange("");
          } else onChange(v ?? null);
        }}
        items={items}
      >
        <SelectTrigger id={id} aria-label={ariaLabel} className="h-10 w-full bg-surface sm:max-w-sm">
          <SelectValue placeholder={placeholder} />
        </SelectTrigger>
        <SelectContent>
          {items.map((o) => (
            <SelectItem key={o.value} value={o.value}>
              {o.label}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
    );
  }

  return (
    <div id={id} role="radiogroup" aria-label={ariaLabel} className={cn("flex flex-wrap gap-2", fill && "[&>*]:flex-1 [&>*]:justify-center")}>
      {options.map((o) => (
        <button
          key={o.value}
          type="button"
          role="radio"
          aria-checked={o.value === value}
          aria-disabled={o.disabled || undefined}
          onClick={() => !o.disabled && onChange(o.value)}
          className={CHOICE_CHIP}
        >
          {/* Nút chọn nhanh chỉ đổi màu khi chọn, không có ô tích; mô tả (nếu có) là dòng nhỏ dưới nhãn */}
          <span className="min-w-0">
            <span className="block">{o.label}</span>
            {o.description && <span className="block text-xs font-normal text-ink-2">{o.description}</span>}
          </span>
        </button>
      ))}
      {other && (
        <button
          type="button"
          role="radio"
          aria-checked={false}
          onClick={() => {
            setOtherMode(true);
            onChange("");
          }}
          className={CHOICE_CHIP}
        >
          {otherLabel}
        </button>
      )}
    </div>
  );
}

/** Ô "Khác": bên trái là danh sách (rộng bằng cả ô) để chọn lại giá trị có sẵn, bên phải ô tự nhập có kiểm tra */
function OtherInput<T extends string>({
  id,
  options,
  value,
  label,
  placeholder,
  validate,
  ariaLabel,
  onPick,
  onType,
}: {
  id?: string;
  options: Option<T>[];
  value: string;
  label: string;
  placeholder?: string;
  validate?: (value: string) => string | null;
  ariaLabel?: string;
  onPick: (value: string) => void;
  onType: (value: string) => void;
}) {
  const boxRef = useRef<HTMLDivElement>(null);
  const [menuWidth, setMenuWidth] = useState<number>();
  const [touched, setTouched] = useState(false);
  const error = touched && value.trim() ? (validate?.(value.trim()) ?? null) : null;
  const items = [...options, { value: OTHER, label }];
  return (
    <div className="space-y-2 sm:max-w-md">
      <div
        ref={boxRef}
        className={cn(
          "flex h-10 items-stretch overflow-hidden rounded-lg border border-input bg-surface focus-within:border-ring focus-within:ring-3 focus-within:ring-ring/50",
          error && "border-destructive",
        )}
      >
        <Select
          value={OTHER}
          onOpenChange={(open) => open && setMenuWidth(boxRef.current?.offsetWidth)}
          onValueChange={(v) => v && v !== OTHER && onPick(v)}
          items={items}
        >
          <SelectTrigger
            aria-label={`${ariaLabel ?? ""} – đổi lựa chọn`.trim()}
            className="h-auto shrink-0 self-stretch rounded-none border-0 border-r border-input bg-(--surface-2) px-3 focus-visible:ring-0 data-[size=default]:h-auto"
          >
            <SelectValue />
          </SelectTrigger>
          <SelectContent align="start" style={menuWidth ? { width: menuWidth } : undefined}>
            {items.map((o) => (
              <SelectItem key={o.value} value={o.value}>
                {o.label}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
        <input
          id={id}
          autoFocus
          value={value}
          placeholder={placeholder}
          aria-label={ariaLabel}
          aria-invalid={Boolean(error) || undefined}
          onChange={(e) => onType(e.target.value)}
          onBlur={() => setTouched(true)}
          className="min-w-0 flex-1 bg-transparent px-3 text-sm outline-none placeholder:text-ink-3/60"
        />
      </div>
      {error && <FieldError>{error}</FieldError>}
    </div>
  );
}

/* ───────────── Thẻ lựa chọn ───────────── */

export const CHOICE_CARD =
  "cursor-pointer rounded-xl border border-transparent bg-surface p-4 text-left shadow-(--elev-1) hover:bg-brand-wash/50 aria-checked:shadow-none focus-visible:outline-none focus-visible:ring-3 focus-visible:ring-ring/50 aria-checked:border-brand aria-checked:bg-brand-wash aria-checked:ring-1 aria-checked:ring-brand aria-disabled:cursor-not-allowed aria-disabled:opacity-(--cds-opacity-disabled)";

/** Ô tích bên phải mỗi lựa chọn, dùng cả khi chỉ được chọn một: nhìn là biết đã chọn hay chưa */
export function CheckIndicator({ checked }: { checked: boolean }) {
  return (
    <span
      aria-hidden
      data-check
      className={cn(
        "flex size-4.5 shrink-0 items-center justify-center rounded border",
        checked ? "border-brand bg-brand text-brand-foreground" : "border-(--control-border) bg-surface",
      )}
    >
      {checked && <CheckIcon className="size-3 stroke-[3]" />}
    </span>
  );
}

/** Lựa chọn cần lời giải thích (bậc học, cách nhập GPA, mức ngân sách...): thẻ có tiêu đề và một dòng mô tả */
export function ChoiceCards<T extends string>({
  options,
  value,
  onChange,
  ariaLabel,
  columns = 2,
}: {
  options: Option<T>[];
  value: string | null | undefined;
  onChange: (value: T) => void;
  ariaLabel?: string;
  /** 1: xếp dọc từng thẻ một hàng */
  columns?: 1 | 2 | 3;
}) {
  return (
    <div role="radiogroup" aria-label={ariaLabel} className={cn("grid gap-3", columns === 3 ? "sm:grid-cols-3" : columns === 2 && "sm:grid-cols-2")}>
      {options.map((o) => (
        <button
          key={o.value}
          type="button"
          role="radio"
          aria-checked={o.value === value}
          aria-disabled={o.disabled || undefined}
          onClick={() => !o.disabled && onChange(o.value)}
          className={cn(CHOICE_CARD, "flex h-full items-start gap-3")}
        >
          <span className="min-w-0 flex-1">
            <span className="block text-sm font-semibold text-ink">{o.label}</span>
            {o.description && <span className="mt-1 block text-xs text-ink-2">{o.description}</span>}
          </span>
          <CheckIndicator checked={o.value === value} />
        </button>
      ))}
    </div>
  );
}

/** Hai, ba trạng thái loại trừ nhau trên cùng một hàng (Đã có điểm / Dự định thi) */
export function SegmentedControl<T extends string>({
  options,
  value,
  onChange,
  ariaLabel,
}: {
  options: Option<T>[];
  value: T;
  onChange: (value: T) => void;
  ariaLabel: string;
}) {
  return (
    <div role="radiogroup" aria-label={ariaLabel} className="inline-flex shrink-0 rounded-lg bg-(--surface-3) p-1">
      {options.map((o) => (
        <button
          key={o.value}
          type="button"
          role="radio"
          aria-checked={o.value === value}
          onClick={() => onChange(o.value)}
          className="h-8 cursor-pointer rounded-md px-3 text-sm font-medium text-ink-2 hover:text-ink focus-visible:outline-none focus-visible:ring-3 focus-visible:ring-ring/50 aria-checked:bg-surface aria-checked:text-ink aria-checked:shadow-sm"
        >
          {o.label}
        </button>
      ))}
    </div>
  );
}

/* ───────────── Danh sách thao tác ───────────── */

/** Nhóm hàng thao tác chung một khung, các hàng ngăn nhau bằng đường kẻ (Thêm chứng chỉ / Tôi chưa thi...).
 *  Nền trắng nổi trên nền nhóm (bóng mờ thay viền) */
export function ActionList({ children, className }: { children: ReactNode; className?: string }) {
  return <div className={cn("divide-y divide-line overflow-hidden rounded-xl bg-surface shadow-(--elev-1)", className)}>{children}</div>;
}

/**
 * Một hàng trong ActionList. Có `checked` thì là ô tích (bấm để bật/tắt),
 * không có thì là nút thao tác (thêm, mở...).
 */
export function ActionRow({
  icon,
  children,
  onClick,
  checked,
  disabled,
}: {
  icon?: ReactNode;
  children: ReactNode;
  onClick: () => void;
  checked?: boolean;
  disabled?: boolean;
}) {
  const isToggle = checked !== undefined;
  return (
    <button
      type="button"
      onClick={onClick}
      disabled={disabled}
      {...(isToggle && { role: "checkbox", "aria-checked": checked })}
      className={cn(
        "flex w-full cursor-pointer items-center gap-3 px-4 py-3.5 text-left text-sm hover:bg-(--surface-2) focus-visible:bg-(--surface-2) focus-visible:outline-none disabled:cursor-not-allowed disabled:opacity-(--cds-opacity-disabled)",
        isToggle ? "text-ink" : "font-medium text-brand",
      )}
    >
      {isToggle ? (
<CheckIndicator checked={Boolean(checked)} />
      ) : (
        icon && <span aria-hidden className="flex size-4.5 shrink-0 items-center justify-center [&_svg]:size-4">{icon}</span>
      )}
      <span className="min-w-0 flex-1">{children}</span>
    </button>
  );
}

/* ───────────── Trạng thái trống ───────────── */

/** Chưa có mục nào: khoảng trống thoáng, không khung, không nền riêng */
export function EmptyState({
  icon,
  title,
  description,
  action,
}: {
  icon: ReactNode;
  title: string;
  description?: string;
  action?: ReactNode;
}) {
  return (
    <div className="flex flex-col items-center px-4 py-16 text-center">
      <span aria-hidden className="mb-4 flex size-12 items-center justify-center rounded-full bg-ground text-ink-2 [&_svg]:size-5">
        {icon}
      </span>
      <p className="text-sm font-semibold text-ink">{title}</p>
      {description && <p className="mt-1 max-w-sm text-sm text-ink-2">{description}</p>}
      {action && <div className="mt-5">{action}</div>}
    </div>
  );
}

/* ───────────── Bảng chọn có tìm kiếm ───────────── */

/**
 * Danh sách dài (ngành học...): ô tìm kiếm, nhóm "hay chọn" và nhóm "toàn bộ", cuộn bên trong khung,
 * hàng cuối dành cho lựa chọn đặc biệt (chưa tìm được, cần tư vấn...).
 */
export function PickerPanel({
  id,
  groups,
  value,
  onChange,
  searchPlaceholder = "Tìm kiếm",
  emptyText = (q: string) => `Không có kết quả cho “${q}”.`,
  loading,
  footer,
  ariaLabel,
}: {
  id?: string;
  groups: { label: string; options: Option[] }[];
  value: string | null | undefined;
  onChange: (value: string) => void;
  searchPlaceholder?: string;
  emptyText?: (query: string) => string;
  loading?: boolean;
  footer?: ReactNode;
  ariaLabel: string;
}) {
  const [query, setQuery] = useState("");
  // Bỏ dấu khi so khớp: "may tinh" vẫn ra "Máy tính"; tìm cả value và từ khoá
  const q = normalizeSearch(query.trim());
  const shown = q
    ? [
        {
          label: "Kết quả",
          options: dedupe(groups.flatMap((g) => g.options)).filter((o) => [o.label, o.value, ...(o.keywords ?? [])].some((s) => normalizeSearch(s).includes(q))),
        },
      ]
    : groups;
  const nothing = shown.every((g) => g.options.length === 0);

  return (
    <div className="overflow-hidden rounded-xl border border-line bg-surface">
      <div className="border-b border-line p-3">
        <div className="relative">
          <SearchIcon aria-hidden className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-ink-3" />
          <Input
            id={id}
            type="search"
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            placeholder={loading ? "Đang tải…" : searchPlaceholder}
            disabled={loading}
            className="h-10 bg-surface pl-9"
          />
        </div>
      </div>
      <div role="radiogroup" aria-label={ariaLabel} className="max-h-80 space-y-5 overflow-y-auto p-3">
        {nothing && q && <p className="px-1 py-6 text-center text-sm text-ink-2">{emptyText(query.trim())}</p>}
        {shown.map(
          (g) =>
            g.options.length > 0 && (
              <div key={g.label}>
                <p className="mb-1.5 px-1 text-xs font-medium text-ink-2">{g.label}</p>
                <div className="grid gap-1 sm:grid-cols-2 lg:grid-cols-3">
                  {g.options.map((o) => (
                    <PickerOption key={o.value} option={o} checked={o.value === value} onPick={onChange} />
                  ))}
                </div>
              </div>
            ),
        )}
      </div>
      {footer && <div className="border-t border-line">{footer}</div>}
    </div>
  );
}

function PickerOption({ option, checked, onPick }: { option: Option; checked: boolean; onPick: (v: string) => void }) {
  return (
    <button
      type="button"
      role="radio"
      aria-checked={checked}
      aria-disabled={option.disabled || undefined}
      onClick={() => !option.disabled && onPick(option.value)}
      className="flex min-h-10 cursor-pointer items-center gap-2.5 rounded-lg px-2.5 py-2 text-left text-sm text-ink hover:bg-ground focus-visible:bg-ground focus-visible:outline-none aria-checked:bg-brand-wash aria-checked:font-medium aria-disabled:cursor-not-allowed aria-disabled:opacity-(--cds-opacity-disabled)"
    >
      <span className="min-w-0 flex-1">{option.label}</span>
      <CheckIndicator checked={checked} />
    </button>
  );
}

function dedupe(options: Option[]) {
  const seen = new Set<string>();
  return options.filter((o) => (seen.has(o.value) ? false : (seen.add(o.value), true)));
}

/* ───────────── Ô có tiền tố ───────────── */

/** Ô nhập kèm danh sách chọn (loại trường + tên trường, số tiền + đơn vị); danh sách rộng bằng cả ô.
 *  `side="end"`: danh sách nằm bên phải (đơn vị tiền). */
export function PrefixInput({
  id,
  prefixes,
  prefix,
  onPrefixChange,
  value,
  onChange,
  placeholder,
  ariaLabel,
  prefixLabel = "loại",
  side = "start",
  inputMode,
}: {
  id?: string;
  prefixes: Option[];
  prefix: string;
  onPrefixChange: (prefix: string) => void;
  value: string;
  onChange: (value: string) => void;
  placeholder?: string;
  ariaLabel?: string;
  prefixLabel?: string;
  side?: "start" | "end";
  inputMode?: "text" | "numeric" | "decimal";
}) {
  const boxRef = useRef<HTMLDivElement>(null);
  const [menuWidth, setMenuWidth] = useState<number>();
  const select = (
    <Select
      value={prefix || null}
      onOpenChange={(open) => open && setMenuWidth(boxRef.current?.offsetWidth)}
      onValueChange={(v) => onPrefixChange(v ?? "")}
      items={prefixes}
    >
      <SelectTrigger
        aria-label={`${ariaLabel ?? ""} – ${prefixLabel}`.trim()}
        className={cn(
          "h-auto shrink-0 self-stretch rounded-none border-0 border-input bg-(--surface-2) px-3 focus-visible:ring-0 data-[size=default]:h-auto",
          side === "start" ? "border-r" : "border-l",
        )}
      >
        <SelectValue placeholder="Chọn" />
      </SelectTrigger>
      <SelectContent align={side === "start" ? "start" : "end"} style={menuWidth ? { width: menuWidth } : undefined}>
        {prefixes.map((o) => (
          <SelectItem key={o.value} value={o.value}>
            {o.label}
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  );
  return (
    <div
      ref={boxRef}
      className="flex h-10 items-stretch overflow-hidden rounded-lg border border-input bg-surface focus-within:border-ring focus-within:ring-3 focus-within:ring-ring/50"
    >
      {side === "start" && select}
      <input
        id={id}
        value={value}
        placeholder={placeholder}
        aria-label={ariaLabel}
        inputMode={inputMode}
        onChange={(e) => onChange(e.target.value)}
        className="min-w-0 flex-1 bg-transparent px-3 text-sm outline-none placeholder:text-ink-3/60"
      />
      {side === "end" && select}
    </div>
  );
}

/* ───────────── Bảng nhập liệu ───────────── */

/** Ô nhập nằm thẳng trong ô bảng: không viền, gợi ý mờ, nền nhạt khi đang gõ */
/** Ô nhập nằm thẳng trong ô bảng: lấp kín ô (góc vuông), không viền, gợi ý mờ, nền nhạt khi rê / đang gõ.
 *  Ô bảng chứa nó đặt p-0. */
export const INLINE_INPUT =
  "block h-11 w-full rounded-none bg-transparent px-3 text-center text-sm tabular-nums text-ink outline-none placeholder:text-ink-3/40 hover:bg-ground focus:bg-brand-wash/50 aria-invalid:bg-risk-wash aria-invalid:text-risk";

/** Kẻ bảng nhập liệu: có vạch dọc giữa các cột */
export const DATA_TABLE = "w-full border-collapse bg-surface text-sm [&_td]:border-line [&_th]:border-line [&_td+td]:border-l [&_th+th]:border-l";

/** Hàng cuối bảng để thêm dòng mới, thay cho nút đứng riêng bên dưới */
export function AddRow({ colSpan, onClick, children }: { colSpan: number; onClick: () => void; children: ReactNode }) {
  return (
    <tr>
      <td colSpan={colSpan} className="border-t border-line p-0">
        <button
          type="button"
          onClick={onClick}
          className="flex w-full cursor-pointer items-center gap-2 px-3 py-3 text-left text-sm font-medium text-brand hover:bg-ground focus-visible:bg-ground focus-visible:outline-none"
        >
          {children}
        </button>
      </td>
    </tr>
  );
}

