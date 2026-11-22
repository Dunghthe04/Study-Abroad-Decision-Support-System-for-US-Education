"use client";

import { useId, useState } from "react";
import { ArrowRightIcon } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { RequiredMark } from "./RequiredMark";
import { CHOICE_CARD, StepHeader, StepNav } from "./StepChrome";
import { cn } from "@/lib/utils";
import { useAuth } from "@/contexts/AuthContext";
import { MAX_BUDGET_VND, MIN_BUDGET_VND } from "@/lib/parent-preference-api";
import {
  BUDGET_RATE,
  DEFAULT_BUDGET_TIERS,
  FALLBACK_MAX_USD,
  FUNDING_SOURCE_OPTIONS,
  SCHOLARSHIP_NEED_OPTIONS,
} from "@/lib/profile-flow-options";
import type { BudgetTierId, FinancialDraft, FundingSource, ProfileMetadataResponse, ScholarshipNeed } from "@/types/profile-flow";

interface Step4FinancialProps {
  data: FinancialDraft;
  metadata: ProfileMetadataResponse | null;
  /** UC-94: số tiền tối đa/năm phụ huynh đã nhập (USD). Có thì ngân sách cố định, học sinh không sửa */
  lockedUsd?: number | null;
  onUpdate: (updated: Partial<FinancialDraft>) => void;
  onPrev: () => void;
  onNext: () => void;
}

type Currency = "vnd" | "usd";

export function Step4Financial({ data, metadata, lockedUsd = null, onUpdate, onPrev, onNext }: Step4FinancialProps) {
  // Lỗi của từng câu, hiện ngay dưới câu đó
  const [budgetError, setBudgetError] = useState<string | null>(null);
  const [aidError, setAidError] = useState<string | null>(null);
  const budgetSectionId = useId();
  const aidSectionId = useId();
  const [currency, setCurrency] = useState<Currency>("vnd");
  const [amountText, setAmountText] = useState("");
  // UC-94: phụ huynh nhập hộ con managed → câu chính là số tiền tối đa/năm chính xác (VND).
  // "Chưa biết chính xác" → chọn hạng (ước tính). Mở lại hồ sơ đã chọn theo hạng thì vào thẳng chế độ hạng.
  const { acting } = useAuth();
  const exactAsk = Boolean(acting);
  const [unsure, setUnsure] = useState(
    () => Boolean(data.annualBudgetTier) && data.annualBudgetVnd == null && data.annualBudgetUsd == null);
  const [vndText, setVndText] = useState(
    data.annualBudgetVnd ? data.annualBudgetVnd.toLocaleString("vi-VN") : "");
  // Tỷ giá mới nhất từ BE, chưa có thì dùng tỷ giá mặc định
  const RATE = Math.round(metadata?.vndPerUsd || BUDGET_RATE);

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
      setBudgetError(null);
      onUpdate({ annualBudgetTier: tier });
    }
  };

  const selectedTierLabel = budgetTiers.find((t) => t.key === data.annualBudgetTier)?.label;

  const vndOutOfRange = (v: number) => v < MIN_BUDGET_VND || v > MAX_BUDGET_VND;
  const rangeMessage = `Số tiền phải từ ${MIN_BUDGET_VND.toLocaleString("vi-VN")} đến ${MAX_BUDGET_VND.toLocaleString("vi-VN")} VNĐ.`;

  const applyExactVnd = (text: string) => {
    const raw = text.replace(/\D/g, "");
    const value = raw ? Number(raw) : null;
    setVndText(value ? value.toLocaleString("vi-VN") : "");
    setBudgetError(null);
    // Hạng tự suy từ số tiền (BE suy lại theo tỷ giá lúc lưu); số ngoài khoảng báo lỗi lúc bấm Tiếp tục
    onUpdate(value && !vndOutOfRange(value)
      ? { annualBudgetVnd: value, annualBudgetTier: tierForUsd(Math.round(value / RATE)) }
      : { annualBudgetVnd: value });
  };

  const switchToTiers = () => {
    setUnsure(true);
    setVndText("");
    setBudgetError(null);
    onUpdate({ annualBudgetVnd: null, annualBudgetUsd: null, annualBudgetTier: null });
  };

  const switchToExact = () => {
    setUnsure(false);
    setBudgetError(null);
    onUpdate({ annualBudgetTier: null });
  };

  const exactVnd = data.annualBudgetVnd ?? null;
  const savedUsd = data.annualBudgetUsd ?? null;

  /** Lỗi câu ngân sách khi phụ huynh nhập hộ (null = hợp lệ) */
  const exactBudgetError = (): string | null => {
    if (unsure) return data.annualBudgetTier ? null : "Vui lòng chọn mức ngân sách mỗi năm.";
    if (exactVnd != null) return vndOutOfRange(exactVnd) ? rangeMessage : null;
    return savedUsd != null ? null : "Vui lòng nhập số tiền tối đa mỗi năm, hoặc bấm \"Chưa biết chính xác\" để chọn mức.";
  };

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
    const budgetMsg = lockedUsd != null
      ? null
      : exactAsk
      ? exactBudgetError()
      : data.annualBudgetTier ? null : "Vui lòng chọn mức ngân sách mỗi năm.";
    const missingBudget = budgetMsg != null;
    const missingAid = !data.needFinancialAid;
    setBudgetError(budgetMsg);
    setAidError(missingAid ? "Vui lòng chọn nhu cầu học bổng của bạn." : null);
    if (missingBudget || missingAid) {
      // Cuộn tới câu sai đầu tiên
      document
        .getElementById(missingBudget ? budgetSectionId : aidSectionId)
        ?.scrollIntoView({ behavior: "smooth", block: "center" });
      return;
    }
    onNext();
  };

  return (
    <div className="flow-stagger space-y-10">
      <StepHeader
        title={
          exactAsk
            ? "Gia đình chi trả được tối đa bao nhiêu cho một năm học?"
            : "Gia đình bạn dự định chi bao nhiêu cho một năm học?"
        }
        lede={
          exactAsk
            ? "Nhập số tiền tối đa bằng VNĐ. Hệ thống chỉ giữ những trường nằm trong mức này."
            : "Chọn mức gần nhất hoặc nhập số cụ thể. Trường vượt ngân sách sẽ được loại."
        }
      />

      {lockedUsd != null ? (
      // UC-94: phụ huynh đã nhập số tiền chính xác → cố định theo phụ huynh, chỉ xem
      <section id={budgetSectionId} className="space-y-3">
        <h2 className="text-h3 font-sans text-foreground">Số tiền tối đa chi trả được mỗi năm</h2>
        <div className="rounded-xl border border-line bg-ground/50 px-4 py-3.5 text-sm">
          <p>
            <strong className="font-semibold text-foreground">{lockedUsd.toLocaleString("vi-VN")} USD/năm</strong>{" "}
            <span className="text-muted-foreground">(≈ {(lockedUsd * RATE).toLocaleString("vi-VN")} VNĐ theo tỷ giá hôm nay)</span>
          </p>
          <p className="mt-1 text-xs text-muted-foreground">
            Phụ huynh đã nhập số này nên bạn không sửa được. Cần đổi, hãy nhờ phụ huynh.
          </p>
        </div>
      </section>
      ) : exactAsk && !unsure ? (
      <section id={budgetSectionId} className="space-y-3">
        <div>
          <Label htmlFor="exactBudgetVnd" className="text-h3 text-foreground">
            Số tiền tối đa chi trả được mỗi năm (VNĐ) <RequiredMark />
          </Label>
          <p className="text-xs text-muted-foreground mt-0.5">
            Gồm học phí và chi phí ăn ở mỗi năm (chưa gồm vé máy bay). Từ 100 triệu đến 10 tỷ đồng.
          </p>
        </div>
        <Input
          id="exactBudgetVnd"
          inputMode="numeric"
          placeholder="VD: 1.300.000.000"
          value={vndText}
          onChange={(e) => applyExactVnd(e.target.value)}
          aria-invalid={budgetError ? true : undefined}
          className="h-9 max-w-xs bg-surface"
        />
        <p className="text-xs text-muted-foreground">
          {exactVnd != null && !vndOutOfRange(exactVnd) ? (
            <>
              ≈ <strong className="font-semibold text-foreground">{Math.round(exactVnd / RATE).toLocaleString("vi-VN")} USD</strong>{" "}
              mỗi năm theo tỷ giá hôm nay (1 USD = {RATE.toLocaleString("vi-VN")} VNĐ). Số USD chốt theo tỷ giá lúc lưu hồ sơ.
            </>
          ) : exactVnd == null && savedUsd != null ? (
            <>
              Đã lưu: <strong className="font-semibold text-foreground">{savedUsd.toLocaleString("vi-VN")} USD/năm</strong>{" "}
              (≈ {(savedUsd * RATE).toLocaleString("vi-VN")} VNĐ theo tỷ giá hôm nay). Để trống thì giữ số này.
            </>
          ) : null}
        </p>
        {budgetError && <p className="text-xs font-medium text-destructive">{budgetError}</p>}
        <Button type="button" variant="outline" size="sm" onClick={switchToTiers} className="cursor-pointer">
          Chưa biết chính xác
        </Button>
      </section>
      ) : (
      // Question 1: Annual Budget Tiers
      <fieldset id={budgetSectionId} role="radiogroup" className="min-w-0 space-y-4">
        <legend className="text-h3 mb-1 text-foreground">
          Ngân sách mỗi năm <RequiredMark />
        </legend>
        <p className="text-xs text-muted-foreground">
          Gồm học phí và chi phí ăn ở mỗi năm (chưa gồm vé máy bay).
        </p>

        <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
          {budgetTiers.map((tier) => (
            <button
              key={tier.key}
              type="button"
              role="radio"
              aria-checked={data.annualBudgetTier === tier.key}
              onClick={() => {
                setBudgetError(null);
                setAmountText("");
                onUpdate({ annualBudgetTier: tier.key });
              }}
              className={cn(CHOICE_CARD, "flex h-full flex-col items-start")}
            >
              <span className="text-sm font-semibold text-foreground">{tier.label}</span>
              <span className="mt-1 line-clamp-2 text-xs text-ink-2">{tier.description}</span>
            </button>
          ))}
        </div>
        {budgetError && <p className="text-xs font-medium text-destructive">{budgetError}</p>}

        {exactAsk ? (
          // Phụ huynh chưa biết số chính xác: hạng chỉ là ước tính
          <div className="space-y-2 rounded-xl bg-ground/50 px-4 py-3.5 text-xs text-muted-foreground">
            <p>
              Chọn theo mức là ước tính: hệ thống lấy trần của mức để lọc trường. Khi biết số chính xác, hãy cập nhật để kết quả sát hơn.
            </p>
            <Button type="button" variant="outline" size="sm" onClick={switchToExact} className="cursor-pointer">
              Nhập số tiền chính xác
            </Button>
          </div>
        ) : (
        /* Nhập số tiền cụ thể → tự chọn mức tương ứng */
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
                    "cursor-pointer rounded-md px-3 py-1 transition-colors focus-visible:outline-none focus-visible:ring-3 focus-visible:ring-ring/50 disabled:cursor-default",
                    currency === c ? "bg-brand text-brand-foreground" : "text-muted-foreground hover:text-foreground"
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
            className="h-9 max-w-xs bg-surface"
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
              "Hệ thống sẽ tự chọn mức gần nhất."
            )}
          </p>
        </div>
        )}
      </fieldset>
      )}

      {/* Question 2: Scholarship Need */}
      <fieldset id={aidSectionId} role="radiogroup" className="min-w-0 space-y-4">
        <legend className="text-h3 mb-4 text-foreground">
          Bạn có cần học bổng không? <RequiredMark />
        </legend>

        <div className="grid gap-3 sm:grid-cols-3">
          {needOptions.map((opt) => (
            <button
              key={opt.key}
              type="button"
              role="radio"
              aria-checked={data.needFinancialAid === opt.key}
              onClick={() => {
                setAidError(null);
                onUpdate({ needFinancialAid: opt.key });
              }}
              className={cn(CHOICE_CARD, "flex h-full flex-col items-start")}
            >
              <span className="text-sm font-semibold text-foreground">{opt.title}</span>
              <span className="mt-1 text-xs text-ink-2">{opt.description}</span>
            </button>
          ))}
        </div>
        {aidError && <p className="text-xs font-medium text-destructive">{aidError}</p>}
      </fieldset>

      {/* Question 3: Funding Source — không bắt buộc, giống câu F3 bên chat */}
      <section className="space-y-3">
        <Label htmlFor="fundingSource" className="text-h3 text-foreground">
          Ai chi trả chính? (không bắt buộc)
        </Label>
        <div className="sm:max-w-sm">
          <Select
            value={data.fundingSource || null}
            onValueChange={(v) => onUpdate({ fundingSource: (v || null) as FundingSource | null })}
            items={FUNDING_SOURCE_OPTIONS}
          >
            <SelectTrigger id="fundingSource" className="h-9 w-full bg-surface">
              <SelectValue placeholder="Chưa chọn" />
            </SelectTrigger>
            <SelectContent className="w-max min-w-(--anchor-width)">
              {FUNDING_SOURCE_OPTIONS.map((opt) => (
                <SelectItem key={opt.value} value={opt.value}>
                  {opt.label}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
      </section>

      <StepNav onPrev={onPrev}>
        <Button onClick={handleNextClick} size="lg" className="gap-1.5">
          Tiếp tục
          <ArrowRightIcon className="size-4" />
        </Button>
      </StepNav>
    </div>
  );
}
