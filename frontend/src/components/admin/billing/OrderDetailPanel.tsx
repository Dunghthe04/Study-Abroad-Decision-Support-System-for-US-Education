"use client";

import { useEffect, useState } from "react";
import { EyeIcon } from "lucide-react";
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
import type { AdminOrderBuyerInfoDto, AdminOrderDetailDto, AdminOrderRecheckDto } from "@/types/admin-billing";
import { DetailPanel } from "../shared/DetailPanel";
import { OrderStatusBadge } from "./OrderStatusBadge";

type Action = "activate" | "reject";

const ACTIONS: Record<Action, { title: string; description: string; confirm: string; done: string }> = {
  activate: {
    title: "Kích hoạt gói thủ công?",
    description: "Đơn được chuyển sang Đã thanh toán và người mua nhận gói. Nhập lý do kích hoạt (bắt buộc).",
    confirm: "Kích hoạt",
    done: "Đã kích hoạt đơn",
  },
  reject: {
    title: "Hủy đơn lệch số tiền?",
    description: "Đơn bị hủy và người mua được thông báo. Nhập ghi chú bắt buộc về cách hoàn tiền ngoài hệ thống.",
    confirm: "Hủy đơn",
    done: "Đã hủy đơn",
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

/** [UC-124..126] Chi tiết đơn: mã GD payOS, số tiền nhận, thông tin người mua đã xóa (UC-91), đối soát payOS, kích hoạt, hủy. */
export function OrderDetailPanel({ orderCode, onClose, onChanged }: OrderDetailPanelProps) {
  const [reload, setReload] = useState(0);
  const [result, setResult] = useState<LoadResult | null>(null);
  const [action, setAction] = useState<Action | null>(null);
  const [note, setNote] = useState("");
  const [busy, setBusy] = useState(false);
  const [rechecking, setRechecking] = useState(false);
  const [recheck, setRecheck] = useState<AdminOrderRecheckDto | null>(null);
  const [revealing, setRevealing] = useState(false);
  const [revealedBuyer, setRevealedBuyer] = useState<AdminOrderBuyerInfoDto | null>(null);

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

  const runRecheck = async () => {
    setRechecking(true);
    try {
      const checked = await adminBillingApi.recheck(orderCode);
      setRecheck(checked);
      toast.add({
        type: checked.gatewayStatus === "PAID" ? "success" : "info",
        title: `payOS: ${checked.gatewayStatus}`,
        description: `Số tiền payOS ghi nhận: ${formatVnd(checked.gatewayAmountPaid)}`,
      });
    } catch (err) {
      toast.add({ type: "error", title: "Không kiểm tra được với payOS", description: adminBillingErrorMessage(err, "Vui lòng thử lại.") });
    } finally {
      setRechecking(false);
    }
  };

  const revealBuyer = async () => {
    setRevealing(true);
    try {
      const info = await adminBillingApi.revealBuyer(orderCode);
      setRevealedBuyer(info);
    } catch (err) {
      toast.add({ type: "error", title: "Không xem được thông tin người mua", description: adminBillingErrorMessage(err, "Vui lòng thử lại.") });
    } finally {
      setRevealing(false);
    }
  };

  const run = async () => {
    if (!action) return;
    const text = ACTIONS[action];
    setBusy(true);
    try {
      if (action === "activate") await adminBillingApi.activate(orderCode, note.trim());
      else await adminBillingApi.reject(orderCode, note.trim());
      toast.add({ type: "success", title: text.done, description: String(orderCode) });
      setAction(null);
      setRecheck(null);
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
  const isMismatch = order?.status === "mismatch";
  const isDeletedBuyer = order !== null && !order.buyerName && order.deletedBuyerCode !== null;
  const canActivate = isMismatch || recheck?.gatewayStatus === "PAID";
  const current = action ? ACTIONS[action] : null;

  const missing = <span title="Chưa có dữ liệu" aria-label="Chưa có dữ liệu" className="text-muted-foreground">—</span>;
  const row = (label: string, value: React.ReactNode) => (
    <div className="grid grid-cols-[9rem_1fr] gap-2 text-sm">
      <dt className="text-muted-foreground">{label}</dt>
      <dd className="min-w-0 break-words">{value ?? missing}</dd>
    </div>
  );

  return (
    <DetailPanel
      title={`Đơn ${orderCode}`}
      description={order?.planName}
      onClose={onClose}
      closeDisabled={busy || rechecking}
      footer={order && unpaid ? (
        <>
          <Button type="button" variant="outline" size="sm" onClick={() => void runRecheck()} disabled={rechecking || busy}>
            {rechecking && <Spinner className="size-3.5" />}
            Kiểm tra với payOS
          </Button>
          <Button
            type="button"
            size="sm"
            onClick={() => openAction("activate")}
            disabled={!canActivate || busy}
            title={!canActivate ? "Cần kiểm tra với payOS và payOS xác nhận PAID trước khi kích hoạt" : undefined}
          >
            Kích hoạt thủ công
          </Button>
          {isMismatch && (
            <Button type="button" variant="destructive" size="sm" onClick={() => openAction("reject")} disabled={busy}>
              Hủy đơn
            </Button>
          )}
        </>
      ) : undefined}
    >
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
            {row("Mã GD payOS", order.gatewayTransactionId ? <span className="font-mono text-xs">{order.gatewayTransactionId}</span> : null)}
            {row("Gói", `${order.planName} (${order.planCode})`)}
            {row("Giá đơn đã lưu", formatVnd(order.amountVnd))}
            {(isMismatch || order.receivedAmountVnd !== null) && row(
              "Số tiền đã nhận",
              order.receivedAmountVnd !== null ? (
                <span className={order.receivedAmountVnd !== order.amountVnd ? "font-semibold text-risk" : "font-medium"}>
                  {formatVnd(order.receivedAmountVnd)}
                  {order.receivedAmountVnd !== order.amountVnd && (
                    <span className="ml-1.5 text-xs font-normal text-muted-foreground">
                      (giá đơn: {formatVnd(order.amountVnd)})
                    </span>
                  )}
                </span>
              ) : null,
            )}
            {row("Nội dung CK", <span className="font-mono">{order.description}</span>)}
            {isDeletedBuyer ? (
              <>
                {row("Người mua", (
                  <div className="space-y-2">
                    <div>
                      <span className="font-mono text-xs font-medium">{order.deletedBuyerCode}</span>
                      <span className="ml-1.5 text-xs text-muted-foreground">(Tài khoản đã xóa)</span>
                    </div>
                    {!revealedBuyer ? (
                      <Button type="button" variant="outline" size="sm" onClick={() => void revealBuyer()} disabled={revealing}>
                        {revealing ? <Spinner className="size-3.5" /> : <EyeIcon className="size-3.5" />}
                        Xem thông tin người mua
                      </Button>
                    ) : (
                      <div className="rounded-md border border-line bg-muted/50 p-2.5 text-xs space-y-1">
                        <div><span className="text-muted-foreground">Họ tên:</span> <span className="font-medium">{revealedBuyer.fullName}</span></div>
                        <div><span className="text-muted-foreground">Email:</span> <span className="font-medium">{revealedBuyer.email}</span></div>
                        <div className="text-[11px] text-muted-foreground">
                          Lưu kế toán tới {formatVnDateTime(revealedBuyer.retainUntilUtc)} · Đã ghi nhật ký truy cập
                        </div>
                      </div>
                    )}
                    {isMismatch && !revealedBuyer && (
                      <p className="text-xs text-muted-foreground">
                        Bấm &ldquo;Xem thông tin người mua&rdquo; để lấy email đã lưu cho việc hoàn tiền ngoài hệ thống.
                      </p>
                    )}
                  </div>
                ))}
              </>
            ) : (
              <>
                {row("Người mua", order.buyerName)}
                {row("Email", order.buyerEmail)}
              </>
            )}
            {row("Tạo lúc", formatVnDateTime(order.createdAt))}
            {row("Hết hạn", formatVnDateTime(order.expiresAt))}
            {row("Thanh toán lúc", order.paidAt ? formatVnDateTime(order.paidAt) : null)}
            {order.cycle && row("Chu kỳ", `${formatVnDateTime(order.cycle.startsAt)} → ${formatVnDateTime(order.cycle.endsAt)}`)}
            {row("Ghi chú admin", order.adminNote)}
            {row("Người xử lý", order.handledBy)}
          </dl>

          {recheck && (
            <Alert variant={recheck.gatewayStatus === "PAID" ? "success" : "warning"}>
              <AlertDescription className="space-y-1 text-xs">
                <div>
                  Kết quả payOS lúc {formatVnDateTime(recheck.checkedAt)}: <strong>{recheck.gatewayStatus}</strong> · Đã nhận{" "}
                  <strong>{formatVnd(recheck.gatewayAmountPaid)}</strong>
                </div>
                {!isMismatch && recheck.gatewayStatus !== "PAID" && (
                  <div>payOS chưa ghi nhận thanh toán — không thể kích hoạt thủ công.</div>
                )}
              </AlertDescription>
            </Alert>
          )}

          {unpaid && !isMismatch && !recheck && (
            <p className="text-xs text-muted-foreground">
              Bấm &ldquo;Kiểm tra với payOS&rdquo; trước; chỉ kích hoạt được khi payOS xác nhận PAID.
            </p>
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
          {action === "reject" && isDeletedBuyer && (
            <div className="rounded-md border border-line bg-muted/50 p-2.5 text-xs space-y-1">
              {revealedBuyer ? (
                <>
                  <div className="font-medium">Email hoàn tiền ngoài hệ thống: {revealedBuyer.email}</div>
                  <div className="text-muted-foreground">Người mua: {revealedBuyer.fullName} ({revealedBuyer.buyerCode})</div>
                </>
              ) : (
                <div className="flex items-center justify-between gap-2">
                  <span>Tài khoản người mua đã xóa. Xem email lưu trữ để hoàn tiền:</span>
                  <Button type="button" variant="outline" size="sm" onClick={() => void revealBuyer()} disabled={revealing}>
                    {revealing ? <Spinner className="size-3.5" /> : "Xem email"}
                  </Button>
                </div>
              )}
            </div>
          )}
          <Textarea
            value={note}
            onChange={(e) => setNote(e.target.value)}
            rows={3}
            disabled={busy}
            placeholder={action === "reject" ? "Ghi chú cách hoàn tiền ngoài hệ thống (bắt buộc)" : "Lý do kích hoạt (bắt buộc)"}
            aria-label="Ghi chú"
            aria-required
          />
          <AlertDialogFooter>
            <AlertDialogCancel disabled={busy}>Hủy</AlertDialogCancel>
            <Button
              type="button"
              variant={action === "reject" ? "destructive" : "default"}
              onClick={() => void run()}
              disabled={busy || !note.trim()}
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
