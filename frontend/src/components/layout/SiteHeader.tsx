"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { FileTextIcon, LogOutIcon, UserIcon } from "lucide-react";
import { useAuth } from "@/contexts/AuthContext";
import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import { Badge } from "@/components/ui/badge";
import { Button, buttonVariants } from "@/components/ui/button";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuGroup,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { Skeleton } from "@/components/ui/skeleton";

const links = [
  { href: "/advisor", label: "Tư vấn AI" },
  { href: "/profile/financial", label: "Tài chính & Ngoại khóa" },
  { href: "/recommendations", label: "Gợi ý trường" },
  { href: "/centers", label: "Trung tâm" },
  // [USAS-365] Liên kết trang phân tích điểm học thuật
  { href: "/profile/academic/analysis", label: "Điểm học thuật" },
];

export function SiteHeader() {
  const { user, isLoading, logout } = useAuth();
  const router = useRouter();

  const handleLogout = async () => {
    await logout();
    router.push("/login");
  };

  return (
    <header className="border-b">
      <nav className="mx-auto flex max-w-5xl items-center justify-between px-4 py-3">
        <div className="flex items-center gap-8">
          <Link href="/" className="font-heading text-xl font-semibold tracking-tight text-brand-ink">
            USAS
          </Link>
          <ul className="flex gap-6 text-sm font-medium text-muted-foreground">
            {links.map((l) => (
              <li key={l.href}>
                <Link href={l.href} className="hover:text-foreground">
                  {l.label}
                </Link>
              </li>
            ))}
            {user && (user.role === "student" || user.role === "parent") && (
              <li>
                <Link
                  href="/profile/academic"
                  className="flex items-center gap-1 text-primary hover:text-primary/80"
                >
                  <FileTextIcon className="size-4" aria-hidden="true" />
                  Hồ sơ học thuật
                </Link>
              </li>
            )}
          </ul>
        </div>

        {/* Trạng thái xác thực của người dùng (Khách vs Đã đăng nhập) */}
        <div className="flex items-center gap-3">
          {isLoading ? (
            <Skeleton className="h-8 w-20" />
          ) : user ? (
            <DropdownMenu>
              <DropdownMenuTrigger render={<Button variant="outline" />}>
                <Avatar size="sm">
                  <AvatarFallback>{user.fullName.charAt(0).toUpperCase()}</AvatarFallback>
                </Avatar>
                <span>{user.fullName}</span>
                <Badge variant="brand">
                  {user.role === "student" ? "Học sinh" : user.role === "parent" ? "Phụ huynh" : user.role}
                </Badge>
              </DropdownMenuTrigger>
              <DropdownMenuContent align="end">
                <DropdownMenuGroup>
                  <DropdownMenuLabel>{user.fullName}</DropdownMenuLabel>
                </DropdownMenuGroup>
                <DropdownMenuSeparator />
                <DropdownMenuItem render={<Link href="/account" />}>
                  <UserIcon />
                  Hồ sơ cá nhân
                </DropdownMenuItem>
                <DropdownMenuItem variant="destructive" onClick={handleLogout}>
                  <LogOutIcon />
                  Đăng xuất
                </DropdownMenuItem>
              </DropdownMenuContent>
            </DropdownMenu>
          ) : (
            <div className="flex items-center gap-2">
              <Link href="/login" className={buttonVariants({ variant: "ghost" })}>
                Đăng nhập
              </Link>
              <Link href="/register" className={buttonVariants({ variant: "default" })}>
                Đăng ký
              </Link>
            </div>
          )}
        </div>
      </nav>
    </header>
  );
}
