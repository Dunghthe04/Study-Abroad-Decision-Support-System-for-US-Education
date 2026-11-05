"use client";

import React, { useCallback, useEffect, useState } from "react";
import { CheckIcon, HourglassIcon, MailIcon, UsersIcon } from "lucide-react";
import { useAuth } from "@/contexts/AuthContext";
import { parentLinksApi, ApiError } from "@/lib/api";
import type { ParentLinkDto } from "@/types/api";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Field, FieldDescription, FieldGroup, FieldLabel } from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import { Spinner } from "@/components/ui/spinner";

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
  const [newParentEmail, setNewParentEmail] = useState("");
  const [revokeTarget, setRevokeTarget] = useState<ParentLinkDto | null>(null);

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
      setError(errorMessage(err, fallback));
      return false;
    } finally {
      setBusy(null);
    }
  };

  const handleChangeEmail = async (e: React.FormEvent) => {
    e.preventDefault();
    const email = newParentEmail.trim();
    if (!email) {
      setError("Vui lòng nhập email phụ huynh.");
      return;
    }
    const ok = await run("change", () => parentLinksApi.changeParentEmail(email), "Không thể gửi lời mời.");
    if (ok) setNewParentEmail("");
  };

  const handleRevoke = async () => {
    if (!revokeTarget) return;
    const target = revokeTarget;
    setRevokeTarget(null);
    await run(`revoke-${target.id}`, () => parentLinksApi.revoke(target.id), "Không thể hủy liên kết.");
  };

  if (!user || (user.role !== "student" && user.role !== "parent")) return null;

  const hasPending = links.some((l) => l.status === "pending");

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <UsersIcon className="size-5" aria-hidden />
          <h2>{isStudent ? "Phụ huynh liên kết" : "Con đã liên kết"}</h2>
        </CardTitle>
        <CardDescription>
          {isStudent
            ? "Học sinh dưới 16 tuổi cần phụ huynh xác nhận đồng ý trước khi hệ thống lưu và phân tích hồ sơ (Luật BVDLCN 91/2025, Điều 24)."
            : "Bạn có thể rút lại sự đồng ý bất cứ lúc nào; hệ thống sẽ ngừng xử lý dữ liệu của con dưới 16 tuổi."}
        </CardDescription>
      </CardHeader>

      <CardContent className="space-y-4">
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
          <div className="flex items-center gap-2 text-sm text-muted-foreground">
            <Spinner />
            Đang tải...
          </div>
        ) : links.length === 0 ? (
          <p className="text-sm italic text-muted-foreground">
            {isStudent ? "Chưa có phụ huynh nào liên kết." : "Chưa có học sinh nào liên kết với tài khoản này."}
          </p>
        ) : (
          <ul className="space-y-3">
            {links.map((link) => (
              <li key={link.id}>
                <Card size="sm">
                  <CardContent className="flex flex-wrap items-center justify-between gap-3">
                    <div className="min-w-0">
                      <div className="font-semibold">
                        {isStudent
                          ? link.parentName ?? link.parentEmail
                          : `${link.studentName}${link.studentAge != null ? ` (${link.studentAge} tuổi)` : ""}`}
                      </div>
                      {isStudent && <div className="truncate text-body-s">{link.parentEmail}</div>}
                      {link.status === "active" && link.acceptedAt && (
                        <div className="text-body-s">Đồng ý ngày {new Date(link.acceptedAt).toLocaleDateString("vi-VN")}</div>
                      )}
                      {link.status === "pending" && link.tokenExpiresAt && (
                        <div className="text-body-s">Lời mời hết hạn {new Date(link.tokenExpiresAt).toLocaleString("vi-VN")}</div>
                      )}
                    </div>
                    <div className="flex flex-wrap items-center gap-2">
                      {link.status === "active" ? (
                        <Badge variant="ok"><CheckIcon aria-hidden />Đã đồng ý</Badge>
                      ) : (
                        <Badge variant="warn"><HourglassIcon aria-hidden />Chờ phụ huynh xác nhận</Badge>
                      )}
                      {isStudent && link.status === "pending" && (
                        <Button
                          type="button"
                          variant="outline"
                          size="sm"
                          disabled={busy !== null}
                          onClick={() => run("resend", () => parentLinksApi.resend(), "Không thể gửi lại lời mời.")}
                        >
                          {busy === "resend" ? <Spinner data-icon="inline-start" /> : <MailIcon aria-hidden />}
                          Gửi lại
                        </Button>
                      )}
                      {link.canRevoke && (
                        <Button
                          type="button"
                          variant="ghost"
                          size="sm"
                          disabled={busy !== null}
                          onClick={() => setRevokeTarget(link)}
                        >
                          {link.status === "pending" ? "Hủy lời mời" : isStudent ? "Hủy liên kết" : "Rút đồng ý"}
                        </Button>
                      )}
                    </div>
                  </CardContent>
                </Card>
                {isStudent && link.status === "active" && !link.canRevoke && (
                  <p className="mt-1 text-body-s">Học sinh dưới 16 tuổi không tự hủy được liên kết; phụ huynh có thể rút đồng ý.</p>
                )}
              </li>
            ))}
          </ul>
        )}

        {isStudent && (
          <form onSubmit={handleChangeEmail}>
            <FieldGroup>
              <Field>
                <FieldLabel htmlFor="parent-email-input">
                  {hasPending ? "Đổi email phụ huynh" : "Mời phụ huynh liên kết"}
                </FieldLabel>
                <div className="flex flex-col gap-2 sm:flex-row">
                  <Input
                    id="parent-email-input"
                    type="email"
                    maxLength={100}
                    value={newParentEmail}
                    onChange={(e) => setNewParentEmail(e.target.value)}
                    placeholder="phuhuynh@gmail.com"
                  />
                  <Button type="submit" disabled={busy !== null}>
                    {busy === "change" && <Spinner data-icon="inline-start" />}
                    Gửi lời mời
                  </Button>
                </div>
                <FieldDescription>
                  {hasPending
                    ? "Lời mời đang chờ sẽ bị hủy và gửi lời mời mới tới email này."
                    : "Phụ huynh nhận mail có link xác nhận, hiệu lực 7 ngày."}
                </FieldDescription>
              </Field>
            </FieldGroup>
          </form>
        )}
      </CardContent>

      <Dialog open={revokeTarget !== null} onOpenChange={(open) => !open && setRevokeTarget(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>
              {revokeTarget?.status === "pending" ? "Hủy lời mời?" : isStudent ? "Hủy liên kết phụ huynh?" : "Rút lại sự đồng ý?"}
            </DialogTitle>
          </DialogHeader>
          <DialogDescription>
            {revokeTarget?.status === "pending"
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
    </Card>
  );
}
