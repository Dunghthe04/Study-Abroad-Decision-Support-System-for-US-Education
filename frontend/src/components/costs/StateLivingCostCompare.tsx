"use client";

import { CheckIcon, Search, X } from "lucide-react";
import dynamic from "next/dynamic";
import { useId, useMemo, useState } from "react";
import { Cell, CompareTable, CornerCell, HeadCell, RowHead } from "@/components/costs/CompareTable";
import { MissingValue } from "@/components/shared/missing-value";
import { REGIONS, rankCells, versusUs } from "@/lib/cost-format";
import { httpLink } from "@/lib/university-display";
import { cn } from "@/lib/utils";
import type { StateLivingCost } from "@/types/cost";

const MAX = 6;

// Dữ liệu đường vẽ bản đồ khoảng 100KB: chỉ tải khi mở chế độ Bang
const UsStateMap = dynamic(() => import("@/components/costs/UsStateMap").then((m) => m.UsStateMap), {
  ssr: false,
  loading: () => <div className="aspect-[975/610] w-full animate-pulse bg-muted" aria-hidden />,
});

/**
 * UC-28: chọn các bang trên bản đồ (hoặc tìm theo tên) và đặt cạnh nhau. Giá trị là chỉ số giá vùng (Mỹ = 100), không phải số USD,
 * nên không đổi thành USD/năm. Bang chưa có dữ liệu ghi rõ, không ước đoán. Mỗi hàng đậm và đổ xám ở bang rẻ nhất.
 */
export function StateLivingCostCompare({ states }: { states: StateLivingCost[] }) {
  const [picked, setPicked] = useState<string[]>([]);
  const chosen = picked.map((c) => states.find((s) => s.code === c)).filter((s): s is StateLivingCost => !!s);
  const hasAnyData = states.some((s) => s.index !== null);
  const toggle = (code: string) =>
    setPicked((p) => (p.includes(code) ? p.filter((c) => c !== code) : p.length >= MAX ? p : [...p, code]));

  const compare = chosen.length >= 2;
  const indexRanks = compare ? rankCells(chosen.map((s) => s.index)) : [];
  const rentRanks = compare ? rankCells(chosen.map((s) => s.housingRentsIndex)) : [];

  return (
    <div className="space-y-10">
      {!hasAnyData && (
        <p className="mx-auto max-w-3xl rounded-xl border border-warn/25 bg-warn-wash p-4 text-center text-sm text-warn">
          Chưa có dữ liệu chi phí sinh hoạt cho bang nào. Dữ liệu sẽ hiện khi được nhập kèm nguồn và ngày lấy.
        </p>
      )}

      <div className="mx-auto w-full max-w-4xl space-y-5">
        <StateSearch states={states} picked={picked} max={MAX} onToggle={toggle} />

        <UsStateMap states={states} picked={picked} max={MAX} onToggle={toggle} />

        {chosen.length > 0 && (
          <div className="flex flex-wrap items-center justify-center gap-2">
            <ul className="flex flex-wrap justify-center gap-2" aria-label="Bang đã chọn">
              {chosen.map((s) => (
                <li key={s.code}>
                  <button
                    type="button"
                    onClick={() => toggle(s.code)}
                    aria-label={`Bỏ ${s.name}`}
                    className="inline-flex items-center gap-1.5 rounded-full bg-brand py-1 pr-2 pl-3 text-sm font-medium text-brand-foreground transition-opacity hover:opacity-85"
                  >
                    {s.name}
                    <X aria-hidden className="size-3.5" />
                  </button>
                </li>
              ))}
            </ul>
            <button type="button" onClick={() => setPicked([])} className="ml-1 text-sm font-medium text-brand underline-offset-4 hover:underline">
              Bỏ chọn tất cả
            </button>
          </div>
        )}
      </div>

      {chosen.length > 0 && (
        <section aria-labelledby="state-table" className="space-y-3">
          <h2 id="state-table" className="text-base font-semibold text-ink">So sánh {chosen.length} bang</h2>
          <CompareTable minWidth={320 + chosen.length * 150}>
            <thead>
              <tr>
                <CornerCell>Chỉ số giá vùng</CornerCell>
                {chosen.map((s) => (
                  <HeadCell key={s.code}>
                    {s.name}
                    <span className="mt-0.5 block text-sm font-normal text-ink-2">{REGIONS[s.region] ?? s.region}</span>
                  </HeadCell>
                ))}
              </tr>
            </thead>
            <tbody>
              <tr>
                <RowHead title="Giá sinh hoạt" sub="Mỹ = 100" />
                {chosen.map((s, i) => (
                  <Cell key={s.code} rank={indexRanks[i]}>
                    {s.index === null ? (
                      <MissingValue />
                    ) : (
                      <>
                        <div className="text-lg">{s.index}</div>
                        <div className="text-sm font-normal text-ink-2">{versusUs(s.index)}</div>
                      </>
                    )}
                  </Cell>
                ))}
              </tr>
              <tr>
                <RowHead title="Tiền thuê nhà" sub="Mỹ = 100" />
                {chosen.map((s, i) => (
                  <Cell key={s.code} rank={rentRanks[i]}>
                    {s.housingRentsIndex === null ? <MissingValue /> : <span className="text-lg">{s.housingRentsIndex}</span>}
                  </Cell>
                ))}
              </tr>
              <tr>
                <RowHead title="Năm số liệu" />
                {chosen.map((s) => <Cell key={s.code}>{s.year ?? <MissingValue />}</Cell>)}
              </tr>
              <tr>
                <RowHead title="Nguồn" />
                {chosen.map((s) => {
                  const sourceLink = httpLink(s.sourceUrl);
                  return (
                    <Cell key={s.code}>
                      <span className="text-sm">
                        {sourceLink ? <a href={sourceLink.href} target="_blank" rel="noopener noreferrer" className="text-brand underline underline-offset-2">{sourceLink.host}</a> : <MissingValue />}
                        {s.retrievedAt && <span className="block text-ink-2">Lấy ngày {new Date(s.retrievedAt).toLocaleDateString("vi-VN")}</span>}
                      </span>
                    </Cell>
                  );
                })}
              </tr>
            </tbody>
          </CompareTable>
        </section>
      )}

    </div>
  );
}

/** Ô tìm bang phía trên bản đồ: bấm vào là hiện cả danh sách, gõ để lọc theo tên hoặc mã (CA, TX...). Chọn lại bang đã chọn thì bỏ chọn. */
function StateSearch({
  states,
  picked,
  max,
  onToggle,
}: {
  states: StateLivingCost[];
  picked: string[];
  max: number;
  onToggle: (code: string) => void;
}) {
  const listId = useId();
  const [query, setQuery] = useState("");
  const [open, setOpen] = useState(false);
  const [active, setActive] = useState(0);
  const full = picked.length >= max;

  const matches = useMemo(() => {
    const q = query.trim().toLowerCase();
    const list = [...states].sort((a, b) => a.name.localeCompare(b.name, "en"));
    return q ? list.filter((s) => s.name.toLowerCase().includes(q) || s.code.toLowerCase() === q) : list;
  }, [states, query]);

  const pick = (s: StateLivingCost) => {
    if (!picked.includes(s.code) && full) return;
    onToggle(s.code);
    setQuery("");
    setOpen(false);
  };

  return (
    <div className="relative w-full">
      <label className="sr-only" htmlFor={listId + "-input"}>Tìm bang</label>
      <Search aria-hidden className="pointer-events-none absolute top-1/2 left-4 size-5 -translate-y-1/2 text-ink-3" />
      <input
        id={listId + "-input"}
        role="combobox"
        aria-expanded={open}
        aria-controls={listId}
        aria-autocomplete="list"
        autoComplete="off"
        value={query}
        placeholder={`Tìm bang theo tên hoặc mã (${picked.length}/${max})`}
        onChange={(e) => { setQuery(e.target.value); setOpen(true); setActive(0); }}
        onFocus={() => setOpen(true)}
        onClick={() => setOpen(true)}
        onBlur={() => setTimeout(() => setOpen(false), 120)}
        onKeyDown={(e) => {
          if (e.key === "Escape") { setOpen(false); return; }
          if (!open || matches.length === 0) return;
          if (e.key === "ArrowDown") { e.preventDefault(); setActive((a) => (a + 1) % matches.length); }
          else if (e.key === "ArrowUp") { e.preventDefault(); setActive((a) => (a - 1 + matches.length) % matches.length); }
          else if (e.key === "Enter") { e.preventDefault(); pick(matches[active]); }
        }}
        className="h-14 w-full rounded-2xl border border-control-border bg-surface pr-4 pl-12 text-base text-ink shadow-[var(--elev-1)] transition-[border-color,box-shadow] outline-none placeholder:text-ink-2 focus-visible:border-brand focus-visible:ring-3 focus-visible:ring-brand/20"
      />
      {open && (
        <ul id={listId} role="listbox" className="absolute z-20 mt-2 max-h-80 w-full overflow-auto rounded-2xl border border-line bg-surface p-1.5 text-left shadow-lg">
          {matches.length === 0 && <li className="px-3 py-3 text-sm text-ink-2">Không có bang nào khớp «{query.trim()}».</li>}
          {matches.map((s, i) => {
            const on = picked.includes(s.code);
            const locked = !on && full;
            return (
              <li
                key={s.code}
                role="option"
                aria-selected={on}
                aria-disabled={locked}
                onMouseDown={(e) => e.preventDefault()}
                onMouseEnter={() => setActive(i)}
                onClick={() => pick(s)}
                className={cn(
                  "flex cursor-pointer items-center justify-between gap-3 rounded-xl px-3 py-2.5 text-sm transition-colors",
                  i === active && "bg-brand-wash",
                  locked && "cursor-not-allowed opacity-40",
                )}
              >
                <span className="min-w-0 truncate font-medium text-ink">
                  {s.name} <span className="font-normal text-ink-2">{s.code}</span>
                </span>
                <span className="flex shrink-0 items-center gap-2 text-ink-2 tabular-nums">
                  {s.index !== null && s.index}
                  {on && <CheckIcon aria-hidden className="size-4 text-brand" />}
                </span>
              </li>
            );
          })}
        </ul>
      )}
    </div>
  );
}
