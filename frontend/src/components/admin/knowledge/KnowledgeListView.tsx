"use client";

import { useState } from "react";
import { CheckIcon, DownloadIcon, FileInputIcon, FilterIcon, PlusIcon, RotateCcwIcon, SearchIcon, WrenchIcon, XIcon } from "lucide-react";
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
import { SlmImportDialog } from "@/components/admin/knowledge/SlmImportDialog";
import { SlmImportResultDialog } from "@/components/admin/knowledge/SlmImportResultDialog";
import { SlmReviewDialog } from "@/components/admin/knowledge/SlmReviewDialog";
import { SlmSampleFormPanel } from "@/components/admin/knowledge/SlmSampleFormPanel";
import { slmSampleColumns } from "@/components/admin/knowledge/slm-sample-columns";
import { useAuth } from "@/contexts/AuthContext";
import { adminSlmSampleApi } from "@/lib/slm-sample-api";
import { CHAT_UNANSWERED_SOURCE, REVIEW_STATUSES, SAMPLE_TYPE_OPTIONS, SPLIT_OPTIONS, sampleToRequest } from "@/lib/slm-sample-format";
import { useFetched } from "@/lib/use-fetched";
import type { SaveSlmSampleRequest, SlmImportResult, SlmReviewStatus, SlmSample, SlmSampleType, SlmSplit } from "@/types/slm-sample";

const FILTER_FIELD_BG = "bg-card dark:bg-input/30";
const TOOLBAR_BUTTON_BG = "border-border bg-card text-foreground shadow-xs hover:bg-muted dark:bg-input/30 dark:hover:bg-input/50";
const PAGE_SIZE = 20;

const TYPE_FILTER_OPTIONS = [{ value: "", label: "Mọi loại mẫu" }, ...SAMPLE_TYPE_OPTIONS];
const SPLIT_FILTER_OPTIONS = [{ value: "", label: "Mọi tập" }, ...SPLIT_OPTIONS];
const SOURCE_FILTER_OPTIONS = [
  { value: "", label: "Mọi nguồn" },
  { value: "manual", label: "Nhập tay" },
  { value: "excel", label: "File Excel" },
  { value: CHAT_UNANSWERED_SOURCE, label: "Chat chưa trả lời" },
];

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
  createdBy: string;
  from: string;
  to: string;
  source: string;
  page: number;
}

const EMPTY_FILTERS: Filters = { search: "", sampleType: "", split: "", status: "", createdBy: "", from: "", to: "", source: "", page: 1 };

function errorText(error: unknown, fallback: string): string {
  return error instanceof Error ? error.message : fallback;
}

export function KnowledgeListView() {
  const { user } = useAuth();
  const isAdmin = user?.role === "admin";
  const [exporting, setExporting] = useState<"jsonl" | "xlsx" | null>(null);
  const [draft, setDraft] = useState<Filters>(EMPTY_FILTERS);
  const [applied, setApplied] = useState<Filters>(EMPTY_FILTERS);
  const [isFilterOpen, setIsFilterOpen] = useState(false);
  const [reloadCounter, setReloadCounter] = useState(0);

  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());
  const [formState, setFormState] = useState<{ sample: SlmSample | null } | null>(null);
  const [reviewStatus, setReviewStatus] = useState<SlmReviewStatus | null>(null);
  const [reviewIds, setReviewIds] = useState<string[]>([]);
  const [deleting, setDeleting] = useState<SlmSample | null>(null);
  const [deleteBusy, setDeleteBusy] = useState(false);
  const [importResult, setImportResult] = useState<SlmImportResult | null>(null);
  const [importOpen, setImportOpen] = useState(false);
  const [importSession, setImportSession] = useState(0);

  const list = useFetched(`list|${JSON.stringify(applied)}|${reloadCounter}`, () =>
    adminSlmSampleApi.list({
      reviewStatus: applied.status === "" ? undefined : (applied.status as SlmReviewStatus),
      sampleType: applied.sampleType === "" ? undefined : (applied.sampleType as SlmSampleType),
      split: applied.split === "" ? undefined : (applied.split as SlmSplit),
      search: applied.search || undefined,
      createdBy: applied.createdBy || undefined,
      from: applied.from || undefined,
      to: applied.to || undefined,
      source: applied.source || undefined,
      page: applied.page,
      pageSize: PAGE_SIZE,
    }),
  );
  const summary = useFetched(`summary|${reloadCounter}`, () => adminSlmSampleApi.summary());
  const creators = useFetched("creators", () => adminSlmSampleApi.creators());
  const creatorOptions = [{ value: "", label: "Mọi người tạo" }, ...(creators.data ?? []).map((item) => ({ value: item.userId, label: item.name }))];

  const items = list.data?.items ?? [];
  const pendingSelectedIds = items.filter((sample) => selectedIds.has(sample.id) && sample.reviewStatus === "pending").map((sample) => sample.id);
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
      const resetToPending = saved.reviewStatus !== sample.reviewStatus;
      toast.add({
        type: "success",
        title: "Đã lưu thay đổi",
        description: resetToPending ? "Nội dung đã đổi nên mẫu quay về Chờ duyệt, cần duyệt lại." : undefined,
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
    selectedIds,
    onToggle: toggleOne,
    onEdit: (sample) => setFormState({ sample }),
    onDelete: setDeleting,
    onChangeField: handleChangeField,
  });

  const activeFilterCount = [applied.sampleType, applied.split, applied.createdBy, applied.from, applied.to, applied.source].filter((value) => value !== "").length;

  const handleExport = async (format: "jsonl" | "xlsx") => {
    setExporting(format);
    try {
      await adminSlmSampleApi.exportApproved(format);
      toast.add({ type: "success", title: "Đã xuất mẫu đã duyệt", description: format === "jsonl" ? "File JSONL" : "File Excel" });
    } catch (error) {
      toast.add({ type: "error", title: "Không xuất được file", description: errorText(error, "Vui lòng thử lại sau.") });
    } finally {
      setExporting(null);
    }
  };

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
            <div className="space-y-1.5">
              <label htmlFor="slm-source-filter" className="text-sm font-medium">Nguồn</label>
              <OptionSelect id="slm-source-filter" value={draft.source} options={SOURCE_FILTER_OPTIONS} onChange={(value) => setDraft({ ...draft, source: value })} className={FILTER_FIELD_BG} />
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
          <Button type="button" variant="outline" size="sm" onClick={handleReset} className={TOOLBAR_BUTTON_BG}>
            <RotateCcwIcon />
            Xóa lọc
          </Button>
        )}
        {isAdmin && (
          <>
            <Button type="button" variant="outline" size="sm" className={TOOLBAR_BUTTON_BG} disabled={exporting !== null} onClick={() => void handleExport("jsonl")}>
              {exporting === "jsonl" ? <Spinner /> : <DownloadIcon />}
              Xuất JSONL
            </Button>
            <Button type="button" variant="outline" size="sm" className={TOOLBAR_BUTTON_BG} disabled={exporting !== null} onClick={() => void handleExport("xlsx")}>
              {exporting === "xlsx" ? <Spinner /> : <DownloadIcon />}
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
            <span className="font-medium">Đã chọn {selectedIds.size} mẫu{pendingSelectedIds.length > 0 && pendingSelectedIds.length < selectedIds.size ? `, ${pendingSelectedIds.length} chờ duyệt` : ""}</span>
            {pendingSelectedIds.length > 0 && BULK_ACTIONS.map((action) => (
              <Button key={action.status} type="button" size="sm" variant="outline" className={action.className} onClick={() => {
                  setReviewIds(pendingSelectedIds);
                  setReviewStatus(action.status);
                }}>
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

      {formState && (
        <SlmSampleFormPanel
          sample={formState.sample}
          version={reloadCounter}
          currentUserId={user?.id}
          onClose={() => setFormState(null)}
          onChanged={handlePanelChanged}
          onDelete={setDeleting}
          onReview={(sample, status) => {
            setReviewIds([sample.id]);
            setReviewStatus(status);
          }}
        />
      )}

      <SlmReviewDialog status={reviewStatus} ids={reviewIds} onClose={() => setReviewStatus(null)} onDone={handleReviewed} />
      <SlmImportDialog key={importSession} open={importOpen} onOpenChange={setImportOpen} onImported={handleImported} />
      <SlmImportResultDialog result={importResult} onClose={() => setImportResult(null)} />

      <AlertDialog open={deleting !== null} onOpenChange={(open) => !open && !deleteBusy && setDeleting(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Xóa mẫu này?</AlertDialogTitle>
            <AlertDialogDescription>Mẫu sẽ bị ẩn khỏi danh sách và không được xuất ra nữa. Lịch sử của mẫu vẫn được giữ lại.</AlertDialogDescription>
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
