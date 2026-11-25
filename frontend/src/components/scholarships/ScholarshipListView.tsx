"use client";

import { useEffect, useRef, useState, useTransition } from "react";
import { useRouter } from "next/navigation";
import { AwardIcon, ChevronLeftIcon, ChevronRightIcon, ListFilterIcon, RotateCcwIcon, SearchIcon, XIcon } from "lucide-react";
import { Button, buttonVariants } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Sheet, SheetContent, SheetDescription, SheetHeader, SheetTitle, SheetTrigger } from "@/components/ui/sheet";
import { cn } from "@/lib/utils";
import type { PagedResult } from "@/types/api";
import type { ScholarshipItem, ScholarshipSearchParams } from "@/types/scholarship";
import { ScholarshipCard } from "./ScholarshipCard";
import { COVERAGE_FILTER_OPTIONS, KIND_FILTER_OPTIONS, LEVEL_OPTIONS, ScholarshipFilters } from "./ScholarshipFilters";

interface ScholarshipListViewProps {
  data: PagedResult<ScholarshipItem>;
  params: ScholarshipSearchParams;
  /** yyyy-MM-dd theo giờ Việt Nam, tính ở server để thẻ "Còn N ngày" không lệch khi hydrate */
  today: string;
}

const label = (list: { value: string; label: string }[], v: string) => list.find((o) => o.value === v)?.label ?? v;
const usd = (n: number) => `$${n.toLocaleString("en-US")}`;

export function ScholarshipListView({ data, params, today }: ScholarshipListViewProps) {
  const router = useRouter();
  const [isPending, startTransition] = useTransition();
  const [mobileFilterOpen, setMobileFilterOpen] = useState(false);
  const [desktopFilterOpen, setDesktopFilterOpen] = useState(true);

  // Đổi bộ lọc / trang: đưa cột kết quả về đầu (cột này tự cuộn ở >= lg)
  const resultsRef = useRef<HTMLDivElement>(null);
  const paramsKey = JSON.stringify(params);
  useEffect(() => {
    resultsRef.current?.scrollTo({ top: 0 });
  }, [paramsKey]);

  const navigate = (next: ScholarshipSearchParams) => {
    const qs = new URLSearchParams();
    for (const [k, v] of Object.entries(next)) {
      // deadlineFrom (tính từ includeExpired) và vnEligible (luôn bật) do server đặt, không đưa lên URL
      if (v === undefined || v === "" || k === "pageSize" || k === "deadlineFrom" || k === "vnEligible" || (k === "page" && v === 1)) continue;
      qs.set(k, String(v));
    }
    const q = qs.toString();
    startTransition(() => router.push(`/scholarships${q ? `?${q}` : ""}`));
  };
  const apply = (patch: Partial<ScholarshipSearchParams>) => navigate({ ...params, ...patch, page: 1 });
  const reset = () => navigate({});

  const chips: { key: string; label: string; clear: Partial<ScholarshipSearchParams> }[] = [];
  if (params.search) chips.push({ key: "search", label: `Từ khóa: "${params.search}"`, clear: { search: undefined } });
  if (params.studyLevel) chips.push({ key: "level", label: label(LEVEL_OPTIONS, params.studyLevel), clear: { studyLevel: undefined } });
  if (params.kind) chips.push({ key: "kind", label: label(KIND_FILTER_OPTIONS, params.kind), clear: { kind: undefined } });
  if (params.coverageType)
    chips.push({ key: "coverage", label: `Hỗ trợ: ${label(COVERAGE_FILTER_OPTIONS, params.coverageType)}`, clear: { coverageType: undefined } });
  if (params.includeExpired) chips.push({ key: "expired", label: "Gồm cả đã hết hạn", clear: { includeExpired: undefined } });
  if (params.minAmountUsd !== undefined || params.maxAmountUsd !== undefined) {
    const { minAmountUsd: lo, maxAmountUsd: hi } = params;
    const range = lo !== undefined && hi !== undefined ? `${usd(lo)} – ${usd(hi)}` : lo !== undefined ? `≥ ${usd(lo)}` : `≤ ${usd(hi!)}`;
    chips.push({ key: "amount", label: `Giá trị: ${range}`, clear: { minAmountUsd: undefined, maxAmountUsd: undefined } });
  }
  if (params.maxMinGpa4 !== undefined) chips.push({ key: "gpa", label: `Đủ GPA ${params.maxMinGpa4.toFixed(1)}`, clear: { maxMinGpa4: undefined } });
  if (params.maxMinIelts !== undefined) chips.push({ key: "ielts", label: `Đủ IELTS ${params.maxMinIelts.toFixed(1)}`, clear: { maxMinIelts: undefined } });
  const filterCount = chips.filter((c) => c.key !== "search").length;

  const filterToggle = (
    <>
      <ListFilterIcon className="size-4" />
      <span>Bộ lọc</span>
      {filterCount > 0 && (
        <span className="rounded-full bg-brand-wash px-1.5 py-0.5 text-xs font-semibold text-brand-ink tabular-nums">{filterCount}</span>
      )}
    </>
  );
  const ghostToggle = "gap-2 border-0 bg-transparent! px-2 text-sm font-medium hover:bg-transparent! hover:text-brand active:bg-transparent!";

  return (
    <div className="space-y-4 lg:flex lg:min-h-0 lg:flex-1 lg:flex-col lg:space-y-0">
      <div className="sticky top-(--header-h) z-20 -mx-4 bg-background px-4 py-2 sm:-mx-8 sm:px-8 lg:static lg:mx-0 lg:shrink-0 lg:px-0 lg:pt-0 lg:pb-4">
        <div className="flex flex-wrap items-center justify-between gap-x-4 gap-y-2 lg:gap-8">
          <div className="flex items-center gap-3">
            <div className="lg:hidden">
              <Sheet open={mobileFilterOpen} onOpenChange={setMobileFilterOpen}>
                <SheetTrigger className={cn(buttonVariants({ variant: "ghost", size: "lg" }), ghostToggle)}>{filterToggle}</SheetTrigger>
                <SheetContent side="left" className="flex w-[320px] flex-col p-0 sm:w-[380px]">
                  <SheetHeader className="border-b border-line p-4">
                    <SheetTitle className="text-base font-bold">Bộ lọc học bổng</SheetTitle>
                    <SheetDescription className="text-xs">Chọn điều kiện để chỉ thấy học bổng bạn nộp được</SheetDescription>
                  </SheetHeader>
                  <div className="flex-1 overflow-y-auto p-4">
                    <ScholarshipFilters values={params} onChange={apply} />
                  </div>
                  <div className="sticky bottom-0 border-t border-line bg-background p-4">
                    <Button type="button" className="w-full" onClick={() => setMobileFilterOpen(false)}>
                      Xem {data.totalCount} học bổng
                    </Button>
                  </div>
                </SheetContent>
              </Sheet>
            </div>

            {desktopFilterOpen ? (
              <div className="hidden items-center justify-between gap-2 lg:flex lg:w-[272px] lg:px-2 xl:w-[300px]">
                <h2 className="font-heading text-2xl font-bold text-foreground">Bộ lọc</h2>
                <Button
                  type="button"
                  variant="ghost"
                  size="icon-sm"
                  onClick={() => setDesktopFilterOpen(false)}
                  aria-label="Đóng bộ lọc"
                  className="text-muted-foreground hover:bg-muted hover:text-foreground"
                >
                  <XIcon className="size-4" />
                </Button>
              </div>
            ) : (
              <Button
                type="button"
                variant="ghost"
                size="lg"
                aria-expanded={false}
                aria-controls="scholarship-filters-sidebar"
                onClick={() => setDesktopFilterOpen(true)}
                className={cn(ghostToggle, "hidden lg:inline-flex")}
              >
                {filterToggle}
              </Button>
            )}

            {isPending && (
              <span className="animate-pulse text-xs text-muted-foreground" aria-live="polite">
                Đang tải...
              </span>
            )}
          </div>

          <form
            role="search"
            key={params.search ?? ""}
            className="flex w-full gap-2 sm:w-auto sm:min-w-[340px] lg:min-w-[400px]"
            onSubmit={(e) => {
              e.preventDefault();
              const value = String(new FormData(e.currentTarget).get("search") ?? "").trim();
              apply({ search: value || undefined });
            }}
          >
            <label htmlFor="scholarship-search" className="sr-only">
              Tìm theo tên học bổng, đơn vị cấp hoặc trường
            </label>
            <Input
              id="scholarship-search"
              name="search"
              type="search"
              placeholder="Tìm học bổng hoặc trường"
              defaultValue={params.search ?? ""}
              className="bg-surface"
            />
            <Button type="submit" size="icon" aria-label="Tìm kiếm" className="shrink-0">
              <SearchIcon className="size-4" />
            </Button>
          </form>
        </div>
      </div>

      <div
        className={cn(
          "grid grid-cols-1 items-start gap-8 lg:min-h-0 lg:flex-1 lg:items-stretch",
          desktopFilterOpen && "lg:grid-cols-[272px_minmax(0,1fr)] xl:grid-cols-[300px_minmax(0,1fr)]",
        )}
      >
        <aside
          id="scholarship-filters-sidebar"
          className={cn(
            "hidden lg:h-full lg:overflow-x-hidden lg:overflow-y-auto lg:px-2 lg:pb-6 [scrollbar-width:thin]",
            desktopFilterOpen && "lg:block",
          )}
        >
          <ScholarshipFilters values={params} onChange={apply} />
        </aside>

        <div ref={resultsRef} className="space-y-4 pt-1.5 lg:h-full lg:overflow-y-auto lg:px-1.5 lg:pb-8">
          <div className={cn("flex flex-wrap items-center gap-2", chips.length === 0 && "hidden")}>
            {chips.map((chip) => (
              <span
                key={chip.key}
                className="inline-flex items-center gap-2 rounded-lg border border-line bg-surface px-3 py-1.5 text-sm font-medium text-foreground shadow-xs transition-colors hover:border-brand/40"
              >
                <span>{chip.label}</span>
                <Button
                  type="button"
                  variant="ghost"
                  size="icon-xs"
                  onClick={() => apply(chip.clear)}
                  className="-mr-1 rounded-full text-muted-foreground hover:bg-muted hover:text-foreground"
                  aria-label={`Xóa bộ lọc ${chip.label}`}
                >
                  <XIcon className="size-3.5" />
                </Button>
              </span>
            ))}
            {chips.length > 0 && (
              <Button variant="ghost" onClick={reset} className="ml-auto px-2.5 text-muted-foreground hover:text-foreground">
                Xóa tất cả
              </Button>
            )}
          </div>

          {data.items.length === 0 ? (
            <div className="space-y-4 py-16 text-center">
              <div className="mx-auto flex size-14 items-center justify-center rounded-full bg-muted text-muted-foreground">
                <AwardIcon className="size-7" />
              </div>
              <div className="space-y-1">
                <h3 className="font-heading text-lg font-bold text-foreground">Chưa có học bổng khớp bộ lọc</h3>
                <p className="mx-auto max-w-md text-sm text-muted-foreground">
                  Thử bỏ bớt điều kiện về giá trị, GPA hoặc IELTS, hoặc tìm bằng từ khóa khác.
                </p>
              </div>
              {chips.length > 0 && (
                <Button onClick={reset} variant="outline" className="gap-2">
                  <RotateCcwIcon className="size-4" />
                  Đặt lại tất cả bộ lọc
                </Button>
              )}
            </div>
          ) : (
            <ul aria-busy={isPending} className={cn("space-y-4", isPending && "pointer-events-none opacity-60 transition-opacity")}>
              {data.items.map((s) => (
                <li key={s.id}>
                  <ScholarshipCard scholarship={s} today={today} />
                </li>
              ))}
            </ul>
          )}

          {data.totalPages > 1 && (
            <div className="flex items-center justify-center gap-4 pt-4 sm:justify-end sm:gap-5">
              <Button
                type="button"
                variant="outline"
                size="lg"
                className="border-line bg-surface px-5 text-foreground shadow-xs hover:bg-muted"
                disabled={isPending || data.page <= 1}
                onClick={() => navigate({ ...params, page: data.page - 1 })}
              >
                <ChevronLeftIcon /> Trước
              </Button>
              <span className="text-sm font-medium text-muted-foreground tabular-nums">
                Trang {data.page} / {data.totalPages}
              </span>
              <Button
                type="button"
                variant="outline"
                size="lg"
                className="border-line bg-surface px-5 text-foreground shadow-xs hover:bg-muted"
                disabled={isPending || data.page >= data.totalPages}
                onClick={() => navigate({ ...params, page: data.page + 1 })}
              >
                Sau <ChevronRightIcon />
              </Button>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
