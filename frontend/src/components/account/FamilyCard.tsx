"use client";

import { useEffect, useState } from "react";
import { useAuth } from "@/contexts/AuthContext";
import {
  AlertDialog,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { NativeSelect, NativeSelectOption } from "@/components/ui/native-select";
import { Progress } from "@/components/ui/progress";
import { Spinner } from "@/components/ui/spinner";
import { formatVnDate, formatVnDateTime } from "@/lib/billing-api";
import { familyApi, familyErrorMessage } from "@/lib/family-api";
import { useFetched } from "@/lib/use-fetched";
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

// [UC-126..131] Mục "Gói Gia đình": chỉ hiện khi GET subscription có family (AccountView quyết định).
// Chủ gói: thành viên, chỗ trống, lời mời, mời/gửi lại/hủy/xóa. Thành viên: chỉ xem và Rời gói.
// Chỉ phần trăm (billing-plan-v2 "Percent-only usage"); thành viên không thấy email hay lời mời (máy chủ trả invites rỗng).
export function FamilyCard({ version, onChanged }: { version: number; onChanged: () => void }) {
  const { user } = useAuth();
  const family = useFetched(`family:${user?.id}:${version}`, familyApi.get);
  const f = family.data;
  const now = useNow(5000);

  const [email, setEmail] = useState("");
  const [seatRole, setSeatRole] = useState<SeatRole>("student");
  const [busy, setBusy] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [confirm, setConfirm] = useState<Confirm>(null);

  const openConfirm = (c: Confirm) => {
    setError(null);
    setConfirm(c);
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
      setError(familyErrorMessage(err));
      return false;
    } finally {
      setBusy(null);
    }
  };

  if (!f) {
    return (
      <Card>
        <CardHeader>
          <CardTitle>Gói Gia đình</CardTitle>
        </CardHeader>
        <CardContent>{family.error ? <p className="text-sm text-risk">{family.error}</p> : <Spinner className="text-brand" />}</CardContent>
      </Card>
    );
  }

  const owner = f.isOwner;
  const studentsFull = f.seats.students.used >= f.seats.students.max;
  const parentsFull = f.seats.parents.used >= f.seats.parents.max;
  const effectiveSeat: SeatRole = seatRole === "student" && studentsFull ? "parent" : seatRole === "parent" && parentsFull ? "student" : seatRole;
  const allFull = studentsFull && parentsFull;
  const emptyGroup = owner && f.members.every((m) => m.role === "owner") && f.invites.length === 0;

  const submitInvite = async (e: React.FormEvent) => {
    e.preventDefault();
    const ok = await run("invite", () => familyApi.invite(email.trim(), effectiveSeat), "Đã gửi lời mời");
    if (ok) setEmail("");
  };

  return (
    <Card>
      <CardHeader>
        <CardTitle>Gói Gia đình</CardTitle>
        <CardDescription>{owner ? "Bạn là chủ gói" : `Thành viên gói Gia đình của ${f.ownerName}`}</CardDescription>
      </CardHeader>
      <CardContent className="space-y-5">
        {error && <p className="text-sm text-risk" role="alert">{error}</p>}
        {notice && <p className="text-sm text-ok" role="status">{notice}</p>}

        {f.cycle && (
          <div className="space-y-1 rounded-lg border border-line p-3">
            <div className="flex justify-between text-sm">
              <span className="font-semibold">{f.cycle.planName}</span>
              <span className="tabular-nums text-muted-foreground">Cả gói đã dùng {f.cycle.usedPercent}%</span>
            </div>
            <Progress value={Math.min(100, f.cycle.usedPercent)} aria-label={`Cả gói đã dùng ${f.cycle.usedPercent}%`} />
            {f.cycle.endsAt && <p className="text-xs text-muted-foreground">Hết hạn ngày {formatVnDate(f.cycle.endsAt)}</p>}
          </div>
        )}

        <div className="space-y-2">
          <div className="flex flex-wrap items-center justify-between gap-2">
            <h3 className="font-semibold">Thành viên</h3>
            {owner && (
              <span className="text-xs text-muted-foreground">
                Học sinh {f.seats.students.used}/{f.seats.students.max} · Phụ huynh {f.seats.parents.used}/{f.seats.parents.max}
              </span>
            )}
          </div>
          <ul className="space-y-2">
            {f.members.map((m) => (
              <li key={m.memberId} className={`space-y-1 rounded-lg border p-3 ${m.isMe ? "border-brand/40 bg-brand-wash/40" : "border-line"}`}>
                <div className="flex flex-wrap items-center gap-2">
                  <span className="font-medium">{m.name}</span>
                  {m.isMe && <Badge variant="brand">Bạn</Badge>}
                  <Badge variant="neutral">{ROLE_LABEL[m.role]}</Badge>
                  {owner && m.role !== "owner" && (
                    <Button
                      type="button"
                      variant="ghost"
                      size="sm"
                      className="ml-auto text-destructive"
                      onClick={() => openConfirm({ kind: "remove", memberId: m.memberId, name: m.name })}
                    >
                      Xóa khỏi gói
                    </Button>
                  )}
                </div>
                <div className="flex items-center gap-2">
                  <Progress value={Math.min(100, m.sharePercent)} className="flex-1" aria-label={`${m.name} đã dùng ${m.sharePercent}% của gói`} />
                  <span className="w-12 text-right text-xs tabular-nums text-muted-foreground">{m.sharePercent}%</span>
                </div>
              </li>
            ))}
          </ul>
          {emptyGroup && (
            <p className="text-sm text-muted-foreground">Gói chưa có thành viên. Nếu trước đây bạn đã có gói Gia đình, hãy mời lại thành viên cũ.</p>
          )}
        </div>

        {owner && f.invites.length > 0 && (
          <div className="space-y-2">
            <h3 className="font-semibold">Lời mời đang chờ</h3>
            <ul className="space-y-2">
              {f.invites.map((i) => {
                const waitUntil = i.canResendAt ? new Date(i.canResendAt).getTime() : 0;
                const tooSoon = now === null || now < waitUntil;
                return (
                  <li key={i.inviteId} className="flex flex-wrap items-center gap-2 rounded-lg border border-line p-3 text-sm">
                    <span className="font-medium">{i.email}</span>
                    <Badge variant="neutral">{ROLE_LABEL[i.seatRole]}</Badge>
                    <span className="text-xs text-muted-foreground">Hết hạn {formatVnDateTime(i.expiresAt)}</span>
                    <span className="ml-auto flex gap-1">
                      <Button
                        type="button"
                        variant="outline"
                        size="sm"
                        disabled={busy !== null || tooSoon}
                        title={tooSoon && i.canResendAt ? `Gửi lại được sau ${formatVnDateTime(i.canResendAt)}` : undefined}
                        onClick={() => run(`resend:${i.inviteId}`, () => familyApi.resendInvite(i.inviteId), "Đã gửi lại lời mời")}
                      >
                        Gửi lại
                      </Button>
                      <Button
                        type="button"
                        variant="ghost"
                        size="sm"
                        className="text-destructive"
                        disabled={busy !== null}
                        onClick={() => run(`cancel:${i.inviteId}`, () => familyApi.cancelInvite(i.inviteId), "Đã hủy lời mời")}
                      >
                        Hủy
                      </Button>
                    </span>
                  </li>
                );
              })}
            </ul>
          </div>
        )}

        {owner && (
          <form onSubmit={submitInvite} className="space-y-2">
            <h3 className="font-semibold">Mời thành viên</h3>
            <div className="flex flex-wrap items-end gap-2">
              <div className="min-w-56 flex-1 space-y-1">
                <Label htmlFor="family-invite-email">Email</Label>
                <Input
                  id="family-invite-email"
                  type="email"
                  required
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                  disabled={allFull}
                  placeholder="email@vidu.com"
                />
              </div>
              <div className="space-y-1">
                <Label htmlFor="family-invite-role">Loại chỗ</Label>
                <NativeSelect
                  id="family-invite-role"
                  value={effectiveSeat}
                  onChange={(e) => setSeatRole(e.target.value as SeatRole)}
                  disabled={allFull}
                >
                  <NativeSelectOption value="student" disabled={studentsFull}>
                    Học sinh{studentsFull ? " (đã đủ)" : ""}
                  </NativeSelectOption>
                  <NativeSelectOption value="parent" disabled={parentsFull}>
                    Phụ huynh{parentsFull ? " (đã đủ)" : ""}
                  </NativeSelectOption>
                </NativeSelect>
              </div>
              <Button type="submit" disabled={allFull || busy !== null || !email.trim()}>
                {busy === "invite" ? "Đang gửi..." : "Gửi lời mời"}
              </Button>
            </div>
            <p className="text-xs text-muted-foreground">
              {allFull ? "Gói đã đủ chỗ (tính cả lời mời đang chờ)." : "Lời mời có hạn 7 ngày. Loại tài khoản của người được mời phải khớp loại chỗ."}
            </p>
          </form>
        )}

        {!owner && (
          <Button type="button" variant="outline" className="text-destructive" onClick={() => openConfirm({ kind: "leave" })}>
            Rời gói
          </Button>
        )}
      </CardContent>

      <AlertDialog open={confirm !== null} onOpenChange={(open) => !open && setConfirm(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>{confirm?.kind === "leave" ? "Rời gói Gia đình?" : `Xóa ${confirm?.kind === "remove" ? confirm.name : ""} khỏi gói?`}</AlertDialogTitle>
            <AlertDialogDescription>
              {confirm?.kind === "leave"
                ? "Bạn sẽ không dùng chung lượt hỏi của gói này nữa. Phần đã dùng vẫn được tính vào gói."
                : "Thành viên này sẽ không dùng chung lượt hỏi của gói nữa và chỗ được giải phóng. Phần đã dùng vẫn được tính vào gói."}
            </AlertDialogDescription>
          </AlertDialogHeader>
          {error && <p className="text-sm text-risk" role="alert">{error}</p>}
          <AlertDialogFooter>
            <AlertDialogCancel disabled={busy !== null}>Hủy</AlertDialogCancel>
            <Button
              type="button"
              variant="destructive"
              disabled={busy !== null}
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
    </Card>
  );
}
