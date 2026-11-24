"use client";

import { useEffect, useRef, useState, useTransition } from "react";
import { useRouter } from "next/navigation";
import {
  Building2Icon,
  ChevronLeftIcon,
  ChevronRightIcon,
  ListFilterIcon,
  RotateCcwIcon,
  SearchIcon,
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
import { UniversityCard } from "./UniversityCard";
import {
  AID_POLICY_OPTIONS,
  CAMPUS_OPTIONS,
  DEADLINE_ROUND_OPTIONS,
  IMPORTANCE_OPTIONS,
  TEST_POLICY_OPTIONS,
  UniversityFilters,
} from "./UniversityFilters";
import { UniversitySortSelect } from "./UniversitySortSelect";
import type { PagedResult } from "@/types/api";
import type {
  UniversityFilterOptions,
  UniversityListItem,
  UniversitySearchParams,
} from "@/types/university";
import { buildUniversitySearchQuery } from "@/lib/university-api";
import { activityLabel, housingTypeLabel, schoolControlLabel } from "@/lib/university-display";
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
  const [desktopFilterOpen, setDesktopFilterOpen] = useState(true);

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

    const qs = buildUniversitySearchQuery({
      ...updated,
      sortBy: updated.sortBy && updated.sortBy !== "name" ? updated.sortBy : undefined,
      rankListId: updated.sortBy === "rank" ? updated.rankListId : undefined,
      studyLevel: undefined,
      page: updated.page && updated.page > 1 ? updated.page : undefined,
      pageSize: undefined,
    });

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
  const usd = (v: number) => `$${v.toLocaleString("en-US")}`;
  const pct = (v: number) => `${Math.round(v * 100)}%`;
  const num = (v: number) => v.toLocaleString("en-US");

  const pushRangeChip = (
    key: string,
    prefix: string,
    min: number | undefined,
    max: number | undefined,
    format: (v: number) => string,
    onRemove: () => void,
  ) => {
    if (min === undefined && max === undefined) return;
    const range =
      min !== undefined && max !== undefined
        ? `${format(min)} – ${format(max)}`
        : min !== undefined
          ? `≥ ${format(min)}`
          : `≤ ${format(max!)}`;
    activeChips.push({ key, label: `${prefix}: ${range}`, onRemove });
  };

  const findOpt = (list: { value: string; label: string }[], val: string) =>
    list.find((o) => o.value === val)?.label ?? val;

  if (currentParams.search) {
    activeChips.push({
      key: "search",
      label: `Từ khóa: "${currentParams.search}"`,
      onRemove: () => applyParams({ search: undefined }),
    });
  }
  if (currentParams.state && currentParams.state !== "all") {
    activeChips.push({
      key: "state",
      label: `Bang: ${findOpt(options.states, currentParams.state)}`,
      onRemove: () => applyParams({ state: undefined }),
    });
  }
  if (currentParams.campusSetting) {
    activeChips.push({
      key: "campus",
      label: `Campus: ${findOpt(CAMPUS_OPTIONS, currentParams.campusSetting)}`,
      onRemove: () => applyParams({ campusSetting: undefined }),
    });
  }
  if (currentParams.control && currentParams.control !== "all") {
    activeChips.push({
      key: "control",
      label: `Loại: ${schoolControlLabel(currentParams.control)}`,
      onRemove: () => applyParams({ control: undefined }),
    });
  }
  if (currentParams.isIvy) {
    activeChips.push({
      key: "ivy",
      label: "Ivy League",
      onRemove: () => applyParams({ isIvy: undefined }),
    });
  }
  if (currentParams.major && currentParams.major !== "all") {
    activeChips.push({
      key: "major",
      label: `Ngành: ${findOpt(options.majors, currentParams.major)}`,
      onRemove: () => applyParams({ major: undefined }),
    });
  }

  pushRangeChip("acceptanceRate", "Tỷ lệ nhận", currentParams.minAcceptanceRate, currentParams.maxAcceptanceRate, pct, () =>
    applyParams({ minAcceptanceRate: undefined, maxAcceptanceRate: undefined }),
  );
  pushRangeChip(
    "intlAcceptanceRate",
    "Tỷ lệ nhận SV quốc tế",
    currentParams.minIntlAcceptanceRate,
    currentParams.maxIntlAcceptanceRate,
    pct,
    () => applyParams({ minIntlAcceptanceRate: undefined, maxIntlAcceptanceRate: undefined }),
  );
  if (currentParams.maxMinGpa4 !== undefined) {
    activeChips.push({
      key: "minGpa4",
      label: `GPA tối thiểu ≤ ${currentParams.maxMinGpa4.toFixed(1)}`,
      onRemove: () => applyParams({ maxMinGpa4: undefined }),
    });
  }
  pushRangeChip("avgGpa4", "GPA TB", currentParams.minAvgGpa4, currentParams.maxAvgGpa4, (v) => v.toFixed(1), () =>
    applyParams({ minAvgGpa4: undefined, maxAvgGpa4: undefined }),
  );
  pushRangeChip("sat", "SAT", currentParams.minSat, currentParams.maxSat, num, () =>
    applyParams({ minSat: undefined, maxSat: undefined }),
  );
  pushRangeChip("act", "ACT", currentParams.minAct, currentParams.maxAct, String, () =>
    applyParams({ minAct: undefined, maxAct: undefined }),
  );
  if (currentParams.satPolicy) {
    activeChips.push({
      key: "satPolicy",
      label: `SAT: ${findOpt(TEST_POLICY_OPTIONS, currentParams.satPolicy)}`,
      onRemove: () => applyParams({ satPolicy: undefined }),
    });
  }
  if (currentParams.actPolicy) {
    activeChips.push({
      key: "actPolicy",
      label: `ACT: ${findOpt(TEST_POLICY_OPTIONS, currentParams.actPolicy)}`,
      onRemove: () => applyParams({ actPolicy: undefined }),
    });
  }
  if (currentParams.maxMinIelts) {
    activeChips.push({
      key: "ielts",
      label: `IELTS ${currentParams.maxMinIelts.toFixed(1)}+`,
      onRemove: () => applyParams({ maxMinIelts: undefined }),
    });
  }
  if (currentParams.maxMinToefl) {
    activeChips.push({
      key: "toefl",
      label: `TOEFL ${currentParams.maxMinToefl}+`,
      onRemove: () => applyParams({ maxMinToefl: undefined }),
    });
  }
  if (currentParams.maxMinDuolingo) {
    activeChips.push({
      key: "duolingo",
      label: `DET ${currentParams.maxMinDuolingo}+`,
      onRemove: () => applyParams({ maxMinDuolingo: undefined }),
    });
  }
  if (currentParams.deadlineRound) {
    activeChips.push({
      key: "deadlineRound",
      label: `Vòng nộp: ${findOpt(DEADLINE_ROUND_OPTIONS, currentParams.deadlineRound)}`,
      onRemove: () => applyParams({ deadlineRound: undefined }),
    });
  }
  if (currentParams.c7TestImportance) {
    activeChips.push({
      key: "c7Test",
      label: `Coi trọng điểm thi: ${findOpt(IMPORTANCE_OPTIONS, currentParams.c7TestImportance)}`,
      onRemove: () => applyParams({ c7TestImportance: undefined }),
    });
  }
  if (currentParams.c7EcImportance) {
    activeChips.push({
      key: "c7Ec",
      label: `Coi trọng ngoại khóa: ${findOpt(IMPORTANCE_OPTIONS, currentParams.c7EcImportance)}`,
      onRemove: () => applyParams({ c7EcImportance: undefined }),
    });
  }
  if (currentParams.testOptional) {
    activeChips.push({
      key: "testOptional",
      label: "Không bắt buộc SAT/ACT",
      onRemove: () => applyParams({ testOptional: undefined }),
    });
  }

  pushRangeChip(
    "ratio",
    "SV : GV",
    currentParams.minStudentFacultyRatio,
    currentParams.maxStudentFacultyRatio,
    (v) => `${v}:1`,
    () => applyParams({ minStudentFacultyRatio: undefined, maxStudentFacultyRatio: undefined }),
  );
  pushRangeChip("retention", "Giữ chân năm 1", currentParams.minRetentionRate, currentParams.maxRetentionRate, pct, () =>
    applyParams({ minRetentionRate: undefined, maxRetentionRate: undefined }),
  );
  pushRangeChip("grad6yr", "Tốt nghiệp 6 năm", currentParams.minGradRate6Yr, currentParams.maxGradRate6Yr, pct, () =>
    applyParams({ minGradRate6Yr: undefined, maxGradRate6Yr: undefined }),
  );
  pushRangeChip("intlStudents", "SV quốc tế", currentParams.minIntlStudents, currentParams.maxIntlStudents, num, () =>
    applyParams({ minIntlStudents: undefined, maxIntlStudents: undefined }),
  );

  pushRangeChip("tuition", "Học phí", currentParams.minTuitionUsd, currentParams.maxTuitionUsd, usd, () =>
    applyParams({ minTuitionUsd: undefined, maxTuitionUsd: undefined }),
  );
  pushRangeChip("totalCost", "Tổng chi phí", currentParams.minTotalCostUsd, currentParams.maxTotalCostUsd, usd, () =>
    applyParams({ minTotalCostUsd: undefined, maxTotalCostUsd: undefined }),
  );
  pushRangeChip("living", "Sinh hoạt phí", currentParams.minLivingUsd, currentParams.maxLivingUsd, usd, () =>
    applyParams({ minLivingUsd: undefined, maxLivingUsd: undefined }),
  );
  if (currentParams.maxApplicationFeeUsd !== undefined) {
    activeChips.push({
      key: "appFee",
      label:
        currentParams.maxApplicationFeeUsd === 0
          ? "Miễn phí nộp hồ sơ"
          : `Phí nộp hồ sơ ≤ $${currentParams.maxApplicationFeeUsd}`,
      onRemove: () => applyParams({ maxApplicationFeeUsd: undefined }),
    });
  }
  if (currentParams.intlAidPolicy) {
    activeChips.push({
      key: "intlAidPolicy",
      label: `Hỗ trợ TC: ${findOpt(AID_POLICY_OPTIONS, currentParams.intlAidPolicy)}`,
      onRemove: () => applyParams({ intlAidPolicy: undefined }),
    });
  }
  pushRangeChip("intlAidAvg", "Học bổng TB", currentParams.minIntlAidAvgUsd, currentParams.maxIntlAidAvgUsd, usd, () =>
    applyParams({ minIntlAidAvgUsd: undefined, maxIntlAidAvgUsd: undefined }),
  );
  pushRangeChip("fyOnCampus", "SV năm 1 ở KTX", currentParams.minFyOnCampusPct, currentParams.maxFyOnCampusPct, pct, () =>
    applyParams({ minFyOnCampusPct: undefined, maxFyOnCampusPct: undefined }),
  );
  if (currentParams.housingType) {
    activeChips.push({
      key: "housingType",
      label: `Nhà ở: ${housingTypeLabel(currentParams.housingType)}`,
      onRemove: () => applyParams({ housingType: undefined }),
    });
  }
  if (currentParams.activity) {
    activeChips.push({
      key: "activity",
      label: `Hoạt động: ${activityLabel(currentParams.activity)}`,
      onRemove: () => applyParams({ activity: undefined }),
    });
  }
  if (currentParams.hasIntlAid) {
    activeChips.push({
      key: "intlAid",
      label: "Có hỗ trợ tài chính SV quốc tế",
      onRemove: () => applyParams({ hasIntlAid: undefined }),
    });
  }
  if (currentParams.hasScholarship) {
    activeChips.push({
      key: "hasScholarship",
      label: "Có học bổng riêng",
      onRemove: () => applyParams({ hasScholarship: undefined }),
    });
  }
  if (currentParams.intlHousing) {
    activeChips.push({
      key: "intlHousing",
      label: "Có KTX cho SV quốc tế",
      onRemove: () => applyParams({ intlHousing: undefined }),
    });
  }
  if (currentParams.maxRank) {
    activeChips.push({
      key: "rank",
      label: `Top ${currentParams.maxRank}`,
      onRemove: () => applyParams({ maxRank: undefined }),
    });
  }

  return (
    <div className="space-y-4 lg:flex lg:min-h-0 lg:flex-1 lg:flex-col lg:space-y-0">
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

      <div className="sticky top-(--header-h) z-20 -mx-4 bg-background px-4 py-2 sm:-mx-8 sm:px-8 lg:static lg:mx-0 lg:shrink-0 lg:px-0 lg:pt-0 lg:pb-4">
        <div className="flex flex-wrap items-center justify-between gap-4 lg:gap-8">
          <div className="flex items-center gap-3">
            <div className="lg:hidden">
              <Sheet open={mobileFilterOpen} onOpenChange={setMobileFilterOpen}>
                <SheetTrigger
                  className={cn(
                    buttonVariants({ variant: "ghost", size: "lg" }),
                    "gap-2 border-0 bg-transparent! px-2 text-sm font-medium hover:bg-transparent! hover:text-brand active:bg-transparent!",
                  )}
                >
                  <ListFilterIcon className="size-4" />
                  <span>Bộ lọc</span>
                  {activeChips.length > 0 && (
                    <span className="rounded-full bg-brand-wash px-1.5 py-0.5 text-xs font-semibold text-brand-ink tabular-nums">
                      {activeChips.length}
                    </span>
                  )}
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
                      hideTitle
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
                aria-controls="university-filters-sidebar"
                onClick={() => setDesktopFilterOpen(true)}
                className="hidden gap-2 border-0 bg-transparent! px-2 text-sm font-medium hover:bg-transparent! hover:text-brand active:bg-transparent! lg:inline-flex"
              >
                <ListFilterIcon className="size-4" />
                <span>Bộ lọc</span>
                {activeChips.length > 0 && (
                  <span className="rounded-full bg-brand-wash px-1.5 py-0.5 text-xs font-semibold text-brand-ink tabular-nums">
                    {activeChips.length}
                  </span>
                )}
              </Button>
            )}

            {isPending && (
              <span className="text-xs text-muted-foreground animate-pulse" aria-live="polite">
                Đang tải...
              </span>
            )}
          </div>

          <UniversitySortSelect
            value={currentParams.sortBy || "name"}
            onValueChange={(sortBy) =>
              applyParams(
                {
                  sortBy,
                  rankListId:
                    sortBy === "rank"
                      ? currentParams.rankListId || options.rankingLists[0]?.value || "USN27-NU"
                      : undefined,
                },
                false,
              )
            }
            rankingLists={options.rankingLists}
            rankListId={currentParams.rankListId || options.rankingLists[0]?.value || "USN27-NU"}
            onRankListChange={(rankListId) => applyParams({ sortBy: "rank", rankListId }, false)}
          />
        </div>
      </div>

      <div
        className={cn(
          "grid grid-cols-1 items-start gap-8 lg:min-h-0 lg:flex-1 lg:items-stretch",
          desktopFilterOpen && "lg:grid-cols-[272px_minmax(0,1fr)] xl:grid-cols-[300px_minmax(0,1fr)]",
        )}
      >
        <aside
          id="university-filters-sidebar"
          className={cn(
            "hidden lg:h-full lg:overflow-y-auto lg:overflow-x-hidden lg:px-2 lg:pb-6 [scrollbar-width:thin]",
            desktopFilterOpen && "lg:block",
          )}
        >
          <UniversityFilters
            options={options}
            values={currentParams}
            hideTitle
            onChange={(patch) => applyParams(patch)}
            onReset={handleResetFilters}
          />
        </aside>

        <div ref={resultsRef} className="space-y-5 pt-1.5 lg:h-full lg:overflow-y-auto lg:px-1.5 lg:pb-8">

          {activeChips.length > 0 && (
            <div className="flex flex-wrap items-center gap-2 pt-1">
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
                className="ml-auto px-2.5 text-muted-foreground hover:text-foreground"
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
                "grid grid-cols-[repeat(auto-fill,minmax(min(100%,max(270px,calc((100%_-_4.5rem)/4))),1fr))] gap-6",
                isPending && "pointer-events-none opacity-60 transition-opacity",
              )}
            >
              {initialData.items.map((uni) => (
                <UniversityCard key={uni.id} university={uni} sortBy={currentParams.sortBy} />
              ))}
            </div>
          )}

          {initialData.totalPages > 1 && (
            <div className="flex items-center justify-center gap-4 pt-4 sm:justify-end sm:gap-5">
              <Button
                type="button"
                variant="outline"
                size="lg"
                className="border-line bg-surface px-5 text-foreground shadow-xs hover:bg-muted"
                disabled={isPending || initialData.page <= 1}
                onClick={() => applyParams({ page: initialData.page - 1 }, false)}
              >
                <ChevronLeftIcon /> Trước
              </Button>
              <span className="text-sm font-medium text-muted-foreground tabular-nums">
                Trang {initialData.page} / {Math.max(initialData.totalPages, 1)}
              </span>
              <Button
                type="button"
                variant="outline"
                size="lg"
                className="border-line bg-surface px-5 text-foreground shadow-xs hover:bg-muted"
                disabled={isPending || initialData.page >= initialData.totalPages}
                onClick={() => applyParams({ page: initialData.page + 1 }, false)}
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
