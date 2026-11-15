"use client";

import React, { useEffect, useMemo, useRef, useState } from "react";
import { FlagIcon, ImageIcon, XIcon } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Spinner } from "@/components/ui/spinner";
import { Textarea } from "@/components/ui/textarea";
import { LEAD_IMAGE_ACCEPT, validateLeadImage } from "@/lib/lead-message";

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
  const inputRef = useRef<HTMLInputElement | null>(null);
  const previewUrl = useMemo(() => (file ? URL.createObjectURL(file) : null), [file]);

  useEffect(() => {
    return () => {
      if (previewUrl) URL.revokeObjectURL(previewUrl);
    };
  }, [previewUrl]);

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
            className="absolute -right-1.5 -top-1.5 rounded-full border bg-background p-0.5 text-muted-foreground hover:text-destructive"
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
        className="inline-flex items-center gap-1 text-[11px] text-muted-foreground hover:text-destructive"
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
        <Button type="button" size="xs" disabled={isSubmitting} onClick={() => void submit()}>
          {isSubmitting ? <Spinner className="size-3" /> : null}
          Gửi báo cáo
        </Button>
      </div>
    </div>
  );
}
