"use client";

import { AlertCircleIcon, ArrowLeftIcon, Loader2Icon, MessageCircleIcon, SparklesIcon } from "lucide-react";
import { Button } from "@/components/ui/button";
import { ProfileReviewCards } from "@/components/flow/ProfileReviewCards";
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
  isSubmitting,
  submitError,
}: Step6ReviewProps) {
  const stepErrors = splitStepErrors(submitError);

  return (
    <div className="space-y-8 animate-in fade-in-50 duration-200">
      {/* Step Header */}
      <div className="space-y-2 border-b border-line pb-4">
        <div className="text-xs font-semibold text-brand">
          Bước 6/6 · khoảng 1 phút
        </div>
        <h1 className="text-h1 text-foreground">Xem lại hồ sơ của bạn</h1>
        <p className="text-body text-muted-foreground">
          Kiểm tra nhanh một lượt. Bấm &quot;Sửa&quot; ở mục nào bạn muốn chỉnh lại trước khi gửi.
        </p>
      </div>

      <ProfileReviewCards
        draft={draft}
        metadata={metadata}
        draftTranscript={draftTranscript}
        onEditStep={onEditStep}
        errorsByStep={stepErrors.byStep}
      />

      {/* Helpful Hint */}
      <div className="rounded-xl border border-brand/20 bg-brand-wash/30 p-4 text-xs text-muted-foreground leading-relaxed">
        💡 <strong className="text-brand-ink">Lưu ý: </strong>
        Ưu tiên chi phí, học bổng hay danh tiếng bạn sẽ chọn ngay ở trang kết quả gợi ý trường,
        và có thể đổi lại bao nhiêu lần cũng được!
      </div>

      {/* Lỗi khi gửi: lỗi theo bước đã hiện ở thẻ bên trên, ở đây nhắc lại và hiện lỗi chung */}
      {submitError && (
        <div className="flex gap-2 rounded-xl border border-destructive/20 bg-destructive/10 p-3 text-xs font-medium text-destructive">
          <AlertCircleIcon className="mt-0.5 size-4 shrink-0" />
          <div className="space-y-1">
            {Object.keys(stepErrors.byStep).length > 0 && (
              <div>Hồ sơ còn lỗi ở các mục viền đỏ bên trên. Bấm &quot;Sửa&quot; ở mục đó để chỉnh lại.</div>
            )}
            {stepErrors.other.map((e, i) => (
              <div key={i}>{e}</div>
            ))}
          </div>
        </div>
      )}

      {/* Bottom Actions */}
      <div className="flex justify-between pt-4 border-t border-line">
        <Button variant="outline" onClick={onPrev} size="lg" className="gap-1.5">
          <ArrowLeftIcon className="size-4" />
          Quay lại
        </Button>
        <Button
          onClick={onSubmit}
          disabled={isSubmitting}
          size="lg"
          className="gap-2 bg-brand hover:bg-brand-ink text-white font-semibold px-6"
        >
          {isSubmitting ? (
            <>
              <Loader2Icon className="size-4 animate-spin" />
              {backToChat ? "Đang lưu hồ sơ..." : "Đang lưu và phân tích gợi ý..."}
            </>
          ) : backToChat ? (
            <>
              <MessageCircleIcon className="size-4" />
              Lưu và quay về chat
            </>
          ) : (
            <>
              <SparklesIcon className="size-4" />
              Nhận gợi ý trường
            </>
          )}
        </Button>
      </div>
    </div>
  );
}
