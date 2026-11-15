"use client";

import { useState } from "react";
import { BanIcon } from "lucide-react";
import { Alert, AlertDescription } from "@/components/ui/alert";
import {
  AlertDialog,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Spinner } from "@/components/ui/spinner";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Textarea } from "@/components/ui/textarea";
import { toast } from "@/components/ui/toast";
import { ApiError, centerSuspensionApi } from "@/lib/api";
import { useFetched } from "@/lib/use-fetched";
import type { RepeatRemovalCenterDto } from "@/types/api";

/**
 * [USAS-2080 / UC-58] Trung tâm bị gỡ tin lặp lại (từ 3 tin): Moderator gửi đề nghị tạm ngừng lên Admin.
 * Chỉ hiện khi có trung tâm đủ điều kiện. Admin xem được nhưng không gửi đề nghị.
 */
export function RepeatRemovalCenters({ canAct }: { canAct: boolean }) {
  const [version, setVersion] = useState(0);
  const centers = useFetched(`repeat-removal|${version}`, () => centerSuspensionApi.repeatRemovalCenters());
  const [target, setTarget] = useState<RepeatRemovalCenterDto | null>(null);
  const [reason, setReason] = useState("");
  const [isSending, setIsSending] = useState(false);

  const list = centers.data;
  if (centers.error) {
    return (
      <Alert variant="destructive">
        <AlertDescription>{centers.error}</AlertDescription>
      </Alert>
    );
  }
  if (!list || list.length === 0) return null;

  const closeDialog = () => {
    if (isSending) return;
    setTarget(null);
    setReason("");
  };

  const send = async () => {
    if (!target || isSending) return;
    const trimmed = reason.trim();
    if (!trimmed) {
      toast.add({ type: "error", title: "Thiếu lý do", description: "Vui lòng nhập lý do đề nghị tạm ngừng." });
      return;
    }
    setIsSending(true);
    try {
      await centerSuspensionApi.propose(target.studyCenterId, trimmed);
      toast.add({ type: "success", title: "Đã gửi đề nghị", description: "Admin sẽ xem và quyết định tạm ngừng trung tâm." });
      setTarget(null);
      setReason("");
      setVersion((current) => current + 1);
    } catch (err: unknown) {
      toast.add({
        type: "error",
        title: "Gửi đề nghị thất bại",
        description: err instanceof ApiError ? err.message : "Không thể gửi đề nghị. Vui lòng thử lại.",
      });
    } finally {
      setIsSending(false);
    }
  };

  return (
    <section aria-label="Trung tâm bị gỡ tin lặp lại" className="flex flex-col gap-2 rounded-md border border-warn/30 bg-warn-wash/40 p-3">
      <div>
        <h2 className="text-sm font-semibold text-ink">Trung tâm bị gỡ tin lặp lại</h2>
        <p className="text-xs text-ink-2">
          Những trung tâm có từ 3 tin bị gỡ trở lên. {canAct ? "Bạn có thể đề nghị Admin tạm ngừng trung tâm." : "Chỉ Moderator gửi đề nghị tạm ngừng."}
        </p>
      </div>
      <div className="overflow-x-auto rounded-md border border-line bg-surface">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Trung tâm</TableHead>
              <TableHead className="text-right">Số tin bị gỡ</TableHead>
              <TableHead>Trạng thái</TableHead>
              {canAct ? <TableHead className="w-44"><span className="sr-only">Thao tác</span></TableHead> : null}
            </TableRow>
          </TableHeader>
          <TableBody>
            {list.map((center) => {
              const suspended = center.verificationStatus === "suspended";
              return (
                <TableRow key={center.studyCenterId}>
                  <TableCell>{center.studyCenterName}</TableCell>
                  <TableCell className="tabular text-right">{center.removedMessageCount}</TableCell>
                  <TableCell>
                    {suspended ? (
                      <Badge variant="risk">Đã tạm ngừng</Badge>
                    ) : center.hasPendingProposal ? (
                      <Badge variant="warn">Đã đề nghị, chờ Admin</Badge>
                    ) : (
                      <Badge variant="neutral">Chưa đề nghị</Badge>
                    )}
                  </TableCell>
                  {canAct ? (
                    <TableCell className="text-right">
                      {suspended || center.hasPendingProposal ? null : (
                        <Button type="button" variant="outline" size="sm" onClick={() => setTarget(center)} className="gap-1 text-risk hover:text-risk">
                          <BanIcon className="size-3.5" />
                          Đề nghị tạm ngừng
                        </Button>
                      )}
                    </TableCell>
                  ) : null}
                </TableRow>
              );
            })}
          </TableBody>
        </Table>
      </div>

      <AlertDialog open={target !== null} onOpenChange={(open) => !open && closeDialog()}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Đề nghị tạm ngừng {target?.studyCenterName}?</AlertDialogTitle>
            <AlertDialogDescription>
              Trung tâm đã có {target?.removedMessageCount} tin bị gỡ. Đề nghị được gửi tới Admin để quyết định; trung tâm chỉ bị tạm ngừng khi Admin đồng ý.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <Textarea
            value={reason}
            onChange={(event) => setReason(event.target.value)}
            maxLength={1000}
            rows={3}
            disabled={isSending}
            placeholder="Lý do đề nghị tạm ngừng (bắt buộc, tối đa 1000 ký tự)"
            aria-label="Lý do đề nghị tạm ngừng"
            className="resize-y text-sm"
          />
          <AlertDialogFooter>
            <AlertDialogCancel disabled={isSending}>Hủy</AlertDialogCancel>
            <Button type="button" variant="destructive" onClick={() => void send()} disabled={isSending || reason.trim() === ""} className="gap-1.5">
              {isSending ? <Spinner className="size-3.5" /> : <BanIcon className="size-3.5" />}
              Gửi đề nghị
            </Button>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </section>
  );
}
