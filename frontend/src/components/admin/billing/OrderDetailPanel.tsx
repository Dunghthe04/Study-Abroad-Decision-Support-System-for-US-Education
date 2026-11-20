"use client";

import { useEffect, useState } from "react";
import {
  AlertDialog,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { Spinner } from "@/components/ui/spinner";
import { Textarea } from "@/components/ui/textarea";
import { toast } from "@/components/ui/toast";
import { adminBillingApi, adminBillingErrorMessage, formatVnDateTime } from "@/lib/admin-billing-api";
import { formatVnd } from "@/lib/billing-api";
import type { AdminOrderDetailDto } from "@/types/admin-billing";
import { DetailPanel } from "../shared/DetailPanel";
import { OrderStatusBadge } from "./OrderStatusBadge";

type Action = "recheck" | "activate" | "reject";

const ACTIONS: Record<Action, { title: string; description: string; confirm: string; done: string; needsNote: boolean }> = {
  recheck: {
    title: "Đối soát với payOS?",
    description: "Hỏi lại payOS trạng thái đơn này và áp dụng kết quả.",
    confirm: "Đối soát",
    done: "Đã đối soát với payOS",
    needsNote: false,
  },
  activate: {
    title: "Kích hoạt thủ công?",
    description: "Đơn được tính là đã thanh toán và người mua nhận gói. Ghi rõ căn cứ (ví dụ sao kê ngân hàng).",
    confirm: "Kích hoạt",
    done: "Đã kích hoạt đơn",
    needsNote: true,
  },
  reject: {
    title: "Từ chối đơn sai số tiền?",
    description: "Đơn bị hủy và người mua được thông báo. Ghi rõ lý do và cách hoàn tiền.",
    confirm: "Từ chối",
    done: "Đã từ chối đơn",
    needsNote: true,
  },
};

/** Payload webhook in đẹp; không phải JSON thì giữ nguyên. */
function prettyJson(raw: string): string {
  try {
    return JSON.stringify(JSON.parse(raw), null, 2);
  } catch {
    return raw;
  }
}

interface OrderDetailPanelProps {
  orderCode: number;
  onClose: () => void;
  /** Gọi sau mỗi thao tác (thành công hay lỗi) để tải lại danh sách. */
  onChanged: () => void;
}

interface LoadResult {
  key: string;
  order: AdminOrderDetailDto | null;
  error: string | null;
}

/** [UC-132..134] Chi tiết đơn: payload, ghi chú, đối soát, kích hoạt, từ chối. */
export function OrderDetailPanel({ orderCode, onClose, onChanged }: OrderDetailPanelProps) {
  const [reload, setReload] = useState(0);
  const [result, setResult] = useState<LoadResult | null>(null);
  const [action, setAction] = useState<Action | null>(null);
  const [note, setNote] = useState("");
  const [busy, setBusy] = useState(false);

  const requestKey = `${orderCode}:${reload}`;
  const isLoading = result === null || result.key !== requestKey;
  const order = isLoading ? null : result.order;

  useEffect(() => {
    let cancelled = false;
    adminBillingApi
      .order(orderCode)
      .then((loaded) => !cancelled && setResult({ key: requestKey, order: loaded, error: null }))
      .catch((err: unknown) => {
        if (!cancelled) setResult({ key: requestKey, order: null, error: adminBillingErrorMessage(err, "Không tải được đơn hàng.") });
      });
    return () => {
      cancelled = true;
    };
  }, [orderCode, requestKey]);

  const openAction = (next: Action) => {
    setNote("");
    setAction(next);
  };

  const run = async () => {
    if (!action) return;
    const text = ACTIONS[action];
    setBusy(true);
    try {
      if (action === "recheck") await adminBillingApi.recheck(orderCode);
      else if (action === "activate") await adminBillingApi.activate(orderCode, note.trim());
      else await adminBillingApi.reject(orderCode, note.trim());
      toast.add({ type: "success", title: text.done, description: String(orderCode) });
      setAction(null);
    } catch (err) {
      toast.add({ type: "error", title: "Không thực hiện được", description: adminBillingErrorMessage(err, "Vui lòng thử lại.") });
      setAction(null);
    } finally {
      // Lỗi already_paid / not_mismatch thường do admin khác vừa xử lý (A10): luôn tải lại.
      setBusy(false);
      setReload((n) => n + 1);
      onChanged();
    }
  };

  const unpaid = order !== null && order.status !== "paid";
  const current = action ? ACTIONS[action] : null;

  const row = (label: string, value: React.ReactNode) => (
    <div className="grid grid-cols-[9rem_1fr] gap-2 text-sm">
      <dt className="text-muted-foreground">{label}</dt>
      <dd className="min-w-0 break-words">{value ?? "—"}</dd>
    </div>
  );

  return (
    <DetailPanel title={`Đơn ${orderCode}`} description={order?.planName} onClose={onClose} closeDisabled={busy}>
      {isLoading && <Skeleton className="h-64 w-full" />}
      {!isLoading && result.error && (
        <Alert variant="destructive">
          <AlertDescription>{result.error}</AlertDescription>
        </Alert>
      )}
      {order && (
        <>
          <dl className="space-y-2">
            {row("Trạng thái", <OrderStatusBadge status={order.status} />)}
            {row("Gói", `${order.planName} (${order.planCode})`)}
            {row("Số tiền", formatVnd(order.amountVnd))}
            {row("Nội dung CK", <span className="font-mono">{order.description}</span>)}
            {row("Người mua", order.buyerName)}
            {row("Email", order.buyerEmail)}
            {row("Tạo lúc", formatVnDateTime(order.createdAt))}
            {row("Hết hạn", formatVnDateTime(order.expiresAt))}
            {row("Thanh toán lúc", formatVnDateTime(order.paidAt))}
            {order.cycle && row("Chu kỳ", `${formatVnDateTime(order.cycle.startsAt)} → ${formatVnDateTime(order.cycle.endsAt)}${order.cycle.queued ? " (xếp hàng)" : ""}`)}
            {row("Ghi chú admin", order.adminNote)}
            {row("Người xử lý", order.handledBy)}
          </dl>

          {unpaid && (
            <div className="flex flex-wrap gap-2">
              <Button type="button" variant="outline" size="sm" onClick={() => openAction("recheck")}>Đối soát với payOS</Button>
              <Button type="button" size="sm" onClick={() => openAction("activate")}>Kích hoạt thủ công</Button>
              {order.status === "mismatch" && (
                <Button type="button" variant="destructive" size="sm" onClick={() => openAction("reject")}>Từ chối</Button>
              )}
            </div>
          )}

          <div className="space-y-1.5">
            <h3 className="text-sm font-medium">Payload từ payOS</h3>
            {order.payloadJson ? (
              <pre className="max-h-80 overflow-auto rounded-md bg-muted p-3 text-xs">{prettyJson(order.payloadJson)}</pre>
            ) : (
              <p className="text-sm text-muted-foreground">Chưa nhận webhook.</p>
            )}
          </div>
        </>
      )}

      <AlertDialog open={action !== null} onOpenChange={(open) => !open && !busy && setAction(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>{current?.title}</AlertDialogTitle>
            <AlertDialogDescription>{current?.description}</AlertDialogDescription>
          </AlertDialogHeader>
          {current?.needsNote && (
            <Textarea
              value={note}
              onChange={(e) => setNote(e.target.value)}
              rows={3}
              disabled={busy}
              placeholder="Ghi chú (bắt buộc)"
              aria-label="Ghi chú"
              aria-required
            />
          )}
          <AlertDialogFooter>
            <AlertDialogCancel disabled={busy}>Hủy</AlertDialogCancel>
            <Button
              type="button"
              variant={action === "reject" ? "destructive" : "default"}
              onClick={() => void run()}
              disabled={busy || (!!current?.needsNote && !note.trim())}
            >
              {busy && <Spinner className="size-3.5" />}
              {current?.confirm}
            </Button>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </DetailPanel>
  );
}
