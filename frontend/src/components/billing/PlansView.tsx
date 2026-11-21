"use client";

import { useState, type ReactNode } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { BookOpenIcon, GraduationCapIcon, ReceiptTextIcon, UsersIcon } from "lucide-react";
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

const PLAN_VISUAL: Record<PlanKind, { boxClass: string; icon: ReactNode }> = {
  free: {
    boxClass: "bg-ground border border-line text-ink-2",
    icon: <BookOpenIcon className="size-6" aria-hidden />,
  },
  personal: {
    boxClass: "bg-brand-wash border border-brand/15 text-brand",
    icon: <GraduationCapIcon className="size-6" aria-hidden />,
  },
  family: {
    boxClass: "bg-gold-wash border border-gold/20 text-gold",
    icon: <UsersIcon className="size-6" aria-hidden />,
  },
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

  const plans = useFetched("plans", billingApi.plans);
  // Chỉ học sinh/phụ huynh mới có GET subscription; khách gọi sẽ đi vào luồng 401 làm mới phiên
  const sub = useFetched<SubscriptionSummaryDto | null>(canBuy ? `sub:${user?.id}` : "none", () =>
    canBuy ? billingApi.subscription() : Promise.resolve(null),
  );

  const [selectedByKind, setSelectedByKind] = useState<Partial<Record<PlanKind, string>>>({});
  const [busy, setBusy] = useState<string | null>(null);
  const [messages, setMessages] = useState<Record<string, string>>({});
  const [ageBlocked, setAgeBlocked] = useState(false);

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
      if (s?.tier === "free") {
        return (
          <Button type="button" variant="secondary" disabled className={ctaClass}>
            Đang dùng gói Miễn phí
          </Button>
        );
      }
      if (s?.tier === "premium") {
        return (
          <Button type="button" variant="outline" disabled className={ctaClass}>
            Đã bao gồm
          </Button>
        );
      }
      return null;
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
        {renew ? `Gia hạn ${plan.name}` : `Mua ${plan.name}`}
      </Button>
    );
  };

  return (
    <div className="mx-auto w-full max-w-6xl space-y-6 px-4">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div className="space-y-1">
          <h1 className="text-h1">Gói dịch vụ</h1>
          <p className="text-sm text-ink-2">
            USDSS Premium mở khóa tư vấn hồ sơ, gợi ý trường và mô phỏng. Hỏi đáp kiến thức du học luôn miễn phí.
          </p>
        </div>
        {canBuy && (
          <Link href="/orders" className={buttonVariants({ variant: "outline", size: "sm" })}>
            <ReceiptTextIcon />
            Lịch sử giao dịch
          </Link>
        )}
      </div>

      {s?.pendingOrderCode && (
        <Alert variant="info">
          <AlertDescription className="flex flex-wrap items-center justify-between gap-2">
            <span>Bạn có một đơn hàng đang chờ thanh toán.</span>
            <Link href={`/orders/${s.pendingOrderCode}`} className="font-medium underline">
              Xem đơn hàng
            </Link>
          </AlertDescription>
        </Alert>
      )}
      {ageBlocked && (
        <Alert variant="warning">
          <AlertDescription>{AGE_REASON}.</AlertDescription>
        </Alert>
      )}

      <div className="grid gap-5 md:grid-cols-2 lg:grid-cols-3">
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
          const visual = PLAN_VISUAL[kind];
          const statusBadge = renderStatusBadge(plan);
          const cta = renderCta(plan);
          const periodUnit =
            plan.kind === "free"
              ? plan.quotaPeriodDays == null
                ? "tháng"
                : `${plan.quotaPeriodDays} ngày`
              : `${plan.durationDays} ngày`;
          const showRecurringCaption =
            plan.kind !== "free" &&
            plan.quotaPeriodDays != null &&
            plan.durationDays != null &&
            plan.quotaPeriodDays < plan.durationDays;
          const features = buildFeatures(plan);

          return (
            <section
              key={kind}
              className={cn(
                "flex flex-col rounded-2xl border bg-surface p-6",
                kind === "personal" ? "border-brand/35 shadow-sm" : "border-line",
              )}
            >
              <div className="flex items-start justify-between gap-3">
                <div className={cn("flex size-14 shrink-0 items-center justify-center rounded-2xl", visual.boxClass)}>
                  {visual.icon}
                </div>

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
                            ? `${option.durationDays}d · ${option.name}`
                            : `${option.durationDays}d`
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

              <div className="mt-5">
                <div className="flex flex-wrap items-center gap-2">
                  <h2 className="font-heading text-2xl font-semibold tracking-tight text-ink">{plan.name}</h2>
                  {statusBadge}
                  {!statusBadge && kind === "personal" && <Badge variant="brand">Phổ biến</Badge>}
                </div>
                {plan.description && <p className="mt-1.5 text-sm leading-relaxed text-ink-2">{plan.description}</p>}
              </div>

              <div className="mt-5">
                <div className="flex items-baseline gap-1.5">
                  <span className="text-3xl font-bold tracking-tight text-ink tabular-nums sm:text-4xl">
                    {plan.priceVnd === 0 ? "0 đ" : formatVnd(plan.priceVnd)}
                  </span>
                  <span className="text-sm font-normal text-ink-2">/ {periodUnit}</span>
                </div>
                {showRecurringCaption && (
                  <p className="mt-1 text-xs text-ink-2">
                    Làm mới hạn mức mỗi {plan.quotaPeriodDays} ngày trong suốt {plan.durationDays} ngày
                  </p>
                )}
              </div>

              {cta && <div className="mt-5">{cta}</div>}
              {messages[plan.code] && <p className="mt-2 text-sm text-risk">{messages[plan.code]}</p>}

              <hr className="my-6 border-t border-line" />

              <ul className="space-y-3 text-sm leading-relaxed text-ink-2">
                {features.map((item, idx) => (
                  <li key={idx} className="flex items-start gap-2.5">
                    <span
                      className="mt-1.5 size-2 shrink-0 rotate-45 rounded-[2px] border border-brand/50 bg-brand-wash"
                      aria-hidden
                    />
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
