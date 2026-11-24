"use client";

import { useEffect, useRef, useState, useTransition } from "react";
import { useRouter } from "next/navigation";
import {
  Building2Icon,
  RotateCcwIcon,
  SearchIcon,
  SlidersHorizontalIcon,
  XIcon,
} from "lucide-react";
import { Button, buttonVariants } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetHeader,
  SheetTitle,
  SheetTrigger,
} from "@/components/ui/sheet";
import { TablePagination } from "@/components/admin/shared/TablePagination";
import { UniversityCard } from "./UniversityCard";
import { UniversityFilters } from "./UniversityFilters";
import { UniversitySortSelect } from "./UniversitySortSelect";
import type { PagedResult } from "@/types/api";
import type {
  UniversityFilterOptions,
  UniversityListItem,
  UniversitySearchParams,
} from "@/types/university";
import { schoolControlLabel } from "@/lib/university-display";
import { cn } from "@/lib/utils";

const CAMPUS_LABELS: Record<string, string> = {
  city: "Thành phố",
  suburb: "Ngoại ô",
  town: "Thị trấn",
  rural: "Nông thôn",
};

interface UniversityListViewProps {
  initialData: PagedResult<UniversityListItem>;
  options: UniversityFilterOptions;
  currentParams: UniversitySearchParams;
}

export function UniversityListView({
  initialData,
  options,
  currentParams,
}: UniversityListViewProps) {
  const router = useRouter();
  const [isPending, startTransition] = useTransition();

  const [mobileFilterOpen, setMobileFilterOpen] = useState(false);

  // Chuyển trang / đổi bộ lọc: đưa cột kết quả về đầu (cột này tự cuộn ở >= lg)
  const resultsRef = useRef<HTMLDivElement>(null);
  const paramsKey = JSON.stringify(currentParams);
  useEffect(() => {
    resultsRef.current?.scrollTo({ top: 0 });
  }, [paramsKey]);

  const applyParams = (next: Partial<UniversitySearchParams>, resetPage = true) => {
    const updated: UniversitySearchParams = {
      ...currentParams,
      ...next,
      page: resetPage ? 1 : next.page ?? currentParams.page ?? 1,
    };

    const qs = new URLSearchParams();
    if (updated.search?.trim()) qs.set("search", updated.search.trim());
    if (updated.state && updated.state !== "all") qs.set("state", updated.state);
    if (updated.control && updated.control !== "all") qs.set("control", updated.control);
    if (updated.major && updated.major !== "all") qs.set("major", updated.major);
    if (updated.maxTuitionUsd && updated.maxTuitionUsd > 0)
      qs.set("maxTuitionUsd", updated.maxTuitionUsd.toString());
    if (updated.maxTotalCostUsd && updated.maxTotalCostUsd > 0)
      qs.set("maxTotalCostUsd", updated.maxTotalCostUsd.toString());
    if (updated.maxMinIelts && updated.maxMinIelts > 0)
      qs.set("maxMinIelts", updated.maxMinIelts.toString());
    if (updated.minAcceptanceRate !== undefined)
      qs.set("minAcceptanceRate", updated.minAcceptanceRate.toString());
    if (updated.maxAcceptanceRate !== undefined)
      qs.set("maxAcceptanceRate", updated.maxAcceptanceRate.toString());
    if (updated.isIvy === true) qs.set("isIvy", "true");
    if (updated.testOptional === true) qs.set("testOptional", "true");
    if (updated.hasIntlAid === true) qs.set("hasIntlAid", "true");
    if (updated.campusSetting) qs.set("campusSetting", updated.campusSetting);
    if (updated.maxRank && updated.maxRank > 0) qs.set("maxRank", updated.maxRank.toString());
    if (updated.sortBy && updated.sortBy !== "rank") qs.set("sortBy", updated.sortBy);
    if (updated.page && updated.page > 1) qs.set("page", updated.page.toString());

    startTransition(() => {
      const q = qs.toString();
      router.push(`/universities${q ? `?${q}` : ""}`);
    });
  };

  const handleResetFilters = () => {
    startTransition(() => {
      router.push("/universities");
    });
  };

  const activeChips: { key: string; label: string; onRemove: () => void }[] = [];

  if (currentParams.search) {
    activeChips.push({
      key: "search",
      label: `Từ khóa: "${currentParams.search}"`,
      onRemove: () => applyParams({ search: undefined }),
    });
  }

  if (currentParams.state && currentParams.state !== "all") {
    const stateObj = options.states.find((s) => s.value === currentParams.state);
    activeChips.push({
      key: "state",
      label: `Bang: ${stateObj?.label ?? currentParams.state}`,
      onRemove: () => applyParams({ state: undefined }),
    });
  }

  if (currentParams.control && currentParams.control !== "all") {
    activeChips.push({
      key: "control",
      label: `Loại: ${schoolControlLabel(currentParams.control)}`,
      onRemove: () => applyParams({ control: undefined }),
    });
  }

  if (currentParams.major && currentParams.major !== "all") {
    activeChips.push({
      key: "major",
      label: `Ngành: ${currentParams.major}`,
      onRemove: () => applyParams({ major: undefined }),
    });
  }

  if (currentParams.maxTuitionUsd) {
    activeChips.push({
      key: "tuition",
      label: `Học phí ≤ $${currentParams.maxTuitionUsd.toLocaleString("en-US")}`,
      onRemove: () => applyParams({ maxTuitionUsd: undefined }),
    });
  }

  if (currentParams.maxTotalCostUsd) {
    activeChips.push({
      key: "totalCost",
      label: `Tổng chi phí ≤ $${currentParams.maxTotalCostUsd.toLocaleString("en-US")}`,
      onRemove: () => applyParams({ maxTotalCostUsd: undefined }),
    });
  }

  if (currentParams.maxMinIelts) {
    activeChips.push({
      key: "ielts",
      label: `IELTS tối thiểu ≤ ${currentParams.maxMinIelts.toFixed(1)}`,
      onRemove: () => applyParams({ maxMinIelts: undefined }),
    });
  }

  if (currentParams.isIvy) {
    activeChips.push({
      key: "ivy",
      label: "Ivy League",
      onRemove: () => applyParams({ isIvy: undefined }),
    });
  }

  if (currentParams.minAcceptanceRate !== undefined || currentParams.maxAcceptanceRate !== undefined) {
    const { minAcceptanceRate: min, maxAcceptanceRate: max } = currentParams;
    const pct = (v: number) => `${Math.round(v * 100)}%`;
    const range =
      min !== undefined && max !== undefined
        ? `${pct(min)} – ${pct(max)}`
        : min !== undefined
          ? `Trên ${pct(min)}`
          : `Dưới ${pct(max!)}`;
    activeChips.push({
      key: "acceptanceRate",
      label: `Tỷ lệ nhận: ${range}`,
      onRemove: () => applyParams({ minAcceptanceRate: undefined, maxAcceptanceRate: undefined }),
    });
  }

  if (currentParams.maxRank) {
    activeChips.push({
      key: "rank",
      label: `Top ${currentParams.maxRank}`,
      onRemove: () => applyParams({ maxRank: undefined }),
    });
  }

  if (currentParams.campusSetting) {
    activeChips.push({
      key: "campus",
      label: `Campus: ${CAMPUS_LABELS[currentParams.campusSetting] ?? currentParams.campusSetting}`,
      onRemove: () => applyParams({ campusSetting: undefined }),
    });
  }

  if (currentParams.testOptional) {
    activeChips.push({
      key: "testOptional",
      label: "Không bắt buộc SAT/ACT",
      onRemove: () => applyParams({ testOptional: undefined }),
    });
  }

  if (currentParams.hasIntlAid) {
    activeChips.push({
      key: "intlAid",
      label: "Có hỗ trợ tài chính SV quốc tế",
      onRemove: () => applyParams({ hasIntlAid: undefined }),
    });
  }

  return (
    <div className="lg:flex lg:min-h-0 lg:flex-1 lg:flex-col">
      <div className="grid grid-cols-1 items-start gap-8 lg:min-h-0 lg:flex-1 lg:grid-cols-4 lg:items-stretch">
        <aside className="hidden lg:col-span-1 lg:block lg:h-full lg:overflow-y-auto lg:pr-2 lg:pb-6">
          <UniversityFilters
            options={options}
            values={currentParams}
            onChange={(patch) => applyParams(patch)}
            onReset={handleResetFilters}
          />
        </aside>

        <div ref={resultsRef} className="space-y-5 lg:col-span-3 lg:h-full lg:overflow-y-auto lg:pr-1 lg:pb-8">
          <form
            role="search"
            key={currentParams.search ?? ""}
            className="flex gap-2 md:hidden"
            onSubmit={(e) => {
              e.preventDefault();
              const value = new FormData(e.currentTarget).get("search");
              applyParams({ search: String(value ?? "").trim() || undefined });
            }}
          >
            <label htmlFor="uni-page-search" className="sr-only">
              Tìm theo tên trường hoặc thành phố
            </label>
            <Input
              id="uni-page-search"
              name="search"
              type="search"
              placeholder="Tìm theo tên trường hoặc thành phố"
              defaultValue={currentParams.search ?? ""}
            />
            <Button type="submit" size="icon" aria-label="Tìm kiếm">
              <SearchIcon className="size-4" />
            </Button>
          </form>

          <div className="flex flex-wrap items-center justify-between gap-4">
            <div className="flex items-center gap-3">
              <div className="lg:hidden">
                <Sheet open={mobileFilterOpen} onOpenChange={setMobileFilterOpen}>
                  <SheetTrigger
                    className={cn(buttonVariants({ variant: "outline", size: "sm" }), "gap-2")}
                  >
                    <SlidersHorizontalIcon className="size-4 text-brand" />
                    <span>Bộ lọc ({activeChips.length})</span>
                  </SheetTrigger>
                  <SheetContent side="left" className="flex w-[320px] flex-col p-0 sm:w-[380px]">
                    <SheetHeader className="p-4 border-b border-line">
                      <SheetTitle className="text-base font-bold">Bộ lọc tìm trường</SheetTitle>
                      <SheetDescription className="text-xs">
                        Tùy chỉnh tiêu chí để tìm trường phù hợp nhất
                      </SheetDescription>
                    </SheetHeader>
                    <div className="flex-1 overflow-y-auto p-4">
                      <UniversityFilters
                        options={options}
                        values={currentParams}
                        onChange={(patch) => applyParams(patch)}
                        onReset={() => {
                          handleResetFilters();
                          setMobileFilterOpen(false);
                        }}
                      />
                    </div>
                    <div className="sticky bottom-0 border-t border-line bg-background p-4">
                      <Button type="button" className="w-full" onClick={() => setMobileFilterOpen(false)}>
                        Xem {initialData.totalCount} trường
                      </Button>
                    </div>
                  </SheetContent>
                </Sheet>
              </div>

              <div className="text-sm font-medium text-foreground" aria-live="polite">
                <span className="font-bold text-brand">{initialData.totalCount}</span> trường phù hợp
                {isPending && <span className="ml-2 text-xs text-muted-foreground animate-pulse">(Đang tải...)</span>}
              </div>
            </div>

            <UniversitySortSelect
              value={currentParams.sortBy || "rank"}
              onValueChange={(sortBy) => applyParams({ sortBy }, false)}
            />
          </div>

          {activeChips.length > 0 && (
            <div className="flex flex-wrap items-center gap-2 pt-1">
              <span className="text-sm font-medium text-muted-foreground">Đang lọc:</span>
              {activeChips.map((chip) => (
                <span
                  key={chip.key}
                  className="inline-flex items-center gap-2 rounded-lg border border-line bg-surface px-3 py-1.5 text-sm font-medium text-foreground shadow-xs transition-colors hover:border-brand/40"
                >
                  <span>{chip.label}</span>
                  <Button
                    type="button"
                    variant="ghost"
                    size="icon-xs"
                    onClick={chip.onRemove}
                    className="-mr-1 rounded-full text-muted-foreground hover:bg-muted hover:text-foreground"
                    aria-label={`Xóa bộ lọc ${chip.label}`}
                  >
                    <XIcon className="size-3.5" />
                  </Button>
                </span>
              ))}
              <Button
                variant="ghost"
                size="default"
                onClick={handleResetFilters}
                className="px-2.5 text-muted-foreground hover:text-foreground"
              >
                Xóa tất cả
              </Button>
            </div>
          )}

          {initialData.items.length === 0 ? (
            <div className="py-16 text-center space-y-4">
              <div className="mx-auto flex size-14 items-center justify-center rounded-full bg-muted text-muted-foreground">
                <Building2Icon className="size-7" />
              </div>
              <div className="space-y-1">
                <h3 className="font-heading text-lg font-bold text-foreground">
                  Không tìm thấy trường nào phù hợp
                </h3>
                <p className="text-sm text-muted-foreground max-w-md mx-auto">
                  Hãy thử nới lỏng các tiêu chuẩn lọc (học phí, IELTS, bang) hoặc tìm kiếm bằng từ khóa khác.
                </p>
              </div>
              <Button onClick={handleResetFilters} variant="outline" className="gap-2">
                <RotateCcwIcon className="size-4" />
                <span>Đặt lại tất cả bộ lọc</span>
              </Button>
            </div>
          ) : (
            <div
              aria-busy={isPending}
              className={cn(
                "grid grid-cols-[repeat(auto-fill,minmax(min(100%,300px),1fr))] gap-6",
                isPending && "pointer-events-none opacity-60 transition-opacity",
              )}
            >
              {initialData.items.map((uni) => (
                <UniversityCard key={uni.id} university={uni} sortBy={currentParams.sortBy} />
              ))}
            </div>
          )}

          {initialData.totalPages > 1 && (
            <div className="pt-6 border-t border-line">
              <TablePagination
                page={initialData.page}
                totalPages={initialData.totalPages}
                totalCount={initialData.totalCount}
                itemLabel="trường đại học"
                disabled={isPending}
                onPageChange={(nextPage) => applyParams({ page: nextPage }, false)}
              />
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
