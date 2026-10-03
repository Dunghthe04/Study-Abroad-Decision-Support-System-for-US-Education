import { RegisterForm } from "@/components/auth/RegisterForm";

export const metadata = {
  title: "Đăng ký – USAS",
  description: "Đăng ký tài khoản học sinh, phụ huynh hoặc trung tâm tư vấn du học Mỹ",
};

export default function RegisterPage() {
  return (
    <main className="flex min-h-[calc(100vh-140px)] items-center justify-center py-10 px-4">
      <RegisterForm />
    </main>
  );
}
