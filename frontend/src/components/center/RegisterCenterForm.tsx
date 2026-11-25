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
import { Field, FieldDescription, FieldGroup, FieldLabel } from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import { PasswordInput } from "@/components/auth/PasswordInput";
import { AUTH_LIMITS, NETWORK_ERROR, PASSWORD_RULE_HINT, checkPhone, confirmPasswordError, emailError, fullNameError, passwordRuleError, requiredTextError } from "@/lib/auth-validation";

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

  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);

    if (!agreeTerms || !agreeCommitment) {
      setError("Vui lòng đồng ý với Điều khoản sử dụng và cam kết tính trung thực của thông tin.");
      return;
    }

    // [UC-01] Cùng luật kiểm lỗi với đăng ký (khớp backend), báo lỗi đầu tiên gặp
    const inputError =
      requiredTextError(centerName, "Tên trung tâm", AUTH_LIMITS.centerNameMax) ??
      fullNameError(registrantName, "Họ tên người đăng ký") ??
      emailError(email) ??
      checkPhone(phone, true).error ??
      passwordRuleError(password) ??
      confirmPasswordError(password, confirmPassword);
    if (inputError) {
      setError(inputError);
      return;
    }

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
        setError(err.message);
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

      <form onSubmit={handleSubmit}>
        <FieldGroup>
          <Field>
            <FieldLabel htmlFor="centerName">Tên trung tâm tư vấn</FieldLabel>
            <Input
              id="centerName"
              required
              maxLength={200}
              value={centerName}
              onChange={(e) => setCenterName(e.target.value)}
              placeholder="VD: Trung tâm Tư vấn Du học Quốc tế Ivy"
            />
          </Field>

          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <Field>
              <FieldLabel htmlFor="registrantName">Họ tên người đăng ký</FieldLabel>
              <Input
                id="registrantName"
                required
                maxLength={100}
                value={registrantName}
                onChange={(e) => setRegistrantName(e.target.value)}
                placeholder="VD: Nguyễn Văn A"
              />
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
            <Field>
              <FieldLabel htmlFor="email">Email liên hệ / đăng nhập</FieldLabel>
              <Input
                id="email"
                type="email"
                required
                maxLength={100}
                autoComplete="email"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                placeholder="contact@center.edu.vn"
              />
              <FieldDescription>Mã OTP kích hoạt sẽ gửi đến email này</FieldDescription>
            </Field>

            <Field>
              <FieldLabel htmlFor="phone">Số điện thoại liên hệ</FieldLabel>
              <Input
                id="phone"
                type="tel"
                required
                maxLength={20}
                autoComplete="tel"
                value={phone}
                onChange={(e) => setPhone(e.target.value)}
                placeholder="0912345678"
              />
              <FieldDescription>Số điện thoại để học sinh và Moderator liên hệ</FieldDescription>
            </Field>
          </div>

          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <Field>
              <FieldLabel htmlFor="password">Mật khẩu</FieldLabel>
              <PasswordInput
                id="password"
                required
                minLength={8}
                maxLength={100}
                autoComplete="new-password"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                placeholder={PASSWORD_RULE_HINT}
              />
            </Field>

            <Field>
              <FieldLabel htmlFor="confirmPassword">Xác nhận mật khẩu</FieldLabel>
              <PasswordInput
                id="confirmPassword"
                required
                minLength={8}
                maxLength={100}
                autoComplete="new-password"
                value={confirmPassword}
                onChange={(e) => setConfirmPassword(e.target.value)}
                placeholder="Nhập lại mật khẩu"
              />
            </Field>
          </div>

          <div className="space-y-3 pt-2 text-sm text-ink-2">
            <label className="flex items-start gap-2.5 cursor-pointer">
              <input
                type="checkbox"
                required
                checked={agreeTerms}
                onChange={(e) => setAgreeTerms(e.target.checked)}
                className="mt-1 h-4 w-4 rounded border-line text-brand focus:ring-brand"
              />
              <span>
                Tôi đồng ý với{" "}
                <Link href="/terms" target="_blank" className="font-medium text-brand hover:underline">
                  Điều khoản sử dụng
                </Link>{" "}
                và{" "}
                <Link href="/privacy" target="_blank" className="font-medium text-brand hover:underline">
                  Chính sách quyền riêng tư
                </Link>{" "}
                của USDSS (bao gồm điều khoản lưu trữ dữ liệu định danh và nhật ký giao dịch phục vụ phòng chống gian lận và tuân thủ quy định pháp luật).
              </span>
            </label>

            <label className="flex items-start gap-2.5 cursor-pointer">
              <input
                type="checkbox"
                required
                checked={agreeCommitment}
                onChange={(e) => setAgreeCommitment(e.target.checked)}
                className="mt-1 h-4 w-4 rounded border-line text-brand focus:ring-brand"
              />
              <span>
                Tôi cam kết: Các thông tin hồ sơ và mức phí dịch vụ tôi công bố trên USDSS là đúng sự thật và tuân thủ các quy định hiện hành.
              </span>
            </label>
          </div>

          {accountPolicies.error && (
            <Alert variant="destructive">
              <AlertDescription>
                Không tải được nội dung điều khoản.{" "}
                <button type="button" className="underline" onClick={accountPolicies.reload}>
                  Thử lại
                </button>
              </AlertDescription>
            </Alert>
          )}

          <Button type="submit" size="xl" disabled={isSubmitting || !accountPolicies.ready} className="w-full mt-2" loading={isSubmitting}>
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

