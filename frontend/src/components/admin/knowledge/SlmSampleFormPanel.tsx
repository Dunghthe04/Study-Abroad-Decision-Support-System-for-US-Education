"use client";

import { useState } from "react";
import { PlusIcon } from "lucide-react";
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
import { InlineSelectCell } from "@/components/admin/shared/InlineSelectCell";
import { OptionSelect } from "@/components/admin/universities/OptionSelect";
import { renderStatusOption } from "@/components/admin/knowledge/SlmStatusBadge";
import { adminSlmSampleApi } from "@/lib/slm-sample-api";
import {
  formatDateTime,
  jsonArrayProblem,
  optionalJsonProblem,
  prettyJson,
  REVIEW_STATUS_OPTIONS,
  SAMPLE_TYPE_OPTIONS,
  sampleTypeLabel,
  SPLIT_OPTIONS,
} from "@/lib/slm-sample-format";
import type { SaveSlmSampleRequest, SlmReviewStatus, SlmSample, SlmSampleType, SlmSplit } from "@/types/slm-sample";

interface FormState {
  sampleType: SlmSampleType;
  split: SlmSplit;
  question: string;
  pendingQuestion: string;
  sourceReview: string;
  intents: string;
  profile: string;
  context: string;
}

type FieldKey = "question" | "intents" | "profile" | "context";
type FieldErrors = Partial<Record<FieldKey, string>>;
type EditingTable = "info" | "labels";

function toForm(sample: SlmSample | null): FormState {
  if (!sample) {
    return {
      sampleType: "knowledge",
      split: "train",
      question: "",
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
    pendingQuestion: sample.pendingQuestion ?? "",
    sourceReview: sample.sourceReview ?? "",
    intents: prettyJson(sample.intents),
    profile: prettyJson(sample.profile),
    context: prettyJson(sample.context),
  };
}

function toPayload(form: FormState): SaveSlmSampleRequest {
  const orNull = (text: string) => (text.trim() === "" ? null : text.trim());
  return {
    sampleType: form.sampleType,
    split: form.split,
    question: form.question.trim(),
    pendingQuestion: orNull(form.pendingQuestion),
    sourceReview: orNull(form.sourceReview),
    intents: form.intents,
    profile: orNull(form.profile),
    context: orNull(form.context),
  };
}

function validate(form: FormState): FieldErrors {
  const errors: FieldErrors = {};
  if (form.question.trim() === "") errors.question = "Vui lòng nhập câu hỏi.";
  const intentsProblem = jsonArrayProblem(form.intents);
  if (intentsProblem) errors.intents = intentsProblem;
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
  onClose: () => void;
  onChanged: (sample: SlmSample, isNew: boolean) => void;
}

export function SlmSampleFormPanel({ sample, onClose, onChanged }: Props) {
  return <SlmSampleForm key={sample?.id ?? "new"} sample={sample} onClose={onClose} onChanged={onChanged} />;
}

function SlmSampleForm({ sample, onClose, onChanged }: Props) {
  const [form, setForm] = useState<FormState>(() => toForm(sample));
  const [editing, setEditing] = useState<EditingTable | null>(null);
  const [errors, setErrors] = useState<FieldErrors>({});
  const [submitError, setSubmitError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

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

  const save = async () => {
    const found = validate(form);
    setErrors(found);
    if (Object.keys(found).length > 0) {
      setSubmitError("Dữ liệu chưa hợp lệ, xem các ô được đánh dấu.");
      return;
    }

    setSaving(true);
    setSubmitError(null);
    try {
      if (sample) {
        const saved = await adminSlmSampleApi.update(sample.id, toPayload(form));
        const resetToDraft = saved.reviewStatus !== sample.reviewStatus;
        toast.add({
          type: "success",
          title: "Đã lưu mẫu",
          description: resetToDraft ? "Nhãn đã đổi nên mẫu quay về Nháp, cần duyệt lại." : undefined,
        });
        setEditing(null);
        onChanged(saved, false);
      } else {
        const created = await adminSlmSampleApi.create(toPayload(form));
        toast.add({ type: "success", title: "Đã thêm mẫu mới" });
        onChanged(created, true);
      }
    } catch (error) {
      setSubmitError(errorText(error, "Không lưu được mẫu."));
    } finally {
      setSaving(false);
    }
  };

  const changeStatus = async (status: string) => {
    if (!sample) return;
    try {
      await adminSlmSampleApi.review([sample.id], status as SlmReviewStatus);
      const fresh = await adminSlmSampleApi.get(sample.id);
      toast.add({ type: "success", title: "Đã đổi trạng thái duyệt" });
      onChanged(fresh, false);
    } catch (error) {
      toast.add({ type: "error", title: "Không đổi được trạng thái", description: errorText(error, "Vui lòng thử lại sau.") });
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
    const handleSubmit = (event: React.FormEvent) => {
      event.preventDefault();
      if (!saving) void save();
    };

    return (
      <DetailPanel title="Thêm mẫu mới" description="Nhập câu hỏi và nhãn; hồ sơ, ý định, ngữ cảnh phải là JSON hợp lệ." onClose={onClose} closeDisabled={saving}>
        <form onSubmit={handleSubmit} className="space-y-5">
          {submitError && <Alert variant="destructive"><AlertDescription>{submitError}</AlertDescription></Alert>}
          <TableFrame title="Thông tin mẫu">
            <RowsTable rows={infoRows} isEditing />
          </TableFrame>
          <TableFrame title="Nhãn đầu ra">
            <RowsTable rows={labelRows} isEditing />
          </TableFrame>
          <div className="flex justify-end gap-2">
            <Button type="button" variant="outline" onClick={onClose} disabled={saving}>Hủy</Button>
            <Button type="submit" disabled={saving}>
              {saving ? <Spinner /> : <PlusIcon />} Tạo mẫu
            </Button>
          </div>
        </form>
      </DetailPanel>
    );
  }

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

  const viewInfoRows: TableRow[] = [
    { label: "Loại mẫu", value: sampleTypeLabel(sample.sampleType) },
    { label: "Tập dữ liệu", value: sample.split },
    { label: "Câu hỏi", value: sample.question },
    { label: "Câu hệ thống đã hỏi", value: sample.pendingQuestion ?? "—" },
    { label: "Nhãn rà soát", value: sample.sourceReview ?? "—" },
    { label: "Cập nhật", value: formatDateTime(sample.updatedAt ?? sample.createdAt) },
  ];

  const reviewRows: TableRow[] = [
    {
      label: "Trạng thái",
      value: (
        <div className="flex justify-end">
          <InlineSelectCell value={sample.reviewStatus} options={REVIEW_STATUS_OPTIONS} label="Đổi trạng thái duyệt" renderOption={renderStatusOption} onChange={changeStatus} />
        </div>
      ),
    },
    { label: "Ghi chú duyệt", value: sample.reviewNote ?? "—" },
    { label: "Duyệt lần cuối", value: formatDateTime(sample.reviewedAt) },
  ];

  const jsonBlocks: { label: string; text: string | null }[] = [
    { label: "Ý định", text: sample.intents },
    { label: "Hồ sơ", text: sample.profile },
    { label: "Ngữ cảnh", text: sample.context },
  ];

  return (
    <DetailPanel title="Chi tiết mẫu huấn luyện" description={sampleTypeLabel(sample.sampleType)} onClose={onClose} closeDisabled={saving}>
      <div className="space-y-5">
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
      </div>
    </DetailPanel>
  );
}
