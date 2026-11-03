import type { UserRole } from "@/types/api";

/** Nhãn hiển thị vai trò người dùng (UI tiếng Việt). */
export const USER_ROLE_LABELS: Record<UserRole, string> = {
  student: "Học sinh",
  parent: "Phụ huynh",
  center: "Trung tâm",
  moderator: "Moderator",
  admin: "Quản trị viên",
};

export function formatUserRole(role: UserRole): string {
  return USER_ROLE_LABELS[role] ?? role;
}
