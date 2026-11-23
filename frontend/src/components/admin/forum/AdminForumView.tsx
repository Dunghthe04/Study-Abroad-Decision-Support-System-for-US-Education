"use client";

import { useCallback, useEffect, useState } from "react";
import { AdminSection } from "@/components/admin/AdminSection";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Spinner } from "@/components/ui/spinner";
import { Textarea } from "@/components/ui/textarea";
import { toast } from "@/components/ui/toast";
import { ApiError } from "@/lib/api";
import { adminForumApi } from "@/lib/community-api";
import type { CommunityReportDto } from "@/types/community";

function RestrictAuthorForm({
  disabled,
  onRestrict,
}: {
  disabled: boolean;
  onRestrict: (days: number, reason: string) => Promise<void>;
}) {
  const [days, setDays] = useState("7");
  const [reason, setReason] = useState("");

  return (
    <div className="flex flex-col gap-2 sm:flex-row sm:items-end">
      <Input
        type="number"
        min={1}
        max={90}
        value={days}
        onChange={(e) => setDays(e.target.value)}
        className="sm:w-24"
        aria-label="Số ngày hạn chế"
        disabled={disabled}
      />
      <Textarea
        rows={2}
        value={reason}
        onChange={(e) => setReason(e.target.value)}
        placeholder="Lý do hạn chế chat"
        className="min-h-10"
        disabled={disabled}
      />
      <Button
        size="sm"
        variant="destructive"
        disabled={disabled}
        onClick={() => void onRestrict(Number(days) || 7, reason.trim() || "Vi phạm cộng đồng")}
      >
        Hạn chế tác giả
      </Button>
    </div>
  );
}

export function AdminForumView() {
  const [items, setItems] = useState<CommunityReportDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [busyId, setBusyId] = useState<string | null>(null);

  const refresh = useCallback(async () => {
    setLoading(true);
    try {
      const page = await adminForumApi.reports("open", 1, 50);
      setItems(page.items);
    } catch (err) {
      toast.add({
        type: "error",
        title: "Không tải được báo cáo",
        description: err instanceof ApiError ? err.message : "Thử lại sau.",
      });
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    void refresh();
  }, [refresh]);

  const act = async (id: string, fn: () => Promise<unknown>, ok: string) => {
    setBusyId(id);
    try {
      await fn();
      toast.add({ type: "success", title: ok });
      await refresh();
    } catch (err) {
      toast.add({
        type: "error",
        title: "Không xử lý được",
        description: err instanceof ApiError ? err.message : "Thử lại sau.",
      });
    } finally {
      setBusyId(null);
    }
  };

  const formatRestrictedTime = (iso?: string | null) => {
    if (!iso) return "";
    const d = new Date(iso);
    if (Number.isNaN(d.getTime())) return "";
    return d.toLocaleString("vi-VN", {
      hour: "2-digit",
      minute: "2-digit",
      day: "2-digit",
      month: "2-digit",
      year: "numeric",
    });
  };

  return (
    <AdminSection href="/admin/forum">
      <div className="space-y-4">
        <div>
          <h1 className="font-heading text-h2">Kiểm duyệt diễn đàn</h1>
          <p className="mt-1 text-sm text-muted-foreground">Báo cáo tin chat cộng đồng đang mở.</p>
        </div>
        {loading ? (
          <div className="flex items-center gap-2 text-sm text-muted-foreground">
            <Spinner /> Đang tải...
          </div>
        ) : items.length === 0 ? (
          <p className="text-sm text-muted-foreground">Không có báo cáo đang mở.</p>
        ) : (
          items.map((r) => (
            <Card key={r.id}>
              <CardHeader>
                <div className="flex items-center justify-between gap-2">
                  <CardTitle className="text-base">{r.authorName}</CardTitle>
                  {r.authorIsRestricted && (
                    <Badge variant="warn" className="text-xs">
                      Đang bị hạn chế {r.authorRestrictedUntil ? `đến ${formatRestrictedTime(r.authorRestrictedUntil)}` : ""}
                    </Badge>
                  )}
                </div>
                <CardDescription>
                  Báo cáo bởi {r.reporterName} · {r.reason}
                  {r.detail ? ` — ${r.detail}` : ""}
                </CardDescription>
              </CardHeader>
              <CardContent className="space-y-3">
                <p className="whitespace-pre-wrap rounded-lg bg-muted/50 p-3 text-sm">{r.messageContent}</p>
                <div className="flex flex-wrap gap-2">
                  <Button
                    size="sm"
                    disabled={busyId === r.id}
                    onClick={() => void act(r.id, () => adminForumApi.hide(r.id), "Đã ẩn tin")}
                  >
                    Ẩn tin
                  </Button>
                  <Button
                    size="sm"
                    variant="outline"
                    disabled={busyId === r.id}
                    onClick={() => void act(r.id, () => adminForumApi.dismiss(r.id), "Đã bỏ báo cáo")}
                  >
                    Bỏ qua
                  </Button>
                </div>
                {r.authorUserId && (
                  <div className="pt-2 border-t">
                    {r.authorIsRestricted ? (
                      <div className="flex items-center justify-between gap-2">
                        <span className="text-xs text-muted-foreground">Tài khoản này đang bị khóa quyền chat.</span>
                        <Button
                          size="sm"
                          variant="outline"
                          disabled={busyId === r.id}
                          onClick={() =>
                            void act(
                              r.id,
                              () => adminForumApi.unrestrict(r.authorUserId!),
                              "Đã gỡ hạn chế cho tác giả",
                            )
                          }
                        >
                          Gỡ hạn chế chat
                        </Button>
                      </div>
                    ) : (
                      <RestrictAuthorForm
                        disabled={busyId === r.id}
                        onRestrict={(days, reason) =>
                          act(
                            r.id,
                            () => adminForumApi.restrict(r.authorUserId!, days, reason),
                            "Đã hạn chế tài khoản",
                          )
                        }
                      />
                    )}
                  </div>
                )}
              </CardContent>
            </Card>
          ))
        )}
      </div>
    </AdminSection>
  );
}
