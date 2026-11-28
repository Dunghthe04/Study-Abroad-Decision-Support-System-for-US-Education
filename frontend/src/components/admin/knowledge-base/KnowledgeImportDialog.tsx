"use client";

import { useEffect, useMemo, useRef, useState } from "react";
import { CircleAlertIcon, FileDownIcon, FileSpreadsheetIcon, FolderOpenIcon } from "lucide-react";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogAction,
  DialogBody,
  DialogCancel,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogIcon,
  DialogTitle,
} from "@/components/ui/dialog";
import { Spinner } from "@/components/ui/spinner";
import { Tabs, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { toast } from "@/components/ui/toast";
import {
  KnowledgeImportErrorTable,
  KnowledgeImportPreviewTable,
  SCROLL_FRAME_CLASS,
} from "@/components/admin/knowledge-base/KnowledgeImportPreviewTable";
import { adminKnowledgeBaseApi } from "@/lib/knowledge-base-api";
import type { KnowledgeImportPreview, KnowledgeImportResult } from "@/types/knowledge-base";

const MAX_FILE_BYTES = 10 * 1024 * 1024;
const XLSX_MIME = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
const TAB_TRIGGER_CLASS =
  "flex-none rounded-none border-0 border-b-2 border-transparent px-3 data-active:border-b-brand data-active:bg-transparent data-active:text-foreground data-active:shadow-none dark:data-active:border-transparent dark:data-active:border-b-brand dark:data-active:bg-transparent after:hidden";

interface Props {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onImported: (result: KnowledgeImportResult) => void;
}

function errorText(error: unknown, fallback: string): string {
  return error instanceof Error ? error.message : fallback;
}

function localFileProblem(file: File): string | null {
  if (!file.name.toLowerCase().endsWith(".xlsx")) return "Chỉ nhận file .xlsx.";
  if (file.size === 0) return "File rỗng.";
  if (file.size > MAX_FILE_BYTES) return "File vượt quá 10 MB.";
  return null;
}

export function KnowledgeImportDialog({ open, onOpenChange, onImported }: Props) {
  const [file, setFile] = useState<File | null>(null);
  const [preview, setPreview] = useState<KnowledgeImportPreview | null>(null);
  const [previewing, setPreviewing] = useState(false);
  const [problem, setProblem] = useState<string | null>(null);
  const [importing, setImporting] = useState(false);
  const [completed, setCompleted] = useState(false);
  const [downloading, setDownloading] = useState(false);
  const [tab, setTab] = useState<"rows" | "errors">("rows");

  const inputRef = useRef<HTMLInputElement>(null);
  // Chọn file mới khi file cũ còn đang đọc: hủy request cũ, bỏ kết quả về muộn
  const activeRequest = useRef<{ id: number; controller: AbortController } | null>(null);
  const requestCounter = useRef(0);

  const errors = useMemo(() => preview?.errors ?? [], [preview]);
  const messagesByRow = useMemo(() => {
    const map = new Map<number, string[]>();
    for (const item of errors) map.set(item.rowNumber, [...(map.get(item.rowNumber) ?? []), item.message]);
    return map;
  }, [errors]);

  const totalRows = preview?.totalRows ?? 0;
  const validRows = Math.max(0, totalRows - messagesByRow.size);
  const canImport = file !== null && preview !== null && !previewing && !importing && !completed && validRows > 0;

  useEffect(() => () => activeRequest.current?.controller.abort(), []);

  const loadPreview = async (picked: File) => {
    activeRequest.current?.controller.abort();
    const id = ++requestCounter.current;
    const controller = new AbortController();
    activeRequest.current = { id, controller };

    setFile(picked);
    setPreview(null);
    setProblem(null);
    setTab("rows");
    setPreviewing(true);
    try {
      const result = await adminKnowledgeBaseApi.previewXlsx(picked, controller.signal);
      if (activeRequest.current?.id === id) setPreview(result);
    } catch (error) {
      if (activeRequest.current?.id === id) setProblem(errorText(error, "Không đọc được file."));
    } finally {
      if (activeRequest.current?.id === id) {
        activeRequest.current = null;
        setPreviewing(false);
      }
    }
  };

  const handleFileChange = (event: React.ChangeEvent<HTMLInputElement>) => {
    const picked = event.target.files?.[0];
    event.target.value = "";
    if (!picked || importing || completed) return;
    const local = localFileProblem(picked);
    if (local) setProblem(local);
    else void loadPreview(picked);
  };

  const handleImport = async () => {
    if (!file || !canImport) return;
    setImporting(true);
    setProblem(null);
    try {
      const result = await adminKnowledgeBaseApi.importXlsx(file);
      setCompleted(true);
      onImported(result);
    } catch (error) {
      setProblem(errorText(error, "Không nhập được file."));
    } finally {
      setImporting(false);
    }
  };

  const handleDownload = async () => {
    setDownloading(true);
    try {
      await adminKnowledgeBaseApi.downloadTemplate();
    } catch (error) {
      toast.add({ type: "error", title: "Không tải được file mẫu", description: errorText(error, "Vui lòng thử lại sau.") });
    } finally {
      setDownloading(false);
    }
  };

  const handleOpenChange = (next: boolean) => {
    if (!next && importing) return;
    if (!next) activeRequest.current?.controller.abort();
    onOpenChange(next);
  };

  const body = () => {
    if (!file || previewing || !preview) {
      return (
        <div className={SCROLL_FRAME_CLASS} aria-busy={previewing}>
          <div className="flex flex-1 flex-col items-center justify-center gap-2 p-6 text-center text-sm text-muted-foreground">
            {previewing ? <><Spinner className="size-5" />Đang đọc file...</> : "Chọn file .xlsx có sheet knowledge_base (tối đa 10 MB). Dữ liệu chỉ được lưu khi bấm \"Nhập dữ liệu\"."}
          </div>
        </div>
      );
    }
    return (
      <>
        <div className="flex shrink-0 flex-wrap items-center justify-between gap-2">
          {errors.length > 0 ? (
            <Tabs value={tab} onValueChange={(value) => setTab(value === "errors" ? "errors" : "rows")}>
              <TabsList variant="line" className="h-auto justify-start gap-0 p-0">
                <TabsTrigger value="rows" className={TAB_TRIGGER_CLASS}>Xem trước</TabsTrigger>
                <TabsTrigger value="errors" className={TAB_TRIGGER_CLASS}>Lỗi ({errors.length})</TabsTrigger>
              </TabsList>
            </Tabs>
          ) : (
            <span className="text-sm font-medium">Xem trước</span>
          )}
          {tab === "rows" && (
            <span className="text-xs text-muted-foreground">
              {preview.rows.length < totalRows ? `Hiển thị ${preview.rows.length} / ${totalRows} dòng đầu tiên` : `${totalRows} dòng`}
            </span>
          )}
        </div>
        {tab === "errors" && errors.length > 0 ? (
          <KnowledgeImportErrorTable errors={errors} />
        ) : (
          <KnowledgeImportPreviewTable rows={preview.rows} messagesByRow={messagesByRow} />
        )}
      </>
    );
  };

  return (
    <Dialog open={open} onOpenChange={handleOpenChange} disablePointerDismissal={importing}>
      <DialogContent variant="form" size="full" className="h-[min(46rem,calc(100dvh-2rem))]">
        <DialogHeader>
          <DialogIcon tone="brand">
            <FileSpreadsheetIcon />
          </DialogIcon>
          <DialogTitle>Nhập tri thức từ Excel</DialogTitle>
          <DialogDescription>Kiểm tra dữ liệu xem trước rồi mới nhập. Dòng có id chưa có thì được thêm, id đã có thì được cập nhật.</DialogDescription>
        </DialogHeader>

        <DialogBody className="flex flex-col gap-3">
          <div className="flex shrink-0 flex-wrap items-center gap-2">
            <input ref={inputRef} type="file" accept={`.xlsx,${XLSX_MIME}`} className="hidden" onChange={handleFileChange} />
            <Button type="button" variant="outline" size="sm" disabled={importing || completed} onClick={() => inputRef.current?.click()}>
              <FolderOpenIcon /> Chọn file
            </Button>
            <Button type="button" variant="link" size="sm" loading={downloading} onClick={() => void handleDownload()}>
              <FileDownIcon /> Tải file mẫu
            </Button>
          </div>

          {file && (
            <div className="flex shrink-0 flex-wrap items-center gap-x-4 gap-y-2 text-sm">
              <span className="flex min-w-0 items-center gap-2">
                <FileSpreadsheetIcon className="size-4 shrink-0 text-muted-foreground" />
                <span className="truncate font-medium" title={file.name}>{file.name}</span>
              </span>
              {preview && (
                <span className="flex flex-wrap items-center gap-2">
                  <Badge variant="neutral">{totalRows} dòng</Badge>
                  <Badge variant="ok">{validRows} hợp lệ</Badge>
                  <Badge variant={messagesByRow.size > 0 ? "risk" : "neutral"}>{messagesByRow.size} dòng lỗi</Badge>
                </span>
              )}
            </div>
          )}

          {problem && (
            <Alert variant="destructive" className="shrink-0">
              <CircleAlertIcon />
              <AlertDescription>{problem}</AlertDescription>
            </Alert>
          )}

          <div className="flex min-h-0 min-w-0 flex-1 flex-col gap-2">{body()}</div>
        </DialogBody>

        <DialogFooter layout="pair">
          <DialogCancel disabled={importing} onClick={() => handleOpenChange(false)}>
            Đóng
          </DialogCancel>
          <DialogAction tone="primary" disabled={!canImport} loading={importing} onClick={() => void handleImport()}>
            Nhập {validRows > 0 ? `${validRows} dòng hợp lệ` : "dữ liệu"}
          </DialogAction>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}