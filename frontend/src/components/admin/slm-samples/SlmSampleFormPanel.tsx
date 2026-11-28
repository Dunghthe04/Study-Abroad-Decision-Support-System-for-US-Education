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
import {
  BlockBody,
  EditableTable,
  RowsTable,
  TableFrame,
  type TableEditState,
  type TableRow,
} from "@/components/admin/shared/EditableTable";
import { OptionSelect } from "@/components/admin/universities/OptionSelect";
import { adminSlmSampleApi } from "@/lib/slm-sample-api";
import { useFetched } from "@/lib/use-fetched";
import {
  formatDateTime,
  isReady,
  jsonArrayProblem,
  optionalJsonProblem,
  prettyJson,
  SAMPLE_TYPE_OPTIONS,
  sampleTypeLabel,
  SOURCE_LABELS,
  SPLIT_OPTIONS,
  versionActionLabel,
} from "@/lib/slm-sample-format";
import type { SaveSlmSampleRequest, SlmSample, SlmSampleType, SlmSampleVersion, SlmSplit } from "@/types/slm-sample";

interface FormState {
  sampleType: SlmSampleType;
  split: SlmSplit;
  question: string;
  source: string;
  pendingQuestion: string;
  sourceReview: string;
  intents: string;
  profile: string;
  context: string;
}

type FieldKey = "question" | "intents" | "profile" | "context";
type FieldErrors = Partial<Record<FieldKey, string>>;
type EditingTable = "info" | "labels";

const Missing = () => (
  <span title="Chưa có dữ liệu" aria-label="Chưa có dữ liệu" className="text-muted-foreground">—</span>
);

function toForm(sample: SlmSample | null): FormState {
  if (!sample) {
    return {
      sampleType: "knowledge",
      split: "train",
      question: "",
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
    source: sample.source,
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
    source: orNull(form.source),
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
  else if (!isReady(form.intents)) errors.intents = "Danh sách ý định không được để trống.";
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
  onClose: () => void;
  onChanged: (sample: SlmSample, isNew: boolean) => void;
  onDelete: (sample: SlmSample) => void;
  onRestore: (sample: SlmSample) => void;
}

export function SlmSampleFormPanel(props: Props) {
  return <SlmSampleForm key={props.sample?.id ?? "new"} {...props} />;
}

function SlmSampleForm({ sample: listed, version, onClose, onChanged, onDelete, onRestore }: Props) {
  const detail = useFetched(`detail|${listed?.id ?? "new"}|${version}`, () =>
    listed ? adminSlmSampleApi.get(listed.id) : Promise.resolve(null),
  );
  const sample = detail.data?.sample ?? listed;
  const versions = detail.data?.versions ?? [];
  const [form, setForm] = useState<FormState>(() => toForm(listed));
  const [editing, setEditing] = useState<EditingTable | null>(null);
  const [errors, setErrors] = useState<FieldErrors>({});
  const [submitError, setSubmitError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const [inspecting, setInspecting] = useState<SlmSampleVersion | null>(null);
  const [restoringVersion, setRestoringVersion] = useState(false);

  const isCreate = sample === null;
  const isDeleted = sample?.deletedAt != null;
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
        toast.add({ type: "success", title: "Đã lưu mẫu" });
        setEditing(null);
        onChanged(saved, false);
      } else {
        const created = await adminSlmSampleApi.create(toPayload(form));
        toast.add({ type: "success", title: "Đã thêm mẫu huấn luyện" });
        onChanged(created, true);
      }
    } catch (error) {
      setSubmitError(errorText(error, "Không lưu được mẫu."));
    } finally {
      setSaving(false);
    }
  };

  const restoreOldVersion = async (target: SlmSampleVersion) => {
    if (!sample) return;
    setRestoringVersion(true);
    try {
      const saved = await adminSlmSampleApi.restoreVersion(sample.id, target.version);
      toast.add({ type: "success", title: `Đã khôi phục nội dung phiên bản v${target.version} (tạo v${saved.version})` });
      setInspecting(null);
      onChanged(saved, false);
    } catch (error) {
      toast.add({ type: "error", title: "Không khôi phục được phiên bản", description: errorText(error, "Vui lòng thử lại sau.") });
    } finally {
      setRestoringVersion(false);
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
      label: "Nguồn",
      inputId: "slm-source-text",
      value: (
        <Input
          id="slm-source-text"
          value={form.source}
          maxLength={256}
          placeholder="Nhập tay"
          disabled={inputsOff}
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
      <DetailPanel
        title="Thêm mẫu mới"
        description="Điền đầy đủ câu hỏi và danh sách ý định (mảng JSON không rỗng)."
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
          {submitError && <Alert variant="destructive"><AlertDescription>{submitError}</AlertDescription></Alert>}
          <TableFrame title="Thông tin mẫu">
            <RowsTable rows={infoRows} isEditing />
          </TableFrame>
          <TableFrame title="Nhãn đầu ra">
            <RowsTable rows={labelRows} isEditing />
          </TableFrame>
        </form>
      </DetailPanel>
    );
  }

  if (!sample) return null;

  const editState = (table: EditingTable): TableEditState => ({
    isEditing: editing === table,
    canEdit: !isDeleted && editing === null && !saving,
    readOnly: isDeleted,
    isSaving: saving,
    hasChanges,
    error: editing === table ? submitError : null,
    onEdit: () => startEdit(table),
    onCancel: stopEdit,
    onSave: () => void save(),
  });

  const pre = (text: string | null) =>
    text && text.trim() !== "" ? (
      <span className="block whitespace-pre-wrap break-words text-left">{text}</span>
    ) : (
      <Missing />
    );

  const viewInfoRows: TableRow[] = [
    { label: "Phiên bản", value: `v${sample.version}` },
    { label: "Loại mẫu", value: sampleTypeLabel(sample.sampleType) },
    { label: "Tập dữ liệu", value: sample.split },
    { label: "Câu hỏi", value: pre(sample.question) },
    { label: "Nguồn", value: SOURCE_LABELS[sample.source] ?? sample.source },
    { label: "Câu hệ thống đã hỏi", value: pre(sample.pendingQuestion) },
    { label: "Nhãn rà soát", value: sample.sourceReview ?? <Missing /> },
    { label: "Người tạo", value: sample.createdByName ?? <Missing /> },
    { label: "Ngày tạo", value: formatDateTime(sample.createdAt) },
    { label: "Cập nhật", value: formatDateTime(sample.updatedAt ?? sample.createdAt) },
    { label: "Ngày xuất gần nhất", value: sample.exportedAt ? formatDateTime(sample.exportedAt) : <Missing /> },
  ];

  const jsonBlocks: { label: string; text: string | null }[] = [
    { label: "Ý định", text: sample.intents },
    { label: "Hồ sơ", text: sample.profile },
    { label: "Ngữ cảnh", text: sample.context },
  ];

  const busy = saving || editing !== null;

  return (
    <DetailPanel
      title="Chi tiết mẫu huấn luyện"
      description={sampleTypeLabel(sample.sampleType)}
      onClose={onClose}
      closeDisabled={saving}
      footer={isDeleted ? (
        <Button type="button" size="sm" variant="outline" disabled={busy} onClick={() => onRestore(sample)}>
          <RotateCcwIcon /> Khôi phục mẫu
        </Button>
      ) : (
        <Button type="button" size="sm" variant="outline" className="text-destructive hover:text-destructive" disabled={busy} onClick={() => onDelete(sample)}>
          <Trash2Icon /> Xóa
        </Button>
      )}
    >
      <div className="space-y-5">
        {detail.error && <Alert variant="destructive"><AlertDescription>{detail.error}</AlertDescription></Alert>}

        {isDeleted && (
          <Alert>
            <AlertDescription>Mẫu này đã bị xóa (chỉ đọc). Khôi phục mẫu để tiếp tục sử dụng hoặc chỉnh sửa.</AlertDescription>
          </Alert>
        )}

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
                    <Badge variant={ver.version === sample.version ? "brand" : "neutral"}>v{ver.version}</Badge>
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
                <DialogTitle>Phiên bản v{inspecting.version}</DialogTitle>
                <DialogDescription>
                  {versionActionLabel(inspecting.action, inspecting.restoredFromVersion)} · {inspecting.actorName ?? "Hệ thống"} · {formatDateTime(inspecting.createdAt)}
                </DialogDescription>
              </DialogHeader>
              <DialogBody className="space-y-3 text-sm">
                <div><span className="text-muted-foreground">Loại mẫu:</span> {sampleTypeLabel(inspecting.sampleType)} · <span className="text-muted-foreground">Tập:</span> {inspecting.split} · <span className="text-muted-foreground">Nguồn:</span> {SOURCE_LABELS[inspecting.source] ?? inspecting.source}</div>
                <div>
                  <div className="text-xs text-muted-foreground">Câu hỏi</div>
                  <div className="whitespace-pre-wrap rounded-md border border-border bg-muted/40 p-2">{inspecting.question}</div>
                </div>
                {inspecting.pendingQuestion && (
                  <div>
                    <div className="text-xs text-muted-foreground">Câu hệ thống đã hỏi</div>
                    <div className="whitespace-pre-wrap rounded-md border border-border bg-muted/40 p-2">{inspecting.pendingQuestion}</div>
                  </div>
                )}
                <div>
                  <div className="text-xs text-muted-foreground">Ý định</div>
                  <pre className="max-h-48 overflow-auto whitespace-pre-wrap break-words rounded-md border border-border bg-muted/40 p-2 font-mono text-xs">{prettyJson(inspecting.intents)}</pre>
                </div>
                {inspecting.profile && (
                  <div>
                    <div className="text-xs text-muted-foreground">Hồ sơ</div>
                    <pre className="max-h-36 overflow-auto whitespace-pre-wrap break-words rounded-md border border-border bg-muted/40 p-2 font-mono text-xs">{prettyJson(inspecting.profile)}</pre>
                  </div>
                )}
                {inspecting.context && (
                  <div>
                    <div className="text-xs text-muted-foreground">Ngữ cảnh</div>
                    <pre className="max-h-36 overflow-auto whitespace-pre-wrap break-words rounded-md border border-border bg-muted/40 p-2 font-mono text-xs">{prettyJson(inspecting.context)}</pre>
                  </div>
                )}
              </DialogBody>
              <DialogFooter layout={!isDeleted && inspecting.version !== sample.version ? "pair" : "single"}>
                <DialogCancel disabled={restoringVersion} onClick={() => setInspecting(null)}>
                  Đóng
                </DialogCancel>
                {!isDeleted && inspecting.version !== sample.version && (
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
