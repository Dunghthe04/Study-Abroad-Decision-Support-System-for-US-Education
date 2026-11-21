"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import Image from "next/image";
import Link from "next/link";
import { QRCodeSVG } from "qrcode.react";
import {
  ArrowRightIcon,
  CheckCircle2Icon,
  CheckIcon,
  ClockIcon,
  CopyIcon,
  ExternalLinkIcon,
  FileCheck2Icon,
  QrCodeIcon,
  ReceiptTextIcon,
  ShieldCheckIcon,
  TriangleAlertIcon,
  XCircleIcon,
  ZapIcon,
} from "lucide-react";
import { ApiError } from "@/lib/api";
import {
  billingApi,
  formatVnd,
  formatVnDateTime,
  getBillingError,
} from "@/lib/billing-api";
import { cn } from "@/lib/utils";
import { ErrorFallback } from "@/components/ui/error-fallback";
import { Spinner } from "@/components/ui/spinner";
import type { OrderDto, OrderStatus } from "@/types/billing";

const POLL_MS = 3000;

const ACTION_ROW =
  "flex w-full items-center gap-3 px-5 py-3.5 text-left text-base transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-brand";

function CopyButton({ value, label }: { value: string; label: string }) {
  const [copied, setCopied] = useState(false);
  const timerRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  useEffect(() => {
    return () => {
      if (timerRef.current) clearTimeout(timerRef.current);
    };
  }, []);

  const copy = async () => {
    try {
      await navigator.clipboard.writeText(value);
      setCopied(true);
      if (timerRef.current) clearTimeout(timerRef.current);
      timerRef.current = setTimeout(() => setCopied(false), 1500);
    } catch {
      // Bỏ qua nếu trình duyệt chặn clipboard
    }
  };

  return (
    <button
      type="button"
      onClick={copy}
      aria-label={copied ? `Đã sao chép ${label}` : `Sao chép ${label}`}
      title={copied ? "Đã sao chép" : `Sao chép ${label}`}
      className="inline-flex size-8 shrink-0 cursor-pointer items-center justify-center rounded-lg border border-line bg-ground text-ink-2 transition-colors hover:bg-surface hover:text-ink"
    >
      {copied ? <CheckIcon className="size-4 text-ok" /> : <CopyIcon className="size-4" />}
    </button>
  );
}

function Detail({ label, wide, children }: { label: string; wide?: boolean; children: React.ReactNode }) {
  return (
    <div className={cn("min-w-0 space-y-1.5", wide && "sm:col-span-2")}>
      <dt className="text-sm font-medium text-ink-2">{label}</dt>
      <dd className="flex min-w-0 items-center gap-2 text-base font-semibold text-ink sm:text-lg">{children}</dd>
    </div>
  );
}

function Countdown({ createdAt, expiresAt }: { createdAt: string; expiresAt: string }) {
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

  const end = new Date(expiresAt).getTime();
  const total = Math.max(1, end - new Date(createdAt).getTime());
  const leftMs = now === null ? total : Math.max(0, end - now);
  const left = Math.floor(leftMs / 1000);
  const pct = Math.min(100, Math.max(0, (leftMs / total) * 100));
  const mm = String(Math.floor(left / 60)).padStart(2, "0");
  const ss = String(left % 60).padStart(2, "0");
  const urgent = left < 60;

  return (
    <div className="space-y-2.5">
      <div className="flex items-baseline justify-between gap-3">
        <span className="text-sm font-medium text-ink-2">Thời gian còn lại</span>
        <span role="timer" className={cn("text-lg font-bold tabular-nums", urgent ? "text-risk" : "text-ink")}>
          {now === null ? "--:--" : `${mm}:${ss}`}
        </span>
      </div>
      <div aria-hidden className="h-2 w-full overflow-hidden rounded-full bg-ground">
        <div
          className={cn(
            "h-full rounded-full transition-[width,background-color] duration-1000 ease-linear motion-reduce:transition-none",
            urgent ? "bg-risk" : left < 300 ? "bg-warn" : "bg-brand",
          )}
          style={{ width: `${pct}%` }}
        />
      </div>
    </div>
  );
}

type TerminalStatus = Exclude<OrderStatus, "pending">;

const TERMINAL_META: Record<
  TerminalStatus,
  {
    title: string;
    description: (order: OrderDto) => string;
    ctaLabel: string;
    ctaHref: string;
    tone: string;
    iconBg: string;
  }
> = {
  paid: {
    title: "Thanh toán thành công",
    description: (o) => `Gói ${o.planName} đã được kích hoạt cho tài khoản của bạn.`,
    ctaLabel: "Xem gói của tôi",
    ctaHref: "/account#subscription",
    tone: "text-ok",
    iconBg: "bg-ok-wash text-ok",
  },
  mismatch: {
    title: "Số tiền chuyển không khớp",
    description: (o) =>
      `Hệ thống nhận được giao dịch khác với số tiền ${formatVnd(o.amountVnd)}. Quản trị viên sẽ kiểm tra và xử lý đơn này.`,
    ctaLabel: "Lịch sử giao dịch",
    ctaHref: "/orders",
    tone: "text-warn",
    iconBg: "bg-warn-wash text-warn",
  },
  expired: {
    title: "Đơn đã hết hạn",
    description: () =>
      "Thời gian thanh toán cho đơn hàng này đã kết thúc. Bạn có thể tạo đơn mới bất cứ lúc nào.",
    ctaLabel: "Quay lại trang gói dịch vụ",
    ctaHref: "/plans",
    tone: "text-ink",
    iconBg: "bg-ground text-ink-2",
  },
  cancelled: {
    title: "Đơn đã bị hủy",
    description: () =>
      "Đơn hàng này đã được hủy và không còn hiệu lực thanh toán.",
    ctaLabel: "Quay lại trang gói dịch vụ",
    ctaHref: "/plans",
    tone: "text-ink",
    iconBg: "bg-ground text-ink-2",
  },
};

function TerminalStatusIcon({ status, className }: { status: TerminalStatus; className?: string }) {
  if (status === "paid") return <CheckCircle2Icon className={className} />;
  if (status === "mismatch") return <TriangleAlertIcon className={className} />;
  if (status === "expired") return <ClockIcon className={className} />;
  return <XCircleIcon className={className} />;
}

function PlanHeader({ order }: { order: OrderDto }) {
  const family = order.planKind === "family";
  return (
    <div className="flex flex-wrap items-center justify-between gap-x-6 gap-y-4 border-b border-line pb-6">
      <div className="flex items-center gap-4">
        <Image
          src={family ? "/plans/family_plan.png" : "/plans/personal_plan.png"}
          alt=""
          width={56}
          height={56}
          className="size-14 shrink-0 object-contain"
        />
        <div className="space-y-0.5">
          <h2 className="font-heading text-2xl font-semibold text-ink">{order.planName}</h2>
          <p className="text-sm text-ink-2 sm:text-[15px]">
            {family ? "Gói USDSS Premium Gia đình" : "Gói USDSS Premium Cá nhân"}
          </p>
        </div>
      </div>
      <p className="text-3xl font-bold tracking-tight text-ink tabular-nums sm:text-4xl">{formatVnd(order.amountVnd)}</p>
    </div>
  );
}

const GUIDE = [
  {
    icon: QrCodeIcon,
    title: "Quét mã QR",
    text: "Mở ứng dụng ngân hàng và quét mã của đơn này, hoặc mở trang thanh toán payOS.",
  },
  {
    icon: FileCheck2Icon,
    title: "Giữ nguyên nội dung",
    text: "Không sửa số tiền và nội dung chuyển khoản để hệ thống tự động đối soát.",
  },
  {
    icon: ZapIcon,
    title: "Gói được kích hoạt",
    text: "Gói Premium mở ngay khi ngân hàng xác nhận. Trang này tự cập nhật trạng thái.",
  },
];

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
  const meta = order.status === "pending" ? null : TERMINAL_META[order.status];

  return (
    <div className="w-full space-y-8">
      <h1 className="font-heading text-3xl font-semibold tracking-tight text-ink sm:text-4xl">
        Thanh toán
      </h1>

      {order.status === "pending" ? (
        <div className="grid gap-8 lg:grid-cols-3 lg:items-start">
          <div className="space-y-4 lg:col-span-1">
            <section className="space-y-5 rounded-2xl border border-line bg-surface p-6 sm:p-7">
              <div className="space-y-1.5">
                <h2 className="font-heading text-xl font-semibold text-ink sm:text-2xl">Quét mã QR để thanh toán</h2>
                <p className="text-sm leading-relaxed text-ink-2 sm:text-[15px]">
                  Mở ứng dụng ngân hàng hoặc ví điện tử hỗ trợ VietQR.
                </p>
              </div>

              <div className="mx-auto flex w-fit items-center justify-center rounded-2xl border border-line bg-white p-5">
                {qr.startsWith("http") ? (
                  // eslint-disable-next-line @next/next/no-img-element -- ảnh QR từ payOS, không qua tối ưu ảnh của Next
                  <img src={qr} alt="Mã QR thanh toán" className="size-58" />
                ) : (
                  <QRCodeSVG value={qr} size={232} aria-label="Mã QR thanh toán" />
                )}
              </div>

              <Countdown createdAt={order.createdAt} expiresAt={order.expiresAt} />
            </section>

            <div className="overflow-hidden rounded-2xl border border-line bg-surface divide-y divide-line">
              <a
                href={order.checkoutUrl}
                target="_blank"
                rel="noopener noreferrer"
                className={cn(ACTION_ROW, "font-semibold text-brand hover:bg-brand-wash/50")}
              >
                <ExternalLinkIcon className="size-5 shrink-0" aria-hidden />
                <span>Mở trang thanh toán payOS</span>
              </a>
              <Link
                href="/orders"
                className={cn(ACTION_ROW, "font-medium text-ink hover:bg-ground")}
              >
                <ReceiptTextIcon className="size-5 shrink-0 text-ink-2" aria-hidden />
                <span>Lịch sử giao dịch</span>
              </Link>
              <button
                type="button"
                onClick={cancel}
                disabled={cancelling}
                className={cn(
                  ACTION_ROW,
                  "cursor-pointer font-medium text-risk hover:bg-risk-wash/60 disabled:cursor-not-allowed disabled:opacity-50",
                )}
              >
                {cancelling ? (
                  <Spinner className="size-5 shrink-0" />
                ) : (
                  <XCircleIcon className="size-5 shrink-0" aria-hidden />
                )}
                <span>Hủy đơn hàng</span>
              </button>
            </div>
            {actionError && (
              <p role="alert" className="text-sm text-risk">
                {actionError}
              </p>
            )}
          </div>

          <div className="space-y-6 lg:col-span-2">
            <section className="rounded-2xl border border-line bg-surface p-6 sm:p-8">
              <PlanHeader order={order} />
              <dl className="grid gap-x-8 gap-y-6 pt-6 sm:grid-cols-2">
                <Detail label="Mã đơn hàng">
                  <span className="tabular-nums">#{order.orderCode}</span>
                </Detail>
                <Detail label="Hạn thanh toán">
                  <span className="tabular-nums">{formatVnDateTime(order.expiresAt)}</span>
                </Detail>
                <Detail label="Số tiền cần chuyển">
                  <span className="tabular-nums">{formatVnd(order.amountVnd)}</span>
                  <CopyButton value={String(order.amountVnd)} label="số tiền" />
                </Detail>
                <Detail label="Nội dung chuyển khoản">
                  <span className="min-w-0 font-mono break-all">{order.description}</span>
                  <CopyButton value={order.description} label="nội dung chuyển khoản" />
                </Detail>
              </dl>
            </section>

            <section className="rounded-2xl border border-line bg-surface p-6 sm:p-8">
              <h2 className="font-heading text-xl font-semibold text-ink sm:text-2xl">Hướng dẫn thanh toán</h2>
              <ol className="mt-6 grid gap-6 sm:grid-cols-3 sm:gap-8">
                {GUIDE.map(({ icon: Icon, title, text }) => (
                  <li
                    key={title}
                    className="relative space-y-3 sm:not-last:after:absolute sm:not-last:after:top-6 sm:not-last:after:left-16 sm:not-last:after:-right-4 sm:not-last:after:h-px sm:not-last:after:bg-line"
                  >
                    <span className="flex size-12 items-center justify-center rounded-full bg-brand-wash text-brand">
                      <Icon aria-hidden className="size-5" />
                    </span>
                    <div className="space-y-1">
                      <p className="text-base font-semibold text-ink">{title}</p>
                      <p className="text-sm leading-relaxed text-ink-2 sm:text-[15px]">{text}</p>
                    </div>
                  </li>
                ))}
              </ol>
              <div className="mt-8 flex items-start gap-3 border-t border-line pt-5">
                <ShieldCheckIcon aria-hidden className="mt-0.5 size-5 shrink-0 text-ok" />
                <p className="text-sm leading-relaxed text-ink-2 sm:text-[15px]">
                  Thanh toán qua payOS bằng VietQR. Đơn chỉ được kích hoạt khi ngân hàng xác nhận đúng số tiền và nội dung.
                </p>
              </div>
            </section>
          </div>
        </div>
      ) : (
        meta && (
          <div className="grid gap-8 lg:grid-cols-3 lg:items-start">
            <div className="space-y-4 lg:col-span-1">
              <section className="space-y-5 rounded-2xl border border-line bg-surface p-6 sm:p-7">
                <span className={cn("inline-flex size-12 items-center justify-center rounded-xl", meta.iconBg)}>
                  <TerminalStatusIcon status={order.status} className="size-6" />
                </span>
                <div className="space-y-2">
                  <h2 className={cn("font-heading text-xl font-semibold sm:text-2xl", meta.tone)}>{meta.title}</h2>
                  <p className="text-sm leading-relaxed text-ink-2 sm:text-[15px]">{meta.description(order)}</p>
                </div>
              </section>

              <div className="overflow-hidden rounded-2xl border border-line bg-surface divide-y divide-line">
                <Link
                  href={meta.ctaHref}
                  className={cn(ACTION_ROW, "font-semibold text-brand hover:bg-brand-wash/50")}
                >
                  {meta.ctaHref === "/orders" ? (
                    <ReceiptTextIcon className="size-5 shrink-0" aria-hidden />
                  ) : (
                    <ArrowRightIcon className="size-5 shrink-0" aria-hidden />
                  )}
                  <span>{meta.ctaLabel}</span>
                </Link>
                {meta.ctaHref !== "/orders" && (
                  <Link
                    href="/orders"
                    className={cn(ACTION_ROW, "font-medium text-ink hover:bg-ground")}
                  >
                    <ReceiptTextIcon className="size-5 shrink-0 text-ink-2" aria-hidden />
                    <span>Lịch sử giao dịch</span>
                  </Link>
                )}
              </div>
            </div>

            <section className="rounded-2xl border border-line bg-surface p-6 sm:p-8 lg:col-span-2">
              <PlanHeader order={order} />
              <dl className="grid gap-x-8 gap-y-6 pt-6 sm:grid-cols-2">
                <Detail label="Mã đơn hàng">
                  <span className="tabular-nums">#{order.orderCode}</span>
                </Detail>
                <Detail label="Nội dung chuyển khoản">
                  <span className="min-w-0 font-mono break-all">{order.description}</span>
                </Detail>
                <Detail label="Thời điểm tạo">
                  <span className="tabular-nums">{formatVnDateTime(order.createdAt)}</span>
                </Detail>
                {order.paidAt && (
                  <Detail label="Thời điểm thanh toán">
                    <span className="tabular-nums">{formatVnDateTime(order.paidAt)}</span>
                  </Detail>
                )}
                {order.cycle && (
                  <>
                    <Detail label="Bắt đầu">
                      <span className="tabular-nums">{formatVnDateTime(order.cycle.startsAt)}</span>
                    </Detail>
                    <Detail label="Kết thúc">
                      <span className="tabular-nums">{formatVnDateTime(order.cycle.endsAt)}</span>
                    </Detail>
                  </>
                )}
              </dl>
            </section>
          </div>
        )
      )}
    </div>
  );
}
