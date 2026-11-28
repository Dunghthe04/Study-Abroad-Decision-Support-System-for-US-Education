"use client";

import React, { useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { Building2Icon, GraduationCapIcon, ShieldCheckIcon, UsersIcon } from "lucide-react";
import { centerAccountApi, ApiError } from "@/lib/api";
import { setPendingEmail } from "@/lib/pending-email";
import { useAccountPolicies } from "@/lib/use-account-policies";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button, buttonVariants } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Field, FieldDescription, FieldError, FieldGroup, FieldLabel } from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import { PasswordInput } from "@/components/auth/PasswordInput";
import { PasswordMatchStatus, PasswordStrengthMeter } from "@/components/auth/PasswordFeedback";
import { AUTH_LIMITS, NETWORK_ERROR, PASSWORD_RULE_HINT, checkPhone, confirmPasswordError, emailError, fullNameError, passwordRuleError, requiredTextError } from "@/lib/auth-validation";

interface FieldErrors {
  centerName?: string;
  registrantName?: string;
  email?: string;
  phone?: string;
  password?: string;
  confirmPassword?: string;
}

// [UC-64] Lỗi backend thuộc ô nào thì báo dưới ô đó (theo đầu câu lỗi của CenterAccountService); còn lại báo ở đầu form
function fieldOfServerError(message: string): keyof FieldErrors | null {
  const m = message.toLowerCase();
  if (m.startsWith("email")) return "email";
  if (m.startsWith("số điện thoại")) return "phone";
  if (m.startsWith("tên trung tâm")) return "centerName";
  if (m.startsWith("họ tên")) return "registrantName";
  if (m.startsWith("mật khẩu xác nhận")) return "confirmPassword";
  if (m.startsWith("mật khẩu")) return "password";
  return null;
}

export function RegisterCenterForm() {
  const router = useRouter();

  const [centerName, setCenterName] = useState("");
  const [registrantName, setRegistrantName] = useState("");
  const [registrantPosition, setRegistrantPosition] = useState("");
  const [email, setEmail] = useState("");
  const [phone, setPhone] = useState("");
  const [password, setPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [agreeTerms, setAgreeTerms] = useState(false);
  const [agreeCommitment, setAgreeCommitment] = useState(false);
  // [UC-104] Bản Điều khoản và Chính sách bảo mật đang hiển thị; gửi kèm khi đăng ký để ghi bằng chứng đồng ý
  const accountPolicies = useAccountPolicies();

  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({});
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  const isConfirmTouched = confirmPassword.length > 0;
  const isPasswordMatch = isConfirmTouched && password === confirmPassword;
  const isPasswordMismatch = isConfirmTouched && password !== confirmPassword;
  // Giống form học sinh/phụ huynh: tích đủ các ô đồng ý mới bấm được nút đăng ký
  const allAgreed = agreeTerms && agreeCommitment;

  const clearFieldError = (fieldName: keyof FieldErrors) => {
    if (fieldErrors[fieldName]) {
      setFieldErrors((prev) => {
        const next = { ...prev };
        delete next[fieldName];
        return next;
      });
    }
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    if (!allAgreed) return;

    // [UC-01] Cùng luật kiểm lỗi với đăng ký (khớp backend), báo dưới từng ô
    const errors: FieldErrors = {};
    const centerNameProblem = requiredTextError(centerName, "Tên trung tâm", AUTH_LIMITS.centerNameMax);
    if (centerNameProblem) errors.centerName = centerNameProblem;
    const registrantProblem = fullNameError(registrantName, "Họ tên người đăng ký");
    if (registrantProblem) errors.registrantName = registrantProblem;
    const emailProblem = emailError(email);
    if (emailProblem) errors.email = emailProblem;
    const phoneProblem = checkPhone(phone, true).error;
    if (phoneProblem) errors.phone = phoneProblem;
    const passwordProblem = passwordRuleError(password);
    if (passwordProblem) errors.password = passwordProblem;
    const confirmProblem = confirmPasswordError(password, confirmPassword);
    if (confirmProblem) errors.confirmPassword = confirmProblem;

    if (Object.keys(errors).length > 0) {
      setFieldErrors(errors);
      document.getElementById(Object.keys(errors)[0])?.focus();
      return;
    }

    setFieldErrors({});
    setIsSubmitting(true);
    try {
      await centerAccountApi.register({
        email: email.trim(),
        password,
        confirmPassword,
        centerName: centerName.trim(),
        phone: phone.trim(),
        registrantName: registrantName.trim(),
        registrantPosition: registrantPosition.trim() || undefined,
        agreeTerms,
        agreeTruthfulnessCommitment: agreeCommitment,
        acceptedPolicies: accountPolicies.acceptedPolicies,
      });

      setPendingEmail(email.trim());
      router.push(`/register/center/verify-email?email=${encodeURIComponent(email.trim())}`);
    } catch (err) {
      if (err instanceof ApiError) {
        // Admin có thể vừa công bố bản mới: tải lại để người dùng đọc đúng bản trước khi đồng ý
        if (err.status === 400) accountPolicies.reload();
        const msg = err.message || "Đăng ký không thành công. Vui lòng thử lại.";
        const field = fieldOfServerError(msg);
        if (field) {
          setFieldErrors((prev) => ({ ...prev, [field]: msg }));
          document.getElementById(field)?.focus();
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

  return (
    <div className="space-y-6">
      <div className="space-y-2 text-center">
        <div className="inline-flex h-12 w-12 items-center justify-center rounded-xl bg-brand/10 text-brand">
          <Building2Icon className="h-6 w-6" />
        </div>
        <h1 className="text-2xl font-bold tracking-tight text-ink">
          Đăng ký đối tác Trung tâm tư vấn
        </h1>
        <p className="text-sm text-ink-2">
          Gia nhập danh bạ tư vấn du học Mỹ USDSS, tiếp cận học sinh tiềm năng và nhận tư vấn trực tiếp.
        </p>
      </div>

      {/* Cùng lựa chọn vai trò với trang Đăng ký: quay lại Học sinh / Phụ huynh bằng liên kết, Trung tâm đang chọn */}
      <div className="space-y-2">
        <p className="text-sm font-medium text-ink">Bạn là:</p>
        <div className="grid grid-cols-3 gap-2">
          <Link href="/register" className={buttonVariants({ variant: "outline" })}>
            <GraduationCapIcon aria-hidden="true" />
            Học sinh
          </Link>
          <Link href="/register?role=parent" className={buttonVariants({ variant: "outline" })}>
            <UsersIcon aria-hidden="true" />
            Phụ huynh
          </Link>
          <span
            aria-current="page"
            className={`${buttonVariants({ variant: "default" })} pointer-events-none`}
          >
            <Building2Icon aria-hidden="true" />
            Trung tâm
          </span>
        </div>
      </div>

      {error && (
        <Alert variant="destructive">
          <AlertDescription>{error}</AlertDescription>
        </Alert>
      )}

      {/* noValidate: không dùng thông báo mặc định của trình duyệt, lỗi báo dưới từng ô như form học sinh/phụ huynh */}
      <form onSubmit={handleSubmit} noValidate>
        <FieldGroup>
          <Field data-invalid={Boolean(fieldErrors.centerName)}>
            <FieldLabel htmlFor="centerName">
              Tên trung tâm tư vấn <span className="text-destructive">*</span>
            </FieldLabel>
            <Input
              id="centerName"
              maxLength={AUTH_LIMITS.centerNameMax}
              value={centerName}
              aria-invalid={Boolean(fieldErrors.centerName)}
              aria-describedby={fieldErrors.centerName ? "centerName-error" : undefined}
              onChange={(e) => {
                setCenterName(e.target.value);
                clearFieldError("centerName");
              }}
              placeholder="VD: Trung tâm Tư vấn Du học Quốc tế Ivy"
            />
            {fieldErrors.centerName && <FieldError id="centerName-error">{fieldErrors.centerName}</FieldError>}
          </Field>

          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <Field data-invalid={Boolean(fieldErrors.registrantName)}>
              <FieldLabel htmlFor="registrantName">
                Họ tên người đăng ký <span className="text-destructive">*</span>
              </FieldLabel>
              <Input
                id="registrantName"
                maxLength={AUTH_LIMITS.fullNameMax}
                value={registrantName}
                aria-invalid={Boolean(fieldErrors.registrantName)}
                aria-describedby={fieldErrors.registrantName ? "registrantName-error" : undefined}
                onChange={(e) => {
                  setRegistrantName(e.target.value);
                  clearFieldError("registrantName");
                }}
                placeholder="VD: Nguyễn Văn A"
              />
              {fieldErrors.registrantName && (
                <FieldError id="registrantName-error">{fieldErrors.registrantName}</FieldError>
              )}
            </Field>

            <Field>
              <FieldLabel htmlFor="registrantPosition">Chức vụ</FieldLabel>
              <Input
                id="registrantPosition"
                maxLength={100}
                value={registrantPosition}
                onChange={(e) => setRegistrantPosition(e.target.value)}
                placeholder="VD: Giám đốc điều hành"
              />
            </Field>
          </div>

          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <Field data-invalid={Boolean(fieldErrors.email)}>
              <FieldLabel htmlFor="email">
                Email liên hệ / đăng nhập <span className="text-destructive">*</span>
              </FieldLabel>
              <Input
                id="email"
                type="email"
                maxLength={AUTH_LIMITS.emailMax}
                autoComplete="email"
                value={email}
                aria-invalid={Boolean(fieldErrors.email)}
                aria-describedby={fieldErrors.email ? "email-error" : "email-desc"}
                onChange={(e) => {
                  setEmail(e.target.value);
                  clearFieldError("email");
                }}
                placeholder="contact@center.edu.vn"
              />
              {fieldErrors.email ? (
                <FieldError id="email-error">{fieldErrors.email}</FieldError>
              ) : (
                <FieldDescription id="email-desc">Mã OTP kích hoạt sẽ gửi đến email này</FieldDescription>
              )}
            </Field>

            <Field data-invalid={Boolean(fieldErrors.phone)}>
              <FieldLabel htmlFor="phone">
                Số điện thoại liên hệ <span className="text-destructive">*</span>
              </FieldLabel>
              <Input
                id="phone"
                type="tel"
                maxLength={AUTH_LIMITS.rawPhoneMax}
                autoComplete="tel"
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
                <FieldDescription id="phone-desc">Số điện thoại để học sinh và Moderator liên hệ</FieldDescription>
              )}
            </Field>
          </div>

          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <Field data-invalid={Boolean(fieldErrors.password)}>
              <FieldLabel htmlFor="password">
                Mật khẩu <span className="text-destructive">*</span>
              </FieldLabel>
              <PasswordInput
                id="password"
                maxLength={AUTH_LIMITS.passwordMax}
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

            <Field data-invalid={Boolean(fieldErrors.confirmPassword) || isPasswordMismatch}>
              <FieldLabel htmlFor="confirmPassword">
                Xác nhận mật khẩu <span className="text-destructive">*</span>
              </FieldLabel>
              <PasswordInput
                id="confirmPassword"
                maxLength={AUTH_LIMITS.passwordMax}
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
                <PasswordMatchStatus
                  matched={isPasswordMatch}
                  id={isPasswordMatch ? "confirmPassword-status" : "confirmPassword-error"}
                />
              ) : (
                fieldErrors.confirmPassword && (
                  <FieldError id="confirmPassword-error">{fieldErrors.confirmPassword}</FieldError>
                )
              )}
            </Field>
          </div>

          <Field className="pt-1">
            <div className="flex items-start gap-2.5">
              <Checkbox
                id="agreeTerms"
                checked={agreeTerms}
                onCheckedChange={(c) => setAgreeTerms(c === true)}
                className="mt-0.5 cursor-pointer"
              />
              <label htmlFor="agreeTerms" className="text-sm leading-snug text-ink-2 cursor-pointer select-none">
                Tôi đồng ý với{" "}
                <Link href="/terms" target="_blank" className="font-medium text-brand hover:underline">
                  Điều khoản sử dụng
                </Link>{" "}
                và{" "}
                <Link href="/privacy" target="_blank" className="font-medium text-brand hover:underline">
                  Chính sách quyền riêng tư
                </Link>{" "}
                của USDSS (bao gồm điều khoản lưu trữ dữ liệu định danh và nhật ký giao dịch phục vụ phòng chống gian lận và tuân thủ quy định pháp luật).
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

            <div className="flex items-start gap-2.5">
              <Checkbox
                id="agreeCommitment"
                checked={agreeCommitment}
                onCheckedChange={(c) => setAgreeCommitment(c === true)}
                className="mt-0.5 cursor-pointer"
              />
              <label htmlFor="agreeCommitment" className="text-sm leading-snug text-ink-2 cursor-pointer select-none">
                Tôi cam kết: Các thông tin hồ sơ và mức phí dịch vụ tôi công bố trên USDSS là đúng sự thật và tuân thủ các quy định hiện hành.
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
          </Field>

          <Button
            type="submit"
            size="xl"
            disabled={isSubmitting || !accountPolicies.ready || !allAgreed}
            className="w-full"
            loading={isSubmitting}
          >
            {isSubmitting ? "Đang xử lý đăng ký..." : "Đăng ký tài khoản trung tâm"}
          </Button>
        </FieldGroup>
      </form>

      <div className="flex items-center justify-center gap-2 pt-2 text-sm text-ink-2">
        <span>Đã có tài khoản đối tác?</span>
        <Link href="/login" className="font-semibold text-brand hover:underline">
          Đăng nhập ngay
        </Link>
      </div>

      <div className="rounded-lg border border-line bg-surface-2 p-3 text-xs text-ink-3 flex items-start gap-2">
        <ShieldCheckIcon className="h-4 w-4 text-brand shrink-0 mt-0.5" />
        <span>
          Sau khi kích hoạt email, trung tâm cần tải lên bản scan Giấy chứng nhận ĐKDN để được Moderator phê duyệt cấp tích xanh và xuất hiện trên hệ thống.
        </span>
      </div>
    </div>
  );
}
