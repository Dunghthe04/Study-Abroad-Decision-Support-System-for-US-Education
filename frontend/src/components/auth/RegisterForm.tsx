"use client";

import React, { useEffect, useRef, useState, useSyncExternalStore } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { Building2Icon, GraduationCapIcon, InfoIcon, UsersIcon } from "lucide-react";
import { useAuth } from "@/contexts/AuthContext";
import { ApiError } from "@/lib/api";
import { setPendingEmail } from "@/lib/pending-email";
import { NETWORK_ERROR, PASSWORD_RULE_HINT, checkPhone, confirmPasswordError, emailError, fullNameError, passwordRuleError } from "@/lib/auth-validation";
import { useAccountPolicies } from "@/lib/use-account-policies";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button, buttonVariants } from "@/components/ui/button";
import { Field, FieldDescription, FieldError, FieldGroup, FieldLabel } from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import { PasswordInput } from "@/components/auth/PasswordInput";
import { PasswordMatchStatus, PasswordStrengthMeter } from "@/components/auth/PasswordFeedback";
import { REGISTER_PARENT_EMAIL_KEY } from "@/components/auth/ParentConsentForm";
import { Label } from "@/components/ui/label";
import { Spinner } from "@/components/ui/spinner";
import { cn } from "@/lib/utils";

// [Q11] Ngưỡng tuổi theo luật VN (khớp AgeGroups ở backend; backend tính lại và là nơi quyết định)
const STUDENT_MIN_AGE = 13;
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
  agreeTerms?: string;
}

export function RegisterForm() {
  const router = useRouter();
  const { register, user, isLoading } = useAuth();

  // [AUTH-059] Đã đăng nhập mà mở /register thì chuyển đi như /login: admin/moderator → /admin, còn lại → trang chủ
  useEffect(() => {
    if (!isLoading && user) {
      router.replace(user.role === "admin" || user.role === "moderator" ? "/admin" : "/");
    }
  }, [isLoading, user, router]);

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
  // [AUTH-041] Ô ngày có chữ nhưng trình duyệt không đọc được (vd 31/02) → value rỗng, validity.badInput = true.
  // Đọc lúc bấm gửi qua ref vì khi value giữ nguyên "" thì onChange không chạy.
  const dobInputRef = useRef<HTMLInputElement>(null);
  const [parentEmail, setParentEmail] = useState("");
  const [password, setPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [agreeTerms, setAgreeTerms] = useState(false);
  // [UC-104] Bản Điều khoản và Chính sách bảo mật đang hiển thị; gửi kèm khi đăng ký để ghi bằng chứng đồng ý
  const accountPolicies = useAccountPolicies();

  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({});
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  const isConfirmTouched = confirmPassword.length > 0;
  const isPasswordMatch = isConfirmTouched && password === confirmPassword;
  const isPasswordMismatch = isConfirmTouched && password !== confirmPassword;

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

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);

    const errors: FieldErrors = {};

    // 1. Kiểm tra tính hợp lệ cơ bản ở phía client dưới từng ô
    const nameError = fullNameError(fullName);
    if (nameError) errors.fullName = nameError;

    if (!dateOfBirth && dobInputRef.current?.validity.badInput) {
      errors.dateOfBirth = "Ngày sinh không tồn tại.";
    } else if (!dateOfBirth) {
      errors.dateOfBirth = "Vui lòng chọn ngày sinh.";
    } else if (dateOfBirth > todayInVietnam()) {
      errors.dateOfBirth = "Ngày sinh không được ở tương lai.";
    } else if (age !== null && age > MAX_AGE) {
      errors.dateOfBirth = "Ngày sinh không hợp lệ.";
    } else if (role === "parent" && age !== null && age < ADULT_AGE) {
      errors.dateOfBirth = `Tài khoản phụ huynh phải từ ${ADULT_AGE} tuổi.`;
    } else if (role === "student" && age !== null && age < STUDENT_MIN_AGE) {
      errors.dateOfBirth = `Học sinh phải từ đủ ${STUDENT_MIN_AGE} tuổi để tự đăng ký. Với học sinh nhỏ hơn, vui lòng nhờ Phụ huynh đăng ký và quản lý hồ sơ.`;
    }

    const emailProblem = emailError(email);
    if (emailProblem) errors.email = emailProblem;

    const phoneCheck = checkPhone(phone);
    if (phoneCheck.error) errors.phone = phoneCheck.error;
    const normalizedPhone = phoneCheck.normalized;

    // [UC-01] 8–100 ký tự, không toàn khoảng trắng, đủ chữ hoa, chữ thường, chữ số, ký tự đặc biệt (khớp backend)
    const passwordError = passwordRuleError(password);
    if (passwordError) {
      errors.password = passwordError;
    }

    const confirmError = confirmPasswordError(password, confirmPassword);
    if (confirmError) errors.confirmPassword = confirmError;

    if (parentEmailRequired && !parentEmail.trim()) {
      errors.parentEmail = `Học sinh dưới ${CHILD_MAX_AGE_EXCLUSIVE} tuổi cần nhập email phụ huynh để phụ huynh đồng ý.`;
    } else if (parentEmail.trim()) {
      const parentEmailProblem = emailError(parentEmail, "Email phụ huynh", false);
      if (parentEmailProblem) {
        errors.parentEmail = parentEmailProblem;
      } else if (isMinorStudent && parentEmail.trim().toLowerCase() === email.trim().toLowerCase()) {
        errors.parentEmail = "Email phụ huynh phải khác email của học sinh.";
      }
    }

    if (!agreeTerms) {
      errors.agreeTerms = "Vui lòng đồng ý với Điều khoản sử dụng và Chính sách bảo mật để tiếp tục.";
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
        acceptedPolicies: accountPolicies.acceptedPolicies,
      });

      // 3. Đăng ký thành công -> Chuyển sang màn hình xác thực mã OTP gửi về email
      setPendingEmail(email.trim());
      clearPrefillParentEmail();
      router.push("/verify-email");
    } catch (err) {
      if (err instanceof ApiError) {
        // Admin có thể vừa công bố bản mới: tải lại để người dùng đọc đúng bản trước khi đồng ý
        if (err.status === 400) accountPolicies.reload();
        const msg = err.message || "Đăng ký không thành công. Vui lòng thử lại.";
        if (msg.toLowerCase().includes("email đã được sử dụng") || msg.toLowerCase().includes("email already in use")) {
          setFieldErrors((prev) => ({ ...prev, email: msg }));
        } else if (msg.toLowerCase().includes("số điện thoại")) {
          // [UC-01 E6] Số điện thoại sai định dạng hoặc đã thuộc tài khoản khác: báo tại ô Số điện thoại
          setFieldErrors((prev) => ({ ...prev, phone: msg }));
        } else {
          setError(msg);
        }
      } else {
        setError(NETWORK_ERROR);
      }
    } finally {
      setIsSubmitting(false);
    }
  };

  // [AUTH-059] Chưa biết đã đăng nhập hay chưa, hoặc đã đăng nhập và đang chuyển đi: không hiện form
  if (isLoading || user) {
    return (
      <div className="flex items-center gap-2 text-sm text-ink-2" role="status">
        <Spinner />
        Đang tải...
      </div>
    );
  }

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
                onClick={() => {
                  setRole("student");
                  setFieldErrors({});
                }}
              >
                <GraduationCapIcon aria-hidden="true" />
                Học sinh
              </Button>
              <Button
                type="button"
                variant={role === "parent" ? "default" : "outline"}
                onClick={() => {
                  setRole("parent");
                  setFieldErrors({});
                }}
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
              ref={dobInputRef}
              type="date"
              max={todayInVietnam()}
              value={dateOfBirth}
              aria-invalid={Boolean(fieldErrors.dateOfBirth)}
              aria-describedby={
                fieldErrors.dateOfBirth
                  ? "dateOfBirth-error"
                  : role === "student" && age !== null && age < STUDENT_MIN_AGE
                  ? "dateOfBirth-under-age"
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
            ) : role === "student" && age !== null && age < STUDENT_MIN_AGE ? (
              <div
                id="dateOfBirth-under-age"
                role="alert"
                className="flex items-start gap-1.5 pt-1 text-xs font-medium text-amber-600 dark:text-amber-400"
              >
                <InfoIcon className="mt-0.5 size-3.5 shrink-0" aria-hidden="true" />
                <span>
                  Học sinh phải từ đủ {STUDENT_MIN_AGE} tuổi để tự đăng ký. Với học sinh nhỏ hơn,
                  vui lòng nhờ{" "}
                  <Link href="/register?role=parent" className="underline underline-offset-2">
                    Phụ huynh đăng ký tài khoản
                  </Link>{" "}
                  và quản lý hồ sơ cho bạn.
                </span>
              </div>
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
              placeholder={PASSWORD_RULE_HINT}
            />
            <PasswordStrengthMeter password={password} />
            {fieldErrors.password && <FieldError id="password-error">{fieldErrors.password}</FieldError>}
          </Field>

          {/* Xác nhận mật khẩu */}
          <Field data-invalid={Boolean(fieldErrors.confirmPassword) || isPasswordMismatch}>
            <FieldLabel htmlFor="confirmPassword">
              Xác nhận mật khẩu <span className="text-destructive">*</span>
            </FieldLabel>
            <PasswordInput
              id="confirmPassword"
              maxLength={100}
              autoComplete="new-password"
              value={confirmPassword}
              aria-invalid={Boolean(fieldErrors.confirmPassword) || isPasswordMismatch}
              aria-describedby={
                isConfirmTouched
                  ? isPasswordMatch
                    ? "confirmPassword-status"
                    : "confirmPassword-error"
                  : fieldErrors.confirmPassword
                  ? "confirmPassword-error"
                  : undefined
              }
              onChange={(e) => {
                setConfirmPassword(e.target.value);
                clearFieldError("confirmPassword");
              }}
              placeholder="Nhập lại mật khẩu"
            />
            {isConfirmTouched ? (
              <PasswordMatchStatus matched={isPasswordMatch} id={isPasswordMatch ? "confirmPassword-status" : "confirmPassword-error"} />
            ) : (
              fieldErrors.confirmPassword && (
                <FieldError id="confirmPassword-error">{fieldErrors.confirmPassword}</FieldError>
              )
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

          {/* Đồng ý điều khoản và chính sách bảo mật */}
          <Field data-invalid={Boolean(fieldErrors.agreeTerms)} className="pt-1">
            <div className="flex items-start gap-2.5">
              <input
                id="agreeTerms"
                type="checkbox"
                checked={agreeTerms}
                aria-invalid={Boolean(fieldErrors.agreeTerms)}
                aria-describedby={fieldErrors.agreeTerms ? "agreeTerms-error" : undefined}
                onChange={(e) => {
                  setAgreeTerms(e.target.checked);
                  clearFieldError("agreeTerms");
                }}
                className={cn(
                  "mt-0.5 size-4 rounded border-line text-brand focus:ring-brand cursor-pointer",
                  fieldErrors.agreeTerms && "border-destructive ring-2 ring-destructive/20"
                )}
              />
              <label htmlFor="agreeTerms" className="text-sm leading-snug text-ink-2 cursor-pointer select-none">
                Tôi đồng ý với{" "}
                <Link href="/terms" target="_blank" className="font-medium text-brand hover:underline">
                  Điều khoản sử dụng
                </Link>{" "}
                và{" "}
                <Link href="/privacy" target="_blank" className="font-medium text-brand hover:underline">
                  Chính sách bảo mật
                </Link>{" "}
                của USDSS.
                {accountPolicies.ready && (accountPolicies.policies?.terms || accountPolicies.policies?.privacy) && (
                  <span className="block text-body-s text-ink-3">
                    Phiên bản hiện hành:
                    {accountPolicies.policies?.terms && ` Điều khoản ${accountPolicies.policies.terms.version}`}
                    {accountPolicies.policies?.terms && accountPolicies.policies?.privacy && ","}
                    {accountPolicies.policies?.privacy && ` Chính sách bảo mật ${accountPolicies.policies.privacy.version}`}.
                  </span>
                )}
              </label>
            </div>
            {accountPolicies.error && (
              <FieldError className="pl-6.5">
                Không tải được nội dung điều khoản.{" "}
                <button type="button" className="underline" onClick={accountPolicies.reload}>
                  Thử lại
                </button>
              </FieldError>
            )}
            {fieldErrors.agreeTerms && (
              <FieldError id="agreeTerms-error" className="pl-6.5">
                {fieldErrors.agreeTerms}
              </FieldError>
            )}
          </Field>

          <Button type="submit" size="xl" disabled={isSubmitting || !accountPolicies.ready || !agreeTerms} className="w-full" loading={isSubmitting}>
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
