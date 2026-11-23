"use client";

import Link from "next/link";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { AccountSectionHeader } from "@/components/account/AccountSectionHeader";
import { getPolicyAcceptanceHistory, getPolicyAcceptanceStatus } from "@/lib/policy-acceptance-api";
import { formatDateTime } from "@/lib/policy-format";
import { useFetched } from "@/lib/use-fetched";
import type { PolicyAcceptanceItem } from "@/types/policy";

const KIND_LABELS: Record<string, string> = {
  terms_of_use: "Điều khoản sử dụng",
  privacy_policy: "Chính sách bảo mật",
};
const KIND_HREFS: Record<string, string> = {
  terms_of_use: "/terms",
  privacy_policy: "/privacy",
};
const label = (kind: string) => KIND_LABELS[kind] ?? kind;

/** Tình trạng một loại: một cụm chữ và màu cho người dùng, không dùng mã kỹ thuật. */
function stateBadge(item: PolicyAcceptanceItem) {
  switch (item.state) {
    case "accepted":
      return <Badge variant="ok">Đã đồng ý bản hiện hành</Badge>;
    case "update_optional":
      return <Badge variant="neutral">Có bản mới (không cần đồng ý lại)</Badge>;
    case "update_required":
    case "never_accepted":
      return <Badge variant="warn">Cần đồng ý</Badge>;
    default:
      return <Badge variant="neutral">Chưa công bố</Badge>;
  }
}

/**
 * [UC-104] "Điều khoản đã đồng ý": mình đang ở bản nào, và mọi lần mình đã đồng ý (phiên bản, thời điểm).
 * Chỉ của chính người đang đăng nhập. Không hiện địa chỉ IP hay thông tin kỹ thuật.
 */
export function PolicyConsentsCard() {
  const status = useFetched("policy-status", getPolicyAcceptanceStatus);
  const history = useFetched("policy-history", getPolicyAcceptanceHistory);
  const items = status.data?.items ?? [];
  const records = history.data?.items ?? [];

  return (
    <div>
      <AccountSectionHeader
        title="Điều khoản đã đồng ý"
        description="Các bản Điều khoản sử dụng và Chính sách bảo mật bạn đã đồng ý, kèm thời điểm. Hệ thống lưu lại để bạn và chúng tôi cùng kiểm chứng được."
      />

      <div className="space-y-6">
        {(status.error || history.error) && (
          <Alert variant="destructive">
            <AlertDescription>Không tải được dữ liệu. Vui lòng tải lại trang.</AlertDescription>
          </Alert>
        )}

        <section className="overflow-hidden rounded-xl border border-line bg-surface">
          <div className="border-b border-line p-5 sm:px-6">
            <h2 className="font-heading text-lg font-semibold text-ink">Tình trạng hiện tại</h2>
          </div>
          <ul className="divide-y divide-line">
            {status.loading && !status.data ? (
              <li className="p-5 sm:px-6"><Skeleton className="h-10 w-full" /></li>
            ) : (
              items.map((item) => {
                const accepted = records.find((r) => r.kind === item.kind && r.isLatest && r.granted);
                return (
                  <li key={item.kind} className="flex flex-wrap items-center justify-between gap-3 p-5 sm:px-6">
                    <div className="min-w-0 space-y-0.5">
                      <div className="font-medium text-ink">{label(item.kind)}</div>
                      <div className="text-sm text-ink-2">
                        {item.acceptedVersion ? `Bạn đã đồng ý phiên bản ${item.acceptedVersion}` : "Bạn chưa đồng ý bản nào"}
                        {item.currentVersion && item.currentVersion !== item.acceptedVersion ? ` · Bản hiện hành: ${item.currentVersion}` : ""}
                        {accepted ? ` · ${formatDateTime(accepted.acceptedAt)}` : ""}
                      </div>
                    </div>
                    <div className="flex flex-wrap items-center gap-3">
                      {stateBadge(item)}
                      <Link href={KIND_HREFS[item.kind] ?? "/terms"} target="_blank" rel="noopener noreferrer" className="text-sm font-medium text-brand hover:underline">
                        Đọc bản hiện hành
                      </Link>
                    </div>
                  </li>
                );
              })
            )}
          </ul>
        </section>

        <section className="overflow-hidden rounded-xl border border-line bg-surface">
          <div className="space-y-1 border-b border-line p-5 sm:px-6">
            <h2 className="font-heading text-lg font-semibold text-ink">Lịch sử</h2>
            <p className="text-xs leading-relaxed text-ink-2">Mỗi lần bạn đồng ý là một dòng. Dòng cũ được giữ nguyên, không bị sửa hay xóa.</p>
          </div>
          {history.loading && !history.data ? (
            <div className="p-5 sm:px-6"><Skeleton className="h-16 w-full" /></div>
          ) : records.length === 0 ? (
            <p className="p-5 text-sm text-ink-2 sm:px-6">Chưa có lần đồng ý nào được ghi lại.</p>
          ) : (
            <div className="overflow-x-auto">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Văn bản</TableHead>
                    <TableHead>Phiên bản</TableHead>
                    <TableHead>Kết quả</TableHead>
                    <TableHead>Thời điểm</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {records.map((r, i) => (
                    <TableRow key={`${r.kind}:${r.version}:${r.acceptedAt}:${i}`}>
                      <TableCell className="font-medium">{label(r.kind)}</TableCell>
                      <TableCell className="tabular">{r.version}</TableCell>
                      <TableCell>
                        <div className="flex flex-wrap gap-1">
                          <Badge variant={r.granted ? "ok" : "neutral"}>{r.granted ? "Đã đồng ý" : "Đã từ chối"}</Badge>
                          {r.isLatest && <Badge variant="brand">Đang áp dụng</Badge>}
                        </div>
                      </TableCell>
                      <TableCell className="tabular">{formatDateTime(r.acceptedAt)}</TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </div>
          )}
        </section>
      </div>
    </div>
  );
}
