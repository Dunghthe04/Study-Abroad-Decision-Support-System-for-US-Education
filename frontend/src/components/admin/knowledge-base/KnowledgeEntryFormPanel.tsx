"use client";

import { useState } from "react";
import { HistoryIcon, RotateCcwIcon, SaveIcon, Trash2Icon } from "lucide-react";
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
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { toast } from "@/components/ui/toast";
import { DetailPanel } from "@/components/admin/shared/DetailPanel";
import { BlockBody, EditableTable, RowsTable, TableFrame, type TableEditState, type TableRow } from "@/components/admin/shared/EditableTable";
import { OptionSelect } from "@/components/admin/universities/OptionSelect";
import { adminKnowledgeBaseApi, duplicateIdOf } from "@/lib/knowledge-base-api";
import { DOC_TYPE_OPTIONS, docTypeName, formatDateTime, isProcedure, LABEL_OPTIONS, labelName } from "@/lib/knowledge-base-format";
import { versionActionLabel } from "@/lib/slm-sample-format";
import { useFetched } from "@/lib/use-fetched";
import type { KnowledgeDocType, KnowledgeEntry, KnowledgeEntryVersion, SaveKnowledgeEntryRequest } from "@/types/knowledge-base";

const MAX_QUESTION = 500;
const MAX_ANSWER = 8000;
const MAX_SOURCE = 2000;
const MAX_SOURCE_URL = 4000;
const MAX_TOPIC_CODE = 64;
const MAX_NOTE = 4000;

const Missing = () => (
  <span title="Chưa có dữ liệu" aria-label="Chưa có dữ liệu" className="text-muted-foreground">—</span>
);

interface FormState {
  entryNo: string;
  docType: string;
  label: string;
  question: string;
  answer: string;
  source: string;
  sourceUrl: string;
  retrievedAt: string;
  topicCode: string;
  note: string;
}

function toForm(entry: KnowledgeEntry | null): FormState {
  return {
    entryNo: entry ? String(entry.entryNo) : "",
    docType: entry?.docType ?? "faq",
    label: entry?.label ?? "",
    question: entry?.question ?? "",
    answer: entry?.answer ?? "",
    source: entry?.source ?? "",
    sourceUrl: entry?.sourceUrl ?? "",
    retrievedAt: entry?.retrievedAt ?? "",
    topicCode: entry?.topicCode ?? "",
    note: entry?.note ?? "",
  };
}

const orNull = (value: string) => (value.trim() === "" ? null : value.trim());

function toPayload(form: FormState): SaveKnowledgeEntryRequest {
  return {
    entryNo: form.entryNo.trim() === "" ? null : Number(form.entryNo),
    docType: form.docType as KnowledgeDocType,
    label: form.label,
    question: form.question.trim(),
    answer: form.answer.trim(),
    source: orNull(form.source),
    sourceUrl: orNull(form.sourceUrl),
    retrievedAt: form.retrievedAt,
    topicCode: orNull(form.topicCode),
    note: orNull(form.note),
  };
}

/** Kiểm tra sớm ở trình duyệt; backend vẫn kiểm tra lại toàn bộ. */
function problemOf(form: FormState): string | null {
  if (form.label === "") return "Vui lòng chọn nhãn.";
  if (form.question.trim() === "") return "Vui lòng nhập câu hỏi.";
  if (form.answer.trim() === "") return "Vui lòng nhập câu trả lời.";
  if (form.source.trim() === "" && form.sourceUrl.trim() === "") return "Cần nhập ít nhất một trong hai: nguồn hoặc link nguồn.";
  if (form.retrievedAt === "") return "Vui lòng nhập ngày thu thập.";
  if (isProcedure(form.docType) && form.topicCode.trim() === "") return "Dòng thủ tục (proc_*) bắt buộc có mã chủ đề.";
  if (form.entryNo.trim() !== "" && !/^[1-9]\d{0,6}$/.test(form.entryNo.trim())) return "Mã số phải là số nguyên dương.";
  return null;
}

function errorText(error: unknown, fallback: string): string {
  return error instanceof Error ? error.message : fallback;
}

interface Props {
  entry: KnowledgeEntry | null;
  reloadKey: number;
  onClose: () => void;
  onChanged: (entry: KnowledgeEntry, isNew: boolean) => void;
  onDelete: (entry: KnowledgeEntry) => void;
  onRestore: (entry: KnowledgeEntry) => void;
  onOpen: (id: string) => void;
}

export function KnowledgeEntryFormPanel(props: Props) {
  // key: đổi mục thì dựng lại form từ đầu, không mang state của mục trước
  return <KnowledgeEntryForm key={props.entry?.id ?? "new"} {...props} />;
}

function KnowledgeEntryForm({ entry, reloadKey, onClose, onChanged, onDelete, onRestore, onOpen }: Props) {
  const versionsResult = useFetched(`kb-versions|${entry?.id ?? "new"}|${reloadKey}`, () =>
    entry ? adminKnowledgeBaseApi.versions(entry.id) : Promise.resolve([]),
  );
  const versions = versionsResult.data ?? [];

  const [form, setForm] = useState<FormState>(() => toForm(entry));
  const [editing, setEditing] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [duplicateId, setDuplicateId] = useState<string | null>(null);
  const [inspecting, setInspecting] = useState<KnowledgeEntryVersion | null>(null);
  const [restoringVersion, setRestoringVersion] = useState(false);

  const isCreate = entry === null;
  const isDeleted = entry?.deletedAt != null;
  const hasChanges = JSON.stringify(form) !== JSON.stringify(toForm(entry));
  const patch = (change: Partial<FormState>) => setForm((current) => ({ ...current, ...change }));

  const warnVectorIfNeeded = (saved: KnowledgeEntry) => {
    if (saved.vectorWarning) {
      toast.add({ type: "warning", title: "Chưa tạo được vector ngay", description: saved.vectorWarning });
    }
  };

  const save = async () => {
    const problem = problemOf(form);
    setDuplicateId(null);
    setError(problem);
    if (problem) return;

    setSaving(true);
    try {
      if (entry) {
        const saved = await adminKnowledgeBaseApi.update(entry.id, toPayload(form));
        toast.add({ type: "success", title: "Đã lưu" });
        warnVectorIfNeeded(saved);
        setEditing(false);
        onChanged(saved, false);
      } else {
        const created = await adminKnowledgeBaseApi.create(toPayload(form));
        toast.add({ type: "success", title: `Đã thêm tri thức #${created.entryNo}` });
        warnVectorIfNeeded(created);
        onChanged(created, true);
      }
    } catch (caught) {
      setDuplicateId(duplicateIdOf(caught));
      setError(errorText(caught, "Không lưu được tri thức."));
    } finally {
      setSaving(false);
    }
  };

  const restoreOldVersion = async (target: KnowledgeEntryVersion) => {
    if (!entry) return;
    setRestoringVersion(true);
    try {
      const saved = await adminKnowledgeBaseApi.restoreVersion(entry.id, target.version);
      toast.add({ type: "success", title: `Đã khôi phục nội dung phiên bản v${target.version} (tạo v${saved.version})` });
      warnVectorIfNeeded(saved);
      setInspecting(null);
      onChanged(saved, false);
    } catch (caught) {
      toast.add({ type: "error", title: "Không khôi phục được phiên bản", description: errorText(caught, "Vui lòng thử lại sau.") });
    } finally {
      setRestoringVersion(false);
    }
  };

  const editRows: TableRow[] = [
    ...(isCreate
      ? [{
          label: "Mã số",
          inputId: "kb-entry-no",
          value: <Input id="kb-entry-no" inputMode="numeric" value={form.entryNo} disabled={saving} placeholder="Bỏ trống để hệ thống tự cấp" onChange={(event) => patch({ entryNo: event.target.value })} />,
        }]
      : []),
    {
      label: "Loại nội dung *",
      inputId: "kb-doc-type",
      value: <OptionSelect id="kb-doc-type" value={form.docType} options={DOC_TYPE_OPTIONS} disabled={saving} onChange={(value) => patch({ docType: value })} />,
    },
    {
      label: "Nhãn *",
      inputId: "kb-label",
      value: <OptionSelect id="kb-label" value={form.label} options={[{ value: "", label: "Chưa chọn nhãn" }, ...LABEL_OPTIONS]} disabled={saving} onChange={(value) => patch({ label: value })} />,
    },
    {
      label: "Câu hỏi *",
      inputId: "kb-question",
      value: <Textarea id="kb-question" rows={2} maxLength={MAX_QUESTION} value={form.question} disabled={saving} onChange={(event) => patch({ question: event.target.value })} />,
    },
    {
      label: `Câu trả lời * (${form.answer.length}/${MAX_ANSWER})`,
      inputId: "kb-answer",
      value: <Textarea id="kb-answer" rows={8} maxLength={MAX_ANSWER} value={form.answer} disabled={saving} onChange={(event) => patch({ answer: event.target.value })} />,
    },
    {
      label: "Nguồn",
      inputId: "kb-source",
      value: <Textarea id="kb-source" rows={3} maxLength={MAX_SOURCE} value={form.source} disabled={saving} placeholder="Tên nguồn bằng chữ" onChange={(event) => patch({ source: event.target.value })} />,
    },
    {
      label: "Link nguồn",
      inputId: "kb-source-url",
      value: <Textarea id="kb-source-url" rows={2} maxLength={MAX_SOURCE_URL} value={form.sourceUrl} disabled={saving} placeholder="Nhiều link cách nhau bởi dấu ;" onChange={(event) => patch({ sourceUrl: event.target.value })} />,
    },
    {
      label: "Ngày thu thập *",
      inputId: "kb-retrieved-at",
      value: <Input id="kb-retrieved-at" type="date" value={form.retrievedAt} disabled={saving} onChange={(event) => patch({ retrievedAt: event.target.value })} />,
    },
    {
      label: "Mã chủ đề",
      inputId: "kb-topic-code",
      value: <Input id="kb-topic-code" maxLength={MAX_TOPIC_CODE} value={form.topicCode} disabled={saving} placeholder="I-20, DS-160... (bắt buộc với thủ tục)" onChange={(event) => patch({ topicCode: event.target.value })} />,
    },
    {
      label: "Ghi chú",
      inputId: "kb-note",
      value: <Textarea id="kb-note" rows={3} maxLength={MAX_NOTE} value={form.note} disabled={saving} onChange={(event) => patch({ note: event.target.value })} />,
    },
  ];

  const errorBox = error && (
    <Alert variant="destructive">
      <AlertDescription>
        {error}
        {duplicateId && (
          <Button type="button" variant="link" size="sm" className="h-auto px-1" onClick={() => onOpen(duplicateId)}>
            Mở tri thức đã có
          </Button>
        )}
      </AlertDescription>
    </Alert>
  );

  if (isCreate) {
    return (
      <DetailPanel
        title="Thêm tri thức"
        description="Điền đủ các mục có dấu *, và ít nhất một trong hai: nguồn hoặc link nguồn."
        onClose={onClose}
        closeDisabled={saving}
        footer={(
          <>
            <Button type="button" variant="outline" onClick={onClose} disabled={saving}>Hủy</Button>
            <Button type="button" onClick={() => void save()} loading={saving}>
              <SaveIcon /> Lưu
            </Button>
          </>
        )}
      >
        <form onSubmit={(event) => event.preventDefault()} className="space-y-5">
          {errorBox}
          <TableFrame title="Nội dung">
            <RowsTable rows={editRows} isEditing />
          </TableFrame>
        </form>
      </DetailPanel>
    );
  }

  const pre = (text: string | null) =>
    text?.trim() ? <span className="block whitespace-pre-wrap break-words text-left">{text}</span> : <Missing />;

  const viewRows: TableRow[] = [
    { label: "Mã số", value: `#${entry.entryNo}` },
    { label: "Phiên bản", value: `v${entry.version}` },
    { label: "Loại nội dung", value: docTypeName(entry.docType) },
    { label: "Nhãn", value: labelName(entry.label) },
    { label: "Câu hỏi", value: pre(entry.question) },
    { label: "Câu trả lời", value: pre(entry.answer) },
    { label: "Nguồn", value: pre(entry.source) },
    { label: "Link nguồn", value: pre(entry.sourceUrl) },
    { label: "Ngày thu thập", value: entry.retrievedAt },
    { label: "Mã chủ đề", value: entry.topicCode ?? <Missing /> },
    { label: "Ghi chú", value: pre(entry.note) },
    { label: "Ngày tạo", value: formatDateTime(entry.createdAt) },
    { label: "Cập nhật", value: formatDateTime(entry.updatedAt ?? entry.createdAt) },
  ];

  const editState: TableEditState = {
    isEditing: editing,
    canEdit: !isDeleted && !editing && !saving,
    readOnly: isDeleted,
    isSaving: saving,
    hasChanges,
    error: null,
    onEdit: () => {
      setForm(toForm(entry));
      setError(null);
      setEditing(true);
    },
    onCancel: () => {
      setForm(toForm(entry));
      setError(null);
      setEditing(false);
    },
    onSave: () => void save(),
  };

  return (
    <DetailPanel
      title={`Tri thức #${entry.entryNo}`}
      description={labelName(entry.label)}
      onClose={onClose}
      closeDisabled={saving}
      footer={isDeleted ? (
        <Button type="button" size="sm" variant="outline" disabled={saving || editing} onClick={() => onRestore(entry)}>
          <RotateCcwIcon /> Khôi phục
        </Button>
      ) : (
        <Button type="button" size="sm" variant="outline" className="text-destructive hover:text-destructive" disabled={saving || editing} onClick={() => onDelete(entry)}>
          <Trash2Icon /> Xóa
        </Button>
      )}
    >
      <div className="space-y-5">
        {errorBox}
        {isDeleted && (
          <Alert>
            <AlertDescription>Mục tri thức này đã bị xóa (chỉ đọc). Hãy khôi phục trước khi chỉnh sửa.</AlertDescription>
          </Alert>
        )}
        <EditableTable title="Nội dung" rows={editing ? editRows : viewRows} edit={editState} />

        <TableFrame title={`Lịch sử phiên bản (${versions.length})`}>
          {versions.length === 0 ? (
            <BlockBody>
              <span className="text-xs text-muted-foreground">Chưa có lịch sử phiên bản.</span>
            </BlockBody>
          ) : (
            versions.map((ver) => (
              <BlockBody key={ver.id}>
                <div className="flex flex-wrap items-center justify-between gap-2 text-xs">
                  <div className="flex flex-wrap items-center gap-2">
                    <Badge variant={ver.version === entry.version ? "brand" : "neutral"}>v{ver.version}</Badge>
                    <span className="font-medium">{versionActionLabel(ver.action, ver.restoredFromVersion)}</span>
                    <span className="text-muted-foreground">· {ver.actorName ?? "Hệ thống"}</span>
                    <span className="text-muted-foreground">· {formatDateTime(ver.createdAt)}</span>
                  </div>
                  <Button type="button" size="sm" variant="outline" className="h-7 px-2 text-xs" onClick={() => setInspecting(ver)}>
                    <HistoryIcon className="size-3.5" /> Xem
                  </Button>
                </div>
              </BlockBody>
            ))
          )}
        </TableFrame>
      </div>

      <Dialog
        open={inspecting !== null}
        onOpenChange={(open) => !open && !restoringVersion && setInspecting(null)}
        disablePointerDismissal={restoringVersion}
      >
        <DialogContent variant="form" size="lg">
          {inspecting && (
            <>
              <DialogHeader>
                <DialogIcon tone="brand">
                  <HistoryIcon />
                </DialogIcon>
                <DialogTitle>Tri thức #{entry.entryNo} · Phiên bản v{inspecting.version}</DialogTitle>
                <DialogDescription>
                  {versionActionLabel(inspecting.action, inspecting.restoredFromVersion)} · {inspecting.actorName ?? "Hệ thống"} · {formatDateTime(inspecting.createdAt)}
                </DialogDescription>
              </DialogHeader>
              <DialogBody className="space-y-3 text-sm">
                <div>
                  <span className="text-muted-foreground">Loại:</span> {docTypeName(inspecting.docType)} · <span className="text-muted-foreground">Nhãn:</span> {labelName(inspecting.label)} · <span className="text-muted-foreground">Ngày thu thập:</span> {inspecting.retrievedAt}
                  {inspecting.topicCode ? <> · <span className="text-muted-foreground">Mã chủ đề:</span> {inspecting.topicCode}</> : null}
                </div>
                <div>
                  <div className="text-xs text-muted-foreground">Câu hỏi</div>
                  <div className="whitespace-pre-wrap rounded-md border border-border bg-muted/40 p-2">{inspecting.question}</div>
                </div>
                <div>
                  <div className="text-xs text-muted-foreground">Câu trả lời</div>
                  <div className="max-h-60 overflow-y-auto whitespace-pre-wrap rounded-md border border-border bg-muted/40 p-2">{inspecting.answer}</div>
                </div>
                {(inspecting.source || inspecting.sourceUrl) && (
                  <div className="space-y-1 text-xs">
                    {inspecting.source && <div><span className="text-muted-foreground">Nguồn:</span> {inspecting.source}</div>}
                    {inspecting.sourceUrl && <div className="break-all"><span className="text-muted-foreground">Link nguồn:</span> {inspecting.sourceUrl}</div>}
                  </div>
                )}
                {inspecting.note && (
                  <div>
                    <div className="text-xs text-muted-foreground">Ghi chú</div>
                    <div className="whitespace-pre-wrap rounded-md border border-border bg-muted/40 p-2 text-xs">{inspecting.note}</div>
                  </div>
                )}
              </DialogBody>
              <DialogFooter layout={!isDeleted && inspecting.version !== entry.version ? "pair" : "single"}>
                <DialogCancel disabled={restoringVersion} onClick={() => setInspecting(null)}>
                  Đóng
                </DialogCancel>
                {!isDeleted && inspecting.version !== entry.version && (
                  <DialogAction tone="primary" loading={restoringVersion} onClick={() => void restoreOldVersion(inspecting)}>
                    Khôi phục phiên bản này
                  </DialogAction>
                )}
              </DialogFooter>
            </>
          )}
        </DialogContent>
      </Dialog>
    </DetailPanel>
  );
}
