"use client";

import React, { useCallback, useEffect, useRef, useState } from "react";
import { RefreshCwIcon, SendIcon } from "lucide-react";
import { useAuth } from "@/contexts/AuthContext";
import { ApiError } from "@/lib/api";
import { getLeadRuleError, leadApi } from "@/lib/lead-api";
import { MAX_LEAD_MESSAGE_LENGTH, formatLeadMessageRoleLabel } from "@/lib/lead-message";
import { ImageAttachPicker, LeadMessageAttachment, ReportMessageControl } from "@/components/lead/LeadMessageParts";
import { cn } from "@/lib/utils";
import type { LeadMessageDto, LeadMessagesThreadDto } from "@/types/api";
import type { SentLeadDto } from "@/types/lead";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Spinner } from "@/components/ui/spinner";
import { Textarea } from "@/components/ui/textarea";
import { toast } from "@/components/ui/toast";

/** Thông tin thẻ UC-44 cần cập nhật sau khi tải / gửi tin. */
export interface SentLeadThreadChange {
  status: string;
  centerMessageCount: number;
}

interface SentLeadMessagesDialogProps {
  lead: SentLeadDto;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onThreadChange?: (change: SentLeadThreadChange) => void;
}

// Đếm giống CenterMessageCount của UC-44: tin trung tâm gửi, chưa bị gỡ
const countCenterMessages = (messages: LeadMessageDto[]) =>
  messages.filter((m) => m.isFromCenter && m.status !== "removed").length;

const loadErrorText = (err: unknown) =>
  getLeadRuleError(err)?.message ?? "Không tải được tin nhắn. Vui lòng thử lại.";

/**
 * [Nhắn tin phía người gửi] Khung tin nhắn với trung tâm trong một yêu cầu đã gửi.
 * Mở từ nút "Tin nhắn" trên thẻ UC-44 (SentLeadsCard). Không tự làm mới: mở khung thì tải, có nút "Tải lại".
 */
export function SentLeadMessagesDialog({ lead, open, onOpenChange, onThreadChange }: SentLeadMessagesDialogProps) {
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>Tin nhắn với {lead.studyCenterName}</DialogTitle>
          <DialogDescription>
            Gửi ngày {new Date(lead.createdAt).toLocaleDateString("vi-VN")} · Học sinh: {lead.studentName}
          </DialogDescription>
        </DialogHeader>
        {/* Chỉ dựng phần thân khi mở, để mỗi lần mở là tải lại luồng tin */}
        {open && <ThreadBody leadId={lead.id} initialStatus={lead.status} onThreadChange={onThreadChange} />}
      </DialogContent>
    </Dialog>
  );
}

function ThreadBody({
  leadId,
  initialStatus,
  onThreadChange,
}: {
  leadId: string;
  initialStatus: string;
  onThreadChange?: (change: SentLeadThreadChange) => void;
}) {
  const { user } = useAuth();
  const [messages, setMessages] = useState<LeadMessageDto[]>([]);
  const [leadStatus, setLeadStatus] = useState(initialStatus);
  const [isLoading, setIsLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  // Giống Đức: hiện "Cập nhật HH:mm" vì khung không tự làm mới
  const [lastSyncedAt, setLastSyncedAt] = useState<Date | null>(null);

  const [draft, setDraft] = useState("");
  const [formError, setFormError] = useState<string | null>(null);
  const [isSending, setIsSending] = useState(false);
  const [imageFile, setImageFile] = useState<File | null>(null);

  // [UC-58] Tin của trung tâm đã báo cáo trong lần mở khung này (backend không trả cờ báo cáo)
  const [reportedIds, setReportedIds] = useState<Set<string>>(() => new Set());

  const reportMessage = async (messageId: string, reason: string) => {
    try {
      await leadApi.reportSentMessage(leadId, messageId, reason);
      setReportedIds((prev) => new Set(prev).add(messageId));
      toast.add({
        type: "success",
        title: "Đã gửi báo cáo",
        description: "Quản trị viên sẽ xem xét tin nhắn này.",
      });
    } catch (err) {
      toast.add({
        type: "error",
        title: "Báo cáo thất bại",
        description: err instanceof ApiError ? err.message : "Không thể gửi báo cáo. Vui lòng thử lại.",
      });
      throw err;
    }
  };

  const bottomRef = useRef<HTMLDivElement | null>(null);
  // Giữ callback mới nhất mà không làm load() chạy lại mỗi lần cha render
  const onChangeRef = useRef(onThreadChange);
  useEffect(() => {
    onChangeRef.current = onThreadChange;
  }, [onThreadChange]);

  const applyThread = useCallback((thread: LeadMessagesThreadDto) => {
    setMessages(thread.messages);
    setLeadStatus(thread.leadStatus);
    setLastSyncedAt(new Date());
    onChangeRef.current?.({ status: thread.leadStatus, centerMessageCount: countCenterMessages(thread.messages) });
  }, []);

  // Nút "Tải lại"
  const load = async () => {
    setIsLoading(true);
    setLoadError(null);
    try {
      applyThread(await leadApi.listSentMessages(leadId));
    } catch (err) {
      setLoadError(loadErrorText(err));
    } finally {
      setIsLoading(false);
    }
  };

  // Tải lần đầu khi mở khung (giống LeadConversationPanel của Đức: chỉ setState trong .then, bỏ kết quả nếu đã đóng)
  useEffect(() => {
    let ignore = false;
    leadApi
      .listSentMessages(leadId)
      .then((thread) => {
        if (!ignore) applyThread(thread);
      })
      .catch((err: unknown) => {
        if (!ignore) setLoadError(loadErrorText(err));
      })
      .finally(() => {
        if (!ignore) setIsLoading(false);
      });
    return () => {
      ignore = true;
    };
  }, [leadId, applyThread]);

  useEffect(() => {
    bottomRef.current?.scrollIntoView({ behavior: "smooth", block: "nearest" });
  }, [messages.length]);

  const isWithdrawn = leadStatus === "withdrawn"; // [UC-80]
  const isClosed = leadStatus === "closed" || isWithdrawn;
  const trimmedDraft = draft.trim();

  const submit = async () => {
    if (isSending || isClosed) return;
    if (!trimmedDraft && !imageFile) {
      setFormError("Vui lòng nhập nội dung tin nhắn hoặc chọn ảnh.");
      return;
    }
    if (trimmedDraft.length > MAX_LEAD_MESSAGE_LENGTH) {
      setFormError(`Tin nhắn tối đa ${MAX_LEAD_MESSAGE_LENGTH} ký tự.`);
      return;
    }

    setFormError(null);
    setIsSending(true);
    try {
      const result = imageFile
        ? await leadApi.sendSentImageMessage(leadId, imageFile, trimmedDraft)
        : await leadApi.sendSentMessage(leadId, trimmedDraft);
      setMessages((prev) => [...prev, result.message]);
      setLeadStatus(result.leadStatus);
      setLastSyncedAt(new Date());
      setDraft("");

      setImageFile(null);
      toast.add({ type: "success", title: "Đã gửi tin cho trung tâm" });
    } catch (err) {
      const rule = getLeadRuleError(err);
      // Trung tâm vừa đóng yêu cầu trong lúc khung đang mở → chuyển sang chỉ đọc
      if (rule?.code === "lead_closed") setLeadStatus("closed");
      if (rule?.code === "lead_withdrawn") setLeadStatus("withdrawn");
      const msg = rule?.message ?? "Không gửi được tin nhắn. Vui lòng thử lại.";
      setFormError(msg);
      toast.add({ type: "error", title: "Gửi tin nhắn thất bại", description: msg });
    } finally {
      setIsSending(false);
    }
  };

  return (
    <div className="space-y-3">
      <div className="flex items-center justify-end gap-2">
        {lastSyncedAt && !isLoading && (
          <span className="text-[11px] text-muted-foreground">
            Cập nhật {lastSyncedAt.toLocaleTimeString("vi-VN", { hour: "2-digit", minute: "2-digit" })}
          </span>
        )}
        <Button type="button" variant="ghost" size="sm" disabled={isLoading} onClick={() => void load()}>
          <RefreshCwIcon className={cn(isLoading && "animate-spin")} data-icon="inline-start" aria-hidden />
          Tải lại
        </Button>
      </div>

      <div className="max-h-80 space-y-2.5 overflow-y-auto rounded-lg border bg-muted/20 p-3" aria-live="polite">
        {isLoading && messages.length === 0 ? (
          <div className="flex items-center justify-center gap-2 py-8 text-sm text-muted-foreground">
            <Spinner />
            Đang tải tin nhắn...
          </div>
        ) : loadError ? (
          <div className="space-y-2 py-6 text-center">
            <p className="text-sm text-destructive">{loadError}</p>
            <Button type="button" variant="outline" size="sm" disabled={isLoading} onClick={() => void load()}>
              Thử lại
            </Button>
          </div>
        ) : messages.length === 0 ? (
          <p className="py-6 text-center text-sm text-muted-foreground">
            Chưa có tin nhắn nào. Bạn có thể nhắn trước cho trung tâm.
          </p>
        ) : (
          messages.map((msg) => (
            <MessageBubble
              key={msg.id}
              msg={msg}
              isMine={msg.senderUserId === user?.id}
              isReported={reportedIds.has(msg.id)}
              onReport={reportMessage}
            />
          ))
        )}
        <div ref={bottomRef} />
      </div>

      {isClosed ? (
        <p className="rounded-md border bg-muted/50 px-3 py-2 text-sm text-muted-foreground">
          {isWithdrawn
            ? "Yêu cầu đã được thu hồi, không gửi thêm tin nhắn được."
            : "Yêu cầu đã hoàn tất, không gửi thêm tin nhắn được."}
        </p>
      ) : (
        <form
          className="space-y-2"
          onSubmit={(e) => {
            e.preventDefault();
            void submit();
          }}
        >
          <Textarea
            value={draft}
            onChange={(e) => {
              setDraft(e.target.value);
              if (formError) setFormError(null);
            }}
            onKeyDown={(e) => {
              if (e.key === "Enter" && (e.ctrlKey || e.metaKey)) {
                e.preventDefault();
                void submit();
              }
            }}
            maxLength={MAX_LEAD_MESSAGE_LENGTH}
            rows={3}
            disabled={isSending}
            placeholder="Nhập tin nhắn cho trung tâm (Ctrl+Enter để gửi)..."
            aria-label="Nội dung tin nhắn"
            aria-invalid={Boolean(formError)}
          />

          <ImageAttachPicker file={imageFile} onChange={setImageFile} onError={setFormError} disabled={isSending} />

          {formError && <p className="text-xs font-medium text-destructive">{formError}</p>}

          <div className="flex items-center justify-between gap-2">
            <span className="text-xs text-muted-foreground">
              {trimmedDraft.length} / {MAX_LEAD_MESSAGE_LENGTH} ký tự
            </span>
            <Button type="submit" size="sm" disabled={isSending || (!trimmedDraft && !imageFile)}>
              {isSending ? <Spinner data-icon="inline-start" /> : <SendIcon data-icon="inline-start" aria-hidden />}
              {isSending ? "Đang gửi..." : "Gửi"}
            </Button>
          </div>
        </form>
      )}
    </div>
  );
}

function MessageBubble({
  msg,
  isMine,
  isReported,
  onReport,
}: {
  msg: LeadMessageDto;
  isMine: boolean;
  isReported: boolean;
  onReport: (messageId: string, reason: string) => Promise<void>;
}) {
  // Backend đã thay nội dung tin bị gỡ bằng câu thông báo
  if (msg.status === "removed") {
    return (
      <p className="mx-auto rounded-lg border border-dashed bg-muted/50 px-3 py-2 text-center text-xs italic text-muted-foreground">
        {msg.content}
      </p>
    );
  }

  return (
    <div
      className={cn(
        "flex max-w-[85%] flex-col rounded-lg border px-3 py-2",
        isMine
          ? "ml-auto border-blue-200 bg-blue-50/80 dark:border-blue-800 dark:bg-blue-950/40"
          : "mr-auto border-border bg-card",
      )}
    >
      <div className="mb-1 flex flex-wrap items-center justify-between gap-2 text-xs">
        <span className="font-semibold">
          {isMine ? "Bạn" : msg.senderName}
          {!isMine && !msg.isFromCenter && (
            <span className="font-normal text-muted-foreground"> · {formatLeadMessageRoleLabel(msg.senderRole)}</span>
          )}
        </span>
        <span className="text-[10px] text-muted-foreground">{new Date(msg.createdAt).toLocaleString("vi-VN")}</span>
      </div>
      {msg.content && <p className="whitespace-pre-wrap break-words text-sm leading-relaxed">{msg.content}</p>}
      <LeadMessageAttachment url={msg.attachmentUrl} />

      {/* [UC-58] Học sinh / phụ huynh báo cáo tin của trung tâm (lời lẽ thô tục, quấy rối...) */}
      {msg.isFromCenter && !isMine && (
        <div className="mt-1.5">
          <ReportMessageControl isReported={isReported} onReport={(reason) => onReport(msg.id, reason)} />
        </div>
      )}
    </div>
  );
}
