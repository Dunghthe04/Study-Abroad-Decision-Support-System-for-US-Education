"use client";

import React, { useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { Building2Icon, GraduationCapIcon, InfoIcon, UsersIcon } from "lucide-react";
import { useAuth } from "@/contexts/AuthContext";
import { ApiError } from "@/lib/api";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Checkbox } from "@/components/ui/checkbox";
import { Field, FieldDescription, FieldGroup, FieldLabel } from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Spinner } from "@/components/ui/spinner";

export function RegisterForm() {
  const router = useRouter();
  const { register } = useAuth();

  const [fullName, setFullName] = useState("");
  const [email, setEmail] = useState("");
  const [phone, setPhone] = useState("");
  const [role, setRole] = useState<"student" | "parent" | "center">("student");
  const [parentAcknowledged, setParentAcknowledged] = useState(false);
  const [password, setPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");

  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

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

    // 1. Kiểm tra tính hợp lệ cơ bản ở phía client
    if (!fullName.trim()) {
      setError(role === "center" ? "Vui lòng nhập tên trung tâm hoặc người đại diện." : "Vui lòng nhập họ và tên.");
      return;
    }
    if (fullName.trim().length > 100) {
      setError("Họ và tên không được vượt quá 100 ký tự.");
      return;
    }
    if (!email.trim()) {
      setError("Vui lòng nhập email.");
      return;
    }
    if (email.trim().length > 100) {
      setError("Email không được vượt quá 100 ký tự.");
      return;
    }
    if (password.length < 8) {
      setError("Mật khẩu phải có tối thiểu 8 ký tự.");
      return;
    }
    if (password.length > 100) {
      setError("Mật khẩu không được vượt quá 100 ký tự.");
      return;
    }
    if (password !== confirmPassword) {
      setError("Mật khẩu xác nhận không khớp.");
      return;
    }
    if (role === "student" && !parentAcknowledged) {
      setError("Học sinh cần xác nhận phụ huynh đã biết về việc tạo tài khoản này.");
      return;
    }

    // 2. Validate số điện thoại Việt Nam nếu có nhập
    let normalizedPhone: string | null = null;
    if (phone.trim()) {
      if (phone.trim().length > 20) {
        setError("Số điện thoại không được vượt quá 20 ký tự.");
        return;
      }
      const phoneValidation = validateVietnamesePhone(phone);
      if (!phoneValidation.valid) {
        setError("Số điện thoại không đúng định dạng Việt Nam (ví dụ: 0912345678 hoặc +84912345678).");
        return;
      }
      normalizedPhone = phoneValidation.normalized!;
    }

    setIsSubmitting(true);
    try {
      // 3. Gọi API đăng ký
      await register({
        fullName: fullName.trim(),
        email: email.trim(),
        phone: normalizedPhone,
        role,
        parentAcknowledged: role === "student" ? true : undefined,
        password,
      });

      // 4. Đăng ký thành công -> Chuyển sang màn hình xác thực mã OTP gửi về email
      router.push(`/verify-email?email=${encodeURIComponent(email.trim())}`);
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.message || "Đăng ký không thành công. Vui lòng thử lại.");
      } else {
        setError("Có lỗi kết nối máy chủ. Vui lòng thử lại sau.");
      }
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <Card className="mx-auto w-full max-w-md">
      <CardHeader className="text-center">
        <CardTitle>
          <h1>Đăng ký tài khoản</h1>
        </CardTitle>
        <CardDescription>
          Tạo tài khoản để nhận tư vấn du học Mỹ và quản lý hồ sơ cá nhân
        </CardDescription>
      </CardHeader>

      <CardContent className="space-y-6">
        {error && (
          <Alert variant="destructive">
            <AlertDescription>{error}</AlertDescription>
          </Alert>
        )}

        <form onSubmit={handleSubmit}>
          <FieldGroup>
            {/* Lựa chọn vai trò: Học sinh, Phụ huynh, Trung tâm */}
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
              <Button
                type="button"
                variant={role === "center" ? "default" : "outline"}
                onClick={() => setRole("center")}
              >
                <Building2Icon aria-hidden="true" />
                Trung tâm
              </Button>
              </div>
            </Field>

            {/* Thông báo riêng cho tài khoản trung tâm */}
            {role === "center" && (
              <Alert variant="warning" role="status">
                <InfoIcon aria-hidden="true" />
                <AlertDescription>
                  Tài khoản Trung tâm du học sẽ ở trạng thái chờ duyệt (Pending) bởi Ban quản trị trước khi có thể công khai hồ sơ.
                </AlertDescription>
              </Alert>
            )}

            {/* Họ và tên */}
            <Field>
              <FieldLabel htmlFor="fullName">
                {role === "center" ? "Tên trung tâm / Đại diện" : "Họ và tên"} <span className="text-destructive">*</span>
              </FieldLabel>
              <Input
                id="fullName"
                type="text"
                required
                maxLength={100}
                value={fullName}
                onChange={(e) => setFullName(e.target.value)}
                placeholder={role === "center" ? "Trung tâm Du học Á Âu" : "Nguyễn Văn A"}
              />
            </Field>

            {/* Email đăng nhập */}
            <Field>
              <FieldLabel htmlFor="email">
                Email <span className="text-destructive">*</span>
              </FieldLabel>
              <Input
                id="email"
                type="email"
                required
                maxLength={100}
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                placeholder="example@gmail.com"
              />
            </Field>

            {/* Số điện thoại Việt Nam */}
            <Field>
              <FieldLabel htmlFor="phone">Số điện thoại liên hệ (Việt Nam)</FieldLabel>
              <Input
                id="phone"
                type="tel"
                maxLength={20}
                value={phone}
                onChange={(e) => setPhone(e.target.value)}
                placeholder="0912 345 678 hoặc +84912345678"
              />
              <FieldDescription>
                Chỉ chấp nhận số điện thoại di động Việt Nam (10 chữ số, đầu số 03, 05, 07, 08, 09).
              </FieldDescription>
            </Field>

            {/* Mật khẩu */}
            <Field>
              <FieldLabel htmlFor="password">
                Mật khẩu <span className="text-destructive">*</span>
              </FieldLabel>
              <Input
                id="password"
                type="password"
                required
                minLength={8}
                maxLength={100}
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                placeholder="Tối thiểu 8 ký tự, tối đa 100"
              />
            </Field>

            {/* Xác nhận mật khẩu */}
            <Field>
              <FieldLabel htmlFor="confirmPassword">
                Xác nhận mật khẩu <span className="text-destructive">*</span>
              </FieldLabel>
              <Input
                id="confirmPassword"
                type="password"
                required
                maxLength={100}
                value={confirmPassword}
                onChange={(e) => setConfirmPassword(e.target.value)}
                placeholder="Nhập lại mật khẩu"
              />
            </Field>

            {/* Checkbox dành riêng cho vai học sinh */}
            {role === "student" && (
              <Field orientation="horizontal">
                <Checkbox
                  id="parentAcknowledged"
                  checked={parentAcknowledged}
                  onCheckedChange={(checked) => setParentAcknowledged(checked)}
                />
                <FieldLabel htmlFor="parentAcknowledged">
                  Tôi xác nhận phụ huynh đã biết về việc tôi đăng ký tài khoản tư vấn du học trên hệ thống.
                </FieldLabel>
              </Field>
            )}

            <Button type="submit" size="lg" disabled={isSubmitting} className="w-full">
              {isSubmitting && <Spinner data-icon="inline-start" />}
              {isSubmitting ? "Đang xử lý..." : "Tạo tài khoản"}
            </Button>
          </FieldGroup>
        </form>

        <p className="text-center text-muted-foreground">
          Đã có tài khoản?{" "}
          <Link href="/login" className="text-primary hover:underline">
            Đăng nhập ngay
          </Link>
        </p>
      </CardContent>
    </Card>
  );
}
