"use client";

import React, { useCallback, useEffect, useState } from "react";
import { ShieldCheckIcon, UserPlusIcon } from "lucide-react";
import { adminUsersApi, ApiError } from "@/lib/api";
import { formatUserRole } from "@/lib/user-role-labels";
import type { UserDto, UserRole } from "@/types/api";
import { USER_ROLE_LABELS } from "@/lib/user-role-labels";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Field, FieldDescription, FieldGroup, FieldLabel } from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import { Spinner } from "@/components/ui/spinner";
import { toast } from "@/components/ui/toast";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";

/** Chuẩn hóa SĐT VN (khớp quy tắc backend). */
function normalizeVietnamesePhone(raw: string): { ok: true; value: string } | { ok: false } {
  const cleaned = raw.trim().replace(/[\s.\-()]/g, "");
  let normalized = cleaned;
  if (normalized.startsWith("+84")) normalized = "0" + normalized.slice(3);
  else if (normalized.startsWith("84") && normalized.length === 11) normalized = "0" + normalized.slice(2);
  const regex = /^0(3|5|7|8|9)\d{8}$/;
  return regex.test(normalized) ? { ok: true, value: normalized } : { ok: false };
}

const ACCOUNT_LIST_PAGE_SIZE = 20;

const ACCOUNT_STATUS_OPTIONS = [
  { value: "", label: "Tất cả trạng thái" },
  { value: "active", label: "active" },
  { value: "unverified", label: "unverified" },
  { value: "pending", label: "pending" },
  { value: "temp_locked", label: "temp_locked" },
  { value: "locked", label: "locked" },
] as const;

const filterSelectClass =
  "h-9 w-full min-w-[10rem] rounded-md border border-input bg-background px-3 text-sm shadow-xs outline-none focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/50";

function statusBadgeVariant(status: string): "default" | "secondary" | "destructive" | "outline" {
  if (status === "active") return "default";
  if (status === "unverified" || status === "pending") return "secondary";
  if (status === "locked" || status === "temp_locked") return "destructive";
  return "outline";
}

/** FE-16: màn quản trị người dùng (admin). */
export function AdminUsersView() {
  const [users, setUsers] = useState<UserDto[]>([]);
  const [listLoading, setListLoading] = useState(true);
  const [listError, setListError] = useState<string | null>(null);
  const [searchInput, setSearchInput] = useState("");
  const [searchQuery, setSearchQuery] = useState("");
  const [roleFilter, setRoleFilter] = useState<UserRole | "">("");
  const [statusFilter, setStatusFilter] = useState("");
  const [page, setPage] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const [totalPages, setTotalPages] = useState(0);

  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [fullName, setFullName] = useState("");
  const [phone, setPhone] = useState("");
  const [formError, setFormError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  const fetchUserPage = useCallback(async (activePage: number, signal?: { cancelled: boolean }) => {
    setListError(null);
    setListLoading(true);
    try {
      const data = await adminUsersApi.list({
        search: searchQuery || undefined,
        role: roleFilter || undefined,
        status: statusFilter || undefined,
        page: activePage,
        pageSize: ACCOUNT_LIST_PAGE_SIZE,
      });
      if (signal?.cancelled) return;
      setUsers(data.items);
      setTotalCount(data.totalCount);
      setTotalPages(data.totalPages ?? Math.max(1, Math.ceil(data.totalCount / data.pageSize)));
    } catch (err) {
      if (signal?.cancelled) return;
      setListError(err instanceof ApiError ? err.message : "Không tải được danh sách người dùng.");
    } finally {
      if (!signal?.cancelled) setListLoading(false);
    }
  }, [roleFilter, searchQuery, statusFilter]);

  const loadUsers = useCallback(
    (pageOverride?: number) => fetchUserPage(pageOverride ?? page),
    [fetchUserPage, page],
  );

  /** Tải danh sách khi đổi trang/lọc — setState sau await, tránh rule react-hooks/set-state-in-effect. */
  useEffect(() => {
    const signal = { cancelled: false };
    void (async () => {
      await fetchUserPage(page, signal);
    })();
    return () => {
      signal.cancelled = true;
    };
  }, [fetchUserPage, page]);

  /** Debounce ô tìm — tránh gọi API mỗi ký tự. */
  useEffect(() => {
    const timer = window.setTimeout(() => {
      setSearchQuery(searchInput.trim());
      setPage(1);
    }, 300);
    return () => window.clearTimeout(timer);
  }, [searchInput]);

  const handleCreateModerator = async (e: React.FormEvent) => {
    e.preventDefault();
    setFormError(null);

    const trimmedEmail = email.trim();
    const trimmedName = fullName.trim();

    if (!trimmedName) {
      setFormError("Họ tên không được để trống.");
      return;
    }
    if (trimmedName.length > 128) {
      setFormError("Họ tên tối đa 128 ký tự.");
      return;
    }
    if (!trimmedEmail) {
      setFormError("Vui lòng nhập email.");
      return;
    }
    if (password.length < 8) {
      setFormError("Mật khẩu tối thiểu 8 ký tự.");
      return;
    }

    let phonePayload: string | null = null;
    if (phone.trim()) {
      const parsed = normalizeVietnamesePhone(phone);
      if (!parsed.ok) {
        setFormError("Số điện thoại không đúng định dạng Việt Nam (vd: 0912345678).");
        return;
      }
      phonePayload = parsed.value;
    }

    setIsSubmitting(true);
    try {
      const created = await adminUsersApi.createModerator({
        email: trimmedEmail,
        password,
        fullName: trimmedName,
        phone: phonePayload,
      });
      setEmail("");
      setPassword("");
      setFullName("");
      setPhone("");
      setPage(1);
      await loadUsers(1);
      toast.add({
        type: "success",
        priority: "high",
        timeout: 6000,
        title: "Đã tạo tài khoản moderator",
        description: created.email,
      });
    } catch (err) {
      setFormError(err instanceof ApiError ? err.message : "Không tạo được tài khoản moderator.");
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className="space-y-8">
      <div>
        <h1 className="font-heading text-2xl font-bold tracking-tight text-brand-ink">Quản trị người dùng</h1>
        <p className="mt-1 text-sm text-muted-foreground">
        </p>
      </div>

      {/* [UC-63] Form tạo moderator */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-lg">
            <UserPlusIcon className="size-5 text-brand" aria-hidden />
            Tạo tài khoản moderator
          </CardTitle>
          <CardDescription>
            Tài khoản được kích hoạt ngay. Moderator đăng nhập bằng email và mật khẩu bạn đặt.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <form onSubmit={handleCreateModerator} className="max-w-lg">
            <FieldGroup>
              {formError && (
                <Alert variant="destructive">
                  <AlertDescription>{formError}</AlertDescription>
                </Alert>
              )}
              <Field>
                <FieldLabel htmlFor="mod-fullName">Họ và tên</FieldLabel>
                <Input
                  id="mod-fullName"
                  value={fullName}
                  onChange={(e) => setFullName(e.target.value)}
                  autoComplete="name"
                  required
                />
              </Field>
              <Field>
                <FieldLabel htmlFor="mod-email">Email đăng nhập</FieldLabel>
                <Input
                  id="mod-email"
                  type="email"
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                  autoComplete="off"
                  required
                />
              </Field>
              <Field>
                <FieldLabel htmlFor="mod-password">Mật khẩu</FieldLabel>
                <Input
                  id="mod-password"
                  type="password"
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  autoComplete="new-password"
                  required
                  minLength={8}
                />
                <FieldDescription>Tối thiểu 8 ký tự.</FieldDescription>
              </Field>
              <Field>
                <FieldLabel htmlFor="mod-phone">Số điện thoại (tùy chọn)</FieldLabel>
                <Input
                  id="mod-phone"
                  type="tel"
                  value={phone}
                  onChange={(e) => setPhone(e.target.value)}
                  placeholder="0912345678"
                />
              </Field>
              <Button type="submit" disabled={isSubmitting} className="gap-2">
                {isSubmitting ? <Spinner className="size-4" /> : <ShieldCheckIcon className="size-4" />}
                Tạo moderator
              </Button>
            </FieldGroup>
          </form>
        </CardContent>
      </Card>

      {/* [UC-60] Tìm, lọc, phân trang danh sách tài khoản */}
      <Card>
        <CardHeader>
          <CardTitle className="text-lg">Danh sách tài khoản</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="flex flex-col gap-3 sm:flex-row sm:flex-wrap sm:items-end">
            <Field className="min-w-[12rem] flex-1">
              <FieldLabel htmlFor="user-search">Tìm email hoặc họ tên</FieldLabel>
              <Input
                id="user-search"
                value={searchInput}
                onChange={(e) => setSearchInput(e.target.value)}
                placeholder="vd: nguyen@..."
                autoComplete="off"
              />
            </Field>
            <Field className="w-full sm:w-auto">
              <FieldLabel htmlFor="user-role-filter">Vai trò</FieldLabel>
              <select
                id="user-role-filter"
                className={filterSelectClass}
                value={roleFilter}
                onChange={(e) => {
                  setRoleFilter(e.target.value as UserRole | "");
                  setPage(1);
                }}
              >
                <option value="">Tất cả vai trò</option>
                {(Object.keys(USER_ROLE_LABELS) as UserRole[]).map((role) => (
                  <option key={role} value={role}>
                    {USER_ROLE_LABELS[role]}
                  </option>
                ))}
              </select>
            </Field>
            <Field className="w-full sm:w-auto">
              <FieldLabel htmlFor="user-status-filter">Trạng thái</FieldLabel>
              <select
                id="user-status-filter"
                className={filterSelectClass}
                value={statusFilter}
                onChange={(e) => {
                  setStatusFilter(e.target.value);
                  setPage(1);
                }}
              >
                {ACCOUNT_STATUS_OPTIONS.map((opt) => (
                  <option key={opt.label} value={opt.value}>
                    {opt.label}
                  </option>
                ))}
              </select>
            </Field>
          </div>

          {listLoading ? (
            <div className="flex items-center gap-2 py-8 text-sm text-muted-foreground">
              <Spinner />
              Đang tải...
            </div>
          ) : listError ? (
            <Alert variant="destructive">
              <AlertDescription className="flex flex-wrap items-center gap-3">
                <span>{listError}</span>
                <Button type="button" variant="outline" size="sm" onClick={() => void loadUsers()}>
                  Thử lại
                </Button>
              </AlertDescription>
            </Alert>
          ) : (
            <>
              <p className="text-sm text-muted-foreground">
                {totalCount === 0
                  ? "Không có bản ghi khớp bộ lọc."
                  : `Hiển thị ${(page - 1) * ACCOUNT_LIST_PAGE_SIZE + 1}–${Math.min(page * ACCOUNT_LIST_PAGE_SIZE, totalCount)} / ${totalCount} tài khoản`}
              </p>
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Email</TableHead>
                    <TableHead>Họ tên</TableHead>
                    <TableHead>Vai trò</TableHead>
                    <TableHead>Trạng thái</TableHead>
                    <TableHead>SĐT</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {users.length === 0 ? (
                    <TableRow>
                      <TableCell colSpan={5} className="text-center text-muted-foreground">
                        Không có kết quả.
                      </TableCell>
                    </TableRow>
                  ) : (
                    users.map((u) => (
                      <TableRow key={u.id}>
                        <TableCell className="font-medium">{u.email}</TableCell>
                        <TableCell>{u.fullName}</TableCell>
                        <TableCell>{formatUserRole(u.role)}</TableCell>
                        <TableCell>
                          <Badge variant={statusBadgeVariant(u.status)}>{u.status}</Badge>
                        </TableCell>
                        <TableCell className="text-muted-foreground">{u.phone ?? "—"}</TableCell>
                      </TableRow>
                    ))
                  )}
                </TableBody>
              </Table>
              {totalPages > 1 && (
                <div className="flex flex-wrap items-center justify-end gap-2 pt-2">
                  <Button
                    type="button"
                    variant="outline"
                    size="sm"
                    disabled={page <= 1 || listLoading}
                    onClick={() => setPage((p) => Math.max(1, p - 1))}
                  >
                    Trang trước
                  </Button>
                  <span className="text-sm text-muted-foreground">
                    Trang {page} / {totalPages}
                  </span>
                  <Button
                    type="button"
                    variant="outline"
                    size="sm"
                    disabled={page >= totalPages || listLoading}
                    onClick={() => setPage((p) => p + 1)}
                  >
                    Trang sau
                  </Button>
                </div>
              )}
            </>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
