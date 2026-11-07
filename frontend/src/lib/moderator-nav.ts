import {
  BookOpenIcon,
  BuildingIcon,
  LayoutDashboardIcon,
  MessagesSquareIcon,
  type LucideIcon,
} from "lucide-react";
import type { UserRole } from "@/types/api";

export type ModeratorAllowedRole = Extract<UserRole, "moderator" | "admin">;

export interface ModeratorNavItem {
  href: string;
  label: string;
  description: string;
  icon: LucideIcon;
  useCases: string[];
}

export const MODERATOR_ROLES: ModeratorAllowedRole[] = ["moderator", "admin"];

export const MODERATOR_NAV: ModeratorNavItem[] = [
  {
    href: "/moderator",
    label: "Tổng quan",
    description: "Bảng điều khiển và lối vào nhanh các phân hệ kiểm duyệt & vận hành",
    icon: LayoutDashboardIcon,
    useCases: [],
  },
  {
    href: "/moderator/centers",
    label: "Trung tâm tư vấn",
    description: "Thêm, xác minh, sửa hoặc ẩn trung tâm và cấp tài khoản trung tâm",
    icon: BuildingIcon,
    useCases: ["UC-59", "UC-64", "UC-71", "UC-72"],
  },
  {
    href: "/moderator/knowledge",
    label: "Tri thức tư vấn",
    description: "Duyệt hoặc từ chối dòng tri thức và xem câu hỏi chưa trả lời",
    icon: BookOpenIcon,
    useCases: ["UC-51", "UC-52", "UC-53"],
  },
  {
    href: "/moderator/forum",
    label: "Kiểm duyệt diễn đàn",
    description: "Nội dung bị báo cáo, gỡ bài, hạn chế người dùng, gỡ đánh giá và tin nhắn",
    icon: MessagesSquareIcon,
    useCases: ["UC-54", "UC-55", "UC-56", "UC-57", "UC-58"],
  },
];

export function isModeratorRoute(pathname: string): boolean {
  return pathname === "/moderator" || pathname.startsWith("/moderator/");
}
