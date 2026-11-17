"use client";

import { useEffect, useState } from "react";
import { FilterIcon, PlusIcon, RotateCcwIcon, SearchIcon } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Popover, PopoverContent, PopoverTitle, PopoverTrigger } from "@/components/ui/popover";
import { useAuth } from "@/contexts/AuthContext";
import { DataTable, type DataTableColumn, type DataTableToolbarSlots } from "../shared/DataTable";
import { DetailPanel } from "../shared/DetailPanel";
import { TablePagination } from "../shared/TablePagination";
import { OptionSelect } from "../universities/OptionSelect";
import { getAdminScholarships } from "@/lib/admin-scholarship-api";
import { scholarshipValueText } from "@/lib/admin-scholarship-display";
import { formatRetrievedAt, type LabeledOption } from "@/lib/admin-university-display";
import { STUDY_LEVELS, studyLevelLabel } from "@/lib/study-levels";
import type { PagedResult } from "@/types/api";
import type { AdminScholarship, AdminScholarshipFilters } from "@/types/admin-scholarship";
import { ScholarshipTable } from "./ScholarshipTable";

const FILTER_FIELD_BG = "bg-card dark:bg-input/30";

const ACTIVE_OPTIONS: LabeledOption[] = [
  { value: "all", label: "Mọi trạng thái" },
  { value: "active", label: "Đang hiển thị" },
  { value: "hidden", label: "Đang ẩn" },
];

const LEVEL_FILTER_OPTIONS: LabeledOption[] = [
  { value: "", label: "Mọi bậc học" },
  ...STUDY_LEVELS.map((level) => ({ value: level, label: studyLevelLabel(level) })),
];

const EMPTY_FILTERS: AdminScholarshipFilters = { search: "", studyLevel: "", active: "all", page: 1 };

interface LoadResult {
  key: string;
  data: PagedResult<AdminScholarship> | null;
  error: string | null;
}

type Selection = { kind: "none" } | { kind: "new" } | { kind: "existing"; scholarship: AdminScholarship; editing: boolean };

export function ScholarshipListView() {
  const { user } = useAuth();
  const canCreate = user?.role === "moderator";
  const [draft, setDraft] = useState<AdminScholarshipFilters>(EMPTY_FILTERS);
  const [applied, setApplied] = useState<AdminScholarshipFilters>(EMPTY_FILTERS);
  const [result, setResult] = useState<LoadResult | null>(null);
  const [selection, setSelection] = useState<Selection>({ kind: "none" });
  const [isFilterOpen, setIsFilterOpen] = useState(false);
  const [reloadCounter, setReloadCounter] = useState(0);

  const requestKey = JSON.stringify({ filters: applied, reloadCounter });
  const isLoading = result === null || result.key !== requestKey;
  const data = isLoading ? null : result.data;
  const error = isLoading ? null : result.error;

  useEffect(() => {
    let cancelled = false;
    getAdminScholarships(applied)
      .then((loaded) => {
        if (!cancelled) setResult({ key: requestKey, data: loaded, error: null });
      })
      .catch((err: unknown) => {
        if (!cancelled) {
          setResult({ key: requestKey, data: null, error: err instanceof Error ? err.message : "Không thể tải danh sách học bổng." });
        }
      });
    return () => {
      cancelled = true;
    };
  }, [applied, requestKey]);

  const patchRow = (saved: AdminScholarship) => {
    if (result?.data) {
      setResult({ ...result, data: { ...result.data, items: result.data.items.map((row) => (row.id === saved.id ? saved : row)) } });
    }
    setSelection((prev) => (prev.kind === "existing" ? { kind: "existing", scholarship: saved, editing: prev.editing } : prev));
  };

  const handleSaved = (saved: AdminScholarship) => {
    patchRow(saved);
    setSelection({ kind: "existing", scholarship: saved, editing: false });
    setReloadCounter((n) => n + 1);
  };

  const handleSearch = (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setApplied({ ...applied, search: draft.search?.trim() ?? "", page: 1 });
  };

  const handleApplyFilters = (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setApplied({ ...draft, search: draft.search?.trim() ?? "", page: 1 });
    setIsFilterOpen(false);
  };

  const handleReset = () => {
    setDraft(EMPTY_FILTERS);
    setApplied(EMPTY_FILTERS);
    setIsFilterOpen(false);
  };

  const columns: DataTableColumn<AdminScholarship>[] = [
    {
      id: "name",
      label: "Học bổng",
      required: true,
      render: (s) => (
        <>
          <button
            type="button"
            className="text-left font-medium text-brand underline-offset-2 hover:underline focus-visible:outline-2 focus-visible:outline-ring"
            onClick={(event) => {
              event.stopPropagation();
              setSelection({ kind: "existing", scholarship: s, editing: false });
            }}
          >
            {s.name}
          </button>
          <div className="text-xs text-muted-foreground">{s.universityName ?? s.provider ?? "—"}</div>
        </>
      ),
    },
    { id: "level", label: "Bậc học", render: (s) => studyLevelLabel(s.studyLevel) },
    { id: "value", label: "Giá trị", render: scholarshipValueText },
    { id: "deadline", label: "Hạn nộp", render: (s) => formatRetrievedAt(s.deadline) },
    { id: "retrievedAt", label: "Ngày lấy dữ liệu", render: (s) => formatRetrievedAt(s.retrievedAt) },
    {
      id: "isActive",
      label: "Hiển thị",
      render: (s) => <Badge variant={s.isActive ? "brand" : "outline"}>{s.isActive ? "Đang hiển thị" : "Đang ẩn"}</Badge>,
    },
  ];

  const activeFilterCount = [applied.studyLevel !== "", applied.active !== "all"].filter(Boolean).length;

  const toolbar: DataTableToolbarSlots = {
    filters: (
      <Popover
        open={isFilterOpen}
        onOpenChange={(open) => {
          if (open) setDraft({ ...applied, search: draft.search });
          setIsFilterOpen(open);
        }}
      >
        <PopoverTrigger render={<Button variant="outline" size="sm" className="border-border bg-muted/60 text-foreground hover:bg-muted" />}>
          <FilterIcon />
          Bộ lọc{activeFilterCount > 0 && ` (${activeFilterCount})`}
        </PopoverTrigger>
        <PopoverContent align="start" className="w-80 max-w-[calc(100vw-2rem)] p-4">
          <PopoverTitle>Bộ lọc học bổng</PopoverTitle>
          <form onSubmit={handleApplyFilters} className="flex flex-col gap-4">
            <div className="space-y-1.5">
              <label htmlFor="scholarship-level-filter" className="text-sm font-medium">Bậc học</label>
              <OptionSelect
                id="scholarship-level-filter"
                value={draft.studyLevel ?? ""}
                options={LEVEL_FILTER_OPTIONS}
                onChange={(value) => setDraft({ ...draft, studyLevel: value })}
                className={FILTER_FIELD_BG}
              />
            </div>
            <div className="space-y-1.5">
              <label htmlFor="scholarship-active-filter" className="text-sm font-medium">Trạng thái hiển thị</label>
              <OptionSelect
                id="scholarship-active-filter"
                value={draft.active ?? "all"}
                options={ACTIVE_OPTIONS}
                onChange={(value) => setDraft({ ...draft, active: value as AdminScholarshipFilters["active"] })}
                className={FILTER_FIELD_BG}
              />
            </div>
            <div className="flex justify-end gap-2">
              <Button type="button" variant="outline" size="sm" onClick={handleReset}>Xóa lọc</Button>
              <Button type="submit" size="sm">Áp dụng</Button>
            </div>
          </form>
        </PopoverContent>
      </Popover>
    ),
    search: (
      <form onSubmit={handleSearch} role="search" className="relative">
        <Input
          value={draft.search ?? ""}
          onChange={(event) => setDraft({ ...draft, search: event.target.value })}
          placeholder="Tìm theo tên học bổng, trường hoặc đơn vị cấp"
          aria-label="Tìm học bổng"
          className={`pr-10 ${FILTER_FIELD_BG}`}
        />
        <Button type="submit" variant="ghost" size="icon-sm" aria-label="Tìm" className="absolute top-1/2 right-1 -translate-y-1/2">
          <SearchIcon />
        </Button>
      </form>
    ),
    actions: (
      <div className="flex items-center gap-2">
        {(draft.search || activeFilterCount > 0) && (
          <Button type="button" variant="outline" size="sm" onClick={handleReset} className="border-border bg-muted/60 text-foreground hover:bg-muted">
            <RotateCcwIcon />
            Xóa lọc
          </Button>
        )}
        {canCreate && (
          <Button type="button" size="sm" onClick={() => setSelection({ kind: "new" })}>
            <PlusIcon />
            Thêm học bổng
          </Button>
        )}
      </div>
    ),
  };

  const close = () => setSelection({ kind: "none" });

  return (
    <div className="flex min-h-0 flex-1 flex-col gap-6 lg:flex-row">
      <div className="flex min-h-0 min-w-0 flex-1 flex-col gap-4">
        <DataTable
          columns={columns}
          items={data?.items ?? []}
          getRowId={(s) => s.id}
          selectedRowId={selection.kind === "existing" ? selection.scholarship.id : undefined}
          onRowSelect={(s) => setSelection({ kind: "existing", scholarship: s, editing: false })}
          isLoading={isLoading}
          error={error}
          emptyMessage="Không có học bổng nào khớp bộ lọc."
          label="Danh sách học bổng"
          toolbar={toolbar}
          footer={
            <TablePagination
              page={data?.page ?? applied.page}
              totalPages={data?.totalPages ?? 0}
              totalCount={data?.totalCount}
              itemLabel="học bổng"
              disabled={isLoading || !!error}
              onPageChange={(page) => setApplied({ ...applied, page })}
            />
          }
        />
      </div>
      {selection.kind === "new" && (
        <DetailPanel title="Thêm học bổng" description="Gắn với một trường hoặc nhập đơn vị cấp." onClose={close}>
          <ScholarshipTable
            key="new"
            scholarship={null}
            isEditing
            canEdit={false}
            onStartEdit={() => {}}
            onCancel={close}
            onSaved={handleSaved}
            onVisibilityChanged={patchRow}
          />
        </DetailPanel>
      )}
      {selection.kind === "existing" && (
        <DetailPanel title={selection.scholarship.name} description="Học bổng" onClose={close}>
          <ScholarshipTable
            key={selection.scholarship.id}
            scholarship={selection.scholarship}
            isEditing={selection.editing}
            canEdit
            onStartEdit={() => setSelection({ ...selection, editing: true })}
            onCancel={() => setSelection({ ...selection, editing: false })}
            onSaved={handleSaved}
            onVisibilityChanged={(saved) => {
              patchRow(saved);
              setReloadCounter((n) => n + 1);
            }}
          />
        </DetailPanel>
      )}
    </div>
  );
}
