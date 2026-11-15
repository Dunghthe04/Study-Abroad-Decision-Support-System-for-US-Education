"use client";

import { useState } from "react";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Spinner } from "@/components/ui/spinner";
import { Textarea } from "@/components/ui/textarea";
import { adminSlmSampleApi } from "@/lib/slm-sample-api";
import { statusLabel } from "@/lib/slm-sample-format";
import type { SlmReviewStatus } from "@/types/slm-sample";

interface Props {
  status: SlmReviewStatus | null;
  ids: string[];
  onClose: () => void;
  onDone: (message: string) => void;
}

export function SlmReviewDialog({ status, ids, onClose, onDone }: Props) {
  return (
    <Dialog open={status !== null} onOpenChange={(next) => !next && onClose()}>
      <DialogContent>
        {status ? <ReviewForm key={status} status={status} ids={ids} onClose={onClose} onDone={onDone} /> : null}
      </DialogContent>
    </Dialog>
  );
}

function ReviewForm({ status, ids, onClose, onDone }: Omit<Props, "status"> & { status: SlmReviewStatus }) {
  const [note, setNote] = useState("");
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const submit = async () => {
    setSaving(true);
    setError(null);
    try {
      const result = await adminSlmSampleApi.review(ids, status, note.trim());
      const missing = result.notFound > 0 ? `, không tìm thấy ${result.notFound}` : "";
      onDone(`Đã chuyển ${result.updated} mẫu sang "${statusLabel(status)}"${missing}.`);
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
        <DialogDescription>Áp dụng cho {ids.length} mẫu đã chọn. Ghi chú không bắt buộc.</DialogDescription>
      </DialogHeader>
      <Textarea value={note} rows={3} placeholder="Ghi chú (tùy chọn)" onChange={(event) => setNote(event.target.value)} />
      {error ? <Alert variant="destructive"><AlertDescription>{error}</AlertDescription></Alert> : null}
      <DialogFooter>
        <Button type="button" variant="outline" disabled={saving} onClick={onClose}>Hủy</Button>
        <Button type="button" disabled={saving} onClick={() => void submit()}>{saving && <Spinner />} Xác nhận</Button>
      </DialogFooter>
    </>
  );
}
