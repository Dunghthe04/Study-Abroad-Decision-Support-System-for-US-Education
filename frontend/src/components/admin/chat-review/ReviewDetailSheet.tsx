"use client";

import { useState, type ReactNode } from "react";
import { ShieldAlertIcon } from "lucide-react";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Sheet, SheetContent, SheetDescription, SheetHeader, SheetTitle } from "@/components/ui/sheet";
import { ApiError } from "@/lib/api";
import { formatDateTime, formatMs } from "@/lib/ai-usage-format";
import { adminChatReviewApi } from "@/lib/chat-review-api";
import {
  failedStepLabel, formatConfidence, intentLabel, outcomeLabel, outOfScopeLabel, queueReason, statusLabel,
} from "@/lib/chat-review-format";
import { useFetched } from "@/lib/use-fetched";
import type { ChatReviewDetail, ChatReviewModelDetail, ChatReviewStatus } from "@/types/chat-review";

interface Props {
  /** null = đóng. Chi tiết chỉ được tải khi mở một câu, vì mỗi lần tải được ghi nhật ký người xem. */
  messageId: string | null;
  onClose: () => void;
  /** Gọi sau khi đổi trạng thái thành công (để tải lại danh sách). */
  onChanged: () => void;
}

const ROLE_LABELS: Record<string, string> = { user: "Học sinh", assistant: "Hệ thống", system: "Hệ thống (lỗi)" };

export function ReviewDetailSheet({ messageId, onClose, onChanged }: Props) {
  return (
    <Sheet open={messageId !== null} onOpenChange={(open) => { if (!open) onClose(); }}>
      <SheetContent side="right" className="w-full gap-0 overflow-y-auto sm:max-w-2xl">
        <div className="flex flex-col gap-4 p-4">
          {messageId ? <Body key={messageId} messageId={messageId} onChanged={onChanged} /> : null}
        </div>
      </SheetContent>
    </Sheet>
  );
}

function Body({ messageId, onChanged }: { messageId: string; onChanged: () => void }) {
  // Nội dung chat chỉ nằm trong bộ nhớ của khung này, mất khi đóng: không ghi vào localStorage hay URL
  const detail = useFetched(`detail|${messageId}`, () => adminChatReviewApi.detail(messageId));
  const data = detail.data;

  return (
    <>
      <SheetHeader className="p-0">
        <SheetTitle>Chi tiết câu</SheetTitle>
        <SheetDescription>
          Đây là nội dung chat của học sinh. Lần mở này đã được ghi vào nhật ký (ai xem, lúc nào).
        </SheetDescription>
      </SheetHeader>

      {detail.error ? (
        <Alert variant="destructive"><AlertDescription>{detail.error}</AlertDescription></Alert>
      ) : null}
      {!data && !detail.error ? <p className="text-sm text-ink-2">Đang tải...</p> : null}
      {data ? <Content data={data} onChanged={onChanged} /> : null}
    </>
  );
}

function Content({ data, onChanged }: { data: ChatReviewDetail; onChanged: () => void }) {
  const model = data.question?.model ?? null;
  const reason = queueReason({
    role: data.role, outcome: data.outcome, failedSteps: data.failedSteps, error: data.error, model,
  });

  return (
    <>
      <div className="flex flex-wrap items-center gap-2">
        <Badge variant={data.outcome === "failed" ? "risk" : "warn"}>{outcomeLabel(data.outcome)}</Badge>
        <Badge variant={data.reviewStatus === "open" ? "brand" : "neutral"}>{statusLabel(data.reviewStatus)}</Badge>
        <span className="tabular text-xs text-ink-3">{formatDateTime(data.createdAt)}</span>
        {data.turnMs != null ? <span className="tabular text-xs text-ink-3">· xử lý {formatMs(data.turnMs)}</span> : null}
      </div>

      <Block title="Vì sao vào hàng đợi">
        <p className="text-ink">{reason.label}</p>
        {reason.detail ? <p className="text-xs text-ink-3">{reason.detail}</p> : null}
        {data.failedSteps.length > 0 ? (
          <p className="text-xs text-ink-3">Bước lỗi: {data.failedSteps.map(failedStepLabel).join(", ")}</p>
        ) : null}
      </Block>

      {data.question ? (
        <Block title="Câu hỏi của học sinh">
          <Text>{data.question.content}</Text>
          {model ? <ModelLabels model={model} /> : <p className="text-xs text-ink-3">Lượt này không qua SLM nên không có nhãn.</p>}
        </Block>
      ) : (
        <Block title="Câu hỏi của học sinh">
          <p className="text-sm text-ink-2">Không có tin hỏi đi kèm (ví dụ advisor không phản hồi).</p>
        </Block>
      )}

      <Block title={data.role === "system" ? "Thông báo lỗi hệ thống" : "Hệ thống đã trả lời"}>
        <Text>{data.reply.content}</Text>
      </Block>

      {data.context.length > 0 ? (
        <details className="rounded-md border border-line">
          <summary className="cursor-pointer px-3 py-2 text-sm font-medium text-ink">Các tin trước đó ({data.context.length})</summary>
          <ul className="flex flex-col gap-2 border-t border-line p-3">
            {data.context.map((message) => (
              <li key={message.messageId} className="text-sm">
                <span className="text-xs font-medium text-ink-3">{ROLE_LABELS[message.role] ?? message.role} · {formatDateTime(message.createdAt)}</span>
                <Text>{message.content}</Text>
              </li>
            ))}
          </ul>
        </details>
      ) : null}

      <Actions key={data.reviewStatus} messageId={data.messageId} current={data.reviewStatus} onChanged={onChanged} />
    </>
  );
}

export function Block({ title, children }: { title: string; children: ReactNode }) {
  return (
    <section className="flex flex-col gap-1.5">
      <h3 className="text-sm font-semibold text-ink">{title}</h3>
      <div className="flex flex-col gap-1.5 rounded-md bg-ground p-3">{children}</div>
    </section>
  );
}

/** Nội dung chat hiện dạng chữ thường (React tự thoát ký tự), giữ xuống dòng của người viết. */
export function Text({ children }: { children: string }) {
  return <p className="whitespace-pre-wrap break-words text-sm text-ink">{children}</p>;
}

export function ModelLabels({ model }: { model: ChatReviewModelDetail }) {
  const confidence = formatConfidence(model.confidence);
  return (
    <div className="mt-1 flex flex-col gap-1.5 border-t border-line pt-2">
      <p className="text-xs font-medium text-ink-2">SLM đã gán nhãn</p>
      <div className="flex flex-wrap items-center gap-1.5 text-xs text-ink-3">
        {confidence ? <span>Độ tin cậy {confidence}</span> : null}
        {model.fallback ? <Badge variant="warn">Dùng dự phòng</Badge> : null}
        {model.outOfScope ? <Badge variant="neutral">{outOfScopeLabel(model.outOfScope)}</Badge> : null}
      </div>
      {model.modelIntents.length > 0 ? (
        <ul className="flex flex-col gap-1">
          {model.modelIntents.map((intent, index) => (
            <li key={index} className="text-xs text-ink-2">
              <span className="font-medium text-ink">{intentLabel(intent.type)}</span>
              {intent.span ? <span> · &ldquo;{intent.span}&rdquo;</span> : null}
              {intent.entities && Object.keys(intent.entities).length > 0 ? (
                <code className="mt-0.5 block break-words rounded bg-surface px-1.5 py-0.5 text-[13px] text-ink-3">{JSON.stringify(intent.entities)}</code>
              ) : null}
            </li>
          ))}
        </ul>
      ) : (
        <p className="text-xs text-ink-3">Model không trả ý nào (SLM hỏng nên không có đầu ra thật).</p>
      )}
    </div>
  );
}

const ACTIONS: Record<ChatReviewStatus, { status: ChatReviewStatus; label: string; primary?: boolean }[]> = {
  open: [
    { status: "resolved", label: "Đã xử lý", primary: true },
    { status: "ignored", label: "Bỏ qua" },
  ],
  resolved: [{ status: "open", label: "Mở lại" }],
  ignored: [{ status: "open", label: "Mở lại" }],
};

function Actions({ messageId, current, onChanged }: { messageId: string; current: ChatReviewStatus; onChanged: () => void }) {
  const [pending, setPending] = useState<ChatReviewStatus | null>(null);
  const [error, setError] = useState<string | null>(null);

  const change = async (status: ChatReviewStatus) => {
    setPending(status);
    setError(null);
    try {
      await adminChatReviewApi.setStatus(messageId, status);
      onChanged();
    } catch (cause) {
      setError(cause instanceof ApiError || cause instanceof Error ? cause.message : "Không đổi được trạng thái.");
      setPending(null);
    }
  };

  return (
    <section className="flex flex-col gap-2 border-t border-line pt-3">
      <p className="flex items-center gap-1.5 text-xs text-ink-3">
        <ShieldAlertIcon aria-hidden className="size-3.5" />
        Mỗi lần đổi trạng thái cũng được ghi nhật ký.
      </p>
      {error ? <Alert variant="destructive"><AlertDescription>{error}</AlertDescription></Alert> : null}
      <div className="flex flex-wrap gap-2">
        {ACTIONS[current].map((action) => (
          <Button
            key={action.status} type="button" variant={action.primary ? "default" : "outline"}
            disabled={pending !== null} onClick={() => void change(action.status)}
          >
            {pending === action.status ? "Đang lưu..." : action.label}
          </Button>
        ))}
      </div>
    </section>
  );
}
