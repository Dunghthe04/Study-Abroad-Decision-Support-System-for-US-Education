"use client";

import { useState } from "react";
import { SaveIcon, Trash2Icon } from "lucide-react";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Spinner } from "@/components/ui/spinner";
import { Textarea } from "@/components/ui/textarea";
import { toast } from "@/components/ui/toast";
import { DetailPanel } from "@/components/admin/shared/DetailPanel";
import { EditableTable, RowsTable, TableFrame, type TableEditState, type TableRow } from "@/components/admin/shared/EditableTable";
import { OptionSelect } from "@/components/admin/universities/OptionSelect";
import { adminKnowledgeBaseApi, duplicateIdOf } from "@/lib/knowledge-base-api";
import { DOC_TYPE_OPTIONS, docTypeName, formatDateTime, isProcedure, LABEL_OPTIONS, labelName } from "@/lib/knowledge-base-format";
import type { KnowledgeDocType, KnowledgeEntry, SaveKnowledgeEntryRequest } from "@/types/knowledge-base";

const MAX_QUESTION = 500;
const MAX_ANSWER = 8000;
const MAX_SOURCE = 2000;
const MAX_SOURCE_URL = 4000;
const MAX_TOPIC_CODE = 64;
const MAX_NOTE = 4000;

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
  imageUrl: string;
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
    imageUrl: entry?.imageUrl ?? "",
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
    imageUrl: orNull(form.imageUrl),
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
  onClose: () => void;
  onChanged: (entry: KnowledgeEntry, isNew: boolean) => void;
  onDelete: (entry: KnowledgeEntry) => void;
  onOpen: (id: string) => void;
}

export function KnowledgeEntryFormPanel(props: Props) {
  // key: đổi mục thì dựng lại form từ đầu, không mang state của mục trước
  return <KnowledgeEntryForm key={props.entry?.id ?? "new"} {...props} />;
}

function KnowledgeEntryForm({ entry, onClose, onChanged, onDelete, onOpen }: Props) {
  const [form, setForm] = useState<FormState>(() => toForm(entry));
  const [editing, setEditing] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [duplicateId, setDuplicateId] = useState<string | null>(null);

  const isCreate = entry === null;
  const hasChanges = JSON.stringify(form) !== JSON.stringify(toForm(entry));
  const patch = (change: Partial<FormState>) => setForm((current) => ({ ...current, ...change }));

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
        setEditing(false);
        onChanged(saved, false);
      } else {
        const created = await adminKnowledgeBaseApi.create(toPayload(form));
        toast.add({ type: "success", title: `Đã thêm tri thức #${created.entryNo}` });
        onChanged(created, true);
      }
    } catch (caught) {
      setDuplicateId(duplicateIdOf(caught));
      setError(errorText(caught, "Không lưu được tri thức."));
    } finally {
      setSaving(false);
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
      label: "Link ảnh",
      inputId: "kb-image-url",
      value: <Input id="kb-image-url" value={form.imageUrl} disabled={saving} onChange={(event) => patch({ imageUrl: event.target.value })} />,
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
      <DetailPanel title="Thêm tri thức" description="Điền đủ các mục có dấu *, và ít nhất một trong hai: nguồn hoặc link nguồn." onClose={onClose} closeDisabled={saving}>
        <form onSubmit={(event) => event.preventDefault()} className="space-y-5">
          {errorBox}
          <TableFrame title="Nội dung">
            <RowsTable rows={editRows} isEditing />
          </TableFrame>
          <div className="flex flex-wrap justify-end gap-2">
            <Button type="button" variant="outline" onClick={onClose} disabled={saving}>Hủy</Button>
            <Button type="button" onClick={() => void save()} disabled={saving}>
              {saving ? <Spinner /> : <SaveIcon />} Lưu
            </Button>
          </div>
        </form>
      </DetailPanel>
    );
  }

  const pre = (text: string | null) => <span className="block whitespace-pre-wrap break-words text-left">{text?.trim() ? text : "—"}</span>;
  const viewRows: TableRow[] = [
    { label: "Mã số", value: `#${entry.entryNo}` },
    { label: "Loại nội dung", value: docTypeName(entry.docType) },
    { label: "Nhãn", value: labelName(entry.label) },
    { label: "Câu hỏi", value: pre(entry.question) },
    { label: "Câu trả lời", value: pre(entry.answer) },
    { label: "Nguồn", value: pre(entry.source) },
    { label: "Link nguồn", value: pre(entry.sourceUrl) },
    { label: "Ngày thu thập", value: entry.retrievedAt },
    { label: "Mã chủ đề", value: entry.topicCode ?? "—" },
    { label: "Link ảnh", value: pre(entry.imageUrl) },
    { label: "Ghi chú", value: pre(entry.note) },
    { label: "Ngày tạo", value: formatDateTime(entry.createdAt) },
    { label: "Cập nhật", value: formatDateTime(entry.updatedAt ?? entry.createdAt) },
  ];

  const editState: TableEditState = {
    isEditing: editing,
    canEdit: !editing && !saving,
    readOnly: false,
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
    <DetailPanel title={`Tri thức #${entry.entryNo}`} description={labelName(entry.label)} onClose={onClose} closeDisabled={saving}>
      <div className="space-y-5">
        {errorBox}
        <div className="flex flex-wrap gap-2">
          <Button type="button" size="sm" variant="outline" className="ml-auto text-destructive hover:text-destructive" disabled={saving || editing} onClick={() => onDelete(entry)}>
            <Trash2Icon /> Xóa
          </Button>
        </div>
        <EditableTable title="Nội dung" rows={editing ? editRows : viewRows} edit={editState} />
      </div>
    </DetailPanel>
  );
}
