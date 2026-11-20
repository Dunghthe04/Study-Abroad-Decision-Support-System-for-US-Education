"use client";

import { useEffect, useMemo, useRef, useState } from "react";
import { CircleAlertIcon, FileDownIcon, FileSpreadsheetIcon, FolderOpenIcon, XIcon } from "lucide-react";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Dialog, DialogClose, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Empty, EmptyDescription, EmptyHeader, EmptyMedia, EmptyTitle } from "@/components/ui/empty";
import { Spinner } from "@/components/ui/spinner";
import { Tabs, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { toast } from "@/components/ui/toast";
import {
  SCROLL_FRAME_CLASS,
  SlmImportErrorTable,
  SlmImportPreviewTable,
  importRowKey,
} from "@/components/admin/slm-samples/SlmImportPreviewTable";
import { adminSlmSampleApi } from "@/lib/slm-sample-api";
import type { SlmImportPreview, SlmImportResult } from "@/types/slm-sample";

const MAX_FILE_BYTES = 10 * 1024 * 1024;
const XLSX_MIME = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

const TAB_TRIGGER_CLASS =
  "flex-none rounded-none border-0 border-b-2 border-transparent px-3 data-active:border-b-brand data-active:bg-transparent data-active:text-foreground data-active:shadow-none dark:data-active:border-transparent dark:data-active:border-b-brand dark:data-active:bg-transparent after:hidden";

type PreviewTab = "rows" | "errors";

interface Props {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onImported: (result: SlmImportResult) => void;
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

function formatFileSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

export function SlmImportDialog({ open, onOpenChange, onImported }: Props) {
  const [file, setFile] = useState<File | null>(null);
  const [preview, setPreview] = useState<SlmImportPreview | null>(null);
  const [previewing, setPreviewing] = useState(false);
  const [previewError, setPreviewError] = useState<string | null>(null);
  const [pickError, setPickError] = useState<string | null>(null);
  const [importing, setImporting] = useState(false);
  const [importError, setImportError] = useState<string | null>(null);
  const [completed, setCompleted] = useState(false);
  const [downloading, setDownloading] = useState(false);
  const [tab, setTab] = useState<PreviewTab>("rows");

  const inputRef = useRef<HTMLInputElement>(null);
  const requestCounter = useRef(0);
  const activeRequest = useRef<{ id: number; controller: AbortController } | null>(null);
  const submitting = useRef(false);

  const errors = useMemo(() => preview?.errors ?? [], [preview]);

  const messagesByRow = useMemo(() => {
    const map = new Map<string, string[]>();
    for (const item of errors) {
      if (item.rowNumber <= 0) continue;
      const key = importRowKey(item.sheetName, item.rowNumber);
      const messages = map.get(key);
      if (messages) messages.push(item.message);
      else map.set(key, [item.message]);
    }
    return map;
  }, [errors]);

  const totalRows = preview?.totalRows ?? 0;
  const invalidRows = messagesByRow.size;
  const validRows = Math.max(0, totalRows - invalidRows);
  const canImport = file !== null && preview !== null && !previewing && !importing && !completed && validRows > 0;

  const cancelActivePreview = () => {
    activeRequest.current?.controller.abort();
    activeRequest.current = null;
  };

  useEffect(
    () => () => {
      activeRequest.current?.controller.abort();
      activeRequest.current = null;
    },
    [],
  );

  const loadPreview = async (picked: File) => {
    cancelActivePreview();
    requestCounter.current += 1;
    const requestId = requestCounter.current;
    const controller = new AbortController();
    activeRequest.current = { id: requestId, controller };

    setFile(picked);
    setPreview(null);
    setPreviewError(null);
    setPickError(null);
    setImportError(null);
    setTab("rows");
    setPreviewing(true);

    try {
      const result = await adminSlmSampleApi.previewXlsx(picked, controller.signal);
      if (activeRequest.current?.id !== requestId) return;
      setPreview(result);
    } catch (error) {
      if (activeRequest.current?.id !== requestId) return;
      setPreviewError(errorText(error, "Không đọc được file."));
    } finally {
      if (activeRequest.current?.id === requestId) {
        activeRequest.current = null;
        setPreviewing(false);
      }
    }
  };

  const handleFileChange = (event: React.ChangeEvent<HTMLInputElement>) => {
    const picked = event.target.files?.[0];
    event.target.value = "";
    if (!picked || importing || completed) return;

    const problem = localFileProblem(picked);
    if (problem) {
      setPickError(problem);
      return;
    }
    void loadPreview(picked);
  };

  const handleImport = async () => {
    if (!file || !canImport || submitting.current) return;
    submitting.current = true;
    setImporting(true);
    setImportError(null);
    try {
      const result = await adminSlmSampleApi.importXlsx(file);
      setCompleted(true);
      onImported(result);
    } catch (error) {
      setImportError(errorText(error, "Không nhập được file."));
    } finally {
      submitting.current = false;
      setImporting(false);
    }
  };

  const handleDownload = async () => {
    setDownloading(true);
    try {
      await adminSlmSampleApi.downloadTemplate();
    } catch (error) {
      toast.add({ type: "error", title: "Không tải được file mẫu", description: errorText(error, "Vui lòng thử lại sau.") });
    } finally {
      setDownloading(false);
    }
  };

  const handleOpenChange = (next: boolean) => {
    if (!next && importing) return;
    if (!next) cancelActivePreview();
    onOpenChange(next);
  };

  const renderBody = () => {
    if (!file) {
      return (
        <div className={SCROLL_FRAME_CLASS}>
          <Empty className="border-0">
            <EmptyHeader>
              <EmptyMedia variant="icon">
                <FileSpreadsheetIcon />
              </EmptyMedia>
              <EmptyTitle>Chưa chọn file</EmptyTitle>
              <EmptyDescription>
                Bấm &quot;Chọn file&quot; để chọn một file .xlsx (tối đa 10 MB). Dữ liệu chỉ được đọc để xem trước, chưa lưu cho đến khi bạn bấm &quot;Nhập dữ liệu&quot;. Tải file mẫu để xem đúng định dạng cột.
              </EmptyDescription>
            </EmptyHeader>
          </Empty>
        </div>
      );
    }

    if (previewing) {
      return (
        <div className={SCROLL_FRAME_CLASS} aria-busy="true">
          <div className="flex flex-1 flex-col items-center justify-center gap-2 text-sm text-muted-foreground">
            <Spinner className="size-5" />
            Đang đọc file...
          </div>
        </div>
      );
    }

    if (!preview) {
      return (
        <div className={SCROLL_FRAME_CLASS}>
          <div className="flex flex-1 items-center justify-center p-6 text-sm text-muted-foreground">
            Không có dữ liệu xem trước. Hãy chọn lại file.
          </div>
        </div>
      );
    }

    const shownRows = preview.rows.length;
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
              {shownRows < totalRows ? `Hiển thị ${shownRows} / ${totalRows} dòng đầu tiên` : `${totalRows} dòng`}
            </span>
          )}
        </div>
        {tab === "errors" && errors.length > 0 ? (
          <SlmImportErrorTable errors={errors} />
        ) : shownRows === 0 ? (
          <div className={SCROLL_FRAME_CLASS}>
            <div className="flex flex-1 items-center justify-center p-6 text-sm text-muted-foreground">
              File không có dòng dữ liệu nào.
            </div>
          </div>
        ) : (
          <SlmImportPreviewTable rows={preview.rows} messagesByRow={messagesByRow} />
        )}
      </>
    );
  };

  return (
    <Dialog open={open} onOpenChange={handleOpenChange} disablePointerDismissal={importing}>
      <DialogContent
        showCloseButton={false}
        className="flex h-[min(46rem,calc(100dvh-2rem))] flex-col gap-3 sm:max-w-[min(72rem,calc(100vw-3rem))]"
      >
        <DialogHeader className="shrink-0 flex-row items-start justify-between gap-3">
          <div className="flex min-w-0 flex-col gap-1.5">
            <DialogTitle className="text-lg">Nhập Excel</DialogTitle>
            <DialogDescription>Chọn một file .xlsx, kiểm tra dữ liệu xem trước rồi mới nhập vào danh sách mẫu.</DialogDescription>
          </div>
          <DialogClose disabled={importing} render={<Button type="button" variant="ghost" size="icon-sm" aria-label="Đóng" />}>
            <XIcon />
          </DialogClose>
        </DialogHeader>

        {file && (
          <div className="flex shrink-0 flex-wrap items-center gap-x-4 gap-y-2 text-sm">
            <div className="flex min-w-0 items-center gap-2">
              <FileSpreadsheetIcon className="size-4 shrink-0 text-muted-foreground" />
              <span className="truncate font-medium" title={file.name}>{file.name}</span>
              <span className="shrink-0 text-muted-foreground">{formatFileSize(file.size)}</span>
            </div>
            {preview && (
              <div className="flex flex-wrap items-center gap-2">
                <Badge variant="neutral">{totalRows} dòng</Badge>
                <Badge variant="ok">{validRows} hợp lệ</Badge>
                <Badge variant={invalidRows > 0 ? "risk" : "neutral"}>{invalidRows} dòng lỗi</Badge>
              </div>
            )}
          </div>
        )}

        {pickError && (
          <Alert variant="destructive" className="shrink-0">
            <CircleAlertIcon />
            <AlertDescription>{pickError}</AlertDescription>
          </Alert>
        )}
        {previewError && (
          <Alert variant="destructive" className="shrink-0">
            <CircleAlertIcon />
            <AlertDescription>{previewError}</AlertDescription>
          </Alert>
        )}
        {preview && validRows === 0 && (
          <Alert variant="destructive" className="shrink-0">
            <CircleAlertIcon />
            <AlertDescription>Không có dòng hợp lệ để nhập.</AlertDescription>
          </Alert>
        )}
        {preview && validRows > 0 && errors.length > 0 && (
          <Alert variant="warning" className="shrink-0">
            <CircleAlertIcon />
            <AlertDescription>Có {errors.length} lỗi. Các dòng lỗi sẽ bị bỏ qua, chỉ {validRows} dòng hợp lệ được nhập.</AlertDescription>
          </Alert>
        )}
        {importError && (
          <Alert variant="destructive" className="shrink-0">
            <CircleAlertIcon />
            <AlertDescription>{importError}</AlertDescription>
          </Alert>
        )}

        <div className="flex min-h-0 min-w-0 flex-1 flex-col gap-2">{renderBody()}</div>

        <DialogFooter className="shrink-0 flex-col sm:flex-row sm:items-center sm:justify-between">
          <div className="flex flex-wrap items-center gap-2">
            <input
              ref={inputRef}
              type="file"
              accept={`.xlsx,${XLSX_MIME}`}
              className="hidden"
              onChange={handleFileChange}
            />
            <Button type="button" variant="outline" disabled={importing || completed} onClick={() => inputRef.current?.click()}>
              <FolderOpenIcon />
              Chọn file
            </Button>
            <Button type="button" variant="link" disabled={downloading} onClick={() => void handleDownload()}>
              {downloading ? <Spinner /> : <FileDownIcon />}
              Tải file mẫu
            </Button>
          </div>
          <Button type="button" disabled={!canImport} onClick={() => void handleImport()}>
            {importing && <Spinner />}
            Nhập dữ liệu
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
