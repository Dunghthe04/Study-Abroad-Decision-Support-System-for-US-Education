"use client";

import React, { useState, useSyncExternalStore } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { Building2Icon, GraduationCapIcon, InfoIcon, UsersIcon } from "lucide-react";
import { useAuth } from "@/contexts/AuthContext";
import { ApiError } from "@/lib/api";
import { setPendingEmail } from "@/lib/pending-email";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button, buttonVariants } from "@/components/ui/button";
import { Field, FieldDescription, FieldError, FieldGroup, FieldLabel } from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import { PasswordInput } from "@/components/auth/PasswordInput";
import { REGISTER_PARENT_EMAIL_KEY } from "@/components/auth/ParentConsentForm";
import { Label } from "@/components/ui/label";

// [Q11] Ngưỡng tuổi theo luật VN (khớp AgeGroups ở backend; backend tính lại và là nơi quyết định)
const CHILD_MAX_AGE_EXCLUSIVE = 16;
const ADULT_AGE = 18;
const MAX_AGE = 100;

// Ngày hôm nay theo giờ Việt Nam, dạng yyyy-MM-dd
function todayInVietnam(): string {
  return new Intl.DateTimeFormat("en-CA", { timeZone: "Asia/Ho_Chi_Minh" }).format(new Date());
}

// Tuổi tròn từ ngày sinh yyyy-MM-dd; null nếu chưa nhập hoặc sai định dạng
function ageFrom(dob: string): number | null {
  const m = /^(\d{4})-(\d{2})-(\d{2})$/.exec(dob);
  if (!m) return null;
  const [ty, tm, td] = todayInVietnam().split("-").map(Number);
  const [y, mo, d] = [Number(m[1]), Number(m[2]), Number(m[3])];
  let age = ty - y;
  if (tm < mo || (tm === mo && td < d)) age--;
  return age;
}

// sessionStorage / URL không đổi trong cùng tab nên không cần subscribe; server render trả ""
const noopSubscribe = () => () => {};

function readUrlRole(): string {
  return new URLSearchParams(window.location.search).get("role") ?? "";
}

function readPrefillParentEmail(): string {
  try {
    return sessionStorage.getItem(REGISTER_PARENT_EMAIL_KEY) ?? "";
  } catch {
    return "";
  }
}

function clearPrefillParentEmail() {
  try {
    sessionStorage.removeItem(REGISTER_PARENT_EMAIL_KEY);
  } catch {
    // bỏ qua
  }
}

interface FieldErrors {
  fullName?: string;
  dateOfBirth?: string;
  email?: string;
  phone?: string;
  password?: string;
  confirmPassword?: string;
  parentEmail?: string;
}

export function RegisterForm() {
  const router = useRouter();
  const { register } = useAuth();

  const [fullName, setFullName] = useState("");
  // Mở từ trang phụ huynh đồng ý: ?role=parent chọn sẵn Phụ huynh, email lấy từ sessionStorage (không có trên URL).
  // Người dùng bấm chọn / sửa ô thì dùng giá trị đã chọn.
  const urlRole = useSyncExternalStore(noopSubscribe, readUrlRole, () => "");
  const prefillEmail = useSyncExternalStore(noopSubscribe, readPrefillParentEmail, () => "");
  const [typedEmail, setEmail] = useState<string | null>(null);
  const email = typedEmail ?? (urlRole === "parent" ? prefillEmail : "");
  const [phone, setPhone] = useState("");
  const [chosenRole, setRole] = useState<"student" | "parent" | null>(null);
  const role = chosenRole ?? (urlRole === "parent" ? "parent" : "student");
  const [dateOfBirth, setDateOfBirth] = useState("");
  const [parentEmail, setParentEmail] = useState("");
  const [password, setPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");

  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({});
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  const clearFieldError = (fieldName: keyof FieldErrors) => {
    if (fieldErrors[fieldName]) {
      setFieldErrors((prev) => {
        const next = { ...prev };
        delete next[fieldName];
        return next;
      });
    }
  };

  const age = ageFrom(dateOfBirth);
  const isMinorStudent = role === "student" && age !== null && age >= 0 && age < ADULT_AGE;
  const parentEmailRequired = isMinorStudent && age !== null && age < CHILD_MAX_AGE_EXCLUSIVE;

  // Hàm kiểm tra và chuẩn hóa số điện thoại Việt Nam
  const validateVietnamesePhone = (raw: string): { valid: boolean; normalized?: string } => {
    const cleaned = raw.trim().replace(/[\s.\-()]/g, "");
    let normalized = cleaned;
    if (normalized.startsWith("+84")) {
      normalized = "0" + normalized.slice(3);
    } else if (normalized.startsWith("84") && normalized.length === 11) {
      normalized = "0" + normalized.slice(2);
    }
    // Định dạng di động 10 số Việt Nam: 03x, 05x, 07x, 08x, 09x
    const regex = /^0(3|5|7|8|9)\d{8}$/;
    if (regex.test(normalized)) {
      return { valid: true, normalized };
    }
    return { valid: false };
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);

    const errors: FieldErrors = {};

    // 1. Kiểm tra tính hợp lệ cơ bản ở phía client dưới từng ô
    if (!fullName.trim()) {
      errors.fullName = "Vui lòng nhập họ và tên.";
    } else if (fullName.trim().length > 100) {
      errors.fullName = "Họ và tên không được vượt quá 100 ký tự.";
    }

    if (!dateOfBirth) {
      errors.dateOfBirth = "Vui lòng chọn ngày sinh.";
    } else if (dateOfBirth > todayInVietnam()) {
      errors.dateOfBirth = "Ngày sinh không được ở tương lai.";
    } else if (age !== null && age > MAX_AGE) {
      errors.dateOfBirth = "Ngày sinh không hợp lệ.";
    } else if (role === "parent" && age !== null && age < ADULT_AGE) {
      errors.dateOfBirth = `Tài khoản phụ huynh phải từ ${ADULT_AGE} tuổi.`;
    }

    if (!email.trim()) {
      errors.email = "Vui lòng nhập email.";
    } else if (email.trim().length > 100) {
      errors.email = "Email không được vượt quá 100 ký tự.";
    } else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email.trim())) {
      errors.email = "Email không đúng định dạng.";
    }

    let normalizedPhone: string | null = null;
    if (phone.trim()) {
      if (phone.trim().length > 20) {
        errors.phone = "Số điện thoại không được vượt quá 20 ký tự.";
      } else {
        const phoneValidation = validateVietnamesePhone(phone);
        if (!phoneValidation.valid) {
          errors.phone = "Số điện thoại không đúng định dạng Việt Nam (ví dụ: 0912 345 678 hoặc +84912345678).";
        } else {
          normalizedPhone = phoneValidation.normalized!;
        }
      }
    }

    if (!password) {
      errors.password = "Vui lòng nhập mật khẩu.";
    } else if (password.length < 8) {
      errors.password = "Mật khẩu phải có tối thiểu 8 ký tự.";
    } else if (password.length > 100) {
      errors.password = "Mật khẩu không được vượt quá 100 ký tự.";
    }

    if (!confirmPassword) {
      errors.confirmPassword = "Vui lòng xác nhận mật khẩu.";
    } else if (password && confirmPassword !== password) {
      errors.confirmPassword = "Mật khẩu xác nhận không khớp.";
    }

    if (parentEmailRequired && !parentEmail.trim()) {
      errors.parentEmail = `Học sinh dưới ${CHILD_MAX_AGE_EXCLUSIVE} tuổi cần nhập email phụ huynh để phụ huynh đồng ý.`;
    } else if (parentEmail.trim()) {
      if (parentEmail.trim().length > 100) {
        errors.parentEmail = "Email phụ huynh không được vượt quá 100 ký tự.";
      } else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(parentEmail.trim())) {
        errors.parentEmail = "Email phụ huynh không đúng định dạng.";
      } else if (isMinorStudent && parentEmail.trim().toLowerCase() === email.trim().toLowerCase()) {
        errors.parentEmail = "Email phụ huynh phải khác email của học sinh.";
      }
    }

    if (Object.keys(errors).length > 0) {
      setFieldErrors(errors);
      const firstFieldId = Object.keys(errors)[0];
      const targetElement = document.getElementById(firstFieldId);
      if (targetElement) {
        targetElement.focus();
      }
      return;
    }

    setFieldErrors({});
    setIsSubmitting(true);
    try {
      // 2. Gọi API đăng ký
      await register({
        fullName: fullName.trim(),
        email: email.trim(),
        phone: normalizedPhone,
        role,
        dateOfBirth,
        parentEmail: isMinorStudent && parentEmail.trim() ? parentEmail.trim() : null,
        password,
      });

      // 3. Đăng ký thành công -> Chuyển sang màn hình xác thực mã OTP gửi về email
      setPendingEmail(email.trim());
      clearPrefillParentEmail();
      router.push("/verify-email");
    } catch (err) {
      if (err instanceof ApiError) {
        const msg = err.message || "Đăng ký không thành công. Vui lòng thử lại.";
        if (msg.toLowerCase().includes("email đã được sử dụng") || msg.toLowerCase().includes("email already in use")) {
          setFieldErrors((prev) => ({ ...prev, email: msg }));
        } else {
          setError(msg);
        }
      } else {
        setError("Có lỗi kết nối máy chủ. Vui lòng thử lại sau.");
      }
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className="space-y-6">
      <div className="space-y-1">
        <h1 className="font-heading text-2xl font-semibold tracking-tight text-ink sm:text-3xl">
          Đăng ký tài khoản
        </h1>
        <p className="text-sm text-ink-2">
          Tạo tài khoản để nhận tư vấn du học Mỹ và quản lý hồ sơ cá nhân
        </p>
      </div>

      {error && (
        <Alert variant="destructive">
          <AlertDescription>{error}</AlertDescription>
        </Alert>
      )}

      <form onSubmit={handleSubmit} autoComplete="off" noValidate>
        <FieldGroup>
          {/* Lựa chọn vai trò: Học sinh, Phụ huynh; Trung tâm (UC-64) có form riêng nên chuyển sang trang đăng ký trung tâm */}
          <Field>
            <Label>Bạn là:</Label>
            <div className="grid grid-cols-3 gap-2">
              <Button
                type="button"
                variant={role === "student" ? "default" : "outline"}
                onClick={() => setRole("student")}
              >
                <GraduationCapIcon aria-hidden="true" />
                Học sinh
              </Button>
              <Button
                type="button"
                variant={role === "parent" ? "default" : "outline"}
                onClick={() => setRole("parent")}
              >
                <UsersIcon aria-hidden="true" />
                Phụ huynh
              </Button>
              <Link href="/register/center" className={buttonVariants({ variant: "outline" })}>
                <Building2Icon aria-hidden="true" />
                Trung tâm
              </Link>
            </div>
          </Field>

          {/* Họ và tên */}
          <Field data-invalid={Boolean(fieldErrors.fullName)}>
            <FieldLabel htmlFor="fullName">
              Họ và tên <span className="text-destructive">*</span>
            </FieldLabel>
            <Input
              id="fullName"
              type="text"
              maxLength={100}
              value={fullName}
              aria-invalid={Boolean(fieldErrors.fullName)}
              aria-describedby={fieldErrors.fullName ? "fullName-error" : undefined}
              onChange={(e) => {
                setFullName(e.target.value);
                clearFieldError("fullName");
              }}
              placeholder="Nguyễn Văn A"
            />
            {fieldErrors.fullName && <FieldError id="fullName-error">{fieldErrors.fullName}</FieldError>}
          </Field>

          {/* Ngày sinh: dùng để tính nhóm tuổi */}
          <Field data-invalid={Boolean(fieldErrors.dateOfBirth)}>
            <FieldLabel htmlFor="dateOfBirth">
              Ngày sinh <span className="text-destructive">*</span>
            </FieldLabel>
            <Input
              id="dateOfBirth"
              type="date"
              max={todayInVietnam()}
              value={dateOfBirth}
              aria-invalid={Boolean(fieldErrors.dateOfBirth)}
              aria-describedby={
                fieldErrors.dateOfBirth
                  ? "dateOfBirth-error"
                  : role === "parent"
                  ? "dateOfBirth-desc"
                  : undefined
              }
              onChange={(e) => {
                setDateOfBirth(e.target.value);
                clearFieldError("dateOfBirth");
              }}
            />
            {fieldErrors.dateOfBirth ? (
              <FieldError id="dateOfBirth-error">{fieldErrors.dateOfBirth}</FieldError>
            ) : (
              role === "parent" && (
                <FieldDescription id="dateOfBirth-desc">
                  Tài khoản phụ huynh dành cho người từ {ADULT_AGE} tuổi.
                </FieldDescription>
              )
            )}
          </Field>

          {/* Email đăng nhập */}
          <Field data-invalid={Boolean(fieldErrors.email)}>
            <FieldLabel htmlFor="email">
              Email <span className="text-destructive">*</span>
            </FieldLabel>
            <Input
              id="email"
              type="email"
              maxLength={100}
              autoComplete="email"
              value={email}
              aria-invalid={Boolean(fieldErrors.email)}
              aria-describedby={fieldErrors.email ? "email-error" : undefined}
              onChange={(e) => {
                setEmail(e.target.value);
                clearFieldError("email");
              }}
              placeholder="example@gmail.com"
            />
            {fieldErrors.email && <FieldError id="email-error">{fieldErrors.email}</FieldError>}
          </Field>

          {/* Số điện thoại Việt Nam */}
          <Field data-invalid={Boolean(fieldErrors.phone)}>
            <FieldLabel htmlFor="phone">Số điện thoại liên hệ (Việt Nam)</FieldLabel>
            <Input
              id="phone"
              type="tel"
              maxLength={20}
              value={phone}
              aria-invalid={Boolean(fieldErrors.phone)}
              aria-describedby={fieldErrors.phone ? "phone-error" : "phone-desc"}
              onChange={(e) => {
                setPhone(e.target.value);
                clearFieldError("phone");
              }}
              placeholder="0912 345 678 hoặc +84912345678"
            />
            {fieldErrors.phone ? (
              <FieldError id="phone-error">{fieldErrors.phone}</FieldError>
            ) : (
              <FieldDescription id="phone-desc">
                Chỉ chấp nhận số điện thoại di động Việt Nam (10 chữ số, đầu số 03, 05, 07, 08, 09).
              </FieldDescription>
            )}
          </Field>

          {/* Mật khẩu */}
          <Field data-invalid={Boolean(fieldErrors.password)}>
            <FieldLabel htmlFor="password">
              Mật khẩu <span className="text-destructive">*</span>
            </FieldLabel>
            <PasswordInput
              id="password"
              minLength={8}
              maxLength={100}
              autoComplete="new-password"
              value={password}
              aria-invalid={Boolean(fieldErrors.password)}
              aria-describedby={fieldErrors.password ? "password-error" : undefined}
              onChange={(e) => {
                setPassword(e.target.value);
                clearFieldError("password");
              }}
              placeholder="Tối thiểu 8 ký tự, tối đa 100"
            />
            {fieldErrors.password && <FieldError id="password-error">{fieldErrors.password}</FieldError>}
          </Field>

          {/* Xác nhận mật khẩu */}
          <Field data-invalid={Boolean(fieldErrors.confirmPassword)}>
            <FieldLabel htmlFor="confirmPassword">
              Xác nhận mật khẩu <span className="text-destructive">*</span>
            </FieldLabel>
            <PasswordInput
              id="confirmPassword"
              maxLength={100}
              autoComplete="new-password"
              value={confirmPassword}
              aria-invalid={Boolean(fieldErrors.confirmPassword)}
              aria-describedby={fieldErrors.confirmPassword ? "confirmPassword-error" : undefined}
              onChange={(e) => {
                setConfirmPassword(e.target.value);
                clearFieldError("confirmPassword");
              }}
              placeholder="Nhập lại mật khẩu"
            />
            {fieldErrors.confirmPassword && (
              <FieldError id="confirmPassword-error">{fieldErrors.confirmPassword}</FieldError>
            )}
          </Field>

          {/* Email phụ huynh: học sinh dưới 18 (bắt buộc khi dưới 16) */}
          {isMinorStudent && (
            <>
              <Alert role="status">
                <InfoIcon aria-hidden="true" />
                <AlertDescription>
                  {parentEmailRequired
                    ? `Theo Luật Bảo vệ dữ liệu cá nhân, với học sinh dưới ${CHILD_MAX_AGE_EXCLUSIVE} tuổi hệ thống cần cha mẹ hoặc người giám hộ đồng ý trước khi lưu và phân tích hồ sơ. Bạn vẫn dùng được tài khoản; các chức năng hồ sơ, gợi ý trường và tư vấn AI sẽ mở khi phụ huynh bấm đồng ý trong email mời.`
                    : `Bạn chưa đủ ${ADULT_AGE} tuổi: có thể nhập email phụ huynh để mời liên kết tài khoản (không bắt buộc). Một số chức năng như chia sẻ hồ sơ cho trung tâm chỉ dành cho người từ ${ADULT_AGE} tuổi.`}
                </AlertDescription>
              </Alert>
              <Field data-invalid={Boolean(fieldErrors.parentEmail)}>
                <FieldLabel htmlFor="parentEmail">
                  Email phụ huynh {parentEmailRequired && <span className="text-destructive">*</span>}
                </FieldLabel>
                <Input
                  id="parentEmail"
                  type="email"
                  maxLength={100}
                  value={parentEmail}
                  aria-invalid={Boolean(fieldErrors.parentEmail)}
                  aria-describedby={fieldErrors.parentEmail ? "parentEmail-error" : "parentEmail-desc"}
                  onChange={(e) => {
                    setParentEmail(e.target.value);
                    clearFieldError("parentEmail");
                  }}
                  placeholder="phuhuynh@gmail.com"
                />
                {fieldErrors.parentEmail ? (
                  <FieldError id="parentEmail-error">{fieldErrors.parentEmail}</FieldError>
                ) : (
                  <FieldDescription id="parentEmail-desc">
                    Lời mời được gửi tới email này sau khi bạn xác thực email của mình.
                  </FieldDescription>
                )}
              </Field>
            </>
          )}

          <Button type="submit" size="xl" disabled={isSubmitting} className="w-full" loading={isSubmitting}>
            {isSubmitting ? "Đang xử lý..." : "Tạo tài khoản"}
          </Button>
        </FieldGroup>
      </form>

      <div className="pt-2 text-center text-sm text-ink-2">
        <p>
          Đã có tài khoản?{" "}
          <Link href="/login" className="font-semibold text-brand hover:underline">
            Đăng nhập ngay
          </Link>
        </p>
      </div>
    </div>
  );
}
