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

/** F1: nhập số tiền VNĐ / USD giống form bước 4, quy ra mức rồi gửi chữ của mức (khớp nút, ghi thẳng) */
export function BudgetWidget({ data, onPick }: { data?: Record<string, unknown> | null; onPick?: (text: string) => void }) {
  const tiers = ((data?.options as Option[] | undefined) ?? DEFAULT_BUDGET_TIERS.map((t) => ({ value: t.key, label: t.label })));
  const [currency, setCurrency] = useState<Currency>("vnd");
  const [amountText, setAmountText] = useState("");

  const amount = Number(amountText.replace(/\D/g, "")) || 0;
  const usd = currency === "vnd" ? Math.round(amount / BUDGET_RATE) : amount;
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
        <p className="text-[11px] text-muted-foreground">
          {tier ? (
            <>
              {currency === "vnd" && <>≈ {usd.toLocaleString("vi-VN")} USD, </>}
              thuộc khoảng <strong className="font-semibold text-foreground">{tier.label}</strong>
            </>
          ) : (
            `Nhập số tiền gia đình chi được mỗi năm (1 USD = ${BUDGET_RATE.toLocaleString("vi-VN")} VNĐ), hoặc bấm chọn khoảng.`
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
