"use client";

import { ArrowLeftIcon, ArrowRightIcon, DollarSignIcon, HelpCircleIcon } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Label } from "@/components/ui/label";
import { cn } from "@/lib/utils";
import type { BudgetTierOption, FinancialDraft, ProfileMetadataResponse } from "@/types/profile-flow";

interface Step4FinancialProps {
  data: FinancialDraft;
  metadata: ProfileMetadataResponse | null;
  onUpdate: (updated: Partial<FinancialDraft>) => void;
  onPrev: () => void;
  onNext: () => void;
}

const DEFAULT_BUDGET_TIERS: BudgetTierOption[] = [
  { key: "under_20k", label: "Dưới 20.000 USD", minUsd: 0, maxUsd: 20000, currency: "USD", description: "Học phí & sinh hoạt phí dưới $20.000/năm" },
  { key: "20k_35k", label: "20.000 – 35.000 USD", minUsd: 20000, maxUsd: 35000, currency: "USD", description: "Học phí & sinh hoạt phí $20.000 – $35.000/năm" },
  { key: "35k_50k", label: "35.000 – 50.000 USD", minUsd: 35000, maxUsd: 50000, currency: "USD", description: "Học phí & sinh hoạt phí $35.000 – $50.000/năm" },
  { key: "50k_70k", label: "50.000 – 70.000 USD", minUsd: 50000, maxUsd: 70000, currency: "USD", description: "Học phí & sinh hoạt phí $50.000 – $70.000/năm" },
  { key: "above_70k", label: "Trên 70.000 USD", minUsd: 70000, maxUsd: null, currency: "USD", description: "Học phí & sinh hoạt phí trên $70.000/năm" },
];

const needScholarshipOptions = [
  {
    key: "mandatory",
    title: "Bắt buộc",
    description: "Không có học bổng thì chưa thể đi",
  },
  {
    key: "preferred",
    title: "Nên có",
    description: "Càng nhiều càng tốt",
  },
  {
    key: "not_needed",
    title: "Không cần",
    description: "Gia đình tự chi trả được",
  },
];

export function Step4Financial({ data, metadata, onUpdate, onPrev, onNext }: Step4FinancialProps) {
  const budgetTiers = metadata?.budgetTiers && metadata.budgetTiers.length > 0
    ? metadata.budgetTiers
    : DEFAULT_BUDGET_TIERS;

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
          Chọn mức gần đúng nhất, không cần chính xác. Chúng tôi dùng nó để loại các trường quá tầm tài chính.
        </p>
      </div>

      {/* Question 1: Annual Budget Tiers */}
      <section className="space-y-4">
        <div>
          <Label className="text-sm font-semibold text-foreground flex items-center gap-1.5">
            <DollarSignIcon className="size-4 text-brand" />
            Mức ngân sách mỗi năm
          </Label>
          <p className="text-xs text-muted-foreground mt-0.5">
            Gồm học phí và chi phí ăn ở. Chưa gồm vé máy bay.
          </p>
        </div>

        <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
          {budgetTiers.map((tier) => {
            const isSelected = data.annualBudgetTier === tier.key;
            return (
              <button
                key={tier.key}
                type="button"
                onClick={() =>
                  onUpdate({
                    annualBudgetTier: tier.key,
                    annualBudgetUsd: tier.maxUsd ?? tier.minUsd ?? null,
                  })
                }
                className={cn(
                  "flex flex-col items-start justify-between rounded-xl border p-4 text-left transition-all cursor-pointer h-full",
                  isSelected
                    ? "border-brand bg-brand-wash/40 ring-2 ring-brand text-brand-ink"
                    : "border-line bg-surface hover:border-brand/40 hover:bg-muted/30"
                )}
              >
                <div className="font-semibold text-sm text-foreground">{tier.label}</div>
                <div className="text-xs text-muted-foreground mt-1 line-clamp-2">
                  {tier.description}
                </div>
              </button>
            );
          })}
        </div>
      </section>

      {/* Question 2: Scholarship Need */}
      <section className="space-y-4 pt-2">
        <div>
          <Label className="text-sm font-semibold text-foreground flex items-center gap-1.5">
            <HelpCircleIcon className="size-4 text-brand" />
            Bạn có cần học bổng để theo học được không?
          </Label>
        </div>

        <div className="grid gap-3 sm:grid-cols-3">
          {needScholarshipOptions.map((opt) => {
            const isSelected = data.needFinancialAid === opt.key;
            return (
              <button
                key={opt.key}
                type="button"
                onClick={() => onUpdate({ needFinancialAid: opt.key })}
                className={cn(
                  "flex flex-col items-start justify-between rounded-xl border p-4 text-left transition-all cursor-pointer h-full",
                  isSelected
                    ? "border-brand bg-brand-wash/40 ring-2 ring-brand text-brand-ink"
                    : "border-line bg-surface hover:border-brand/40 hover:bg-muted/30"
                )}
              >
                <div className="font-semibold text-sm text-foreground">{opt.title}</div>
                <div className="text-xs text-muted-foreground mt-1">{opt.description}</div>
              </button>
            );
          })}
        </div>
      </section>

      {/* Bottom Actions */}
      <div className="flex justify-between pt-4 border-t border-line">
        <Button variant="outline" onClick={onPrev} size="lg" className="gap-1.5">
          <ArrowLeftIcon className="size-4" />
          Quay lại
        </Button>
        <Button onClick={onNext} size="lg" className="gap-1.5">
          Tiếp tục
          <ArrowRightIcon className="size-4" />
        </Button>
      </div>
    </div>
  );
}
