"use client";

import { useState } from "react";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { NativeSelect } from "@/components/ui/native-select";
import { ChoiceGroup } from "@/components/admin/review-batches/ChoiceGroup";
import { ApiError } from "@/lib/api";
import { formatDateTime } from "@/lib/ai-usage-format";
import { adminReviewBatchApi } from "@/lib/review-batch-api";
import {
  ANSWER_VERDICT_OPTIONS, CAUSE_OPTIONS, formFromVerdict, INTENT_OPTIONS, LABEL_VERDICT_OPTIONS, needsCause, needsCorrectIntent,
  toRequest, validateVerdict, type VerdictForm as VerdictFormState,
} from "@/lib/review-batch-format";
import type { ReviewVerdict, SlmIntentType } from "@/types/review-batch";

interface Props {
  batchId: string;
  itemId: string;
  /** Kết luận đã lưu trước đó của chính người duyệt (null nếu chưa chấm). */
  initial: ReviewVerdict | null;
  /** true khi sau câu này không còn câu nào chưa chấm: nút đổi thành "Lưu" thay vì "Lưu và sang câu kế". */
  isLastToGrade: boolean;
  onSaved: () => void;
}

/** Biểu mẫu chấm một câu: nhãn SLM gán đúng không, câu trả lời đúng không và vì sao sai. Gắn key theo câu để mỗi câu là một biểu mẫu mới. */
export function VerdictForm({ batchId, itemId, initial, isLastToGrade, onSaved }: Props) {
  const [form, setForm] = useState<VerdictFormState>(() => formFromVerdict(initial));
  const [pending, setPending] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const problem = validateVerdict(form);
  const touched = form.label !== null || form.answer !== null;

  const save = async () => {
    const request = toRequest(form);
    if (!request) return;
    setPending(true);
    setError(null);
    try {
      await adminReviewBatchApi.saveVerdict(batchId, itemId, request);
      onSaved();
    } catch (cause) {
      setError(cause instanceof ApiError || cause instanceof Error ? cause.message : "Không lưu được kết luận.");
      setPending(false);
    }
  };

  return (
    <section className="flex flex-col gap-4 border-t border-line pt-4">
      {initial ? (
        <p className="text-xs text-ink-3">Bạn đã chấm câu này lúc {formatDateTime(initial.updatedAt)}. Có thể sửa và lưu lại.</p>
      ) : null}

      <div className="flex flex-col gap-2">
        <h3 className="text-sm font-semibold text-ink">1. Nhãn SLM gán cho câu hỏi có đúng không?</h3>
        <ChoiceGroup
          label="Nhãn SLM gán" value={form.label} options={LABEL_VERDICT_OPTIONS}
          onChange={(label) => setForm({ ...form, label })}
        />
        {needsCorrectIntent(form) ? (
          <label className="flex flex-col gap-1 text-sm text-ink">
            Nhãn đúng phải là gì?
            <NativeSelect
              value={form.correctIntent ?? ""}
              onChange={(event) => setForm({ ...form, correctIntent: (event.target.value || null) as SlmIntentType | null })}
            >
              <option value="">Không chắc</option>
              {INTENT_OPTIONS.map((option) => <option key={option.id} value={option.id}>{option.label}</option>)}
            </NativeSelect>
          </label>
        ) : null}
      </div>

      <div className="flex flex-col gap-2">
        <h3 className="text-sm font-semibold text-ink">2. Câu trả lời của hệ thống</h3>
        <ChoiceGroup
          label="Đánh giá câu trả lời" value={form.answer} options={ANSWER_VERDICT_OPTIONS}
          onChange={(answer) => setForm({ ...form, answer })}
        />
        {needsCause(form) ? (
          <div className="flex flex-col gap-1.5">
            <p className="text-sm text-ink">Vì sao sai?</p>
            <ChoiceGroup label="Nguyên nhân sai" value={form.cause} options={CAUSE_OPTIONS} onChange={(cause) => setForm({ ...form, cause })} />
          </div>
        ) : null}
      </div>

      {touched && problem ? <p className="text-xs text-risk">{problem}</p> : null}
      {error ? <Alert variant="destructive"><AlertDescription>{error}</AlertDescription></Alert> : null}

      <div>
        <Button type="button" disabled={pending || problem !== null} onClick={() => void save()}>
          {pending ? "Đang lưu..." : isLastToGrade ? "Lưu" : "Lưu và sang câu kế"}
        </Button>
      </div>
    </section>
  );
}
