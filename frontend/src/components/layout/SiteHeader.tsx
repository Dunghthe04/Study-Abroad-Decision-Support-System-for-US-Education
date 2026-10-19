"use client";

import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
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
import { cn } from "@/lib/utils";

const links = [
  { href: "/", label: "Trang chủ" },
  { href: "/methodology", label: "Giới thiệu phương pháp" },
  { href: "/analysis", label: "Phân tích tư vấn" },
  { href: "/advisor", label: "Chat AI" },
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

  // Luồng hồ sơ 6 bước có header riêng (ProfileFlowHeader) — chỉ giữ 1 header
  if (pathname.startsWith("/analysis")) return null;

  return (
    <header className="border-b bg-surface/95 backdrop-blur-sm sticky top-0 z-40">
      <nav className="mx-auto flex max-w-5xl items-center justify-between px-4 py-3">
        <div className="flex items-center gap-8">
          <Link
            href="/"
            className="flex items-center gap-2 font-heading text-xl font-bold tracking-tight text-brand-ink transition-opacity hover:opacity-90"
          >
            <span className="flex size-7 items-center justify-center rounded-lg bg-brand text-white font-sans text-xs font-black shadow-xs">
              US
            </span>
            <span>USAS</span>
          </Link>
          <ul className="flex items-center gap-1 sm:gap-2 text-sm font-medium">
            {links.map((l) => {
              const active = isLinkActive(l.href);
              return (
                <li key={l.href}>
                  <Link
                    href={l.href}
                    className={cn(
                      "relative rounded-md px-3 py-1.5 transition-all text-sm font-medium inline-flex items-center gap-1.5",
                      active
                        ? "bg-brand/10 text-brand font-semibold shadow-2xs"
                        : "text-muted-foreground hover:bg-muted/60 hover:text-foreground"
                    )}
                  >
                    {l.label}
                    {active && (
                      <span className="absolute -bottom-3 left-3 right-3 h-0.5 rounded-full bg-brand" />
                    )}
                  </Link>
                </li>
              );
            })}
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
                <DropdownMenuItem render={<Link href="/analysis" />}>
                  <FileTextIcon />
                  Phân tích tư vấn
                </DropdownMenuItem>
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

