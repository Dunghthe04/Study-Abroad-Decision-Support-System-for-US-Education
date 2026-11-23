"use client";

import { useEffect, useState } from "react";
import {
  LogOutIcon,
  MailIcon,
  SendIcon,
  Trash2Icon,
  UserPlusIcon,
  XIcon,
} from "lucide-react";
import { useAuth } from "@/contexts/AuthContext";
import { AccountSectionHeader } from "@/components/account/AccountSectionHeader";
import { Alert, AlertDescription } from "@/components/ui/alert";
import {
  AlertDialog,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog";
import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import {
  Empty,
  EmptyContent,
  EmptyDescription,
  EmptyHeader,
  EmptyTitle,
} from "@/components/ui/empty";
import { Field, FieldDescription, FieldLabel } from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import { NativeSelect, NativeSelectOption } from "@/components/ui/native-select";
import { Progress } from "@/components/ui/progress";
import { Skeleton } from "@/components/ui/skeleton";
import { formatVnDateTime } from "@/lib/billing-api";
import { familyApi, familyErrorMessage } from "@/lib/family-api";
import { useFetched } from "@/lib/use-fetched";
import { cn } from "@/lib/utils";
import type { FamilyDto, SeatRole } from "@/types/billing";

const ROLE_LABEL: Record<FamilyDto["members"][number]["role"], string> = {
  owner: "Chủ gói",
  student: "Học sinh",
  parent: "Phụ huynh",
};

type Confirm = { kind: "remove"; memberId: string; name: string } | { kind: "leave" } | null;

/** Giây hiện tại, chỉ đổi trong interval (không gọi Date.now() khi render). */
function useNow(intervalMs: number): number | null {
  const [now, setNow] = useState<number | null>(null);
  useEffect(() => {
    const tick = () => setNow(Date.now());
    const first = setTimeout(tick, 0);
    const id = setInterval(tick, intervalMs);
    return () => {
      clearTimeout(first);
      clearInterval(id);
    };
  }, [intervalMs]);
  return now;
}

// [UC-126..131] Mục "Chia sẻ cho gia đình": chỉ hiện khi GET subscription có family (AccountView quyết định).
// Tối đa 4 thành viên (2 Học sinh + 2 Phụ huynh) dùng chung hạn mức của mọi gói Gia đình của chủ gói. Hạn mức xem ở mục "Hạn mức".
// Chủ gói: thành viên, chỗ trống, lời mời, mời/gửi lại/hủy/xóa. Thành viên: chỉ xem và Rời gói.
export function FamilyCard({ version, onChanged, familyCycleCount }: { version: number; onChanged: () => void; familyCycleCount: number }) {
  const { user } = useAuth();
  const family = useFetched(`family:${user?.id}:${version}`, familyApi.get);
  const f = family.data;
  const now = useNow(5000);

  const [inviteOpen, setInviteOpen] = useState(false);
  const [email, setEmail] = useState("");
  const [seatRole, setSeatRole] = useState<SeatRole>("student");
  const [dialogError, setDialogError] = useState<string | null>(null);
  const [busy, setBusy] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [confirm, setConfirm] = useState<Confirm>(null);

  const openConfirm = (c: Confirm) => {
    setError(null);
    setConfirm(c);
  };

  const openInviteDialog = () => {
    setDialogError(null);
    setInviteOpen(true);
  };

  const run = async (key: string, action: () => Promise<unknown>, success?: string) => {
    setBusy(key);
    setError(null);
    setNotice(null);
    try {
      await action();
      if (success) setNotice(success);
      setConfirm(null);
      onChanged();
      return true;
    } catch (err) {
      const msg = familyErrorMessage(err);
      setError(msg);
      return msg;
    } finally {
      setBusy(null);
    }
  };

  if (!f) {
    return (
      <div>
        <AccountSectionHeader
          title="Chia sẻ cho gia đình"
          description="Mời tối đa 2 Học sinh và 2 Phụ huynh dùng chung hạn mức các gói Gia đình của bạn."
        />
        {family.error ? (
          <Alert variant="destructive">
            <AlertDescription>{family.error}</AlertDescription>
          </Alert>
        ) : (
          <div className="space-y-4">
            <Skeleton className="h-32 w-full rounded-xl" />
            <Skeleton className="h-44 w-full rounded-xl" />
          </div>
        )}
      </div>
    );
  }

  const owner = f.isOwner;
  const studentsFull = f.seats.students.used >= f.seats.students.max;
  const parentsFull = f.seats.parents.used >= f.seats.parents.max;
  const effectiveSeat: SeatRole =
    seatRole === "student" && studentsFull
      ? "parent"
      : seatRole === "parent" && parentsFull
        ? "student"
        : seatRole;
  const allFull = studentsFull && parentsFull;
  const emptyGroup = owner && f.members.every((m) => m.role === "owner") && f.invites.length === 0;

  const submitInvite = async (e: React.FormEvent) => {
    e.preventDefault();
    const trimmed = email.trim();
    if (!trimmed) {
      setDialogError("Vui lòng nhập email thành viên.");
      return;
    }
    setDialogError(null);
    const res = await run("invite", () => familyApi.invite(trimmed, effectiveSeat), "Đã gửi lời mời");
    if (res === true) {
      setEmail("");
      setInviteOpen(false);
    } else {
      setDialogError(res);
    }
  };

  return (
    <div>
      <AccountSectionHeader
        title="Chia sẻ cho gia đình"
        description={
          owner
            ? "Mời tối đa 2 Học sinh và 2 Phụ huynh dùng chung hạn mức tất cả gói Gia đình của bạn."
            : `Bạn đang dùng chung hạn mức các gói Gia đình của ${f.ownerName}.`
        }
        actions={
          owner ? (
            !emptyGroup ? (
              <Button type="button" size="lg" disabled={allFull} onClick={openInviteDialog}>
                <UserPlusIcon data-icon="inline-start" aria-hidden />
                Mời thành viên
              </Button>
            ) : undefined
          ) : (
            <Button
              type="button"
              variant="outline"
              size="lg"
              className="bg-surface text-risk hover:bg-risk-wash hover:text-risk"
              onClick={() => openConfirm({ kind: "leave" })}
            >
              <LogOutIcon data-icon="inline-start" aria-hidden />
              Rời gói
            </Button>
          )
        }
      />

      <div className="space-y-6">
        {error && (
          <Alert variant="destructive">
            <AlertDescription>{error}</AlertDescription>
          </Alert>
        )}
        {notice && (
          <Alert variant="success" role="status">
            <AlertDescription>{notice}</AlertDescription>
          </Alert>
        )}

        {familyCycleCount > 0 && (
          <p className="text-sm text-ink-2">
            {owner ? "Đang chia sẻ" : "Đang dùng chung"} <span className="font-medium text-ink">{familyCycleCount}</span> gói Gia đình
          </p>
        )}

        {/* Danh sách thành viên */}
        <div className="space-y-3">
          <div className="flex flex-wrap items-center justify-between gap-2">
            <h2 className="font-heading text-lg font-semibold text-ink">Thành viên</h2>
            {owner && (
              <span className="text-xs text-ink-2 tabular-nums">
                Học sinh {f.seats.students.used}/{f.seats.students.max} · Phụ huynh{" "}
                {f.seats.parents.used}/{f.seats.parents.max}
              </span>
            )}
          </div>

          <ul className="divide-y divide-line overflow-hidden rounded-xl border border-line bg-surface">
            {f.members.map((m) => (
              <li
                key={m.memberId}
                className={cn(
                  "flex flex-col gap-3 px-5 py-4 sm:flex-row sm:items-center sm:justify-between sm:px-6",
                  m.isMe && "bg-brand-wash/30",
                )}
              >
                <div className="flex min-w-0 items-center gap-3">
                  <Avatar className="size-9 shrink-0">
                    <AvatarFallback className="bg-brand-wash text-xs font-semibold text-brand-ink">
                      {m.name.charAt(0).toUpperCase()}
                    </AvatarFallback>
                  </Avatar>
                  <div className="min-w-0">
                    <div className="flex flex-wrap items-center gap-2">
                      <span className="truncate font-semibold text-ink">{m.name}</span>
                      {m.isMe && <Badge variant="brand">Bạn</Badge>}
                      <Badge variant="neutral">{ROLE_LABEL[m.role]}</Badge>
                    </div>
                  </div>
                </div>

                <div className="flex flex-wrap items-center gap-3 sm:justify-end">
                  <div className="flex min-w-44 flex-1 items-center gap-2.5 sm:flex-initial">
                    <Progress
                      value={Math.min(100, m.sharePercent)}
                      className="flex-1"
                      aria-label={`${m.name} đã dùng ${m.sharePercent}% của gói`}
                    />
                    <span className="w-10 text-right text-xs font-medium text-ink-2 tabular-nums">
                      {m.sharePercent}%
                    </span>
                  </div>
                  {owner && m.role !== "owner" && (
                    <Button
                      type="button"
                      variant="ghost"
                      size="sm"
                      className="text-risk hover:bg-risk-wash hover:text-risk"
                      onClick={() => openConfirm({ kind: "remove", memberId: m.memberId, name: m.name })}
                    >
                      <Trash2Icon data-icon="inline-start" aria-hidden />
                      Xóa khỏi gói
                    </Button>
                  )}
                </div>
              </li>
            ))}
          </ul>

          {emptyGroup && (
            <Empty className="py-12">
              <EmptyHeader>
                <EmptyTitle className="text-base font-semibold text-ink">
                  Chưa có thành viên nào khác
                </EmptyTitle>
                <EmptyDescription>
                  Mời người thân để cùng dùng chung hạn mức các gói Gia đình của bạn.
                </EmptyDescription>
              </EmptyHeader>
              <EmptyContent>
                <Button type="button" size="lg" disabled={allFull} onClick={openInviteDialog}>
                  <UserPlusIcon data-icon="inline-start" aria-hidden />
                  Mời thành viên
                </Button>
              </EmptyContent>
            </Empty>
          )}
        </div>

        {/* Lời mời đang chờ */}
        {owner && f.invites.length > 0 && (
          <div className="space-y-3">
            <h2 className="font-heading text-lg font-semibold text-ink">Lời mời đang chờ</h2>
            <ul className="divide-y divide-line overflow-hidden rounded-xl border border-line bg-surface">
              {f.invites.map((i) => {
                const waitUntil = i.canResendAt ? new Date(i.canResendAt).getTime() : 0;
                const tooSoon = now === null || now < waitUntil;
                return (
                  <li
                    key={i.inviteId}
                    className="flex flex-wrap items-center justify-between gap-3 px-5 py-4 text-sm sm:px-6"
                  >
                    <div className="min-w-0 space-y-0.5">
                      <div className="flex flex-wrap items-center gap-2">
                        <span className="truncate font-medium text-ink">{i.email}</span>
                        <Badge variant="neutral">{ROLE_LABEL[i.seatRole]}</Badge>
                      </div>
                      <p className="text-xs text-ink-2">Hết hạn {formatVnDateTime(i.expiresAt)}</p>
                    </div>
                    <div className="flex shrink-0 items-center gap-2">
                      <Button
                        type="button"
                        variant="outline"
                        size="sm"
                        disabled={busy !== null || tooSoon}
                        title={
                          tooSoon && i.canResendAt
                            ? `Gửi lại được sau ${formatVnDateTime(i.canResendAt)}`
                            : undefined
                        }
                        onClick={() =>
                          run(`resend:${i.inviteId}`, () => familyApi.resendInvite(i.inviteId), "Đã gửi lại lời mời")
                        }
                        loading={busy === `resend:${i.inviteId}`}
                      >
                        <MailIcon data-icon="inline-start" aria-hidden />
                        Gửi lại
                      </Button>
                      <Button
                        type="button"
                        variant="ghost"
                        size="sm"
                        className="text-risk hover:bg-risk-wash hover:text-risk"
                        disabled={busy !== null}
                        onClick={() =>
                          run(`cancel:${i.inviteId}`, () => familyApi.cancelInvite(i.inviteId), "Đã hủy lời mời")
                        }
                      >
                        <XIcon data-icon="inline-start" aria-hidden />
                        Hủy
                      </Button>
                    </div>
                  </li>
                );
              })}
            </ul>
          </div>
        )}
      </div>

      {/* Hộp thoại mời thành viên */}
      <Dialog
        open={inviteOpen}
        onOpenChange={(open) => {
          if (busy === "invite") return;
          setInviteOpen(open);
          if (!open) setDialogError(null);
        }}
      >
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>Mời thành viên vào gói Gia đình</DialogTitle>
            <DialogDescription>
              Lời mời có hiệu lực 7 ngày. Loại tài khoản của người được mời phải khớp với loại chỗ đã chọn. Gói Gia đình có tối đa 2 Học sinh và 2 Phụ huynh.
            </DialogDescription>
          </DialogHeader>

          <form onSubmit={submitInvite} noValidate className="space-y-4">
            {dialogError && (
              <Alert variant="destructive">
                <AlertDescription>{dialogError}</AlertDescription>
              </Alert>
            )}

            <Field>
              <FieldLabel htmlFor="family-invite-email">Email thành viên</FieldLabel>
              <Input
                id="family-invite-email"
                type="email"
                required
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                disabled={allFull || busy === "invite"}
                placeholder="email@vidu.com"
                className="h-10"
              />
            </Field>

            <Field>
              <FieldLabel htmlFor="family-invite-role">Loại chỗ</FieldLabel>
              <NativeSelect
                id="family-invite-role"
                value={effectiveSeat}
                onChange={(e) => setSeatRole(e.target.value as SeatRole)}
                disabled={allFull || busy === "invite"}
                className="h-10 w-full"
              >
                <NativeSelectOption value="student" disabled={studentsFull}>
                  Học sinh{studentsFull ? " (đã đủ)" : ""}
                </NativeSelectOption>
                <NativeSelectOption value="parent" disabled={parentsFull}>
                  Phụ huynh{parentsFull ? " (đã đủ)" : ""}
                </NativeSelectOption>
              </NativeSelect>
              <FieldDescription>
                {allFull
                  ? "Gói đã đủ chỗ (tính cả lời mời đang chờ)."
                  : `Còn ${f.seats.students.max - f.seats.students.used} chỗ Học sinh · ${f.seats.parents.max - f.seats.parents.used} chỗ Phụ huynh.`}
              </FieldDescription>
            </Field>

            <DialogFooter>
              <Button
                type="button"
                variant="outline"
                onClick={() => setInviteOpen(false)}
                disabled={busy === "invite"}
              >
                Hủy
              </Button>
              <Button
                type="submit"
                disabled={allFull || busy !== null || !email.trim()}
                loading={busy === "invite"}
              >
                <SendIcon data-icon="inline-start" aria-hidden />
                {busy === "invite" ? "Đang gửi..." : "Gửi lời mời"}
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>

      <AlertDialog open={confirm !== null} onOpenChange={(open) => !open && setConfirm(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>
              {confirm?.kind === "leave"
                ? "Rời gói Gia đình?"
                : `Xóa ${confirm?.kind === "remove" ? confirm.name : ""} khỏi gói?`}
            </AlertDialogTitle>
            <AlertDialogDescription>
              {confirm?.kind === "leave"
                ? "Bạn sẽ không dùng chung lượt hỏi của các gói Gia đình này nữa. Phần đã dùng vẫn được tính vào gói."
                : "Thành viên này sẽ không dùng chung lượt hỏi của các gói Gia đình nữa và chỗ được giải phóng. Phần đã dùng vẫn được tính vào gói."}
            </AlertDialogDescription>
          </AlertDialogHeader>
          {error && <p className="text-sm text-risk" role="alert">{error}</p>}
          <AlertDialogFooter>
            <AlertDialogCancel disabled={busy !== null}>Hủy</AlertDialogCancel>
            <Button
              type="button"
              variant="destructive"
              disabled={busy !== null}
              loading={busy === "leave" || (confirm?.kind === "remove" && busy === `remove:${confirm.memberId}`)}
              onClick={() => {
                if (confirm?.kind === "leave") run("leave", familyApi.leave, "Bạn đã rời gói Gia đình");
                else if (confirm?.kind === "remove") {
                  const id = confirm.memberId;
                  run(`remove:${id}`, () => familyApi.removeMember(id), "Đã xóa thành viên khỏi gói");
                }
              }}
            >
              {confirm?.kind === "leave" ? "Rời gói" : "Xóa"}
            </Button>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}
