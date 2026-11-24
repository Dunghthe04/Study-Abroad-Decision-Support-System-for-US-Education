"use client";

import { useId, useState } from "react";
import { ArrowRightIcon } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { ChoiceCards, ChoicePicker, Field, FormBody, FormGroup, FormSection, PrefixInput } from "./FormKit";
import { StepHeader, StepNav } from "./StepChrome";
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

const CURRENCY_OPTIONS: { value: Currency; label: string }[] = [
  { value: "vnd", label: "VNĐ" },
  { value: "usd", label: "USD" },
];

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

  const budgetOptions = budgetTiers.map((t) => ({ value: t.key, label: t.label, description: t.description }));
  const aidOptions = needOptions.map((o) => ({ value: o.key, label: o.title, description: o.description }));

  return (
    <>
      <StepHeader
        title={
          exactAsk
            ? "Gia đình chi trả được tối đa bao nhiêu cho một năm học?"
            : "Gia đình bạn dự định chi bao nhiêu cho một năm học?"
        }
      />
      <FormBody>
        <FormGroup
          id={budgetSectionId}
          title="Ngân sách"
          description={
            lockedUsd != null
              ? "Số tiền tối đa gia đình chi trả được mỗi năm."
              : exactAsk && !unsure
                ? "Số tiền tối đa chi trả được mỗi năm, gồm học phí và chi phí ăn ở (chưa gồm vé máy bay). Từ 100 triệu đến 10 tỷ đồng."
                : "Gồm học phí và chi phí ăn ở mỗi năm (chưa gồm vé máy bay)."
          }
          required={lockedUsd == null}
          error={budgetError}
        >
          {lockedUsd != null ? (
            // UC-94: phụ huynh đã nhập số tiền chính xác → cố định theo phụ huynh, chỉ xem
            <div className="space-y-1 py-6 text-sm">
              <p>
                <strong className="font-semibold text-ink">{lockedUsd.toLocaleString("vi-VN")} USD/năm</strong>{" "}
                <span className="text-ink-2">(≈ {(lockedUsd * RATE).toLocaleString("vi-VN")} VNĐ theo tỷ giá hôm nay)</span>
              </p>
              <p className="text-xs text-ink-2">Phụ huynh đã nhập số này nên bạn không sửa được. Cần đổi, hãy nhờ phụ huynh.</p>
            </div>
          ) : exactAsk && !unsure ? (
            <div className="space-y-4 py-6">
              <Field
                label="Số tiền (VNĐ)"
                htmlFor="exactBudgetVnd"
                hint={
                  exactVnd != null && !vndOutOfRange(exactVnd) ? (
                    <>
                      ≈ <strong className="font-semibold text-ink">{Math.round(exactVnd / RATE).toLocaleString("vi-VN")} USD</strong>{" "}
                      mỗi năm theo tỷ giá hôm nay (1 USD = {RATE.toLocaleString("vi-VN")} VNĐ). Số USD chốt theo tỷ giá lúc lưu hồ sơ.
                    </>
                  ) : exactVnd == null && savedUsd != null ? (
                    <>
                      Đã lưu: <strong className="font-semibold text-ink">{savedUsd.toLocaleString("vi-VN")} USD/năm</strong>{" "}
                      (≈ {(savedUsd * RATE).toLocaleString("vi-VN")} VNĐ theo tỷ giá hôm nay). Để trống thì giữ số này.
                    </>
                  ) : undefined
                }
              >
                <Input
                  id="exactBudgetVnd"
                  inputMode="numeric"
                  placeholder="VD: 1.300.000.000"
                  value={vndText}
                  onChange={(e) => applyExactVnd(e.target.value)}
                  aria-invalid={budgetError ? true : undefined}
                  className="h-10 sm:max-w-xs"
                />
              </Field>
              <Button type="button" variant="outline" onClick={switchToTiers}>
                Chưa biết chính xác
              </Button>
            </div>
          ) : (
            <>
              <div className="space-y-4 py-6">
                <ChoiceCards
                  ariaLabel="Ngân sách mỗi năm"
                  columns={3}
                  options={budgetOptions}
                  value={data.annualBudgetTier}
                  onChange={(v) => {
                    setBudgetError(null);
                    setAmountText("");
                    onUpdate({ annualBudgetTier: v });
                  }}
                />
                {exactAsk && (
                  // Phụ huynh chưa biết số chính xác: hạng chỉ là ước tính
                  <div className="space-y-3">
                    <p className="text-sm text-ink-2">
                      Chọn theo mức là ước tính: hệ thống lấy trần của mức để lọc trường. Khi biết số chính xác, hãy cập nhật để kết quả sát hơn.
                    </p>
                    <Button type="button" variant="outline" onClick={switchToExact}>
                      Nhập số tiền chính xác
                    </Button>
                  </div>
                )}
              </div>
              {!exactAsk && (
                /* Nhập số tiền cụ thể → tự chọn mức tương ứng */
                <FormSection title="Hoặc nhập số tiền cụ thể">
                  <PrefixInput
                    id="budgetAmount"
                    side="end"
                    inputMode="numeric"
                    ariaLabel="Số tiền cụ thể"
                    prefixLabel="đơn vị tiền"
                    prefixes={CURRENCY_OPTIONS}
                    prefix={currency}
                    onPrefixChange={(c) => {
                      setCurrency(c as Currency);
                      applyAmount(amountText, c as Currency);
                    }}
                    placeholder={currency === "vnd" ? "VD: 900.000.000" : "VD: 36.000"}
                    value={amount > 0 ? amount.toLocaleString("vi-VN") : amountText.replace(/\D/g, "")}
                    onChange={(t) => applyAmount(t, currency)}
                  />
                  {amount > 0 && (
                    // Chỉ hiện số đã quy đổi; tỉ giá để trong chú thích khi rê chuột
                    <p
                      className="text-right text-sm font-semibold tabular-nums text-ink"
                      title={`Tỉ giá 1 USD = ${RATE.toLocaleString("vi-VN")} VNĐ`}
                    >
                      ≈{" "}
                      {currency === "vnd"
                        ? `${amountUsd.toLocaleString("vi-VN")} USD`
                        : `${(amount * RATE).toLocaleString("vi-VN")} VNĐ`}
                    </p>
                  )}
                </FormSection>
              )}
            </>
          )}
        </FormGroup>

        <FormGroup title="Học bổng và chi trả">
        <FormSection id={aidSectionId} title="Bạn có cần học bổng không?" required error={aidError}>
          <ChoiceCards
            ariaLabel="Nhu cầu học bổng"
            columns={1}
            options={aidOptions}
            value={data.needFinancialAid}
            onChange={(v) => {
              setAidError(null);
              onUpdate({ needFinancialAid: v });
            }}
          />
        </FormSection>

        <FormSection title="Ai chi trả chính?">
          <ChoicePicker
            ariaLabel="Nguồn chi trả chính"
            options={FUNDING_SOURCE_OPTIONS}
            value={data.fundingSource}
            onChange={(v) => onUpdate({ fundingSource: (v || null) as FundingSource | null })}
          />
        </FormSection>
        </FormGroup>
      </FormBody>

      <StepNav onPrev={onPrev}>
        <Button onClick={handleNextClick} size="lg" className="gap-1.5">
          Tiếp tục
          <ArrowRightIcon className="size-4" />
        </Button>
      </StepNav>
    </>
  );
}
