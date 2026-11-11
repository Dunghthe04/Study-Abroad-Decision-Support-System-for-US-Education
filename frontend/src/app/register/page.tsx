import type { Metadata } from "next";
import { AuthSplitLayout } from "@/components/auth/AuthSplitLayout";
import { RegisterForm } from "@/components/auth/RegisterForm";

export const metadata: Metadata = {
  title: "Đăng ký tài khoản – USAS",
  description: "Đăng ký tài khoản học sinh hoặc phụ huynh để nhận tư vấn du học Mỹ",
};

export default function RegisterPage() {
  return (
    <AuthSplitLayout>
      <RegisterForm />
    </AuthSplitLayout>
  );
}
