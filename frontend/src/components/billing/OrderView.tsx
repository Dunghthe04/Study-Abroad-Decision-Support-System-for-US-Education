"use client";

import { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { QRCodeSVG } from "qrcode.react";
import { CheckIcon, ClockIcon, ExternalLinkIcon, TriangleAlertIcon } from "lucide-react";
import { ApiError } from "@/lib/api";
import { billingApi, formatVnd, formatVnDateTime, getBillingError, ORDER_STATUS_BADGE, ORDER_STATUS_LABEL } from "@/lib/billing-api";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button, buttonVariants } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { ErrorFallback } from "@/components/ui/error-fallback";
import { Spinner } from "@/components/ui/spinner";
import type { OrderDto } from "@/types/billing";

const POLL_MS = 3000;

function Row({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div className="flex flex-wrap justify-between gap-2 border-b border-line py-2 text-sm last:border-0">
      <span className="text-muted-foreground">{label}</span>
      <span className="font-medium">{children}</span>
    </div>
  );
}

function Countdown({ expiresAt }: { expiresAt: string }) {
  // "Bây giờ" chỉ đổi trong callback của interval (không gọi Date.now() khi render)
  const [now, setNow] = useState<number | null>(null);
  useEffect(() => {
    const tick = () => setNow(Date.now());
    const first = setTimeout(tick, 0);
    const id = setInterval(tick, 1000);
    return () => {
      clearTimeout(first);
      clearInterval(id);
    };
  }, []);
  if (now === null) return null;
  const left = Math.max(0, Math.floor((new Date(expiresAt).getTime() - now) / 1000));
  const mm = String(Math.floor(left / 60)).padStart(2, "0");
  const ss = String(left % 60).padStart(2, "0");
  return (
    <span className="tabular-nums">
      {left > 0 ? `Còn ${mm}:${ss} để thanh toán` : "Đơn đã quá hạn thanh toán, đang cập nhật trạng thái..."}
    </span>
  );
}

// [UC-78] Trang đơn hàng. Quay lại từ payOS không tự kích hoạt gói: chỉ webhook (hoặc quản trị viên) mới đổi trạng thái,
// trang này chỉ hỏi lại máy chủ mỗi 3 giây khi tab đang mở và đơn còn chờ.
export function OrderView({ orderCode }: { orderCode: string }) {
  const validCode = /^\d+$/.test(orderCode);
  const [order, setOrder] = useState<OrderDto | null>(null);
  const [notFound, setNotFound] = useState(!validCode);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [cancelling, setCancelling] = useState(false);
  const [actionError, setActionError] = useState<string | null>(null);

  const load = useCallback(
    () =>
      billingApi.getOrder(orderCode).then(
        (o) => {
          setOrder(o);
          setLoadError(null);
        },
        (err: unknown) => {
          if (err instanceof ApiError && (err.status === 404 || err.status === 403)) setNotFound(true);
          else setLoadError(err instanceof Error ? err.message : "Không tải được đơn hàng.");
        },
      ),
    [orderCode],
  );

  useEffect(() => {
    if (validCode) load();
  }, [validCode, load]);

  const pending = order?.status === "pending";
  useEffect(() => {
    if (!pending) return;
    const visible = () => document.visibilityState === "visible";
    const id = setInterval(() => {
      if (visible()) load();
    }, POLL_MS);
    const onVisible = () => {
      if (visible()) load();
    };
    document.addEventListener("visibilitychange", onVisible);
    return () => {
      clearInterval(id);
      document.removeEventListener("visibilitychange", onVisible);
    };
  }, [pending, load]);

  const cancel = async () => {
    setCancelling(true);
    setActionError(null);
    try {
      setOrder(await billingApi.cancelOrder(orderCode));
    } catch (err) {
      const e = getBillingError(err);
      if (e?.code === "not_pending") await load();
      else setActionError(e?.message ?? (err instanceof Error ? err.message : "Chưa hủy được đơn."));
    } finally {
      setCancelling(false);
    }
  };

  if (notFound) {
    return (
      <ErrorFallback
        title="Không tìm thấy đơn hàng"
        description="Đơn hàng không tồn tại hoặc không thuộc tài khoản của bạn."
        action={{ label: "Lịch sử giao dịch", href: "/orders" }}
      />
    );
  }
  if (!order) {
    return loadError ? (
      <ErrorFallback title="Không tải được đơn hàng" description={loadError} action={{ label: "Thử lại", onClick: load }} />
    ) : (
      <div className="flex min-h-[40vh] items-center justify-center">
        <Spinner className="size-8 text-brand" />
      </div>
    );
  }

  const qr = order.qrCode;
  return (
    <div className="mx-auto w-full max-w-xl space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h1 className="text-h1">Đơn hàng #{order.orderCode}</h1>
        <Badge variant={ORDER_STATUS_BADGE[order.status]}>
          {ORDER_STATUS_LABEL[order.status]}
        </Badge>
      </div>

      {order.status === "pending" && (
        <Card>
          <CardHeader>
            <CardTitle>Quét mã QR để thanh toán</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="flex justify-center">
              {qr.startsWith("http") ? (
                // eslint-disable-next-line @next/next/no-img-element -- ảnh QR từ payOS, không qua tối ưu ảnh của Next
                <img src={qr} alt="Mã QR thanh toán" className="size-60" />
              ) : (
                <div className="rounded-lg bg-white p-3">
                  <QRCodeSVG value={qr} size={224} aria-label="Mã QR thanh toán" />
                </div>
              )}
            </div>
            <div>
              <Row label="Gói">{order.planName}</Row>
              <Row label="Số tiền">{formatVnd(order.amountVnd)}</Row>
              <Row label="Nội dung chuyển khoản">
                <span className="font-mono">{order.description}</span>
              </Row>
            </div>
            <Alert variant="info" role="status">
              <ClockIcon />
              <AlertTitle className="flex items-center gap-2">
                <Spinner className="size-3.5" />
                Đang chờ xác nhận thanh toán
              </AlertTitle>
              <AlertDescription>
                <Countdown expiresAt={order.expiresAt} />
                <span className="block">Gói được kích hoạt khi hệ thống nhận được xác nhận từ ngân hàng, trang này tự cập nhật.</span>
              </AlertDescription>
            </Alert>
            {actionError && <p className="text-sm text-risk">{actionError}</p>}
            <div className="flex flex-wrap gap-2">
              <a href={order.checkoutUrl} target="_blank" rel="noopener noreferrer" className={buttonVariants()}>
                Mở trang thanh toán payOS
                <ExternalLinkIcon />
              </a>
              <Button type="button" variant="outline" onClick={cancel} disabled={cancelling}>
                {cancelling ? "Đang hủy..." : "Hủy đơn"}
              </Button>
            </div>
          </CardContent>
        </Card>
      )}

      {order.status === "paid" && (
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2 text-ok">
              <CheckIcon />
              Thanh toán thành công
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <div>
              <Row label="Mã đơn">{order.orderCode}</Row>
              <Row label="Gói">{order.planName}</Row>
              <Row label="Số tiền">{formatVnd(order.amountVnd)}</Row>
              <Row label="Thời điểm thanh toán">{formatVnDateTime(order.paidAt)}</Row>
              {order.cycle?.queued ? (
                <Row label="Kỳ sử dụng">Đã xếp hàng, bắt đầu khi kỳ hiện tại kết thúc hoặc dùng hết</Row>
              ) : (
                order.cycle && (
                  <>
                    <Row label="Bắt đầu">{formatVnDateTime(order.cycle.startsAt)}</Row>
                    <Row label="Kết thúc">{formatVnDateTime(order.cycle.endsAt)}</Row>
                  </>
                )
              )}
            </div>
            <Link href="/account#subscription" className={buttonVariants()}>
              Xem gói của tôi
            </Link>
          </CardContent>
        </Card>
      )}

      {order.status === "mismatch" && (
        <Alert variant="warning">
          <TriangleAlertIcon />
          <AlertTitle>Số tiền chuyển không khớp, quản trị viên sẽ xử lý</AlertTitle>
          <AlertDescription>
            Đơn {order.orderCode}, gói {order.planName}, số tiền cần chuyển {formatVnd(order.amountVnd)}.
          </AlertDescription>
        </Alert>
      )}

      {(order.status === "expired" || order.status === "cancelled") && (
        <Card>
          <CardContent className="space-y-3">
            <p className="text-sm">
              Đơn {order.orderCode} (gói {order.planName}, {formatVnd(order.amountVnd)}){" "}
              {order.status === "expired" ? "đã hết hạn thanh toán." : "đã bị hủy."}
            </p>
            <Link href="/plans" className={buttonVariants()}>
              Quay lại trang gói dịch vụ
            </Link>
          </CardContent>
        </Card>
      )}

      <Link href="/orders" className="inline-block text-sm text-muted-foreground underline">
        Lịch sử giao dịch
      </Link>
    </div>
  );
}
