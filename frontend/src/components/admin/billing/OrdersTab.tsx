"use client";

import { useEffect, useState } from "react";
import { SearchIcon } from "lucide-react";
import { Input } from "@/components/ui/input";
import { Button } from "@/components/ui/button";
import { NativeSelect, NativeSelectOption } from "@/components/ui/native-select";
import { adminBillingApi, adminBillingErrorMessage, formatVnDateTime } from "@/lib/admin-billing-api";
import { formatVnd } from "@/lib/billing-api";
import type { PagedResult } from "@/types/api";
import type { AdminOrderDto, AdminOrderFilters } from "@/types/admin-billing";
import type { OrderStatus } from "@/types/billing";
import { DataTable, type DataTableColumn } from "../shared/DataTable";
import { TablePagination } from "../shared/TablePagination";
import { OrderDetailPanel } from "./OrderDetailPanel";
import { ORDER_STATUS_LABELS, OrderStatusBadge } from "./OrderStatusBadge";

const EMPTY_FILTERS: AdminOrderFilters = { status: "", q: "", page: 1 };

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
    { id: "orderCode", label: "Mã đơn", required: true, render: (o) => <span className="font-mono text-xs">{o.orderCode}</span> },
    {
      id: "buyer",
      label: "Người mua",
      render: (o) => (
        <>
          {/* [UC-75 lớp 2] Người mua đã xóa tài khoản: chỉ hiện mã, họ tên và email xem trong chi tiết đơn */}
          <div className="font-medium">{o.buyerName ?? (o.buyerCode ? "Đã xóa tài khoản" : "—")}</div>
          <div className="text-xs text-muted-foreground">{o.buyerEmail ?? (o.buyerCode ? `Mã ${o.buyerCode.slice(0, 8)}` : "")}</div>
        </>
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
              <NativeSelect
                aria-label="Lọc theo trạng thái"
                value={applied.status}
                onChange={(e) => setApplied({ ...applied, status: e.target.value as OrderStatus | "", page: 1 })}
              >
                <NativeSelectOption value="">Mọi trạng thái</NativeSelectOption>
                {Object.entries(ORDER_STATUS_LABELS).map(([value, label]) => (
                  <NativeSelectOption key={value} value={value}>{label}</NativeSelectOption>
                ))}
              </NativeSelect>
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
                  placeholder="Tìm theo mã đơn hoặc email người mua"
                  aria-label="Tìm đơn hàng"
                  className="pr-10"
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
      {selected !== null && (
        <OrderDetailPanel
          key={selected}
          orderCode={selected}
          onClose={() => setSelected(null)}
          onChanged={() => setReload((n) => n + 1)}
        />
      )}
    </div>
  );
}
