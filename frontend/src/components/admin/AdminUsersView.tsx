"use client";

import React, { useCallback, useEffect, useMemo, useState } from "react";
import Link from "next/link";
import {
  BanIcon,
  CakeIcon,
  EyeIcon,
  FilterIcon,
  KeyRoundIcon,
  RotateCcwIcon,
  SearchIcon,
  ShieldCheckIcon,
  UserPlusIcon,
} from "lucide-react";
import { adminUsersApi, ApiError } from "@/lib/api";
import { useAuth } from "@/contexts/AuthContext";
import { formatUserRole, USER_ROLE_LABELS } from "@/lib/user-role-labels";
import type { CreateModeratorAccountRequest, UserDto, UserRole } from "@/types/api";
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
import { PASSWORD_RULE_HINT, checkPhone, emailError, fullNameError, passwordRuleError } from "@/lib/auth-validation";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";

const ACCOUNT_LIST_PAGE_SIZE = 20;

const ACCOUNT_STATUS_OPTIONS = [
  { value: "", label: "Tất cả trạng thái" },
  { value: "active", label: "Đang hoạt động" },
  { value: "unverified", label: "Chưa kích hoạt" },
  { value: "pending", label: "Chờ duyệt" },
  { value: "temp_locked", label: "Tạm khóa" },
  { value: "locked", label: "Đã khóa" },
  // [UC-56 bước 2, UC-80] Tài khoản đã yêu cầu xóa, đang trong 30 ngày chờ xóa hẳn
  { value: "deleted", label: "Chờ xóa" },
] as const;

/** [UC-61] Khóa tài khoản: chỉ Admin (quyết định D2 bản 14/11), khóa được moderator, center, student, parent. */
function canAdminLock(target: UserDto, currentUserRole?: string, currentUserId?: string): boolean {
  if (currentUserId && target.id === currentUserId) return false;
  if (target.status === "locked") return false;
  // [UC-80] Tài khoản chờ xóa không khóa được (server cũng chặn)
  if (target.status === "deleted") return false;
  if (target.role === "admin") return false;

  return currentUserRole === "admin";
}

/** [UC-113] Sửa ngày sinh: chỉ Admin, chỉ tài khoản Học sinh / Phụ huynh (server kiểm tra lại). */
function canAdminEditBirthDate(target: UserDto, currentUserRole?: string): boolean {
  return currentUserRole === "admin" && (target.role === "student" || target.role === "parent");
}

/** yyyy-MM-dd -> dd/MM/yyyy để hiển thị. */
function formatBirthDate(value?: string | null): string {
  if (!value) return "Chưa có";
  const [y, m, d] = value.split("-");
  return y && m && d ? `${d}/${m}/${y}` : value;
}

/** Hôm nay theo giờ Việt Nam (UTC+7), dạng yyyy-MM-dd, làm giới hạn trên cho ô chọn ngày. */
function todayInVietnam(): string {
  return new Date(Date.now() + 7 * 3600 * 1000).toISOString().slice(0, 10);
}

/** [UC-62] Mở khóa tài khoản: chỉ Admin (quyết định D2 bản 14/11), mở khóa được moderator, center, student, parent. */
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
  if (status === "unverified" || status === "pending" || status === "deleted") return "warn";
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
    case "deleted":
      return "Chờ xóa";
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

  // [UC-113] Dialog sửa ngày sinh: tải chi tiết để có ngày sinh hiện tại
  const [birthTarget, setBirthTarget] = useState<UserDto | null>(null);
  const [birthDetailLoading, setBirthDetailLoading] = useState(false);
  const [birthValue, setBirthValue] = useState("");
  const [birthReason, setBirthReason] = useState("");
  const [birthSubmitting, setBirthSubmitting] = useState(false);
  const [birthDialogError, setBirthDialogError] = useState<string | null>(null);

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

  const openBirthDialog = useCallback(
    async (user: UserDto) => {
      if (!canAdminEditBirthDate(user, currentUser?.role)) return;
      setBirthTarget(user);
      setBirthValue(user.dateOfBirth ?? "");
      setBirthReason("");
      setBirthDialogError(null);
      setBirthDetailLoading(true);
      try {
        const detail = await adminUsersApi.getById(user.id);
        setBirthTarget((prev) => (prev?.id === user.id ? { ...prev, ...detail } : prev));
        setBirthValue(detail.dateOfBirth ?? "");
      } catch (err) {
        setBirthDialogError(err instanceof ApiError ? err.message : "Không tải được ngày sinh hiện tại.");
      } finally {
        setBirthDetailLoading(false);
      }
    },
    [currentUser?.role]
  );

  const closeBirthDialog = () => {
    if (birthSubmitting) return;
    setBirthTarget(null);
    setBirthValue("");
    setBirthReason("");
    setBirthDialogError(null);
  };

  const handleConfirmBirthDate = async () => {
    if (!birthTarget || !canAdminEditBirthDate(birthTarget, currentUser?.role)) return;
    const reason = birthReason.trim();
    if (!birthValue) {
      setBirthDialogError("Vui lòng chọn ngày sinh mới.");
      return;
    }
    if (birthValue > todayInVietnam()) {
      setBirthDialogError("Ngày sinh không được ở tương lai.");
      return;
    }
    if (birthValue === birthTarget.dateOfBirth) {
      setBirthDialogError("Ngày sinh mới trùng với ngày sinh hiện tại.");
      return;
    }
    if (!reason) {
      setBirthDialogError("Vui lòng nhập lý do sửa ngày sinh.");
      return;
    }
    setBirthDialogError(null);
    setBirthSubmitting(true);
    try {
      const updated = await adminUsersApi.updateBirthDate(birthTarget.id, { birthDate: birthValue, reason });
      toast.add({
        type: "success",
        priority: "high",
        timeout: 6000,
        title: "Đã sửa ngày sinh",
        description:
          updated.age != null ? `${birthTarget.email} · tuổi hiện tại: ${updated.age}` : birthTarget.email,
      });
      if (detailTarget?.id === birthTarget.id) {
        setDetailTarget((prev) => (prev ? { ...prev, ...updated } : null));
      }
      setBirthTarget(null);
      setBirthValue("");
      setBirthReason("");
      await loadUsers();
    } catch (err) {
      setBirthDialogError(err instanceof ApiError ? err.message : "Không sửa được ngày sinh.");
    } finally {
      setBirthSubmitting(false);
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
    setIsCreateOpen(true);
  };

  const handleCreateAccount = async (e: React.FormEvent) => {
    e.preventDefault();
    setFormError(null);

    if (isModerator) {
      setFormError("Chỉ quản trị viên mới được cấp tài khoản kiểm duyệt viên.");
      return;
    }

    const trimmedEmail = email.trim();
    const trimmedName = fullName.trim();

    // [UC-01] Cùng luật kiểm lỗi với đăng ký (khớp backend): họ tên tối đa 100 ký tự
    const phoneCheck = checkPhone(phone);
    const inputError =
      fullNameError(trimmedName) ?? emailError(trimmedEmail) ?? passwordRuleError(password) ?? phoneCheck.error;
    if (inputError) {
      setFormError(inputError);
      return;
    }
    const phonePayload = phoneCheck.normalized;

    setIsSubmitting(true);
    try {
      const payload: CreateModeratorAccountRequest = {
        email: trimmedEmail,
        password,
        fullName: trimmedName,
        phone: phonePayload,
      };
      const created: UserDto = await adminUsersApi.createModerator(payload);

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
        title: "Đã tạo tài khoản moderator",
        description: created.email,
      });
    } catch (err) {
      setFormError(err instanceof ApiError ? err.message : "Không tạo được tài khoản.");
    } finally {
      setIsSubmitting(false);
    }
  };

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

  // [UC-150] Khung chi tiết tải thêm ngày sinh, tuổi và yêu cầu sửa ngày sinh đang chờ (danh sách không có)
  const detailTargetId = detailTarget?.id;
  const detailTargetRole = detailTarget?.role;
  useEffect(() => {
    if (!detailTargetId || (detailTargetRole !== "student" && detailTargetRole !== "parent")) return;
    let cancelled = false;
    adminUsersApi
      .getById(detailTargetId)
      .then((detail) => {
        if (cancelled) return;
        setDetailTarget((prev) =>
          prev?.id === detailTargetId
            ? {
                ...prev,
                dateOfBirth: detail.dateOfBirth,
                age: detail.age,
                pendingBirthDateRequestId: detail.pendingBirthDateRequestId ?? null,
                pendingBirthDate: detail.pendingBirthDate ?? null,
              }
            : prev
        );
      })
      .catch(() => {
        // Khung chi tiết vẫn hiện thông tin từ danh sách
      });
    return () => {
      cancelled = true;
    };
  }, [detailTargetId, detailTargetRole]);

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
          <button
            type="button"
            onClick={(e) => {
              e.stopPropagation();
              toggleDetailTarget(u);
            }}
            className="cursor-pointer rounded-sm text-left font-medium text-brand underline-offset-2 hover:underline focus-visible:outline-none focus-visible:ring-3 focus-visible:ring-ring/50"
          >
            {u.email}
          </button>
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
        {/* Chỉ admin cấp tài khoản moderator; trung tâm tự đăng ký và chờ duyệt hồ sơ */}
        {!isModerator && (
          <Button type="button" size="sm" onClick={handleOpenCreateModal} className="gap-1.5">
            <UserPlusIcon className="size-4" />
            Cấp tài khoản
          </Button>
        )}
      </>
    ),
  };

  return (
    <div className="flex min-h-0 flex-1 flex-col gap-4">
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
                {detailTarget.dateOfBirth && (
                  <div className="flex items-start justify-between gap-3">
                    <span className="text-ink-2 shrink-0">Ngày sinh</span>
                    <span className="text-ink text-right">
                      {formatBirthDate(detailTarget.dateOfBirth)}
                      {detailTarget.age != null ? ` (${detailTarget.age} tuổi)` : ""}
                    </span>
                  </div>
                )}
                {detailTarget.pendingBirthDateRequestId && (
                  <div className="flex items-start justify-between gap-3">
                    <span className="text-ink-2 shrink-0">Đề nghị sửa ngày sinh</span>
                    <span className="text-ink text-right">
                      {formatBirthDate(detailTarget.pendingBirthDate)}{" "}
                      <Link
                        href={`/admin/birth-date-requests?id=${detailTarget.pendingBirthDateRequestId}`}
                        className="text-primary underline underline-offset-2"
                      >
                        Xem yêu cầu
                      </Link>
                    </span>
                  </div>
                )}
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

              {canAdminEditBirthDate(detailTarget, currentUser?.role) && (
                <div className="flex flex-wrap items-center gap-2 pt-2 border-t border-line">
                  <Button
                    type="button"
                    variant="outline"
                    size="sm"
                    onClick={() => void openBirthDialog(detailTarget)}
                    className="gap-1.5"
                  >
                    <CakeIcon className="size-3.5" />
                    Sửa ngày sinh
                  </Button>
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

      {/* [UC-63] Dialog Cấp tài khoản mới: chỉ Admin tạo Moderator */}
      <Dialog open={isCreateOpen} onOpenChange={setIsCreateOpen}>
        <DialogContent className="max-w-md">
          <DialogHeader>
            <DialogTitle className="flex items-center gap-2">
              <UserPlusIcon className="size-5 text-brand" />
              Cấp tài khoản mới
            </DialogTitle>
            <DialogDescription>
              Tạo tài khoản Kiểm duyệt viên (tài khoản được kích hoạt ngay).
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
                <div
                  id="create-role"
                  className="h-9 px-3 flex items-center rounded-md border border-input bg-muted/40 text-sm font-medium"
                >
                  Kiểm duyệt viên (Moderator)
                </div>
                <FieldDescription>
                  Tài khoản có quyền kiểm duyệt nội dung, duyệt hồ sơ trung tâm và quản lý người dùng cấp dưới.
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
                <FieldDescription>{PASSWORD_RULE_HINT}.</FieldDescription>
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
                <Button type="submit" loading={isSubmitting} className="gap-2 bg-brand text-white hover:bg-brand/90">
                  <ShieldCheckIcon className="size-4" />
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
              loading={lockSubmitting}
              className="gap-1.5"
            >
              <BanIcon className="size-4" />
              Xác nhận khóa
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* [UC-113] Sửa ngày sinh theo yêu cầu hỗ trợ */}
      <Dialog open={birthTarget !== null} onOpenChange={(open) => !open && closeBirthDialog()}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle className="flex items-center gap-2">
              <CakeIcon className="size-5" />
              Sửa ngày sinh
            </DialogTitle>
            <DialogDescription>
              {birthTarget
                ? `Sửa ngày sinh của ${birthTarget.fullName} (${birthTarget.email}) theo yêu cầu hỗ trợ. Hệ thống tính lại tuổi và áp lại các quy tắc theo mốc 16 và 18 tuổi; thao tác được ghi nhật ký.`
                : ""}
            </DialogDescription>
          </DialogHeader>

          <div className="rounded-lg border border-line bg-ground p-3 text-sm flex items-center justify-between gap-3">
            <span className="text-ink-2">Ngày sinh hiện tại</span>
            <span className="font-medium text-ink">
              {birthDetailLoading ? (
                <Spinner className="size-4" />
              ) : (
                <>
                  {formatBirthDate(birthTarget?.dateOfBirth)}
                  {birthTarget?.age != null ? ` (${birthTarget.age} tuổi)` : ""}
                </>
              )}
            </span>
          </div>

          <Field>
            <FieldLabel htmlFor="birth-date">Ngày sinh mới <span className="text-destructive">*</span></FieldLabel>
            <Input
              id="birth-date"
              type="date"
              value={birthValue}
              max={todayInVietnam()}
              onChange={(e) => setBirthValue(e.target.value)}
              disabled={birthSubmitting || birthDetailLoading}
            />
            {birthTarget?.role === "parent" && (
              <FieldDescription>Tài khoản phụ huynh phải từ 18 tuổi.</FieldDescription>
            )}
          </Field>

          <Field>
            <FieldLabel htmlFor="birth-reason">Lý do sửa <span className="text-destructive">*</span></FieldLabel>
            <Input
              id="birth-reason"
              value={birthReason}
              onChange={(e) => setBirthReason(e.target.value)}
              placeholder="Ví dụ: Người dùng gửi yêu cầu hỗ trợ kèm giấy tờ, khai nhầm năm sinh khi đăng ký"
              maxLength={500}
              disabled={birthSubmitting}
            />
          </Field>

          {birthDialogError && (
            <Alert variant="destructive">
              <AlertDescription>{birthDialogError}</AlertDescription>
            </Alert>
          )}

          <DialogFooter>
            <Button type="button" variant="outline" onClick={closeBirthDialog} disabled={birthSubmitting}>
              Hủy
            </Button>
            <Button
              type="button"
              onClick={() => void handleConfirmBirthDate()}
              disabled={birthDetailLoading}
              loading={birthSubmitting}
              className="gap-1.5 bg-brand text-white hover:bg-brand/90"
            >
              <CakeIcon className="size-4" />
              Lưu ngày sinh
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
              loading={unlockSubmitting}
            >
              <KeyRoundIcon className="size-4" />
              Xác nhận mở khóa
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
