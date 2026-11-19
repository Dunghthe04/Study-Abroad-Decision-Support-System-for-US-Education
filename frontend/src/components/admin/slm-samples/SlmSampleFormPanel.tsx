"use client";

import { useState } from "react";
import { CheckIcon, SaveIcon, SendIcon, Trash2Icon, WrenchIcon, XIcon } from "lucide-react";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Spinner } from "@/components/ui/spinner";
import { Textarea } from "@/components/ui/textarea";
import { toast } from "@/components/ui/toast";
import { DetailPanel } from "@/components/admin/shared/DetailPanel";
import {
  BlockBody,
  EditableTable,
  RowsTable,
  TableFrame,
  type TableEditState,
  type TableRow,
} from "@/components/admin/shared/EditableTable";
import { OptionSelect } from "@/components/admin/universities/OptionSelect";
import { SlmSampleHistory } from "@/components/admin/slm-samples/SlmSampleHistory";
import { SlmStatusBadge } from "@/components/admin/slm-samples/SlmStatusBadge";
import { adminSlmSampleApi } from "@/lib/slm-sample-api";
import { useFetched } from "@/lib/use-fetched";
import {
  canSubmit,
  formatDateTime,
  isFromChat,
  isReady,
  jsonArrayProblem,
  optionalJsonProblem,
  prettyJson,
  SAMPLE_TYPE_OPTIONS,
  sampleToRequest,
  sampleTypeLabel,
  SOURCE_LABELS,
  SPLIT_OPTIONS,
} from "@/lib/slm-sample-format";
import type { SaveSlmSampleRequest, SlmReviewStatus, SlmSample, SlmSampleType, SlmSplit } from "@/types/slm-sample";

interface FormState {
  sampleType: SlmSampleType;
  split: SlmSplit;
  question: string;
  answer: string;
  topic: string;
  source: string;
  pendingQuestion: string;
  sourceReview: string;
  intents: string;
  profile: string;
  context: string;
}

type FieldKey = "question" | "answer" | "intents" | "profile" | "context";
type FieldErrors = Partial<Record<FieldKey, string>>;
type EditingTable = "info" | "labels";

function toForm(sample: SlmSample | null): FormState {
  if (!sample) {
    return {
      sampleType: "knowledge",
      split: "train",
      question: "",
      answer: "",
      topic: "",
      source: "",
      pendingQuestion: "",
      sourceReview: "",
      intents: "[]",
      profile: "",
      context: "",
    };
  }
  return {
    sampleType: sample.sampleType,
    split: sample.split,
    question: sample.question,
    answer: sample.answer ?? "",
    topic: sample.topic ?? "",
    source: sample.source,
    pendingQuestion: sample.pendingQuestion ?? "",
    sourceReview: sample.sourceReview ?? "",
    intents: prettyJson(sample.intents),
    profile: prettyJson(sample.profile),
    context: prettyJson(sample.context),
  };
}

function toPayload(form: FormState, submit: boolean): SaveSlmSampleRequest {
  const orNull = (text: string) => (text.trim() === "" ? null : text.trim());
  return {
    sampleType: form.sampleType,
    split: form.split,
    question: form.question.trim(),
    answer: orNull(form.answer),
    topic: orNull(form.topic),
    source: orNull(form.source),
    submit,
    pendingQuestion: orNull(form.pendingQuestion),
    sourceReview: orNull(form.sourceReview),
    intents: form.intents,
    profile: orNull(form.profile),
    context: orNull(form.context),
  };
}

function validate(form: FormState, submit: boolean): FieldErrors {
  const errors: FieldErrors = {};
  if (form.question.trim() === "") errors.question = "Vui lòng nhập câu hỏi.";
  const intentsProblem = jsonArrayProblem(form.intents);
  if (intentsProblem) errors.intents = intentsProblem;
  else if (submit && !isReady(form.answer, form.intents)) errors.answer = "Cần nhập câu trả lời (hoặc ý định) trước khi gửi duyệt.";
  const profileProblem = optionalJsonProblem(form.profile);
  if (profileProblem) errors.profile = profileProblem;
  const contextProblem = optionalJsonProblem(form.context);
  if (contextProblem) errors.context = contextProblem;
  return errors;
}

function errorText(error: unknown, fallback: string): string {
  return error instanceof Error ? error.message : fallback;
}

interface Props {
  sample: SlmSample | null;
  version: number;
  currentUserId: string | undefined;
  onClose: () => void;
  onChanged: (sample: SlmSample, isNew: boolean) => void;
  onDelete: (sample: SlmSample) => void;
  onReview: (sample: SlmSample, status: SlmReviewStatus) => void;
}

export function SlmSampleFormPanel(props: Props) {
  return <SlmSampleForm key={props.sample?.id ?? "new"} {...props} />;
}

function SlmSampleForm({ sample: listed, version, currentUserId, onClose, onChanged, onDelete, onReview }: Props) {
  const detail = useFetched(`detail|${listed?.id ?? "new"}|${version}`, () =>
    listed ? adminSlmSampleApi.get(listed.id) : Promise.resolve(null),
  );
  const sample = detail.data?.sample ?? listed;
  const [form, setForm] = useState<FormState>(() => toForm(listed));
  const [editing, setEditing] = useState<EditingTable | null>(null);
  const [errors, setErrors] = useState<FieldErrors>({});
  const [submitError, setSubmitError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const [actionError, setActionError] = useState<string | null>(null);

  const isCreate = sample === null;
  const formEnabled = isCreate || editing !== null;
  const hasChanges = JSON.stringify(form) !== JSON.stringify(toForm(sample));

  const patch = (change: Partial<FormState>) => setForm((current) => ({ ...current, ...change }));

  const startEdit = (table: EditingTable) => {
    setForm(toForm(sample));
    setErrors({});
    setSubmitError(null);
    setEditing(table);
  };

  const stopEdit = () => {
    setForm(toForm(sample));
    setErrors({});
    setSubmitError(null);
    setEditing(null);
  };

  const save = async (submit = false) => {
    const found = validate(form, submit);
    setErrors(found);
    if (Object.keys(found).length > 0) {
      setSubmitError("Dữ liệu chưa hợp lệ, xem các ô được đánh dấu.");
      return;
    }

    setSaving(true);
    setSubmitError(null);
    try {
      if (sample) {
        const saved = await adminSlmSampleApi.update(sample.id, toPayload(form, submit));
        const resetToPending = saved.reviewStatus !== sample.reviewStatus;
        toast.add({
          type: "success",
          title: submit ? "Đã gửi duyệt" : "Đã lưu mẫu",
          description: resetToPending && !submit ? "Nội dung đã đổi nên mẫu quay về Chờ duyệt, cần duyệt lại." : undefined,
        });
        setEditing(null);
        onChanged(saved, false);
      } else {
        const created = await adminSlmSampleApi.create(toPayload(form, submit));
        toast.add({ type: "success", title: submit ? "Đã thêm mẫu và gửi duyệt" : "Đã lưu nháp" });
        onChanged(created, true);
      }
    } catch (error) {
      setSubmitError(errorText(error, "Không lưu được mẫu."));
    } finally {
      setSaving(false);
    }
  };

  const submitExisting = async () => {
    if (!sample) return;
    setSaving(true);
    setActionError(null);
    try {
      const saved = await adminSlmSampleApi.update(sample.id, { ...sampleToRequest(sample), submit: true });
      toast.add({ type: "success", title: "Đã gửi duyệt" });
      onChanged(saved, false);
    } catch (error) {
      setActionError(errorText(error, "Không gửi duyệt được."));
    } finally {
      setSaving(false);
    }
  };

  const inputsOff = saving;

  const infoRows: TableRow[] = [
    {
      label: "Loại mẫu *",
      inputId: "slm-type",
      value: (
        <OptionSelect id="slm-type" value={form.sampleType} options={SAMPLE_TYPE_OPTIONS} disabled={inputsOff} onChange={(value) => patch({ sampleType: value as SlmSampleType })} />
      ),
    },
    {
      label: "Tập dữ liệu *",
      inputId: "slm-split",
      value: <OptionSelect id="slm-split" value={form.split} options={SPLIT_OPTIONS} disabled={inputsOff} onChange={(value) => patch({ split: value as SlmSplit })} />,
    },
    {
      label: "Câu hỏi *",
      inputId: "slm-question",
      error: errors.question,
      value: (
        <Textarea id="slm-question" rows={2} value={form.question} disabled={inputsOff} aria-invalid={errors.question ? true : undefined} onChange={(event) => patch({ question: event.target.value })} />
      ),
    },
    {
      label: "Câu trả lời",
      inputId: "slm-answer",
      error: errors.answer,
      value: (
        <Textarea id="slm-answer" rows={5} value={form.answer} disabled={inputsOff} aria-invalid={errors.answer ? true : undefined} placeholder="Nhập câu trả lời cho câu hỏi" onChange={(event) => patch({ answer: event.target.value })} />
      ),
    },
    {
      label: "Chủ đề",
      inputId: "slm-topic",
      value: <Input id="slm-topic" value={form.topic} maxLength={128} disabled={inputsOff} onChange={(event) => patch({ topic: event.target.value })} />,
    },
    {
      label: "Nguồn",
      inputId: "slm-source-text",
      value: (
        <Input
          id="slm-source-text"
          value={form.source}
          maxLength={256}
          placeholder="Nhập tay"
          disabled={inputsOff || (sample !== null && isFromChat(sample))}
          onChange={(event) => patch({ source: event.target.value })}
        />
      ),
    },
    {
      label: "Câu hệ thống đã hỏi",
      inputId: "slm-pending",
      value: <Input id="slm-pending" value={form.pendingQuestion} disabled={inputsOff} onChange={(event) => patch({ pendingQuestion: event.target.value })} />,
    },
    {
      label: "Nhãn rà soát",
      inputId: "slm-source",
      value: <Input id="slm-source" value={form.sourceReview} maxLength={16} placeholder="v10" disabled={inputsOff} onChange={(event) => patch({ sourceReview: event.target.value })} />,
    },
  ];

  const jsonRow = (label: string, id: string, field: "intents" | "profile" | "context"): TableRow => ({
    label,
    inputId: id,
    error: errors[field],
    value: (
      <Textarea
        id={id}
        rows={field === "intents" ? 6 : 4}
        spellCheck={false}
        value={form[field]}
        disabled={inputsOff}
        aria-invalid={errors[field] ? true : undefined}
        className="font-mono text-xs"
        onChange={(event) => patch({ [field]: event.target.value })}
      />
    ),
  });

  const labelRows: TableRow[] = [
    jsonRow("Ý định (JSON) *", "slm-intents", "intents"),
    jsonRow("Hồ sơ (JSON)", "slm-profile", "profile"),
    jsonRow("Ngữ cảnh (JSON)", "slm-context", "context"),
  ];

  if (isCreate) {
    return (
      <DetailPanel title="Thêm mẫu mới" description="Lưu nháp để sửa tiếp sau, hoặc gửi duyệt khi đã có câu trả lời." onClose={onClose} closeDisabled={saving}>
        <form onSubmit={(event) => event.preventDefault()} className="space-y-5">
          {submitError && <Alert variant="destructive"><AlertDescription>{submitError}</AlertDescription></Alert>}
          <TableFrame title="Thông tin mẫu">
            <RowsTable rows={infoRows} isEditing />
          </TableFrame>
          <TableFrame title="Nhãn đầu ra">
            <RowsTable rows={labelRows} isEditing />
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

  if (!sample) return null;

  const editState = (table: EditingTable): TableEditState => ({
    isEditing: editing === table,
    canEdit: editing === null && !saving,
    isSaving: saving,
    hasChanges,
    error: editing === table ? submitError : null,
    onEdit: () => startEdit(table),
    onCancel: stopEdit,
    onSave: () => void save(),
  });

  const pre = (text: string | null) => <span className="block whitespace-pre-wrap break-words text-left">{text && text.trim() !== "" ? text : "—"}</span>;

  const viewInfoRows: TableRow[] = [
    { label: "Loại mẫu", value: sampleTypeLabel(sample.sampleType) },
    { label: "Tập dữ liệu", value: sample.split },
    { label: "Câu hỏi", value: pre(sample.question) },
    { label: "Câu trả lời", value: pre(sample.answer) },
    { label: "Chủ đề", value: sample.topic ?? "—" },
    { label: "Nguồn", value: SOURCE_LABELS[sample.source] ?? sample.source },
    { label: "Câu hệ thống đã hỏi", value: sample.pendingQuestion ?? "—" },
    { label: "Nhãn rà soát", value: sample.sourceReview ?? "—" },
    { label: "Người tạo", value: sample.createdByName ?? (isFromChat(sample) ? "Hệ thống" : "—") },
    { label: "Ngày tạo", value: formatDateTime(sample.createdAt) },
    { label: "Cập nhật", value: formatDateTime(sample.updatedAt ?? sample.createdAt) },
  ];

  const reviewRows: TableRow[] = [
    { label: "Trạng thái", value: <div className="flex justify-end"><SlmStatusBadge status={sample.reviewStatus} /></div> },
    { label: sample.reviewStatus === "approved" ? "Ghi chú duyệt" : "Lý do / ghi chú", value: pre(sample.reviewNote) },
    { label: "Người duyệt", value: detail.data?.reviewedByName ?? "—" },
    { label: "Duyệt lần cuối", value: formatDateTime(sample.reviewedAt) },
  ];

  const jsonBlocks: { label: string; text: string | null }[] = [
    { label: "Ý định", text: sample.intents },
    { label: "Hồ sơ", text: sample.profile },
    { label: "Ngữ cảnh", text: sample.context },
  ];

  const touchedByMe = detail.data?.history.some((item) => item.actorUserId === currentUserId && (item.action === "create" || item.action === "edit" || item.action === "submit"));
  const canReview = sample.reviewStatus === "pending" && currentUserId !== undefined && sample.createdByUserId !== currentUserId && detail.data !== undefined && !touchedByMe;
  const ready = isReady(sample.answer, sample.intents);
  const busy = saving || editing !== null;

  return (
    <DetailPanel title="Chi tiết mẫu huấn luyện" description={sampleTypeLabel(sample.sampleType)} onClose={onClose} closeDisabled={saving}>
      <div className="space-y-5">
        {isFromChat(sample) && sample.reviewStatus === "draft" && (
          <Alert>
            <AlertDescription>Câu hỏi này lấy từ chat mà hệ thống chưa trả lời được. Hãy nhập câu trả lời rồi gửi duyệt, hoặc xóa nếu không phù hợp.</AlertDescription>
          </Alert>
        )}
        {actionError && <Alert variant="destructive"><AlertDescription>{actionError}</AlertDescription></Alert>}
        {detail.error && <Alert variant="destructive"><AlertDescription>{detail.error}</AlertDescription></Alert>}

        <div className="flex flex-wrap gap-2">
          {canSubmit(sample.reviewStatus) && (
            <Button type="button" size="sm" disabled={busy || !ready} title={ready ? undefined : "Cần có câu trả lời hoặc ý định trước khi gửi duyệt"} onClick={() => void submitExisting()}>
              {saving ? <Spinner /> : <SendIcon />} Gửi duyệt
            </Button>
          )}
          {canReview && (
            <>
              <Button type="button" size="sm" variant="outline" className="border-ok/20 bg-ok-wash text-ok hover:bg-ok-wash/70" disabled={busy} onClick={() => onReview(sample, "approved")}>
                <CheckIcon /> Duyệt
              </Button>
              <Button type="button" size="sm" variant="outline" className="border-risk/20 bg-risk-wash text-risk hover:bg-risk-wash/70" disabled={busy} onClick={() => onReview(sample, "rejected")}>
                <XIcon /> Từ chối
              </Button>
              <Button type="button" size="sm" variant="outline" className="border-warn/20 bg-warn-wash text-warn hover:bg-warn-wash/70" disabled={busy} onClick={() => onReview(sample, "needs_fix")}>
                <WrenchIcon /> Cần sửa
              </Button>
            </>
          )}
          <Button type="button" size="sm" variant="outline" className="ml-auto text-destructive hover:text-destructive" disabled={busy} onClick={() => onDelete(sample)}>
            <Trash2Icon /> Xóa
          </Button>
        </div>

        <EditableTable title="Duyệt" rows={reviewRows} />
        <EditableTable title="Thông tin mẫu" rows={formEnabled && editing === "info" ? infoRows : viewInfoRows} edit={editState("info")} />
        <TableFrame title="Nhãn đầu ra" edit={editState("labels")}>
          {editing === "labels" ? (
            <RowsTable rows={labelRows} isEditing />
          ) : (
            jsonBlocks.map((block) => (
              <BlockBody key={block.label}>
                <div className="mb-1 text-xs text-muted-foreground">{block.label}</div>
                <pre className="max-h-48 overflow-auto whitespace-pre-wrap break-words font-mono text-xs">{prettyJson(block.text) || "—"}</pre>
              </BlockBody>
            ))
          )}
        </TableFrame>
        <SlmSampleHistory history={detail.data?.history ?? []} />
      </div>
    </DetailPanel>
  );
}
