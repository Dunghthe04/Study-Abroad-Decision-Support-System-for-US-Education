import type { Metadata } from "next";
import { AuthSplitLayout } from "@/components/auth/AuthSplitLayout";
import { RegisterCenterForm } from "@/components/center/RegisterCenterForm";

export const metadata: Metadata = {
  title: "Đăng ký trung tâm – USDSS",
  description: "Trung tâm tư vấn du học tự đăng ký tài khoản và gia nhập danh bạ USDSS",
};

export default function RegisterCenterPage() {
  return (
    <AuthSplitLayout>
      <RegisterCenterForm />
    </AuthSplitLayout>
  );
}

