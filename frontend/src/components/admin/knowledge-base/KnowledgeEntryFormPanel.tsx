"use client";

import { useState } from "react";
import { ArchiveRestoreIcon, CheckIcon, SaveIcon, SendIcon, Trash2Icon, WrenchIcon, XIcon } from "lucide-react";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Spinner } from "@/components/ui/spinner";
import { Textarea } from "@/components/ui/textarea";
import { toast } from "@/components/ui/toast";
import { DetailPanel } from "@/components/admin/shared/DetailPanel";
import { EditableTable, RowsTable, TableFrame, type TableEditState, type TableRow } from "@/components/admin/shared/EditableTable";
import { OptionSelect } from "@/components/admin/universities/OptionSelect";
import { SlmStatusBadge } from "@/components/admin/slm-samples/SlmStatusBadge";
import { KnowledgeEntryHistory } from "@/components/admin/knowledge-base/KnowledgeEntryHistory";
import { adminKnowledgeBaseApi, duplicateIdOf } from "@/lib/knowledge-base-api";
import {
  canSubmit,
  entryToRequest,
  formatDateTime,
  isReady,
  KNOWLEDGE_LEVELS,
  LABEL_OPTIONS,
  labelName,
  levelName,
  ORIGIN_LABELS,
} from "@/lib/knowledge-base-format";
import { useFetched } from "@/lib/use-fetched";
import type { KnowledgeEntry, KnowledgeLevel, KnowledgeReviewStatus, SaveKnowledgeEntryRequest } from "@/types/knowledge-base";

const MAX_QUESTION = 500;
const MAX_ANSWER = 4000;
const MAX_SOURCE = 2000;
const LABEL_SELECT_OPTIONS = [{ value: "", label: "Chưa chọn nhãn" }, ...LABEL_OPTIONS];

interface FormState {
  label: string;
  levels: KnowledgeLevel[];
  question: string;
  answer: string;
  source: string;
}

function toForm(entry: KnowledgeEntry | null): FormState {
  return {
    label: entry?.label ?? "",
    levels: entry?.levels ?? [],
    question: entry?.question ?? "",
    answer: entry?.answer ?? "",
    source: entry?.source ?? "",
  };
}

function toPayload(form: FormState, submit: boolean): SaveKnowledgeEntryRequest {
  const orNull = (text: string) => (text.trim() === "" ? null : text.trim());
  return { label: orNull(form.label), levels: form.levels, question: form.question.trim(), answer: orNull(form.answer), source: orNull(form.source), submit };
}

/** Kiểm tra sớm ở trình duyệt; backend vẫn kiểm tra lại toàn bộ. */
function problemOf(form: FormState, submit: boolean): string | null {
  if (form.question.trim() === "") return "Vui lòng nhập câu hỏi.";
  if (submit && !isReady({ label: form.label || null, levels: form.levels, answer: form.answer, source: form.source }))
    return "Cần chọn nhãn, bậc học và nhập câu trả lời, nguồn trước khi gửi duyệt.";
  return null;
}

function errorText(error: unknown, fallback: string): string {
  return error instanceof Error ? error.message : fallback;
}

interface Props {
  entry: KnowledgeEntry | null;
  version: number;
  currentUserId: string | undefined;
  onClose: () => void;
  onChanged: (entry: KnowledgeEntry, isNew: boolean) => void;
  onDelete: (entry: KnowledgeEntry) => void;
  onRestore: (entry: KnowledgeEntry) => void;
  onReview: (entry: KnowledgeEntry, status: KnowledgeReviewStatus) => void;
  onOpen: (id: string) => void;
}

export function KnowledgeEntryFormPanel(props: Props) {
  // key: đổi mục thì dựng lại form từ đầu, không mang state của mục trước
  return <KnowledgeEntryForm key={props.entry?.id ?? "new"} {...props} />;
}

function KnowledgeEntryForm({ entry: listed, version, currentUserId, onClose, onChanged, onDelete, onRestore, onReview, onOpen }: Props) {
  const detail = useFetched(`kb-detail|${listed?.id ?? "new"}|${version}`, () =>
    listed ? adminKnowledgeBaseApi.get(listed.id) : Promise.resolve(null),
  );
  const entry = detail.data?.entry ?? listed;
  const [form, setForm] = useState<FormState>(() => toForm(listed));
  const [editing, setEditing] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [duplicateId, setDuplicateId] = useState<string | null>(null);

  const isCreate = entry === null;
  const hasChanges = JSON.stringify(form) !== JSON.stringify(toForm(entry));
  const patch = (change: Partial<FormState>) => setForm((current) => ({ ...current, ...change }));

  const toggleLevel = (level: KnowledgeLevel, checked: boolean) => {
    if (level === "chung") patch({ levels: checked ? ["chung"] : [] });
    else patch({ levels: checked ? [...form.levels, level] : form.levels.filter((item) => item !== level) });
  };

  const fail = (caught: unknown, fallback: string) => {
    setDuplicateId(duplicateIdOf(caught));
    setError(errorText(caught, fallback));
  };

  const save = async (submit: boolean) => {
    const problem = problemOf(form, submit);
    setDuplicateId(null);
    setError(problem);
    if (problem) return;

    setSaving(true);
    try {
      if (entry) {
        const saved = await adminKnowledgeBaseApi.update(entry.id, toPayload(form, submit));
        const backToPending = entry.reviewStatus === "approved" && saved.reviewStatus === "pending";
        toast.add({ type: "success", title: submit ? "Đã gửi duyệt" : "Đã lưu", description: backToPending ? "Tri thức đã duyệt bị sửa nên quay về Chờ duyệt." : undefined });
        setEditing(false);
        onChanged(saved, false);
      } else {
        const created = await adminKnowledgeBaseApi.create(toPayload(form, submit));
        toast.add({ type: "success", title: submit ? "Đã thêm và gửi duyệt" : "Đã lưu nháp" });
        onChanged(created, true);
      }
    } catch (caught) {
      fail(caught, "Không lưu được tri thức.");
    } finally {
      setSaving(false);
    }
  };

  const submitExisting = async () => {
    if (!entry) return;
    setSaving(true);
    setError(null);
    try {
      const saved = await adminKnowledgeBaseApi.update(entry.id, { ...entryToRequest(entry), submit: true });
      toast.add({ type: "success", title: "Đã gửi duyệt" });
      onChanged(saved, false);
    } catch (caught) {
      fail(caught, "Không gửi duyệt được.");
    } finally {
      setSaving(false);
    }
  };

  const editRows: TableRow[] = [
    {
      label: "Nhãn",
      inputId: "kb-label",
      value: <OptionSelect id="kb-label" value={form.label} options={LABEL_SELECT_OPTIONS} disabled={saving} onChange={(value) => patch({ label: value })} />,
    },
    {
      label: "Bậc học",
      value: (
        <fieldset className="flex flex-wrap gap-x-4 gap-y-2" disabled={saving}>
          <legend className="sr-only">Bậc học</legend>
          {KNOWLEDGE_LEVELS.map((level) => (
            <label key={level.id} className="flex items-center gap-2 text-sm">
              <Checkbox
                checked={form.levels.includes(level.id)}
                disabled={saving || (level.id !== "chung" && form.levels.includes("chung"))}
                onCheckedChange={(checked) => toggleLevel(level.id, checked === true)}
              />
              {level.label}
            </label>
          ))}
        </fieldset>
      ),
    },
    {
      label: "Câu hỏi *",
      inputId: "kb-question",
      value: <Textarea id="kb-question" rows={2} maxLength={MAX_QUESTION} value={form.question} disabled={saving} onChange={(event) => patch({ question: event.target.value })} />,
    },
    {
      label: `Câu trả lời (${form.answer.length}/${MAX_ANSWER})`,
      inputId: "kb-answer",
      value: <Textarea id="kb-answer" rows={8} maxLength={MAX_ANSWER} value={form.answer} disabled={saving} onChange={(event) => patch({ answer: event.target.value })} />,
    },
    {
      label: "Nguồn",
      inputId: "kb-source",
      value: (
        <Textarea id="kb-source" rows={4} maxLength={MAX_SOURCE} value={form.source} disabled={saving} placeholder={"Nhà cung cấp\nMã: ...\nNgày thu: dd/MM/yyyy"} onChange={(event) => patch({ source: event.target.value })} />
      ),
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
      <DetailPanel title="Thêm tri thức" description="Lưu nháp chỉ cần câu hỏi; gửi duyệt cần đủ nhãn, bậc học, câu trả lời, nguồn." onClose={onClose} closeDisabled={saving}>
        <form onSubmit={(event) => event.preventDefault()} className="space-y-5">
          {errorBox}
          <TableFrame title="Nội dung">
            <RowsTable rows={editRows} isEditing />
          </TableFrame>
          <div className="flex flex-wrap justify-end gap-2">
            <Button type="button" variant="outline" onClick={onClose} disabled={saving}>Hủy</Button>
            <Button type="button" variant="outline" onClick={() => void save(false)} disabled={saving}>
              {saving ? <Spinner /> : <SaveIcon />} Lưu nháp
            </Button>
            <Button type="button" onClick={() => void save(true)} disabled={saving}>
              {saving ? <Spinner /> : <SendIcon />} Gửi duyệt
            </Button>
          </div>
        </form>
      </DetailPanel>
    );
  }

  if (!entry) return null;

  const pre = (text: string | null) => <span className="block whitespace-pre-wrap break-words text-left">{text?.trim() ? text : "—"}</span>;
  const viewRows: TableRow[] = [
    { label: "Mã số", value: `#${entry.entryNo}` },
    { label: "Nhãn", value: labelName(entry.label) },
    { label: "Bậc học", value: entry.levels.length ? entry.levels.map(levelName).join(", ") : "—" },
    { label: "Câu hỏi", value: pre(entry.question) },
    { label: "Câu trả lời", value: pre(entry.answer) },
    { label: "Nguồn", value: pre(entry.source) },
    { label: "Nguồn gốc", value: ORIGIN_LABELS[entry.origin] ?? entry.origin },
    { label: "Người tạo", value: entry.createdByName ?? (entry.origin === "chat-unanswered" ? "Hệ thống" : "—") },
    { label: "Ngày tạo", value: formatDateTime(entry.createdAt) },
    { label: "Cập nhật", value: formatDateTime(entry.updatedAt ?? entry.createdAt) },
  ];
  const reviewRows: TableRow[] = [
    { label: "Trạng thái", value: <div className="flex justify-end"><SlmStatusBadge status={entry.reviewStatus} /></div> },
    { label: entry.reviewStatus === "approved" ? "Ghi chú duyệt" : "Lý do / ghi chú", value: pre(entry.reviewNote) },
    { label: "Người duyệt", value: detail.data?.reviewedByName ?? "—" },
    { label: "Duyệt lần cuối", value: formatDateTime(entry.reviewedAt) },
  ];

  const deleted = entry.deletedAt !== null;
  const touchedByMe = detail.data?.history.some(
    (item) => item.actorUserId === currentUserId && (item.action === "create" || item.action === "edit" || item.action === "submit"),
  );
  // Nút chỉ là gợi ý cho người dùng; backend mới là nơi chặn thật (skippedOwn)
  const canReview = !deleted && entry.reviewStatus === "pending" && currentUserId !== undefined
    && entry.createdByUserId !== currentUserId && detail.data !== undefined && !touchedByMe;
  const busy = saving || editing;

  const editState: TableEditState = {
    isEditing: editing,
    canEdit: !editing && !saving && !deleted,
    readOnly: deleted,
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
    onSave: () => void save(false),
  };

  return (
    <DetailPanel title={`Tri thức #${entry.entryNo}`} description={labelName(entry.label)} onClose={onClose} closeDisabled={saving}>
      <div className="space-y-5">
        {deleted && (
          <Alert>
            <AlertDescription>Tri thức này đã bị xóa lúc {formatDateTime(entry.deletedAt)}. Khôi phục để sửa hoặc duyệt tiếp.</AlertDescription>
          </Alert>
        )}
        {!deleted && entry.origin === "chat-unanswered" && entry.reviewStatus === "draft" && (
          <Alert>
            <AlertDescription>Câu hỏi lấy từ chat mà hệ thống chưa trả lời được. Hãy bổ sung nhãn, bậc học, câu trả lời, nguồn rồi gửi duyệt, hoặc xóa nếu không phù hợp.</AlertDescription>
          </Alert>
        )}
        {errorBox}
        {detail.error && <Alert variant="destructive"><AlertDescription>{detail.error}</AlertDescription></Alert>}

        <div className="flex flex-wrap gap-2">
          {deleted ? (
            <Button type="button" size="sm" onClick={() => onRestore(entry)}>
              <ArchiveRestoreIcon /> Khôi phục
            </Button>
          ) : (
            <>
              {canSubmit(entry.reviewStatus) && (
                <Button type="button" size="sm" disabled={busy || !isReady(entry)} title={isReady(entry) ? undefined : "Cần đủ nhãn, bậc học, câu trả lời và nguồn"} onClick={() => void submitExisting()}>
                  {saving ? <Spinner /> : <SendIcon />} Gửi duyệt
                </Button>
              )}
              {canReview && (
                <>
                  <Button type="button" size="sm" variant="outline" className="border-ok/20 bg-ok-wash text-ok hover:bg-ok-wash/70" disabled={busy} onClick={() => onReview(entry, "approved")}>
                    <CheckIcon /> Duyệt
                  </Button>
                  <Button type="button" size="sm" variant="outline" className="border-risk/20 bg-risk-wash text-risk hover:bg-risk-wash/70" disabled={busy} onClick={() => onReview(entry, "rejected")}>
                    <XIcon /> Từ chối
                  </Button>
                  <Button type="button" size="sm" variant="outline" className="border-warn/20 bg-warn-wash text-warn hover:bg-warn-wash/70" disabled={busy} onClick={() => onReview(entry, "needs_fix")}>
                    <WrenchIcon /> Cần sửa
                  </Button>
                </>
              )}
              <Button type="button" size="sm" variant="outline" className="ml-auto text-destructive hover:text-destructive" disabled={busy} onClick={() => onDelete(entry)}>
                <Trash2Icon /> Xóa
              </Button>
            </>
          )}
        </div>

        <EditableTable title="Duyệt" rows={reviewRows} />
        <EditableTable title="Nội dung" rows={editing ? editRows : viewRows} edit={editState} />
        <KnowledgeEntryHistory history={detail.data?.history ?? []} />
      </div>
    </DetailPanel>
  );
}