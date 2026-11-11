"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { usePathname, useRouter } from "next/navigation";
import {
  BookOpenIcon,
  BookmarkCheckIcon,
  Building2Icon,
  InboxIcon,
  KeyRoundIcon,
  LayoutDashboardIcon,
  LogOutIcon,
  SearchIcon,
  ShieldIcon,
  UserIcon,
  UsersIcon,
} from "lucide-react";
import { formatUserRole } from "@/lib/user-role-labels";
import { useAuth } from "@/contexts/AuthContext";
import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import { Badge } from "@/components/ui/badge";
import { buttonVariants } from "@/components/ui/button";
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
import { isAdminRoute } from "@/lib/admin-nav";
import { isWideRoute } from "@/lib/layout-routes";
import { cn } from "@/lib/utils";

/**
 * Thanh đầu trang 2 hàng.
 * Hàng trên: logo, ô tìm trường, tài khoản (khách: "Đã có tài khoản? Đăng nhập" + "Đăng ký ngay"; đã đăng nhập: ảnh đại diện).
 * Hàng dưới: điều hướng chính bên trái, nút "Phân tích hồ sơ của bạn" bên phải.
 * Trang chủ: nền trong suốt nằm trên ảnh trường, cuộn xuống thì thành nền trắng như các trang khác.
 */
export function SiteHeader() {
  const { user, isLoading, logout } = useAuth();
  const router = useRouter();
  const pathname = usePathname();

  const isAdmin = user?.role === "admin";
  const isModerator = user?.role === "moderator";
  const canOpenAdmin = isAdmin || isModerator;
  const isStudentOrParent = user?.role === "student" || user?.role === "parent";

  const navLinks = [
    { href: canOpenAdmin ? "/admin" : "/", label: isModerator ? "Kiểm duyệt" : isAdmin ? "Quản trị" : "Trang chủ" },
    { href: "/universities", label: "Tìm trường" },
    { href: canOpenAdmin ? "/admin/centers" : "/centers", label: "Trung tâm" },
    { href: "/methodology", label: "Phương pháp của chúng tôi" },
    ...(isStudentOrParent ? [{ href: "/target-schools", label: "Trường mục tiêu" }] : []),
    ...(user?.role === "center" ? [{ href: "/center/leads", label: "Yêu cầu liên hệ" }] : []),
  ];

  const isLinkActive = (href: string) => {
    if (href === "/admin" || href === "/") return pathname === href;
    if (href === "/admin/centers") return pathname.startsWith("/admin/centers") || pathname.startsWith("/centers");
    if (href === "/universities") return pathname.startsWith("/universities") || pathname.startsWith("/university");
    return pathname.startsWith(href);
  };

  const overHero = pathname === "/";
  const [scrolled, setScrolled] = useState(false);
  useEffect(() => {
    if (!overHero) return;
    const onScroll = () => setScrolled(window.scrollY > 24);
    onScroll();
    window.addEventListener("scroll", onScroll, { passive: true });
    return () => window.removeEventListener("scroll", onScroll);
  }, [overHero]);
  const clear = overHero && !scrolled;

  // Ô tìm trường: chuyển sang trang Tìm trường với từ khóa (?search=, cùng tham số trang đó đang đọc)
  const [query, setQuery] = useState("");
  const submitSearch = (e: React.FormEvent) => {
    e.preventDefault();
    const q = query.trim();
    router.push(q ? `/universities?search=${encodeURIComponent(q)}` : "/universities");
  };

  const handleLogout = async () => {
    await logout();
    router.push("/login");
  };

  return (
    // Nền xanh thương hiệu trên mọi trang; trang chủ khi chưa cuộn thì trong suốt nằm trên ảnh trường
    <header
      className={cn(
        "sticky top-0 z-40 border-b text-white transition-colors duration-300 print:hidden",
        clear ? "border-white/15 bg-brand-ink/55" : "border-brand-ink bg-brand",
      )}
    >
      <div
        className={cn(
          "mx-auto px-4",
          isAdminRoute(pathname) || pathname.startsWith("/recommendations")
            ? "max-w-none sm:px-6"
            : isWideRoute(pathname)
              ? "max-w-[1560px] sm:px-8 lg:px-12"
              : "max-w-5xl",
        )}
      >
        {/* Hàng trên */}
        <div className="flex h-14 items-center gap-4 lg:gap-8">
          <Link
            href={canOpenAdmin ? "/admin" : "/"}
            className="shrink-0 font-heading text-xl font-bold tracking-tight text-white transition-opacity hover:opacity-90"
          >
            USAS
          </Link>

          <form role="search" onSubmit={submitSearch} className="relative hidden max-w-2xl flex-1 md:block">
            <label htmlFor="site-search" className="sr-only">
              Tìm trường
            </label>
            <input
              id="site-search"
              type="search"
              value={query}
              onChange={(e) => setQuery(e.target.value)}
              placeholder="Tìm trường theo tên, thành phố, bang…"
              // Ô tìm kiếm luôn nền trắng trên nền xanh của thanh đầu
              className="h-10 w-full rounded-lg border border-transparent bg-surface pr-11 pl-3.5 text-sm text-ink outline-none transition-colors placeholder:text-ink-3 focus-visible:ring-3 focus-visible:ring-white/40"
            />
            <button
              type="submit"
              aria-label="Tìm"
              className="absolute top-1/2 right-1 flex size-8 -translate-y-1/2 items-center justify-center rounded-md text-ink-2 transition-colors hover:bg-muted hover:text-ink"
            >
              <SearchIcon className="size-4" />
            </button>
          </form>

          <div className="ml-auto flex shrink-0 items-center gap-2 sm:gap-3">
            {isLoading ? (
              <Skeleton className="h-9 w-24" />
            ) : user ? (
              <DropdownMenu>
                <DropdownMenuTrigger
                  aria-label={`Tài khoản của ${user.fullName}`}
                  className="rounded-full outline-none transition-transform hover:scale-105 focus-visible:ring-3 focus-visible:ring-white/50 active:scale-95"
                >
                  <Avatar className="size-9">
                    <AvatarFallback className="bg-white font-semibold text-brand-ink">
                      {user.fullName.charAt(0).toUpperCase()}
                    </AvatarFallback>
                  </Avatar>
                </DropdownMenuTrigger>
                <DropdownMenuContent align="end" className="w-64">
                  {/* Đầu menu: ảnh đại diện, tên, vai trò */}
                  <DropdownMenuGroup>
                    <DropdownMenuLabel className="flex items-center gap-3 px-2 py-2.5">
                      <Avatar className="size-10">
                        <AvatarFallback className="bg-brand-wash font-semibold text-brand-ink">
                          {user.fullName.charAt(0).toUpperCase()}
                        </AvatarFallback>
                      </Avatar>
                      <span className="flex min-w-0 flex-col items-start gap-1">
                        <span className="w-full truncate text-sm font-semibold text-ink">{user.fullName}</span>
                        <Badge variant="neutral">{formatUserRole(user.role)}</Badge>
                      </span>
                    </DropdownMenuLabel>
                  </DropdownMenuGroup>

                  {isStudentOrParent && (
                    <>
                      <DropdownMenuSeparator />
                      <DropdownMenuItem render={<Link href="/target-schools" />}>
                        <BookmarkCheckIcon />
                        Trường mục tiêu
                      </DropdownMenuItem>
                    </>
                  )}

                  {/* Hồ sơ */}
                  <DropdownMenuSeparator />
                  <DropdownMenuItem render={<Link href="/account#personal" />}>
                    <UserIcon />
                    Thông tin cá nhân
                  </DropdownMenuItem>
                  {isStudentOrParent && (
                    <DropdownMenuItem render={<Link href="/account#academic" />}>
                      <BookOpenIcon />
                      Hồ sơ học thuật
                    </DropdownMenuItem>
                  )}

                  {/* Tài khoản */}
                  <DropdownMenuSeparator />
                  <DropdownMenuItem render={<Link href="/account#password" />}>
                    <KeyRoundIcon />
                    Bảo mật tài khoản
                  </DropdownMenuItem>
                  {isStudentOrParent && (
                    <DropdownMenuItem render={<Link href="/account#parent-links" />}>
                      <UsersIcon />
                      {user.role === "student" ? "Liên kết phụ huynh" : "Con đã liên kết"}
                    </DropdownMenuItem>
                  )}

                  {/* Khu vực theo vai trò (trung tâm, quản trị) */}
                  {(user.role === "center" || canOpenAdmin) && (
                    <>
                      <DropdownMenuSeparator />
                      {canOpenAdmin && (
                        <DropdownMenuItem render={<Link href="/admin" />}>
                          <LayoutDashboardIcon />
                          {isModerator ? "Khu vực kiểm duyệt" : "Trang quản trị"}
                        </DropdownMenuItem>
                      )}
                      <DropdownMenuItem render={<Link href="/center/leads" />}>
                        <InboxIcon />
                        Yêu cầu liên hệ nhận được
                      </DropdownMenuItem>
                      {canOpenAdmin && (
                        <DropdownMenuItem render={<Link href="/admin/centers" />}>
                          <Building2Icon />
                          Quản lý trung tâm
                        </DropdownMenuItem>
                      )}
                      {isAdmin && (
                        <DropdownMenuItem render={<Link href="/admin/users" />}>
                          <ShieldIcon />
                          Quản trị người dùng
                        </DropdownMenuItem>
                      )}
                    </>
                  )}

                  <DropdownMenuSeparator />
                  <DropdownMenuItem variant="destructive" onClick={handleLogout}>
                    <LogOutIcon />
                    Đăng xuất
                  </DropdownMenuItem>
                </DropdownMenuContent>
              </DropdownMenu>
            ) : (
              <>
                <p className="hidden text-sm text-white/85 lg:block">
                  Đã có tài khoản?{" "}
                  <Link href="/login" className="font-semibold text-white underline-offset-4 hover:underline">
                    Đăng nhập
                  </Link>
                </p>
                <Link
                  href="/login"
                  className={buttonVariants({ variant: "ghost", className: "lg:hidden !text-white hover:!bg-white/10" })}
                >
                  Đăng nhập
                </Link>
                <Link
                  href="/register"
                  className={buttonVariants({
                    variant: "outline",
                    size: "lg",
                    className: "active:translate-y-px !border-white/60 !bg-transparent !text-white hover:!bg-white/10",
                  })}
                >
                  Đăng ký ngay
                </Link>
              </>
            )}
          </div>
        </div>

        {/* Hàng dưới */}
        <nav aria-label="Điều hướng chính" className="flex h-12 items-center justify-between gap-4">
          <ul className="-ml-3 flex min-w-0 items-center overflow-x-auto [scrollbar-width:none]">
            {navLinks.map((l) => {
              const active = isLinkActive(l.href);
              return (
                <li key={l.href}>
                  <Link
                    href={l.href}
                    aria-current={active ? "page" : undefined}
                    className={cn(
                      "relative inline-flex h-12 items-center whitespace-nowrap px-3 text-sm font-medium transition-colors",
                      "after:absolute after:inset-x-3 after:bottom-0 after:h-0.5 after:origin-left after:scale-x-0 after:rounded-full after:transition-transform after:duration-300 hover:after:scale-x-100 motion-reduce:after:transition-none",
                      "after:bg-white",
                      active ? "font-semibold text-white after:scale-x-100" : "text-white/80 hover:text-white",
                    )}
                  >
                    {l.label}
                  </Link>
                </li>
              );
            })}
          </ul>
          {!canOpenAdmin && (
            <Link
              href="/analysis"
              className={buttonVariants({
                variant: "ghost",
                size: "lg",
                className: "shrink-0 active:translate-y-px !bg-white !text-brand-ink hover:!bg-white/90",
              })}
            >
              Phân tích hồ sơ của tôi
            </Link>
          )}
        </nav>
      </div>
    </header>
  );
}
