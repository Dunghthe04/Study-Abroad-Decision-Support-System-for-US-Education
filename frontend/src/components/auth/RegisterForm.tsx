"use client";

import React, { useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { GraduationCapIcon, InfoIcon, UsersIcon } from "lucide-react";
import { useAuth } from "@/contexts/AuthContext";
import { ApiError } from "@/lib/api";
import { setPendingEmail } from "@/lib/pending-email";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Field, FieldDescription, FieldGroup, FieldLabel } from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Spinner } from "@/components/ui/spinner";

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

export function RegisterForm() {
  const router = useRouter();
  const { register } = useAuth();

  const [fullName, setFullName] = useState("");
  const [email, setEmail] = useState("");
  const [phone, setPhone] = useState("");
  const [role, setRole] = useState<"student" | "parent">("student");
  const [dateOfBirth, setDateOfBirth] = useState("");
  const [parentEmail, setParentEmail] = useState("");
  const [password, setPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");

  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

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

    // 1. Kiểm tra tính hợp lệ cơ bản ở phía client
    if (!fullName.trim()) {
      setError("Vui lòng nhập họ và tên.");
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
    if (age === null) {
      setError("Vui lòng nhập ngày sinh.");
      return;
    }
    if (dateOfBirth > todayInVietnam()) {
      setError("Ngày sinh không được ở tương lai.");
      return;
    }
    if (age > MAX_AGE) {
      setError("Ngày sinh không hợp lệ.");
      return;
    }
    if (role === "parent" && age < ADULT_AGE) {
      setError(`Tài khoản phụ huynh phải từ ${ADULT_AGE} tuổi.`);
      return;
    }
    if (parentEmailRequired && !parentEmail.trim()) {
      setError(`Học sinh dưới ${CHILD_MAX_AGE_EXCLUSIVE} tuổi cần nhập email phụ huynh để phụ huynh đồng ý.`);
      return;
    }
    if (isMinorStudent && parentEmail.trim().toLowerCase() === email.trim().toLowerCase()) {
      setError("Email phụ huynh phải khác email của học sinh.");
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
        dateOfBirth,
        parentEmail: isMinorStudent && parentEmail.trim() ? parentEmail.trim() : null,
        password,
      });

      // 4. Đăng ký thành công -> Chuyển sang màn hình xác thực mã OTP gửi về email
      setPendingEmail(email.trim());
      router.push("/verify-email");
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
            {/* Lựa chọn vai trò: Học sinh, Phụ huynh (trung tâm do admin tạo tài khoản) */}
            <Field>
              <Label>Bạn là:</Label>
              <div className="grid grid-cols-2 gap-2">
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
              </div>
            </Field>

            {/* Họ và tên */}
            <Field>
              <FieldLabel htmlFor="fullName">
                Họ và tên <span className="text-destructive">*</span>
              </FieldLabel>
              <Input
                id="fullName"
                type="text"
                required
                maxLength={100}
                value={fullName}
                onChange={(e) => setFullName(e.target.value)}
                placeholder="Nguyễn Văn A"
              />
            </Field>

            {/* Ngày sinh: dùng để tính nhóm tuổi */}
            <Field>
              <FieldLabel htmlFor="dateOfBirth">
                Ngày sinh <span className="text-destructive">*</span>
              </FieldLabel>
              <Input
                id="dateOfBirth"
                type="date"
                required
                max={todayInVietnam()}
                value={dateOfBirth}
                onChange={(e) => setDateOfBirth(e.target.value)}
              />
              {role === "parent" && (
                <FieldDescription>Tài khoản phụ huynh dành cho người từ {ADULT_AGE} tuổi.</FieldDescription>
              )}
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
                <Field>
                  <FieldLabel htmlFor="parentEmail">
                    Email phụ huynh {parentEmailRequired && <span className="text-destructive">*</span>}
                  </FieldLabel>
                  <Input
                    id="parentEmail"
                    type="email"
                    required={parentEmailRequired}
                    maxLength={100}
                    value={parentEmail}
                    onChange={(e) => setParentEmail(e.target.value)}
                    placeholder="phuhuynh@gmail.com"
                  />
                  <FieldDescription>
                    Lời mời được gửi tới email này sau khi bạn xác thực email của mình.
                  </FieldDescription>
                </Field>
              </>
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
