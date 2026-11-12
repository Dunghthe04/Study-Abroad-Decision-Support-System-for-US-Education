"use client";

import { useState } from "react";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { OptionSelect } from "@/components/admin/universities/OptionSelect";
import { DetailPanel } from "@/components/admin/shared/DetailPanel";
import { Spinner } from "@/components/ui/spinner";
import { Textarea } from "@/components/ui/textarea";
import { jsonProblem, SAMPLE_TYPES, SPLITS } from "@/lib/slm-sample-format";
import { adminSlmSampleApi } from "@/lib/slm-sample-api";
import type { SlmSample, SlmSampleType, SlmSplit } from "@/types/slm-sample";

interface FormState {
  sampleType: SlmSampleType;
  question: string;
  profile: string;
  pendingQuestion: string;
  intents: string;
  context: string;
  split: SlmSplit;
  sourceReview: string;
}

type FieldErrors = Partial<Record<"question" | "profile" | "intents" | "context", string>>;

function initialState(sample: SlmSample | null): FormState {
  if (!sample) {
    return {
      sampleType: "collect_profile",
      question: "",
      profile: "{}",
      pendingQuestion: "",
      intents: "[]",
      context: "{}",
      split: "train",
      sourceReview: "",
    };
  }
  return {
    sampleType: sample.sampleType,
    question: sample.question,
    profile: sample.profile,
    pendingQuestion: sample.pendingQuestion ?? "",
    intents: sample.intents,
    context: sample.context,
    split: sample.split,
    sourceReview: sample.sourceReview ?? "",
  };
}

function validate(form: FormState): FieldErrors {
  const errors: FieldErrors = {};
  if (form.question.trim() === "") errors.question = "Không được để trống.";
  const profileProblem = jsonProblem(form.profile);
  if (profileProblem) errors.profile = profileProblem;
  const intentsProblem = jsonProblem(form.intents);
  if (intentsProblem) errors.intents = intentsProblem;
  const contextProblem = jsonProblem(form.context);
  if (contextProblem) errors.context = contextProblem;
  return errors;
}

interface Props {
  sample: SlmSample | null;
  onClose: () => void;
  onSaved: (isNew: boolean) => void;
}

const TYPE_OPTIONS = SAMPLE_TYPES.map((item) => ({ value: item.id, label: item.label }));
const SPLIT_OPTIONS = SPLITS.map((item) => ({ value: item.id, label: item.label }));

export function SlmSampleFormPanel({ sample, onClose, onSaved }: Props) {
  return <SlmSampleForm key={sample?.id ?? "new"} sample={sample} onClose={onClose} onSaved={onSaved} />;
}

function SlmSampleForm({ sample, onClose, onSaved }: Props) {
  const [form, setForm] = useState<FormState>(() => initialState(sample));
  const [errors, setErrors] = useState<FieldErrors>({});
  const [saving, setSaving] = useState(false);
  const [submitError, setSubmitError] = useState<string | null>(null);

  const patch = (change: Partial<FormState>) => setForm((current) => ({ ...current, ...change }));

  const submit = async (event: React.FormEvent) => {
    event.preventDefault();
    const found = validate(form);
    setErrors(found);
    if (Object.keys(found).length > 0) return;

    setSaving(true);
    setSubmitError(null);
    const payload = {
      sampleType: form.sampleType,
      question: form.question.trim(),
      profile: form.profile,
      pendingQuestion: form.pendingQuestion.trim() === "" ? null : form.pendingQuestion.trim(),
      intents: form.intents,
      context: form.context,
      split: form.split,
      sourceReview: form.sourceReview.trim() === "" ? null : form.sourceReview.trim(),
    };
    try {
      if (sample) await adminSlmSampleApi.update(sample.id, payload);
      else await adminSlmSampleApi.create(payload);
      onSaved(sample === null);
    } catch (error) {
      setSubmitError(error instanceof Error ? error.message : "Không lưu được mẫu.");
    } finally {
      setSaving(false);
    }
  };

  return (
    <DetailPanel
      title={sample ? "Sửa mẫu huấn luyện" : "Thêm mẫu huấn luyện"}
      description="Hồ sơ, ý định và ngữ cảnh phải là JSON hợp lệ."
      onClose={onClose}
      closeDisabled={saving}
    >
    <form onSubmit={submit} className="flex flex-col gap-4">

      <div className="grid gap-3 sm:grid-cols-2">
        <label className="flex flex-col gap-1 text-xs text-ink-2">
          Loại mẫu
          <OptionSelect value={form.sampleType} options={TYPE_OPTIONS} onChange={(value) => patch({ sampleType: value as SlmSampleType })} />
        </label>
        <label className="flex flex-col gap-1 text-xs text-ink-2">
          Tập dữ liệu
          <OptionSelect value={form.split} options={SPLIT_OPTIONS} onChange={(value) => patch({ split: value as SlmSplit })} />
        </label>
      </div>

      <label className="flex flex-col gap-1 text-xs text-ink-2">
        Câu hỏi
        <Textarea value={form.question} rows={2} aria-invalid={errors.question ? true : undefined} onChange={(event) => patch({ question: event.target.value })} />
        {errors.question ? <span className="text-destructive">{errors.question}</span> : null}
      </label>

      <label className="flex flex-col gap-1 text-xs text-ink-2">
        Câu hỏi đang chờ
        <Input value={form.pendingQuestion} onChange={(event) => patch({ pendingQuestion: event.target.value })} />
      </label>

      <JsonField label="Hồ sơ (JSON)" value={form.profile} error={errors.profile} onChange={(value) => patch({ profile: value })} />
      <JsonField label="Ý định (JSON)" value={form.intents} error={errors.intents} onChange={(value) => patch({ intents: value })} />
      <JsonField label="Ngữ cảnh (JSON)" value={form.context} error={errors.context} onChange={(value) => patch({ context: value })} />

      <label className="flex flex-col gap-1 text-xs text-ink-2">
        Nguồn
        <Input value={form.sourceReview} onChange={(event) => patch({ sourceReview: event.target.value })} />
      </label>

      {submitError ? <Alert variant="destructive"><AlertDescription>{submitError}</AlertDescription></Alert> : null}

      <div className="flex justify-end gap-2">
        <Button type="button" variant="outline" disabled={saving} onClick={onClose}>Hủy</Button>
        <Button type="submit" disabled={saving}>{saving && <Spinner />} Lưu</Button>
      </div>
    </form>
    </DetailPanel>
  );
}

function JsonField({ label, value, error, onChange }: { label: string; value: string; error?: string; onChange: (value: string) => void }) {
  return (
    <label className="flex flex-col gap-1 text-xs text-ink-2">
      {label}
      <Textarea value={value} rows={4} spellCheck={false} aria-invalid={error ? true : undefined} className="font-mono text-xs" onChange={(event) => onChange(event.target.value)} />
      {error ? <span className="text-destructive">{error}</span> : null}
    </label>
  );
}
