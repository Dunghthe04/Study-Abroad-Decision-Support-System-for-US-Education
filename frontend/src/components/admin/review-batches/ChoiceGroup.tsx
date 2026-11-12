import { cn } from "cn";

interface Props<T extends string> {
  label: string;
  /** null là chưa chọn: khác với Segmented (luôn có một giá trị), biểu mẫu chấm bắt đầu từ chưa chọn để không chấm nhầm theo mặc định. */
  value: T | null;
  options: { id: T; label: string }[];
  onChange: (next: T) => void;
}

/** Nhóm lựa chọn một trong vài giá trị, có thể chưa chọn. Dùng cho biểu mẫu chấm. */
export function ChoiceGroup<T extends string>({ label, value, options, onChange }: Props<T>) {
  return (
    <div role="radiogroup" aria-label={label} className="flex flex-wrap gap-1.5">
      {options.map((option) => {
        const checked = option.id === value;
        return (
          <button
            key={option.id}
            type="button"
            role="radio"
            aria-checked={checked}
            onClick={() => onChange(option.id)}
            className={cn(
              "rounded-md border px-3 py-1.5 text-sm font-medium transition-colors outline-none focus-visible:ring-2 focus-visible:ring-ring",
              checked ? "border-brand bg-brand-wash text-brand-ink" : "border-line bg-surface text-ink-2 hover:text-ink",
            )}
          >
            {option.label}
          </button>
        );
      })}
    </div>
  );
}
