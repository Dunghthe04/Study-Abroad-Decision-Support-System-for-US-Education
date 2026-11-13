"use client";

import React, { useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { Building2Icon, ShieldCheckIcon } from "lucide-react";
import { authApi, ApiError } from "@/lib/api";
import { setPendingEmail } from "@/lib/pending-email";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Field, FieldDescription, FieldGroup, FieldLabel } from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import { PasswordInput } from "@/components/auth/PasswordInput";
import { Spinner } from "@/components/ui/spinner";

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

  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);

    if (!agreeTerms || !agreeCommitment) {
      setError("Vui lòng đồng ý với Điều khoản sử dụng và cam kết tính trung thực của thông tin.");
      return;
    }

    if (password.length < 8) {
      setError("Mật khẩu phải có ít nhất 8 ký tự.");
      return;
    }

    if (password !== confirmPassword) {
      setError("Mật khẩu xác nhận không khớp.");
      return;
    }

    setIsSubmitting(true);
    try {
      await authApi.registerCenter({
        email: email.trim(),
        password,
        confirmPassword,
        centerName: centerName.trim(),
        phone: phone.trim(),
        registrantName: registrantName.trim(),
        registrantPosition: registrantPosition.trim() || undefined,
        agreeTerms,
        agreeTruthfulnessCommitment: agreeCommitment,
      });

      setPendingEmail(email.trim());
      router.push(`/register/center/verify-email?email=${encodeURIComponent(email.trim())}`);
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.message);
      } else {
        setError("Đăng ký không thành công. Vui lòng kiểm tra lại thông tin và thử lại.");
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
          Gia nhập danh bạ tư vấn du học Mỹ USAS, tiếp cận học sinh tiềm năng và nhận tư vấn trực tiếp.
        </p>
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
                placeholder="Tối thiểu 8 ký tự"
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
                của USAS.
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
                Tôi cam kết: Các thông tin hồ sơ và mức phí dịch vụ tôi công bố trên USAS là đúng sự thật và tuân thủ các quy định hiện hành.
              </span>
            </label>
          </div>

          <Button type="submit" size="xl" disabled={isSubmitting} className="w-full mt-2">
            {isSubmitting && <Spinner data-icon="inline-start" />}
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

