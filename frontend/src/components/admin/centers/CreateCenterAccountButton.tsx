"use client";

import React, { useState } from "react";
import {
  UserPlusIcon,
  Building2Icon,
  EyeIcon,
  EyeOffIcon,
  SparklesIcon,
} from "lucide-react";
import { adminUsersApi, ApiError } from "@/lib/api";
import { useAuth } from "@/contexts/AuthContext";
import type { CreateCenterAccountRequest, UserDto } from "@/types/api";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Field, FieldLabel } from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import { Spinner } from "@/components/ui/spinner";
import { toast } from "@/components/ui/toast";
import { cn } from "@/lib/utils";

/** Chuẩn hóa SĐT VN (khớp quy tắc backend) */
function normalizeVietnamesePhone(raw: string): { ok: true; value: string } | { ok: false } {
  const cleaned = raw.trim().replace(/[\s.\-()]/g, "");
  let normalized = cleaned;
  if (normalized.startsWith("+84")) normalized = "0" + normalized.slice(3);
  else if (normalized.startsWith("84") && normalized.length === 11) normalized = "0" + normalized.slice(2);
  const regex = /^0(3|5|7|8|9)\d{8}$/;
  return regex.test(normalized) ? { ok: true, value: normalized } : { ok: false };
}

function generateQuickPassword(): string {
  const prefixes = ["Center", "DuHoc", "Study", "USAS", "Partner"];
  const prefix = prefixes[Math.floor(Math.random() * prefixes.length)];
  const num = Math.floor(Math.random() * 900 + 100);
  const specials = ["@", "#", "!", "$", "%"];
  const spec = specials[Math.floor(Math.random() * specials.length)];
  return `${prefix}${num}${spec}A1`;
}

interface FormInnerProps {
  initialName: string;
  initialEmail: string;
  initialPhone: string;
  onClose: () => void;
  onSuccess?: (user: UserDto) => void;
}

function CreateCenterAccountFormContent({
  initialName,
  initialEmail,
  initialPhone,
  onClose,
  onSuccess,
}: FormInnerProps) {
  const [fullName, setFullName] = useState(initialName);
  const [email, setEmail] = useState(initialEmail);
  const [password, setPassword] = useState("");
  const [phone, setPhone] = useState(initialPhone);
  const [showPassword, setShowPassword] = useState(false);

  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  const handleGeneratePassword = () => {
    const pw = generateQuickPassword();
    setPassword(pw);
    setShowPassword(true);
    toast.add({
      type: "info",
      timeout: 3000,
      title: "Đã tạo mật khẩu ngẫu nhiên",
      description: pw,
    });
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);

    const trimmedName = fullName.trim() || null;
    const trimmedEmail = email.trim();

    if (!trimmedEmail) {
      setError("Vui lòng nhập email đăng nhập.");
      return;
    }
    if (trimmedName && trimmedName.length > 100) {
      setError("Họ và tên tối đa 100 ký tự.");
      return;
    }
    if (password.length < 8) {
      setError("Mật khẩu tối thiểu 8 ký tự.");
      return;
    }

    let phonePayload: string | null = null;
    if (phone.trim()) {
      const parsed = normalizeVietnamesePhone(phone);
      if (!parsed.ok) {
        setError("Số điện thoại không đúng định dạng Việt Nam (ví dụ: 0912345678).");
        return;
      }
      phonePayload = parsed.value;
    }

    setIsSubmitting(true);
    try {
      const payload: CreateCenterAccountRequest = {
        email: trimmedEmail,
        password,
        fullName: trimmedName,
        phone: phonePayload,
      };

      const created = await adminUsersApi.createCenter(payload);

      toast.add({
        type: "success",
        priority: "high",
        timeout: 6000,
        title: "Đã tạo tài khoản trung tâm thành công",
        description: `${created.fullName} (${created.email})`,
      });

      onSuccess?.(created);
      onClose();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Không thể tạo tài khoản trung tâm.");
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <form onSubmit={handleSubmit} className="space-y-4 pt-1">
      {/* Security notice */}


      {error && (
        <Alert variant="destructive">
          <AlertDescription>{error}</AlertDescription>
        </Alert>
      )}

      <Field>
        <FieldLabel htmlFor="center-acc-email">Email đăng nhập *</FieldLabel>
        <Input
          id="center-acc-email"
          type="email"
          placeholder="Ví dụ: contact@aau-edu.vn hoặc admin@center.edu.vn"
          value={email}
          onChange={(e) => setEmail(e.target.value)}
          disabled={isSubmitting}
          required
          autoFocus
        />
      </Field>

      <Field>
        <div className="flex items-center justify-between pb-1">
          <FieldLabel htmlFor="center-acc-password">Mật khẩu ban đầu *</FieldLabel>
          <button
            type="button"
            onClick={handleGeneratePassword}
            disabled={isSubmitting}
            className="inline-flex items-center gap-1 text-xs text-brand hover:underline cursor-pointer"
          >
            <SparklesIcon className="size-3" />
            <span>Tạo ngẫu nhiên</span>
          </button>
        </div>
        <div className="relative">
          <Input
            id="center-acc-password"
            type={showPassword ? "text" : "password"}
            placeholder="Tối thiểu 8 ký tự..."
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            disabled={isSubmitting}
            required
            className="pr-10"
          />
          <button
            type="button"
            onClick={() => setShowPassword(!showPassword)}
            className="absolute right-3 top-1/2 -translate-y-1/2 text-muted-foreground hover:text-foreground"
            tabIndex={-1}
          >
            {showPassword ? <EyeOffIcon className="size-4" /> : <EyeIcon className="size-4" />}
          </button>
        </div>
      </Field>

      <Field>
        <FieldLabel htmlFor="center-acc-name">Họ và tên người dùng (tùy chọn)</FieldLabel>
        <Input
          id="center-acc-name"
          placeholder="Ví dụ: Nguyễn Văn A (nếu để trống sẽ tự lấy theo email)"
          value={fullName}
          onChange={(e) => setFullName(e.target.value)}
          disabled={isSubmitting}
        />
      </Field>

      <Field>
        <FieldLabel htmlFor="center-acc-phone">Số điện thoại liên hệ (tùy chọn)</FieldLabel>
        <Input
          id="center-acc-phone"
          placeholder="Ví dụ: 0912345678"
          value={phone}
          onChange={(e) => setPhone(e.target.value)}
          disabled={isSubmitting}
        />
      </Field>

      <DialogFooter className="pt-3">
        <Button type="button" variant="outline" onClick={onClose} disabled={isSubmitting}>
          Hủy
        </Button>
        <Button type="submit" disabled={isSubmitting} className="gap-1.5 bg-brand text-white hover:bg-brand/90">
          {isSubmitting ? (
            <>
              <Spinner className="size-4" />
              <span>Đang tạo...</span>
            </>
          ) : (
            <>
              <UserPlusIcon className="size-4" />
              <span>Tạo tài khoản</span>
            </>
          )}
        </Button>
      </DialogFooter>
    </form>
  );
}

export interface CreateCenterAccountButtonProps {
  className?: string;
  size?: "default" | "sm" | "lg" | "xs" | "icon" | "icon-xs" | "icon-sm" | "icon-lg";
  variant?: "default" | "outline" | "secondary" | "ghost" | "link";
  initialName?: string;
  initialEmail?: string;
  initialPhone?: string;
  onSuccess?: (user: UserDto) => void;
  isOpenControlled?: boolean;
  onOpenChangeControlled?: (open: boolean) => void;
  hideTriggerButton?: boolean;
}

export function CreateCenterAccountButton({
  className,
  size = "sm",
  variant = "outline",
  initialName = "",
  initialEmail = "",
  initialPhone = "",
  onSuccess,
  isOpenControlled,
  onOpenChangeControlled,
  hideTriggerButton = false,
}: CreateCenterAccountButtonProps) {
  const { user } = useAuth();

  const [internalOpen, setInternalOpen] = useState(false);
  const isControlled = typeof isOpenControlled === "boolean";
  const isOpen = isControlled ? isOpenControlled : internalOpen;

  const setIsOpen = (next: boolean) => {
    if (isControlled) {
      onOpenChangeControlled?.(next);
    } else {
      setInternalOpen(next);
    }
  };

  // Chỉ Admin mới có quyền tạo tài khoản
  const isAdmin = user?.role === "admin";
  if (!isAdmin) return null;

  const handleOpen = () => {
    setIsOpen(true);
  };

  const handleClose = () => {
    setIsOpen(false);
  };

  return (
    <>
      {!hideTriggerButton && (
        <Button
          type="button"
          variant={variant}
          size={size}
          onClick={handleOpen}
          className={cn(
            "gap-1.5 border-brand/30 text-brand hover:bg-brand/5 hover:text-brand",
            className
          )}
        >
          <UserPlusIcon className="size-4 shrink-0" aria-hidden />
          <span>Tạo tài khoản cho trung tâm</span>
        </Button>
      )}

      <Dialog open={isOpen} onOpenChange={(open) => !open && handleClose()}>
        <DialogContent className="sm:max-w-lg p-6">
          <DialogHeader>
            <div className="flex items-center gap-2 text-brand">
              <Building2Icon className="size-5" />
              <DialogTitle className="text-lg">Tạo tài khoản cho trung tâm</DialogTitle>
            </div>
            <DialogDescription>
              Cấp tài khoản người dùng với vai trò <strong>Trung tâm</strong> để đại diện cơ sở đăng nhập và quản lý dịch vụ trên USAS.
            </DialogDescription>
          </DialogHeader>

          {isOpen && (
            <CreateCenterAccountFormContent
              key={`${initialName}-${initialEmail}-${initialPhone}`}
              initialName={initialName}
              initialEmail={initialEmail}
              initialPhone={initialPhone}
              onClose={handleClose}
              onSuccess={onSuccess}
            />
          )}
        </DialogContent>
      </Dialog>
    </>
  );
}
