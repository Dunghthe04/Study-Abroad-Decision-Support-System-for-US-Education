import type { Metadata } from "next";
import { AdminSection } from "@/components/admin/AdminSection";

export const metadata: Metadata = {
  title: "Tài khoản – Quản trị USAS",
};

export default function AdminAccountsPage() {
  return <AdminSection href="/admin/accounts" />;
}
