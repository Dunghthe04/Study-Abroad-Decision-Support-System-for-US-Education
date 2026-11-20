"use client";

import { useState } from "react";
import { SendIcon } from "lucide-react";
import { cn } from "@/lib/utils";
import { BUDGET_RATE, DEFAULT_BUDGET_TIERS, FALLBACK_MAX_USD } from "@/lib/profile-flow-options";
import type { BudgetTierId } from "@/types/profile-flow";

interface Option {
  value: string;
  label: string;
}

type Currency = "vnd" | "usd";

// Mức chứa số USD theo ngưỡng mặc định của form bước 4; vượt mức cuối thì lấy mức cuối
function tierForUsd(tiers: Option[], usd: number): Option | undefined {
  return tiers.find((t) => {
    const max = FALLBACK_MAX_USD[t.value as BudgetTierId];
    return max != null && usd < max;
  }) ?? tiers[tiers.length - 1];
}

/**
 * UC-94: F1 khi phụ huynh nhập hộ con managed (data.exact), giống bước 4 của form: chỉ nhập số tiền tối đa/năm
 * chính xác bằng VNĐ, gửi "<số> VNĐ" để advisor ghi số tiền (không quy ra mức như ô thường)
 */
function ExactBudgetInput({ data, onPick }: { data: Record<string, unknown>; onPick?: (text: string) => void }) {
  // Đang sửa hồ sơ: điền sẵn số tiền cũ (data.initial.amountVnd)
  const initialVnd = Number((data.initial as { amountVnd?: number } | undefined)?.amountVnd) || 0;
  const [amountText, setAmountText] = useState(initialVnd > 0 ? String(initialVnd) : "");
  const rate = Number(data.rate) || BUDGET_RATE;
  const min = Number(data.minVnd) || 100_000_000;
  const max = Number(data.maxVnd) || 10_000_000_000;

  const amount = Number(amountText.replace(/\D/g, "")) || 0;
  const valid = amount >= min && amount <= max;

  const submit = () => {
    if (valid && onPick) onPick(`${amount.toLocaleString("vi-VN")} VNĐ`);
  };

  return (
    <div className="mt-2.5 space-y-2 rounded-xl border border-line bg-surface p-2.5">
      <div className="flex items-center gap-2">
        <input
          inputMode="numeric"
          aria-label="Số tiền tối đa mỗi năm (VNĐ)"
          placeholder="VD: 1.300.000.000"
          disabled={!onPick}
          value={amount > 0 ? amount.toLocaleString("vi-VN") : ""}
          onChange={(e) => setAmountText(e.target.value)}
          onKeyDown={(e) => e.key === "Enter" && submit()}
          className="w-full min-w-0 rounded-lg border border-line bg-surface px-2.5 py-1.5 text-[13px] text-foreground outline-none focus:border-brand disabled:cursor-not-allowed disabled:opacity-60"
        />
        <span className="shrink-0 text-xs font-medium text-muted-foreground">VNĐ</span>
      </div>
      <div className="flex items-center justify-between gap-2">
        <p className={cn("text-xs", amount > 0 && !valid ? "text-destructive" : "text-muted-foreground")}>
          {amount > 0 && !valid ? (
            `Số tiền phải từ ${min.toLocaleString("vi-VN")} đến ${max.toLocaleString("vi-VN")} VNĐ.`
          ) : valid ? (
            <>
              ≈ <strong className="font-semibold text-foreground">{Math.round(amount / rate).toLocaleString("vi-VN")} USD</strong>{" "}
              mỗi năm (1 USD = {rate.toLocaleString("vi-VN")} VNĐ), chốt theo tỷ giá lúc lưu hồ sơ.
            </>
          ) : (
            "Nhập số tiền tối đa gia đình chi trả được mỗi năm, từ 100 triệu đến 10 tỷ đồng."
          )}
        </p>
        <button
          type="button"
          disabled={!onPick || !valid}
          onClick={submit}
          className="inline-flex h-7 shrink-0 cursor-pointer items-center gap-1 rounded-full bg-brand px-3 text-xs font-semibold text-white transition-colors hover:bg-brand-ink disabled:cursor-not-allowed disabled:opacity-50"
        >
          <SendIcon className="size-3.5" />
          Gửi
        </button>
      </div>
    </div>
  );
}

/** F1: nhập số tiền VNĐ / USD giống form bước 4, quy ra mức rồi gửi chữ của mức (khớp nút, ghi thẳng) */
export function BudgetWidget({ data, onPick }: { data?: Record<string, unknown> | null; onPick?: (text: string) => void }) {
  if (data?.exact) return <ExactBudgetInput data={data} onPick={onPick} />;
  return <TierBudgetInput data={data} onPick={onPick} />;
}

function TierBudgetInput({ data, onPick }: { data?: Record<string, unknown> | null; onPick?: (text: string) => void }) {
  const tiers = ((data?.options as Option[] | undefined) ?? DEFAULT_BUDGET_TIERS.map((t) => ({ value: t.key, label: t.label })));
  const [currency, setCurrency] = useState<Currency>("vnd");
  const [amountText, setAmountText] = useState("");
  // Tỷ giá advisor gửi kèm (lấy mới mỗi ngày), không có thì dùng tỷ giá mặc định
  const rate = Number(data?.rate) || BUDGET_RATE;

  const amount = Number(amountText.replace(/\D/g, "")) || 0;
  const usd = currency === "vnd" ? Math.round(amount / rate) : amount;
  const tier = amount > 0 ? tierForUsd(tiers, usd) : undefined;

  const submit = () => {
    if (tier && onPick) onPick(tier.label);
  };

  return (
    <div className="mt-2.5 space-y-2 rounded-xl border border-line bg-surface p-2.5">
      <div className="flex items-center gap-2">
        <input
          inputMode="numeric"
          aria-label="Số tiền mỗi năm"
          placeholder={currency === "vnd" ? "VD: 900.000.000" : "VD: 36.000"}
          disabled={!onPick}
          value={amount > 0 ? amount.toLocaleString("vi-VN") : ""}
          onChange={(e) => setAmountText(e.target.value)}
          onKeyDown={(e) => e.key === "Enter" && submit()}
          className="w-full min-w-0 rounded-lg border border-line bg-surface px-2.5 py-1.5 text-[13px] text-foreground outline-none focus:border-brand disabled:cursor-not-allowed disabled:opacity-60"
        />
        <div className="flex shrink-0 rounded-lg border border-line p-0.5 text-xs">
          {(["vnd", "usd"] as const).map((c) => (
            <button
              key={c}
              type="button"
              disabled={!onPick}
              onClick={() => setCurrency(c)}
              className={cn(
                "cursor-pointer rounded-md px-2.5 py-1 transition-colors disabled:cursor-not-allowed",
                currency === c ? "bg-foreground text-background" : "text-muted-foreground hover:text-foreground"
              )}
            >
              {c === "vnd" ? "VNĐ" : "USD"}
            </button>
          ))}
        </div>
      </div>
      <div className="flex items-center justify-between gap-2">
        <p className="text-xs text-muted-foreground">
          {tier ? (
            <>
              {currency === "vnd" && <>≈ {usd.toLocaleString("vi-VN")} USD, </>}
              thuộc khoảng <strong className="font-semibold text-foreground">{tier.label}</strong>
            </>
          ) : (
            `Nhập số tiền gia đình chi được mỗi năm (1 USD = ${rate.toLocaleString("vi-VN")} VNĐ), hoặc bấm chọn khoảng.`
          )}
        </p>
        <button
          type="button"
          disabled={!onPick || !tier}
          onClick={submit}
          className="inline-flex h-7 shrink-0 cursor-pointer items-center gap-1 rounded-full bg-brand px-3 text-xs font-semibold text-white transition-colors hover:bg-brand-ink disabled:cursor-not-allowed disabled:opacity-50"
        >
          <SendIcon className="size-3.5" />
          Gửi
        </button>
      </div>
    </div>
  );
}
