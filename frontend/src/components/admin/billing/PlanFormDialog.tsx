"use client";

import { useState } from "react";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { NativeSelect, NativeSelectOption } from "@/components/ui/native-select";
import { Spinner } from "@/components/ui/spinner";
import { Switch } from "@/components/ui/switch";
import { Textarea } from "@/components/ui/textarea";
import { adminBillingApi, adminBillingErrorMessage, PLAN_KIND_LABELS } from "@/lib/admin-billing-api";
import type { AdminPlanDto, SavePlanRequest } from "@/types/admin-billing";
import type { PlanKind } from "@/types/billing";

const CODE_PATTERN = /^[a-z0-9-]+$/;

interface PlanFormDialogProps {
  /** null = tạo gói mới. */
  plan: AdminPlanDto | null;
  hasFreePlan: boolean;
  onClose: () => void;
  onSaved: () => void;
}

const toText = (n: number | null | undefined) => (n === null || n === undefined ? "" : String(n));

/** Số nguyên ≥ 0, hoặc null nếu ô không hợp lệ. */
function wholeNumber(text: string): number | null {
  const n = Number(text.trim());
  return text.trim() !== "" && Number.isInteger(n) && n >= 0 ? n : null;
}

export function PlanFormDialog({ plan, hasFreePlan, onClose, onSaved }: PlanFormDialogProps) {
  const isNew = plan === null;
  const [code, setCode] = useState("");
  const [kind, setKind] = useState<PlanKind>(plan?.kind ?? "personal");
  const [name, setName] = useState(plan?.name ?? "");
  const [description, setDescription] = useState(plan?.description ?? "");
  const [price, setPrice] = useState(toText(plan?.priceVnd ?? 0));
  const [duration, setDuration] = useState(toText(plan?.durationDays ?? 30));
  const [tokenQuota, setTokenQuota] = useState(toText(plan?.tokenQuota ?? 0));
  const [estimated, setEstimated] = useState(toText(plan?.estimatedQuestions ?? 0));
  const [isActive, setIsActive] = useState(plan?.isActive ?? true);
  const [displayOrder, setDisplayOrder] = useState(toText(plan?.displayOrder ?? 0));
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  const isFree = kind === "free";

  const submit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const numbers = {
      price: isFree ? 0 : wholeNumber(price),
      tokenQuota: wholeNumber(tokenQuota),
      estimated: wholeNumber(estimated),
      displayOrder: wholeNumber(displayOrder),
    };
    if (isNew && !CODE_PATTERN.test(code.trim())) {
      setError("Mã gói chỉ gồm chữ thường, số và dấu gạch ngang.");
      return;
    }
    if (!name.trim()) {
      setError("Cần nhập tên gói.");
      return;
    }
    if (Object.values(numbers).some((n) => n === null)) {
      setError("Giá, quota token, số câu hỏi ước tính và thứ tự hiển thị phải là số nguyên từ 0 trở lên.");
      return;
    }
    const body: SavePlanRequest = {
      ...(isNew ? { code: code.trim(), kind } : {}),
      name: name.trim(),
      description: description.trim() || null,
      priceVnd: numbers.price!,
      durationDays: isFree ? null : Number(duration),
      tokenQuota: numbers.tokenQuota!,
      estimatedQuestions: numbers.estimated!,
      isActive: isFree ? true : isActive,
      displayOrder: numbers.displayOrder!,
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

  return (
    <Dialog open onOpenChange={(open) => !open && !saving && onClose()}>
      <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>{isNew ? "Thêm gói" : `Sửa gói ${plan.code}`}</DialogTitle>
          <DialogDescription>
            {isNew ? "Mã và loại gói không đổi được sau khi tạo." : `Loại: ${PLAN_KIND_LABELS[plan.kind]}. Giá, thời hạn và quota mới chỉ áp dụng cho đơn và chu kỳ sau này.`}
          </DialogDescription>
        </DialogHeader>
        <form onSubmit={submit} className="space-y-4">
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
                <NativeSelect id="plan-kind" className="w-full" value={kind} onChange={(e) => setKind(e.target.value as PlanKind)}>
                  <NativeSelectOption value="free" disabled={hasFreePlan}>{PLAN_KIND_LABELS.free}</NativeSelectOption>
                  <NativeSelectOption value="personal">{PLAN_KIND_LABELS.personal}</NativeSelectOption>
                  <NativeSelectOption value="family">{PLAN_KIND_LABELS.family}</NativeSelectOption>
                </NativeSelect>
              ))}
            </div>
          )}
          {field("plan-name", "Tên gói", <Input id="plan-name" value={name} onChange={(e) => setName(e.target.value)} required />)}
          {field("plan-description", "Mô tả", (
            <Textarea id="plan-description" value={description} onChange={(e) => setDescription(e.target.value)} rows={3} />
          ))}
          <div className="grid gap-4 sm:grid-cols-2">
            {field("plan-price", "Giá (VND)", (
              <Input id="plan-price" type="number" min={0} step={1} value={isFree ? "0" : price} disabled={isFree} onChange={(e) => setPrice(e.target.value)} />
            ), isFree ? "Gói Miễn phí luôn 0đ" : undefined)}
            {!isFree && field("plan-duration", "Thời hạn", (
              <NativeSelect id="plan-duration" className="w-full" value={duration} onChange={(e) => setDuration(e.target.value)}>
                <NativeSelectOption value="30">30 ngày</NativeSelectOption>
                <NativeSelectOption value="90">90 ngày</NativeSelectOption>
              </NativeSelect>
            ))}
            {field("plan-quota", "Quota token", (
              <Input id="plan-quota" type="number" min={0} step={1} value={tokenQuota} onChange={(e) => setTokenQuota(e.target.value)} />
            ), "Chu kỳ đang chạy giữ quota cũ")}
            {field("plan-estimated", "Số câu hỏi ước tính", (
              <Input id="plan-estimated" type="number" min={0} step={1} value={estimated} onChange={(e) => setEstimated(e.target.value)} />
            ), "Chỉ để hiển thị")}
            {field("plan-order", "Thứ tự hiển thị", (
              <Input id="plan-order" type="number" min={0} step={1} value={displayOrder} onChange={(e) => setDisplayOrder(e.target.value)} />
            ))}
          </div>
          <div className="flex items-center justify-between gap-3">
            <label htmlFor="plan-active" className="text-sm font-medium">Đang mở bán</label>
            <Switch id="plan-active" checked={isFree || isActive} disabled={isFree} onCheckedChange={setIsActive} />
          </div>
          <DialogFooter>
            <Button type="button" variant="outline" onClick={onClose} disabled={saving}>Hủy</Button>
            <Button type="submit" disabled={saving}>
              {saving && <Spinner className="size-3.5" />}
              {isNew ? "Tạo gói" : "Lưu"}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
