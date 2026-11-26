"use client";

import { Building2, Search } from "lucide-react";
import Image from "next/image";
import { forwardRef, useEffect, useId, useState } from "react";
import { Spinner } from "@/components/ui/spinner";
import { searchUniversities } from "@/lib/university-api";
import { MISSING_UNIVERSITY_DATA, schoolControlLabel, universityMoney, universityUrl } from "@/lib/university-display";
import { cn } from "@/lib/utils";
import type { UniversityListItem } from "@/types/university";

interface Props {
  onPick: (school: UniversityListItem) => void;
  /** Trường đã chọn: vẫn hiện trong danh sách nhưng không bấm được */
  pickedCodes?: string[];
  placeholder?: string;
  label: string;
  disabled?: boolean;
  /** Nút đặt bên phải trong ô nhập (vd. "So sánh") */
  action?: React.ReactNode;
}

const SUGGEST_SIZE = 6;

const money = (value: number | null) => {
  const text = universityMoney(value);
  return text === MISSING_UNIVERSITY_DATA ? null : text;
};

/**
 * Ô tìm trường dùng chung cho tra cứu chi phí. Bấm vào ô là hiện ngay danh sách trường gợi ý (chưa cần gõ);
 * gõ từ 2 ký tự thì lọc theo tên, mã hoặc thành phố. Mũi tên lên/xuống + Enter để chọn.
 */
export const SchoolSearch = forwardRef<HTMLInputElement, Props>(function SchoolSearch(
  { onPick, pickedCodes = [], placeholder = "Vui lòng nhập tên trường hoặc mã trường để tiếp tục", label, disabled = false, action },
  ref,
) {
  const listId = useId();
  const [query, setQuery] = useState("");
  const [results, setResults] = useState<UniversityListItem[]>([]);
  const [suggested, setSuggested] = useState<UniversityListItem[] | null>(null);
  const [resultsFor, setResultsFor] = useState("");
  const [open, setOpen] = useState(false);
  const [active, setActive] = useState(0);
  const ready = query.trim().length >= 2;
  // Đang tải khi kết quả đang giữ chưa phải của câu gõ hiện tại
  const loading = ready && resultsFor !== query;

  useEffect(() => {
    if (!ready) return;
    const controller = new AbortController();
    const timer = setTimeout(() => {
      searchUniversities({ search: query, pageSize: SUGGEST_SIZE, page: 1 }, controller.signal)
        .then((r) => { setResults(r.items); setActive(0); setResultsFor(query); })
        .catch(() => { if (!controller.signal.aborted) { setResults([]); setResultsFor(query); } });
    }, 250);
    return () => { clearTimeout(timer); controller.abort(); };
  }, [query, ready]);

  // Gợi ý tải một lần, khi ô được bấm vào lần đầu
  const loadSuggested = () => {
    if (suggested !== null) return;
    setSuggested([]);
    searchUniversities({ pageSize: SUGGEST_SIZE, page: 1 })
      .then((r) => setSuggested(r.items))
      .catch(() => setSuggested([]));
  };

  const items = ready ? results : (suggested ?? []);
  // Đang tải (hoặc chưa có gì để hiện) thì ẩn hẳn khung, không hiện chữ chờ
  const showList = open && !disabled && (ready ? !loading : items.length > 0);

  const pick = (u: UniversityListItem) => {
    if (pickedCodes.includes(u.code)) return;
    onPick(u);
    setQuery("");
    setResults([]);
    setResultsFor("");
    setOpen(false);
  };

  return (
    <div className="relative w-full">
      <label className="sr-only" htmlFor={listId + "-input"}>{label}</label>
      <Search aria-hidden className="pointer-events-none absolute top-1/2 left-4 size-5 -translate-y-1/2 text-ink-3" />
      <input
        ref={ref}
        id={listId + "-input"}
        role="combobox"
        aria-expanded={showList}
        aria-controls={listId}
        aria-autocomplete="list"
        autoComplete="off"
        disabled={disabled}
        value={query}
        placeholder={placeholder}
        onChange={(e) => { setQuery(e.target.value); setOpen(true); }}
        onFocus={() => { setOpen(true); loadSuggested(); }}
        onClick={() => { setOpen(true); loadSuggested(); }}
        onBlur={() => setTimeout(() => setOpen(false), 120)}
        onKeyDown={(e) => {
          if (e.key === "Escape") { setOpen(false); return; }
          if (!showList || items.length === 0) return;
          if (e.key === "ArrowDown") { e.preventDefault(); setActive((a) => (a + 1) % items.length); }
          else if (e.key === "ArrowUp") { e.preventDefault(); setActive((a) => (a - 1 + items.length) % items.length); }
          else if (e.key === "Enter") { e.preventDefault(); pick(items[active]); }
        }}
        className={cn("h-14 w-full rounded-2xl border border-control-border bg-surface pl-12 text-base text-ink shadow-[var(--elev-1)] transition-[border-color,box-shadow] outline-none placeholder:text-ink-2 focus-visible:border-brand focus-visible:ring-3 focus-visible:ring-brand/20 disabled:cursor-not-allowed disabled:bg-muted disabled:text-ink-3", action ? "pr-36" : "pr-12")}
      />
      {loading && ready && <Spinner className={cn("absolute top-1/2 size-4 -translate-y-1/2 text-ink-3", action ? "right-32" : "right-4")} />}
      {action && <div className="absolute top-1/2 right-2.5 -translate-y-1/2">{action}</div>}
      {showList && (
        <div className="absolute z-20 mt-2 w-full overflow-hidden rounded-2xl border border-line bg-surface text-left shadow-lg">
          {!ready && (
            <p className="px-4 pt-3 pb-1 text-xs font-medium text-ink-2">
              Gợi ý
            </p>
          )}
          <ul id={listId} role="listbox" className="max-h-96 overflow-auto p-1.5">
            {ready && items.length === 0 && (
              <li className="px-3 py-3 text-sm text-ink-2">Không tìm thấy trường nào khớp «{query.trim()}».</li>
            )}
            {items.map((u, i) => (
              <SchoolRow
                key={u.code}
                school={u}
                active={i === active}
                picked={pickedCodes.includes(u.code)}
                onHover={() => setActive(i)}
                onPick={() => pick(u)}
              />
            ))}
          </ul>
        </div>
      )}
    </div>
  );
});

function SchoolRow({
  school,
  active,
  picked,
  onHover,
  onPick,
}: {
  school: UniversityListItem;
  active: boolean;
  picked: boolean;
  onHover: () => void;
  onPick: () => void;
}) {
  const cover = universityUrl(school.coverUrl);
  const place = [school.city, school.stateName ?? school.state].filter(Boolean).join(", ");
  const control = school.control ? schoolControlLabel(school.control) : null;
  const tuition = money(school.tuitionUsd);
  const total = money(school.totalCostUsd);
  return (
    <li
      role="option"
      aria-selected={active}
      aria-disabled={picked}
      onMouseDown={(e) => e.preventDefault()}
      onMouseEnter={onHover}
      onClick={onPick}
      className={cn(
        "flex cursor-pointer items-center gap-3.5 rounded-xl p-2 transition-colors",
        active && "bg-brand-wash",
        picked && "cursor-default opacity-50",
      )}
    >
      <span className="relative flex h-14 w-[4.5rem] shrink-0 items-center justify-center overflow-hidden rounded-lg bg-muted">
        {cover ? (
          <Image src={cover} alt="" fill sizes="72px" className="object-cover" />
        ) : (
          <Building2 aria-hidden className="size-5 text-ink-3" />
        )}
      </span>
      <span className="min-w-0 flex-1">
        <span className="block truncate text-sm font-semibold text-ink">{school.name}</span>
        <span className="block truncate text-xs text-ink-2">{[place, control].filter(Boolean).join(" · ") || school.code}</span>
        {(tuition || total) && (
          <span className="mt-0.5 block truncate text-xs text-ink-2 tabular-nums">
            {tuition && <>Học phí <span className="font-medium text-ink">{tuition}</span></>}
            {tuition && total && " · "}
            {total && <>Tổng <span className="font-medium text-ink">{total}</span>/năm</>}
          </span>
        )}
      </span>
      {picked && <span className="shrink-0 text-xs text-ink-2">Đã chọn</span>}
    </li>
  );
}
