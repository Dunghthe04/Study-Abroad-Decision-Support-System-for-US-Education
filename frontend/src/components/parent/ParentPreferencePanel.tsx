"use client";

// UC-76 (2d): phụ huynh kéo 3 thanh ưu tiên + nhập ngân sách VND, xem thử ngay, bấm Lưu để con xem được.

import { useEffect, useState } from "react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Slider } from "@/components/ui/slider";
import { Spinner } from "@/components/ui/spinner";
import { PremiumLockedCard } from "@/components/billing/PremiumLockedCard";
import { ApiError } from "@/lib/api";
import { isPremiumRequired } from "@/lib/billing-api";
import {
  DEFAULT_PREF,
  MAX_BUDGET_VND,
  MIN_BUDGET_VND,
  parentPreferenceApi,
  type PrefTriple,
} from "@/lib/parent-preference-api";
import { useFetched } from "@/lib/use-fetched";
import { PREF_LEVELS, PREF_NAMES, PreferenceComparison } from "@/components/parent/PreferenceComparison";

const VND = new Intl.NumberFormat("vi-VN");

type Draft = { pref: PrefTriple; budgetText: string };
type Query = { key: string; preview?: { pref: PrefTriple; budgetVnd: number | null } };

const digits = (text: string) => text.replace(/\D/g, "");
const formatBudget = (text: string) => (digits(text) ? VND.format(Number(digits(text))) : "");

export function ParentPreferencePanel({ studentId }: { studentId: string }) {
  const [version, setVersion] = useState(0);
  const saved = useFetched(`pp-saved-${studentId}-${version}`, () => parentPreferenceApi.get(studentId));

  // null: đang theo ý đã lưu (không setState trong effect để đồng bộ)
  const [draft, setDraft] = useState<Draft | null>(null);
  const savedPref = (saved.data?.pref as PrefTriple | undefined) ?? DEFAULT_PREF;
  const savedBudgetText = saved.data?.budgetVnd != null ? VND.format(saved.data.budgetVnd) : "";
  const pref = draft?.pref ?? savedPref;
  const budgetText = draft?.budgetText ?? savedBudgetText;
  const budget = digits(budgetText) ? Number(digits(budgetText)) : null;
  const budgetError =
    budget != null && (budget < MIN_BUDGET_VND || budget > MAX_BUDGET_VND)
      ? `Ngân sách phải từ ${VND.format(MIN_BUDGET_VND)} đến ${VND.format(MAX_BUDGET_VND)} VND.`
      : null;

  // Đổi thanh/số tiền thì đợi 300 ms mới gọi xem thử
  const queryKey = draft && !budgetError ? `${pref.join(",")}|${budget ?? ""}` : "saved";
  const [query, setQuery] = useState<Query>({ key: "saved" });
  useEffect(() => {
    const t = setTimeout(
      () => setQuery(queryKey === "saved" ? { key: "saved" } : { key: queryKey, preview: { pref, budgetVnd: budget } }),
      300,
    );
    return () => clearTimeout(t);
    // pref/budget đã nằm trong queryKey
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [queryKey]);

  const compare = useFetched(`pp-cmp-${studentId}-${version}-${query.key}`, () =>
    parentPreferenceApi.compare(studentId, query.preview),
  );

  const [saving, setSaving] = useState(false);
  const [saveMessage, setSaveMessage] = useState<string | null>(null);
  const [saveLocked, setSaveLocked] = useState(false);

  const dirty =
    saved.data == null ||
    saved.data.hasPref === false ||   // mới có số tiền từ Tổng quan: lưu ưu tiên lần đầu
    pref.some((v, i) => v !== savedPref[i]) ||
    (budget ?? null) !== (saved.data?.budgetVnd ?? null);
  const canSave = !saved.loading && !budgetError && dirty && !saving;

  const update = (next: Partial<Draft>) => {
    setSaveMessage(null);
    // dạng hàm: 2 thay đổi liền nhau không ghi đè nhau
    setDraft((d) => ({ pref: d?.pref ?? savedPref, budgetText: d?.budgetText ?? savedBudgetText, ...next }));
  };

  const save = async () => {
    setSaving(true);
    setSaveMessage(null);
    try {
      await parentPreferenceApi.save(studentId, pref, budget);
      setDraft(null);
      setQuery({ key: "saved" });
      setVersion((v) => v + 1);
      setSaveMessage("Đã lưu. Con sẽ thấy danh sách theo ưu tiên của bạn.");
    } catch (e) {
      if (isPremiumRequired(e)) setSaveLocked(true);
      else setSaveMessage(e instanceof ApiError ? e.message : "Không lưu được, thử lại sau.");
    } finally {
      setSaving(false);
    }
  };

  if (saved.loading && saved.data == null) {
    return (
      <div className="flex min-h-[20vh] items-center justify-center">
        <Spinner className="size-6 text-brand" />
      </div>
    );
  }
  if (saveLocked || isPremiumRequired(saved.rawError) || isPremiumRequired(compare.rawError)) {
    return (
      <PremiumLockedCard
        title="So sánh ưu tiên Học sinh & Phụ huynh cần tài khoản USDSS Premium"
        note="Nâng cấp USDSS Premium để chọn ưu tiên, nhập ngân sách và so sánh danh sách trường với con."
      />
    );
  }
  if (saved.error) {
    return <p className="rounded-2xl border border-line bg-surface p-8 text-center text-sm text-muted-foreground">{saved.error}</p>;
  }

  const data = compare.data;
  const rate = data?.vndPerUsd ?? null;
  const studentPref = data?.studentPref ?? null;

  return (
    <div className="space-y-6">
      <section className="space-y-5 rounded-2xl border border-line bg-surface p-4">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <h2 className="text-h3 text-foreground">Ưu tiên của bạn</h2>
          {data?.isPreview && <Badge variant="warn">Đang xem thử, chưa lưu</Badge>}
        </div>

        {PREF_NAMES.map((name, i) => (
          <div key={name} className="space-y-2">
            <div className="flex flex-wrap items-baseline justify-between gap-2">
              <p className="text-sm font-semibold text-foreground">{name}</p>
              <p className="text-sm text-brand">{PREF_LEVELS[pref[i]]}</p>
            </div>
            <Slider
              value={[pref[i]]}
              min={0}
              max={4}
              step={1}
              aria-label={name}
              getAriaValueText={(_text, v) => PREF_LEVELS[v] ?? String(v)}
              onValueChange={(v) => {
                const value = Array.isArray(v) ? v[0] : v;
                setSaveMessage(null);
                setDraft((d) => {
                  const next = [...(d?.pref ?? savedPref)] as PrefTriple;
                  next[i] = value;
                  return { pref: next, budgetText: d?.budgetText ?? savedBudgetText };
                });
              }}
            />
            {studentPref && <p className="text-xs text-muted-foreground">Con chọn: {PREF_LEVELS[studentPref[i]]}</p>}
          </div>
        ))}

        <div className="space-y-1">
          <label htmlFor="pp-budget" className="text-sm font-semibold text-foreground">
            Ngân sách mỗi năm (VND)
          </label>
          <Input
            id="pp-budget"
            inputMode="numeric"
            placeholder="Để trống: dùng ngân sách trong hồ sơ của con"
            value={budgetText}
            aria-invalid={budgetError != null}
            onChange={(e) => update({ budgetText: formatBudget(e.target.value) })}
          />
          {budgetError ? (
            <p className="text-xs text-risk">{budgetError}</p>
          ) : budget == null ? (
            <p className="text-xs text-muted-foreground">Dùng ngân sách trong hồ sơ của con.</p>
          ) : rate ? (
            <p className="text-xs text-muted-foreground">
              ≈ {VND.format(Math.round(budget / rate))} USD theo tỷ giá {VND.format(Math.round(rate))} VND/USD{" "}
              {data?.savedAt && !data.isPreview
                ? `đã chốt ngày ${new Date(data.savedAt).toLocaleDateString("vi-VN")}`
                : "hôm nay"}
            </p>
          ) : null}
        </div>

        <div className="flex flex-wrap items-center gap-3">
          <Button type="button" disabled={!canSave} onClick={save} loading={saving}>
            Lưu và cho con xem
          </Button>
          {saveMessage && <p className="text-sm text-ink-2" role="status">{saveMessage}</p>}
        </div>
      </section>

      {compare.error ? (
        <p className="rounded-2xl border border-line bg-surface p-8 text-center text-sm text-muted-foreground">
          {compare.error}
        </p>
      ) : data ? (
        <div className={compare.loading ? "opacity-60 transition-opacity" : "transition-opacity"}>
          <PreferenceComparison data={data} parentName="Bạn" />
        </div>
      ) : (
        <div className="flex min-h-[20vh] items-center justify-center">
          <Spinner className="size-6 text-brand" />
        </div>
      )}
    </div>
  );
}
