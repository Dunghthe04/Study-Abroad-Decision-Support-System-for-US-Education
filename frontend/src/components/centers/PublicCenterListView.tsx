"use client";

import { useEffect, useId, useMemo, useRef, useState } from "react";
import Link from "next/link";
import {
  BadgeCheckIcon,
  Building2Icon,
  CheckIcon,
  ChevronLeftIcon,
  ChevronRightIcon,
  ClockIcon,
  FileCheckIcon,
  FileTextIcon,
  ListFilterIcon,
  RotateCcwIcon,
  PlusIcon,
  SearchIcon,
  StarIcon,
  XIcon,
} from "lucide-react";
import type { StudyCenter } from "@/types/api";
import { useAuth } from "@/contexts/AuthContext";
import { useProvinces } from "@/hooks/use-provinces";
import { getEnrichedCenterProfile } from "@/lib/center-details";
import { COMPARE_MAX, COMPARE_MIN, compareHref } from "@/lib/center-compare";
import { formatRating } from "@/lib/rating";
import { cn } from "@/lib/utils";
import { Button, buttonVariants } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Checkbox } from "@/components/ui/checkbox";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetHeader,
  SheetTitle,
  SheetTrigger,
} from "@/components/ui/sheet";
import { SafeImage } from "@/components/shared/safe-image";
import {
  BooleanFilterRow,
  CheckboxChoiceFilter,
  CollapsibleFilter,
  FilterCategory,
  findLabel,
} from "@/components/universities/UniversityFilters";
import { feeHeadline } from "@/lib/center-fee";
import { centerServiceLabel } from "@/lib/center-services";
import { TrustIcon } from "./CenterBadges";

interface PublicCenterListViewProps {
  initialCenters: StudyCenter[];
}

type SortKey = "default" | "rating_desc" | "name_asc";

const TYPE_OPTIONS = [
  { value: "study_abroad", label: "Tư vấn Du học" },
  { value: "test_prep", label: "Luyện thi Chứng chỉ" },
  { value: "hybrid", label: "Toàn diện (Du học & Luyện thi)" },
];

const STATUS_OPTIONS = [
  { value: "verified", label: "Đã xác minh" },
  { value: "unverified", label: "Chờ xác minh" },
];

const SERVICE_OPTIONS = [
  "CHON_TRUONG",
  "SAN_HOC_BONG",
  "SUA_BAI_LUAN",
  "LUYEN_PHONG_VAN",
  "LUYEN_THI",
];

const SORT_OPTIONS: { value: SortKey; label: string }[] = [
  { value: "default", label: "Mặc định hệ thống" },
  { value: "rating_desc", label: "Đánh giá cao nhất" },
  { value: "name_asc", label: "Tên trung tâm (A–Z)" },
];

function centerTypeLabel(centerType?: string | null) {
  if (centerType === "hybrid") return "Toàn diện";
  if (centerType === "english_training" || centerType === "test_prep") return "Luyện thi Chứng chỉ";
  return "Tư vấn Du học";
}

function StatItem({ label, value }: { label: string; value: React.ReactNode }) {
  return (
    <div className="min-w-0">
      <dt className="text-xs text-muted-foreground">{label}</dt>
      <dd className="truncate text-sm font-semibold text-foreground tabular-nums">{value}</dd>
    </div>
  );
}

interface FilterPanelProps {
  search: string;
  cityFilter: string | undefined;
  typeFilter: string | undefined;
  statusFilter: string | undefined;
  selectedServices: string[];
  licensedOnly: boolean;
  isAdmin: boolean;
  hasActiveFilter: boolean;
  hideTitle?: boolean;
  onSearchChange: (val: string) => void;
  onCityChange: (val: string | undefined) => void;
  onTypeChange: (val: string | undefined) => void;
  onStatusChange: (val: string | undefined) => void;
  onToggleService: (svc: string) => void;
  onClearServices: () => void;
  onLicensedOnlyChange: (checked: boolean) => void;
  onReset: () => void;
}

function CenterFiltersPanel({
  search,
  cityFilter,
  typeFilter,
  statusFilter,
  selectedServices,
  licensedOnly,
  isAdmin,
  hasActiveFilter,
  hideTitle = false,
  onSearchChange,
  onCityChange,
  onTypeChange,
  onStatusChange,
  onToggleService,
  onClearServices,
  onLicensedOnlyChange,
  onReset,
}: FilterPanelProps) {
  const provinces = useProvinces();
  const cityOptions = useMemo(
    () => provinces.map((name) => ({ value: name, label: name })),
    [provinces],
  );
  const serviceBaseId = useId();

  const renderChoice = (
    title: string,
    choiceOptions: { value: string; label: string }[],
    value: string | undefined,
    onSelect: (next: string | undefined) => void,
    defaultOpen = false,
  ) => (
    <CollapsibleFilter
      title={title}
      activeSummary={findLabel(choiceOptions, value)}
      onClear={() => onSelect(undefined)}
      defaultOpen={defaultOpen}
    >
      {(collapse) => (
        <CheckboxChoiceFilter
          label={title}
          options={choiceOptions}
          value={value}
          onValueChange={(next) => {
            onSelect(next);
            if (next !== undefined) collapse();
          }}
        />
      )}
    </CollapsibleFilter>
  );

  const serviceSummary =
    selectedServices.length > 0 ? selectedServices.map(centerServiceLabel).join(", ") : null;

  return (
    <div className="min-w-0 space-y-4 overflow-x-hidden">
      {!hideTitle && (
        <div className="flex items-center justify-between gap-2">
          <h2 className="font-heading text-2xl font-bold text-foreground">Bộ lọc</h2>
        </div>
      )}

      <FilterCategory>
        <div className="relative pb-2">
          <SearchIcon className="pointer-events-none absolute left-3 top-[18px] size-4 -translate-y-1/2 text-muted-foreground" />
          <Input
            value={search}
            onChange={(e) => onSearchChange(e.target.value)}
            aria-label="Tìm trung tâm theo tên, mã hoặc địa chỉ"
            placeholder="Tên trung tâm, mã, địa chỉ..."
            className="h-9 border-ink-3/45 bg-surface pl-9 text-sm"
          />
        </div>
        {renderChoice("Địa điểm", cityOptions, cityFilter, onCityChange, true)}
        {renderChoice("Mô hình hoạt động", TYPE_OPTIONS, typeFilter, onTypeChange, true)}
      </FilterCategory>

      <FilterCategory className="border-t border-line pt-4">
        <CollapsibleFilter
          title="Dịch vụ cung cấp"
          activeSummary={serviceSummary}
          onClear={onClearServices}
          defaultOpen
        >
          <div role="group" aria-label="Dịch vụ cung cấp" className="flex flex-col gap-2">
            {SERVICE_OPTIONS.map((svc) => {
              const id = `${serviceBaseId}-${svc}`;
              return (
                <div key={svc} className="flex min-w-0 items-start gap-2.5">
                  <Checkbox
                    id={id}
                    checked={selectedServices.includes(svc)}
                    className="mt-0.5 border-ink-3/65 bg-surface"
                    onCheckedChange={() => onToggleService(svc)}
                  />
                  <Label
                    htmlFor={id}
                    className="min-w-0 cursor-pointer text-sm leading-snug font-normal text-foreground"
                  >
                    {centerServiceLabel(svc)}
                  </Label>
                </div>
              );
            })}
          </div>
        </CollapsibleFilter>
      </FilterCategory>

      <FilterCategory className="border-t border-line pt-4">
        <BooleanFilterRow
          label="Có Giấy phép Sở GD&ĐT"
          checked={licensedOnly}
          onCheckedChange={onLicensedOnlyChange}
        />
        {isAdmin && renderChoice("Trạng thái xác minh", STATUS_OPTIONS, statusFilter, onStatusChange)}
      </FilterCategory>

      <div className="border-t border-line pt-4">
        <Button
          type="button"
          variant="outline"
          disabled={!hasActiveFilter}
          onClick={onReset}
          className="w-full border-ink-3/45 bg-surface text-foreground hover:bg-muted"
        >
          <RotateCcwIcon className="size-4" />
          Đặt lại bộ lọc
        </Button>
      </div>
    </div>
  );
}

function CenterCard({
  center,
  isAdmin,
  comparing,
  compareFull,
  onToggleCompare,
}: {
  center: StudyCenter;
  isAdmin: boolean;
  comparing: boolean;
  compareFull: boolean;
  onToggleCompare: () => void;
}) {
  const profile = getEnrichedCenterProfile(center);
  const isVerified = center.verificationStatus === "verified";
  const detailHref = `/centers/${center.id}`;
  const fee = feeHeadline(center.feeMin, center.feeMax);
  // Tỷ lệ phản hồi chỉ tin được khi có đủ mẫu (cùng ngưỡng với trang chi tiết)
  const responseRate = (center.responseSampleCount ?? 0) >= 5 ? center.responseRatePercent ?? null : null;
  const subtitle = [center.city, centerTypeLabel(center.centerType)].filter(Boolean).join(" · ");
  // Hai dấu cạnh tên: đã xác minh và giấy tờ pháp lý (kèm số giấy phép/MST), chi tiết hiện khi rê chuột
  const licenseOrTax = center.licenseNumber || center.taxCode;
  const paperLines = [
    profile.legalComplianceMode === "licensed" ? "Cấp phép Sở GD&ĐT" : "Thông báo hoạt động Sở GD",
    licenseOrTax ? `Giấy phép / MST: ${licenseOrTax}` : null,
  ].filter((line): line is string => Boolean(line));

  return (
    <div className="relative h-full">
      {/* Ô so sánh nằm ngoài <Link> để không lồng điều khiển trong liên kết, giống nút lưu ở thẻ trường */}
      <Button
        type="button"
        size="sm"
        variant={comparing ? "default" : "outline"}
        aria-pressed={comparing}
        disabled={!comparing && compareFull}
        onClick={onToggleCompare}
        title={!comparing && compareFull ? `Chỉ so sánh tối đa ${COMPARE_MAX} trung tâm` : undefined}
        className={cn(
          "absolute top-3 right-3 z-10 gap-1.5 shadow-xs",
          comparing ? "bg-destructive text-white hover:bg-destructive/85" : "bg-white/95",
        )}
      >
        {comparing ? <CheckIcon className="size-4" /> : <PlusIcon className="size-4" />}
        {comparing ? "Đã thêm so sánh" : "So sánh"}
      </Button>
      <Link
        href={detailHref}
        className="group block h-full rounded-xl outline-none focus-visible:ring-3 focus-visible:ring-ring/50"
      >
        <Card className="h-full gap-0 py-0 transition-[box-shadow,ring-color] duration-150 group-hover:ring-brand/45 group-hover:shadow-sm">
          <div className="relative aspect-[4/3] w-full overflow-hidden bg-brand-wash">
            <SafeImage
              src={profile.coverImage}
              alt={center.name}
              fallback={
                <div className="flex h-full w-full items-center justify-center">
                  <Building2Icon className="size-10 text-brand/30" />
                </div>
              }
            />
          </div>

          <CardContent className="flex flex-1 flex-col gap-3 py-4">
            <div className="flex items-start gap-3">
              <div className="relative flex size-16 shrink-0 items-center justify-center overflow-hidden rounded-xl border border-line bg-white">
                <SafeImage
                  src={profile.logoImage}
                  alt={`Logo ${center.name}`}
                  fallback={<Building2Icon className="size-5 text-brand" />}
                />
              </div>
              <div className="min-w-0 flex-1">
                <div className="flex items-start justify-between gap-2">
                  <span
                    className="line-clamp-2 font-heading text-lg font-semibold leading-snug text-foreground group-hover:underline"
                    title={center.name}
                  >
                    {center.name}
                  </span>
                  <span className="mt-0.5 flex shrink-0 items-center gap-1">
                    {(isVerified || isAdmin) && (
                      <TrustIcon label={isVerified ? "Đã xác minh" : "Chờ xác minh"}>
                        {isVerified ? (
                          <BadgeCheckIcon className="size-4 text-ok" aria-hidden />
                        ) : (
                          <ClockIcon className="size-4 text-warn" aria-hidden />
                        )}
                      </TrustIcon>
                    )}
                    <TrustIcon label={paperLines.join(". ")} lines={paperLines}>
                      {profile.legalComplianceMode === "licensed" ? (
                        <FileCheckIcon className="size-4 text-ok" aria-hidden />
                      ) : (
                        <FileTextIcon className="size-4 text-muted-foreground" aria-hidden />
                      )}
                    </TrustIcon>
                  </span>
                </div>
                {subtitle && <p className="truncate text-xs text-muted-foreground">{subtitle}</p>}
              </div>
            </div>

            <dl className="grid grid-cols-2 gap-x-4 gap-y-3">
              <StatItem label="Phí tham khảo" value={fee ? `${fee} ₫` : "-"} />
              <StatItem
                label="Đánh giá"
                value={
                  (center.reviewCount ?? 0) > 0 ? (
                    <span className="inline-flex items-center gap-1">
                      <StarIcon className="size-3.5 fill-current text-gold" />
                      <span>{formatRating(center.averageRating)}</span>
                      <span className="text-xs font-normal text-muted-foreground">({center.reviewCount})</span>
                    </span>
                  ) : (
                    "-"
                  )
                }
              />
              <StatItem label="Phản hồi 90 ngày" value={responseRate != null ? `${responseRate}%` : "-"} />
              <StatItem
                label="Hoạt động"
                value={center.establishedYear ? `${new Date().getFullYear() - center.establishedYear} năm` : "-"}
              />
            </dl>
          </CardContent>
        </Card>
      </Link>
    </div>
  );
}

export function PublicCenterListView({ initialCenters }: PublicCenterListViewProps) {
  const { user } = useAuth();
  const isAdmin = user?.role === "admin" || user?.role === "moderator";

  const [search, setSearch] = useState("");
  const [cityFilter, setCityFilter] = useState<string | undefined>(undefined);
  const [typeFilter, setTypeFilter] = useState<string | undefined>(undefined);
  const [statusFilter, setStatusFilter] = useState<string | undefined>(undefined);
  const [selectedServices, setSelectedServices] = useState<string[]>([]);
  const [licensedOnly, setLicensedOnly] = useState(false);
  const [sortBy, setSortBy] = useState<SortKey>("default");
  const [page, setPage] = useState(1);
  const [mobileFilterOpen, setMobileFilterOpen] = useState(false);
  const [desktopFilterOpen, setDesktopFilterOpen] = useState(true);
  // Trung tâm đang chọn để so sánh (theo id, giữ nguyên khi đổi trang hoặc bộ lọc)
  const [compareIds, setCompareIds] = useState<string[]>([]);
  const toggleCompare = (id: string) =>
    setCompareIds((prev) =>
      prev.includes(id) ? prev.filter((x) => x !== id) : prev.length >= COMPARE_MAX ? prev : [...prev, id],
    );
  const comparedCenters = compareIds
    .map((id) => initialCenters.find((c) => c.id === id))
    .filter((c): c is StudyCenter => !!c);
  const pageSize = 12;

  // Chuyển trang / đổi bộ lọc: đưa cột kết quả về đầu (cột này tự cuộn ở >= lg)
  const resultsRef = useRef<HTMLDivElement>(null);
  useEffect(() => {
    resultsRef.current?.scrollTo({ top: 0 });
  }, [search, cityFilter, typeFilter, statusFilter, selectedServices, licensedOnly, sortBy, page]);

  const filteredAndSorted = useMemo(() => {
    const filtered = initialCenters.filter((c) => {
      if (!isAdmin && c.verificationStatus !== "verified") {
        return false;
      }

      if (search.trim()) {
        const q = search.toLowerCase().trim();
        const matchesName = c.name.toLowerCase().includes(q);
        const matchesCode = c.code.toLowerCase().includes(q);
        const matchesAddress = (c.address ?? "").toLowerCase().includes(q);
        const matchesCity = (c.city ?? "").toLowerCase().includes(q);
        if (!matchesName && !matchesCode && !matchesAddress && !matchesCity) {
          return false;
        }
      }

      if (cityFilter) {
        // API trả city đã đổi về tên chuẩn nên so bằng là đủ
        if (c.city !== cityFilter) {
          return false;
        }
      }

      if (typeFilter) {
        const rawType = c.centerType || "study_abroad";
        const centerType = rawType === "english_training" ? "test_prep" : rawType;
        if (centerType !== typeFilter) {
          return false;
        }
      }

      if (selectedServices.length > 0) {
        const centerServices = c.services ?? [];
        if (!selectedServices.every((svc) => centerServices.includes(svc))) {
          return false;
        }
      }

      if (licensedOnly) {
        const profile = getEnrichedCenterProfile(c);
        if (profile.legalComplianceMode !== "licensed") {
          return false;
        }
      }

      if (isAdmin && statusFilter) {
        const status = c.verificationStatus || "unverified";
        if (status !== statusFilter) {
          return false;
        }
      }

      return true;
    });

    if (sortBy === "default") return filtered;

    return [...filtered].sort((a, b) => {
      if (sortBy === "name_asc") return a.name.localeCompare(b.name, "vi");
      // Sắp xếp theo số liệu thật; trung tâm chưa có số liệu xếp cuối
      if (sortBy === "rating_desc") {
        const ra = (a.reviewCount ?? 0) > 0 ? Number(a.averageRating) : -1;
        const rb = (b.reviewCount ?? 0) > 0 ? Number(b.averageRating) : -1;
        return rb - ra;
      }
      return 0;
    });
  }, [initialCenters, search, cityFilter, typeFilter, selectedServices, licensedOnly, statusFilter, isAdmin, sortBy]);

  const totalCount = filteredAndSorted.length;
  const totalPages = Math.ceil(totalCount / pageSize);
  const paginatedCenters = useMemo(() => {
    const start = (page - 1) * pageSize;
    return filteredAndSorted.slice(start, start + pageSize);
  }, [filteredAndSorted, page]);

  const handleResetFilters = () => {
    setSearch("");
    setCityFilter(undefined);
    setTypeFilter(undefined);
    setStatusFilter(undefined);
    setSelectedServices([]);
    setLicensedOnly(false);
    setPage(1);
  };

  const toggleService = (svc: string) => {
    setSelectedServices((prev) =>
      prev.includes(svc) ? prev.filter((item) => item !== svc) : [...prev, svc],
    );
    setPage(1);
  };

  const activeChips: { key: string; label: string; onRemove: () => void }[] = [];
  if (search.trim()) {
    activeChips.push({
      key: "search",
      label: `Từ khóa: "${search.trim()}"`,
      onRemove: () => {
        setSearch("");
        setPage(1);
      },
    });
  }
  if (cityFilter) {
    activeChips.push({
      key: "city",
      label: `Địa điểm: ${cityFilter}`,
      onRemove: () => {
        setCityFilter(undefined);
        setPage(1);
      },
    });
  }
  if (typeFilter) {
    const typeLabel = TYPE_OPTIONS.find((t) => t.value === typeFilter)?.label ?? typeFilter;
    activeChips.push({
      key: "type",
      label: `Mô hình: ${typeLabel}`,
      onRemove: () => {
        setTypeFilter(undefined);
        setPage(1);
      },
    });
  }
  for (const svc of selectedServices) {
    activeChips.push({
      key: `svc-${svc}`,
      label: `Dịch vụ: ${centerServiceLabel(svc)}`,
      onRemove: () => toggleService(svc),
    });
  }
  if (licensedOnly) {
    activeChips.push({
      key: "licensed",
      label: "Có Giấy phép Sở GD&ĐT",
      onRemove: () => {
        setLicensedOnly(false);
        setPage(1);
      },
    });
  }
  if (isAdmin && statusFilter) {
    activeChips.push({
      key: "status",
      label: statusFilter === "verified" ? "Đã xác minh" : "Chờ xác minh",
      onRemove: () => {
        setStatusFilter(undefined);
        setPage(1);
      },
    });
  }

  const filterPanelProps: FilterPanelProps = {
    search,
    cityFilter,
    typeFilter,
    statusFilter,
    selectedServices,
    licensedOnly,
    isAdmin,
    hasActiveFilter: activeChips.length > 0,
    onSearchChange: (v) => {
      setSearch(v);
      setPage(1);
    },
    onCityChange: (v) => {
      setCityFilter(v);
      setPage(1);
    },
    onTypeChange: (v) => {
      setTypeFilter(v);
      setPage(1);
    },
    onStatusChange: (v) => {
      setStatusFilter(v);
      setPage(1);
    },
    onToggleService: toggleService,
    onClearServices: () => {
      setSelectedServices([]);
      setPage(1);
    },
    onLicensedOnlyChange: (checked) => {
      setLicensedOnly(checked);
      setPage(1);
    },
    onReset: handleResetFilters,
  };

  return (
    <div className="space-y-4 lg:flex lg:min-h-0 lg:flex-1 lg:flex-col lg:space-y-0">
      <form
        role="search"
        className="flex gap-2 md:hidden"
        onSubmit={(e) => e.preventDefault()}
      >
        <label htmlFor="center-page-search" className="sr-only">
          Tìm theo tên trung tâm, mã hoặc địa chỉ
        </label>
        <Input
          id="center-page-search"
          type="search"
          placeholder="Tìm theo tên trung tâm, mã hoặc địa chỉ"
          value={search}
          onChange={(e) => {
            setSearch(e.target.value);
            setPage(1);
          }}
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
                  <SheetHeader className="border-b border-line p-4">
                    <SheetTitle className="text-base font-bold">Bộ lọc trung tâm</SheetTitle>
                    <SheetDescription className="text-xs">
                      Lọc theo địa điểm, mô hình, dịch vụ và pháp lý
                    </SheetDescription>
                  </SheetHeader>
                  <div className="flex-1 overflow-y-auto p-4">
                    <CenterFiltersPanel
                      {...filterPanelProps}
                      hideTitle
                      onReset={() => {
                        handleResetFilters();
                        setMobileFilterOpen(false);
                      }}
                    />
                  </div>
                  <div className="sticky bottom-0 border-t border-line bg-background p-4">
                    <Button type="button" className="w-full" onClick={() => setMobileFilterOpen(false)}>
                      Xem {totalCount} trung tâm
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
                aria-controls="center-filters-sidebar"
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

            <span className="text-sm font-medium text-ink">
              <span className="font-bold text-brand tabular-nums">{totalCount}</span> trung tâm phù hợp
            </span>
          </div>

          <div className="flex flex-wrap items-center gap-2.5">
            <span className="whitespace-nowrap text-sm font-medium text-muted-foreground">
              Sắp xếp theo
            </span>
            <Select
              value={sortBy}
              onValueChange={(val) => {
                if (val !== null) {
                  setSortBy(val as SortKey);
                  setPage(1);
                }
              }}
              items={SORT_OPTIONS}
            >
              <SelectTrigger
                aria-label="Sắp xếp theo"
                className="h-9! w-[220px] border-line bg-surface px-3 text-sm font-medium shadow-xs"
              >
                <SelectValue placeholder="Sắp xếp theo..." />
              </SelectTrigger>
              <SelectContent align="end" className="w-max min-w-56">
                {SORT_OPTIONS.map((opt) => (
                  <SelectItem key={opt.value} value={opt.value} className="text-sm">
                    {opt.label}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
        </div>
      </div>

      <div
        className={cn(
          "grid grid-cols-1 items-start gap-8 lg:min-h-0 lg:flex-1 lg:items-stretch",
          desktopFilterOpen && "lg:grid-cols-[272px_minmax(0,1fr)] xl:grid-cols-[300px_minmax(0,1fr)]",
        )}
      >
        <aside
          id="center-filters-sidebar"
          className={cn(
            "hidden lg:h-full lg:overflow-y-auto lg:overflow-x-hidden lg:px-2 lg:pb-6 [scrollbar-width:thin]",
            desktopFilterOpen && "lg:block",
          )}
        >
          <CenterFiltersPanel {...filterPanelProps} hideTitle />
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

          {paginatedCenters.length === 0 ? (
            <div className="space-y-4 py-16 text-center">
              <div className="mx-auto flex size-14 items-center justify-center rounded-full bg-muted text-muted-foreground">
                <Building2Icon className="size-7" />
              </div>
              <div className="space-y-1">
                <h3 className="font-heading text-lg font-bold text-foreground">
                  Không tìm thấy trung tâm nào phù hợp
                </h3>
                <p className="mx-auto max-w-md text-sm text-muted-foreground">
                  Hãy thử bỏ bớt điều kiện lọc về dịch vụ, mô hình hoặc tìm kiếm theo từ khóa khác.
                </p>
              </div>
              <Button onClick={handleResetFilters} variant="outline" className="gap-2">
                <RotateCcwIcon className="size-4" />
                <span>Đặt lại tất cả bộ lọc</span>
              </Button>
            </div>
          ) : (
            <div className="grid grid-cols-[repeat(auto-fill,minmax(min(100%,max(270px,calc((100%_-_4.5rem)/4))),1fr))] gap-6">
              {paginatedCenters.map((center) => (
                <CenterCard
                  key={center.id}
                  center={center}
                  isAdmin={isAdmin}
                  comparing={compareIds.includes(center.id)}
                  compareFull={compareIds.length >= COMPARE_MAX}
                  onToggleCompare={() => toggleCompare(center.id)}
                />
              ))}
            </div>
          )}

          {totalPages > 1 && (
            <div className="flex items-center justify-center gap-4 pt-4 sm:justify-end sm:gap-5">
              <Button
                type="button"
                variant="outline"
                size="lg"
                className="border-line bg-surface px-5 text-foreground shadow-xs hover:bg-muted"
                disabled={page <= 1}
                onClick={() => setPage((p) => p - 1)}
              >
                <ChevronLeftIcon /> Trước
              </Button>
              <span className="text-sm font-medium text-muted-foreground tabular-nums">
                Trang {page} / {Math.max(totalPages, 1)}
              </span>
              <Button
                type="button"
                variant="outline"
                size="lg"
                className="border-line bg-surface px-5 text-foreground shadow-xs hover:bg-muted"
                disabled={page >= totalPages}
                onClick={() => setPage((p) => p + 1)}
              >
                Sau <ChevronRightIcon />
              </Button>
            </div>
          )}

          {comparedCenters.length > 0 && <div className="h-20" aria-hidden />}
        </div>
      </div>

      {comparedCenters.length > 0 && (
        <div className="fixed inset-x-0 bottom-0 z-40 border-t border-line bg-surface/95 shadow-[0_-4px_16px_rgba(0,0,0,0.08)] backdrop-blur">
          <div className="mx-auto flex max-w-6xl flex-wrap items-center gap-3 px-4 py-3">
            <div className="flex min-w-0 flex-1 flex-wrap items-center gap-2">
              <span className="text-sm font-medium text-ink-2">
                Đã chọn {comparedCenters.length}/{COMPARE_MAX}:
              </span>
              {comparedCenters.length >= COMPARE_MAX && (
                <span className="text-xs font-medium text-amber-700 dark:text-amber-300">
                  Đã chọn tối đa {COMPARE_MAX} trung tâm
                </span>
              )}
              {comparedCenters.map((c) => (
                <span
                  key={c.id}
                  className="inline-flex max-w-52 items-center gap-1.5 rounded-lg border border-line bg-ground px-2.5 py-1 text-sm font-medium text-ink"
                >
                  <span className="truncate">{c.name}</span>
                  <Button
                    type="button"
                    variant="ghost"
                    size="icon-xs"
                    onClick={() => toggleCompare(c.id)}
                    className="rounded-full text-ink-3 hover:bg-surface hover:text-ink"
                    aria-label={`Bỏ ${c.name} khỏi so sánh`}
                  >
                    <XIcon className="size-3.5" />
                  </Button>
                </span>
              ))}
            </div>
            <Button variant="ghost" size="sm" onClick={() => setCompareIds([])}>
              Bỏ chọn
            </Button>
            {comparedCenters.length >= COMPARE_MIN ? (
              <Link
                href={compareHref(compareIds)}
                className={cn(buttonVariants({ variant: "default" }))}
              >
                So sánh ({comparedCenters.length})
              </Link>
            ) : (
              <span className="text-sm text-ink-3">Chọn thêm ít nhất 1 trung tâm nữa</span>
            )}
          </div>
        </div>
      )}
    </div>
  );
}
