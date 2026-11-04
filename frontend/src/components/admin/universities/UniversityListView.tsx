"use client";

import { useEffect, useState } from "react";
import { FilterIcon, PlusIcon, RotateCcwIcon, SearchIcon } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { DataTable, type DataTableToolbarSlots } from "@/components/admin/shared/DataTable";
import {
  Popover,
  PopoverContent,
  PopoverTitle,
  PopoverTrigger,
} from "@/components/ui/popover";
import { TablePagination } from "@/components/admin/shared/TablePagination";
import { getAdminUniversities } from "@/lib/admin-university-api";
import {
  CONTROL_FILTER_OPTIONS,
  type LabeledOption,
} from "@/lib/admin-university-display";
import { profileFlowApi } from "@/lib/profile-flow-api";
import type { UsStateOption } from "@/types/profile-flow";
import { CreateUniversityPanel } from "./CreateUniversityPanel";
import { universityColumns } from "./university-columns";
import { OfferingsPanel } from "./OfferingsPanel";
import { OptionSelect } from "./OptionSelect";
import type { PagedResult } from "@/types/api";
import type {
  AdminUniversityActiveFilter,
  AdminUniversityDetail,
  AdminUniversityFilters,
  AdminUniversityListItem,
} from "@/types/admin-university";

/** Nền cho ô lọc để tách khỏi nền trang. */
const FILTER_FIELD_BG = "bg-card dark:bg-input/30";

const ACTIVE_FILTER_OPTIONS: LabeledOption[] = [
  { value: "all", label: "Mọi trạng thái" },
  { value: "active", label: "Đang hiển thị" },
  { value: "hidden", label: "Đang ẩn" },
];

const EMPTY_FILTERS: AdminUniversityFilters = {
  search: "",
  state: "",
  control: "",
  active: "all",
  page: 1,
};

interface LoadResult {
  key: string;
  data: PagedResult<AdminUniversityListItem> | null;
  error: string | null;
}

export function UniversityListView() {
  const [draft, setDraft] = useState<AdminUniversityFilters>(EMPTY_FILTERS);
  const [applied, setApplied] = useState<AdminUniversityFilters>(EMPTY_FILTERS);
  const [result, setResult] = useState<LoadResult | null>(null);
  const [selected, setSelected] = useState<AdminUniversityListItem | null>(
    null,
  );
  const [isCreating, setIsCreating] = useState(false);
  const [isFilterOpen, setIsFilterOpen] = useState(false);
  const [reloadCounter, setReloadCounter] = useState(0);
  const [states, setStates] = useState<UsStateOption[]>([]);

  const requestKey = JSON.stringify({ filters: applied, reloadCounter });
  const isLoading = result === null || result.key !== requestKey;
  const data = isLoading ? null : result.data;
  const error = isLoading ? null : result.error;

  useEffect(() => {
    let cancelled = false;

    getAdminUniversities(applied)
      .then((loaded) => {
        if (!cancelled) {
          setResult({ key: requestKey, data: loaded, error: null });
        }
      })
      .catch((err: unknown) => {
        if (!cancelled) {
          const message =
            err instanceof Error
              ? err.message
              : "Không thể tải danh sách trường.";
          setResult({ key: requestKey, data: null, error: message });
        }
      });

    return () => {
      cancelled = true;
    };
  }, [applied, requestKey]);

  useEffect(() => {
    let cancelled = false;
    profileFlowApi.getMetadata()
      .then((metadata) => {
        if (!cancelled) {
          setStates(metadata.states);
        }
      })
      .catch(() => {
        if (!cancelled) {
          setStates([]);
        }
      });
    return () => {
      cancelled = true;
    };
  }, []);

  const handleSelect = (university: AdminUniversityListItem) => {
    setSelected(university);
    setIsCreating(false);
  };

  const handleSubmit = (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setApplied({ ...draft, search: draft.search.trim(), state: draft.state.trim().toUpperCase(), page: 1 });
    setIsFilterOpen(false);
  };

  const handleSearch = (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setApplied({ ...applied, search: draft.search.trim(), page: 1 });
  };

  const handleResetFilters = () => {
    setDraft({ ...EMPTY_FILTERS, search: draft.search });
    setApplied({ ...EMPTY_FILTERS, search: applied.search });
    setIsFilterOpen(false);
  };

  const handleReset = () => {
    setDraft(EMPTY_FILTERS);
    setApplied(EMPTY_FILTERS);
    setIsFilterOpen(false);
  };

  const handleFilterOpenChange = (open: boolean) => {
    if (open) {
      setDraft({ ...applied, search: draft.search });
    }
    setIsFilterOpen(open);
  };

  const goToPage = (page: number) => {
    setApplied({ ...applied, page });
  };

  const handleUniversityChanged = (saved: AdminUniversityDetail) => {
    const updateRow = (row: AdminUniversityListItem): AdminUniversityListItem => {
      if (row.id !== saved.id) {
        return row;
      }
      return {
        ...row,
        name: saved.name,
        city: saved.city,
        state: saved.state,
        control: saved.control,
        acceptanceRate: saved.acceptanceRate,
        isActive: saved.isActive,
        logoUrl: saved.logoUrl,
      };
    };

    if (result && result.data) {
      const items = result.data.items.map(updateRow);
      setResult({ ...result, data: { ...result.data, items } });
    }
    if (selected) {
      setSelected(updateRow(selected));
    }
  };

  const handleUniversityCreated = (created: AdminUniversityDetail) => {
    const newItem: AdminUniversityListItem = {
      id: created.id,
      code: created.code,
      name: created.name,
      city: created.city,
      state: created.state,
      control: created.control,
      acceptanceRate: created.acceptanceRate,
      isActive: created.isActive,
      studyLevels: [],
      latestRetrievedAt: null,
      logoUrl: created.logoUrl,
    };

    setReloadCounter((current) => current + 1);
    setSelected(newItem);
    setIsCreating(false);
  };

  const columns = universityColumns(states, handleSelect);

  const totalPages = data?.totalPages ?? 0;
  const currentPage = data?.page ?? applied.page;

  const activeFilterTextColor =
    draft.active === "all" ? "text-muted-foreground" : "";

  const activeFilterCount = [
    applied.state !== "",
    applied.control !== "",
    applied.active !== "all",
  ].filter(Boolean).length;

  const toolbar: DataTableToolbarSlots = {
    filters: (
      <Popover open={isFilterOpen} onOpenChange={handleFilterOpenChange}>
        <PopoverTrigger
          render={
            <Button
              variant="outline"
              size="sm"
              className="border-border bg-muted/60 text-foreground hover:bg-muted"
            />
          }
        >
          <FilterIcon />
          Bộ lọc{activeFilterCount > 0 && ` (${activeFilterCount})`}
        </PopoverTrigger>
        <PopoverContent align="start" className="w-80 max-w-[calc(100vw-2rem)] p-4">
          <PopoverTitle>Bộ lọc trường</PopoverTitle>
          <form onSubmit={handleSubmit} className="flex flex-col gap-4">
            <div className="space-y-1.5">
              <label htmlFor="university-state-filter" className="text-sm font-medium">Bang</label>
              <Input
                id="university-state-filter"
                value={draft.state}
                onChange={(event) => setDraft({ ...draft, state: event.target.value })}
                placeholder="Mã bang (CA)"
                maxLength={2}
                className={FILTER_FIELD_BG}
              />
            </div>
            <div className="space-y-1.5">
              <label htmlFor="university-control-filter" className="text-sm font-medium">Loại trường</label>
              <OptionSelect
                id="university-control-filter"
                value={draft.control}
                options={CONTROL_FILTER_OPTIONS}
                onChange={(value) => setDraft({ ...draft, control: value })}
                className={FILTER_FIELD_BG}
              />
            </div>
            <div className="space-y-1.5">
              <label htmlFor="university-active-filter" className="text-sm font-medium">Trạng thái hiển thị</label>
              <OptionSelect
                id="university-active-filter"
                value={draft.active}
                options={ACTIVE_FILTER_OPTIONS}
                onChange={(value) => setDraft({ ...draft, active: value as AdminUniversityActiveFilter })}
                className={`${FILTER_FIELD_BG} ${activeFilterTextColor}`}
              />
            </div>
            <div className="flex justify-end gap-2">
              <Button type="button" variant="outline" size="sm" onClick={handleResetFilters}>
                Xóa lọc
              </Button>
              <Button type="submit" size="sm">Áp dụng</Button>
            </div>
          </form>
        </PopoverContent>
      </Popover>
    ),
    search: (
      <form onSubmit={handleSearch} role="search" className="relative">
        <Input
          value={draft.search}
          onChange={(event) => setDraft({ ...draft, search: event.target.value })}
          placeholder="Tìm theo tên hoặc mã trường"
          aria-label="Tìm theo tên hoặc mã trường"
          className={`pr-10 ${FILTER_FIELD_BG}`}
        />
        <Button type="submit" variant="ghost" size="icon-sm" aria-label="Tìm trường" className="absolute top-1/2 right-1 -translate-y-1/2">
          <SearchIcon />
        </Button>
      </form>
    ),
    actions: (
      <div className="flex items-center gap-2">
        {(draft.search || activeFilterCount > 0) && (
          <Button
            type="button"
            variant="outline"
            size="sm"
            onClick={handleReset}
            className="border-border bg-muted/60 text-foreground hover:bg-muted"
          >
            <RotateCcwIcon />
            Xóa lọc
          </Button>
        )}
        <Button
          type="button"
          size="sm"
          onClick={() => setIsCreating(true)}
        >
          <PlusIcon />
          Thêm trường
        </Button>
      </div>
    ),
  };

  return (
    <div className="flex min-h-0 flex-1 flex-col gap-6 lg:flex-row">
      <div className="flex min-h-0 min-w-0 flex-1 flex-col gap-4">
        <DataTable
          columns={columns}
          items={data?.items ?? []}
          getRowId={(university) => university.id}
          selectedRowId={isCreating ? undefined : selected?.id}
          onRowSelect={handleSelect}
          isLoading={isLoading}
          error={error}
          emptyMessage="Không có trường nào khớp bộ lọc."
          label="Danh sách trường"
          toolbar={toolbar}
          footer={
            <TablePagination page={currentPage} totalPages={totalPages} totalCount={data?.totalCount}
              itemLabel="trường" disabled={isLoading || !!error} onPageChange={goToPage} />
          }
        />
      </div>
      {isCreating ? (
        <CreateUniversityPanel
          onClose={() => setIsCreating(false)}
          onCreated={handleUniversityCreated}
        />
      ) : (
        <OfferingsPanel
          university={selected}
          onClose={() => setSelected(null)}
          onUniversityChanged={handleUniversityChanged}
        />
      )}
    </div>
  );
}
