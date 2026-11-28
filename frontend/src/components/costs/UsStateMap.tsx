"use client";

import { useRef, useState } from "react";
import { REGIONS, versusUs } from "@/lib/cost-format";
import { US_MAP_VIEWBOX, US_STATE_PATHS } from "@/lib/us-state-paths";
import { cn } from "@/lib/utils";
import type { StateLivingCost } from "@/types/cost";

// Bang quá nhỏ để bấm trên bản đồ: có thêm nút mã bang bên dưới
const TINY = new Set(["DC", "RI", "DE", "CT", "NJ", "MD", "MA", "VT", "NH"]);

interface Hover {
  state: StateLivingCost;
  x: number;
  y: number;
}

/** Bản đồ nước Mỹ để chọn bang: bấm bang để chọn hoặc bỏ, bang đã chọn tô màu thương hiệu. Đạt tối đa thì các bang còn lại mờ đi. */
export function UsStateMap({
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
  const wrapRef = useRef<HTMLDivElement>(null);
  const [hover, setHover] = useState<Hover | null>(null);
  const full = picked.length >= max;
  const onMap = states.filter((s) => US_STATE_PATHS[s.name]);
  const offMap = states.filter((s) => !US_STATE_PATHS[s.name]);
  const tiny = [...onMap.filter((s) => TINY.has(s.code)), ...offMap];

  const place = (state: StateLivingCost, clientX: number, clientY: number) => {
    const box = wrapRef.current?.getBoundingClientRect();
    if (!box) return;
    setHover({ state, x: clientX - box.left, y: clientY - box.top });
  };

  // Bàn phím: tooltip đặt ở giữa khung ôm của bang đang focus
  const placeFromElement = (state: StateLivingCost, el: SVGPathElement) => {
    const box = wrapRef.current?.getBoundingClientRect();
    const rect = el.getBoundingClientRect();
    if (!box) return;
    setHover({ state, x: rect.left + rect.width / 2 - box.left, y: rect.top + rect.height / 2 - box.top });
  };

  return (
    <div className="space-y-3">
      <div ref={wrapRef} className="relative">
        <svg
          viewBox={US_MAP_VIEWBOX}
          role="group"
          aria-label="Bản đồ các bang nước Mỹ"
          className="block h-auto w-full"
          onMouseLeave={() => setHover(null)}
        >
          {onMap.map((s) => {
            const on = picked.includes(s.code);
            const locked = !on && full;
            return (
              <path
                key={s.code}
                d={US_STATE_PATHS[s.name]}
                role="button"
                tabIndex={locked ? -1 : 0}
                aria-pressed={on}
                aria-disabled={locked}
                aria-label={`${s.name}${s.index !== null ? `, chỉ số ${s.index}` : ""}`}
                onClick={() => !locked && onToggle(s.code)}
                onKeyDown={(e) => {
                  if ((e.key === "Enter" || e.key === " ") && !locked) {
                    e.preventDefault();
                    onToggle(s.code);
                  }
                }}
                onMouseMove={(e) => place(s, e.clientX, e.clientY)}
                onFocus={(e) => placeFromElement(s, e.currentTarget)}
                onBlur={() => setHover(null)}
                strokeLinejoin="round"
                className={cn(
                  "stroke-surface stroke-[1.25] outline-none transition-[fill,opacity] duration-150 focus-visible:stroke-brand-ink focus-visible:stroke-[2.5]",
                  on ? "fill-brand" : "fill-brand-wash",
                  locked ? "cursor-not-allowed opacity-40" : "cursor-pointer",
                  !on && !locked && "hover:fill-brand/40",
                  on && "hover:fill-brand/80",
                )}
              />
            );
          })}
        </svg>

        {hover && (
          <div
            role="presentation"
            className="pointer-events-none absolute z-10 -translate-x-1/2 -translate-y-[calc(100%+0.75rem)] rounded-lg bg-ink px-3.5 py-2.5 text-xs whitespace-nowrap text-surface shadow-lg"
            style={{ left: hover.x, top: hover.y }}
          >
            <p className="text-sm font-semibold">
              {hover.state.name} <span className="font-normal opacity-70">{REGIONS[hover.state.region] ?? hover.state.region}</span>
            </p>
            {hover.state.index === null ? (
              <p className="mt-1 opacity-80">Chưa có dữ liệu</p>
            ) : (
              <dl className="mt-1.5 space-y-0.5 tabular-nums">
                <div className="flex justify-between gap-6">
                  <dt className="opacity-70">Giá sinh hoạt (Mỹ = 100)</dt>
                  <dd className="font-semibold">{hover.state.index}</dd>
                </div>
                <div className="flex justify-between gap-6">
                  <dt className="opacity-70">Tiền thuê nhà (Mỹ = 100)</dt>
                  <dd className="font-semibold">{hover.state.housingRentsIndex ?? "—"}</dd>
                </div>
                <p className="pt-0.5 opacity-80">{versusUs(hover.state.index)}</p>
              </dl>
            )}
          </div>
        )}
      </div>

      {tiny.length > 0 && (
        <ul className="flex flex-wrap items-center justify-center gap-1.5" aria-label="Bang nhỏ khó bấm trên bản đồ">
          {tiny.map((s) => {
            const on = picked.includes(s.code);
            const locked = !on && full;
            return (
              <li key={s.code}>
                <button
                  type="button"
                  aria-pressed={on}
                  disabled={locked}
                  title={s.name}
                  aria-label={s.name}
                  onClick={() => onToggle(s.code)}
                  className={cn(
                    "h-8 min-w-10 rounded-md px-2 text-xs font-semibold tabular-nums transition-colors disabled:cursor-not-allowed disabled:opacity-40",
                    on ? "bg-brand text-brand-foreground hover:bg-brand/85" : "bg-brand-wash text-brand-ink hover:bg-brand/20",
                  )}
                >
                  {s.code}
                </button>
              </li>
            );
          })}
        </ul>
      )}
    </div>
  );
}
