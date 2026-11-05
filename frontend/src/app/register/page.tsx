import type { Metadata } from "next";
import { RegisterForm } from "@/components/auth/RegisterForm";

export const metadata: Metadata = {
  title: "Đăng ký tài khoản – USAS",
  description: "Đăng ký tài khoản học sinh hoặc phụ huynh để nhận tư vấn du học Mỹ",
};

export default function RegisterPage() {
  return (
    <div className="flex min-h-[calc(100vh-12rem)] items-center justify-center py-6">
      <RegisterForm />
    </div>
  );
}
