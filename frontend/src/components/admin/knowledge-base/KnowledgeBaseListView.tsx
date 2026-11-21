"use client";

import { useState } from "react";
import { CheckIcon, FileDownIcon, FileInputIcon, FilterIcon, PlusIcon, RotateCcwIcon, SearchIcon, WrenchIcon, XIcon } from "lucide-react";
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
import { Tabs, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { toast } from "@/components/ui/toast";
import { DataTable, type DataTableToolbarSlots } from "@/components/admin/shared/DataTable";
import { TablePagination } from "@/components/admin/shared/TablePagination";
import { OptionSelect } from "@/components/admin/universities/OptionSelect";
import { KnowledgeEntryFormPanel } from "@/components/admin/knowledge-base/KnowledgeEntryFormPanel";
import { KnowledgeImportDialog } from "@/components/admin/knowledge-base/KnowledgeImportDialog";
import { KnowledgeImportResultDialog } from "@/components/admin/knowledge-base/KnowledgeImportResultDialog";
import { KnowledgeReviewDialog } from "@/components/admin/knowledge-base/KnowledgeReviewDialog";
import { knowledgeBaseColumns } from "@/components/admin/knowledge-base/knowledge-base-columns";
import { useAuth } from "@/contexts/AuthContext";
import { adminKnowledgeBaseApi, duplicateIdOf } from "@/lib/knowledge-base-api";
import { LABEL_OPTIONS, LEVEL_OPTIONS, ORIGIN_LABELS, REVIEW_STATUSES, SORT_OPTIONS } from "@/lib/knowledge-base-format";
import { useFetched } from "@/lib/use-fetched";
import type { KnowledgeEntry, KnowledgeImportResult, KnowledgeLevel, KnowledgeOrigin, KnowledgeReviewStatus, KnowledgeSort } from "@/types/knowledge-base";

const FILTER_FIELD_BG = "bg-card dark:bg-input/30";
const TOOLBAR_BUTTON_BG = "border-border bg-card text-foreground shadow-xs hover:bg-muted dark:bg-input/30 dark:hover:bg-input/50";
const TAB_TRIGGER_CLASS =
  "flex-none rounded-none border-0 border-b-2 border-transparent px-3 data-active:border-b-brand data-active:bg-transparent data-active:text-foreground data-active:shadow-none dark:data-active:border-transparent dark:data-active:border-b-brand dark:data-active:bg-transparent after:hidden";
const PAGE_SIZE = 20;
const DELETED_TAB = "deleted";

const LABEL_FILTER_OPTIONS = [{ value: "", label: "Mọi nhãn" }, ...LABEL_OPTIONS];
const LEVEL_FILTER_OPTIONS = [{ value: "", label: "Mọi bậc học" }, ...LEVEL_OPTIONS];
const ORIGIN_FILTER_OPTIONS = [{ value: "", label: "Mọi nguồn gốc" }, ...Object.entries(ORIGIN_LABELS).map(([value, label]) => ({ value, label }))];

const BULK_ACTIONS: { status: KnowledgeReviewStatus; label: string; icon: typeof CheckIcon; className: string }[] = [
  { status: "approved", label: "Duyệt", icon: CheckIcon, className: "border-ok/20 bg-ok-wash text-ok hover:bg-ok-wash/70" },
  { status: "rejected", label: "Từ chối", icon: XIcon, className: "border-risk/20 bg-risk-wash text-risk hover:bg-risk-wash/70" },
  { status: "needs_fix", label: "Yêu cầu sửa", icon: WrenchIcon, className: "border-warn/20 bg-warn-wash text-warn hover:bg-warn-wash/70" },
];

interface Filters {
  search: string;
  label: string;
  level: string;
  source: string;
  origin: string;
  createdBy: string;
  sort: string;
  tab: string; // "" = tất cả, một trạng thái, hoặc "deleted"
  page: number;
}

const EMPTY_FILTERS: Filters = { search: "", label: "", level: "", source: "", origin: "", createdBy: "", sort: "newest", tab: "", page: 1 };

function errorText(error: unknown, fallback: string): string {
  return error instanceof Error ? error.message : fallback;
}

export function KnowledgeBaseListView() {
  const { user } = useAuth();
  const [draft, setDraft] = useState<Filters>(EMPTY_FILTERS);
  const [applied, setApplied] = useState<Filters>(EMPTY_FILTERS);
  const [isFilterOpen, setIsFilterOpen] = useState(false);
  const [reloadCounter, setReloadCounter] = useState(0);
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());
  const [formState, setFormState] = useState<{ entry: KnowledgeEntry | null } | null>(null);
  const [reviewStatus, setReviewStatus] = useState<KnowledgeReviewStatus | null>(null);
  const [reviewIds, setReviewIds] = useState<string[]>([]);
  const [deleting, setDeleting] = useState<KnowledgeEntry | null>(null);
  const [deleteBusy, setDeleteBusy] = useState(false);
  const [importOpen, setImportOpen] = useState(false);
  const [importSession, setImportSession] = useState(0);
  const [importResult, setImportResult] = useState<KnowledgeImportResult | null>(null);

  const isTrash = applied.tab === DELETED_TAB;

  // key gồm mọi bộ lọc đã áp dụng + bộ đếm reload: đổi bất kỳ thứ gì thì useFetched tải lại
  const list = useFetched(`kb-list|${JSON.stringify(applied)}|${reloadCounter}`, () =>
    adminKnowledgeBaseApi.list({
      search: applied.search || undefined,
      label: applied.label || undefined,
      level: (applied.level || undefined) as KnowledgeLevel | undefined,
      source: applied.source || undefined,
      origin: (applied.origin || undefined) as KnowledgeOrigin | undefined,
      createdBy: applied.createdBy || undefined,
      reviewStatus: (isTrash || applied.tab === "" ? undefined : applied.tab) as KnowledgeReviewStatus | undefined,
      deleted: isTrash,
      sort: applied.sort as KnowledgeSort,
      page: applied.page,
      pageSize: PAGE_SIZE,
    }),
  );
  const creators = useFetched("kb-creators", () => adminKnowledgeBaseApi.creators());
  const creatorOptions = [{ value: "", label: "Mọi người tạo" }, ...(creators.data ?? []).map((item) => ({ value: item.userId, label: item.name }))];

  const items = list.data?.items ?? [];
  const pendingSelectedIds = items.filter((entry) => selectedIds.has(entry.id) && entry.reviewStatus === "pending").map((entry) => entry.id);
  const reload = () => setReloadCounter((current) => current + 1);

  const apply = (next: Filters) => {
    setApplied(next);
    setSelectedIds(new Set());
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
    const { label, level, source, origin, createdBy, sort } = draft;
    apply({ ...applied, label, level, source: source.trim(), origin, createdBy, sort, page: 1 });
    setIsFilterOpen(false);
  };

  const handleReset = () => {
    setDraft({ ...EMPTY_FILTERS, tab: applied.tab });
    apply({ ...EMPTY_FILTERS, tab: applied.tab });
    setIsFilterOpen(false);
  };

  const openById = async (id: string) => {
    try {
      const detail = await adminKnowledgeBaseApi.get(id);
      setFormState({ entry: detail.entry });
    } catch (error) {
      toast.add({ type: "error", title: "Không mở được tri thức", description: errorText(error, "Vui lòng thử lại sau.") });
    }
  };

  const restore = async (entry: KnowledgeEntry) => {
    try {
      const restored = await adminKnowledgeBaseApi.restore(entry.id);
      toast.add({ type: "success", title: `Đã khôi phục tri thức #${restored.entryNo}` });
      if (formState?.entry?.id === entry.id) setFormState({ entry: restored });
      reload();
    } catch (error) {
      const existingId = duplicateIdOf(error);
      toast.add({ type: "error", title: "Không khôi phục được", description: errorText(error, "Vui lòng thử lại sau.") });
      if (existingId) void openById(existingId);
    }
  };

  const confirmDelete = async () => {
    if (!deleting) return;
    setDeleteBusy(true);
    try {
      await adminKnowledgeBaseApi.remove(deleting.id);
      toggleOne(deleting.id, false);
      if (formState?.entry?.id === deleting.id) setFormState(null);
      if (items.length === 1 && applied.page > 1) setApplied({ ...applied, page: applied.page - 1 });
      toast.add({ type: "success", title: "Đã xóa tri thức", description: "Có thể khôi phục trong mục Đã xóa." });
      reload();
    } catch (error) {
      toast.add({ type: "error", title: "Không xóa được tri thức", description: errorText(error, "Vui lòng thử lại sau.") });
    } finally {
      setDeleting(null);
      setDeleteBusy(false);
    }
  };

  const handleTemplate = async () => {
    try {
      await adminKnowledgeBaseApi.downloadTemplate();
    } catch (error) {
      toast.add({ type: "error", title: "Không tải được file mẫu", description: errorText(error, "Vui lòng thử lại sau.") });
    }
  };

  const columns = knowledgeBaseColumns({
    selectedIds,
    onToggle: toggleOne,
    onOpen: (entry) => setFormState({ entry }),
    onDelete: setDeleting,
    onRestore: (entry) => void restore(entry),
  });

  const activeFilterCount = [applied.label, applied.level, applied.source, applied.origin, applied.createdBy].filter((value) => value !== "").length
    + (applied.sort !== "newest" ? 1 : 0);

  const field = (id: string, label: string, control: React.ReactNode) => (
    <div className="space-y-1.5">
      <label htmlFor={id} className="text-sm font-medium">{label}</label>
      {control}
    </div>
  );

  const toolbar: DataTableToolbarSlots = {
    filters: (
      <Popover open={isFilterOpen} onOpenChange={(open) => { if (open) setDraft({ ...applied, search: draft.search }); setIsFilterOpen(open); }}>
        <PopoverTrigger render={<Button variant="outline" size="sm" className={TOOLBAR_BUTTON_BG} />}>
          <FilterIcon />
          Bộ lọc{activeFilterCount > 0 && ` (${activeFilterCount})`}
        </PopoverTrigger>
        <PopoverContent align="start" className="w-80 max-w-[calc(100vw-2rem)] p-4">
          <PopoverTitle>Bộ lọc tri thức</PopoverTitle>
          <form onSubmit={handleFilterSubmit} className="flex flex-col gap-4">
            {field("kb-label-filter", "Nhãn", <OptionSelect id="kb-label-filter" value={draft.label} options={LABEL_FILTER_OPTIONS} onChange={(value) => setDraft({ ...draft, label: value })} className={FILTER_FIELD_BG} />)}
            {field("kb-level-filter", "Bậc học", <OptionSelect id="kb-level-filter" value={draft.level} options={LEVEL_FILTER_OPTIONS} onChange={(value) => setDraft({ ...draft, level: value })} className={FILTER_FIELD_BG} />)}
            {field("kb-source-filter", "Nguồn có chứa", <Input id="kb-source-filter" value={draft.source} placeholder="EduPath, EXP-KB..." onChange={(event) => setDraft({ ...draft, source: event.target.value })} className={FILTER_FIELD_BG} />)}
            {field("kb-origin-filter", "Nguồn gốc", <OptionSelect id="kb-origin-filter" value={draft.origin} options={ORIGIN_FILTER_OPTIONS} onChange={(value) => setDraft({ ...draft, origin: value })} className={FILTER_FIELD_BG} />)}
            {field("kb-creator-filter", "Người tạo", <OptionSelect id="kb-creator-filter" value={draft.createdBy} options={creatorOptions} onChange={(value) => setDraft({ ...draft, createdBy: value })} className={FILTER_FIELD_BG} />)}
            {field("kb-sort", "Sắp xếp", <OptionSelect id="kb-sort" value={draft.sort} options={SORT_OPTIONS} onChange={(value) => setDraft({ ...draft, sort: value })} className={FILTER_FIELD_BG} />)}
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
          value={draft.search}
          onChange={(event) => setDraft({ ...draft, search: event.target.value })}
          placeholder="Tìm trong câu hỏi, câu trả lời"
          aria-label="Tìm trong câu hỏi, câu trả lời"
          className={`pr-10 ${FILTER_FIELD_BG}`}
        />
        <Button type="submit" variant="ghost" size="icon-sm" aria-label="Tìm" className="absolute top-1/2 right-1 -translate-y-1/2">
          <SearchIcon />
        </Button>
      </form>
    ),
    actions: (
      <div className="flex flex-wrap items-center gap-2">
        {(applied.search || activeFilterCount > 0) && (
          <Button type="button" variant="outline" size="sm" onClick={handleReset} className={TOOLBAR_BUTTON_BG}>
            <RotateCcwIcon /> Xóa lọc
          </Button>
        )}
        <Button type="button" variant="outline" size="sm" className={TOOLBAR_BUTTON_BG} onClick={() => void handleTemplate()}>
          <FileDownIcon /> Tải file mẫu
        </Button>
        <Button type="button" variant="outline" size="sm" className={TOOLBAR_BUTTON_BG} onClick={() => { setImportSession((n) => n + 1); setImportOpen(true); }}>
          <FileInputIcon /> Nhập từ Excel
        </Button>
        <Button type="button" size="sm" onClick={() => setFormState({ entry: null })}>
          <PlusIcon /> Thêm tri thức
        </Button>
      </div>
    ),
  };

  return (
    <div className="flex min-h-0 flex-1 flex-col gap-6 lg:flex-row">
      <div className="flex min-h-0 min-w-0 flex-1 flex-col gap-3">
        <Tabs value={applied.tab === "" ? "all" : applied.tab} onValueChange={(value) => apply({ ...applied, tab: value === "all" ? "" : String(value), page: 1 })}>
          <TabsList variant="line" className="h-auto w-full justify-start gap-0 overflow-x-auto border-b border-border p-0">
            <TabsTrigger value="all" className={TAB_TRIGGER_CLASS}>Tất cả</TabsTrigger>
            {REVIEW_STATUSES.map((item) => (
              <TabsTrigger key={item.id} value={item.id} className={TAB_TRIGGER_CLASS}>{item.label}</TabsTrigger>
            ))}
            <TabsTrigger value={DELETED_TAB} className={TAB_TRIGGER_CLASS}>Đã xóa</TabsTrigger>
          </TabsList>
        </Tabs>

        {selectedIds.size > 0 && (
          <div className="flex flex-wrap items-center gap-2 rounded-md border border-border bg-muted/60 px-3 py-2 text-sm">
            <span className="font-medium">Đã chọn {selectedIds.size} tri thức{pendingSelectedIds.length < selectedIds.size ? `, ${pendingSelectedIds.length} chờ duyệt` : ""}</span>
            {pendingSelectedIds.length > 0 && BULK_ACTIONS.map((action) => (
              <Button key={action.status} type="button" size="sm" variant="outline" className={action.className} onClick={() => { setReviewIds(pendingSelectedIds); setReviewStatus(action.status); }}>
                <action.icon /> {action.label}
              </Button>
            ))}
            <Button type="button" size="sm" variant="ghost" onClick={() => setSelectedIds(new Set())}>Bỏ chọn</Button>
          </div>
        )}

        <DataTable
          columns={columns}
          items={items}
          getRowId={(entry) => entry.id}
          selectedRowId={formState?.entry?.id}
          onRowSelect={(entry) => setFormState({ entry })}
          isLoading={list.loading && !list.data}
          error={list.error}
          emptyMessage={isTrash ? "Không có tri thức nào đã xóa." : "Không có tri thức nào khớp bộ lọc."}
          label="Danh sách kho tri thức"
          toolbar={toolbar}
          footer={
            <TablePagination
              page={list.data?.page ?? applied.page}
              totalPages={list.data?.totalPages ?? 0}
              totalCount={list.data?.totalCount}
              itemLabel="tri thức"
              disabled={list.loading || !!list.error}
              onPageChange={(page) => apply({ ...applied, page })}
            />
          }
        />
      </div>

      {formState && (
        <KnowledgeEntryFormPanel
          entry={formState.entry}
          version={reloadCounter}
          currentUserId={user?.id}
          onClose={() => setFormState(null)}
          onChanged={(entry, isNew) => {
            setFormState(isNew ? null : { entry });
            reload();
          }}
          onDelete={setDeleting}
          onRestore={(entry) => void restore(entry)}
          onReview={(entry, status) => {
            setReviewIds([entry.id]);
            setReviewStatus(status);
          }}
          onOpen={(id) => void openById(id)}
        />
      )}

      <KnowledgeReviewDialog
        status={reviewStatus}
        ids={reviewIds}
        onClose={() => setReviewStatus(null)}
        onDone={(message) => {
          setReviewStatus(null);
          setSelectedIds(new Set());
          toast.add({ type: "success", title: message });
          reload();
        }}
      />
      <KnowledgeImportDialog
        key={importSession}
        open={importOpen}
        onOpenChange={setImportOpen}
        onImported={(result) => {
          setImportOpen(false);
          setImportResult(result);
          reload();
        }}
      />
      <KnowledgeImportResultDialog result={importResult} onClose={() => setImportResult(null)} />

      <AlertDialog open={deleting !== null} onOpenChange={(open) => !open && !deleteBusy && setDeleting(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Xóa tri thức này?</AlertDialogTitle>
            <AlertDialogDescription>Có thể khôi phục trong mục Đã xóa. Lịch sử của tri thức vẫn được giữ lại.</AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel disabled={deleteBusy}>Hủy</AlertDialogCancel>
            <AlertDialogAction onClick={() => void confirmDelete()} loading={deleteBusy}>
              Xóa
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}