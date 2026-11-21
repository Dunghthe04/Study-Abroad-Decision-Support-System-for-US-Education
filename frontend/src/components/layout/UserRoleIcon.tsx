import { Building2Icon, GraduationCapIcon, ShieldIcon, UsersIcon } from "lucide-react";
import type { UserRole } from "@/types/api";

/** Icon theo vai trò, dùng chung cho thanh bên trang Tài khoản và menu avatar ở thanh đầu trang. */
export function UserRoleIcon({ role, className }: { role: UserRole; className?: string }) {
  switch (role) {
    case "student":
      return <GraduationCapIcon className={className} aria-hidden />;
    case "parent":
      return <UsersIcon className={className} aria-hidden />;
    case "center":
      return <Building2Icon className={className} aria-hidden />;
    default:
      return <ShieldIcon className={className} aria-hidden />;
  }
}
