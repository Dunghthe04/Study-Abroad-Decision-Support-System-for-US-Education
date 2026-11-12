"use client";

import { useRef, useState } from "react";
import { DownloadIcon, FilterIcon, PlusIcon, RotateCcwIcon, SearchIcon, UploadIcon } from "lucide-react";
import { Alert, AlertDescription } from "@/components/ui/alert";
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Popover, PopoverContent, PopoverTitle, PopoverTrigger } from "@/components/ui/popover";
import { Spinner } from "@/components/ui/spinner";
import { Tabs, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { DataTable, type DataTableToolbarSlots } from "@/components/admin/shared/DataTable";
import { TablePagination } from "@/components/admin/shared/TablePagination";
import { OptionSelect } from "@/components/admin/universities/OptionSelect";
import { SlmImportResultDialog } from "@/components/admin/knowledge/SlmImportResultDialog";
import { SlmReviewDialog } from "@/components/admin/knowledge/SlmReviewDialog";
import { SlmSampleFormPanel } from "@/components/admin/knowledge/SlmSampleFormPanel";
import { slmSampleColumns } from "@/components/admin/knowledge/slm-sample-columns";
import { adminSlmSampleApi } from "@/lib/slm-sample-api";
import { REVIEW_STATUSES, SAMPLE_TYPES, SPLITS } from "@/lib/slm-sample-format";
import { useFetched } from "@/lib/use-fetched";
import type { SlmImportResult, SlmReviewStatus, SlmSample } from "@/types/slm-sample";

const FILTER_FIELD_BG = "bg-card dark:bg-input/30";
const PAGE_SIZE = 20;

const TYPE_OPTIONS = [{ value: "", label: "Mọi loại mẫu" }, ...SAMPLE_TYPES.map((item) => ({ value: item.id, label: item.label }))];
const SPLIT_OPTIONS = [{ value: "", label: "Mọi tập" }, ...SPLITS.map((item) => ({ value: item.id, label: item.label }))];

const BULK_ACTIONS: { status: SlmReviewStatus; label: string }[] = [
  { status: "approved", label: "Duyệt" },
  { status: "rejected", label: "Từ chối" },
  { status: "needs_fix", label: "Cần sửa" },
];

interface Filters {
  search: string;
  sampleType: string;
  split: string;
  status: string;
  page: number;
}

const EMPTY_FILTERS: Filters = { search: "", sampleType: "", split: "", status: "", page: 1 };

function errorText(error: unknown, fallback: string): string {
  return error instanceof Error ? error.message : fallback;
}

export function KnowledgeListView() {
  const [draft, setDraft] = useState<Filters>(EMPTY_FILTERS);
  const [applied, setApplied] = useState<Filters>(EMPTY_FILTERS);
  const [isFilterOpen, setIsFilterOpen] = useState(false);
  const [reloadCounter, setReloadCounter] = useState(0);

  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());
  const [formState, setFormState] = useState<{ sample: SlmSample | null } | null>(null);
  const [reviewStatus, setReviewStatus] = useState<SlmReviewStatus | null>(null);
  const [deleting, setDeleting] = useState<SlmSample | null>(null);
  const [deleteBusy, setDeleteBusy] = useState(false);
  const [importResult, setImportResult] = useState<SlmImportResult | null>(null);
  const [importBusy, setImportBusy] = useState(false);
  const [exportBusy, setExportBusy] = useState(false);
  const [notice, setNotice] = useState<string | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  const fileInput = useRef<HTMLInputElement>(null);

  const list = useFetched(`list|${JSON.stringify(applied)}|${reloadCounter}`, () =>
    adminSlmSampleApi.list({
      reviewStatus: applied.status === "" ? undefined : (applied.status as SlmReviewStatus),
      sampleType: applied.sampleType === "" ? undefined : (applied.sampleType as never),
      split: applied.split === "" ? undefined : (applied.split as never),
      search: applied.search || undefined,
      page: applied.page,
      pageSize: PAGE_SIZE,
    }),
  );
  const summary = useFetched(`summary|${reloadCounter}`, () => adminSlmSampleApi.summary());

  const items = list.data?.items ?? [];
  const reload = () => setReloadCounter((current) => current + 1);

  const apply = (next: Filters) => {
    setApplied(next);
    setSelectedIds(new Set());
  };

  const countOf = (status: string): string => {
    if (!summary.data) return "";
    const value = status === "" ? summary.data.total : (summary.data.byStatus[status as SlmReviewStatus] ?? 0);
    return ` (${value})`;
  };

  const toggleOne = (id: string, checked: boolean) => {
    const next = new Set(selectedIds);
    if (checked) next.add(id);
    else next.delete(id);
    setSelectedIds(next);
  };

  const handleSearch = (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    apply({ ...applied, search: draft.search.trim(), page: 1 });
  };

  const handleFilterSubmit = (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    apply({ ...applied, sampleType: draft.sampleType, split: draft.split, page: 1 });
    setIsFilterOpen(false);
  };

  const handleResetFilters = () => {
    setDraft({ ...draft, sampleType: "", split: "" });
    apply({ ...applied, sampleType: "", split: "", page: 1 });
    setIsFilterOpen(false);
  };

  const handleReset = () => {
    setDraft({ ...EMPTY_FILTERS, status: applied.status });
    apply({ ...EMPTY_FILTERS, status: applied.status });
    setIsFilterOpen(false);
  };

  const handleFilterOpenChange = (open: boolean) => {
    if (open) setDraft({ ...applied, search: draft.search });
    setIsFilterOpen(open);
  };

  const handleSaved = (isNew: boolean) => {
    setFormState(null);
    setNotice(isNew ? "Đã thêm mẫu." : "Đã lưu mẫu.");
    setActionError(null);
    reload();
  };

  const handleReviewed = (message: string) => {
    setReviewStatus(null);
    setSelectedIds(new Set());
    setNotice(message);
    setActionError(null);
    reload();
  };

  const confirmDelete = async () => {
    if (!deleting) return;
    setDeleteBusy(true);
    try {
      await adminSlmSampleApi.remove(deleting.id);
      const next = new Set(selectedIds);
      next.delete(deleting.id);
      setSelectedIds(next);
      if (formState?.sample?.id === deleting.id) setFormState(null);
      if (items.length === 1 && applied.page > 1) setApplied({ ...applied, page: applied.page - 1 });
      setDeleting(null);
      setNotice("Đã xóa mẫu.");
      setActionError(null);
      reload();
    } catch (error) {
      setDeleting(null);
      setActionError(errorText(error, "Không xóa được mẫu."));
    } finally {
      setDeleteBusy(false);
    }
  };

  const handleFile = async (event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    event.target.value = "";
    if (!file) return;
    if (!file.name.toLowerCase().endsWith(".xlsx")) {
      setActionError("Chỉ nhận file .xlsx.");
      return;
    }
    setImportBusy(true);
    setActionError(null);
    try {
      setImportResult(await adminSlmSampleApi.importXlsx(file));
      reload();
    } catch (error) {
      setActionError(errorText(error, "Không nhập được file."));
    } finally {
      setImportBusy(false);
    }
  };

  const handleExport = async () => {
    setExportBusy(true);
    setActionError(null);
    try {
      await adminSlmSampleApi.exportJsonl(applied.split === "" ? undefined : (applied.split as never));
    } catch (error) {
      setActionError(errorText(error, "Không xuất được file."));
    } finally {
      setExportBusy(false);
    }
  };

  const columns = slmSampleColumns({
    selectedIds,
    onToggle: toggleOne,
    onEdit: (sample) => setFormState({ sample }),
    onDelete: setDeleting,
  });

  const activeFilterCount = [applied.sampleType !== "", applied.split !== ""].filter(Boolean).length;

  const toolbar: DataTableToolbarSlots = {
    filters: (
      <Popover open={isFilterOpen} onOpenChange={handleFilterOpenChange}>
        <PopoverTrigger render={<Button variant="outline" size="sm" className="border-border bg-muted/60 text-foreground hover:bg-muted" />}>
          <FilterIcon />
          Bộ lọc{activeFilterCount > 0 && ` (${activeFilterCount})`}
        </PopoverTrigger>
        <PopoverContent align="start" className="w-80 max-w-[calc(100vw-2rem)] p-4">
          <PopoverTitle>Bộ lọc mẫu</PopoverTitle>
          <form onSubmit={handleFilterSubmit} className="flex flex-col gap-4">
            <div className="space-y-1.5">
              <label htmlFor="slm-type-filter" className="text-sm font-medium">Loại mẫu</label>
              <OptionSelect id="slm-type-filter" value={draft.sampleType} options={TYPE_OPTIONS} onChange={(value) => setDraft({ ...draft, sampleType: value })} className={FILTER_FIELD_BG} />
            </div>
            <div className="space-y-1.5">
              <label htmlFor="slm-split-filter" className="text-sm font-medium">Tập dữ liệu</label>
              <OptionSelect id="slm-split-filter" value={draft.split} options={SPLIT_OPTIONS} onChange={(value) => setDraft({ ...draft, split: value })} className={FILTER_FIELD_BG} />
            </div>
            <div className="flex justify-end gap-2">
              <Button type="button" variant="outline" size="sm" onClick={handleResetFilters}>Xóa lọc</Button>
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
          placeholder="Tìm theo câu hỏi"
          aria-label="Tìm theo câu hỏi"
          className={`pr-10 ${FILTER_FIELD_BG}`}
        />
        <Button type="submit" variant="ghost" size="icon-sm" aria-label="Tìm mẫu" className="absolute top-1/2 right-1 -translate-y-1/2">
          <SearchIcon />
        </Button>
      </form>
    ),
    actions: (
      <div className="flex flex-wrap items-center gap-2">
        {(draft.search || activeFilterCount > 0) && (
          <Button type="button" variant="outline" size="sm" onClick={handleReset} className="border-border bg-muted/60 text-foreground hover:bg-muted">
            <RotateCcwIcon />
            Xóa lọc
          </Button>
        )}
        <input ref={fileInput} type="file" accept=".xlsx" className="hidden" onChange={(event) => void handleFile(event)} />
        <Button type="button" variant="outline" size="sm" disabled={importBusy} onClick={() => fileInput.current?.click()}>
          {importBusy ? <Spinner /> : <UploadIcon />} Nhập Excel
        </Button>
        <Button type="button" variant="outline" size="sm" disabled={exportBusy} onClick={() => void handleExport()}>
          {exportBusy ? <Spinner /> : <DownloadIcon />} Xuất JSONL
        </Button>
        <Button type="button" size="sm" onClick={() => setFormState({ sample: null })}>
          <PlusIcon />
          Thêm mẫu
        </Button>
      </div>
    ),
  };

  const message = list.error ?? summary.error;

  return (
    <div className="flex min-h-0 flex-1 flex-col gap-6 lg:flex-row">
      <div className="flex min-h-0 min-w-0 flex-1 flex-col gap-4">
        <header className="flex flex-col gap-1">
          <h1 className="text-h2">Quản lý tri thức</h1>
          <p className="text-sm text-muted-foreground">Dữ liệu huấn luyện SLM: duyệt, sửa, nhập từ Excel và xuất JSONL theo tập dữ liệu.</p>
        </header>

        {notice && <Alert><AlertDescription>{notice}</AlertDescription></Alert>}
        {actionError && <Alert variant="destructive"><AlertDescription>{actionError}</AlertDescription></Alert>}

        <Tabs value={applied.status === "" ? "all" : applied.status} onValueChange={(value) => apply({ ...applied, status: value === "all" ? "" : String(value), page: 1 })}>
          <TabsList>
            <TabsTrigger value="all">Tất cả{countOf("")}</TabsTrigger>
            {REVIEW_STATUSES.map((item) => (
              <TabsTrigger key={item.id} value={item.id}>{item.label}{countOf(item.id)}</TabsTrigger>
            ))}
          </TabsList>
        </Tabs>

        {selectedIds.size > 0 && (
          <div className="flex flex-wrap items-center gap-2 rounded-md border border-border bg-muted/40 px-3 py-2 text-sm">
            <span>Đã chọn {selectedIds.size} mẫu</span>
            {BULK_ACTIONS.map((action) => (
              <Button key={action.status} type="button" size="sm" variant="outline" onClick={() => setReviewStatus(action.status)}>
                {action.label}
              </Button>
            ))}
            <Button type="button" size="sm" variant="ghost" onClick={() => setSelectedIds(new Set())}>Bỏ chọn</Button>
          </div>
        )}

        <DataTable
          columns={columns}
          items={items}
          getRowId={(sample) => sample.id}
          selectedRowId={formState?.sample?.id}
          onRowSelect={(sample) => setFormState({ sample })}
          isLoading={list.loading && !list.data}
          error={message}
          emptyMessage="Không có mẫu nào khớp bộ lọc."
          label="Danh sách mẫu huấn luyện"
          toolbar={toolbar}
          footer={
            <TablePagination
              page={list.data?.page ?? applied.page}
              totalPages={list.data?.totalPages ?? 0}
              totalCount={list.data?.totalCount}
              itemLabel="mẫu"
              disabled={list.loading || !!list.error}
              onPageChange={(page) => apply({ ...applied, page })}
            />
          }
        />
      </div>

      {formState && (
        <SlmSampleFormPanel sample={formState.sample} onClose={() => setFormState(null)} onSaved={handleSaved} />
      )}

      <SlmReviewDialog status={reviewStatus} ids={Array.from(selectedIds)} onClose={() => setReviewStatus(null)} onDone={handleReviewed} />
      <SlmImportResultDialog result={importResult} onClose={() => setImportResult(null)} />

      <AlertDialog open={deleting !== null} onOpenChange={(open) => !open && !deleteBusy && setDeleting(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Xóa mẫu này?</AlertDialogTitle>
            <AlertDialogDescription>Mẫu bị xóa không khôi phục được và sẽ không còn trong file xuất.</AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel disabled={deleteBusy}>Hủy</AlertDialogCancel>
            <AlertDialogAction onClick={() => void confirmDelete()} disabled={deleteBusy}>
              {deleteBusy && <Spinner />} Xóa
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}
