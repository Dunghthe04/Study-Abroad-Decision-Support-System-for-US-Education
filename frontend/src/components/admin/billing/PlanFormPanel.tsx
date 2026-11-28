"use client";

import { useState } from "react";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Switch } from "@/components/ui/switch";
import { Textarea } from "@/components/ui/textarea";
import { adminBillingApi, adminBillingErrorMessage, PLAN_KIND_LABELS } from "@/lib/admin-billing-api";
import { formatMultiplier } from "@/lib/billing-api";
import type { AdminPlanDto, SavePlanRequest } from "@/types/admin-billing";
import type { PlanKind } from "@/types/billing";
import { DetailPanel } from "../shared/DetailPanel";

const CODE_PATTERN = /^[a-z0-9-]+$/;
const DURATION_PRESETS = [7, 30, 90, 365] as const;
const KIND_OPTIONS = [
  { value: "personal", label: PLAN_KIND_LABELS.personal },
  { value: "family", label: PLAN_KIND_LABELS.family },
];

interface PlanFormPanelProps {
  /** null = tạo gói mới. */
  plan: AdminPlanDto | null;
  freePlan: AdminPlanDto | null;
  onClose: () => void;
  onSaved: () => void;
}

const toText = (n: number | null | undefined) => (n === null || n === undefined ? "" : String(n));

/** Số nguyên ≥ 0, hoặc null nếu ô không hợp lệ. */
function wholeNumber(text: string): number | null {
  const n = Number(text.trim());
  return text.trim() !== "" && Number.isInteger(n) && n >= 0 ? n : null;
}

/** Số nguyên ≥ 1, hoặc null nếu ô không hợp lệ. */
function positiveInt(text: string): number | null {
  const n = wholeNumber(text);
  return n !== null && n >= 1 ? n : null;
}

export function PlanFormPanel({ plan, freePlan, onClose, onSaved }: PlanFormPanelProps) {
  const isNew = plan === null;
  const [code, setCode] = useState("");
  const [kind, setKind] = useState<PlanKind>(plan?.kind ?? "personal");
  const [name, setName] = useState(plan?.name ?? "");
  const [description, setDescription] = useState(plan?.description ?? "");
  const [price, setPrice] = useState(toText(plan?.priceVnd ?? 0));
  const [duration, setDuration] = useState(toText(plan?.durationDays ?? 30));
  const [tokenQuota, setTokenQuota] = useState(toText(plan?.tokenQuota ?? 0));
  const [quotaPeriodDays, setQuotaPeriodDays] = useState(toText(plan?.quotaPeriodDays));
  const [isActive, setIsActive] = useState(plan?.isActive ?? true);
  const [displayOrder, setDisplayOrder] = useState(toText(plan?.displayOrder ?? 0));
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  const isFree = kind === "free";

  const parsedQuota = wholeNumber(tokenQuota);
  const parsedDuration = isFree ? null : positiveInt(duration);
  const parsedQuotaPeriod = quotaPeriodDays.trim() === "" ? null : positiveInt(quotaPeriodDays);
  const effectivePeriodDays = isFree ? parsedQuotaPeriod : (parsedQuotaPeriod ?? parsedDuration);
  const periodLabel = effectivePeriodDays ? `${effectivePeriodDays} ngày` : isFree ? "tháng" : "cả kỳ";

  const previewMultiplier = (() => {
    if (isFree || !freePlan || freePlan.tokenQuota <= 0 || parsedQuota === null || !effectivePeriodDays) return null;
    const freePeriodDays = freePlan.quotaPeriodDays ?? 30;
    const mult = (parsedQuota / effectivePeriodDays) / (freePlan.tokenQuota / freePeriodDays);
    return mult > 0 && Number.isFinite(mult) ? mult : null;
  })();

  const submit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const parsedPrice = isFree ? 0 : wholeNumber(price);
    const parsedOrder = wholeNumber(displayOrder);

    if (isNew && !CODE_PATTERN.test(code.trim())) {
      setError("Mã gói chỉ gồm chữ thường, số và dấu gạch ngang.");
      return;
    }
    if (!name.trim()) {
      setError("Cần nhập tên gói.");
      return;
    }
    if (parsedPrice === null || parsedQuota === null || parsedOrder === null) {
      setError("Giá, quota token và thứ tự hiển thị phải là số nguyên từ 0 trở lên.");
      return;
    }
    if (!isFree && (parsedDuration === null || parsedDuration < 1 || parsedDuration > 3650)) {
      setError("Thời hạn gói trả phí phải là số nguyên từ 1 đến 3650 ngày.");
      return;
    }
    if (quotaPeriodDays.trim() !== "") {
      if (parsedQuotaPeriod === null) {
        setError("Chu kỳ cấp token phải là số nguyên từ 1 trở lên.");
        return;
      }
      if (isFree && parsedQuotaPeriod > 365) {
        setError("Chu kỳ cấp token của gói Miễn phí tối đa 365 ngày.");
        return;
      }
      if (!isFree && parsedDuration !== null && parsedQuotaPeriod > parsedDuration) {
        setError("Chu kỳ cấp token không được lớn hơn thời hạn gói.");
        return;
      }
    }

    const body: SavePlanRequest = isFree && plan
      ? {
          name: plan.name,
          description: description.trim() || null,
          priceVnd: 0,
          durationDays: null,
          tokenQuota: parsedQuota,
          quotaPeriodDays: plan.quotaPeriodDays,
          isActive: true,
          displayOrder: plan.displayOrder,
        }
      : {
          ...(isNew ? { code: code.trim(), kind } : {}),
          name: name.trim(),
          description: description.trim() || null,
          priceVnd: parsedPrice,
          durationDays: isFree ? null : parsedDuration,
          tokenQuota: parsedQuota,
          quotaPeriodDays: parsedQuotaPeriod,
          isActive: isFree ? true : isActive,
          displayOrder: parsedOrder,
        };
    setSaving(true);
    setError(null);
    try {
      if (isNew) await adminBillingApi.createPlan(body);
      else await adminBillingApi.updatePlan(plan.id, body);
      onSaved();
    } catch (err) {
      setError(adminBillingErrorMessage(err, "Không lưu được gói. Vui lòng thử lại."));
    } finally {
      setSaving(false);
    }
  };

  const field = (id: string, label: string, control: React.ReactNode, hint?: string) => (
    <div className="space-y-1.5">
      <label htmlFor={id} className="text-sm font-medium">{label}</label>
      {control}
      {hint && <p className="text-xs text-muted-foreground">{hint}</p>}
    </div>
  );

  const formId = "plan-form";

  return (
    <DetailPanel
      title={isNew ? "Thêm gói" : `Sửa gói ${plan.name}`}
      description={isNew ? "Mã và loại gói không đổi được sau khi tạo." : `Loại: ${PLAN_KIND_LABELS[plan.kind]}. Giá, thời hạn và quota mới chỉ áp dụng cho đơn và chu kỳ sau này.`}
      onClose={onClose}
      closeDisabled={saving}
      footer={(
        <>
          <Button type="button" variant="outline" onClick={onClose} disabled={saving}>Hủy</Button>
          <Button type="submit" form={formId} loading={saving}>
            {isNew ? "Tạo gói" : "Lưu"}
          </Button>
        </>
      )}
    >
      <form id={formId} onSubmit={submit} className="space-y-4">
        {error && (
          <Alert variant="destructive">
            <AlertDescription>{error}</AlertDescription>
          </Alert>
        )}
        {isNew && (
          <div className="grid gap-4 sm:grid-cols-2">
            {field("plan-code", "Mã gói", (
              <Input id="plan-code" value={code} onChange={(e) => setCode(e.target.value)} placeholder="personal-1m" required pattern="[a-z0-9\-]+" />
            ), "Chữ thường, số, dấu gạch ngang")}
            {field("plan-kind", "Loại gói", (
              <Select
                value={kind}
                onValueChange={(v) => {
                  if (v !== null) setKind(v as PlanKind);
                }}
                items={KIND_OPTIONS}
              >
                <SelectTrigger id="plan-kind" className="w-full">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {KIND_OPTIONS.map((opt) => (
                    <SelectItem key={opt.value} value={opt.value}>
                      {opt.label}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            ))}
          </div>
        )}
        {isFree && (
          <Alert>
            <AlertDescription>Gói Miễn phí chỉ sửa được hạn mức token và mô tả.</AlertDescription>
          </Alert>
        )}
        {field("plan-name", "Tên gói", <Input id="plan-name" value={name} disabled={isFree} onChange={(e) => setName(e.target.value)} required />)}
        {field("plan-description", "Mô tả", (
          <Textarea id="plan-description" value={description} onChange={(e) => setDescription(e.target.value)} rows={3} />
        ))}
        <div className="grid gap-4 sm:grid-cols-2">
          {field("plan-price", "Giá (VND)", (
            <Input id="plan-price" type="number" min={0} step={1} value={isFree ? "0" : price} disabled={isFree} onChange={(e) => setPrice(e.target.value)} />
          ), isFree ? "Gói Miễn phí luôn 0đ" : undefined)}
          {!isFree && field("plan-duration", "Thời hạn (ngày)", (
            <div className="space-y-1.5">
              <Input
                id="plan-duration"
                type="number"
                min={1}
                max={3650}
                step={1}
                value={duration}
                onChange={(e) => setDuration(e.target.value)}
              />
              <div className="flex flex-wrap gap-1">
                {DURATION_PRESETS.map((days) => (
                  <button
                    key={days}
                    type="button"
                    onClick={() => setDuration(String(days))}
                    className={`rounded-md border px-2 py-0.5 text-xs transition-colors cursor-pointer ${
                      duration.trim() === String(days)
                        ? "border-brand bg-brand-wash font-medium text-brand"
                        : "border-line bg-ground text-ink-2 hover:text-ink"
                    }`}
                  >
                    {days} ngày
                  </button>
                ))}
              </div>
            </div>
          ))}
          {field("plan-quota", "Quota token", (
            <Input id="plan-quota" type="number" min={0} step={1} value={tokenQuota} onChange={(e) => setTokenQuota(e.target.value)} />
          ), "Chu kỳ đang chạy giữ quota cũ")}
          {field("plan-quota-period", "Chu kỳ cấp token (ngày)", (
            <Input
              id="plan-quota-period"
              type="number"
              min={1}
              max={isFree ? 365 : (parsedDuration ?? 3650)}
              step={1}
              disabled={isFree}
              placeholder={isFree ? "Mỗi tháng" : "Cả kỳ"}
              value={quotaPeriodDays}
              onChange={(e) => setQuotaPeriodDays(e.target.value)}
            />
          ), isFree
            ? "Để trống = làm mới ngày 1 mỗi tháng, hoặc nhập số ngày (vd 7, 30)."
            : "Để trống = cấp 1 lần cho cả kỳ. Nhập số ngày (vd 7, 30) để cấp lại token mỗi N ngày.")}
          {field("plan-order", "Thứ tự hiển thị", (
            <Input id="plan-order" type="number" min={0} step={1} disabled={isFree} value={displayOrder} onChange={(e) => setDisplayOrder(e.target.value)} />
          ))}
        </div>

        {parsedQuota !== null && (
          <div className="rounded-lg border border-line bg-ground px-3.5 py-2.5 text-xs text-ink-2 space-y-0.5">
            <p className="font-medium text-ink">
              Cấp {parsedQuota.toLocaleString("vi-VN")} token mỗi {periodLabel}
            </p>
            {previewMultiplier !== null && (
              <p>≈ Gấp {formatMultiplier(previewMultiplier)}x hạn mức gói Miễn phí</p>
            )}
          </div>
        )}

        <div className="flex items-center justify-between gap-3">
          <label htmlFor="plan-active" className="text-sm font-medium">Đang mở bán</label>
          <Switch id="plan-active" checked={isFree || isActive} disabled={isFree} onCheckedChange={setIsActive} />
        </div>
      </form>
    </DetailPanel>
  );
}
