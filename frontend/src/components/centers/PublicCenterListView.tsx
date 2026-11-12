"use client";

import { useId, useMemo, useState } from "react";
import Link from "next/link";
import {
  Building2Icon,
  ExternalLinkIcon,
  RotateCcwIcon,
  SearchIcon,
  ShieldCheckIcon,
  SlidersHorizontalIcon,
  StarIcon,
  XIcon,
} from "lucide-react";
import type { StudyCenter } from "@/types/api";
import { useAuth } from "@/contexts/AuthContext";
import { getEnrichedCenterProfile } from "@/lib/center-details";
import { centerServiceLabel } from "@/lib/center-services";
import { cn } from "@/lib/utils";
import { buttonVariants, Button } from "@/components/ui/button";
import { Card, CardContent, CardFooter } from "@/components/ui/card";
import { Checkbox } from "@/components/ui/checkbox";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { RadioGroup, RadioGroupItem } from "@/components/ui/radio-group";
import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetHeader,
  SheetTitle,
  SheetTrigger,
} from "@/components/ui/sheet";
import { TablePagination } from "@/components/admin/shared/TablePagination";
import { OptionSelect } from "@/components/admin/universities/OptionSelect";
import {
  CenterLegalModeBadge,
  CenterTypeBadge,
  CenterVerificationBadge,
} from "./CenterBadges";

interface PublicCenterListViewProps {
  initialCenters: StudyCenter[];
}

type SortKey = "default" | "visa_desc" | "rating_desc" | "name_asc";

const CITY_OPTIONS = [
  { value: "all", label: "Tất cả địa điểm" },
  { value: "Hà Nội", label: "Hà Nội" },
  { value: "TP.HCM", label: "TP. Hồ Chí Minh" },
  { value: "Hải Phòng", label: "Hải Phòng" },
  { value: "Đà Nẵng", label: "Đà Nẵng" },
];

const TYPE_OPTIONS = [
  { value: "all", label: "Mọi loại hình" },
  { value: "study_abroad", label: "Tư vấn Du học" },
  { value: "english_training", label: "Luyện thi Chứng chỉ" },
  { value: "hybrid", label: "Toàn diện (Du học & Luyện thi)" },
];

const STATUS_OPTIONS = [
  { value: "all", label: "Tất cả trạng thái" },
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
  { value: "visa_desc", label: "Tỷ lệ đỗ Visa cao nhất" },
  { value: "rating_desc", label: "Đánh giá cao nhất" },
  { value: "name_asc", label: "Tên trung tâm (A–Z)" },
];

function FilterSection({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <fieldset>
      <legend className="mb-1.5 text-sm font-semibold text-foreground">{title}</legend>
      {children}
    </fieldset>
  );
}

function RadioFilter({
  name,
  options,
  value,
  onValueChange,
}: {
  name: string;
  options: { value: string; label: string }[];
  value: string;
  onValueChange: (value: string) => void;
}) {
  const baseId = useId();
  return (
    <RadioGroup
      name={name}
      value={value}
      onValueChange={(next) => onValueChange(String(next))}
      className="gap-2"
    >
      {options.map((option) => {
        const id = `${baseId}-${option.value}`;
        return (
          <div key={option.value} className="flex items-center gap-2.5">
            <RadioGroupItem id={id} value={option.value} />
            <Label htmlFor={id} className="cursor-pointer text-sm font-normal text-ink">
              {option.label}
            </Label>
          </div>
        );
      })}
    </RadioGroup>
  );
}

function StatItem({
  label,
  value,
  valueClassName,
}: {
  label: string;
  value: React.ReactNode;
  valueClassName?: string;
}) {
  return (
    <div className="min-w-0">
      <dt className="text-xs text-ink-3">{label}</dt>
      <dd className={cn("truncate text-sm font-semibold text-ink tabular-nums", valueClassName)}>
        {value}
      </dd>
    </div>
  );
}

interface FilterPanelProps {
  search: string;
  cityFilter: string;
  typeFilter: string;
  statusFilter: string;
  selectedServices: string[];
  licensedOnly: boolean;
  isAdmin: boolean;
  hasActiveFilter: boolean;
  onSearchChange: (val: string) => void;
  onCityChange: (val: string) => void;
  onTypeChange: (val: string) => void;
  onStatusChange: (val: string) => void;
  onToggleService: (svc: string) => void;
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
  onSearchChange,
  onCityChange,
  onTypeChange,
  onStatusChange,
  onToggleService,
  onLicensedOnlyChange,
  onReset,
}: FilterPanelProps) {
  const licensedId = useId();
  const serviceBaseId = useId();

  return (
    <div className="space-y-5">
      <div className="flex h-8 items-center justify-between">
        <h2 className="font-heading text-xl font-bold text-ink">Bộ lọc</h2>
        {hasActiveFilter && (
          <Button variant="ghost" size="sm" onClick={onReset} className="text-ink-2 hover:text-ink">
            <RotateCcwIcon className="size-3.5" />
            Đặt lại
          </Button>
        )}
      </div>

      <FilterSection title="Tìm kiếm">
        <div className="relative">
          <SearchIcon className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-ink-3" />
          <Input
            value={search}
            onChange={(e) => onSearchChange(e.target.value)}
            aria-label="Tìm trung tâm theo tên, mã hoặc địa chỉ"
            placeholder="Tên trung tâm, mã, địa chỉ..."
            className="h-9 bg-surface pl-9"
          />
        </div>
      </FilterSection>

      <FilterSection title="Địa điểm">
        <RadioFilter name="center-city" options={CITY_OPTIONS} value={cityFilter} onValueChange={onCityChange} />
      </FilterSection>

      <FilterSection title="Mô hình hoạt động">
        <RadioFilter name="center-type" options={TYPE_OPTIONS} value={typeFilter} onValueChange={onTypeChange} />
      </FilterSection>

      <FilterSection title="Dịch vụ cung cấp">
        <div className="space-y-2">
          {SERVICE_OPTIONS.map((svc) => {
            const id = `${serviceBaseId}-${svc}`;
            return (
              <div key={svc} className="flex items-center gap-2.5">
                <Checkbox
                  id={id}
                  checked={selectedServices.includes(svc)}
                  onCheckedChange={() => onToggleService(svc)}
                />
                <Label htmlFor={id} className="cursor-pointer text-sm font-normal text-ink">
                  {centerServiceLabel(svc)}
                </Label>
              </div>
            );
          })}
        </div>
      </FilterSection>

      <FilterSection title="Pháp lý minh bạch">
        <div className="flex items-center gap-2.5">
          <Checkbox
            id={licensedId}
            checked={licensedOnly}
            onCheckedChange={(checked) => onLicensedOnlyChange(Boolean(checked))}
          />
          <Label htmlFor={licensedId} className="cursor-pointer text-sm font-normal text-ink">
            Có Giấy phép Sở GD&amp;ĐT
          </Label>
        </div>
      </FilterSection>

      {isAdmin && (
        <FilterSection title="Trạng thái xác minh">
          <RadioFilter
            name="center-status"
            options={STATUS_OPTIONS}
            value={statusFilter}
            onValueChange={onStatusChange}
          />
        </FilterSection>
      )}
    </div>
  );
}

function CenterCard({ center, isAdmin }: { center: StudyCenter; isAdmin: boolean }) {
  const profile = getEnrichedCenterProfile(center);
  const isVerified = center.verificationStatus === "verified";
  const detailHref = `/centers/${center.id}`;
  const subtitle = [center.city, center.code].filter(Boolean).join(" · ");

  return (
    <Card className="group gap-0 py-0">
      <div className="relative aspect-[4/3] w-full overflow-hidden bg-brand-ink">
        {profile.coverImage ? (
          <img
            src={profile.coverImage}
            alt={center.name}
            className="h-full w-full object-cover transition-transform duration-300 group-hover:scale-105"
          />
        ) : (
          <div className="flex h-full w-full items-center justify-center bg-brand-wash">
            <Building2Icon className="size-10 text-brand/30" />
          </div>
        )}

        <div className="absolute inset-0 bg-gradient-to-t from-black/85 via-black/25 to-transparent" />

        <div className="absolute bottom-3 left-3 right-3 flex items-end gap-3">
          <div className="relative flex size-11 shrink-0 items-center justify-center overflow-hidden rounded-lg bg-white">
            {profile.logoImage ? (
              <img src={profile.logoImage} alt={`Logo ${center.name}`} className="h-full w-full object-contain p-1" />
            ) : (
              <Building2Icon className="size-5 text-brand" />
            )}
          </div>
          <div className="min-w-0">
            <Link
              href={detailHref}
              className="line-clamp-2 font-heading text-lg font-bold leading-snug text-white hover:underline"
              title={center.name}
            >
              {center.name}
            </Link>
            {subtitle && <p className="truncate text-xs text-white/85">{subtitle}</p>}
          </div>
        </div>
      </div>

      <CardContent className="flex flex-1 flex-col gap-3 py-4">
        <div className="flex flex-wrap gap-1.5">
          <CenterTypeBadge centerType={center.centerType} compact />
          {isAdmin && <CenterVerificationBadge verified={isVerified} />}
          {!isAdmin && isVerified && <CenterVerificationBadge verified label="Đã xác minh" />}
          <CenterLegalModeBadge mode={profile.legalComplianceMode} />
        </div>

        <dl className="grid grid-cols-2 gap-x-4 gap-y-2">
          <StatItem
            label="Tỷ lệ đỗ Visa"
            value={`${profile.visaSuccessRate}%`}
            valueClassName="text-ok"
          />
          <StatItem
            label="Đánh giá"
            value={
              <span className="inline-flex items-center gap-1">
                <StarIcon className="size-3.5 fill-current text-gold" />
                <span>{profile.rating.toFixed(1)}</span>
                <span className="text-xs font-normal text-ink-3">({profile.reviewCount})</span>
              </span>
            }
          />
          <StatItem label="Nhân sự" value={`${profile.staffCount} chuyên viên`} />
          <StatItem
            label="Giấy phép / MST"
            value={center.licenseNumber || center.taxCode || `Từ ${profile.establishedYear}`}
          />
        </dl>
      </CardContent>

      <CardFooter className="bg-transparent py-3">
        <Link href={detailHref} className={cn(buttonVariants({ variant: "default" }), "w-full")}>
          Xem chi tiết
        </Link>
      </CardFooter>
    </Card>
  );
}

export function PublicCenterListView({ initialCenters }: PublicCenterListViewProps) {
  const { user } = useAuth();
  const isAdmin = user?.role === "admin" || user?.role === "moderator";

  const [search, setSearch] = useState("");
  const [cityFilter, setCityFilter] = useState("all");
  const [typeFilter, setTypeFilter] = useState("all");
  const [statusFilter, setStatusFilter] = useState("all");
  const [selectedServices, setSelectedServices] = useState<string[]>([]);
  const [licensedOnly, setLicensedOnly] = useState(false);
  const [sortBy, setSortBy] = useState<SortKey>("default");
  const [page, setPage] = useState(1);
  const [mobileFilterOpen, setMobileFilterOpen] = useState(false);
  const pageSize = 9;

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

      if (cityFilter !== "all") {
        const targetCity = cityFilter === "TP.HCM" ? "hồ chí minh" : cityFilter.toLowerCase();
        if (!c.city?.toLowerCase().includes(targetCity)) {
          return false;
        }
      }

      if (typeFilter !== "all") {
        const centerType = c.centerType || "study_abroad";
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

      if (isAdmin && statusFilter !== "all") {
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
      const pa = getEnrichedCenterProfile(a);
      const pb = getEnrichedCenterProfile(b);
      if (sortBy === "visa_desc") return pb.visaSuccessRate - pa.visaSuccessRate;
      if (sortBy === "rating_desc") return pb.rating - pa.rating;
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
    setCityFilter("all");
    setTypeFilter("all");
    setStatusFilter("all");
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
  if (cityFilter !== "all") {
    const cityLabel = CITY_OPTIONS.find((c) => c.value === cityFilter)?.label ?? cityFilter;
    activeChips.push({
      key: "city",
      label: `Địa điểm: ${cityLabel}`,
      onRemove: () => {
        setCityFilter("all");
        setPage(1);
      },
    });
  }
  if (typeFilter !== "all") {
    const typeLabel = TYPE_OPTIONS.find((t) => t.value === typeFilter)?.label ?? typeFilter;
    activeChips.push({
      key: "type",
      label: `Mô hình: ${typeLabel}`,
      onRemove: () => {
        setTypeFilter("all");
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
  if (isAdmin && statusFilter !== "all") {
    activeChips.push({
      key: "status",
      label: statusFilter === "verified" ? "Đã xác minh" : "Chờ xác minh",
      onRemove: () => {
        setStatusFilter("all");
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
    onLicensedOnlyChange: (checked) => {
      setLicensedOnly(checked);
      setPage(1);
    },
    onReset: handleResetFilters,
  };

  return (
    <div className="space-y-6">
      {isAdmin && (
        <div className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-brand/20 bg-brand-wash/70 p-3.5 text-sm">
          <div className="flex items-center gap-2 text-brand-ink">
            <ShieldCheckIcon className="size-4 shrink-0 text-brand" />
            <span>
              Bạn đang đăng nhập với quyền{" "}
              <strong>{user?.role === "admin" ? "Quản trị viên" : "Moderator"}</strong>. Vào khu vực kiểm duyệt để thêm mới hoặc duyệt hồ sơ cơ sở.
            </span>
          </div>
          <Link href="/admin/centers" className={cn(buttonVariants({ size: "sm" }), "gap-1.5 text-xs")}>
            <span>Vào Quản lý trung tâm</span>
            <ExternalLinkIcon className="size-3.5" />
          </Link>
        </div>
      )}

      <div className="space-y-1 border-b border-line pb-5">
        <h1 className="text-h1 text-ink">Danh bạ Trung tâm Tư vấn Du học &amp; Đào tạo</h1>
        <p className="text-body-s text-ink-2">
          Tìm hiểu và so sánh các trung tâm tư vấn du học Hoa Kỳ, luyện thi chứng chỉ uy tín và thông tin pháp lý được xác minh trên USAS.
        </p>
      </div>

      <div className="grid grid-cols-1 items-start gap-8 lg:grid-cols-4">
        <aside className="hidden lg:sticky lg:top-[calc(var(--header-h)+1.5rem)] lg:col-span-1 lg:block">
          <CenterFiltersPanel {...filterPanelProps} />
        </aside>

        <div className="space-y-5 lg:col-span-3">
          <div className="flex flex-wrap items-center justify-between gap-4">
            <div className="flex items-center gap-3">
              <div className="lg:hidden">
                <Sheet open={mobileFilterOpen} onOpenChange={setMobileFilterOpen}>
                  <SheetTrigger
                    className={cn(buttonVariants({ variant: "outline", size: "sm" }), "cursor-pointer gap-2")}
                  >
                    <SlidersHorizontalIcon className="size-4 text-brand" />
                    <span>Bộ lọc ({activeChips.length})</span>
                  </SheetTrigger>
                  <SheetContent side="left" className="w-[320px] overflow-y-auto p-0 sm:w-[380px]">
                    <SheetHeader className="border-b border-line p-4">
                      <SheetTitle className="font-heading text-base font-bold">Bộ lọc trung tâm</SheetTitle>
                      <SheetDescription className="text-xs">
                        Lọc theo địa điểm, mô hình, dịch vụ và pháp lý
                      </SheetDescription>
                    </SheetHeader>
                    <div className="p-4">
                      <CenterFiltersPanel {...filterPanelProps} />
                    </div>
                  </SheetContent>
                </Sheet>
              </div>

              <div className="text-sm font-medium text-ink">
                <span className="font-bold text-brand tabular-nums">{totalCount}</span> trung tâm phù hợp
              </div>
            </div>

            <div className="flex items-center gap-2 text-sm text-ink-2">
              <Label htmlFor="public-centers-sort" className="shrink-0 font-normal text-ink-2">
                Sắp xếp:
              </Label>
              <OptionSelect
                id="public-centers-sort"
                value={sortBy}
                options={SORT_OPTIONS}
                onChange={(val) => {
                  setSortBy(val as SortKey);
                  setPage(1);
                }}
                size="sm"
                className="w-[12.5rem] bg-surface font-medium text-ink"
              />
            </div>
          </div>

          {activeChips.length > 0 && (
            <div className="flex flex-wrap items-center gap-2 pt-1">
              <span className="text-sm font-medium text-ink-2">Đang lọc:</span>
              {activeChips.map((chip) => (
                <span
                  key={chip.key}
                  className="inline-flex items-center gap-2 rounded-lg border border-line bg-surface px-3 py-1.5 text-sm font-medium text-ink transition-colors hover:border-brand/40"
                >
                  <span>{chip.label}</span>
                  <button
                    type="button"
                    onClick={chip.onRemove}
                    className="rounded-full p-0.5 text-ink-3 hover:bg-ground hover:text-ink"
                    aria-label={`Xóa bộ lọc ${chip.label}`}
                  >
                    <XIcon className="size-3.5" />
                  </button>
                </span>
              ))}
              <Button variant="ghost" size="sm" onClick={handleResetFilters} className="h-8 px-2.5 text-sm text-ink-2">
                Xóa tất cả
              </Button>
            </div>
          )}

          {paginatedCenters.length === 0 ? (
            <div className="space-y-4 rounded-xl border border-line bg-surface py-16 text-center">
              <div className="mx-auto flex size-14 items-center justify-center rounded-full bg-brand-wash text-brand">
                <Building2Icon className="size-7" />
              </div>
              <div className="space-y-1 px-4">
                <h3 className="font-heading text-lg font-bold text-ink">
                  Không tìm thấy trung tâm nào phù hợp
                </h3>
                <p className="mx-auto max-w-md text-sm text-ink-2">
                  Hãy thử bỏ bớt điều kiện lọc về dịch vụ, mô hình hoặc tìm kiếm theo từ khóa khác.
                </p>
              </div>
              <Button onClick={handleResetFilters} variant="outline" className="gap-2">
                <RotateCcwIcon className="size-4" />
                <span>Đặt lại tất cả bộ lọc</span>
              </Button>
            </div>
          ) : (
            <div className="grid grid-cols-1 gap-6 md:grid-cols-2 xl:grid-cols-3">
              {paginatedCenters.map((center) => (
                <CenterCard key={center.id} center={center} isAdmin={isAdmin} />
              ))}
            </div>
          )}

          {totalPages > 1 && (
            <div className="border-t border-line pt-6">
              <TablePagination
                page={page}
                totalPages={totalPages}
                totalCount={totalCount}
                itemLabel="trung tâm"
                onPageChange={(nextPage) => setPage(nextPage)}
              />
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
