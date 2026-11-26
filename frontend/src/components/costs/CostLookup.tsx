"use client";

import { CheckIcon, ChevronsUpDownIcon, GraduationCapIcon, MapPinIcon } from "lucide-react";
import Link from "next/link";
import { SchoolCostLookup } from "@/components/costs/SchoolCostLookup";
import { StateLivingCostCompare } from "@/components/costs/StateLivingCostCompare";
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger } from "@/components/ui/dropdown-menu";
import { ErrorFallback } from "@/components/ui/error-fallback";
import { cn } from "@/lib/utils";
import type { StateLivingCost } from "@/types/cost";

export type CostMode = "school" | "state";

const MODES: { value: CostMode; label: string; href: string; icon: typeof GraduationCapIcon; hint: string }[] = [
  { value: "school", label: "Trường", href: "/costs", icon: GraduationCapIcon, hint: "Ước tính và so sánh từ 1 đến 4 trường" },
  { value: "state", label: "Bang", href: "/costs?mode=state", icon: MapPinIcon, hint: "So sánh mặt bằng giá sinh hoạt theo bang" },
];

const DESCRIPTION: Record<CostMode, string | null> = {
  school: "Xem chi phí của một trường, hoặc thêm đến 4 trường để so sánh.",
  state: null,
};

/** Trang "Tra cứu chi phí": một chỗ cho ước tính, so sánh trường và so sánh bang; công tắc Trường / Bang nằm ngay cạnh tiêu đề. */
export function CostLookup({
  mode,
  initialCodes,
  states,
}: {
  mode: CostMode;
  initialCodes: string[];
  /** Chỉ có khi mode = "state"; null là tải lỗi */
  states: StateLivingCost[] | null;
}) {
  return (
    // Chưa chọn trường nào (SchoolCostLookup đặt data-empty): đưa tiêu đề và ô tìm ra giữa vùng nhìn thấy
    <section className="@container mx-auto flex w-full max-w-6xl flex-col gap-10 px-4 py-10 pb-20 sm:px-6 has-data-empty:min-h-[calc(100dvh-var(--header-h,6.5rem))] has-data-empty:justify-center has-data-empty:pb-[12vh]">
      <header className="flex flex-col items-center gap-4 text-center">
        <div className="flex flex-wrap items-center justify-center gap-x-3 gap-y-2">
          <h1 className="font-heading text-3xl font-semibold tracking-tight text-ink sm:text-4xl">Tra cứu chi phí</h1>
          <ModeMenu mode={mode} />
        </div>
        {DESCRIPTION[mode] && <p className="mx-auto max-w-2xl text-base text-pretty text-ink-2">{DESCRIPTION[mode]}</p>}
      </header>

      {mode === "school" ? (
        <SchoolCostLookup initialCodes={initialCodes} />
      ) : states ? (
        <StateLivingCostCompare states={states} />
      ) : (
        <ErrorFallback title="Không thể tải dữ liệu các bang" description="Đã xảy ra lỗi khi kết nối với máy chủ API. Vui lòng thử lại sau." />
      )}
    </section>
  );
}

function ModeMenu({ mode }: { mode: CostMode }) {
  const current = MODES.find((m) => m.value === mode) ?? MODES[0];
  return (
    <DropdownMenu>
      <DropdownMenuTrigger
        aria-label={`Đang tra cứu theo ${current.label}. Đổi cách tra cứu`}
        className="group inline-flex h-10 cursor-pointer items-center gap-2 rounded-xl border border-transparent bg-transparent px-3 font-heading text-xl font-semibold tracking-tight text-brand outline-none transition-[background-color] hover:bg-brand-wash/60 focus-visible:ring-3 focus-visible:ring-brand/25 data-popup-open:bg-brand-wash/60 sm:h-11 sm:text-2xl"
      >
        {current.label}
        <ChevronsUpDownIcon aria-hidden className="size-4 shrink-0 text-brand/70 transition-transform group-data-popup-open:scale-110" />
      </DropdownMenuTrigger>
      <DropdownMenuContent align="center" sideOffset={8} className="w-72 p-1.5">
        {MODES.map((m) => {
          const Icon = m.icon;
          const selected = m.value === mode;
          return (
            <DropdownMenuItem
              key={m.value}
              render={<Link href={m.href} scroll={false} aria-current={selected ? "true" : undefined} />}
              className="cursor-pointer items-start gap-3 rounded-lg px-3 py-2.5"
            >
              <Icon aria-hidden strokeWidth={1.75} className={cn("mt-0.5 size-5 shrink-0", selected ? "text-brand" : "text-ink-2")} />
              <span className="min-w-0 flex-1">
                <span className="block text-[13px] font-semibold text-ink">{m.label}</span>
                <span className="block text-xs text-ink-2">{m.hint}</span>
              </span>
              {selected && <CheckIcon aria-hidden className="mt-1 size-4 shrink-0 text-brand" />}
            </DropdownMenuItem>
          );
        })}
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
