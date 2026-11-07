"use client";

import React, { useCallback, useEffect, useState } from "react";
import {
  BanIcon,
  CheckCircle2Icon,
  ClockIcon,
  EyeIcon,
  FilterIcon,
  KeyRoundIcon,
  RotateCcwIcon,
  SearchIcon,
  ShieldAlertIcon,
  ShieldCheckIcon,
  UserCheckIcon,
  UserPlusIcon,
  UsersIcon,
} from "lucide-react";
import { adminUsersApi, ApiError } from "@/lib/api";
import { useAuth } from "@/contexts/AuthContext";
import { formatUserRole, USER_ROLE_LABELS } from "@/lib/user-role-labels";
import type { CreateCenterAccountRequest, CreateModeratorAccountRequest, UserDto, UserRole } from "@/types/api";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Field, FieldDescription, FieldGroup, FieldLabel } from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import { Spinner } from "@/components/ui/spinner";
import { toast } from "@/components/ui/toast";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
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
  { value: "active", label: "Đang hoạt động" },
  { value: "unverified", label: "Chưa kích hoạt" },
  { value: "pending", label: "Chờ duyệt" },
  { value: "temp_locked", label: "Tạm khóa" },
  { value: "locked", label: "Đã khóa" },
] as const;

const filterSelectClass =
  "h-9 w-full min-w-[10rem] rounded-md border border-input bg-background px-3 text-sm shadow-xs outline-none focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/50";

/** [UC-61] Chỉ Admin mới có quyền khóa tài khoản (moderator không được phép). */
function canAdminLock(target: UserDto, currentUserRole?: string, currentUserId?: string): boolean {
  if (currentUserRole !== "admin") return false;
  if (target.role === "admin") return false;
  if (currentUserId && target.id === currentUserId) return false;
  return target.status !== "locked";
}

/** [UC-62] Chỉ Admin mới có quyền mở khóa tài khoản (moderator không được phép). */
function canAdminUnlock(target: UserDto, currentUserRole?: string): boolean {
  if (currentUserRole !== "admin") return false;
  if (target.role === "admin") return false;
  return target.status === "locked" || target.status === "temp_locked";
}

const LOCK_REASONS_STORAGE_KEY = "usas_user_lock_reasons";

function getLockReasonCache(userId: string): string | null {
  if (typeof window === "undefined") return null;
  try {
    const raw = localStorage.getItem(LOCK_REASONS_STORAGE_KEY);
    if (!raw) return null;
    const map = JSON.parse(raw) as Record<string, string>;
    return typeof map[userId] === "string" && map[userId].trim() ? map[userId].trim() : null;
  } catch {
    return null;
  }
}

function saveLockReasonCache(userId: string, reason: string): void {
  if (typeof window === "undefined") return;
  try {
    const raw = localStorage.getItem(LOCK_REASONS_STORAGE_KEY);
    const map = raw ? (JSON.parse(raw) as Record<string, string>) : {};
    map[userId] = reason;
    localStorage.setItem(LOCK_REASONS_STORAGE_KEY, JSON.stringify(map));
  } catch {
    // Ignore storage errors
  }
}

function removeLockReasonCache(userId: string): void {
  if (typeof window === "undefined") return;
  try {
    const raw = localStorage.getItem(LOCK_REASONS_STORAGE_KEY);
    if (!raw) return;
    const map = JSON.parse(raw) as Record<string, string>;
    delete map[userId];
    localStorage.setItem(LOCK_REASONS_STORAGE_KEY, JSON.stringify(map));
  } catch {
    // Ignore storage errors
  }
}

function statusBadgeVariant(status: string): "default" | "secondary" | "destructive" | "outline" {
  if (status === "active") return "default";
  if (status === "unverified" || status === "pending") return "secondary";
  if (status === "locked" || status === "temp_locked") return "destructive";
  return "outline";
}

function formatStatusLabel(status: string): string {
  switch (status) {
    case "active":
      return "Hoạt động";
    case "locked":
      return "Đã khóa";
    case "temp_locked":
      return "Tạm khóa";
    case "unverified":
      return "Chưa xác thực";
    case "pending":
      return "Chờ duyệt";
    default:
      return status;
  }
}

/** [UC-60..UC-63] Màn hình Quản trị người dùng (Admin User Management). */
export function AdminUsersView() {
  const { user: currentUser } = useAuth();
  const isAdmin = currentUser?.role === "admin";
  const isModerator = currentUser?.role === "moderator";

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

  // Form tạo tài khoản (Moderator hoặc Center)
  const [isCreateOpen, setIsCreateOpen] = useState(false);
  const [createRole, setCreateRole] = useState<"moderator" | "center">("moderator");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [fullName, setFullName] = useState("");
  const [phone, setPhone] = useState("");
  const [formError, setFormError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  // Dialog xem chi tiết
  const [detailTarget, setDetailTarget] = useState<UserDto | null>(null);

  // Dialog khóa tài khoản
  const [lockTarget, setLockTarget] = useState<UserDto | null>(null);
  const [lockReason, setLockReason] = useState("");
  const [lockSubmitting, setLockSubmitting] = useState(false);
  const [lockDialogError, setLockDialogError] = useState<string | null>(null);

  // Dialog mở khóa tài khoản
  const [unlockTarget, setUnlockTarget] = useState<UserDto | null>(null);
  const [unlockReason, setUnlockReason] = useState("");
  const [unlockSubmitting, setUnlockSubmitting] = useState(false);
  const [unlockDialogError, setUnlockDialogError] = useState<string | null>(null);

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
      const mergedItems = data.items.map((u) => {
        if (u.status === "locked" && !u.lockReason) {
          const cached = getLockReasonCache(u.id);
          if (cached) return { ...u, lockReason: cached };
        }
        return u;
      });
      setUsers(mergedItems);
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

  /** Tải danh sách khi đổi trang/lọc */
  useEffect(() => {
    const signal = { cancelled: false };
    void (async () => {
      await fetchUserPage(page, signal);
    })();
    return () => {
      signal.cancelled = true;
    };
  }, [fetchUserPage, page]);

  /** Debounce ô tìm kiếm */
  useEffect(() => {
    const timer = window.setTimeout(() => {
      setSearchQuery(searchInput.trim());
      setPage(1);
    }, 300);
    return () => window.clearTimeout(timer);
  }, [searchInput]);

  const handleResetFilters = () => {
    setSearchInput("");
    setSearchQuery("");
    setRoleFilter("");
    setStatusFilter("");
    setPage(1);
  };

  const openLockDialog = (user: UserDto) => {
    if (!isAdmin) return;
    setLockTarget(user);
    setLockReason("");
    setLockDialogError(null);
  };

  const closeLockDialog = () => {
    if (lockSubmitting) return;
    setLockTarget(null);
    setLockReason("");
    setLockDialogError(null);
  };

  const handleConfirmLock = async () => {
    if (!isAdmin || !lockTarget) return;
    const reason = lockReason.trim();
    if (!reason) {
      setLockDialogError("Vui lòng nhập lý do khóa.");
      return;
    }
    setLockDialogError(null);
    setLockSubmitting(true);
    try {
      await adminUsersApi.lock(lockTarget.id, { reason });
      saveLockReasonCache(lockTarget.id, reason);
      setUsers((prev) =>
        prev.map((u) => (u.id === lockTarget.id ? { ...u, status: "locked", lockReason: reason } : u))
      );
      toast.add({
        type: "success",
        priority: "high",
        timeout: 6000,
        title: "Đã khóa tài khoản",
        description: lockTarget.email,
      });
      if (detailTarget?.id === lockTarget.id) {
        setDetailTarget((prev) => prev ? { ...prev, status: "locked", lockReason: reason } : null);
      }
      setLockTarget(null);
      setLockReason("");
      setLockDialogError(null);
      await loadUsers();
    } catch (err) {
      setLockDialogError(err instanceof ApiError ? err.message : "Không khóa được tài khoản.");
    } finally {
      setLockSubmitting(false);
    }
  };

  const openUnlockDialog = (user: UserDto) => {
    if (!isAdmin) return;
    setUnlockTarget(user);
    setUnlockReason("");
    setUnlockDialogError(null);
  };

  const closeUnlockDialog = () => {
    if (unlockSubmitting) return;
    setUnlockTarget(null);
    setUnlockReason("");
    setUnlockDialogError(null);
  };

  const handleConfirmUnlock = async () => {
    if (!isAdmin || !unlockTarget) return;
    const reason = unlockReason.trim();
    if (!reason) {
      setUnlockDialogError("Vui lòng nhập lý do mở khóa.");
      return;
    }
    setUnlockDialogError(null);
    setUnlockSubmitting(true);
    try {
      await adminUsersApi.unlock(unlockTarget.id, { reason });
      removeLockReasonCache(unlockTarget.id);
      setUsers((prev) =>
        prev.map((u) => (u.id === unlockTarget.id ? { ...u, status: "active", lockReason: null } : u))
      );
      toast.add({
        type: "success",
        priority: "high",
        timeout: 6000,
        title: "Đã mở khóa tài khoản",
        description: unlockTarget.email,
      });
      if (detailTarget?.id === unlockTarget.id) {
        setDetailTarget((prev) => prev ? { ...prev, status: "active", lockReason: null } : null);
      }
      setUnlockTarget(null);
      setUnlockReason("");
      setUnlockDialogError(null);
      await loadUsers();
    } catch (err) {
      setUnlockDialogError(err instanceof ApiError ? err.message : "Không mở khóa được tài khoản.");
    } finally {
      setUnlockSubmitting(false);
    }
  };

  const handleOpenCreateModal = () => {
    setEmail("");
    setPassword("");
    setFullName("");
    setPhone("");
    setFormError(null);
    if (isModerator) {
      setCreateRole("center");
    } else {
      setCreateRole("moderator");
    }
    setIsCreateOpen(true);
  };

  const handleCreateAccount = async (e: React.FormEvent) => {
    e.preventDefault();
    setFormError(null);

    if (isModerator && createRole !== "center") {
      setFormError("Kiểm duyệt viên chỉ có quyền cấp tài khoản cho trung tâm tư vấn.");
      return;
    }
    if (isAdmin && createRole !== "moderator") {
      setFormError("Quản trị viên chỉ cấp tài khoản kiểm duyệt viên tại khu vực này. Cấp tài khoản trung tâm do Moderator phụ trách.");
      return;
    }

    const trimmedEmail = email.trim();
    const trimmedName = fullName.trim();

    if (!trimmedName) {
      setFormError("Họ và tên không được để trống.");
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
      let created: UserDto;
      if (createRole === "center") {
        const payload: CreateCenterAccountRequest = {
          email: trimmedEmail,
          password,
          fullName: trimmedName,
          phone: phonePayload,
        };
        created = await adminUsersApi.createCenter(payload);
      } else {
        const payload: CreateModeratorAccountRequest = {
          email: trimmedEmail,
          password,
          fullName: trimmedName,
          phone: phonePayload,
        };
        created = await adminUsersApi.createModerator(payload);
      }

      setIsCreateOpen(false);
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
        title: createRole === "center" ? "Đã tạo tài khoản trung tâm" : "Đã tạo tài khoản moderator",
        description: created.email,
      });
    } catch (err) {
      setFormError(err instanceof ApiError ? err.message : "Không tạo được tài khoản.");
    } finally {
      setIsSubmitting(false);
    }
  };

  // Tính số liệu nhanh từ danh sách đang tải
  const activeCount = users.filter((u) => u.status === "active").length;
  const lockedCount = users.filter((u) => u.status === "locked" || u.status === "temp_locked").length;
  const pendingCount = users.filter((u) => u.status === "unverified" || u.status === "pending").length;

  return (
    <div className="flex h-full min-h-0 flex-1 flex-col overflow-y-auto space-y-6 pr-1 pb-8">
      {/* Header bar */}
      <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h1 className="text-xl font-bold tracking-tight text-foreground sm:text-2xl">
            Quản trị người dùng
          </h1>
          <p className="text-sm text-muted-foreground">
            Quản lý danh sách tài khoản, trạng thái hoạt động và phân quyền đặc thù
          </p>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          <Button
            type="button"
            variant="outline"
            size="sm"
            onClick={() => void loadUsers()}
            disabled={listLoading}
            className="gap-1.5"
          >
            <RotateCcwIcon className="size-3.5" />
            Làm mới
          </Button>
          <Button
            type="button"
            size="sm"
            onClick={handleOpenCreateModal}
            className="gap-1.5 bg-brand text-white hover:bg-brand/90"
          >
            <UserPlusIcon className="size-4" />
            {isModerator ? "Cấp tài khoản trung tâm" : "Cấp tài khoản moderator"}
          </Button>
        </div>
      </div>

      {/* KPI Cards */}
      <div className="grid gap-3 grid-cols-2 sm:grid-cols-4">
        <Card className="border border-border/70 shadow-2xs">
          <CardContent className="flex items-center gap-3 p-4">
            <div className="rounded-lg bg-brand/10 p-2 text-brand">
              <UsersIcon className="size-5" />
            </div>
            <div>
              <p className="text-xs font-medium text-muted-foreground">Tổng tài khoản</p>
              <p className="text-xl font-bold text-foreground">{totalCount}</p>
            </div>
          </CardContent>
        </Card>

        <Card className="border border-border/70 shadow-2xs">
          <CardContent className="flex items-center gap-3 p-4">
            <div className="rounded-lg bg-emerald-500/10 p-2 text-emerald-600">
              <CheckCircle2Icon className="size-5" />
            </div>
            <div>
              <p className="text-xs font-medium text-muted-foreground">Trang này hoạt động</p>
              <p className="text-xl font-bold text-foreground">{activeCount}</p>
            </div>
          </CardContent>
        </Card>

        <Card className="border border-border/70 shadow-2xs">
          <CardContent className="flex items-center gap-3 p-4">
            <div className="rounded-lg bg-rose-500/10 p-2 text-rose-600">
              <ShieldAlertIcon className="size-5" />
            </div>
            <div>
              <p className="text-xs font-medium text-muted-foreground">Bị khóa / Tạm khóa</p>
              <p className="text-xl font-bold text-foreground">{lockedCount}</p>
            </div>
          </CardContent>
        </Card>

        <Card className="border border-border/70 shadow-2xs">
          <CardContent className="flex items-center gap-3 p-4">
            <div className="rounded-lg bg-amber-500/10 p-2 text-amber-600">
              <ClockIcon className="size-5" />
            </div>
            <div>
              <p className="text-xs font-medium text-muted-foreground">Chờ xác thực</p>
              <p className="text-xl font-bold text-foreground">{pendingCount}</p>
            </div>
          </CardContent>
        </Card>
      </div>

      {/* [UC-60] Tìm, lọc, phân trang danh sách tài khoản */}
      <Card className="border border-border/80 shadow-2xs">
        <CardHeader className="pb-3">
          <CardTitle className="text-lg flex items-center justify-between">
            <span>Danh sách tài khoản</span>
            {(searchQuery || roleFilter || statusFilter) && (
              <Button
                type="button"
                variant="ghost"
                size="xs"
                onClick={handleResetFilters}
                className="text-xs text-muted-foreground hover:text-foreground gap-1"
              >
                <RotateCcwIcon className="size-3" />
                Xóa bộ lọc
              </Button>
            )}
          </CardTitle>
          <CardDescription>
            Tìm kiếm theo email, họ tên; phân loại theo vai trò và tình trạng tài khoản.
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          {/* Thanh công cụ lọc */}
          <div className="flex flex-col gap-3 sm:flex-row sm:flex-wrap sm:items-end">
            <Field className="min-w-[14rem] flex-1">
              <FieldLabel htmlFor="user-search" className="text-xs">
                Tìm email hoặc họ tên
              </FieldLabel>
              <div className="relative">
                <SearchIcon className="absolute left-3 top-1/2 -translate-y-1/2 size-4 text-muted-foreground" />
                <Input
                  id="user-search"
                  value={searchInput}
                  onChange={(e) => setSearchInput(e.target.value)}
                  placeholder="vd: nguyen@example.com hoặc Nguyễn Văn A..."
                  className="pl-9"
                  autoComplete="off"
                />
              </div>
            </Field>

            <Field className="w-full sm:w-auto">
              <FieldLabel htmlFor="user-role-filter" className="text-xs flex items-center gap-1">
                <FilterIcon className="size-3 text-muted-foreground" />
                Vai trò
              </FieldLabel>
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
              <FieldLabel htmlFor="user-status-filter" className="text-xs flex items-center gap-1">
                <FilterIcon className="size-3 text-muted-foreground" />
                Trạng thái
              </FieldLabel>
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

          {/* Nội dung danh sách */}
          {listLoading ? (
            <div className="flex items-center justify-center gap-2 py-12 text-sm text-muted-foreground">
              <Spinner className="size-5" />
              Đang tải danh sách người dùng...
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
              <div className="flex items-center justify-between text-xs text-muted-foreground">
                <span>
                  {totalCount === 0
                    ? "Không có tài khoản nào khớp bộ lọc."
                    : `Hiển thị ${(page - 1) * ACCOUNT_LIST_PAGE_SIZE + 1}–${Math.min(
                      page * ACCOUNT_LIST_PAGE_SIZE,
                      totalCount,
                    )} trên tổng số ${totalCount} tài khoản`}
                </span>
              </div>

              <div className="rounded-md border border-border/80 overflow-hidden">
                <Table>
                  <TableHeader>
                    <TableRow className="bg-muted/40">
                      <TableHead>Email</TableHead>
                      <TableHead>Họ tên</TableHead>
                      <TableHead>Vai trò</TableHead>
                      <TableHead>Trạng thái</TableHead>
                      <TableHead>Số điện thoại</TableHead>
                      <TableHead className="text-right">Thao tác</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {users.length === 0 ? (
                      <TableRow>
                        <TableCell colSpan={6} className="h-28 text-center text-muted-foreground">
                          Không tìm thấy người dùng phù hợp.
                        </TableCell>
                      </TableRow>
                    ) : (
                      users.map((u) => (
                        <TableRow key={u.id} className="hover:bg-muted/30">
                          <TableCell className="font-medium">
                            <span className="block max-w-[15rem] truncate" title={u.email}>
                              {u.email}
                            </span>
                          </TableCell>
                          <TableCell>
                            <span className="block max-w-[12rem] truncate" title={u.fullName}>
                              {u.fullName}
                            </span>
                          </TableCell>
                          <TableCell>
                            <Badge variant="outline" className="font-normal text-xs">
                              {formatUserRole(u.role)}
                            </Badge>
                          </TableCell>
                          <TableCell>
                            <Badge variant={statusBadgeVariant(u.status)} className="capitalize text-xs font-normal">
                              {formatStatusLabel(u.status)}
                            </Badge>
                          </TableCell>
                          <TableCell className="text-muted-foreground text-sm">
                            {u.phone ?? "—"}
                          </TableCell>
                          <TableCell className="text-right">
                            <div className="flex items-center justify-end gap-1.5">
                              <Button
                                type="button"
                                variant="ghost"
                                size="sm"
                                className="size-8 p-0 text-muted-foreground hover:text-foreground shrink-0 flex items-center justify-center"
                                onClick={() => {
                                  const cached = getLockReasonCache(u.id);
                                  setDetailTarget({
                                    ...u,
                                    lockReason: u.lockReason || cached || null,
                                  });
                                }}
                                title="Xem chi tiết"
                              >
                                <EyeIcon className="size-4" />
                                <span className="sr-only">Chi tiết</span>
                              </Button>

                              <div className="w-[88px] shrink-0 flex items-center justify-end">
                                {canAdminUnlock(u, currentUser?.role) ? (
                                  <Button
                                    type="button"
                                    variant="outline"
                                    size="sm"
                                    className="h-8 w-full gap-1 text-emerald-600 hover:text-emerald-700 hover:bg-emerald-50 dark:hover:bg-emerald-950 border-emerald-200"
                                    onClick={() => openUnlockDialog(u)}
                                  >
                                    <KeyRoundIcon className="size-3.5" aria-hidden />
                                    Mở khóa
                                  </Button>
                                ) : canAdminLock(u, currentUser?.role, currentUser?.id) ? (
                                  <Button
                                    type="button"
                                    variant="outline"
                                    size="sm"
                                    className="h-8 w-full gap-1 text-rose-600 hover:text-rose-700 hover:bg-rose-50 dark:hover:bg-rose-950 border-rose-200"
                                    onClick={() => openLockDialog(u)}
                                  >
                                    <BanIcon className="size-3.5" aria-hidden />
                                    Khóa
                                  </Button>
                                ) : (
                                  <div className="w-full text-center text-xs text-muted-foreground">
                                    —
                                  </div>
                                )}
                              </div>
                            </div>
                          </TableCell>
                        </TableRow>
                      ))
                    )}
                  </TableBody>
                </Table>
              </div>

              {totalPages > 1 && (
                <div className="flex flex-wrap items-center justify-between gap-2 pt-2">
                  <span className="text-xs text-muted-foreground">
                    Trang {page} / {totalPages}
                  </span>
                  <div className="flex items-center gap-1.5">
                    <Button
                      type="button"
                      variant="outline"
                      size="sm"
                      disabled={page <= 1 || listLoading}
                      onClick={() => setPage((p) => Math.max(1, p - 1))}
                    >
                      Trang trước
                    </Button>
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
                </div>
              )}
            </>
          )}
        </CardContent>
      </Card>

      {/* [UC-63 & UC-71] Dialog Cấp tài khoản mới (Moderator tạo Center, Admin tạo Moderator) */}
      <Dialog open={isCreateOpen} onOpenChange={setIsCreateOpen}>
        <DialogContent className="max-w-md">
          <DialogHeader>
            <DialogTitle className="flex items-center gap-2">
              <UserPlusIcon className="size-5 text-brand" />
              {isModerator ? "Cấp tài khoản Trung tâm tư vấn" : "Cấp tài khoản Kiểm duyệt viên"}
            </DialogTitle>
            <DialogDescription>
              {isModerator
                ? "Tạo tài khoản đại diện cho Trung tâm tư vấn (tài khoản vai trò center được kích hoạt ngay)."
                : "Tạo tài khoản Kiểm duyệt viên cho nhân sự hệ thống (khách không thể tự đăng ký)."}
            </DialogDescription>
          </DialogHeader>

          <form onSubmit={handleCreateAccount}>
            <FieldGroup>
              {formError && (
                <Alert variant="destructive">
                  <AlertDescription>{formError}</AlertDescription>
                </Alert>
              )}

              <Field>
                <FieldLabel htmlFor="create-role">Loại tài khoản</FieldLabel>
                <div className="h-9 px-3 flex items-center rounded-md border border-input bg-muted/40 text-sm font-medium">
                  {isModerator ? "Trung tâm tư vấn (Center)" : "Kiểm duyệt viên (Moderator)"}
                </div>
                <FieldDescription>
                  {isModerator
                    ? "Tài khoản đại diện quản lý thông tin và chương trình của trung tâm du học."
                    : "Tài khoản có quyền kiểm duyệt nội dung diễn đàn và bài viết tri thức."}
                </FieldDescription>
              </Field>

              <Field>
                <FieldLabel htmlFor="create-fullName">
                  Họ và tên người dùng
                </FieldLabel>
                <Input
                  id="create-fullName"
                  value={fullName}
                  onChange={(e) => setFullName(e.target.value)}
                  placeholder="Ví dụ: Nguyễn Văn A"
                  autoComplete="name"
                  required
                />
              </Field>

              <Field>
                <FieldLabel htmlFor="create-email">Email đăng nhập</FieldLabel>
                <Input
                  id="create-email"
                  type="email"
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                  placeholder="admin@example.com"
                  autoComplete="off"
                  required
                />
              </Field>

              <Field>
                <FieldLabel htmlFor="create-password">Mật khẩu khởi tạo</FieldLabel>
                <Input
                  id="create-password"
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
                <FieldLabel htmlFor="create-phone">Số điện thoại (tùy chọn)</FieldLabel>
                <Input
                  id="create-phone"
                  type="tel"
                  value={phone}
                  onChange={(e) => setPhone(e.target.value)}
                  placeholder="0912345678"
                />
              </Field>

              <DialogFooter className="pt-2">
                <Button
                  type="button"
                  variant="outline"
                  onClick={() => setIsCreateOpen(false)}
                  disabled={isSubmitting}
                >
                  Hủy
                </Button>
                <Button type="submit" disabled={isSubmitting} className="gap-2 bg-brand text-white hover:bg-brand/90">
                  {isSubmitting ? <Spinner className="size-4" /> : <ShieldCheckIcon className="size-4" />}
                  Tạo tài khoản
                </Button>
              </DialogFooter>
            </FieldGroup>
          </form>
        </DialogContent>
      </Dialog>

      {/* Dialog xem chi tiết tài khoản */}
      <Dialog open={detailTarget !== null} onOpenChange={(open) => !open && setDetailTarget(null)}>
        <DialogContent className="max-w-md">
          <DialogHeader>
            <DialogTitle className="flex items-center gap-2">
              <UserCheckIcon className="size-5 text-brand" />
              Chi tiết tài khoản
            </DialogTitle>
            <DialogDescription>
              Thông tin chi tiết và phân quyền của tài khoản người dùng.
            </DialogDescription>
          </DialogHeader>

          {detailTarget && (
            <div className="space-y-4 py-2 text-sm">
              <div className="grid grid-cols-3 gap-2 border-b pb-2">
                <span className="text-muted-foreground font-medium">Họ tên:</span>
                <span className="col-span-2 font-semibold text-foreground">{detailTarget.fullName}</span>
              </div>
              <div className="grid grid-cols-3 gap-2 border-b pb-2">
                <span className="text-muted-foreground font-medium">Email:</span>
                <span className="col-span-2 text-foreground font-mono text-xs break-all">{detailTarget.email}</span>
              </div>
              <div className="grid grid-cols-3 gap-2 border-b pb-2">
                <span className="text-muted-foreground font-medium">Vai trò:</span>
                <span className="col-span-2">
                  <Badge variant="outline">{formatUserRole(detailTarget.role)}</Badge>
                </span>
              </div>
              <div className="grid grid-cols-3 gap-2 border-b pb-2">
                <span className="text-muted-foreground font-medium">Trạng thái:</span>
                <span className="col-span-2">
                  <Badge variant={statusBadgeVariant(detailTarget.status)}>
                    {formatStatusLabel(detailTarget.status)}
                  </Badge>
                </span>
              </div>

              {(detailTarget.status === "locked" || detailTarget.status === "temp_locked") && (
                <div className="rounded-lg border border-destructive/30 bg-destructive/5 p-3 space-y-1.5">
                  <div className="flex items-center gap-1.5 text-xs font-semibold text-destructive">
                    <BanIcon className="size-4 shrink-0" />
                    <span>Lý do khóa tài khoản:</span>
                  </div>
                  <div className="text-xs text-foreground leading-relaxed font-medium">
                    {detailTarget.lockReason || getLockReasonCache(detailTarget.id) || (
                      detailTarget.status === "temp_locked"
                        ? "Tài khoản bị tạm khóa do nhập sai mật khẩu 5 lần liên tiếp (chờ Quản trị viên mở khóa)."
                        : "Vi phạm quy chế sử dụng và chính sách của hệ thống."
                    )}
                  </div>
                </div>
              )}
              <div className="grid grid-cols-3 gap-2 border-b pb-2">
                <span className="text-muted-foreground font-medium">Số điện thoại:</span>
                <span className="col-span-2 text-foreground">{detailTarget.phone ?? "Chưa cung cấp"}</span>
              </div>
              <div className="grid grid-cols-3 gap-2 border-b pb-2">
                <span className="text-muted-foreground font-medium">Mã ID:</span>
                <span className="col-span-2 text-xs font-mono text-muted-foreground break-all">{detailTarget.id}</span>
              </div>

              {detailTarget.ageGroup && (
                <div className="grid grid-cols-3 gap-2 border-b pb-2">
                  <span className="text-muted-foreground font-medium">Nhóm độ tuổi:</span>
                  <span className="col-span-2 capitalize text-foreground">{detailTarget.ageGroup}</span>
                </div>
              )}

              {detailTarget.parentConsentStatus && (
                <div className="grid grid-cols-3 gap-2 border-b pb-2">
                  <span className="text-muted-foreground font-medium">Đồng thuận PH:</span>
                  <span className="col-span-2 capitalize text-foreground">{detailTarget.parentConsentStatus}</span>
                </div>
              )}

              {detailTarget.blockedFeatures && detailTarget.blockedFeatures.length > 0 && (
                <div className="space-y-1 pt-1">
                  <span className="text-muted-foreground font-medium block">Tính năng đang bị chặn:</span>
                  <div className="flex flex-wrap gap-1">
                    {detailTarget.blockedFeatures.map((f) => (
                      <Badge key={f} variant="destructive" className="text-2xs font-normal">
                        {f}
                      </Badge>
                    ))}
                  </div>
                </div>
              )}

              <DialogFooter className="pt-4 flex items-center justify-between sm:justify-between w-full">
                <div>
                  {canAdminUnlock(detailTarget, currentUser?.role) ? (
                    <Button
                      type="button"
                      variant="outline"
                      size="sm"
                      className="gap-1 text-emerald-600 hover:text-emerald-700 hover:bg-emerald-50"
                      onClick={() => {
                        const t = detailTarget;
                        openUnlockDialog(t);
                      }}
                    >
                      <KeyRoundIcon className="size-3.5" />
                      Mở khóa
                    </Button>
                  ) : canAdminLock(detailTarget, currentUser?.role, currentUser?.id) ? (
                    <Button
                      type="button"
                      variant="outline"
                      size="sm"
                      className="gap-1 text-rose-600 hover:text-rose-700 hover:bg-rose-50"
                      onClick={() => {
                        const t = detailTarget;
                        openLockDialog(t);
                      }}
                    >
                      <BanIcon className="size-3.5" />
                      Khóa tài khoản
                    </Button>
                  ) : null}
                </div>

                <Button type="button" variant="outline" size="sm" onClick={() => setDetailTarget(null)}>
                  Đóng
                </Button>
              </DialogFooter>
            </div>
          )}
        </DialogContent>
      </Dialog>

      {/* [UC-61] Xác nhận khóa tài khoản */}
      <Dialog open={lockTarget !== null} onOpenChange={(open) => !open && closeLockDialog()}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle className="flex items-center gap-2 text-rose-600">
              <BanIcon className="size-5" />
              Khóa tài khoản người dùng
            </DialogTitle>
            <DialogDescription>
              {lockTarget
                ? `Khóa tài khoản ${lockTarget.fullName} (${lockTarget.email}). Người dùng sẽ không thể đăng nhập vào hệ thống.`
                : ""}
            </DialogDescription>
          </DialogHeader>

          <Field>
            <FieldLabel htmlFor="lock-reason">Lý do khóa tài khoản <span className="text-destructive">*</span></FieldLabel>
            <Input
              id="lock-reason"
              value={lockReason}
              onChange={(e) => setLockReason(e.target.value)}
              placeholder="Ví dụ: Vi phạm quy định sử dụng, nghi vấn gian lận thông tin..."
              maxLength={500}
              disabled={lockSubmitting}
            />
          </Field>

          {lockDialogError && (
            <Alert variant="destructive">
              <AlertDescription>{lockDialogError}</AlertDescription>
            </Alert>
          )}

          <DialogFooter>
            <Button type="button" variant="outline" onClick={closeLockDialog} disabled={lockSubmitting}>
              Hủy
            </Button>
            <Button
              type="button"
              variant="destructive"
              onClick={() => void handleConfirmLock()}
              disabled={lockSubmitting}
              className="gap-1.5"
            >
              {lockSubmitting ? <Spinner className="size-4" /> : <BanIcon className="size-4" />}
              Xác nhận khóa
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* [UC-62] Xác nhận mở khóa tài khoản */}
      <Dialog open={unlockTarget !== null} onOpenChange={(open) => !open && closeUnlockDialog()}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle className="flex items-center gap-2 text-emerald-600">
              <KeyRoundIcon className="size-5" />
              Mở khóa tài khoản
            </DialogTitle>
            <DialogDescription>
              {unlockTarget
                ? `Mở khóa cho ${unlockTarget.fullName} (${unlockTarget.email}). Tài khoản sẽ có thể đăng nhập bình thường.`
                : ""}
            </DialogDescription>
          </DialogHeader>

          <Field>
            <FieldLabel htmlFor="unlock-reason">Lý do mở khóa <span className="text-destructive">*</span></FieldLabel>
            <Input
              id="unlock-reason"
              value={unlockReason}
              onChange={(e) => setUnlockReason(e.target.value)}
              placeholder="Ví dụ: Người dùng đã xác minh danh tính và giải trình hợp lệ..."
              maxLength={500}
              disabled={unlockSubmitting}
            />
          </Field>

          {unlockDialogError && (
            <Alert variant="destructive">
              <AlertDescription>{unlockDialogError}</AlertDescription>
            </Alert>
          )}

          <DialogFooter>
            <Button type="button" variant="outline" onClick={closeUnlockDialog} disabled={unlockSubmitting}>
              Hủy
            </Button>
            <Button
              type="button"
              className="bg-emerald-600 text-white hover:bg-emerald-700 gap-1.5"
              onClick={() => void handleConfirmUnlock()}
              disabled={unlockSubmitting}
            >
              {unlockSubmitting ? <Spinner className="size-4" /> : <KeyRoundIcon className="size-4" />}
              Xác nhận mở khóa
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
