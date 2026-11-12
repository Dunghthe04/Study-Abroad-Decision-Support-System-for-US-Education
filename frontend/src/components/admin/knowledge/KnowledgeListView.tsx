"use client";

import { useRef, useState } from "react";
import { CheckIcon, DownloadIcon, FilterIcon, PlusIcon, RotateCcwIcon, SearchIcon, UploadIcon, WrenchIcon, XIcon } from "lucide-react";
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
import { toast } from "@/components/ui/toast";
import { DataTable, type DataTableToolbarSlots } from "@/components/admin/shared/DataTable";
import { TablePagination } from "@/components/admin/shared/TablePagination";
import { OptionSelect } from "@/components/admin/universities/OptionSelect";
import { SlmImportResultDialog } from "@/components/admin/knowledge/SlmImportResultDialog";
import { SlmReviewDialog } from "@/components/admin/knowledge/SlmReviewDialog";
import { SlmSampleFormPanel } from "@/components/admin/knowledge/SlmSampleFormPanel";
import { slmSampleColumns } from "@/components/admin/knowledge/slm-sample-columns";
import { adminSlmSampleApi } from "@/lib/slm-sample-api";
import { REVIEW_STATUSES, SAMPLE_TYPE_OPTIONS, SPLIT_OPTIONS, sampleToRequest } from "@/lib/slm-sample-format";
import { useFetched } from "@/lib/use-fetched";
import type { SaveSlmSampleRequest, SlmImportResult, SlmReviewStatus, SlmSample, SlmSampleType, SlmSplit } from "@/types/slm-sample";

const FILTER_FIELD_BG = "bg-card dark:bg-input/30";
const TOOLBAR_BUTTON_BG = "border-border bg-muted/60 text-foreground hover:bg-muted";
const PAGE_SIZE = 20;

const TYPE_FILTER_OPTIONS = [{ value: "", label: "Mọi loại mẫu" }, ...SAMPLE_TYPE_OPTIONS];
const SPLIT_FILTER_OPTIONS = [{ value: "", label: "Mọi tập" }, ...SPLIT_OPTIONS];

const TAB_TRIGGER_CLASS =
  "flex-none rounded-none border-0 border-b-2 border-transparent px-3 data-active:border-b-brand data-active:bg-transparent data-active:text-foreground data-active:shadow-none dark:data-active:border-transparent dark:data-active:border-b-brand dark:data-active:bg-transparent after:hidden";

const BULK_ACTIONS: { status: SlmReviewStatus; label: string; icon: typeof CheckIcon; className: string }[] = [
  { status: "approved", label: "Duyệt", icon: CheckIcon, className: "border-ok/20 bg-ok-wash text-ok hover:bg-ok-wash/70" },
  { status: "rejected", label: "Từ chối", icon: XIcon, className: "border-risk/20 bg-risk-wash text-risk hover:bg-risk-wash/70" },
  { status: "needs_fix", label: "Cần sửa", icon: WrenchIcon, className: "border-warn/20 bg-warn-wash text-warn hover:bg-warn-wash/70" },
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
  const fileInput = useRef<HTMLInputElement>(null);

  const list = useFetched(`list|${JSON.stringify(applied)}|${reloadCounter}`, () =>
    adminSlmSampleApi.list({
      reviewStatus: applied.status === "" ? undefined : (applied.status as SlmReviewStatus),
      sampleType: applied.sampleType === "" ? undefined : (applied.sampleType as SlmSampleType),
      split: applied.split === "" ? undefined : (applied.split as SlmSplit),
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

  const handlePanelChanged = (sample: SlmSample, isNew: boolean) => {
    setFormState(isNew ? null : { sample });
    reload();
  };

  const handleChangeField = async (sample: SlmSample, change: Partial<SaveSlmSampleRequest>) => {
    try {
      const saved = await adminSlmSampleApi.update(sample.id, { ...sampleToRequest(sample), ...change });
      const resetToDraft = saved.reviewStatus !== sample.reviewStatus;
      toast.add({
        type: "success",
        title: "Đã lưu thay đổi",
        description: resetToDraft ? "Nhãn đã đổi nên mẫu quay về Nháp, cần duyệt lại." : undefined,
      });
      if (formState?.sample?.id === sample.id) setFormState({ sample: saved });
      reload();
    } catch (error) {
      toast.add({ type: "error", title: "Không lưu được thay đổi", description: errorText(error, "Vui lòng thử lại sau.") });
    }
  };

  const handleReviewed = (message: string) => {
    setReviewStatus(null);
    setSelectedIds(new Set());
    toast.add({ type: "success", title: message });
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
      toast.add({ type: "success", title: "Đã xóa mẫu" });
      reload();
    } catch (error) {
      setDeleting(null);
      toast.add({ type: "error", title: "Không xóa được mẫu", description: errorText(error, "Vui lòng thử lại sau.") });
    } finally {
      setDeleteBusy(false);
    }
  };

  const handleFile = async (event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    event.target.value = "";
    if (!file) return;
    if (!file.name.toLowerCase().endsWith(".xlsx")) {
      toast.add({ type: "error", title: "Chỉ nhận file .xlsx" });
      return;
    }
    setImportBusy(true);
    try {
      setImportResult(await adminSlmSampleApi.importXlsx(file));
      reload();
    } catch (error) {
      toast.add({ type: "error", title: "Không nhập được file", description: errorText(error, "Vui lòng thử lại sau.") });
    } finally {
      setImportBusy(false);
    }
  };

  const handleExport = async () => {
    setExportBusy(true);
    try {
      await adminSlmSampleApi.exportJsonl(applied.split === "" ? undefined : (applied.split as SlmSplit));
    } catch (error) {
      toast.add({ type: "error", title: "Không xuất được file", description: errorText(error, "Vui lòng thử lại sau.") });
    } finally {
      setExportBusy(false);
    }
  };

  const columns = slmSampleColumns({
    selectedIds,
    onToggle: toggleOne,
    onEdit: (sample) => setFormState({ sample }),
    onDelete: setDeleting,
    onChangeField: handleChangeField,
  });

  const activeFilterCount = [applied.sampleType !== "", applied.split !== ""].filter(Boolean).length;

  const toolbar: DataTableToolbarSlots = {
    filters: (
      <Popover open={isFilterOpen} onOpenChange={handleFilterOpenChange}>
        <PopoverTrigger render={<Button variant="outline" size="sm" className={TOOLBAR_BUTTON_BG} />}>
          <FilterIcon />
          Bộ lọc{activeFilterCount > 0 && ` (${activeFilterCount})`}
        </PopoverTrigger>
        <PopoverContent align="start" className="w-80 max-w-[calc(100vw-2rem)] p-4">
          <PopoverTitle>Bộ lọc mẫu</PopoverTitle>
          <form onSubmit={handleFilterSubmit} className="flex flex-col gap-4">
            <div className="space-y-1.5">
              <label htmlFor="slm-type-filter" className="text-sm font-medium">Loại mẫu</label>
              <OptionSelect id="slm-type-filter" value={draft.sampleType} options={TYPE_FILTER_OPTIONS} onChange={(value) => setDraft({ ...draft, sampleType: value })} className={FILTER_FIELD_BG} />
            </div>
            <div className="space-y-1.5">
              <label htmlFor="slm-split-filter" className="text-sm font-medium">Tập dữ liệu</label>
              <OptionSelect id="slm-split-filter" value={draft.split} options={SPLIT_FILTER_OPTIONS} onChange={(value) => setDraft({ ...draft, split: value })} className={FILTER_FIELD_BG} />
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
          <Button type="button" variant="outline" size="sm" onClick={handleReset} className={TOOLBAR_BUTTON_BG}>
            <RotateCcwIcon />
            Xóa lọc
          </Button>
        )}
        <input ref={fileInput} type="file" accept=".xlsx" className="hidden" onChange={(event) => void handleFile(event)} />
        <Button type="button" variant="outline" size="sm" className={TOOLBAR_BUTTON_BG} disabled={importBusy} onClick={() => fileInput.current?.click()}>
          {importBusy ? <Spinner /> : <UploadIcon />} Nhập Excel
        </Button>
        <Button type="button" variant="outline" size="sm" className={TOOLBAR_BUTTON_BG} disabled={exportBusy} onClick={() => void handleExport()}>
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
      <div className="flex min-h-0 min-w-0 flex-1 flex-col gap-3">
        <Tabs value={applied.status === "" ? "all" : applied.status} onValueChange={(value) => apply({ ...applied, status: value === "all" ? "" : String(value), page: 1 })}>
          <TabsList variant="line" className="h-auto w-full justify-start gap-0 border-b border-border p-0">
            <TabsTrigger value="all" className={TAB_TRIGGER_CLASS}>Tất cả{countOf("")}</TabsTrigger>
            {REVIEW_STATUSES.map((item) => (
              <TabsTrigger key={item.id} value={item.id} className={TAB_TRIGGER_CLASS}>{item.label}{countOf(item.id)}</TabsTrigger>
            ))}
          </TabsList>
        </Tabs>

        {selectedIds.size > 0 && (
          <div className="flex flex-wrap items-center gap-2 rounded-md border border-border bg-muted/60 px-3 py-2 text-sm">
            <span className="font-medium">Đã chọn {selectedIds.size} mẫu</span>
            {BULK_ACTIONS.map((action) => (
              <Button key={action.status} type="button" size="sm" variant="outline" className={action.className} onClick={() => setReviewStatus(action.status)}>
                <action.icon />
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

      {formState && <SlmSampleFormPanel sample={formState.sample} onClose={() => setFormState(null)} onChanged={handlePanelChanged} />}

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
