"use client";

import { useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { CheckIcon, ReceiptTextIcon, UsersIcon } from "lucide-react";
import { useAuth } from "@/contexts/AuthContext";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button, buttonVariants } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle } from "@/components/ui/card";
import { ErrorFallback } from "@/components/ui/error-fallback";
import { Spinner } from "@/components/ui/spinner";
import { billingApi, formatVnd, getBillingError } from "@/lib/billing-api";
import { useFetched } from "@/lib/use-fetched";
import type { PlanDto, SubscriptionSummaryDto } from "@/types/billing";

const AGE_REASON = "Tài khoản dưới 16 tuổi chưa thể mua gói";

function seatsText(plan: PlanDto): string {
  if (!plan.seats) return "1 tài khoản";
  const { students, parents } = plan.seats;
  return `Người mua và tối đa ${students + parents} tài khoản (${students} Học sinh, ${parents} Phụ huynh)`;
}

function durationText(plan: PlanDto): string {
  if (plan.kind === "free") return "Làm mới đầu mỗi tháng";
  return plan.durationDays === 30 ? "1 tháng (30 ngày)" : plan.durationDays === 90 ? "3 tháng (90 ngày)" : `${plan.durationDays} ngày`;
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

  // Mua hay gia hạn: nút theo làn (Cá nhân / Gia đình) mà người dùng đã có kỳ hay chưa
  const action = (plan: PlanDto) => {
    if (plan.kind === "free") return null;
    if (authLoading) return null;
    if (!user) {
      return (
        <Link href="/login?redirect=/plans" className={buttonVariants({ variant: "outline", className: "w-full" })}>
          Đăng nhập để mua
        </Link>
      );
    }
    if (!canBuy) return null;
    // Thành viên gói Gia đình của người khác không gia hạn hộ (bảng "Who sees the Family section")
    if (plan.kind === "family" && familyMember) return null;
    const renew = plan.kind === "personal" ? !!s?.personal : !!s?.family;
    return (
      <Button
        className="w-full"
        disabled={ageBlocked || busy !== null}
        title={ageBlocked ? AGE_REASON : undefined}
        onClick={() => void buy(plan.code)}
      >
        {busy === plan.code && <Spinner />}
        {renew ? "Gia hạn" : "Mua gói"}
      </Button>
    );
  };

  const badge = (plan: PlanDto) => {
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

  return (
    <div className="mx-auto w-full max-w-6xl space-y-6 px-4">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div className="space-y-1">
          <h1 className="text-h1">Gói dịch vụ</h1>
          <p className="text-sm text-muted-foreground">
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

      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        {plans.data.map((plan) => {
          const footer = action(plan);
          return (
          <Card key={plan.code} className="flex flex-col">
            <CardHeader className="space-y-2">
              <div className="flex flex-wrap items-center gap-2">
                <CardTitle>{plan.name}</CardTitle>
                {badge(plan)}
              </div>
              {plan.description && <CardDescription>{plan.description}</CardDescription>}
            </CardHeader>
            <CardContent className="flex-1 space-y-3">
              <p className="text-2xl font-bold">{plan.priceVnd === 0 ? "Miễn phí" : formatVnd(plan.priceVnd)}</p>
              <ul className="space-y-1.5 text-sm text-ink-2">
                <li className="flex gap-2">
                  <CheckIcon className="mt-0.5 size-4 shrink-0 text-ok" aria-hidden />
                  {durationText(plan)}
                </li>
                <li className="flex gap-2">
                  <CheckIcon className="mt-0.5 size-4 shrink-0 text-ok" aria-hidden />
                  Khoảng {plan.estimatedQuestions} câu hỏi
                </li>
                <li className="flex gap-2">
                  <UsersIcon className="mt-0.5 size-4 shrink-0 text-ok" aria-hidden />
                  {seatsText(plan)}
                </li>
              </ul>
              {messages[plan.code] && <p className="text-sm text-risk">{messages[plan.code]}</p>}
            </CardContent>
            {footer && <CardFooter>{footer}</CardFooter>}
          </Card>
          );
        })}
      </div>
    </div>
  );
}
