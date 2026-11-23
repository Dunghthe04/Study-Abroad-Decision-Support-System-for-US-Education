"use client";

import { useState } from "react";
import { FileDownIcon, FileInputIcon, FilterIcon, PlusIcon, RotateCcwIcon, SearchIcon } from "lucide-react";
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
import { toast } from "@/components/ui/toast";
import { DataTable, type DataTableToolbarSlots } from "@/components/admin/shared/DataTable";
import { TablePagination } from "@/components/admin/shared/TablePagination";
import { OptionSelect } from "@/components/admin/universities/OptionSelect";
import { KnowledgeEntryFormPanel } from "@/components/admin/knowledge-base/KnowledgeEntryFormPanel";
import { KnowledgeImportDialog } from "@/components/admin/knowledge-base/KnowledgeImportDialog";
import { KnowledgeImportResultDialog } from "@/components/admin/knowledge-base/KnowledgeImportResultDialog";
import { knowledgeBaseColumns } from "@/components/admin/knowledge-base/knowledge-base-columns";
import { adminKnowledgeBaseApi } from "@/lib/knowledge-base-api";
import { DOC_TYPE_OPTIONS, LABEL_OPTIONS, SORT_OPTIONS } from "@/lib/knowledge-base-format";
import { useFetched } from "@/lib/use-fetched";
import type { KnowledgeDocType, KnowledgeEntry, KnowledgeImportResult, KnowledgeSort } from "@/types/knowledge-base";

const FILTER_FIELD_BG = "bg-card dark:bg-input/30";
const TOOLBAR_BUTTON_BG = "border-border bg-card text-foreground shadow-xs hover:bg-muted dark:bg-input/30 dark:hover:bg-input/50";
const PAGE_SIZE = 20;

const LABEL_FILTER_OPTIONS = [{ value: "", label: "Mọi nhãn" }, ...LABEL_OPTIONS];
const DOC_TYPE_FILTER_OPTIONS = [{ value: "", label: "Mọi loại" }, ...DOC_TYPE_OPTIONS];

interface Filters {
  search: string;
  docType: string;
  label: string;
  topicCode: string;
  sort: string;
  page: number;
}

const EMPTY_FILTERS: Filters = { search: "", docType: "", label: "", topicCode: "", sort: "newest", page: 1 };

function errorText(error: unknown, fallback: string): string {
  return error instanceof Error ? error.message : fallback;
}

export function KnowledgeBaseListView() {
  const [draft, setDraft] = useState<Filters>(EMPTY_FILTERS);
  const [applied, setApplied] = useState<Filters>(EMPTY_FILTERS);
  const [isFilterOpen, setIsFilterOpen] = useState(false);
  const [reloadCounter, setReloadCounter] = useState(0);
  const [formState, setFormState] = useState<{ entry: KnowledgeEntry | null } | null>(null);
  const [deleting, setDeleting] = useState<KnowledgeEntry | null>(null);
  const [deleteBusy, setDeleteBusy] = useState(false);
  const [importOpen, setImportOpen] = useState(false);
  const [importSession, setImportSession] = useState(0);
  const [importResult, setImportResult] = useState<KnowledgeImportResult | null>(null);

  // key gồm mọi bộ lọc đã áp dụng + bộ đếm reload: đổi bất kỳ thứ gì thì useFetched tải lại
  const list = useFetched(`kb-list|${JSON.stringify(applied)}|${reloadCounter}`, () =>
    adminKnowledgeBaseApi.list({
      search: applied.search || undefined,
      docType: (applied.docType || undefined) as KnowledgeDocType | undefined,
      label: applied.label || undefined,
      topicCode: applied.topicCode || undefined,
      sort: applied.sort as KnowledgeSort,
      page: applied.page,
      pageSize: PAGE_SIZE,
    }),
  );

  const items = list.data?.items ?? [];
  const reload = () => setReloadCounter((current) => current + 1);

  const handleSearch = (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setApplied({ ...applied, search: draft.search.trim(), page: 1 });
  };

  const handleFilterSubmit = (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const { docType, label, topicCode, sort } = draft;
    setApplied({ ...applied, docType, label, topicCode: topicCode.trim(), sort, page: 1 });
    setIsFilterOpen(false);
  };

  const handleReset = () => {
    setDraft(EMPTY_FILTERS);
    setApplied(EMPTY_FILTERS);
    setIsFilterOpen(false);
  };

  const openById = async (id: string) => {
    try {
      setFormState({ entry: await adminKnowledgeBaseApi.get(id) });
    } catch (error) {
      toast.add({ type: "error", title: "Không mở được tri thức", description: errorText(error, "Vui lòng thử lại sau.") });
    }
  };

  const confirmDelete = async () => {
    if (!deleting) return;
    setDeleteBusy(true);
    try {
      await adminKnowledgeBaseApi.remove(deleting.id);
      if (formState?.entry?.id === deleting.id) setFormState(null);
      if (items.length === 1 && applied.page > 1) setApplied({ ...applied, page: applied.page - 1 });
      toast.add({ type: "success", title: "Đã xóa tri thức" });
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

  const columns = knowledgeBaseColumns({ onOpen: (entry) => setFormState({ entry }), onDelete: setDeleting });

  const activeFilterCount = [applied.docType, applied.label, applied.topicCode].filter((value) => value !== "").length
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
            {field("kb-doc-type-filter", "Loại nội dung", <OptionSelect id="kb-doc-type-filter" value={draft.docType} options={DOC_TYPE_FILTER_OPTIONS} onChange={(value) => setDraft({ ...draft, docType: value })} className={FILTER_FIELD_BG} />)}
            {field("kb-label-filter", "Nhãn", <OptionSelect id="kb-label-filter" value={draft.label} options={LABEL_FILTER_OPTIONS} onChange={(value) => setDraft({ ...draft, label: value })} className={FILTER_FIELD_BG} />)}
            {field("kb-topic-filter", "Mã chủ đề", <Input id="kb-topic-filter" value={draft.topicCode} placeholder="I-20, DS-160..." onChange={(event) => setDraft({ ...draft, topicCode: event.target.value })} className={FILTER_FIELD_BG} />)}
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
        <DataTable
          columns={columns}
          items={items}
          getRowId={(entry) => entry.id}
          selectedRowId={formState?.entry?.id}
          onRowSelect={(entry) => setFormState({ entry })}
          isLoading={list.loading && !list.data}
          error={list.error}
          emptyMessage="Không có tri thức nào khớp bộ lọc."
          label="Danh sách kho tri thức"
          toolbar={toolbar}
          footer={
            <TablePagination
              page={list.data?.page ?? applied.page}
              totalPages={list.data?.totalPages ?? 0}
              totalCount={list.data?.totalCount}
              itemLabel="tri thức"
              disabled={list.loading || !!list.error}
              onPageChange={(page) => setApplied({ ...applied, page })}
            />
          }
        />
      </div>

      {formState && (
        <KnowledgeEntryFormPanel
          entry={formState.entry}
          onClose={() => setFormState(null)}
          onChanged={(entry, isNew) => {
            setFormState(isNew ? null : { entry });
            reload();
          }}
          onDelete={setDeleting}
          onOpen={(id) => void openById(id)}
        />
      )}

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
            <AlertDialogDescription>Tri thức bị xóa hẳn và chat không còn dùng nó nữa.</AlertDialogDescription>
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
