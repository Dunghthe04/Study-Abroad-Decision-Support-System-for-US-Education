"use client";

import { useState } from "react";
import Link from "next/link";
import { ScaleIcon } from "lucide-react";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { centerLegalHoldApi } from "@/lib/api";
import { formatDateTime } from "@/lib/ai-usage-format";
import { useFetched } from "@/lib/use-fetched";
import { CenterLegalHoldDialog, type LegalHoldAction } from "./CenterLegalHoldDialog";

const DELETION_STATUS: Record<string, string> = {
  pending: "Trung tâm đang có yêu cầu xóa chờ xử lý.",
  approved: "Trung tâm có yêu cầu xóa đã duyệt, chờ tới hạn xóa hẳn.",
};

/** [UC-93] Thẻ giữ pháp lý ở trang chi tiết trung tâm của Administrator. */
export function CenterLegalHoldCard({ centerId, centerName }: { centerId: string; centerName: string }) {
  const [version, setVersion] = useState(0);
  const [action, setAction] = useState<LegalHoldAction | null>(null);
  const query = useFetched(`center-legal-hold|${centerId}|${version}`, () => centerLegalHoldApi.get(centerId));
  const hold = query.data;

  return (
    <Card className="border-line">
      <CardHeader className="pb-3">
        <CardTitle className="flex flex-wrap items-center gap-2 text-base">
          <ScaleIcon className="size-4 text-brand" />
          Giữ pháp lý dữ liệu
          {hold ? (
            hold.isLegalHold ? <Badge variant="risk">Đang giữ</Badge> : <Badge variant="neutral">Không giữ</Badge>
          ) : null}
        </CardTitle>
        <CardDescription>
          Bật khi trung tâm bị nghi lừa đảo, có khiếu nại, tranh chấp hoặc cơ quan có thẩm quyền yêu cầu giữ dữ liệu. Trong thời gian giữ,
          dữ liệu trung tâm và tài khoản chủ không bị xóa và yêu cầu xóa không thể duyệt.
        </CardDescription>
      </CardHeader>
      <CardContent className="flex flex-col gap-3 text-sm">
        {query.error ? (
          <Alert variant="destructive">
            <AlertDescription>{query.error}</AlertDescription>
          </Alert>
        ) : null}

        {hold?.isLegalHold ? (
          <dl className="grid gap-1 rounded-md border bg-muted/30 px-3 py-2 text-xs">
            <div>
              <dt className="inline font-medium">Lý do: </dt>
              <dd className="inline whitespace-pre-wrap break-words">{hold.reason ?? "—"}</dd>
            </div>
            <div>
              <dt className="inline font-medium">Từ: </dt>
              <dd className="inline">
                {hold.since ? formatDateTime(hold.since) : "—"}
                {hold.byName ? ` · ${hold.byName}` : ""}
              </dd>
            </div>
          </dl>
        ) : null}

        {hold?.openDeletionRequestStatus ? (
          <p className="text-xs text-ink-2">
            {DELETION_STATUS[hold.openDeletionRequestStatus] ?? ""}
            {hold.openDeletionRequestStatus === "approved" && hold.scheduledPurgeAt
              ? ` Hạn xóa hẳn: ${formatDateTime(hold.scheduledPurgeAt)}.`
              : ""}{" "}
            <Link href="/admin/center-deletion-requests" className="underline underline-offset-2">
              Xem yêu cầu xóa
            </Link>
          </p>
        ) : null}

        {hold ? (
          <div>
            {hold.isLegalHold ? (
              <Button type="button" size="sm" variant="outline" onClick={() => setAction("release")}>
                Gỡ giữ pháp lý
              </Button>
            ) : (
              <Button type="button" size="sm" variant="destructive" onClick={() => setAction("set")}>
                Bật giữ pháp lý
              </Button>
            )}
          </div>
        ) : null}
      </CardContent>

      <CenterLegalHoldDialog
        target={action ? { action, centerId, centerName } : null}
        onClose={() => setAction(null)}
        onDone={() => {
          setAction(null);
          setVersion((v) => v + 1);
        }}
      />
    </Card>
  );
}
