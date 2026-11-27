"use client";

import { useEffect, useState } from "react";
import { PencilIcon, PlusIcon, Trash2Icon } from "lucide-react";
import {
  AlertDialog,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog";
import { Button } from "@/components/ui/button";
import { Spinner } from "@/components/ui/spinner";
import { Switch } from "@/components/ui/switch";
import { toast } from "@/components/ui/toast";
import { adminBillingApi, adminBillingErrorMessage, PLAN_KIND_LABELS } from "@/lib/admin-billing-api";
import { formatMultiplier, formatVnd } from "@/lib/billing-api";
import type { AdminPlanDto, SavePlanRequest } from "@/types/admin-billing";
import { DataTable, type DataTableColumn } from "../shared/DataTable";
import { RightPanelSlot } from "../shared/DetailPanel";
import { PlanFormPanel } from "./PlanFormPanel";

interface LoadResult {
  key: number;
  plans: AdminPlanDto[];
  error: string | null;
}

type Editing = { kind: "none" } | { kind: "new" } | { kind: "edit"; plan: AdminPlanDto };

const fail = (title: string, err: unknown, fallback: string) =>
  toast.add({ type: "error", title, description: adminBillingErrorMessage(err, fallback) });

/** [UC-79] Danh sách gói: thêm, sửa, bật/tắt mở bán, xóa gói chưa dùng. */
export function PlansTab() {
  const [reload, setReload] = useState(0);
  const [result, setResult] = useState<LoadResult | null>(null);
  const [editing, setEditing] = useState<Editing>({ kind: "none" });
  const [deleting, setDeleting] = useState<AdminPlanDto | null>(null);
  const [busyId, setBusyId] = useState<string | null>(null);

  const isLoading = result === null || result.key !== reload;
  const plans = result?.plans ?? [];

  useEffect(() => {
    let cancelled = false;
    adminBillingApi
      .plans()
      .then((loaded) => !cancelled && setResult({ key: reload, plans: loaded, error: null }))
      .catch((err: unknown) => {
        if (!cancelled) setResult({ key: reload, plans: [], error: adminBillingErrorMessage(err, "Không tải được danh sách gói.") });
      });
    return () => {
      cancelled = true;
    };
  }, [reload]);

  const refresh = () => setReload((n) => n + 1);

  // ponytail: không có PATCH, bật/tắt mở bán gửi lại toàn bộ gói qua PUT.
  const toggleActive = async (plan: AdminPlanDto, isActive: boolean) => {
    const body: SavePlanRequest = {
      name: plan.name,
      description: plan.description,
      priceVnd: plan.priceVnd,
      durationDays: plan.durationDays,
      tokenQuota: plan.tokenQuota,
      quotaPeriodDays: plan.quotaPeriodDays,
      isActive,
      displayOrder: plan.displayOrder,
    };
    setBusyId(plan.id);
    try {
      await adminBillingApi.updatePlan(plan.id, body);
    } catch (err) {
      fail("Không đổi được trạng thái mở bán", err, "Vui lòng thử lại.");
    } finally {
      setBusyId(null);
      refresh();
    }
  };

  const confirmDelete = async () => {
    if (!deleting) return;
    setBusyId(deleting.id);
    try {
      await adminBillingApi.deletePlan(deleting.id);
      toast.add({ type: "success", title: "Đã xóa gói", description: deleting.name });
    } catch (err) {
      fail("Không xóa được gói", err, "Vui lòng thử lại.");
    } finally {
      setBusyId(null);
      setDeleting(null);
      refresh();
    }
  };

  const columns: DataTableColumn<AdminPlanDto>[] = [
    {
      id: "code",
      label: "Mã",
      required: true,
      render: (p) => <span className="font-mono text-xs">{p.code}</span>,
    },
    {
      id: "name",
      label: "Tên gói",
      required: true,
      render: (p) => <span className="font-medium">{p.name}</span>,
    },
    {
      id: "kind",
      label: "Loại gói",
      render: (p) => PLAN_KIND_LABELS[p.kind],
    },
    { id: "price", label: "Giá", render: (p) => formatVnd(p.priceVnd) },
    { id: "duration", label: "Thời hạn", render: (p) => (p.durationDays ? `${p.durationDays} ngày` : "Không giới hạn") },
    {
      id: "quota",
      label: "Cấp token",
      render: (p) => {
        const period = p.quotaPeriodDays ?? p.durationDays;
        const cadence = period ? `${period} ngày` : "tháng";
        return `${p.tokenQuota.toLocaleString("vi-VN")} / ${cadence}`;
      },
    },
    {
      id: "multiplier",
      label: "So với Free",
      render: (p) => {
        if (p.kind === "free") return "Mốc chuẩn (1x)";
        return p.quotaMultiplier != null ? `Gấp ${formatMultiplier(p.quotaMultiplier)}x` : "—";
      },
    },
    {
      id: "isActive",
      label: "Mở bán",
      render: (p) => (
        <Switch
          checked={p.isActive}
          disabled={p.kind === "free" || busyId === p.id}
          onCheckedChange={(checked) => void toggleActive(p, checked)}
          onClick={(e) => e.stopPropagation()}
          aria-label={`Mở bán gói ${p.name}`}
        />
      ),
    },
    { id: "displayOrder", label: "Thứ tự", render: (p) => p.displayOrder },
    { id: "activeCycles", label: "Chu kỳ đang dùng", render: (p) => p.activeCycles },
    { id: "orderCount", label: "Số đơn", render: (p) => p.orderCount },
    {
      id: "actions",
      label: "Thao tác",
      required: true,
      render: (p) => (
        <div className="flex gap-1">
          <Button type="button" variant="ghost" size="icon-sm" aria-label={`Sửa gói ${p.name}`} onClick={() => setEditing({ kind: "edit", plan: p })}>
            <PencilIcon />
          </Button>
          {p.kind !== "free" && p.activeCycles === 0 && p.orderCount === 0 && (
            <Button type="button" variant="ghost" size="icon-sm" aria-label={`Xóa gói ${p.name}`} onClick={() => setDeleting(p)}>
              <Trash2Icon />
            </Button>
          )}
        </div>
      ),
    },
  ];

  return (
    <div className="flex min-h-0 flex-1 flex-col gap-6 lg:flex-row">
      <div className="flex min-h-0 min-w-0 flex-1 flex-col gap-4">
        <DataTable
          columns={columns}
          items={plans}
          getRowId={(p) => p.id}
          isLoading={isLoading}
          error={isLoading ? null : result.error}
          emptyMessage="Chưa có gói nào."
          label="Danh sách gói"
          toolbar={{
            actions: (
              <Button type="button" size="sm" onClick={() => setEditing({ kind: "new" })}>
                <PlusIcon />
                Thêm gói
              </Button>
            ),
          }}
        />
      </div>

      <RightPanelSlot item={editing.kind === "none" ? null : editing}>
        {(active) => (
          <PlanFormPanel
            key={active.kind === "edit" ? active.plan.id : "new"}
            plan={active.kind === "edit" ? active.plan : null}
            freePlan={plans.find((p) => p.kind === "free") ?? null}
            onClose={() => setEditing({ kind: "none" })}
            onSaved={() => {
              toast.add({ type: "success", title: active.kind === "new" ? "Đã tạo gói" : "Đã lưu gói" });
              setEditing({ kind: "none" });
              refresh();
            }}
          />
        )}
      </RightPanelSlot>

      <AlertDialog open={deleting !== null} onOpenChange={(open) => !open && busyId === null && setDeleting(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Xóa gói {deleting?.name}?</AlertDialogTitle>
            <AlertDialogDescription>Gói chưa có đơn hàng hay chu kỳ nào. Xóa rồi không khôi phục được.</AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel disabled={busyId !== null}>Hủy</AlertDialogCancel>
            <Button type="button" variant="destructive" onClick={() => void confirmDelete()} disabled={busyId !== null}>
              {busyId !== null && <Spinner className="size-3.5" />}
              Xóa gói
            </Button>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}
