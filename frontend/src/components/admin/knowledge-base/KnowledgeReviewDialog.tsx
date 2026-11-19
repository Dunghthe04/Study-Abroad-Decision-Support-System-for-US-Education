"use client";

import { useState } from "react";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Spinner } from "@/components/ui/spinner";
import { Textarea } from "@/components/ui/textarea";
import { adminKnowledgeBaseApi } from "@/lib/knowledge-base-api";
import { statusLabel } from "@/lib/knowledge-base-format";
import type { KnowledgeReviewStatus } from "@/types/knowledge-base";

interface Props {
  status: KnowledgeReviewStatus | null;
  ids: string[];
  onClose: () => void;
  onDone: (message: string) => void;
}

export function KnowledgeReviewDialog({ status, ids, onClose, onDone }: Props) {
  return (
    <Dialog open={status !== null} onOpenChange={(next) => !next && onClose()}>
      <DialogContent>
        {status ? <ReviewForm key={status} status={status} ids={ids} onClose={onClose} onDone={onDone} /> : null}
      </DialogContent>
    </Dialog>
  );
}

function ReviewForm({ status, ids, onClose, onDone }: Omit<Props, "status"> & { status: KnowledgeReviewStatus }) {
  const [note, setNote] = useState("");
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const needsNote = status !== "approved";
  const missingNote = needsNote && note.trim() === "";

  const submit = async () => {
    setSaving(true);
    setError(null);
    try {
      const result = await adminKnowledgeBaseApi.review(ids, status, note.trim());
      const skipped = [
        result.notFound > 0 ? `không tìm thấy ${result.notFound}` : "",
        result.skippedOwn > 0 ? `${result.skippedOwn} mục do chính bạn tạo hoặc sửa` : "",
        result.skippedNotPending > 0 ? `${result.skippedNotPending} mục không ở trạng thái chờ duyệt` : "",
        result.skippedNotReady > 0 ? `${result.skippedNotReady} mục chưa đủ nội dung` : "",
      ].filter(Boolean);
      onDone(`Đã chuyển ${result.updated} tri thức sang "${statusLabel(status)}"${skipped.length ? `. Bỏ qua: ${skipped.join(", ")}` : ""}.`);
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : "Không cập nhật được.");
    } finally {
      setSaving(false);
    }
  };

  return (
    <>
      <DialogHeader>
        <DialogTitle>Chuyển sang &quot;{statusLabel(status)}&quot;</DialogTitle>
        <DialogDescription>Áp dụng cho {ids.length} tri thức đã chọn. {needsNote ? "Bắt buộc nhập lý do." : "Ghi chú không bắt buộc."}</DialogDescription>
      </DialogHeader>
      <Textarea
        value={note}
        rows={3}
        maxLength={1000}
        placeholder={needsNote ? "Lý do (bắt buộc)" : "Ghi chú (tùy chọn)"}
        aria-label={needsNote ? "Lý do" : "Ghi chú"}
        onChange={(event) => setNote(event.target.value)}
      />
      {error ? <Alert variant="destructive"><AlertDescription>{error}</AlertDescription></Alert> : null}
      <DialogFooter>
        <Button type="button" variant="outline" disabled={saving} onClick={onClose}>Hủy</Button>
        <Button type="button" disabled={saving || missingNote} onClick={() => void submit()}>{saving && <Spinner />} Xác nhận</Button>
      </DialogFooter>
    </>
  );
}