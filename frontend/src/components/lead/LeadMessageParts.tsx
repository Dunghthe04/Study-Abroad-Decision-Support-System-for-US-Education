"use client";

import React, { useEffect, useMemo, useRef, useState } from "react";
import { FlagIcon, ImageIcon, XIcon } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Textarea } from "@/components/ui/textarea";
import { LEAD_IMAGE_ACCEPT, validateLeadImage } from "@/lib/lead-message";
import { cn } from "@/lib/utils";

/** Ảnh đính kèm trong bong bóng tin nhắn; bấm để mở ảnh gốc ở tab mới. */
export function LeadMessageAttachment({ url }: { url?: string | null }) {
  if (!url) return null;
  return (
    <a href={url} target="_blank" rel="noopener noreferrer" className="mt-1.5 block">
      {/* eslint-disable-next-line @next/next/no-img-element */}
      <img
        src={url}
        alt="Ảnh đính kèm"
        loading="lazy"
        referrerPolicy="no-referrer"
        className="max-h-56 max-w-full rounded-md border object-contain"
      />
    </a>
  );
}

/** Chọn một ảnh để đính kèm: nút, xem trước và kiểm tra loại / dung lượng phía client. */
export function ImageAttachPicker({
  file,
  onChange,
  onError,
  disabled,
}: {
  file: File | null;
  onChange: (file: File | null) => void;
  onError: (message: string | null) => void;
  disabled?: boolean;
}) {
  // [SC-40] Tắt gửi ảnh trong nhắn tin với trung tâm bằng cấu hình
  const allowImage = process.env.NEXT_PUBLIC_ALLOW_CHAT_IMAGE_UPLOAD === "true";

  const inputRef = useRef<HTMLInputElement | null>(null);
  const previewUrl = useMemo(() => (allowImage && file ? URL.createObjectURL(file) : null), [allowImage, file]);

  useEffect(() => {
    return () => {
      if (previewUrl) URL.revokeObjectURL(previewUrl);
    };
  }, [previewUrl]);

  // Gọi hooks trước, rồi mới ẩn nút (React yêu cầu hooks chạy cùng thứ tự mỗi lần render)
  if (!allowImage) return null;

  const pick = (e: React.ChangeEvent<HTMLInputElement>) => {
    const picked = e.target.files?.[0] ?? null;
    e.target.value = "";
    if (!picked) return;
    const error = validateLeadImage(picked);
    if (error) {
      onError(error);
      return;
    }
    onError(null);
    onChange(picked);
  };

  return (
    <div className="flex items-center gap-2">
      <input ref={inputRef} type="file" accept={LEAD_IMAGE_ACCEPT} className="hidden" onChange={pick} />
      <Button
        type="button"
        variant="outline"
        size="sm"
        disabled={disabled}
        onClick={() => inputRef.current?.click()}
        className="gap-1.5 text-xs"
      >
        <ImageIcon className="size-3.5" aria-hidden />
        Đính kèm ảnh
      </Button>
      {file && previewUrl && (
        <div className="relative">
          {/* eslint-disable-next-line @next/next/no-img-element */}
          <img src={previewUrl} alt="Ảnh sắp gửi" className="size-10 rounded border object-cover" />
          <button
            type="button"
            disabled={disabled}
            onClick={() => {
              onChange(null);
              onError(null);
            }}
            aria-label="Bỏ ảnh đính kèm"
            className="absolute -right-1.5 -top-1.5 cursor-pointer rounded-full border bg-background p-0.5 text-muted-foreground hover:text-destructive focus-visible:outline-none focus-visible:ring-3 focus-visible:ring-ring/50 disabled:cursor-default"
          >
            <XIcon className="size-3" />
          </button>
        </div>
      )}
    </div>
  );
}

/** Nút "Báo cáo" mở ô nhập lý do tại chỗ. `onReport` ném lỗi nếu thất bại để giữ form. */
export function ReportMessageControl({
  isReported,
  onReport,
}: {
  isReported: boolean;
  onReport: (reason: string) => Promise<void>;
}) {
  const [isReporting, setIsReporting] = useState(false);
  const [reason, setReason] = useState("");
  const [isSubmitting, setIsSubmitting] = useState(false);

  if (isReported) {
    return <p className="text-[11px] text-muted-foreground">Đã báo cáo, quản trị viên sẽ xem xét.</p>;
  }

  if (!isReporting) {
    return (
      <button
        type="button"
        onClick={() => setIsReporting(true)}
        className="inline-flex cursor-pointer items-center gap-1 rounded text-[11px] text-muted-foreground hover:text-destructive focus-visible:outline-none focus-visible:ring-3 focus-visible:ring-ring/50 disabled:cursor-default"
      >
        <FlagIcon className="size-3" aria-hidden />
        Báo cáo tin nhắn
      </button>
    );
  }

  const submit = async () => {
    setIsSubmitting(true);
    try {
      await onReport(reason);
      setIsReporting(false);
      setReason("");
    } catch {
      // lỗi đã hiện ở toast; giữ form để thử lại
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className="space-y-1.5">
      <Textarea
        value={reason}
        onChange={(e) => setReason(e.target.value)}
        maxLength={500}
        rows={2}
        disabled={isSubmitting}
        placeholder="Vì sao tin này vi phạm? (lời lẽ thô tục, quấy rối...)"
        aria-label="Lý do báo cáo tin nhắn"
        className="text-xs"
      />
      <div className="flex justify-end gap-1.5">
        <Button type="button" variant="ghost" size="xs" disabled={isSubmitting} onClick={() => setIsReporting(false)}>
          Hủy
        </Button>
        <Button type="button" size="xs" disabled={isSubmitting} onClick={() => void submit()} loading={isSubmitting}>
          Gửi báo cáo
        </Button>
      </div>
    </div>
  );
}


/**
 * [Chat realtime] Trạng thái của phía đối diện: chấm xanh khi đang online, xám khi ngoại tuyến.
 * Chưa kết nối realtime thì không khẳng định gì, chỉ báo khung đang cập nhật định kỳ.
 */
export function PeerPresenceBadge({ connected, online, peerLabel }: { connected: boolean; online: boolean; peerLabel: string }) {
  if (!connected) {
    return <span className="text-[11px] text-muted-foreground">Đang cập nhật định kỳ</span>;
  }
  return (
    <span className="inline-flex items-center gap-1.5 text-[11px] text-muted-foreground" role="status">
      <span className={cn("size-2 rounded-full", online ? "bg-emerald-500" : "bg-muted-foreground/40")} aria-hidden />
      {online ? `${peerLabel} đang online` : `${peerLabel} ngoại tuyến`}
    </span>
  );
}

/** [Chat realtime] "... đang nhập" với ba chấm nhấp nháy, đặt dưới danh sách tin. */
export function TypingIndicator({ peerLabel }: { peerLabel: string }) {
  return (
    <div className="mr-auto flex items-center gap-1.5 text-[11px] text-muted-foreground" role="status" aria-live="polite">
      <span className="inline-flex gap-0.5" aria-hidden>
        {[0, 150, 300].map((delay) => (
          <span
            key={delay}
            className="size-1 animate-bounce rounded-full bg-muted-foreground/60"
            style={{ animationDelay: `${delay}ms` }}
          />
        ))}
      </span>
      {peerLabel} đang nhập...
    </div>
  );
}

/** [Chat realtime] "Đã xem" hoặc "Đã gửi" dưới tin cuối cùng của chính mình (giống Messenger). */
export function MessageReceipt({ readAt }: { readAt?: string | null }) {
  return (
    <span className="mt-1 self-end text-[10px] text-muted-foreground">
      {readAt
        ? `Đã xem ${new Date(readAt).toLocaleTimeString("vi-VN", { hour: "2-digit", minute: "2-digit" })}`
        : "Đã gửi"}
    </span>
  );
}
