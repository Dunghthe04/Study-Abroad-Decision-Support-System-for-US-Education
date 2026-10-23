"use client";

import { useState } from "react";
import { ArrowLeftIcon, ArrowRightIcon, DollarSignIcon, HelpCircleIcon, AlertTriangleIcon } from "lucide-react";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { cn } from "@/lib/utils";
import { DEFAULT_BUDGET_TIERS, SCHOLARSHIP_NEED_OPTIONS } from "@/lib/profile-flow-options";
import type { BudgetTierId, FinancialDraft, ProfileMetadataResponse, ScholarshipNeed } from "@/types/profile-flow";

interface Step4FinancialProps {
  data: FinancialDraft;
  metadata: ProfileMetadataResponse | null;
  onUpdate: (updated: Partial<FinancialDraft>) => void;
  onPrev: () => void;
  onNext: () => void;
}

// Tỉ giá chỉ dùng để quy đổi trên form; số tiền không được lưu, chỉ lưu mã mức
const RATE = 25000;
// Ngưỡng trên (USD) của từng mức khi metadata không có minUsd/maxUsd
const FALLBACK_MAX_USD: Partial<Record<BudgetTierId, number>> = {
  tier_under_20k: 20000,
  tier_20k_35k: 35000,
  tier_35k_50k: 50000,
  tier_50k_70k: 70000,
};

type Currency = "vnd" | "usd";

export function Step4Financial({ data, metadata, onUpdate, onPrev, onNext }: Step4FinancialProps) {
  const [financialError, setFinancialError] = useState<string | null>(null);
  const [currency, setCurrency] = useState<Currency>("vnd");
  const [amountText, setAmountText] = useState("");

  // Chỉ lưu mã mức; min/max USD do BE lấy từ seed metadata khi commit
  const budgetTiers: { key: BudgetTierId; label: string; description: string }[] =
    metadata?.budgetTiers && metadata.budgetTiers.length > 0
      ? metadata.budgetTiers.map((t) => ({
          key: t.key as BudgetTierId,
          label: t.label,
          description: t.description,
        }))
      : DEFAULT_BUDGET_TIERS;

  const amount = Number(amountText.replace(/\D/g, "")) || 0;
  const amountUsd = currency === "vnd" ? Math.round(amount / RATE) : amount;

  // Tìm mức chứa số USD: ưu tiên min/max trong metadata, không có thì dùng ngưỡng mặc định
  const tierForUsd = (usd: number): BudgetTierId | null => {
    const metaTiers = metadata?.budgetTiers ?? [];
    if (metaTiers.some((t) => t.minUsd != null || t.maxUsd != null)) {
      const hit = metaTiers.find((t) => (t.minUsd ?? 0) <= usd && (t.maxUsd == null || usd < t.maxUsd));
      if (hit) return hit.key as BudgetTierId;
    }
    const keys = budgetTiers.map((t) => t.key);
    return keys.find((k) => FALLBACK_MAX_USD[k] != null && usd < FALLBACK_MAX_USD[k]!) ?? keys[keys.length - 1] ?? null;
  };

  const applyAmount = (text: string, cur: Currency) => {
    setAmountText(text);
    const value = Number(text.replace(/\D/g, "")) || 0;
    if (value <= 0) return;
    const usd = cur === "vnd" ? Math.round(value / RATE) : value;
    const tier = tierForUsd(usd);
    if (tier) {
      setFinancialError(null);
      onUpdate({ annualBudgetTier: tier });
    }
  };

  const selectedTierLabel = budgetTiers.find((t) => t.key === data.annualBudgetTier)?.label;

  const needOptions: { key: ScholarshipNeed; title: string; description: string }[] =
    SCHOLARSHIP_NEED_OPTIONS.map((o) => {
      const fromMeta = metadata?.financialNeedLevels.find((m) => m.key === o.value);
      return {
        key: o.value,
        title: fromMeta?.label || o.label,
        description: fromMeta?.description || o.description,
      };
    });

  const handleNextClick = () => {
    if (!data.annualBudgetTier) {
      setFinancialError("Vui lòng chọn mức ngân sách mỗi năm trước khi tiếp tục.");
      return;
    }
    if (!data.needFinancialAid) {
      setFinancialError("Vui lòng chọn nhu cầu học bổng của bạn trước khi tiếp tục.");
      return;
    }
    setFinancialError(null);
    onNext();
  };

  return (
    <div className="space-y-8 animate-in fade-in-50 duration-200">
      {/* Step Header */}
      <div className="space-y-2 border-b border-line pb-4">
        <div className="text-xs font-semibold text-brand tracking-wide uppercase">
          Bước 4/6 · khoảng 30 giây
        </div>
        <h1 className="text-h1 text-foreground">
          Gia đình bạn dự định chi bao nhiêu cho một năm học?
        </h1>
        <p className="text-body text-muted-foreground">
          Chọn mức gần đúng nhất hoặc nhập số tiền cụ thể. Chúng tôi dùng nó để loại các trường vượt ngân sách của bạn.
        </p>
      </div>

      {/* Question 1: Annual Budget Tiers */}
      <section className="space-y-4">
        <div>
          <Label className="text-sm font-semibold text-foreground flex items-center gap-1.5">
            <DollarSignIcon className="size-4 text-brand" />
            Mức ngân sách mỗi năm · Chọn 1 mức phù hợp nhất
          </Label>
          <p className="text-xs text-muted-foreground mt-0.5">
            Gồm học phí và chi phí ăn ở mỗi năm (chưa gồm vé máy bay).
          </p>
        </div>

        <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
          {budgetTiers.map((tier) => {
            const isSelected = data.annualBudgetTier === tier.key;
            return (
              <button
                key={tier.key}
                type="button"
                onClick={() => {
                  setFinancialError(null);
                  setAmountText("");
                  onUpdate({ annualBudgetTier: tier.key });
                }}
                className={cn(
                  "flex flex-col items-start justify-between rounded-xl border p-4 text-left transition-all cursor-pointer h-full shadow-2xs",
                  isSelected
                    ? "border-brand bg-brand-wash/40 ring-2 ring-brand text-brand-ink"
                    : "border-line bg-surface hover:border-brand/40 hover:bg-muted/30"
                )}
              >
                <div className="flex items-center justify-between w-full">
                  <div className="font-semibold text-sm text-foreground">{tier.label}</div>
                  <div
                    className={cn(
                      "size-4 rounded-full border flex items-center justify-center shrink-0 transition-all ml-2",
                      isSelected
                        ? "border-brand bg-brand"
                        : "border-muted-foreground/40 bg-surface"
                    )}
                  >
                    {isSelected && <div className="size-1.5 rounded-full bg-white" />}
                  </div>
                </div>
                <div className="text-xs text-muted-foreground mt-2 line-clamp-2">
                  {tier.description}
                </div>
              </button>
            );
          })}
        </div>

        {/* Nhập số tiền cụ thể → tự chọn mức tương ứng */}
        <div className="space-y-2.5 rounded-xl bg-ground/50 px-4 py-3.5">
          <div className="flex flex-wrap items-center justify-between gap-2">
            <Label htmlFor="budgetAmount" className="text-sm font-semibold text-foreground">
              Hoặc nhập số tiền cụ thể
            </Label>
            <div className="inline-flex rounded-lg border border-line bg-surface p-0.5 text-xs font-medium">
              {(["vnd", "usd"] as const).map((c) => (
                <button
                  key={c}
                  type="button"
                  aria-pressed={currency === c}
                  onClick={() => {
                    setCurrency(c);
                    applyAmount(amountText, c);
                  }}
                  className={cn(
                    "cursor-pointer rounded-md px-3 py-1 transition-colors",
                    currency === c ? "bg-foreground text-background" : "text-muted-foreground hover:text-foreground"
                  )}
                >
                  {c === "vnd" ? "VNĐ" : "USD"}
                </button>
              ))}
            </div>
          </div>
          <Input
            id="budgetAmount"
            inputMode="numeric"
            placeholder={currency === "vnd" ? "VD: 900.000.000" : "VD: 36.000"}
            value={amount > 0 ? amount.toLocaleString("vi-VN") : amountText.replace(/\D/g, "")}
            onChange={(e) => applyAmount(e.target.value, currency)}
            className="max-w-xs bg-surface"
          />
          <p className="text-xs text-muted-foreground">
            {amount > 0 ? (
              <>
                {currency === "vnd" ? (
                  <>
                    ≈ <strong className="font-semibold text-foreground">{amountUsd.toLocaleString("vi-VN")} USD</strong>{" "}
                    mỗi năm (tỉ giá 1 USD = {RATE.toLocaleString("vi-VN")} VNĐ)
                  </>
                ) : (
                  <>{amountUsd.toLocaleString("vi-VN")} USD mỗi năm</>
                )}
                {selectedTierLabel && <> · đã tự chọn mức {selectedTierLabel}</>}
              </>
            ) : (
              "Nhập tổng số tiền gia đình có thể chi mỗi năm, hệ thống tự chọn mức gần nhất."
            )}
          </p>
        </div>
      </section>

      {/* Question 2: Scholarship Need */}
      <section className="space-y-4 pt-2">
        <div>
          <Label className="text-sm font-semibold text-foreground flex items-center gap-1.5">
            <HelpCircleIcon className="size-4 text-brand" />
            Bạn có cần học bổng để theo học được không? · Chọn 1 lựa chọn
          </Label>
        </div>

        <div className="grid gap-3 sm:grid-cols-3">
          {needOptions.map((opt) => {
            const isSelected = data.needFinancialAid === opt.key;
            return (
              <button
                key={opt.key}
                type="button"
                onClick={() => {
                  setFinancialError(null);
                  onUpdate({ needFinancialAid: opt.key });
                }}
                className={cn(
                  "flex flex-col items-start justify-between rounded-xl border p-4 text-left transition-all cursor-pointer h-full shadow-2xs",
                  isSelected
                    ? "border-brand bg-brand-wash/40 ring-2 ring-brand text-brand-ink"
                    : "border-line bg-surface hover:border-brand/40 hover:bg-muted/30"
                )}
              >
                <div className="flex items-center justify-between w-full">
                  <div className="font-semibold text-sm text-foreground">{opt.title}</div>
                  <div
                    className={cn(
                      "size-4 rounded-full border flex items-center justify-center shrink-0 transition-all ml-2",
                      isSelected
                        ? "border-brand bg-brand"
                        : "border-muted-foreground/40 bg-surface"
                    )}
                  >
                    {isSelected && <div className="size-1.5 rounded-full bg-white" />}
                  </div>
                </div>
                <div className="text-xs text-muted-foreground mt-2">{opt.description}</div>
              </button>
            );
          })}
        </div>
      </section>

      {/* Financial Validation Error Notice */}
      {financialError && (
        <Alert className="border-destructive/60 bg-destructive/10 animate-in fade-in-50">
          <AlertTriangleIcon className="size-4 text-destructive" />
          <AlertTitle className="text-sm font-semibold text-destructive">Thông tin tài chính chưa hoàn tất</AlertTitle>
          <AlertDescription className="text-xs text-destructive font-medium leading-relaxed">
            {financialError}
          </AlertDescription>
        </Alert>
      )}

      {/* Bottom Actions */}
      <div className="flex justify-between pt-4 border-t border-line">
        <Button variant="outline" onClick={onPrev} size="lg" className="gap-1.5 cursor-pointer">
          <ArrowLeftIcon className="size-4" />
          Quay lại
        </Button>
        <Button onClick={handleNextClick} size="lg" className="gap-1.5 cursor-pointer">
          Tiếp tục
          <ArrowRightIcon className="size-4" />
        </Button>
      </div>
    </div>
  );
}
