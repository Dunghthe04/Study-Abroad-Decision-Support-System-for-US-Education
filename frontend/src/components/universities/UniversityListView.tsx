"use client";

import { useState, useTransition } from "react";
import { useRouter } from "next/navigation";
import {
  Building2Icon,
  RotateCcwIcon,
  SlidersHorizontalIcon,
  XIcon,
} from "lucide-react";
import { Button, buttonVariants } from "@/components/ui/button";
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
      label: `Học phí <= $${currentParams.maxTuitionUsd.toLocaleString()}`,
      onRemove: () => applyParams({ maxTuitionUsd: undefined }),
    });
  }

  if (currentParams.maxMinIelts) {
    activeChips.push({
      key: "ielts",
      label: `IELTS <= ${currentParams.maxMinIelts}`,
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
    activeChips.push({
      key: "acceptanceRate",
      label: "Tỷ lệ trúng tuyển",
      onRemove: () => applyParams({ minAcceptanceRate: undefined, maxAcceptanceRate: undefined }),
    });
  }

  return (
    <div className="space-y-6">
      <div className="grid grid-cols-1 lg:grid-cols-4 gap-8 items-start">
        <aside className="hidden lg:block lg:col-span-1 sticky top-20">
          <UniversityFilters
            options={options}
            values={currentParams}
            onChange={(patch) => applyParams(patch)}
            onReset={handleResetFilters}
          />
        </aside>

        <div className="lg:col-span-3 space-y-5">
          <div className="flex flex-wrap items-center justify-between gap-4">
            <div className="flex items-center gap-3">
              <div className="lg:hidden">
                <Sheet open={mobileFilterOpen} onOpenChange={setMobileFilterOpen}>
                  <SheetTrigger
                    className={cn(buttonVariants({ variant: "outline", size: "sm" }), "gap-2 cursor-pointer")}
                  >
                    <SlidersHorizontalIcon className="size-4 text-brand" />
                    <span>Bộ lọc ({activeChips.length})</span>
                  </SheetTrigger>
                  <SheetContent side="left" className="w-[320px] sm:w-[380px] p-0 overflow-y-auto">
                    <SheetHeader className="p-4 border-b border-line">
                      <SheetTitle className="text-base font-bold">Bộ lọc tìm trường</SheetTitle>
                      <SheetDescription className="text-xs">
                        Tùy chỉnh tiêu chí để tìm trường phù hợp nhất
                      </SheetDescription>
                    </SheetHeader>
                    <div className="p-4">
                      <UniversityFilters
                        options={options}
                        values={currentParams}
                        onChange={(patch) => {
                          applyParams(patch);
                          setMobileFilterOpen(false);
                        }}
                        onReset={() => {
                          handleResetFilters();
                          setMobileFilterOpen(false);
                        }}
                      />
                    </div>
                  </SheetContent>
                </Sheet>
              </div>

              <div className="text-sm font-medium text-foreground">
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
                  <button
                    type="button"
                    onClick={chip.onRemove}
                    className="rounded-full p-0.5 text-muted-foreground hover:bg-muted hover:text-foreground"
                    aria-label={`Xóa bộ lọc ${chip.label}`}
                  >
                    <XIcon className="size-3.5" />
                  </button>
                </span>
              ))}
              <Button
                variant="ghost"
                size="sm"
                onClick={handleResetFilters}
                className="h-8 px-2.5 text-sm text-muted-foreground hover:text-foreground"
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
            <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-6">
              {initialData.items.map((uni) => (
                <UniversityCard key={uni.id} university={uni} />
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
