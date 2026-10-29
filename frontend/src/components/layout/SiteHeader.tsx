"use client";

import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { FileTextIcon, LogOutIcon, ShieldIcon, UserIcon } from "lucide-react";
import { formatUserRole } from "@/lib/user-role-labels";
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
import { isWideRoute } from "@/lib/layout-routes";
import { cn } from "@/lib/utils";

const links = [
  { href: "/", label: "Trang chủ" },
  { href: "/methodology", label: "Giới thiệu phương pháp" },
  { href: "/analysis", label: "Phân tích tư vấn" },
];

export function SiteHeader() {
  const { user, isLoading, logout } = useAuth();
  const router = useRouter();
  const pathname = usePathname();

  const isLinkActive = (href: string) => {
    if (href === "/") {
      return pathname === "/";
    }
    return pathname.startsWith(href);
  };

  const handleLogout = async () => {
    await logout();
    router.push("/login");
  };

  return (
    <header className="sticky top-0 z-40 border-b border-line bg-surface print:hidden">
      <nav
        className={cn(
          "mx-auto flex items-center justify-between gap-6 px-4 py-2.5",
          isWideRoute(pathname) ? "max-w-[1320px] sm:px-6" : "max-w-5xl"
        )}
      >
        <div className="flex min-w-0 items-center gap-6">
          <Link
            href="/"
            className="flex items-center gap-2 font-heading text-xl font-bold tracking-tight text-brand-ink transition-opacity hover:opacity-90"
          >
            <span className="flex size-[30px] items-center justify-center rounded-lg bg-brand-ink text-white font-sans text-xs font-black">
              US
            </span>
            <span>USAS</span>
          </Link>
          <ul className="flex items-center gap-1 overflow-x-auto text-sm font-medium [scrollbar-width:none]">
            {links.map((l) => {
              const active = isLinkActive(l.href);
              return (
                <li key={l.href}>
                  <Link
                    href={l.href}
                    className={cn(
                      "inline-flex items-center whitespace-nowrap rounded-full px-3.5 py-1.5 text-sm font-medium transition-colors",
                      active
                        ? "bg-brand-wash text-brand font-semibold"
                        : "text-muted-foreground hover:bg-muted/60 hover:text-foreground"
                    )}
                  >
                    {l.label}
                  </Link>
                </li>
              );
            })}
          </ul>
        </div>

        {/* Trạng thái xác thực của người dùng (Khách vs Đã đăng nhập) */}
        <div className="flex shrink-0 items-center gap-3">
          {isLoading ? (
            <Skeleton className="h-8 w-20" />
          ) : user ? (
            <DropdownMenu>
              <DropdownMenuTrigger
                render={
                  <Button
                    variant="outline"
                    className="h-9 gap-2 rounded-full border-line pr-2 pl-1"
                  />
                }
              >
                <Avatar size="sm">
                  <AvatarFallback className="bg-brand-wash text-brand-ink font-semibold">
                    {user.fullName.charAt(0).toUpperCase()}
                  </AvatarFallback>
                </Avatar>
                <span className="hidden max-w-40 truncate sm:inline">{user.fullName}</span>
                <Badge variant="brand" className="hidden md:inline-flex">
                  {formatUserRole(user.role)}
                </Badge>
              </DropdownMenuTrigger>
              <DropdownMenuContent align="end">
                <DropdownMenuGroup>
                  <DropdownMenuLabel>{user.fullName}</DropdownMenuLabel>
                </DropdownMenuGroup>
                <DropdownMenuSeparator />
                <DropdownMenuItem render={<Link href="/analysis" />}>
                  <FileTextIcon />
                  Phân tích tư vấn
                </DropdownMenuItem>
                <DropdownMenuItem render={<Link href="/account" />}>
                  <UserIcon />
                  Hồ sơ cá nhân
                </DropdownMenuItem>
                {user.role === "admin" && (
                  <DropdownMenuItem render={<Link href="/admin/users" />}>
                    <ShieldIcon />
                    Quản trị người dùng
                  </DropdownMenuItem>
                )}
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

