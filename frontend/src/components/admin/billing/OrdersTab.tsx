"use client";

import { useEffect, useState } from "react";
import { SearchIcon } from "lucide-react";
import { Input } from "@/components/ui/input";
import { Button } from "@/components/ui/button";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { adminBillingApi, adminBillingErrorMessage, formatVnDateTime } from "@/lib/admin-billing-api";
import { formatVnd } from "@/lib/billing-api";
import type { PagedResult } from "@/types/api";
import type { AdminOrderDto, AdminOrderFilters } from "@/types/admin-billing";
import type { OrderStatus } from "@/types/billing";
import { DataTable, type DataTableColumn } from "../shared/DataTable";
import { RightPanelSlot } from "../shared/DetailPanel";
import { TablePagination } from "../shared/TablePagination";
import { OrderDetailPanel } from "./OrderDetailPanel";
import { ORDER_STATUS_LABELS, OrderStatusBadge } from "./OrderStatusBadge";

const EMPTY_FILTERS: AdminOrderFilters = { status: "", q: "", from: "", to: "", page: 1 };
const ALL_STATUS = "__all__";
const STATUS_OPTIONS = [
  { value: ALL_STATUS, label: "Mọi trạng thái" },
  ...Object.entries(ORDER_STATUS_LABELS).map(([value, label]) => ({ value, label })),
];

interface LoadResult {
  key: string;
  data: PagedResult<AdminOrderDto> | null;
  error: string | null;
}

/** [UC-132..134] Đơn hàng: lọc trạng thái, tìm mã đơn hoặc email người mua, xem chi tiết và xử lý. */
export function OrdersTab() {
  const [applied, setApplied] = useState<AdminOrderFilters>(EMPTY_FILTERS);
  const [search, setSearch] = useState("");
  const [reload, setReload] = useState(0);
  const [result, setResult] = useState<LoadResult | null>(null);
  const [selected, setSelected] = useState<number | null>(null);

  const requestKey = JSON.stringify({ applied, reload });
  const isLoading = result === null || result.key !== requestKey;
  const data = isLoading ? null : result.data;

  useEffect(() => {
    let cancelled = false;
    adminBillingApi
      .orders(applied)
      .then((loaded) => !cancelled && setResult({ key: requestKey, data: loaded, error: null }))
      .catch((err: unknown) => {
        if (!cancelled) setResult({ key: requestKey, data: null, error: adminBillingErrorMessage(err, "Không tải được danh sách đơn hàng.") });
      });
    return () => {
      cancelled = true;
    };
  }, [applied, requestKey]);

  const columns: DataTableColumn<AdminOrderDto>[] = [
    {
      id: "orderCode",
      label: "Mã đơn",
      required: true,
      render: (o) => (
        <>
          <div className="font-mono text-xs">{o.orderCode}</div>
          {o.gatewayTransactionId && (
            <div className="font-mono text-[11px] text-muted-foreground" title="Mã giao dịch payOS">
              {o.gatewayTransactionId}
            </div>
          )}
        </>
      ),
    },
    {
      id: "buyer",
      label: "Người mua",
      render: (o) =>
        o.buyerName ? (
          <>
            <div className="font-medium">{o.buyerName}</div>
            <div className="text-xs text-muted-foreground">{o.buyerEmail ?? ""}</div>
          </>
        ) : o.deletedBuyerCode ? (
          <>
            <div className="font-mono text-xs font-medium">{o.deletedBuyerCode}</div>
            <div className="text-xs text-muted-foreground">Tài khoản đã xóa</div>
          </>
        ) : (
          <span title="Chưa có dữ liệu" aria-label="Chưa có dữ liệu" className="text-muted-foreground">—</span>
        ),
    },
    { id: "plan", label: "Gói", render: (o) => o.planName },
    { id: "amount", label: "Số tiền", render: (o) => formatVnd(o.amountVnd) },
    { id: "status", label: "Trạng thái", required: true, render: (o) => <OrderStatusBadge status={o.status} /> },
    { id: "createdAt", label: "Tạo lúc", render: (o) => formatVnDateTime(o.createdAt) },
  ];

  return (
    <div className="flex min-h-0 flex-1 flex-col gap-6 lg:flex-row">
      <div className="flex min-h-0 min-w-0 flex-1 flex-col gap-4">
        <DataTable
          columns={columns}
          items={data?.items ?? []}
          getRowId={(o) => String(o.orderCode)}
          selectedRowId={selected === null ? undefined : String(selected)}
          onRowSelect={(o) => setSelected(o.orderCode)}
          isLoading={isLoading}
          error={isLoading ? null : result.error}
          emptyMessage="Không có đơn hàng nào khớp bộ lọc."
          label="Danh sách đơn hàng"
          toolbar={{
            filters: (
              <div className="flex flex-wrap items-center gap-2">
                <Select
                  value={applied.status || ALL_STATUS}
                  onValueChange={(v) =>
                    setApplied({
                      ...applied,
                      status: (v === ALL_STATUS || v === null ? "" : v) as OrderStatus | "",
                      page: 1,
                    })
                  }
                  items={STATUS_OPTIONS}
                >
                  <SelectTrigger aria-label="Lọc theo trạng thái" className="bg-card shadow-xs">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {STATUS_OPTIONS.map((opt) => (
                      <SelectItem key={opt.value} value={opt.value}>
                        {opt.label}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
                <Input
                  type="date"
                  aria-label="Từ ngày"
                  title="Từ ngày"
                  value={applied.from}
                  onChange={(e) => setApplied({ ...applied, from: e.target.value, page: 1 })}
                  className="w-auto bg-card shadow-xs"
                />
                <Input
                  type="date"
                  aria-label="Đến ngày"
                  title="Đến ngày"
                  value={applied.to}
                  onChange={(e) => setApplied({ ...applied, to: e.target.value, page: 1 })}
                  className="w-auto bg-card shadow-xs"
                />
                {(applied.from || applied.to) && (
                  <Button
                    type="button"
                    variant="ghost"
                    size="sm"
                    onClick={() => setApplied({ ...applied, from: "", to: "", page: 1 })}
                  >
                    Xóa ngày
                  </Button>
                )}
              </div>
            ),
            search: (
              <form
                role="search"
                className="relative"
                onSubmit={(e) => {
                  e.preventDefault();
                  setApplied({ ...applied, q: search.trim(), page: 1 });
                }}
              >
                <Input
                  value={search}
                  onChange={(e) => setSearch(e.target.value)}
                  placeholder="Tìm theo mã đơn, mã GD payOS, tên hoặc email"
                  aria-label="Tìm đơn hàng"
                  className="bg-card pr-10 shadow-xs"
                />
                <Button type="submit" variant="ghost" size="icon-sm" aria-label="Tìm" className="absolute top-1/2 right-1 -translate-y-1/2">
                  <SearchIcon />
                </Button>
              </form>
            ),
          }}
          footer={
            <TablePagination
              page={data?.page ?? applied.page}
              totalPages={data?.totalPages ?? 0}
              totalCount={data?.totalCount}
              itemLabel="đơn hàng"
              disabled={isLoading || !data}
              onPageChange={(page) => setApplied({ ...applied, page })}
            />
          }
        />
      </div>
      <RightPanelSlot item={selected}>
        {(code) => (
          <OrderDetailPanel
            key={code}
            orderCode={code}
            onClose={() => setSelected(null)}
            onChanged={() => setReload((n) => n + 1)}
          />
        )}
      </RightPanelSlot>
    </div>
  );
}
