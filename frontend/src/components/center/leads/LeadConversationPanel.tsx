"use client";

import React, { useCallback, useEffect, useMemo, useRef, useState } from "react";
import {
  AlertTriangleIcon,
  MessageSquareIcon,
  RefreshCwIcon,
  SendIcon,
  ShieldAlertIcon,
} from "lucide-react";
import { ApiError, centerLeadsApi } from "@/lib/api";
import {
  MAX_LEAD_MESSAGE_LENGTH,
  formatLeadMessageRoleLabel,
  inspectLeadMessageDraft,
} from "@/lib/lead-message-filter";
import type { LeadMessageDto, LeadMessagesThreadDto } from "@/types/api";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Spinner } from "@/components/ui/spinner";
import { Textarea } from "@/components/ui/textarea";
import { toast } from "@/components/ui/toast";
import { cn } from "@/lib/utils";

interface LeadConversationPanelProps {
  leadId: string;
  leadStatus: string;
  onLeadStatusChange?: (newStatus: string) => void;
}

function getLoadThreadErrorMessage(err: unknown): string {
  return err instanceof ApiError
    ? err.message
    : "Không thể tải lịch sử trao đổi tin nhắn.";
}

/**
 * [USAS-2053 / UC-74] Khu vực đọc và trả lời tin nhắn trao đổi giữa Trung tâm tư vấn và Học sinh/Phụ huynh.
 * Áp dụng kiểm tra đầu vào hai lớp (client + server) và hiển thị cảnh báo nếu bộ lọc gắn cờ số điện thoại/quảng cáo.
 */
export function LeadConversationPanel({
  leadId,
  leadStatus,
  onLeadStatusChange,
}: LeadConversationPanelProps) {
  const [messages, setMessages] = useState<LeadMessageDto[]>([]);
  const [flaggedCount, setFlaggedCount] = useState(0);
  const [showFlaggedOnly, setShowFlaggedOnly] = useState(false);
  const [lastSyncedAt, setLastSyncedAt] = useState<Date | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);

  const [draft, setDraft] = useState("");
  const [formError, setFormError] = useState<string | null>(null);
  const [isSending, setIsSending] = useState(false);

  const bottomRef = useRef<HTMLDivElement | null>(null);

  const applyThreadResult = useCallback((thread: LeadMessagesThreadDto) => {
    setMessages(thread.messages);
    setFlaggedCount(thread.flaggedCount);
    setLastSyncedAt(new Date());
    setLoadError(null);
    setIsLoading(false);
  }, []);

  const handleRefresh = useCallback(async () => {
    setIsLoading(true);
    setLoadError(null);
    try {
      const thread = await centerLeadsApi.getMessages(leadId);
      applyThreadResult(thread);
    } catch (err: unknown) {
      setLoadError(getLoadThreadErrorMessage(err));
      setIsLoading(false);
    }
  }, [leadId, applyThreadResult]);

  useEffect(() => {
    let ignore = false;

    centerLeadsApi
      .getMessages(leadId)
      .then((thread) => {
        if (!ignore) {
          applyThreadResult(thread);
        }
      })
      .catch((err: unknown) => {
        if (!ignore) {
          setLoadError(getLoadThreadErrorMessage(err));
          setIsLoading(false);
        }
      });

    return () => {
      ignore = true;
    };
  }, [leadId, applyThreadResult]);

  useEffect(() => {
    bottomRef.current?.scrollIntoView({ behavior: "smooth", block: "nearest" });
  }, [messages.length, showFlaggedOnly]);

  const trimmedDraft = draft.trim();
  const draftInspection = useMemo(
    () => inspectLeadMessageDraft(trimmedDraft),
    [trimmedDraft]
  );

  const visibleMessages = useMemo(() => {
    if (!showFlaggedOnly) return messages;
    return messages.filter((m) => m.isFlagged && m.status !== "removed");
  }, [messages, showFlaggedOnly]);

  /**
   * [USAS-2053 / UC-74] Kiểm tra hợp lệ phía client trước khi gọi API gửi phản hồi.
   */
  const submitMessage = async () => {
    if (isSending) return;

    if (!trimmedDraft) {
      setFormError("Vui lòng nhập nội dung tin nhắn trước khi gửi.");
      return;
    }

    if (trimmedDraft.length > MAX_LEAD_MESSAGE_LENGTH) {
      setFormError(`Nội dung tin nhắn tối đa ${MAX_LEAD_MESSAGE_LENGTH} ký tự.`);
      return;
    }

    setFormError(null);
    setIsSending(true);
    try {
      const result = await centerLeadsApi.sendMessage(leadId, trimmedDraft);
      setMessages((prev) => [...prev, result.message]);
      if (result.message.isFlagged) {
        setFlaggedCount((c) => c + 1);
      }
      setLastSyncedAt(new Date());
      setDraft("");
      if (showFlaggedOnly && !result.message.isFlagged) {
        setShowFlaggedOnly(false);
      }

      if (result.leadStatus && result.leadStatus !== leadStatus) {
        onLeadStatusChange?.(result.leadStatus);
      }

      if (result.message.isFlagged) {
        toast.add({
          type: "warning",
          title: "Tin nhắn đã gửi (có gắn cờ bộ lọc)",
          description:
            result.message.flagReason ??
            "Tin nhắn có dấu hiệu chứa số điện thoại hoặc quảng cáo nên đã được gắn cờ để kiểm duyệt.",
        });
      } else {
        toast.add({
          type: "success",
          title: "Đã gửi phản hồi",
          description: "Tin nhắn đã được lưu và gửi tới học sinh / phụ huynh.",
        });
      }
    } catch (err: unknown) {
      const msg =
        err instanceof ApiError
          ? err.message
          : "Không thể gửi tin nhắn. Vui lòng thử lại.";
      setFormError(msg);
      toast.add({
        type: "error",
        title: "Gửi tin nhắn thất bại",
        description: msg,
      });
    } finally {
      setIsSending(false);
    }
  };

  const handleFormSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    void submitMessage();
  };

  const handleKeyDown = (e: React.KeyboardEvent<HTMLTextAreaElement>) => {
    if ((e.ctrlKey || e.metaKey) && e.key === "Enter") {
      e.preventDefault();
      void submitMessage();
    }
  };

  return (
    <div className="space-y-3 pt-3 border-t">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div className="flex flex-wrap items-center gap-2">
          <h4 className="text-xs font-semibold uppercase tracking-wider text-muted-foreground flex items-center gap-1.5">
            <MessageSquareIcon className="size-4 text-brand" />
            Trao đổi & Trả lời tin nhắn ({messages.length})
          </h4>
          {flaggedCount > 0 && (
            <button
              type="button"
              onClick={() => setShowFlaggedOnly((prev) => !prev)}
              aria-pressed={showFlaggedOnly}
              title="Lọc chỉ xem các tin nhắn bị bộ lọc gắn cờ"
              className="inline-flex items-center"
            >
              <Badge
                variant={showFlaggedOnly ? "default" : "outline"}
                className={cn(
                  "text-[11px] gap-1 cursor-pointer transition-colors",
                  showFlaggedOnly
                    ? "bg-amber-600 text-white border-amber-600"
                    : "bg-amber-50 text-amber-800 border-amber-300 hover:bg-amber-100"
                )}
              >
                <AlertTriangleIcon className="size-3" />
                {flaggedCount} tin nhắn bị gắn cờ
                {showFlaggedOnly ? " (Đang lọc)" : ""}
              </Badge>
            </button>
          )}
        </div>

        <div className="flex items-center gap-2">
          {lastSyncedAt && !isLoading && (
            <span className="text-[11px] text-muted-foreground hidden sm:inline">
              Cập nhật{" "}
              {lastSyncedAt.toLocaleTimeString("vi-VN", {
                hour: "2-digit",
                minute: "2-digit",
              })}
            </span>
          )}
          <Button
            type="button"
            variant="ghost"
            size="sm"
            onClick={() => void handleRefresh()}
            disabled={isLoading}
            className="h-7 px-2 text-xs gap-1.5 text-muted-foreground"
          >
            <RefreshCwIcon className={cn("size-3", isLoading && "animate-spin")} />
            Tải lại
          </Button>
        </div>
      </div>

      {/* Message List */}
      <div className="rounded-lg border bg-muted/20 p-3 max-h-72 overflow-y-auto space-y-2.5">
        {isLoading ? (
          <div className="py-8 flex flex-col items-center justify-center gap-2 text-xs text-muted-foreground">
            <Spinner className="size-5 text-brand" />
            <span>Đang tải lịch sử tin nhắn...</span>
          </div>
        ) : loadError ? (
          <div className="py-6 text-center space-y-2">
            <p className="text-xs text-destructive">{loadError}</p>
            <Button
              type="button"
              variant="outline"
              size="sm"
              onClick={() => void handleRefresh()}
              className="h-7 text-xs"
            >
              Thử lại
            </Button>
          </div>
        ) : visibleMessages.length === 0 ? (
          <div className="py-6 text-center text-xs text-muted-foreground">
            {showFlaggedOnly
              ? "Không có tin nhắn bị gắn cờ đang hiển thị."
              : "Chưa có tin nhắn trao đổi nào. Hãy gửi phản hồi đầu tiên tới học sinh / phụ huynh."}
          </div>
        ) : (
          visibleMessages.map((msg) => {
            const isRemoved = msg.status === "removed";
            return (
              <div
                key={msg.id}
                className={cn(
                  "flex flex-col max-w-[85%] rounded-lg px-3 py-2 text-xs border",
                  isRemoved
                    ? "mx-auto max-w-full bg-muted/50 border-dashed text-muted-foreground italic"
                    : msg.isFromCenter
                      ? "ml-auto bg-blue-50/80 dark:bg-blue-950/40 border-blue-200 dark:border-blue-800"
                      : "mr-auto bg-card border-border"
                )}
              >
                {!isRemoved && (
                  <div className="flex flex-wrap items-center justify-between gap-2 mb-1">
                    <div className="flex items-center gap-1.5 min-w-0">
                      <span className="font-semibold text-foreground truncate">
                        {msg.senderName}
                      </span>
                      <span className="text-[10px] px-1.5 py-0.5 rounded bg-background/70 border text-muted-foreground whitespace-nowrap">
                        {formatLeadMessageRoleLabel(msg.senderRole)}
                      </span>
                    </div>
                    <span className="text-[10px] text-muted-foreground whitespace-nowrap">
                      {new Date(msg.createdAt).toLocaleString("vi-VN")}
                    </span>
                  </div>
                )}

                <p className="text-sm text-foreground whitespace-pre-wrap break-words leading-relaxed">
                  {msg.content}
                </p>

                {!isRemoved && msg.isFlagged && (
                  <div className="mt-1.5 flex items-start gap-1.5 rounded bg-amber-100/80 dark:bg-amber-950/60 border border-amber-300 dark:border-amber-700 px-2 py-1 text-[11px] text-amber-900 dark:text-amber-200">
                    <ShieldAlertIcon className="size-3.5 shrink-0 mt-0.5 text-amber-700 dark:text-amber-300" />
                    <span>
                      <strong>Bộ lọc gắn cờ:</strong>{" "}
                      {msg.flagReason || "Vi phạm quy định chia sẻ thông tin liên hệ / quảng cáo"}
                    </span>
                  </div>
                )}
              </div>
            );
          })
        )}
        <div ref={bottomRef} />
      </div>

      {/* Reply Composer */}
      <form onSubmit={handleFormSubmit} className="space-y-2">
        {leadStatus === "new" && (
          <p className="text-[11px] text-muted-foreground">
            Gửi phản hồi đầu tiên sẽ tự động chuyển trạng thái yêu cầu từ{" "}
            <span className="font-medium text-foreground">Mới nhận</span> sang{" "}
            <span className="font-medium text-amber-700">Đang trao đổi</span>.
          </p>
        )}

        <Textarea
          value={draft}
          onChange={(e) => {
            setDraft(e.target.value);
            if (formError) setFormError(null);
          }}
          onKeyDown={handleKeyDown}
          maxLength={MAX_LEAD_MESSAGE_LENGTH}
          rows={3}
          disabled={isSending}
          placeholder="Nhập nội dung phản hồi tới học sinh / phụ huynh (Ctrl+Enter để gửi)..."
          aria-label="Nội dung tin nhắn phản hồi"
          aria-invalid={Boolean(formError)}
          className="text-sm resize-y"
        />

        {draftInspection.isFlagged && (
          <div className="flex items-start gap-2 rounded-md border border-amber-300 bg-amber-50/70 dark:bg-amber-950/30 px-2.5 py-1.5 text-xs text-amber-900 dark:text-amber-200">
            <AlertTriangleIcon className="size-3.5 shrink-0 mt-0.5 text-amber-600" />
            <span>
              <strong>Lưu ý bộ lọc:</strong> Tin nhắn có dấu hiệu vi phạm (
              {draftInspection.reasons.join("; ")}) nên sẽ được gắn cờ để kiểm duyệt sau khi gửi.
            </span>
          </div>
        )}

        {formError && (
          <p className="text-xs text-destructive font-medium">{formError}</p>
        )}

        <div className="flex items-center justify-between gap-2">
          <span
            className={cn(
              "text-xs",
              trimmedDraft.length > MAX_LEAD_MESSAGE_LENGTH
                ? "text-destructive font-medium"
                : "text-muted-foreground"
            )}
          >
            {trimmedDraft.length} / {MAX_LEAD_MESSAGE_LENGTH} ký tự
          </span>

          <Button
            type="submit"
            size="sm"
            disabled={isSending || !trimmedDraft || trimmedDraft.length > MAX_LEAD_MESSAGE_LENGTH}
            className="gap-1.5 text-xs"
          >
            {isSending ? (
              <>
                <Spinner className="size-3.5" />
                Đang gửi...
              </>
            ) : (
              <>
                <SendIcon className="size-3.5" />
                Gửi phản hồi
              </>
            )}
          </Button>
        </div>
      </form>
    </div>
  );
}

