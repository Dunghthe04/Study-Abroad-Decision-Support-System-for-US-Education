import type { ComponentType } from "react";
import {
  ActivityIcon,
  ArchiveXIcon,
  BarChart3Icon,
  BookOpenIcon,
  BuildingIcon,
  ClipboardCheckIcon,
  CreditCardIcon,
  FileTextIcon,
  GraduationCapIcon,
  LayoutDashboardIcon,
  LibraryBigIcon,
  MailWarningIcon,
  MessagesSquareIcon,
  ReceiptTextIcon,
  SchoolIcon,
  ShieldAlertIcon,
  SlidersHorizontalIcon,
  UsersIcon,
  type LucideIcon,
} from "lucide-react";
import {
  ActivityFilledIcon,
  ArchiveXFilledIcon,
  BarChart3FilledIcon,
  BookOpenFilledIcon,
  BuildingFilledIcon,
  ClipboardCheckFilledIcon,
  CreditCardFilledIcon,
  FileTextFilledIcon,
  GraduationCapFilledIcon,
  LayoutDashboardFilledIcon,
  LibraryBigFilledIcon,
  MailWarningFilledIcon,
  MessagesSquareFilledIcon,
  ReceiptTextFilledIcon,
  SchoolFilledIcon,
  ShieldAlertFilledIcon,
  SlidersHorizontalFilledIcon,
  UsersFilledIcon,
} from "@/components/admin/AdminNavIcons";
import type { UserRole } from "@/types/api";

export type AdminRole = Extract<UserRole, "admin" | "moderator">;

export type AdminNavSectionId = "overview" | "data" | "moderation" | "ai" | "system";

export interface AdminNavSectionMeta {
  id: AdminNavSectionId;
  title: string | null;
}

export interface AdminNavItem {
  href: string;
  label: string;
  description: string;
  icon: LucideIcon;
  filledIcon: ComponentType<{ className?: string }>;
  section: AdminNavSectionId;
  roles: AdminRole[];
  useCases: string[];
  /**
   * Ẩn khỏi menu và trang tổng quan nhưng trang vẫn vào được (đường dẫn trực tiếp, liên kết từ trang khác) và vẫn qua kiểm tra vai trò
   * của AdminSection. Dùng cho chức năng chưa muốn đưa lên menu.
   */
  hidden?: boolean;
}

export interface AdminNavSection extends AdminNavSectionMeta {
  items: AdminNavItem[];
}

export const ADMIN_ROLES: AdminRole[] = ["admin", "moderator"];

export const ADMIN_NAV_SECTIONS: AdminNavSectionMeta[] = [
  { id: "overview", title: null },
  { id: "data", title: "Dữ liệu & Tri thức" },
  { id: "moderation", title: "Trung tâm & Kiểm duyệt" },
  { id: "ai", title: "AI & Chất lượng" },
  { id: "system", title: "Hệ thống" },
];

export const ADMIN_NAV: AdminNavItem[] = [
  {
    href: "/admin",
    label: "Tổng quan",
    description: "Lối vào nhanh tới các khu vực quản trị",
    icon: LayoutDashboardIcon,
    filledIcon: LayoutDashboardFilledIcon,
    section: "overview",
    roles: ["admin", "moderator"],
    useCases: [],
  },
  {
    href: "/admin/universities",
    label: "Dữ liệu trường",
    description: "Moderator nhập, sửa, ẩn hoặc hiện trường; Admin chỉ xem danh sách và chi tiết",
    icon: SchoolIcon,
    filledIcon: SchoolFilledIcon,
    section: "data",
    // Quyết định D2 bản 14/11: dữ liệu trường do Moderator quản lý, Admin chỉ xem
    roles: ["admin", "moderator"],
    useCases: ["UC-65", "UC-66", "UC-67", "UC-84"],
  },
  {
    href: "/admin/scholarships",
    label: "Học bổng",
    description: "Moderator nhập, sửa, ẩn hoặc hiện học bổng; Admin chỉ xem danh sách và chi tiết",
    icon: GraduationCapIcon,
    filledIcon: GraduationCapFilledIcon,
    section: "data",
    roles: ["admin", "moderator"],
    useCases: ["UC-109"],
  },
  {
    href: "/admin/slm-samples",
    label: "Mẫu huấn luyện SLM",
    description: "Duyệt, sửa và nhập mẫu huấn luyện SLM từ Excel",
    icon: BookOpenIcon,
    filledIcon: BookOpenFilledIcon,
    section: "data",
    roles: ["admin", "moderator"],
    useCases: ["UC-51", "UC-52"],
  },
  {
    href: "/admin/knowledge-base",
    label: "Kho tri thức",
    description: "Quản lý câu hỏi – câu trả lời đã kiểm chứng: thêm, sửa, duyệt và nhập từ Excel",
    icon: LibraryBigIcon,
    filledIcon: LibraryBigFilledIcon,
    section: "data",
    roles: ["admin", "moderator"],
    useCases: ["UC-118", "UC-119", "UC-120", "UC-121", "UC-122", "UC-123", "UC-124", "UC-125", "UC-53"],
  },
  {
    href: "/admin/centers",
    label: "Trung tâm tư vấn",
    description: "Thêm, xác minh, sửa hoặc ẩn trung tâm và tạo tài khoản trung tâm",
    icon: BuildingIcon,
    filledIcon: BuildingFilledIcon,
    section: "moderation",
    // Chỉ moderator quản lý trung tâm; admin chỉ cấp tài khoản moderator
    roles: ["moderator"],
    useCases: ["UC-59", "UC-64", "UC-71", "UC-72"],
  },
  {
    href: "/admin/center-profile-changes",
    label: "Kiểm tra thay đổi trung tâm",
    description: "Đối chiếu website, fanpage trung tâm tự sửa và đánh giá Đạt hoặc Không đạt",
    icon: ClipboardCheckIcon,
    filledIcon: ClipboardCheckFilledIcon,
    section: "moderation",
    // [UC-114] Chỉ Moderator kiểm tra thay đổi trung tâm
    roles: ["moderator"],
    useCases: ["UC-114"],
  },
  {
    href: "/admin/forum",
    label: "Kiểm duyệt diễn đàn",
    description: "Nội dung bị báo cáo, gỡ bài, hạn chế người dùng và gỡ đánh giá",
    icon: MessagesSquareIcon,
    filledIcon: MessagesSquareFilledIcon,
    section: "moderation",
    roles: ["admin", "moderator"],
    useCases: ["UC-54", "UC-55", "UC-56", "UC-57"],
  },
  {
    href: "/admin/center-deletion-requests",
    label: "Yêu cầu xóa trung tâm",
    description: "Trung tâm xin xóa tài khoản và dữ liệu: Admin kiểm tra danh tính người gửi rồi duyệt hoặc từ chối",
    icon: ArchiveXIcon,
    filledIcon: ArchiveXFilledIcon,
    section: "moderation",
    roles: ["admin"],
    useCases: ["UC-93"],
  },
  {
    href: "/admin/lead-message-reports",
    label: "Tin nhắn bị báo cáo",
    description: "Tin nhắn liên hệ bị báo cáo: Admin xem, bỏ qua báo cáo hoặc khóa tài khoản vi phạm (gỡ luôn tin nhắn)",
    icon: MailWarningIcon,
    filledIcon: MailWarningFilledIcon,
    section: "moderation",
    roles: ["admin"],
    useCases: ["UC-58"],
  },
  {
    href: "/admin/ai-usage",
    label: "Quản lý token",
    description: "Token, chi phí và chất lượng các lần gọi AI",
    icon: BarChart3Icon,
    filledIcon: BarChart3FilledIcon,
    section: "ai",
    roles: ["admin"],
    useCases: ["UC-83"],
  },
  {
    href: "/admin/ai-quality",
    label: "Chất lượng AI",
    description: "Tỉ lệ trả lời được, không trả lời được, từ chối, lỗi, thời gian phản hồi và hội thoại mẫu đã che thông tin",
    icon: MessagesSquareIcon,
    filledIcon: MessagesSquareFilledIcon,
    section: "ai",
    roles: ["admin"],
    useCases: ["UC-90"],
  },
  {
    href: "/admin/review-batches",
    label: "Duyệt mẫu",
    description: "Bốc ngẫu nhiên các câu hệ thống đã trả lời để đọc và chấm đúng sai, đo tỷ lệ lỗi",
    icon: ClipboardCheckIcon,
    filledIcon: ClipboardCheckFilledIcon,
    section: "ai",
    roles: ["admin"],
    useCases: [],
    // Ẩn khỏi menu; trang Chất lượng AI (UC-90) có liên kết sang đây cho nhóm câu đã trả lời được
    hidden: true,
  },
  {
    href: "/admin/users",
    label: "Quản trị người dùng",
    description: "Danh sách, khóa, mở khóa tài khoản và cấp tài khoản moderator (chỉ quản trị viên)",
    icon: UsersIcon,
    filledIcon: UsersFilledIcon,
    section: "system",
    roles: ["admin"],
    useCases: ["UC-60", "UC-61", "UC-62", "UC-63"],
  },
  {
    href: "/admin/plans",
    label: "Gói dịch vụ",
    description: "Quản lý gói dịch vụ, giá và hạn mức",
    icon: CreditCardIcon,
    filledIcon: CreditCardFilledIcon,
    section: "system",
    roles: ["admin"],
    useCases: ["UC-79"],
  },
  {
    href: "/admin/orders",
    label: "Đơn hàng",
    description: "Xem và xử lý đơn thanh toán",
    icon: ReceiptTextIcon,
    filledIcon: ReceiptTextFilledIcon,
    section: "system",
    roles: ["admin"],
    useCases: ["UC-132", "UC-133", "UC-134"],
  },
  {
    href: "/admin/parameters",
    label: "Tham số hệ thống",
    description: "Bảng quy đổi điểm, tỷ giá và căn cứ tham số thuật toán gợi ý",
    icon: SlidersHorizontalIcon,
    filledIcon: SlidersHorizontalFilledIcon,
    section: "system",
    // Moderator vào để sửa bảng quy đổi điểm (UC-68); admin chỉ xem bảng này. Tham số thuật toán (UC-89, gộp UC-69) chỉ xem.
    roles: ["admin", "moderator"],
    useCases: ["UC-68", "UC-70", "UC-89"],
  },
  {
    href: "/admin/policies",
    label: "Chính sách & điều khoản",
    description: "Soạn, xem trước và công bố điều khoản sử dụng, chính sách bảo mật theo phiên bản; bản đã công bố không sửa được",
    icon: FileTextIcon,
    filledIcon: FileTextFilledIcon,
    section: "system",
    roles: ["admin"],
    useCases: ["UC-103"],
  },
  {
    href: "/admin/sensitivity",
    label: "Độ nhạy tham số",
    description: "Nhích từng tham số thuật toán gợi ý ±10%, ±20% trên hồ sơ giả lập và xem kết quả thay đổi bao nhiêu",
    icon: ActivityIcon,
    filledIcon: ActivityFilledIcon,
    section: "system",
    roles: ["admin"],
    useCases: ["UC-85"],
  },
  {
    href: "/admin/access-logs",
    label: "Nhật ký truy cập",
    description: "Xem và lọc nhật ký ai đã xem hoặc xuất dữ liệu nhạy cảm (tài liệu xác minh, tin nhắn báo cáo, xuất mẫu)",
    icon: ShieldAlertIcon,
    filledIcon: ShieldAlertFilledIcon,
    section: "system",
    roles: ["admin"],
    useCases: ["UC-97"],
  },
];

export function adminNavForRole(role: UserRole | undefined): AdminNavItem[] {
  if (role !== "admin" && role !== "moderator") {
    return [];
  }
  return ADMIN_NAV.filter((item) => !item.hidden && item.roles.includes(role));
}

export function adminNavSectionsForRole(role: UserRole | undefined): AdminNavSection[] {
  const items = adminNavForRole(role);
  return ADMIN_NAV_SECTIONS.map((section) => ({
    ...section,
    items: items.filter((item) => item.section === section.id),
  })).filter((section) => section.items.length > 0);
}

export function findAdminNavItem(href: string): AdminNavItem | undefined {
  if (href === "/admin/accounts") {
    return ADMIN_NAV.find((item) => item.href === "/admin/users");
  }
  return ADMIN_NAV.find((item) => item.href === href);
}

export function isAdminRoute(pathname: string): boolean {
  return pathname === "/admin" || pathname.startsWith("/admin/");
}
