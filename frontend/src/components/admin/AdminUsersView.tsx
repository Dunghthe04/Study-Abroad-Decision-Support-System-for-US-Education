"use client";

import React, { useCallback, useEffect, useMemo, useState } from "react";
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
import { Field, FieldDescription, FieldGroup, FieldLabel } from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import { Spinner } from "@/components/ui/spinner";
import { toast } from "@/components/ui/toast";
import { Label } from "@/components/ui/label";
import { Popover, PopoverContent, PopoverTrigger } from "@/components/ui/popover";
import {
  DataTable,
  type DataTableColumn,
  type DataTableToolbarSlots,
} from "@/components/admin/shared/DataTable";
import { DetailPanel } from "@/components/admin/shared/DetailPanel";
import { TablePagination } from "@/components/admin/shared/TablePagination";
import { OptionSelect } from "@/components/admin/universities/OptionSelect";
import { cn } from "@/lib/utils";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";

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

/** [UC-61] Khóa tài khoản: Admin khóa được moderator, center, users. Moderator chỉ khóa được cấp dưới (center, student, parent). */
function canAdminLock(target: UserDto, currentUserRole?: string, currentUserId?: string): boolean {
  if (currentUserId && target.id === currentUserId) return false;
  if (target.status === "locked") return false;
  if (target.role === "admin") return false;

  if (currentUserRole === "admin") {
    return true;
  }

  if (currentUserRole === "moderator") {
    return target.role !== "moderator";
  }

  return false;
}

/** [UC-62] Mở khóa tài khoản: Admin mở khóa được moderator, center, users. Moderator chỉ mở khóa được cấp dưới (center, student, parent). */
function canAdminUnlock(target: UserDto, currentUserRole?: string): boolean {
  if (target.role === "admin") return false;
  if (target.status !== "locked" && target.status !== "temp_locked") return false;

  if (currentUserRole === "admin") {
    return true;
  }

  if (currentUserRole === "moderator") {
    return target.role !== "moderator";
  }

  return false;
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

function statusBadgeVariant(status: string): "ok" | "warn" | "risk" | "neutral" {
  if (status === "active") return "ok";
  if (status === "unverified" || status === "pending") return "warn";
  if (status === "locked" || status === "temp_locked") return "risk";
  return "neutral";
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

  const openLockDialog = useCallback(
    (user: UserDto) => {
      if (!canAdminLock(user, currentUser?.role, currentUser?.id)) return;
      setLockTarget(user);
      setLockReason("");
      setLockDialogError(null);
    },
    [currentUser?.id, currentUser?.role]
  );

  const closeLockDialog = () => {
    if (lockSubmitting) return;
    setLockTarget(null);
    setLockReason("");
    setLockDialogError(null);
  };

  const handleConfirmLock = async () => {
    if (!lockTarget || !canAdminLock(lockTarget, currentUser?.role, currentUser?.id)) return;
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

  const openUnlockDialog = useCallback(
    (user: UserDto) => {
      if (!canAdminUnlock(user, currentUser?.role)) return;
      setUnlockTarget(user);
      setUnlockReason("");
      setUnlockDialogError(null);
    },
    [currentUser?.role]
  );

  const closeUnlockDialog = () => {
    if (unlockSubmitting) return;
    setUnlockTarget(null);
    setUnlockReason("");
    setUnlockDialogError(null);
  };

  const handleConfirmUnlock = async () => {
    if (!unlockTarget || !canAdminUnlock(unlockTarget, currentUser?.role)) return;
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

  const [filterMenuOpen, setFilterMenuOpen] = useState(false);
  const [draftRole, setDraftRole] = useState<string>("all");
  const [draftStatus, setDraftStatus] = useState<string>("all");

  const activeFilterCount = (roleFilter ? 1 : 0) + (statusFilter ? 1 : 0);
  const hasActiveSearchOrFilter = activeFilterCount > 0 || searchQuery.length > 0;

  const roleFilterOptions = useMemo<{ value: string; label: string }[]>(
    () => [
      { value: "all", label: "Tất cả vai trò" },
      ...(Object.keys(USER_ROLE_LABELS) as UserRole[]).map((role) => ({
        value: role,
        label: USER_ROLE_LABELS[role],
      })),
    ],
    []
  );

  const statusFilterOptions = useMemo<{ value: string; label: string }[]>(
    () =>
      ACCOUNT_STATUS_OPTIONS.map((opt) => ({
        value: opt.value || "all",
        label: opt.label,
      })),
    []
  );

  const createRoleOptions = useMemo<{ value: string; label: string }[]>(
    () => [
      { value: "moderator", label: "Kiểm duyệt viên (Moderator - UC-63)" },
      { value: "center", label: "Trung tâm tư vấn (Center - UC-64)" },
    ],
    []
  );

  const toggleDetailTarget = useCallback((u: UserDto) => {
    const cached = getLockReasonCache(u.id);
    setDetailTarget((prev) =>
      prev?.id === u.id
        ? null
        : {
            ...u,
            lockReason: u.lockReason || cached || null,
          }
    );
  }, []);

  const handleFilterMenuOpenChange = (open: boolean) => {
    if (open) {
      setDraftRole(roleFilter || "all");
      setDraftStatus(statusFilter || "all");
    }
    setFilterMenuOpen(open);
  };

  const handleApplyFilters = (e: React.FormEvent) => {
    e.preventDefault();
    setRoleFilter(draftRole === "all" ? "" : (draftRole as UserRole));
    setStatusFilter(draftStatus === "all" ? "" : draftStatus);
    setPage(1);
    setFilterMenuOpen(false);
  };

  const handleClearAllFilters = () => {
    handleResetFilters();
    setDraftRole("all");
    setDraftStatus("all");
    setFilterMenuOpen(false);
  };

  const userColumns = useMemo<DataTableColumn<UserDto>[]>(
    () => [
      {
        id: "email",
        label: "Email",
        required: true,
        render: (u) => (
          <div className="flex flex-col min-w-[180px]">
            <button
              type="button"
              onClick={(e) => {
                e.stopPropagation();
                toggleDetailTarget(u);
              }}
              className="text-left font-medium text-brand hover:underline truncate max-w-[230px]"
            >
              {u.email}
            </button>
            <span className="text-xs text-ink-3 font-mono">{u.id.slice(0, 8)}</span>
          </div>
        ),
      },
      {
        id: "fullName",
        label: "Họ tên",
        render: (u) => <span className="text-ink font-medium">{u.fullName || "—"}</span>,
      },
      {
        id: "role",
        label: "Vai trò",
        render: (u) => (
          <Badge variant="outline" className="text-xs font-normal whitespace-nowrap">
            {formatUserRole(u.role)}
          </Badge>
        ),
      },
      {
        id: "status",
        label: "Trạng thái",
        render: (u) => (
          <Badge variant={statusBadgeVariant(u.status)} className="text-xs font-normal whitespace-nowrap">
            {formatStatusLabel(u.status)}
          </Badge>
        ),
      },
      {
        id: "phone",
        label: "Số điện thoại",
        render: (u) => <span className="text-xs text-ink-2 font-mono">{u.phone || "—"}</span>,
      },
      {
        id: "actions",
        label: "Thao tác",
        required: true,
        headerClassName: "w-px whitespace-nowrap text-right",
        cellClassName: "w-px text-right whitespace-nowrap",
        render: (u) => {
          const canLock = canAdminLock(u, currentUser?.role, currentUser?.id);
          const canUnlock = canAdminUnlock(u, currentUser?.role);
          return (
            <div
              className="inline-flex items-center justify-end gap-1"
              onClick={(e) => e.stopPropagation()}
            >
              <Button
                type="button"
                variant="ghost"
                size="xs"
                className="text-ink-2 hover:text-ink"
                onClick={() => toggleDetailTarget(u)}
              >
                <EyeIcon className="size-3.5" />
                Chi tiết
              </Button>
              <div className="w-[88px] shrink-0 flex items-center justify-end">
                {canUnlock ? (
                  <Button
                    type="button"
                    variant="ghost"
                    size="xs"
                    className="w-full justify-center text-ok hover:text-ok hover:bg-ok-wash"
                    onClick={() => openUnlockDialog(u)}
                  >
                    <KeyRoundIcon className="size-3.5" />
                    Mở khóa
                  </Button>
                ) : canLock ? (
                  <Button
                    type="button"
                    variant="ghost"
                    size="xs"
                    className="w-full justify-center text-risk hover:text-risk hover:bg-risk-wash"
                    onClick={() => openLockDialog(u)}
                  >
                    <BanIcon className="size-3.5" />
                    Khóa
                  </Button>
                ) : (
                  <span className="w-full text-center text-xs text-ink-3 select-none">—</span>
                )}
              </div>
            </div>
          );
        },
      },
    ],
    [currentUser?.id, currentUser?.role, openLockDialog, openUnlockDialog, toggleDetailTarget]
  );

  const userToolbar: DataTableToolbarSlots = {
    filters: (
      <Popover open={filterMenuOpen} onOpenChange={handleFilterMenuOpenChange}>
        <PopoverTrigger
          render={
            <Button
              type="button"
              variant="outline"
              size="sm"
              className="border-border bg-muted/60 text-foreground hover:bg-muted"
            />
          }
        >
          <FilterIcon className="size-4" />
          Bộ lọc
          {activeFilterCount > 0 ? ` (${activeFilterCount})` : ""}
        </PopoverTrigger>
        <PopoverContent align="start" className="w-80 max-w-[calc(100vw-2rem)] p-4">
          <form onSubmit={handleApplyFilters} className="space-y-4">
            <div className="space-y-1.5">
              <Label htmlFor="admin-users-role-filter">Vai trò</Label>
              <OptionSelect
                id="admin-users-role-filter"
                value={draftRole}
                options={roleFilterOptions}
                onChange={setDraftRole}
                size="sm"
                className="bg-surface"
              />
            </div>

            <div className="space-y-1.5">
              <Label htmlFor="admin-users-status-filter">Trạng thái</Label>
              <OptionSelect
                id="admin-users-status-filter"
                value={draftStatus}
                options={statusFilterOptions}
                onChange={setDraftStatus}
                size="sm"
                className="bg-surface"
              />
            </div>

            <div className="flex items-center justify-end gap-2 pt-1">
              <Button type="button" variant="outline" size="sm" onClick={handleClearAllFilters}>
                Xóa lọc
              </Button>
              <Button type="submit" size="sm">
                Áp dụng
              </Button>
            </div>
          </form>
        </PopoverContent>
      </Popover>
    ),
    search: (
      <form
        onSubmit={(e) => {
          e.preventDefault();
          setSearchQuery(searchInput.trim());
          setPage(1);
        }}
        role="search"
        className="relative"
      >
        <Input
          id="admin-users-search"
          type="text"
          value={searchInput}
          onChange={(e) => setSearchInput(e.target.value)}
          placeholder="Tìm theo email hoặc họ tên..."
          aria-label="Tìm kiếm tài khoản"
          className="pr-10 bg-surface"
        />
        <Button
          type="submit"
          variant="ghost"
          size="icon-sm"
          className="absolute top-1/2 right-1 -translate-y-1/2"
          aria-label="Tìm kiếm"
        >
          <SearchIcon className="size-4" />
        </Button>
      </form>
    ),
    actions: (
      <>
        {hasActiveSearchOrFilter && (
          <Button type="button" variant="outline" size="sm" onClick={handleClearAllFilters}>
            Xóa lọc
          </Button>
        )}
        <Button
          type="button"
          variant="outline"
          size="sm"
          onClick={() => void loadUsers()}
          disabled={listLoading}
          className="gap-1.5"
        >
          <RotateCcwIcon className={cn("size-3.5", listLoading && "animate-spin")} />
          Làm mới
        </Button>
        <Button type="button" size="sm" onClick={handleOpenCreateModal} className="gap-1.5">
          <UserPlusIcon className="size-4" />
          Cấp tài khoản
        </Button>
      </>
    ),
  };

  return (
    <div className="flex min-h-0 flex-1 flex-col gap-4">
      <header className="shrink-0">
        <h1 className="text-h2">Quản trị người dùng</h1>
        <p className="text-sm text-ink-2 mt-1">
          {isModerator
            ? "Xem danh sách tài khoản hệ thống và cấp tài khoản cho các trung tâm tư vấn đối tác."
            : "Quản lý toàn bộ tài khoản học sinh, trung tâm tư vấn và cấp tài khoản kiểm duyệt viên / trung tâm."}
        </p>
      </header>

      {/* Compact KPI summary tiles */}
      <div className="shrink-0 grid grid-cols-2 lg:grid-cols-4 gap-3">
        <div className="rounded-xl border border-line bg-surface px-4 py-3 flex items-center gap-3">
          <div className="size-9 rounded-lg bg-brand-wash text-brand flex items-center justify-center shrink-0">
            <UsersIcon className="size-4" />
          </div>
          <div className="min-w-0">
            <p className="text-xs text-ink-2 truncate">Tổng kết quả</p>
            <p className="font-heading text-lg font-bold tabular-nums text-ink">{totalCount}</p>
          </div>
        </div>
        <div className="rounded-xl border border-line bg-surface px-4 py-3 flex items-center gap-3">
          <div className="size-9 rounded-lg bg-ok-wash text-ok flex items-center justify-center shrink-0">
            <CheckCircle2Icon className="size-4" />
          </div>
          <div className="min-w-0">
            <p className="text-xs text-ink-2 truncate">Hoạt động (trang)</p>
            <p className="font-heading text-lg font-bold tabular-nums text-ink">{activeCount}</p>
          </div>
        </div>
        <div className="rounded-xl border border-line bg-surface px-4 py-3 flex items-center gap-3">
          <div className="size-9 rounded-lg bg-risk-wash text-risk flex items-center justify-center shrink-0">
            <ShieldAlertIcon className="size-4" />
          </div>
          <div className="min-w-0">
            <p className="text-xs text-ink-2 truncate">Bị khóa / Tạm khóa</p>
            <p className="font-heading text-lg font-bold tabular-nums text-ink">{lockedCount}</p>
          </div>
        </div>
        <div className="rounded-xl border border-line bg-surface px-4 py-3 flex items-center gap-3">
          <div className="size-9 rounded-lg bg-warn-wash text-warn flex items-center justify-center shrink-0">
            <ClockIcon className="size-4" />
          </div>
          <div className="min-w-0">
            <p className="text-xs text-ink-2 truncate">Chờ xác thực</p>
            <p className="font-heading text-lg font-bold tabular-nums text-ink">{pendingCount}</p>
          </div>
        </div>
      </div>

      {/* Master-detail table & side panel */}
      <div className="flex min-h-0 flex-1 flex-col gap-6 lg:flex-row">
        <div className="flex min-h-0 min-w-0 flex-1 flex-col">
          <DataTable
            columns={userColumns}
            items={users}
            getRowId={(u) => u.id}
            selectedRowId={detailTarget?.id}
            onRowSelect={toggleDetailTarget}
            isLoading={listLoading}
            error={listError ?? undefined}
            emptyMessage="Không tìm thấy tài khoản nào phù hợp."
            label="Danh sách tài khoản người dùng"
            toolbar={userToolbar}
            footer={
              <TablePagination
                page={page}
                totalPages={Math.max(1, totalPages)}
                totalCount={totalCount}
                itemLabel="tài khoản"
                disabled={listLoading}
                onPageChange={setPage}
              />
            }
          />
        </div>

        {detailTarget && (
          <DetailPanel
            title={detailTarget.fullName || detailTarget.email}
            description={formatUserRole(detailTarget.role)}
            onClose={() => setDetailTarget(null)}
          >
            <div className="space-y-4">
              <div className="flex flex-wrap items-center gap-2">
                <Badge variant={statusBadgeVariant(detailTarget.status)} className="text-xs">
                  {formatStatusLabel(detailTarget.status)}
                </Badge>
                <Badge variant="outline" className="text-xs">
                  {formatUserRole(detailTarget.role)}
                </Badge>
              </div>

              <div className="rounded-lg border border-line bg-ground p-3.5 text-sm space-y-2.5">
                <div className="flex items-start justify-between gap-3">
                  <span className="text-ink-2 shrink-0">Email</span>
                  <span className="font-medium text-ink text-right break-all">
                    {detailTarget.email}
                  </span>
                </div>
                <div className="flex items-start justify-between gap-3">
                  <span className="text-ink-2 shrink-0">Họ và tên</span>
                  <span className="font-medium text-ink text-right">
                    {detailTarget.fullName || "Chưa cập nhật"}
                  </span>
                </div>
                <div className="flex items-start justify-between gap-3">
                  <span className="text-ink-2 shrink-0">Số điện thoại</span>
                  <span className="font-mono text-ink text-right">
                    {detailTarget.phone ?? "Chưa cung cấp"}
                  </span>
                </div>
                {detailTarget.ageGroup && (
                  <div className="flex items-start justify-between gap-3">
                    <span className="text-ink-2 shrink-0">Nhóm độ tuổi</span>
                    <span className="capitalize text-ink text-right">{detailTarget.ageGroup}</span>
                  </div>
                )}
                {detailTarget.parentConsentStatus && (
                  <div className="flex items-start justify-between gap-3">
                    <span className="text-ink-2 shrink-0">Đồng thuận PH</span>
                    <span className="capitalize text-ink text-right">
                      {detailTarget.parentConsentStatus}
                    </span>
                  </div>
                )}
                <div className="flex items-start justify-between gap-3 pt-1 border-t border-line">
                  <span className="text-ink-2 shrink-0">ID tài khoản</span>
                  <span className="font-mono text-xs text-ink-3 text-right break-all">
                    {detailTarget.id}
                  </span>
                </div>
              </div>

              {(detailTarget.status === "locked" || detailTarget.status === "temp_locked") && (
                <div className="rounded-lg border border-risk/30 bg-risk-wash p-3 text-xs text-risk space-y-1">
                  <div className="flex items-center gap-1.5 font-semibold">
                    <BanIcon className="size-3.5 shrink-0" />
                    <span>Lý do khóa tài khoản</span>
                  </div>
                  <p className="leading-relaxed">
                    {detailTarget.lockReason ||
                      getLockReasonCache(detailTarget.id) ||
                      (detailTarget.status === "temp_locked"
                        ? "Tài khoản bị tạm khóa do nhập sai mật khẩu 5 lần liên tiếp (chờ Quản trị viên mở khóa)."
                        : "Vi phạm quy chế sử dụng và chính sách của hệ thống.")}
                  </p>
                </div>
              )}

              {detailTarget.blockedFeatures && detailTarget.blockedFeatures.length > 0 && (
                <div className="space-y-1.5">
                  <p className="text-xs font-medium text-ink-2">Tính năng đang bị chặn</p>
                  <div className="flex flex-wrap gap-1.5">
                    {detailTarget.blockedFeatures.map((f) => (
                      <Badge key={f} variant="destructive" className="text-xs font-normal">
                        {f}
                      </Badge>
                    ))}
                  </div>
                </div>
              )}

              {(canAdminLock(detailTarget, currentUser?.role, currentUser?.id) ||
                canAdminUnlock(detailTarget, currentUser?.role)) && (
                <div className="flex flex-wrap items-center gap-2 pt-2 border-t border-line">
                  {canAdminLock(detailTarget, currentUser?.role, currentUser?.id) && (
                    <Button
                      type="button"
                      variant="outline"
                      size="sm"
                      onClick={() => openLockDialog(detailTarget)}
                      className="gap-1.5 text-risk border-risk/30 hover:bg-risk-wash hover:text-risk"
                    >
                      <BanIcon className="size-3.5" />
                      Khóa tài khoản
                    </Button>
                  )}
                  {canAdminUnlock(detailTarget, currentUser?.role) && (
                    <Button
                      type="button"
                      variant="outline"
                      size="sm"
                      onClick={() => openUnlockDialog(detailTarget)}
                      className="gap-1.5 text-ok border-ok/30 hover:bg-ok-wash hover:text-ok"
                    >
                      <KeyRoundIcon className="size-3.5" />
                      Mở khóa tài khoản
                    </Button>
                  )}
                </div>
              )}
            </div>
          </DetailPanel>
        )}
      </div>

      {/* [UC-63 & UC-71] Dialog Cấp tài khoản mới (Moderator tạo Center, Admin tạo Moderator hoặc Center) */}
      <Dialog open={isCreateOpen} onOpenChange={setIsCreateOpen}>
        <DialogContent className="max-w-md">
          <DialogHeader>
            <DialogTitle className="flex items-center gap-2">
              <UserPlusIcon className="size-5 text-brand" />
              {isModerator ? "Cấp tài khoản Trung tâm tư vấn" : "Cấp tài khoản mới"}
            </DialogTitle>
            <DialogDescription>
              {isModerator
                ? "Tạo tài khoản đại diện cho Trung tâm tư vấn (tài khoản vai trò center được kích hoạt ngay)."
                : "Tạo tài khoản Kiểm duyệt viên hoặc Trung tâm tư vấn (tài khoản được kích hoạt ngay)."}
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
                {isModerator ? (
                  <div className="h-9 px-3 flex items-center rounded-md border border-input bg-muted/40 text-sm font-medium">
                    Trung tâm tư vấn (Center)
                  </div>
                ) : (
                  <OptionSelect
                    id="create-role"
                    value={createRole}
                    options={createRoleOptions}
                    onChange={(val) => setCreateRole(val as "moderator" | "center")}
                    className="bg-surface"
                  />
                )}
                <FieldDescription>
                  {createRole === "moderator"
                    ? "Tài khoản có quyền kiểm duyệt nội dung, quản lý trung tâm và người dùng cấp dưới."
                    : "Tài khoản đại diện quản lý thông tin và chương trình của trung tâm du học."}
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

      {/* [UC-61] Xác nhận khóa tài khoản */}
      <Dialog open={lockTarget !== null} onOpenChange={(open) => !open && closeLockDialog()}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle className="flex items-center gap-2 text-risk">
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
            <DialogTitle className="flex items-center gap-2 text-ok">
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
              className="bg-ok text-white hover:bg-ok/90 gap-1.5"
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
