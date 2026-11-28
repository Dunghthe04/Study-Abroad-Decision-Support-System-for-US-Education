"use client";

import { useState } from "react";
import { DownloadIcon, FileInputIcon, FilterIcon, PlusIcon, RotateCcwIcon, SearchIcon, Trash2Icon } from "lucide-react";
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogIcon,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Popover, PopoverContent, PopoverTitle, PopoverTrigger } from "@/components/ui/popover";
import { Tabs, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { toast } from "@/components/ui/toast";
import { DataTable, type DataTableToolbarSlots } from "@/components/admin/shared/DataTable";
import { RightPanelSlot } from "@/components/admin/shared/DetailPanel";
import { TablePagination } from "@/components/admin/shared/TablePagination";
import { OptionSelect } from "@/components/admin/universities/OptionSelect";
import { SlmImportDialog } from "@/components/admin/slm-samples/SlmImportDialog";
import { SlmImportResultDialog } from "@/components/admin/slm-samples/SlmImportResultDialog";
import { SlmSampleFormPanel } from "@/components/admin/slm-samples/SlmSampleFormPanel";
import { slmSampleColumns } from "@/components/admin/slm-samples/slm-sample-columns";
import { useAuth } from "@/contexts/AuthContext";
import { adminSlmSampleApi } from "@/lib/slm-sample-api";
import { SAMPLE_TYPE_OPTIONS, SOURCE_LABELS, SPLIT_OPTIONS, sampleToRequest } from "@/lib/slm-sample-format";
import { useFetched } from "@/lib/use-fetched";
import type { SaveSlmSampleRequest, SlmImportResult, SlmSample, SlmSampleType, SlmSplit } from "@/types/slm-sample";

const FILTER_FIELD_BG = "bg-card dark:bg-input/30";
const TOOLBAR_BUTTON_BG = "border-border bg-card text-foreground shadow-xs hover:bg-muted dark:bg-input/30 dark:hover:bg-input/50";
const PAGE_SIZE = 20;

const TYPE_FILTER_OPTIONS = [{ value: "", label: "Mọi loại mẫu" }, ...SAMPLE_TYPE_OPTIONS];
const SPLIT_FILTER_OPTIONS = [{ value: "", label: "Mọi tập" }, ...SPLIT_OPTIONS];

const TAB_TRIGGER_CLASS =
  "flex-none rounded-none border-0 border-b-2 border-transparent px-3 data-active:border-b-brand data-active:bg-transparent data-active:text-foreground data-active:shadow-none dark:data-active:border-transparent dark:data-active:border-b-brand dark:data-active:bg-transparent after:hidden";

interface Filters {
  search: string;
  sampleType: string;
  split: string;
  deleted: boolean;
  createdBy: string;
  from: string;
  to: string;
  source: string;
  page: number;
}

const EMPTY_FILTERS: Filters = { search: "", sampleType: "", split: "", deleted: false, createdBy: "", from: "", to: "", source: "", page: 1 };

function errorText(error: unknown, fallback: string): string {
  return error instanceof Error ? error.message : fallback;
}

export function SlmSampleListView() {
  const { user } = useAuth();
  const isAdmin = user?.role === "admin";
  const [exporting, setExporting] = useState<"jsonl" | "xlsx" | null>(null);
  const [draft, setDraft] = useState<Filters>(EMPTY_FILTERS);
  const [applied, setApplied] = useState<Filters>(EMPTY_FILTERS);
  const [isFilterOpen, setIsFilterOpen] = useState(false);
  const [reloadCounter, setReloadCounter] = useState(0);

  const [formState, setFormState] = useState<{ sample: SlmSample | null } | null>(null);
  const [deleting, setDeleting] = useState<SlmSample | null>(null);
  const [deleteBusy, setDeleteBusy] = useState(false);
  const [importResult, setImportResult] = useState<SlmImportResult | null>(null);
  const [importOpen, setImportOpen] = useState(false);
  const [importSession, setImportSession] = useState(0);

  const list = useFetched(`list|${JSON.stringify(applied)}|${reloadCounter}`, () =>
    adminSlmSampleApi.list({
      sampleType: applied.sampleType === "" ? undefined : (applied.sampleType as SlmSampleType),
      split: applied.split === "" ? undefined : (applied.split as SlmSplit),
      search: applied.search || undefined,
      createdBy: applied.createdBy || undefined,
      from: applied.from || undefined,
      to: applied.to || undefined,
      source: applied.source || undefined,
      deleted: applied.deleted,
      page: applied.page,
      pageSize: PAGE_SIZE,
    }),
  );
  const summary = useFetched(`summary|${reloadCounter}`, () => adminSlmSampleApi.summary());
  const creators = useFetched("creators", () => adminSlmSampleApi.creators());
  const sources = useFetched(`sources|${reloadCounter}`, () => adminSlmSampleApi.sources());

  const creatorOptions = [{ value: "", label: "Mọi người tạo" }, ...(creators.data ?? []).map((item) => ({ value: item.userId, label: item.name }))];
  const sourceValues = sources.data ?? ["manual", "excel"];
  const sourceOptions = [
    { value: "", label: "Mọi nguồn" },
    ...sourceValues.map((value) => ({ value, label: SOURCE_LABELS[value] ?? value })),
  ];

  const items = list.data?.items ?? [];
  const reload = () => setReloadCounter((current) => current + 1);

  const apply = (next: Filters) => {
    setApplied(next);
  };

  const handleSearch = (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    apply({ ...applied, search: draft.search.trim(), page: 1 });
  };

  const handleFilterSubmit = (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (draft.from && draft.to && draft.from > draft.to) {
      toast.add({ type: "error", title: "Khoảng ngày không hợp lệ", description: "Ngày bắt đầu phải trước hoặc bằng ngày kết thúc." });
      return;
    }
    apply({ ...applied, sampleType: draft.sampleType, split: draft.split, createdBy: draft.createdBy, from: draft.from, to: draft.to, source: draft.source, page: 1 });
    setIsFilterOpen(false);
  };

  const handleResetFilters = () => {
    const cleared = { sampleType: "", split: "", createdBy: "", from: "", to: "", source: "" };
    setDraft({ ...draft, ...cleared });
    apply({ ...applied, ...cleared, page: 1 });
    setIsFilterOpen(false);
  };

  const handleReset = () => {
    setDraft({ ...EMPTY_FILTERS, deleted: applied.deleted });
    apply({ ...EMPTY_FILTERS, deleted: applied.deleted });
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
      toast.add({ type: "success", title: "Đã lưu thay đổi" });
      if (formState?.sample?.id === sample.id) setFormState({ sample: saved });
      reload();
    } catch (error) {
      toast.add({ type: "error", title: "Không lưu được thay đổi", description: errorText(error, "Vui lòng thử lại sau.") });
    }
  };

  const handleRestore = async (sample: SlmSample) => {
    try {
      const restored = await adminSlmSampleApi.restore(sample.id);
      toast.add({ type: "success", title: "Đã khôi phục mẫu" });
      if (formState?.sample?.id === sample.id) setFormState(applied.deleted ? null : { sample: restored });
      if (items.length === 1 && applied.page > 1) setApplied({ ...applied, page: applied.page - 1 });
      reload();
    } catch (error) {
      toast.add({ type: "error", title: "Không khôi phục được mẫu", description: errorText(error, "Vui lòng thử lại sau.") });
    }
  };

  const confirmDelete = async () => {
    if (!deleting) return;
    setDeleteBusy(true);
    try {
      await adminSlmSampleApi.remove(deleting.id);
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

  const openImport = () => {
    setImportSession((current) => current + 1);
    setImportOpen(true);
  };

  const handleImported = (result: SlmImportResult) => {
    setImportOpen(false);
    setImportResult(result);
    reload();
  };

  const columns = slmSampleColumns({
    deleted: applied.deleted,
    onEdit: (sample) => setFormState({ sample }),
    onDelete: setDeleting,
    onRestore: (sample) => void handleRestore(sample),
    onChangeField: handleChangeField,
  });

  const activeFilterCount = [applied.sampleType, applied.split, applied.createdBy, applied.from, applied.to, applied.source].filter((value) => value !== "").length;

  const handleExport = async (format: "jsonl" | "xlsx") => {
    setExporting(format);
    try {
      await adminSlmSampleApi.exportAll(format);
      toast.add({ type: "success", title: "Đã xuất mẫu huấn luyện", description: format === "jsonl" ? "File JSONL" : "File Excel" });
      reload();
    } catch (error) {
      toast.add({ type: "error", title: "Không xuất được file", description: errorText(error, "Vui lòng thử lại sau.") });
    } finally {
      setExporting(null);
    }
  };

  const toolbar: DataTableToolbarSlots = {
    filters: (
      <Popover open={isFilterOpen} onOpenChange={handleFilterOpenChange}>
        <PopoverTrigger
          render={
            <Button
              variant="outline"
              size="sm"
              className={
                activeFilterCount > 0
                  ? `${TOOLBAR_BUTTON_BG} border-brand/40 font-medium text-brand`
                  : TOOLBAR_BUTTON_BG
              }
            />
          }
        >
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
            <div className="space-y-1.5">
              <label htmlFor="slm-source-filter" className="text-sm font-medium">Nguồn</label>
              <OptionSelect id="slm-source-filter" value={draft.source} options={sourceOptions} onChange={(value) => setDraft({ ...draft, source: value })} className={FILTER_FIELD_BG} />
            </div>
            <div className="space-y-1.5">
              <label htmlFor="slm-creator-filter" className="text-sm font-medium">Người tạo</label>
              <OptionSelect id="slm-creator-filter" value={draft.createdBy} options={creatorOptions} onChange={(value) => setDraft({ ...draft, createdBy: value })} className={FILTER_FIELD_BG} />
            </div>
            <div className="grid grid-cols-2 gap-2">
              <div className="space-y-1.5">
                <label htmlFor="slm-from-filter" className="text-sm font-medium">Từ ngày</label>
                <Input id="slm-from-filter" type="date" value={draft.from} max={draft.to || undefined} onChange={(event) => setDraft({ ...draft, from: event.target.value })} className={FILTER_FIELD_BG} />
              </div>
              <div className="space-y-1.5">
                <label htmlFor="slm-to-filter" className="text-sm font-medium">Đến ngày</label>
                <Input id="slm-to-filter" type="date" value={draft.to} min={draft.from || undefined} onChange={(event) => setDraft({ ...draft, to: event.target.value })} className={FILTER_FIELD_BG} />
              </div>
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
          <Button
            type="button"
            variant="ghost"
            size="sm"
            onClick={handleReset}
            className="border-transparent bg-transparent text-muted-foreground shadow-none hover:bg-muted hover:text-foreground"
          >
            <RotateCcwIcon />
            Xóa lọc
          </Button>
        )}
        {isAdmin && (
          <>
            <Button type="button" variant="outline" size="sm" className={TOOLBAR_BUTTON_BG} disabled={exporting !== null} loading={exporting === "jsonl"} onClick={() => void handleExport("jsonl")}>
              <DownloadIcon />
              Xuất JSONL
            </Button>
            <Button type="button" variant="outline" size="sm" className={TOOLBAR_BUTTON_BG} disabled={exporting !== null} loading={exporting === "xlsx"} onClick={() => void handleExport("xlsx")}>
              <DownloadIcon />
              Xuất Excel
            </Button>
          </>
        )}
        <Button type="button" variant="outline" size="sm" className={TOOLBAR_BUTTON_BG} onClick={openImport}>
          <FileInputIcon />
          Nhập Excel
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
        <Tabs
          value={applied.deleted ? "deleted" : "active"}
          onValueChange={(value) => {
            setFormState(null);
            apply({ ...applied, deleted: value === "deleted", page: 1 });
          }}
        >
          <TabsList variant="line" className="h-auto w-full justify-start gap-0 border-b border-border p-0">
            <TabsTrigger value="active" className={TAB_TRIGGER_CLASS}>
              Đang dùng{summary.data ? ` (${summary.data.total})` : ""}
            </TabsTrigger>
            <TabsTrigger value="deleted" className={TAB_TRIGGER_CLASS}>
              Đã xóa{summary.data ? ` (${summary.data.deleted})` : ""}
            </TabsTrigger>
          </TabsList>
        </Tabs>

        <DataTable
          columns={columns}
          items={items}
          getRowId={(sample) => sample.id}
          selectedRowId={formState?.sample?.id}
          onRowSelect={(sample) => setFormState({ sample })}
          isLoading={list.loading && !list.data}
          error={message}
          emptyMessage={applied.deleted ? "Không có mẫu đã xóa nào." : "Không có mẫu nào khớp bộ lọc."}
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

      <RightPanelSlot item={formState}>
        {(active) => (
          <SlmSampleFormPanel
            sample={active.sample}
            version={reloadCounter}
            onClose={() => setFormState(null)}
            onChanged={handlePanelChanged}
            onDelete={setDeleting}
            onRestore={(sample) => void handleRestore(sample)}
          />
        )}
      </RightPanelSlot>

      <SlmImportDialog key={importSession} open={importOpen} onOpenChange={setImportOpen} onImported={handleImported} />
      <SlmImportResultDialog result={importResult} onClose={() => setImportResult(null)} />

      <AlertDialog open={deleting !== null} onOpenChange={(open) => !open && !deleteBusy && setDeleting(null)}>
        <AlertDialogContent variant="action">
          <AlertDialogHeader>
            <AlertDialogIcon tone="destructive">
              <Trash2Icon />
            </AlertDialogIcon>
            <AlertDialogTitle>Xóa mẫu này?</AlertDialogTitle>
            <AlertDialogDescription>Mẫu sẽ chuyển sang danh sách Đã xóa và không có trong các lần xuất sau. Bạn có thể khôi phục lại.</AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter layout="pair">
            <AlertDialogCancel disabled={deleteBusy}>Hủy</AlertDialogCancel>
            <AlertDialogAction tone="destructive" onClick={() => void confirmDelete()} loading={deleteBusy}>
              Xóa
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}
