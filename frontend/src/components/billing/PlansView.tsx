"use client";

import { useState, type ReactNode } from "react";
import Image from "next/image";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { CheckIcon, ClockIcon } from "lucide-react";
import { useAuth } from "@/contexts/AuthContext";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button, buttonVariants } from "@/components/ui/button";
import { ErrorFallback } from "@/components/ui/error-fallback";
import { Spinner } from "@/components/ui/spinner";
import { billingApi, formatMultiplier, formatVnd, getBillingError } from "@/lib/billing-api";
import { useFetched } from "@/lib/use-fetched";
import { cn } from "@/lib/utils";
import type { PlanDto, PlanKind, SubscriptionSummaryDto } from "@/types/billing";

const AGE_REASON = "Tài khoản dưới 16 tuổi chưa thể mua gói";
const KIND_ORDER: readonly PlanKind[] = ["free", "personal", "family"];

const PLAN_IMAGE: Record<PlanKind, string> = {
  free: "/plans/free_plan.png",
  personal: "/plans/personal_plan.png",
  family: "/plans/family_plan.png",
};

function buildFeatures(plan: PlanDto): ReactNode[] {
  if (plan.kind === "free") {
    return [
      plan.quotaPeriodDays
        ? `Hạn mức cơ bản làm mới mỗi ${plan.quotaPeriodDays} ngày`
        : "Hạn mức cơ bản làm mới ngày 1 hàng tháng",
      "Hỏi đáp kiến thức du học Mỹ với USDSS Advisor",
      "Tra cứu và so sánh số liệu các trường đại học Mỹ",
      "Lưu danh sách trường mục tiêu và theo dõi hồ sơ cơ bản",
    ];
  }

  const quotaBullet: ReactNode =
    plan.quotaMultiplier != null ? (
      <>
        <strong className="font-semibold text-ink">Gấp {formatMultiplier(plan.quotaMultiplier)}x hạn mức</strong> so với gói Miễn phí
        {plan.quotaPeriodDays && plan.durationDays && plan.quotaPeriodDays < plan.durationDays
          ? `, cấp lại mỗi ${plan.quotaPeriodDays} ngày`
          : ` trong ${plan.durationDays} ngày`}
      </>
    ) : (
      `Hạn mức ưu tiên trong ${plan.durationDays} ngày`
    );

  if (plan.kind === "personal") {
    return [
      quotaBullet,
      "Mở khóa toàn bộ trợ lý AI USDSS Advisor (đánh giá hồ sơ & lập lộ trình)",
      "Gợi ý danh sách trường phân nhóm An toàn – Vừa sức – Mạo hiểm",
      "Mô phỏng kịch bản What-if và tính mục tiêu Goal-seeking",
      "Phân tích độ phù hợp từng trường và gợi ý trung tâm tư vấn",
      "Dành cho 1 tài khoản cá nhân",
    ];
  }

  const students = plan.seats?.students ?? 1;
  const parents = plan.seats?.parents ?? 2;
  return [
    quotaBullet,
    "Toàn bộ tính năng của gói USDSS Premium Cá nhân",
    `Chia sẻ tối đa ${students + parents} tài khoản (${students} Học sinh, ${parents} Phụ huynh) dùng chung hạn mức`,
    "Quản lý thành viên, gửi lời mời qua email và theo dõi tỷ lệ sử dụng",
    "Tài khoản dưới 16 tuổi vẫn được tham gia khi chủ gói mời",
  ];
}

export function PlansView() {
  const router = useRouter();
  const { user, isLoading: authLoading } = useAuth();
  const canBuy = user?.role === "student" || user?.role === "parent";

  const [subVersion, setSubVersion] = useState(0);
  const plans = useFetched("plans", billingApi.plans);
  // Chỉ học sinh/phụ huynh mới có GET subscription; khách gọi sẽ đi vào luồng 401 làm mới phiên
  const sub = useFetched<SubscriptionSummaryDto | null>(canBuy ? `sub:${user?.id}:${subVersion}` : "none", () =>
    canBuy ? billingApi.subscription() : Promise.resolve(null),
  );

  const [selectedByKind, setSelectedByKind] = useState<Partial<Record<PlanKind, string>>>({});
  const [busy, setBusy] = useState<string | null>(null);
  const [messages, setMessages] = useState<Record<string, string>>({});
  const [ageBlocked, setAgeBlocked] = useState(false);
  const [cancellingPending, setCancellingPending] = useState(false);
  const [pendingError, setPendingError] = useState<string | null>(null);

  const cancelPendingOrder = async () => {
    const orderCode = sub.data?.pendingOrderCode;
    if (!orderCode) return;
    setCancellingPending(true);
    setPendingError(null);
    try {
      await billingApi.cancelOrder(orderCode);
      setSubVersion((v) => v + 1);
    } catch (err) {
      const e = getBillingError(err);
      if (e?.code === "not_pending") setSubVersion((v) => v + 1);
      else setPendingError(e?.message ?? (err instanceof Error ? err.message : "Chưa hủy được đơn."));
    } finally {
      setCancellingPending(false);
    }
  };

  const buy = async (code: string) => {
    setBusy(code);
    setMessages((m) => ({ ...m, [code]: "" }));
    try {
      const order = await billingApi.createOrder(code);
      router.push(`/orders/${order.orderCode}`);
    } catch (err) {
      const e = getBillingError(err);
      if (e?.code === "pending_order_exists" && e.orderCode) {
        router.push(`/orders/${e.orderCode}`);
        return;
      }
      if (e?.code === "age_restricted") setAgeBlocked(true);
      else setMessages((m) => ({ ...m, [code]: e?.message ?? (err instanceof Error ? err.message : "Chưa tạo được đơn hàng.") }));
    } finally {
      setBusy(null);
    }
  };

  if (plans.loading && !plans.data) {
    return (
      <div className="flex min-h-[40vh] items-center justify-center">
        <Spinner className="size-8 text-brand" />
      </div>
    );
  }
  if (!plans.data) {
    return <ErrorFallback title="Không tải được bảng giá" description={plans.error ?? undefined} />;
  }

  const s = sub.data;
  const familyMember = s?.family?.role === "member";

  const grouped = KIND_ORDER.map((kind) => {
    const items = plans.data!
      .filter((p) => p.kind === kind)
      .sort((a, b) => (a.durationDays ?? 0) - (b.durationDays ?? 0));
    return { kind, items };
  }).filter((g) => g.items.length > 0);

  const activeCodeForKind = (kind: PlanKind): string | undefined => {
    if (kind === "personal") return s?.personal?.planCode;
    if (kind === "family") return s?.family?.planCode;
    return undefined;
  };

  const renderStatusBadge = (plan: PlanDto) => {
    if (!s) return null;
    if (plan.kind === "free" && s.tier === "free") return <Badge variant="neutral">Đang dùng</Badge>;
    if (plan.kind === "personal" && s.personal?.planCode === plan.code) return <Badge variant="ok">Gói hiện tại</Badge>;
    if (plan.kind === "family" && s.family?.planCode === plan.code) {
      return (
        <Badge variant="ok">{familyMember ? `Thành viên gói Gia đình của ${s.family.ownerName}` : "Gói hiện tại"}</Badge>
      );
    }
    return null;
  };

  const renderCta = (plan: PlanDto) => {
    const ctaClass = "h-11 w-full rounded-xl font-semibold";
    if (authLoading) return null;

    if (plan.kind === "free") {
      if (!user) {
        return (
          <Link href="/register" className={buttonVariants({ variant: "outline", className: ctaClass })}>
            Đăng ký miễn phí
          </Link>
        );
      }
      if (!canBuy) return null;
      return (
        <Link href="/account#subscription" className={buttonVariants({ variant: "outline", className: ctaClass })}>
          Quản lý gói của bạn
        </Link>
      );
    }

    if (!user) {
      return (
        <Link
          href="/login?redirect=/plans"
          className={buttonVariants({ variant: plan.kind === "personal" ? "default" : "outline", className: ctaClass })}
        >
          Đăng nhập để mua
        </Link>
      );
    }
    if (!canBuy) return null;
    if (plan.kind === "family" && familyMember) {
      return (
        <div className="rounded-xl border border-line bg-ground px-3.5 py-2.5 text-xs leading-relaxed text-ink-2">
          Bạn đang là thành viên gói Gia đình của <span className="font-medium text-ink">{s?.family?.ownerName}</span>
        </div>
      );
    }

    const renew = plan.kind === "personal" ? !!s?.personal : !!s?.family;
    return (
      <Button
        type="button"
        variant={plan.kind === "personal" ? "default" : "outline"}
        className={ctaClass}
        loading={busy === plan.code}
        disabled={ageBlocked || (busy !== null && busy !== plan.code)}
        title={ageBlocked ? AGE_REASON : undefined}
        onClick={() => void buy(plan.code)}
      >
        {renew ? "Gia hạn" : "Thanh toán"}
      </Button>
    );
  };

  return (
    <div className="w-full space-y-10 sm:space-y-14">
      <h1 className="font-heading text-center text-3xl font-semibold tracking-tight text-ink sm:text-4xl lg:text-[2.625rem]">
        Chọn gói phù hợp với bạn
      </h1>

      {s?.pendingOrderCode && (
        <div
          role="status"
          className="rounded-2xl border border-brand/20 bg-brand-wash/60 px-5 py-4 sm:px-6 sm:py-4"
        >
          <div className="flex flex-wrap items-center justify-between gap-4">
            <div className="flex items-center gap-3.5">
              <ClockIcon className="size-5 shrink-0 text-brand" aria-hidden />
              <span className="text-sm font-medium text-ink sm:text-base">
                Bạn có một đơn hàng đang chờ thanh toán.
              </span>
            </div>
            <div className="flex flex-wrap items-center gap-2.5">
              <Link
                href={`/orders/${s.pendingOrderCode}`}
                className={buttonVariants({ size: "sm", className: "h-9 rounded-lg px-4 font-semibold" })}
              >
                Thanh toán
              </Link>
              <Button
                type="button"
                variant="outline"
                size="sm"
                loading={cancellingPending}
                onClick={() => void cancelPendingOrder()}
                className="h-9 rounded-lg bg-surface px-4 font-medium text-ink hover:bg-ground"
              >
                Hủy đơn
              </Button>
            </div>
          </div>
          {pendingError && <p className="mt-2.5 text-sm text-risk">{pendingError}</p>}
        </div>
      )}
      {ageBlocked && (
        <Alert variant="warning">
          <AlertDescription>{AGE_REASON}.</AlertDescription>
        </Alert>
      )}

      <div className="grid gap-5 md:grid-cols-2 lg:grid-cols-3 lg:grid-rows-[auto_auto_auto_1fr] lg:gap-y-0">
          {grouped.map(({ kind, items }) => {
            const explicitCode = selectedByKind[kind];
            const activeCode = activeCodeForKind(kind);
            const plan =
              items.find((p) => p.code === explicitCode) ??
              items.find((p) => p.code === activeCode) ??
              items[0];

            const hasDuplicateDuration = items.some(
              (p, idx) => items.findIndex((q) => q.durationDays === p.durationDays) !== idx,
            );
            const statusBadge = renderStatusBadge(plan);
            const cta = renderCta(plan);
            const periodUnit =
              plan.kind === "free"
                ? plan.quotaPeriodDays == null
                  ? "tháng"
                  : `${plan.quotaPeriodDays} ngày`
                : `${plan.durationDays} ngày`;
            const features = buildFeatures(plan);

            return (
              <section
                key={kind}
                className="flex flex-col rounded-2xl border border-line bg-surface p-6 lg:row-span-4 lg:grid lg:grid-rows-subgrid"
              >
                <div className="pb-5">
                  <div className="mb-5 flex items-start justify-between gap-3">
                    <Image
                      src={PLAN_IMAGE[kind]}
                      alt=""
                      width={64}
                      height={64}
                      className="size-16 shrink-0 object-contain"
                    />

                    {items.length > 1 && (
                      <div
                        role="tablist"
                        aria-label={`Chọn thời hạn ${plan.name}`}
                        className="inline-flex flex-wrap items-center gap-0.5 rounded-xl border border-line bg-ground p-1"
                      >
                        {items.map((option) => {
                          const isSelected = option.code === plan.code;
                          const tabLabel =
                            option.durationDays != null
                              ? hasDuplicateDuration
                                ? `${option.durationDays} ngày · ${option.name}`
                                : `${option.durationDays} ngày`
                              : option.name;
                          return (
                            <button
                              key={option.code}
                              type="button"
                              role="tab"
                              aria-selected={isSelected}
                              aria-label={option.durationDays != null ? `${option.durationDays} ngày` : option.name}
                              onClick={() => setSelectedByKind((prev) => ({ ...prev, [kind]: option.code }))}
                              className={cn(
                                "cursor-pointer rounded-lg px-2.5 py-1 text-xs transition-colors",
                                isSelected
                                  ? "bg-surface font-semibold text-brand shadow-xs ring-1 ring-brand/15"
                                  : "text-ink-2 hover:text-ink",
                              )}
                            >
                              {tabLabel}
                            </button>
                          );
                        })}
                      </div>
                    )}
                  </div>

                  <div className="flex flex-wrap items-center gap-2">
                    <h2 className="font-heading text-2xl font-semibold tracking-tight text-ink">{plan.name}</h2>
                    {statusBadge}
                  </div>
                  {plan.description && <p className="mt-1.5 text-sm leading-relaxed text-ink-2">{plan.description}</p>}
                </div>

                <div className="pb-5">
                  <div className="flex items-baseline gap-1.5">
                    <span className="text-3xl font-bold tracking-tight text-ink tabular-nums sm:text-4xl">
                      {plan.priceVnd === 0 ? "0 đ" : formatVnd(plan.priceVnd)}
                    </span>
                    <span className="text-sm font-normal text-ink-2">/ {periodUnit}</span>
                  </div>
                </div>

                <div className="space-y-2">
                  {cta}
                  {messages[plan.code] && <p className="text-sm text-risk">{messages[plan.code]}</p>}
                  <hr className="my-6 border-t border-line" />
                </div>

                <ul className="space-y-3 text-sm leading-relaxed text-ink-2">
                  {features.map((item, idx) => (
                    <li key={idx} className="flex items-start gap-2.5">
                      <CheckIcon className="mt-0.5 size-4 shrink-0 text-risk" aria-hidden />
                      <span>{item}</span>
                    </li>
                  ))}
                </ul>
              </section>
            );
          })}
      </div>
    </div>
  );
}
