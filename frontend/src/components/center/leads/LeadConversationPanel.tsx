"use client";

import React, { useCallback, useEffect, useRef, useState } from "react";
import { AlertTriangleIcon, MessageSquareIcon, RefreshCwIcon, SendIcon } from "lucide-react";
import { ApiError, centerLeadsApi } from "@/lib/api";
import { MAX_LEAD_MESSAGE_LENGTH, formatLeadMessageRoleLabel } from "@/lib/lead-message";
import { getLeadRuleError } from "@/lib/lead-api";
import { LEAD_MESSAGING_BLOCK_CODES, getLeadMessagingBlockReason } from "@/types/lead";
import {
  ImageAttachPicker,
  LeadMessageAttachment,
  MessageReceipt,
  PeerPresenceBadge,
  ReportMessageControl,
  TypingIndicator,
} from "@/components/lead/LeadMessageParts";
import { useLeadChat } from "@/hooks/use-lead-chat";
import type { LeadMessageDto, LeadMessagesThreadDto } from "@/types/api";
import { Button } from "@/components/ui/button";
import { Spinner } from "@/components/ui/spinner";
import { Textarea } from "@/components/ui/textarea";
import { toast } from "@/components/ui/toast";
import { cn } from "@/lib/utils";

interface LeadConversationPanelProps {
  leadId: string;
  leadStatus: string;
  /** [UC-74] Ngày gửi yêu cầu, để chặn nhắn khi quá 90 ngày. */
  leadCreatedAt?: string | null;
  onLeadStatusChange?: (newStatus: string) => void;
}

function getLoadThreadErrorMessage(err: unknown): string {
  return err instanceof ApiError
    ? err.message
    : "Không thể tải lịch sử trao đổi tin nhắn.";
}

/**
 * [USAS-2053 / UC-74] Khu vực đọc và trả lời tin nhắn trao đổi giữa Trung tâm tư vấn và Học sinh/Phụ huynh.
 * Trung tâm nhắn thoải mái (chữ và ảnh), có thể báo cáo tin của học sinh / phụ huynh để Admin xem xét.
 */
export function LeadConversationPanel({
  leadId,
  leadStatus,
  leadCreatedAt,
  onLeadStatusChange,
}: LeadConversationPanelProps) {
  // [UC-74] Chỉ nhắn khi yêu cầu Đã chấp nhận; server chặn thêm thì giữ lý do server trả về
  const [serverBlockReason, setServerBlockReason] = useState<string | null>(null);
  const blockReason = serverBlockReason ?? getLeadMessagingBlockReason(leadStatus, leadCreatedAt);
  const [messages, setMessages] = useState<LeadMessageDto[]>([]);
  const [lastSyncedAt, setLastSyncedAt] = useState<Date | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);

  const [draft, setDraft] = useState("");
  const [formError, setFormError] = useState<string | null>(null);
  const [isSending, setIsSending] = useState(false);
  const [imageFile, setImageFile] = useState<File | null>(null);
  // [UC-58] Tin đã báo cáo trong phiên này (backend không trả cờ báo cáo cho trung tâm)
  const [reportedIds, setReportedIds] = useState<Set<string>>(() => new Set());

  const bottomRef = useRef<HTMLDivElement | null>(null);

  const applyThreadResult = useCallback((thread: LeadMessagesThreadDto) => {
    setMessages(thread.messages);
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

  // [Chat realtime] Tải lại im lặng (không bật spinner) khi nối lại mạng, quay lại tab hoặc chưa có realtime
  const resync = useCallback(() => {
    centerLeadsApi
      .getMessages(leadId)
      .then(applyThreadResult)
      .catch(() => undefined);
  }, [leadId, applyThreadResult]);

  const chat = useLeadChat({
    leadId,
    onNewMessage: (message) =>
      setMessages((prev) => (prev.some((m) => m.id === message.id) ? prev : [...prev, message])),
    onMessagesRead: (ids, readAt) =>
      setMessages((prev) => prev.map((m) => (ids.includes(m.id) ? { ...m, readAt } : m))),
    onResync: resync,
  });

  useEffect(() => {
    bottomRef.current?.scrollIntoView({ behavior: "smooth", block: "nearest" });
  }, [messages.length, chat.peerTyping]);

  // Tin cuối cùng của trung tâm (chưa gỡ) mang dấu "Đã gửi / Đã xem"
  const lastOwnId = [...messages].reverse().find((m) => m.isFromCenter && m.status !== "removed")?.id;

  const trimmedDraft = draft.trim();

  /**
   * [USAS-2053 / UC-74] Kiểm tra hợp lệ phía client trước khi gọi API gửi phản hồi.
   */
  const submitMessage = async () => {
    if (isSending || blockReason) return;

    if (!trimmedDraft && !imageFile) {
      setFormError("Vui lòng nhập nội dung tin nhắn hoặc chọn ảnh trước khi gửi.");
      return;
    }

    if (trimmedDraft.length > MAX_LEAD_MESSAGE_LENGTH) {
      setFormError(`Nội dung tin nhắn tối đa ${MAX_LEAD_MESSAGE_LENGTH} ký tự.`);
      return;
    }

    setFormError(null);
    setIsSending(true);
    try {
      const result = imageFile
        ? await centerLeadsApi.sendImageMessage(leadId, imageFile, trimmedDraft)
        : await centerLeadsApi.sendMessage(leadId, trimmedDraft);
      // Bản tin có thể đã về qua realtime trước khi REST trả lời: bỏ tin trùng
      setMessages((prev) => (prev.some((m) => m.id === result.message.id) ? prev : [...prev, result.message]));
      setLastSyncedAt(new Date());
      setDraft("");
      setImageFile(null);
      chat.notifyTyping(false);

      if (result.leadStatus && result.leadStatus !== leadStatus) {
        onLeadStatusChange?.(result.leadStatus);
      }

      toast.add({
        type: "success",
        title: "Đã gửi phản hồi",
        description: "Tin nhắn đã được lưu và gửi tới học sinh / phụ huynh.",
      });
    } catch (err: unknown) {
      const rule = getLeadRuleError(err);
      if (rule && (LEAD_MESSAGING_BLOCK_CODES as readonly string[]).includes(rule.code)) {
        setServerBlockReason(rule.message);
      }
      const msg =
        rule?.message ??
        (err instanceof ApiError
          ? err.message
          : "Không thể gửi tin nhắn. Vui lòng thử lại.");
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

  const reportMessage = async (messageId: string, reason: string) => {
    try {
      await centerLeadsApi.reportMessage(leadId, messageId, reason);
      setReportedIds((prev) => new Set(prev).add(messageId));
      toast.add({
        type: "success",
        title: "Đã gửi báo cáo",
        description: "Quản trị viên sẽ xem xét tin nhắn này.",
      });
    } catch (err: unknown) {
      toast.add({
        type: "error",
        title: "Báo cáo thất bại",
        description: err instanceof ApiError ? err.message : "Không thể gửi báo cáo. Vui lòng thử lại.",
      });
      throw err;
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
        </div>

        <div className="flex items-center gap-2">
          <PeerPresenceBadge connected={chat.connected} online={chat.peerOnline} peerLabel="Học sinh / phụ huynh" />
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

      {/* Security Warning Banner */}
      <div className="rounded-md border border-amber-500/30 bg-amber-500/10 px-3 py-2 text-xs text-amber-800 dark:text-amber-200 flex items-start gap-2">
        <AlertTriangleIcon className="size-4 text-amber-600 shrink-0 mt-0.5" />
        <span>
          <strong>Lưu ý bảo mật:</strong> USAS không thu phí dịch vụ qua tin nhắn. Tuyệt đối không yêu cầu hoặc thực hiện chuyển khoản tiền cọc qua tài khoản cá nhân.
        </span>
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
        ) : messages.length === 0 ? (
          <div className="py-6 text-center text-xs text-muted-foreground">
            Chưa có tin nhắn trao đổi nào. Hãy gửi phản hồi đầu tiên tới học sinh / phụ huynh.
          </div>
        ) : (
          messages.map((msg) => {
            const isRemoved = msg.status === "removed";
            return (
              <div
                key={msg.id}
                className={cn(
                  "flex flex-col max-w-[85%] rounded-lg px-3 py-2 text-xs border",
                  isRemoved
                    ? cn("bg-muted/50 border-dashed text-muted-foreground italic", msg.isFromCenter ? "ml-auto" : "mr-auto")
                    : msg.isFromCenter
                      ? "ml-auto bg-brand-wash/75 border-brand/25"
                      : "mr-auto bg-surface border-line"
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

                {msg.content && (
                  <p
                    className={cn(
                      "whitespace-pre-wrap break-words leading-relaxed",
                      isRemoved ? "text-xs italic text-muted-foreground" : "text-sm text-foreground"
                    )}
                  >
                    {msg.content}
                  </p>
                )}
                {!isRemoved && <LeadMessageAttachment url={msg.attachmentUrl} />}

                {!isRemoved && msg.isFromCenter && msg.id === lastOwnId && <MessageReceipt readAt={msg.readAt} />}

                {/* [UC-58] Trung tâm báo cáo tin của học sinh / phụ huynh */}
                {!isRemoved && !msg.isFromCenter && (
                  <div className="mt-1.5">
                    <ReportMessageControl
                      isReported={reportedIds.has(msg.id)}
                      onReport={(reason) => reportMessage(msg.id, reason)}
                    />
                  </div>
                )}
              </div>
            );
          })
        )}
        {chat.peerTyping && <TypingIndicator peerLabel="Học sinh / phụ huynh" />}
        <div ref={bottomRef} />
      </div>

      {/* Reply Composer: [UC-74] khóa và nêu lý do khi không nhắn được */}
      {blockReason ? (
        <p className="rounded-md border bg-muted/50 px-3 py-2 text-sm text-muted-foreground">{blockReason}</p>
      ) : (
        <form onSubmit={handleFormSubmit} className="space-y-2">

          <Textarea
            value={draft}
            onChange={(e) => {
              setDraft(e.target.value);
              chat.notifyTyping(e.target.value.trim().length > 0);
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

          <ImageAttachPicker file={imageFile} onChange={setImageFile} onError={setFormError} disabled={isSending} />

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
              disabled={isSending || (!trimmedDraft && !imageFile) || trimmedDraft.length > MAX_LEAD_MESSAGE_LENGTH}
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
      )}
    </div>
  );
}

