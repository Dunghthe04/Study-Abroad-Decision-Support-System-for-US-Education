import {
  BarChart3Icon,
  BookOpenIcon,
  BuildingIcon,
  CreditCardIcon,
  LayoutDashboardIcon,
  MessagesSquareIcon,
  SchoolIcon,
  SlidersHorizontalIcon,
  UsersIcon,
  type LucideIcon,
} from "lucide-react";
import type { UserRole } from "@/types/api";

export type AdminRole = Extract<UserRole, "admin" | "moderator">;

export interface AdminNavItem {
  href: string;
  label: string;
  description: string;
  icon: LucideIcon;
  roles: AdminRole[];
  useCases: string[];
}

export const ADMIN_ROLES: AdminRole[] = ["admin", "moderator"];

export const ADMIN_NAV: AdminNavItem[] = [
  {
    href: "/admin",
    label: "Tổng quan",
    description: "Lối vào nhanh tới các khu vực quản trị",
    icon: LayoutDashboardIcon,
    roles: ["admin", "moderator"],
    useCases: [],
  },
  {
    href: "/admin/accounts",
    label: "Tài khoản",
    description: "Danh sách, khóa, mở khóa tài khoản và tạo tài khoản moderator",
    icon: UsersIcon,
    roles: ["admin"],
    useCases: ["UC-60", "UC-61", "UC-62", "UC-63"],
  },
  {
    href: "/admin/universities",
    label: "Dữ liệu trường",
    description: "Nhập, sửa, ẩn hoặc hiện trường và xem độ mới của dữ liệu",
    icon: SchoolIcon,
    roles: ["admin"],
    useCases: ["UC-65", "UC-66", "UC-67", "UC-84"],
  },
  {
    href: "/admin/knowledge",
    label: "Tri thức",
    description: "Duyệt hoặc từ chối dòng tri thức và xem câu hỏi chưa trả lời",
    icon: BookOpenIcon,
    roles: ["admin", "moderator"],
    useCases: ["UC-51", "UC-52", "UC-53"],
  },
  {
    href: "/admin/centers",
    label: "Trung tâm tư vấn",
    description: "Thêm, xác minh, sửa hoặc ẩn trung tâm và tạo tài khoản trung tâm",
    icon: BuildingIcon,
    roles: ["admin"],
    useCases: ["UC-59", "UC-64", "UC-71", "UC-72"],
  },
  {
    href: "/admin/plans",
    label: "Gói và giao dịch",
    description: "Quản lý gói dịch vụ, giá và xem giao dịch",
    icon: CreditCardIcon,
    roles: ["admin"],
    useCases: ["UC-79"],
  },
  {
    href: "/admin/parameters",
    label: "Tham số hệ thống",
    description: "Bảng quy đổi điểm, ngưỡng gợi ý, tỷ giá và luật tư vấn",
    icon: SlidersHorizontalIcon,
    roles: ["admin"],
    useCases: ["UC-68", "UC-69", "UC-70", "UC-88"],
  },
  {
    href: "/admin/forum",
    label: "Kiểm duyệt diễn đàn",
    description: "Nội dung bị báo cáo, gỡ bài, hạn chế người dùng, gỡ đánh giá và tin nhắn",
    icon: MessagesSquareIcon,
    roles: ["admin", "moderator"],
    useCases: ["UC-54", "UC-55", "UC-56", "UC-57", "UC-58"],
  },
  {
    href: "/admin/reports",
    label: "Báo cáo",
    description: "Mức dùng AI, chi phí và chất lượng AI",
    icon: BarChart3Icon,
    roles: ["admin"],
    useCases: ["UC-83", "UC-90"],
  },
];

export function adminNavForRole(role: UserRole | undefined): AdminNavItem[] {
  if (role !== "admin" && role !== "moderator") {
    return [];
  }
  return ADMIN_NAV.filter((item) => item.roles.includes(role));
}

export function findAdminNavItem(href: string): AdminNavItem | undefined {
  return ADMIN_NAV.find((item) => item.href === href);
}

export function isAdminRoute(pathname: string): boolean {
  return pathname === "/admin" || pathname.startsWith("/admin/");
}
