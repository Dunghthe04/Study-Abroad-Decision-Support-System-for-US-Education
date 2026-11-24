"use client";

import { AlertCircleIcon, Loader2Icon, MessageCircleIcon, SaveIcon, SparklesIcon } from "lucide-react";
import { Button } from "@/components/ui/button";
import { ProfileReviewCards } from "@/components/flow/ProfileReviewCards";
import { FormBody, FormGroup } from "./FormKit";
import { StepHeader, StepNav } from "./StepChrome";
import type { DraftTranscriptFile, ProfileFlowDraft, ProfileMetadataResponse } from "@/types/profile-flow";

// BE trả lỗi khi gửi hồ sơ thành một chuỗi "Bước 1: ... Bước 3: ..." → tách theo bước để hiện ở thẻ của bước đó
function splitStepErrors(message: string | null | undefined) {
  const byStep: Record<number, string[]> = {};
  const other: string[] = [];
  for (const part of (message ?? "").split(/(?=Bước \d+: )/)) {
    const text = part.trim();
    if (!text) continue;
    const m = /^Bước (\d+): ([\s\S]+)$/.exec(text);
    if (m) {
      const detail = m[2].charAt(0).toUpperCase() + m[2].slice(1);
      (byStep[Number(m[1])] ??= []).push(detail);
    } else {
      other.push(text);
    }
  }
  return { byStep, other };
}

interface Step6ReviewProps {
  draft: ProfileFlowDraft;
  draftTranscript?: DraftTranscriptFile | null;
  metadata: ProfileMetadataResponse | null;
  onEditStep: (stepNumber: number) => void;
  onPrev: () => void;
  onSubmit: () => void;
  /** Form mở từ chat: nút cuối lưu hồ sơ rồi quay về chat thay vì gợi ý trường */
  backToChat?: boolean;
  /** Tích "Chỉ sửa hồ sơ": nút chỉ lưu, không gợi ý trường, không về chat */
  editOnly?: boolean;
  /** Nhãn nút gửi khi kết quả trả lời trong chat, theo việc học sinh cần (xem đánh giá, xem lộ trình, quay về chat) */
  chatLabel?: string;
  isSubmitting: boolean;
  /** Lỗi BE trả về khi gửi hồ sơ */
  submitError?: string | null;
}

export function Step6Review({
  draft,
  draftTranscript,
  metadata,
  onEditStep,
  onPrev,
  onSubmit,
  backToChat = false,
  editOnly = false,
  chatLabel = "Lưu và quay về chat",
  isSubmitting,
  submitError,
}: Step6ReviewProps) {
  const stepErrors = splitStepErrors(submitError);

  const hasNote = !backToChat && !editOnly;
  return (
    <>
      <StepHeader title="Xem lại hồ sơ của bạn" />
      <FormBody lede="Kiểm tra lại một lượt trước khi gửi.">
        <FormGroup title="Hồ sơ của bạn">
        <ProfileReviewCards
          draft={draft}
          metadata={metadata}
          draftTranscript={draftTranscript}
          onEditStep={onEditStep}
          errorsByStep={stepErrors.byStep}
        />

        {(hasNote || submitError) && (
          <div className="space-y-4 py-6 text-sm">
            {hasNote && <p className="text-ink-2">Bạn sẽ chọn ưu tiên (chi phí, học bổng, danh tiếng) ở trang kết quả.</p>}
            {/* Lỗi khi gửi: lỗi theo bước đã hiện ở mục của bước đó, ở đây nhắc lại và hiện lỗi chung */}
            {submitError && (
              <div role="alert" className="flex gap-2 font-medium text-destructive">
                <AlertCircleIcon aria-hidden className="mt-0.5 size-4 shrink-0" />
                <div className="space-y-1">
                  {Object.keys(stepErrors.byStep).length > 0 && (
                    <div>Hồ sơ còn lỗi ở các mục có chữ đỏ bên trên. Bấm &quot;Sửa&quot; ở mục đó để chỉnh lại.</div>
                  )}
                  {stepErrors.other.map((e, i) => (
                    <div key={i}>{e}</div>
                  ))}
                </div>
              </div>
            )}
          </div>
        )}
        </FormGroup>
      </FormBody>

      <StepNav onPrev={onPrev}>
        <Button onClick={onSubmit} disabled={isSubmitting} size="lg" className="gap-2 px-6 font-semibold">
          {isSubmitting ? (
            <>
              <Loader2Icon className="size-4 animate-spin" />
              {backToChat || editOnly ? "Đang lưu hồ sơ..." : "Đang lưu và phân tích gợi ý..."}
            </>
          ) : editOnly ? (
            <>
              <SaveIcon className="size-4" />
              Lưu hồ sơ
            </>
          ) : backToChat ? (
            <>
              <MessageCircleIcon className="size-4" />
              {chatLabel}
            </>
          ) : (
            <>
              <SparklesIcon className="size-4" />
              Nhận gợi ý trường
            </>
          )}
        </Button>
      </StepNav>
    </>
  );
}
