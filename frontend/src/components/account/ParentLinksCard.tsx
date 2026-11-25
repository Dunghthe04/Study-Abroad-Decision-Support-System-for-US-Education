"use client";

import React, { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import {
  ArrowRightLeftIcon,
  CheckIcon,
  HourglassIcon,
  MailIcon,
  SendIcon,
  UnlinkIcon,
  Trash2Icon,
  UserPlusIcon,
  XIcon,
} from "lucide-react";
import { useAuth } from "@/contexts/AuthContext";
import { parentLinksApi, ApiError } from "@/lib/api";
import type { ParentLinkDto } from "@/types/api";
import { parentChildrenApi } from "@/lib/parent-children-api";
import { useFetched } from "@/lib/use-fetched";
import { AccountSectionHeader } from "@/components/account/AccountSectionHeader";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import { Badge } from "@/components/ui/badge";
import { Button, buttonVariants } from "@/components/ui/button";
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
import { Checkbox } from "@/components/ui/checkbox";
import { Label } from "@/components/ui/label";
import { Input } from "@/components/ui/input";
import { Skeleton } from "@/components/ui/skeleton";

function errorMessage(err: unknown, fallback: string) {
  if (err instanceof ApiError) return err.message || fallback;
  return "Có lỗi kết nối máy chủ. Vui lòng thử lại sau.";
}

// [Q11] Học sinh: phụ huynh đã liên kết / lời mời đang chờ (gửi lại, đổi email, hủy).
// Phụ huynh: các con đã liên kết và rút đồng ý. Quyền rút do server tính (canRevoke).
export function ParentLinksCard() {
  const { user } = useAuth();
  const isStudent = user?.role === "student";

  const [links, setLinks] = useState<ParentLinkDto[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);
  const [busy, setBusy] = useState<string | null>(null);
  const [inviteOpen, setInviteOpen] = useState(false);
  const [newParentEmail, setNewParentEmail] = useState("");
  const [dialogError, setDialogError] = useState<string | null>(null);
  const [revokeTarget, setRevokeTarget] = useState<ParentLinkDto | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<ParentLinkDto | null>(null);
  // Giữ tên con cho tiêu đề khi hộp thoại đang đóng dần (deleteTarget đã về null)
  const [deleteName, setDeleteName] = useState("");
  const [deleteUnderstood, setDeleteUnderstood] = useState(false);

  // UC-76: nút So sánh ưu tiên chỉ cho con có tài khoản thật (getMine có thể trả cả con managed)
  const { data: children } = useFetched(isStudent ? "no-children" : "parent-children", () =>
    isStudent ? Promise.resolve([]) : parentChildrenApi.listChildren(),
  );
  const realChildIds = new Set((children ?? []).filter((c) => !c.isManaged).map((c) => c.studentUserId));

  const load = useCallback(
    () =>
      parentLinksApi
        .getMine()
        .then((res) => setLinks(res.data))
        .catch((err) => setError(errorMessage(err, "Không tải được danh sách liên kết.")))
        .finally(() => setIsLoading(false)),
    [],
  );

  useEffect(() => {
    load();
  }, [load]);

  const run = async (key: string, action: () => Promise<{ message: string | null }>, fallback: string) => {
    setError(null);
    setSuccessMessage(null);
    setBusy(key);
    try {
      const res = await action();
      setSuccessMessage(res.message || "Đã cập nhật.");
      await load();
      return true;
    } catch (err) {
      const msg = errorMessage(err, fallback);
      setError(msg);
      return msg;
    } finally {
      setBusy(null);
    }
  };

  const openInviteDialog = () => {
    setDialogError(null);
    setInviteOpen(true);
  };

  const handleChangeEmail = async (e: React.FormEvent) => {
    e.preventDefault();
    const email = newParentEmail.trim();
    if (!email) {
      setDialogError("Vui lòng nhập email phụ huynh.");
      return;
    }
    setDialogError(null);
    const res = await run("change", () => parentLinksApi.changeParentEmail(email), "Không thể gửi lời mời.");
    if (res === true) {
      setNewParentEmail("");
      setInviteOpen(false);
    } else {
      setDialogError(res);
    }
  };

  const handleRevoke = async () => {
    if (!revokeTarget) return;
    const target = revokeTarget;
    setRevokeTarget(null);
    await run(`revoke-${target.id}`, () => parentLinksApi.revoke(target.id), "Không thể hủy liên kết.");
  };

  // [UC-75 1a] Rút đồng ý kèm xóa tài khoản con dưới 16 (chưa biết tuổi coi như dưới 16, máy chủ kiểm lại)
  const handleRevokeAndDelete = async () => {
    if (!deleteTarget) return;
    const target = deleteTarget;
    setDeleteTarget(null);
    setDeleteUnderstood(false);
    await run(`delete-${target.id}`, () => parentLinksApi.revokeAndDelete(target.id), "Không thể xóa tài khoản của con.");
  };

  if (!user || (user.role !== "student" && user.role !== "parent")) return null;

  // [UC-115] Lời mời chưa thành liên kết: đang chờ, hết hạn hoặc phụ huynh từ chối (gửi lại / đổi email đưa về chờ)
  const isOpenInvite = (l: ParentLinkDto) => l.status === "pending" || l.status === "expired" || l.status === "rejected";
  const hasPending = links.some(isOpenInvite);

  return (
    <div>
      <AccountSectionHeader
        title={isStudent ? "Phụ huynh liên kết" : "Con đã liên kết"}
        description={
          isStudent
            ? "Học sinh dưới 16 tuổi cần phụ huynh xác nhận đồng ý trước khi hệ thống lưu và phân tích hồ sơ (Luật BVDLCN 91/2025, Điều 24)."
            : "Bạn có thể rút lại sự đồng ý bất cứ lúc nào; hệ thống sẽ ngừng xử lý dữ liệu của con dưới 16 tuổi."
        }
        actions={
          isStudent ? (
            !isLoading && links.length > 0 ? (
              <Button type="button" size="lg" onClick={openInviteDialog}>
                <UserPlusIcon data-icon="inline-start" aria-hidden />
                {hasPending ? "Đổi email phụ huynh" : "Mời phụ huynh"}
              </Button>
            ) : undefined
          ) : !isLoading && links.length > 0 ? (
            <Link
              href="/parent/children/add"
              className={buttonVariants({ variant: "outline", size: "lg", className: "bg-surface" })}
            >
              <UserPlusIcon data-icon="inline-start" aria-hidden />
              Thêm con
            </Link>
          ) : undefined
        }
      />

      <div className="space-y-4">
        {error && (
          <Alert variant="destructive">
            <AlertDescription>{error}</AlertDescription>
          </Alert>
        )}
        {successMessage && (
          <Alert variant="success" role="status">
            <AlertDescription>{successMessage}</AlertDescription>
          </Alert>
        )}

        {isLoading ? (
          <div className="space-y-3">
            <Skeleton className="h-20 w-full rounded-xl" />
            <Skeleton className="h-20 w-full rounded-xl" />
          </div>
        ) : links.length === 0 ? (
          <Empty className="py-16">
            <EmptyHeader>
              <EmptyTitle className="text-base font-semibold text-ink">
                {isStudent ? "Chưa có phụ huynh nào liên kết" : "Chưa có học sinh nào liên kết"}
              </EmptyTitle>
              <EmptyDescription>
                {isStudent
                  ? "Gửi lời mời qua email để phụ huynh xác nhận đồng ý và đồng hành cùng hồ sơ du học của bạn."
                  : "Khi con gửi lời mời liên kết qua email hoặc bạn thêm hồ sơ con, danh sách sẽ hiển thị tại đây."}
              </EmptyDescription>
            </EmptyHeader>
            <EmptyContent>
              {isStudent ? (
                <Button type="button" size="lg" onClick={openInviteDialog}>
                  <SendIcon data-icon="inline-start" aria-hidden />
                  Mời phụ huynh liên kết
                </Button>
              ) : (
                <Link href="/parent/children/add" className={buttonVariants({ size: "lg" })}>
                  <UserPlusIcon data-icon="inline-start" aria-hidden />
                  Thêm hồ sơ con
                </Link>
              )}
            </EmptyContent>
          </Empty>
        ) : (
          <ul className="divide-y divide-line overflow-hidden rounded-xl border border-line bg-surface">
            {links.map((link) => {
              const displayName = isStudent
                ? link.parentName ?? link.parentEmail
                : `${link.studentName}${link.studentAge != null ? ` (${link.studentAge} tuổi)` : ""}`;
              const initial = displayName.charAt(0).toUpperCase();

              return (
                <li key={link.id} className="px-5 py-4 sm:px-6">
                  <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
                    <div className="flex min-w-0 items-center gap-3.5">
                      <Avatar className="size-10 shrink-0">
                        <AvatarFallback className="bg-brand-wash text-sm font-semibold text-brand-ink">
                          {initial}
                        </AvatarFallback>
                      </Avatar>
                      <div className="min-w-0 space-y-0.5">
                        <div className="truncate font-semibold text-ink">{displayName}</div>
                        {isStudent && link.parentName && (
                          <div className="truncate text-xs text-ink-2">{link.parentEmail}</div>
                        )}
                        {link.status === "active" && link.acceptedAt && (
                          <div className="text-xs text-ink-3">
                            Đồng ý ngày {new Date(link.acceptedAt).toLocaleDateString("vi-VN")}
                          </div>
                        )}
                        {link.status === "revoked" && (
                          <div className="text-xs text-ink-3">
                            Phụ huynh đã rút đồng ý. Gửi lại yêu cầu để phụ huynh xác nhận qua email mới.
                          </div>
                        )}
                        {link.status === "pending" && link.tokenExpiresAt && (
                          <div className="text-xs text-ink-3">
                            Lời mời hết hạn {new Date(link.tokenExpiresAt).toLocaleString("vi-VN")}
                          </div>
                        )}
                      </div>
                    </div>

                    <div className="flex flex-wrap items-center gap-2">
                      {link.status === "active" ? (
                        <Badge variant="ok"><CheckIcon aria-hidden />Đã đồng ý</Badge>
                      ) : link.status === "rejected" ? (
                        <Badge variant="warn"><XIcon aria-hidden />Phụ huynh đã từ chối</Badge>
                      ) : link.status === "expired" ? (
                        <Badge variant="warn"><HourglassIcon aria-hidden />Lời mời hết hạn</Badge>
                      ) : link.status === "revoked" ? (
                        <Badge variant="warn"><UnlinkIcon aria-hidden />Đã thu hồi</Badge>
                      ) : (
                        <Badge variant="warn"><HourglassIcon aria-hidden />Chờ phụ huynh xác nhận</Badge>
                      )}

                      {isStudent && isOpenInvite(link) && (
                        <Button
                          type="button"
                          variant="outline"
                          size="sm"
                          disabled={busy !== null}
                          onClick={() => run("resend", () => parentLinksApi.resend(), "Không thể gửi lại lời mời.")}
                          loading={busy === "resend"}
                        >
                          <MailIcon data-icon="inline-start" aria-hidden />
                          Gửi lại
                        </Button>
                      )}

                      {/* [UC-111] Phụ huynh đã rút đồng ý: gửi yêu cầu mới tới cùng email (link cũ không dùng lại) */}
                      {isStudent && link.status === "revoked" && (
                        <Button
                          type="button"
                          variant="outline"
                          size="sm"
                          disabled={busy !== null}
                          onClick={() =>
                            run("reinvite", () => parentLinksApi.changeParentEmail(link.parentEmail), "Không thể gửi lại yêu cầu.")
                          }
                          loading={busy === "reinvite"}
                        >
                          <MailIcon data-icon="inline-start" aria-hidden />
                          Gửi lại yêu cầu
                        </Button>
                      )}

                      {!isStudent && link.status === "active" && realChildIds.has(link.studentUserId) && (
                        <Link
                          href={`/parent/children/${encodeURIComponent(link.studentUserId)}?tab=preference`}
                          className={buttonVariants({ variant: "outline", size: "sm" })}
                        >
                          <ArrowRightLeftIcon data-icon="inline-start" aria-hidden />
                          So sánh ưu tiên
                        </Link>
                      )}

                      {link.canRevoke && (
                        <Button
                          type="button"
                          variant="ghost"
                          size="sm"
                          className="text-risk hover:bg-risk-wash hover:text-risk"
                          disabled={busy !== null}
                          onClick={() => setRevokeTarget(link)}
                        >
                          <UnlinkIcon data-icon="inline-start" aria-hidden />
                          {link.status !== "active" ? "Hủy lời mời" : isStudent ? "Hủy liên kết" : "Rút đồng ý"}
                        </Button>
                      )}

                      {!isStudent && link.status === "active" && (link.studentAge == null || link.studentAge < 16) && (
                        <Button
                          type="button"
                          variant="ghost"
                          size="sm"
                          className="text-risk hover:bg-risk-wash hover:text-risk"
                          disabled={busy !== null}
                          onClick={() => {
                            setDeleteUnderstood(false);
                            setDeleteName(link.studentName);
                            setDeleteTarget(link);
                          }}
                          loading={busy === `delete-${link.id}`}
                        >
                          <Trash2Icon data-icon="inline-start" aria-hidden />
                          Rút đồng ý và xóa tài khoản
                        </Button>
                      )}
                    </div>
                  </div>

                  {isStudent && link.status === "active" && !link.canRevoke && (
                    <p className="mt-2 text-xs text-ink-3 sm:pl-13.5">
                      Học sinh dưới 16 tuổi không tự hủy được liên kết; phụ huynh có thể rút đồng ý.
                    </p>
                  )}
                </li>
              );
            })}
          </ul>
        )}
      </div>

      {/* Hộp thoại mời / đổi email phụ huynh */}
      <Dialog
        open={inviteOpen}
        onOpenChange={(open) => {
          if (busy === "change") return;
          setInviteOpen(open);
          if (!open) setDialogError(null);
        }}
      >
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>{hasPending ? "Đổi email phụ huynh" : "Mời phụ huynh liên kết"}</DialogTitle>
            <DialogDescription>
              {hasPending
                ? "Lời mời cũ sẽ bị hủy và hệ thống gửi lời mời mới tới email này."
                : "Phụ huynh sẽ nhận được email chứa liên kết xác nhận, có hiệu lực trong 7 ngày."}
            </DialogDescription>
          </DialogHeader>

          <form onSubmit={handleChangeEmail} noValidate className="space-y-4">
            {dialogError && (
              <Alert variant="destructive">
                <AlertDescription>{dialogError}</AlertDescription>
              </Alert>
            )}

            <Field>
              <FieldLabel htmlFor="parent-email-input">Email phụ huynh</FieldLabel>
              <Input
                id="parent-email-input"
                type="email"
                required
                maxLength={100}
                value={newParentEmail}
                onChange={(e) => setNewParentEmail(e.target.value)}
                placeholder="phuhuynh@gmail.com"
                disabled={busy === "change"}
                className="h-10"
              />
              <FieldDescription>
                Nhập đúng địa chỉ email phụ huynh đang sử dụng để nhận thư xác nhận.
              </FieldDescription>
            </Field>

            <DialogFooter>
              <Button
                type="button"
                variant="outline"
                onClick={() => setInviteOpen(false)}
                disabled={busy === "change"}
              >
                Hủy
              </Button>
              <Button type="submit" disabled={busy !== null} loading={busy === "change"}>
                <SendIcon data-icon="inline-start" aria-hidden />
                {busy === "change" ? "Đang gửi..." : "Gửi lời mời"}
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>

      {/* Hộp thoại xác nhận hủy liên kết / rút đồng ý */}
      <Dialog open={revokeTarget !== null} onOpenChange={(open) => !open && setRevokeTarget(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>
              {revokeTarget && revokeTarget.status !== "active"
                ? "Hủy lời mời?"
                : isStudent
                  ? "Hủy liên kết phụ huynh?"
                  : "Rút lại sự đồng ý?"}
            </DialogTitle>
          </DialogHeader>
          <DialogDescription>
            {revokeTarget && revokeTarget.status !== "active"
              ? "Link trong mail mời sẽ không dùng được nữa."
              : isStudent
                ? "Phụ huynh sẽ không còn liên kết với tài khoản của bạn và sẽ nhận email thông báo."
                : "Hệ thống sẽ ngừng xử lý dữ liệu của con; con dưới 16 tuổi quay về chế độ hạn chế cho tới khi có đồng ý mới."}
          </DialogDescription>
          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => setRevokeTarget(null)}>
              Không
            </Button>
            <Button type="button" variant="destructive" onClick={handleRevoke}>
              Xác nhận
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* [UC-75 1a] Hộp thoại xác nhận rút đồng ý kèm xóa tài khoản con dưới 16 */}
      <Dialog
        open={deleteTarget !== null}
        onOpenChange={(open) => {
          if (!open) {
            setDeleteTarget(null);
            setDeleteUnderstood(false);
          }
        }}
      >
        <DialogContent>
          <DialogHeader>
            <DialogTitle className="pr-6">Rút đồng ý và xóa tài khoản của {deleteName}?</DialogTitle>
          </DialogHeader>
          <DialogDescription>Việc này khác với chỉ rút đồng ý:</DialogDescription>
          <ul className="list-disc space-y-1 pl-5 text-sm text-ink-2">
            <li>Hệ thống ngừng xử lý dữ liệu và rút mọi đồng ý của phụ huynh cho con.</li>
            <li>Tài khoản của con bị khóa ngay và xóa vĩnh viễn sau 30 ngày. Con không tự khôi phục được khi chưa đủ 16 tuổi.</li>
            <li>Bạn nhận email có nút &quot;Hủy xóa và đồng ý lại cho con&quot; và &quot;Chỉ hủy xóa&quot;, dùng được tới ngày xóa vĩnh viễn.</li>
          </ul>
          <div className="flex items-start gap-2">
            <Checkbox
              id="parent-delete-child-understood"
              checked={deleteUnderstood}
              onCheckedChange={(checked) => setDeleteUnderstood(Boolean(checked))}
            />
            <Label htmlFor="parent-delete-child-understood" className="leading-snug text-ink">
              Tôi hiểu tài khoản và dữ liệu của con sẽ bị xóa vĩnh viễn nếu không hủy trong 30 ngày.
            </Label>
          </div>
          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => setDeleteTarget(null)}>
              Không
            </Button>
            <Button type="button" variant="destructive" disabled={!deleteUnderstood} onClick={handleRevokeAndDelete}>
              Rút đồng ý và xóa
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
