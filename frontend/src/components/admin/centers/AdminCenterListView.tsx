"use client";

import { formatRating } from "@/lib/rating";
import React, { useState, useEffect, useCallback, useMemo } from "react";
import {
  Building2Icon,
  EyeIcon,
  EyeOffIcon,
  FilterIcon,
  MapPinIcon,
  PencilIcon,
  RotateCcwIcon,
  SearchIcon,
  ShieldCheckIcon,
  StarIcon,
  TrendingUpIcon,
} from "lucide-react";
import { studyCentersApi, ApiError } from "@/lib/api";
import type { StudyCenter, PagedResult } from "@/types/api";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Popover, PopoverContent, PopoverTrigger } from "@/components/ui/popover";
import { toast } from "@/components/ui/toast";
import {
  DataTable,
  type DataTableColumn,
  type DataTableToolbarSlots,
} from "@/components/admin/shared/DataTable";
import { TablePagination } from "@/components/admin/shared/TablePagination";
import { OptionSelect } from "@/components/admin/universities/OptionSelect";
import { normalizeComplianceMode } from "@/lib/center-legal";
import { AdminVerificationQueueModal } from "@/components/admin/centers/AdminVerificationQueueModal";
import { AdminCenterDetailPanel } from "@/components/admin/centers/AdminCenterDetailPanel";
import {
  CenterActionConfirmDialog,
  type CenterConfirmAction,
} from "@/components/admin/centers/CenterActionConfirmDialog";
import { usePendingCenterCount } from "@/hooks/use-pending-center-count";
import { EditCenterButton } from "@/components/admin/centers/EditCenterButton";
import {
  CenterLegalModeBadge,
  CenterTypeBadge,
  CenterStatusBadge,
  CenterVisibilityBadge,
} from "@/components/centers/CenterBadges";
import { cn } from "cn";

const PAGE_SIZE = 10;
const FILTER_FIELD_BG = "bg-surface";

type FilterOption = { value: string; label: string };

const CITY_OPTIONS: FilterOption[] = [
  { value: "all", label: "Tất cả địa điểm" },
  { value: "Hà Nội", label: "Hà Nội" },
  { value: "TP.HCM", label: "TP. Hồ Chí Minh" },
  { value: "Hải Phòng", label: "Hải Phòng" },
];

const TYPE_OPTIONS: FilterOption[] = [
  { value: "all", label: "Mọi loại hình" },
  { value: "study_abroad", label: "Tư vấn Du học" },
  { value: "test_prep", label: "Luyện thi Chứng chỉ" },
  { value: "hybrid", label: "Toàn diện (Du học & Luyện thi)" },
];

const STATUS_OPTIONS: FilterOption[] = [
  { value: "all", label: "Mọi trạng thái" },
  { value: "verified", label: "Đã xác minh" },
  { value: "pending_review", label: "Chờ duyệt hồ sơ" },
  { value: "unverified", label: "Chưa xác minh" },
  { value: "rejected", label: "Bị từ chối" },
  { value: "suspended", label: "Tạm ngưng" },
  { value: "hidden", label: "Đã ẩn khỏi gợi ý" },
];

export function AdminCenterListView() {
  const [centers, setCenters] = useState<StudyCenter[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [totalPages, setTotalPages] = useState(1);
  const [page, setPage] = useState(1);

  const [searchInput, setSearchInput] = useState("");
  const [search, setSearch] = useState("");
  const [cityFilter, setCityFilter] = useState("all");
  const [typeFilter, setTypeFilter] = useState("all");
  const [statusFilter, setStatusFilter] = useState("all");

  const [filterMenuOpen, setFilterMenuOpen] = useState(false);
  const [draftCity, setDraftCity] = useState("all");
  const [draftType, setDraftType] = useState("all");
  const [draftStatus, setDraftStatus] = useState("all");

  const [isLoading, setIsLoading] = useState(true);
  const [listError, setListError] = useState<string | null>(null);
  const [selectedCenter, setSelectedCenter] = useState<StudyCenter | null>(null);
  const [confirmTarget, setConfirmTarget] = useState<{
    center: StudyCenter;
    action: CenterConfirmAction;
  } | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [failedLogos, setFailedLogos] = useState<Record<string, true>>({});
  const [queueOpen, setQueueOpen] = useState(false);
  const { count: pendingCount, refresh: refreshPending } = usePendingCenterCount(true);

  const markLogoFailed = (url?: string | null) => {
    if (!url) return;
    setFailedLogos((prev) => (prev[url] ? prev : { ...prev, [url]: true }));
  };

  const fetchCenters = useCallback(
    async (activePage: number, signal?: { cancelled: boolean }) => {
      setIsLoading(true);
      setListError(null);
      try {
        const res: PagedResult<StudyCenter> = await studyCentersApi.search({
          search: search.trim() || undefined,
          city: cityFilter !== "all" ? cityFilter : undefined,
          centerType: typeFilter !== "all" ? typeFilter : undefined,
          verificationStatus:
            statusFilter !== "all" && statusFilter !== "hidden" ? statusFilter : undefined,
          includeInactive: statusFilter !== "hidden",
          onlyInactive: statusFilter === "hidden",
          page: activePage,
          pageSize: PAGE_SIZE,
        });

        if (signal?.cancelled) return;

        const items = res.items || [];
        const total = res.totalCount ?? items.length;

        setCenters(items);
        setTotalCount(total);
        setTotalPages(Math.max(1, res.totalPages ?? Math.ceil(total / PAGE_SIZE)));
      } catch (err) {
        if (!signal?.cancelled) {
          setListError(
            err instanceof ApiError ? err.message : "Không tải được danh sách trung tâm tư vấn."
          );
        }
      } finally {
        if (!signal?.cancelled) {
          setIsLoading(false);
        }
      }
    },
    [cityFilter, search, statusFilter, typeFilter]
  );

  useEffect(() => {
    const signal = { cancelled: false };
    void (async () => {
      await fetchCenters(page, signal);
    })();
    return () => {
      signal.cancelled = true;
    };
  }, [fetchCenters, page]);

  const activeFilterCount =
    (cityFilter !== "all" ? 1 : 0) +
    (typeFilter !== "all" ? 1 : 0) +
    (statusFilter !== "all" ? 1 : 0);

  const hasActiveSearchOrFilter = activeFilterCount > 0 || search.length > 0;

  const handleFilterMenuOpenChange = (open: boolean) => {
    if (open) {
      setDraftCity(cityFilter);
      setDraftType(typeFilter);
      setDraftStatus(statusFilter);
    }
    setFilterMenuOpen(open);
  };

  const handleApplyFilters = (e: React.FormEvent) => {
    e.preventDefault();
    setCityFilter(draftCity);
    setTypeFilter(draftType);
    setStatusFilter(draftStatus);
    setPage(1);
    setFilterMenuOpen(false);
  };

  const handleResetFilters = () => {
    setSearchInput("");
    setSearch("");
    setCityFilter("all");
    setTypeFilter("all");
    setStatusFilter("all");
    setDraftCity("all");
    setDraftType("all");
    setDraftStatus("all");
    setPage(1);
    setFilterMenuOpen(false);
  };

  const handleSearchSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    setSearch(searchInput.trim());
    setPage(1);
  };

  const updateCenterInState = useCallback((updated: StudyCenter) => {
    setCenters((prev) => prev.map((c) => (c.id === updated.id ? { ...c, ...updated } : c)));
    setSelectedCenter((prev) => (prev?.id === updated.id ? { ...prev, ...updated } : prev));
  }, []);

  // Xác minh chỉ thực hiện trong hàng đợi hồ sơ (UC-59); ở đây chỉ còn ẩn/hiện (UC-72).
  const handleConfirmTarget = async () => {
    if (!confirmTarget) return;
    setIsSubmitting(true);
    const targetCenter = confirmTarget.center;
    const action = confirmTarget.action;

    try {
      if (action === "hide" || action === "show") {
        const targetActive = action === "show";
        const updated = await studyCentersApi.setActiveStatus(targetCenter.id, targetActive);
        updateCenterInState({
          ...targetCenter,
          ...updated,
          isActive: targetActive,
        });

        toast.add({
          type: "success",
          title: targetActive ? "Đã hiện trung tâm" : "Đã ẩn trung tâm",
          description: targetActive
            ? `Trung tâm ${targetCenter.name} đã được hiển thị lại trên danh bạ và luồng gợi ý.`
            : `Trung tâm ${targetCenter.name} đã được ẩn khỏi danh bạ và luồng gợi ý.`,
        });
      }

      setConfirmTarget(null);
    } catch (err: unknown) {
      const msg = err instanceof ApiError ? err.message : "Thao tác không thành công.";
      toast.add({
        type: "error",
        title: "Lỗi thao tác",
        description: msg,
      });
    } finally {
      setIsSubmitting(false);
    }
  };

  const columns = useMemo<DataTableColumn<StudyCenter>[]>(
    () => [
      {
        id: "name",
        label: "Trung tâm & Mã",
        required: true,
        render: (c) => (
          <div className="flex items-center gap-3 min-w-[220px]">
            <div className="size-9 rounded-lg overflow-hidden border border-line bg-ground shrink-0 flex items-center justify-center">
              {c.logoUrl && !failedLogos[c.logoUrl] ? (
                <img
                  src={c.logoUrl}
                  alt={c.name}
                  onError={() => markLogoFailed(c.logoUrl)}
                  className="size-full object-cover"
                />
              ) : (
                <Building2Icon className="size-4 text-ink-3" />
              )}
            </div>
            <div className="min-w-0">
              <button
                type="button"
                onClick={(e) => {
                  e.stopPropagation();
                  setSelectedCenter((prev) => (prev?.id === c.id ? null : c));
                }}
                className="font-medium text-brand underline-offset-2 hover:underline text-left block"
              >
                {c.name}
              </button>
              <span className="font-mono text-xs text-ink-3">{c.code}</span>
            </div>
          </div>
        ),
      },
      {
        id: "type",
        label: "Mô hình",
        render: (c) => <CenterTypeBadge centerType={c.centerType} compact />,
      },
      {
        id: "location",
        label: "Địa điểm & Pháp lý",
        render: (c) => (
          <div className="space-y-1 text-xs">
            <div className="flex items-center gap-1 font-medium text-ink">
              <MapPinIcon className="size-3.5 text-brand shrink-0" />
              <span>
                {c.district ? `${c.district}, ${c.city ?? ""}` : (c.city ?? "Toàn quốc")}
              </span>
            </div>
            {c.licenseNumber && (
              <div className="text-ink-2 flex items-center gap-1">
                <ShieldCheckIcon className="size-3 text-ok shrink-0" />
                <span>{c.licenseNumber}</span>
              </div>
            )}
            {(c.taxCode || c.legalComplianceMode) && (
              <div className="flex items-center gap-1.5 flex-wrap text-xs text-ink-3">
                {c.taxCode && <span className="font-mono">MST: {c.taxCode}</span>}
                {c.legalComplianceMode && (
                  <CenterLegalModeBadge mode={normalizeComplianceMode(c.legalComplianceMode)} />
                )}
              </div>
            )}
          </div>
        ),
      },
      {
        id: "metrics",
        label: "Visa & Đánh giá",
        render: (c) => (
          <div className="space-y-1 text-xs whitespace-nowrap">
            {c.visaSuccessRate != null ? (
              <div className="flex items-center gap-1 font-medium text-ok">
                <TrendingUpIcon className="size-3.5" />
                Đỗ Visa: {c.visaSuccessRate}%
              </div>
            ) : (
              <div className="text-ink-3 italic">Chưa rõ tỷ lệ Visa</div>
            )}
            {(c.reviewCount ?? 0) > 0 && c.averageRating != null && (
              <div className="flex items-center gap-1 text-ink-2">
                <StarIcon className="size-3 fill-warn text-warn" />
                <span className="font-medium text-ink">{formatRating(c.averageRating)}</span>
                <span>({c.reviewCount ?? 0})</span>
              </div>
            )}
          </div>
        ),
      },
      {
        id: "status",
        label: "Trạng thái",
        render: (c) => {
          const isVerified = c.verificationStatus === "verified";
          return (
            <div className="flex flex-col gap-1 items-start whitespace-nowrap">
              <CenterStatusBadge status={c.verificationStatus} />
              {isVerified && c.verifiedAt && (
                <span className="text-xs text-ink-3">
                  {new Date(c.verifiedAt).toLocaleDateString("vi-VN")}
                </span>
              )}
              {c.isActive === false && <CenterVisibilityBadge active={false} />}
            </div>
          );
        },
      },
      {
        id: "actions",
        label: "Thao tác",
        required: true,
        headerClassName: "w-px whitespace-nowrap text-right",
        cellClassName: "w-px text-right whitespace-nowrap",
        render: (c) => {
          return (
            <div
              className="inline-flex items-center justify-end gap-1"
              onClick={(e) => e.stopPropagation()}
            >
              <EditCenterButton
                center={c}
                size="xs"
                variant="ghost"
                className="text-ink-2 hover:text-ink"
                onSuccess={updateCenterInState}
              >
                <PencilIcon className="size-3.5" />
                <span className="hidden xl:inline">Sửa</span>
              </EditCenterButton>

              {c.isActive === false ? (
                <Button
                  type="button"
                  variant="ghost"
                  size="xs"
                  className="text-ok hover:text-ok hover:bg-ok-wash"
                  onClick={() => setConfirmTarget({ center: c, action: "show" })}
                  title="Hiện lại trung tâm"
                >
                  <EyeIcon className="size-3.5" />
                  <span className="hidden xl:inline">Hiện</span>
                </Button>
              ) : (
                <Button
                  type="button"
                  variant="ghost"
                  size="xs"
                  className="text-ink-2 hover:text-ink"
                  onClick={() => setConfirmTarget({ center: c, action: "hide" })}
                  title="Ẩn trung tâm"
                >
                  <EyeOffIcon className="size-3.5" />
                  <span className="hidden xl:inline">Ẩn</span>
                </Button>
              )}
            </div>
          );
        },
      },
    ],
    [failedLogos, updateCenterInState]
  );

  const toolbar: DataTableToolbarSlots = {
    filters: (
      <Popover open={filterMenuOpen} onOpenChange={handleFilterMenuOpenChange}>
        <PopoverTrigger
          render={
            <Button
              type="button"
              variant="outline"
              size="sm"
              className="border-border bg-muted/60 text-foreground hover:bg-muted"
            />
          }
        >
          <FilterIcon className="size-4" />
          Bộ lọc
          {activeFilterCount > 0 ? ` (${activeFilterCount})` : ""}
        </PopoverTrigger>
        <PopoverContent align="start" className="w-80 max-w-[calc(100vw-2rem)] p-4">
          <form onSubmit={handleApplyFilters} className="space-y-4">
            <div className="space-y-1.5">
              <Label htmlFor="center-filter-city">Địa điểm</Label>
              <OptionSelect
                id="center-filter-city"
                value={draftCity}
                options={CITY_OPTIONS}
                onChange={setDraftCity}
                size="sm"
                className={FILTER_FIELD_BG}
              />
            </div>

            <div className="space-y-1.5">
              <Label htmlFor="center-filter-type">Loại hình trung tâm</Label>
              <OptionSelect
                id="center-filter-type"
                value={draftType}
                options={TYPE_OPTIONS}
                onChange={setDraftType}
                size="sm"
                className={FILTER_FIELD_BG}
              />
            </div>

            <div className="space-y-1.5">
              <Label htmlFor="center-filter-status">Trạng thái duyệt</Label>
              <OptionSelect
                id="center-filter-status"
                value={draftStatus}
                options={STATUS_OPTIONS}
                onChange={setDraftStatus}
                size="sm"
                className={FILTER_FIELD_BG}
              />
            </div>

            <div className="flex items-center justify-end gap-2 pt-1">
              <Button type="button" variant="outline" size="sm" onClick={handleResetFilters}>
                Xóa lọc
              </Button>
              <Button type="submit" size="sm">
                Áp dụng
              </Button>
            </div>
          </form>
        </PopoverContent>
      </Popover>
    ),
    search: (
      <form onSubmit={handleSearchSubmit} role="search" className="relative">
        <Input
          id="admin-centers-search"
          type="text"
          value={searchInput}
          onChange={(e) => setSearchInput(e.target.value)}
          placeholder="Tìm theo tên, mã (CTY_...), địa chỉ..."
          aria-label="Tìm kiếm trung tâm tư vấn"
          className={cn("pr-10", FILTER_FIELD_BG)}
        />
        <Button
          type="submit"
          variant="ghost"
          size="icon-sm"
          className="absolute top-1/2 right-1 -translate-y-1/2"
          aria-label="Tìm kiếm"
        >
          <SearchIcon className="size-4" />
        </Button>
      </form>
    ),
    actions: (
      <>
        {hasActiveSearchOrFilter && (
          <Button type="button" variant="outline" size="sm" onClick={handleResetFilters}>
            Xóa lọc
          </Button>
        )}
        <Button
          type="button"
          variant="outline"
          size="sm"
          onClick={() => void fetchCenters(page)}
          disabled={isLoading}
          className="gap-1.5"
        >
          <RotateCcwIcon className={cn("size-3.5", isLoading && "animate-spin")} />
          Làm mới
        </Button>
        <Button
          type="button"
          variant="outline"
          size="sm"
          onClick={() => setQueueOpen(true)}
          className="gap-1.5"
        >
          <ShieldCheckIcon className="size-4" />
          Hồ sơ xác minh
          {pendingCount > 0 && (
            <span className="inline-flex min-w-5 items-center justify-center rounded-full bg-risk px-1.5 text-[11px] font-semibold leading-5 text-white">
              {pendingCount}
            </span>
          )}
        </Button>
      </>
    ),
  };

  return (
    <div className="flex min-h-0 flex-1 flex-col gap-4">
      <header className="shrink-0">
        <h1 className="text-h2">Trung tâm tư vấn</h1>
        <p className="text-sm text-ink-2 mt-1">
          Danh sách đơn vị tư vấn du học, hồ sơ pháp lý và kiểm duyệt
        </p>
      </header>

      <div className="flex min-h-0 flex-1 flex-col gap-6 lg:flex-row">
        <div className="flex min-h-0 min-w-0 flex-1 flex-col">
          <DataTable
            columns={columns}
            items={centers}
            getRowId={(c) => c.id}
            selectedRowId={selectedCenter?.id}
            onRowSelect={(c) => setSelectedCenter((prev) => (prev?.id === c.id ? null : c))}
            isLoading={isLoading}
            error={listError ?? undefined}
            emptyMessage="Không tìm thấy trung tâm tư vấn nào phù hợp."
            label="Danh sách trung tâm tư vấn"
            toolbar={toolbar}
            footer={
              <TablePagination
                page={page}
                totalPages={totalPages}
                totalCount={totalCount}
                itemLabel="trung tâm"
                disabled={isLoading}
                onPageChange={setPage}
              />
            }
          />
        </div>

        {selectedCenter && (
          <AdminCenterDetailPanel
            center={selectedCenter}
            onClose={() => setSelectedCenter(null)}
          />
        )}
      </div>

      <CenterActionConfirmDialog
        center={confirmTarget?.center ?? null}
        action={confirmTarget?.action ?? null}
        isSubmitting={isSubmitting}
        onClose={() => setConfirmTarget(null)}
        onConfirm={handleConfirmTarget}
      />

      <AdminVerificationQueueModal
        isOpen={queueOpen}
        onClose={() => {
          setQueueOpen(false);
          void refreshPending();
        }}
        onRefreshCenters={() => void fetchCenters(page)}
      />
    </div>
  );
}
