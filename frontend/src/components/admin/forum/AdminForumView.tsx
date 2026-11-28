"use client";

import { useCallback, useEffect, useId, useState } from "react";
import { BanIcon, CheckIcon, EyeOffIcon, FlagIcon, LockIcon, LockOpenIcon, RefreshCwIcon, ShieldCheckIcon } from "lucide-react";
import { AdminSection } from "@/components/admin/AdminSection";
import { Button } from "@/components/ui/button";
import { Empty, EmptyHeader, EmptyMedia, EmptyTitle } from "@/components/ui/empty";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Popover, PopoverContent, PopoverTrigger } from "@/components/ui/popover";
import { Skeleton } from "@/components/ui/skeleton";
import { Textarea } from "@/components/ui/textarea";
import { toast } from "@/components/ui/toast";
import { ToggleGroup, ToggleGroupItem } from "@/components/ui/toggle-group";
import { Tooltip, TooltipContent, TooltipTrigger } from "@/components/ui/tooltip";
import { ApiError } from "@/lib/api";
import { adminForumApi } from "@/lib/community-api";
import { cn } from "@/lib/utils";
import type { CommunityReportDto } from "@/types/community";

const DURATION_PRESETS = ["1", "3", "7", "30"];

function initials(name: string) {
  const parts = name.trim().split(/\s+/).filter(Boolean);
  if (parts.length === 0) return "?";
  if (parts.length === 1) return parts[0].slice(0, 2).toUpperCase();
  return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
}

function formatFullDateTime(iso: string) {
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return "";
  return d.toLocaleString("vi-VN", {
    hour: "2-digit",
    minute: "2-digit",
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
  });
}

/** "Hôm nay 17:00" / "Hôm qua 17:00" / "dd/MM/yyyy 17:00" */
function formatHeaderTime(iso: string) {
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return "";
  const key = (x: Date) => `${x.getFullYear()}-${x.getMonth() + 1}-${x.getDate()}`;
  const now = new Date();
  const yesterday = new Date(now);
  yesterday.setDate(yesterday.getDate() - 1);
  const day =
    key(d) === key(now)
      ? "Hôm nay"
      : key(d) === key(yesterday)
        ? "Hôm qua"
        : d.toLocaleDateString("vi-VN", { day: "2-digit", month: "2-digit", year: "numeric" });
  const hm = d.toLocaleTimeString("vi-VN", { hour: "2-digit", minute: "2-digit" });
  return `${day} ${hm}`;
}

/** "vừa xong" / "n phút trước" / "n giờ trước" / "dd/MM" */
function formatRelative(iso: string) {
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return "";
  const mins = Math.floor((Date.now() - d.getTime()) / 60000);
  if (mins < 1) return "vừa xong";
  if (mins < 60) return `${mins} phút trước`;
  if (mins < 24 * 60) return `${Math.floor(mins / 60)} giờ trước`;
  return d.toLocaleDateString("vi-VN", { day: "2-digit", month: "2-digit" });
}

function Tip({ label, children }: { label: string; children: React.ReactElement }) {
  return (
    <Tooltip>
      <TooltipTrigger render={children} />
      <TooltipContent>{label}</TooltipContent>
    </Tooltip>
  );
}

function Dot() {
  return (
    <span aria-hidden className="text-ink-3">
      ·
    </span>
  );
}

function RestrictAuthorForm({
  authorName,
  disabled,
  onRestrict,
}: {
  authorName: string;
  disabled: boolean;
  onRestrict: (days: number, reason: string) => Promise<void>;
}) {
  const [open, setOpen] = useState(false);
  const [days, setDays] = useState("7");
  const [reason, setReason] = useState("");
  const uid = useId();
  const n = Number(days) || 7;

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger
        render={
          <Button size="sm" variant="outline" disabled={disabled}>
            <BanIcon data-icon="inline-start" />
            Hạn chế
          </Button>
        }
      />
      <PopoverContent align="end" className="w-80 space-y-3 p-4">
        <div className="flex items-center gap-2">
          <BanIcon aria-hidden className="size-4 shrink-0 text-risk" />
          <p className="truncate text-sm font-semibold text-ink">Hạn chế chat {authorName}</p>
        </div>
        <div className="space-y-2">
          <ToggleGroup
            variant="outline"
            spacing={1}
            aria-label="Thời hạn hạn chế"
            value={DURATION_PRESETS.includes(days) ? [days] : []}
            onValueChange={(v) => {
              if (v[0]) setDays(v[0]);
            }}
          >
            {DURATION_PRESETS.map((d) => (
              <ToggleGroupItem key={d} value={d} className="aria-pressed:bg-brand-wash aria-pressed:text-brand">
                {d} ngày
              </ToggleGroupItem>
            ))}
          </ToggleGroup>
          <div className="flex items-center gap-2">
            <Label htmlFor={`${uid}-days`} className="text-ink-2">
              Số ngày
            </Label>
            <Input
              id={`${uid}-days`}
              type="number"
              min={1}
              max={90}
              value={days}
              onChange={(e) => setDays(e.target.value)}
              className="w-20"
            />
          </div>
        </div>
        <div className="space-y-1.5">
          <Label htmlFor={`${uid}-reason`} className="text-ink-2">
            Lý do
          </Label>
          <Textarea
            id={`${uid}-reason`}
            rows={2}
            value={reason}
            onChange={(e) => setReason(e.target.value)}
            placeholder="Vi phạm cộng đồng"
          />
        </div>
        <Button
          size="sm"
          variant="destructive"
          className="w-full"
          disabled={disabled}
          onClick={() => {
            setOpen(false);
            void onRestrict(n, reason.trim() || "Vi phạm cộng đồng");
          }}
        >
          Hạn chế {n} ngày
        </Button>
      </PopoverContent>
    </Popover>
  );
}

function ReportSkeleton() {
  return (
    <li className="space-y-3 px-5 py-4 sm:px-6" aria-hidden>
      <Skeleton className="h-6 w-64" />
      <div className="flex gap-3">
        <Skeleton className="size-10 shrink-0 rounded-full" />
        <div className="flex-1 space-y-2">
          <Skeleton className="h-4 w-48" />
          <Skeleton className="h-4 w-full max-w-md" />
        </div>
      </div>
    </li>
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

  return (
    <AdminSection href="/admin/forum">
      <div className="max-w-5xl space-y-4">
        <div className="flex items-center gap-3">
          <h1 className="font-heading text-h2">Kiểm duyệt diễn đàn</h1>
          {items.length > 0 && (
            <span
              className="inline-flex h-6 items-center gap-1 rounded-full bg-risk-wash px-2 text-xs font-semibold text-risk"
              aria-label={`${items.length} báo cáo đang mở`}
            >
              <FlagIcon aria-hidden className="size-3" />
              {items.length}
            </span>
          )}
          <div className="ml-auto">
            <Tip label="Tải lại">
              <Button
                type="button"
                variant="ghost"
                size="icon"
                className="text-ink-2"
                aria-label="Tải lại"
                onClick={() => void refresh()}
              >
                <RefreshCwIcon className={cn("size-4", loading && "animate-spin")} />
              </Button>
            </Tip>
          </div>
        </div>

        <div className="overflow-hidden rounded-xl bg-surface ring-1 ring-line">
          {loading && items.length === 0 ? (
            <ul className="divide-y divide-line" aria-busy>
              {[0, 1, 2].map((i) => (
                <ReportSkeleton key={i} />
              ))}
            </ul>
          ) : items.length === 0 ? (
            <Empty className="py-14">
              <EmptyHeader>
                <EmptyMedia className="size-12 rounded-full bg-ok-wash text-ok">
                  <ShieldCheckIcon aria-hidden className="size-6" />
                </EmptyMedia>
                <EmptyTitle className="text-base text-ink">Không còn báo cáo nào</EmptyTitle>
              </EmptyHeader>
            </Empty>
          ) : (
            <ul className="divide-y divide-line">
              {items.map((r) => {
                const busy = busyId === r.id;
                return (
                  <li
                    key={r.id}
                    className={cn(
                      "grid gap-3 px-5 py-4 sm:px-6 lg:grid-cols-[1fr_auto] lg:items-start",
                      busy && "pointer-events-none opacity-60",
                    )}
                  >
                    <div className="min-w-0">
                      <div className="space-y-1">
                        <div className="flex flex-wrap items-center gap-x-2 gap-y-1">
                          <span className="inline-flex h-6 items-center gap-1 rounded-md bg-risk-wash px-1.5 text-xs font-semibold text-risk">
                            <FlagIcon aria-hidden className="size-3" />
                            {r.reason}
                          </span>
                          <Dot />
                          <span className="inline-flex items-center gap-1.5" title="Người báo cáo">
                            <span aria-hidden className="size-5 rounded-full bg-line" />
                            <span className="sr-only">Báo cáo bởi</span>
                            <span className="text-[13px] text-ink-2">{r.reporterName}</span>
                          </span>
                          <Dot />
                          <time dateTime={r.createdAt} title={formatFullDateTime(r.createdAt)} className="text-xs text-ink-3">
                            {formatRelative(r.createdAt)}
                          </time>
                        </div>
                        {r.detail && <p className="line-clamp-2 text-[13px] text-ink-2">{`“${r.detail}”`}</p>}
                      </div>

                      <div className="mt-3 flex gap-3">
                        <span
                          aria-hidden
                          className="flex size-10 shrink-0 items-center justify-center rounded-full bg-brand-wash text-sm font-semibold text-brand"
                        >
                          {initials(r.authorName)}
                        </span>
                        <div className="min-w-0 flex-1">
                          <div className="flex flex-wrap items-center gap-x-2 gap-y-0.5">
                            <span className="text-[15px] font-semibold text-ink">{r.authorName}</span>
                            {r.authorIsRestricted && (
                              <Tooltip>
                                <TooltipTrigger render={<span className="inline-flex shrink-0 cursor-default items-center" />}>
                                  <LockIcon aria-hidden className="size-3.5 text-warn" />
                                  <span className="sr-only">
                                    {r.authorRestrictedUntil
                                      ? `Bị hạn chế đến ${formatFullDateTime(r.authorRestrictedUntil)}`
                                      : "Đang bị hạn chế"}
                                  </span>
                                </TooltipTrigger>
                                <TooltipContent>
                                  {r.authorRestrictedUntil
                                    ? `Bị hạn chế đến ${formatFullDateTime(r.authorRestrictedUntil)}`
                                    : "Đang bị hạn chế"}
                                </TooltipContent>
                              </Tooltip>
                            )}
                            <time
                              dateTime={r.messageCreatedAt}
                              title={formatFullDateTime(r.messageCreatedAt)}
                              className="text-xs text-ink-3"
                            >
                              {formatHeaderTime(r.messageCreatedAt)}
                            </time>
                          </div>
                          <p className="max-w-[75ch] whitespace-pre-wrap break-words text-[15px] leading-[1.45] text-ink">
                            {r.messageContent}
                          </p>
                        </div>
                      </div>
                    </div>

                    <div className="flex flex-wrap gap-2 lg:justify-end">
                      <Button
                        size="sm"
                        disabled={busy}
                        onClick={() => void act(r.id, () => adminForumApi.hide(r.id), "Đã ẩn tin")}
                      >
                        <EyeOffIcon data-icon="inline-start" />
                        Ẩn tin
                      </Button>
                      <Button
                        size="sm"
                        variant="ghost"
                        disabled={busy}
                        onClick={() => void act(r.id, () => adminForumApi.dismiss(r.id), "Đã bỏ báo cáo")}
                      >
                        <CheckIcon data-icon="inline-start" />
                        Bỏ qua
                      </Button>
                      {r.authorUserId &&
                        (r.authorIsRestricted ? (
                          <Button
                            size="sm"
                            variant="outline"
                            disabled={busy}
                            onClick={() =>
                              void act(
                                r.id,
                                () => adminForumApi.unrestrict(r.authorUserId!),
                                "Đã gỡ hạn chế cho tác giả",
                              )
                            }
                          >
                            <LockOpenIcon data-icon="inline-start" />
                            Gỡ hạn chế
                          </Button>
                        ) : (
                          <RestrictAuthorForm
                            authorName={r.authorName}
                            disabled={busy}
                            onRestrict={(days, reason) =>
                              act(
                                r.id,
                                () => adminForumApi.restrict(r.authorUserId!, days, reason),
                                "Đã hạn chế tài khoản",
                              )
                            }
                          />
                        ))}
                    </div>
                  </li>
                );
              })}
            </ul>
          )}
        </div>
      </div>
    </AdminSection>
  );
}
