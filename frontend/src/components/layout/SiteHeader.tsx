"use client";

import Image from "next/image";
import Link from "next/link";
import { useEffect, useState } from "react";
import { usePathname, useRouter } from "next/navigation";
import {
  AwardIcon,
  BookOpenIcon,
  BookmarkCheckIcon,
  Building2Icon,
  CalculatorIcon,
  ChevronDownIcon,
  MapPinIcon,
  CreditCardIcon,
  CrownIcon,
  GaugeIcon,
  GraduationCapIcon,
  HeartHandshakeIcon,
  HomeIcon,
  InboxIcon,
  KeyRoundIcon,
  LayoutDashboardIcon,
  LogOutIcon,
  MessagesSquareIcon,
  PlusIcon,
  SearchIcon,
  SendIcon,
  ShieldIcon,
  UserIcon,
  UsersIcon,
} from "lucide-react";
import { billingApi } from "@/lib/billing-api";
import { useFetched } from "@/lib/use-fetched";
import { formatUserRole } from "@/lib/user-role-labels";
import { UserRoleIcon } from "@/components/layout/UserRoleIcon";
import { useAuth } from "@/contexts/AuthContext";
import { useIsLinkedParent } from "@/hooks/use-is-linked-parent";
import type { QuotaOverviewDto, SubscriptionSummaryDto } from "@/types/billing";
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
import { NotificationBell } from "@/components/notifications/NotificationBell";
import { cn } from "@/lib/utils";

/**
 * Thanh đầu trang 2 hàng.
 * Hàng trên: logo, ô tìm trường, tài khoản (khách: "Đã có tài khoản? Đăng nhập" + "Đăng ký ngay"; đã đăng nhập: ảnh đại diện).
 * Hàng dưới: điều hướng chính bên trái, nút "Phân tích hồ sơ của bạn" bên phải.
 * Trang chủ: nền trong suốt nằm trên ảnh trường, cuộn xuống thì thành nền trắng như các trang khác.
 */
// Mục menu avatar: to và thoáng hơn mặc định để dễ bấm
const MENU_ITEM = "cursor-pointer gap-3 rounded-lg px-3 py-2 text-sm [&_svg:not([class*='size-'])]:size-[17px]";
const MENU_LABEL = "px-3 pt-3 pb-1 text-[13px]";

function formatSubscriptionLabel(sub: SubscriptionSummaryDto): string {
  const activePool =
    sub.chargingPool === "family"
      ? sub.family ?? sub.personal
      : sub.chargingPool === "personal"
        ? sub.personal ?? sub.family
        : sub.family ?? sub.personal;
  return activePool ? `Gói ${activePool.planName}` : "Gói Miễn phí";
}

export function SiteHeader() {
  const { user, isLoading, logout, acting } = useAuth();
  const router = useRouter();
  const pathname = usePathname();

  const isAdmin = user?.role === "admin";
  const isModerator = user?.role === "moderator";
  const canOpenAdmin = isAdmin || isModerator;
  const isStudentOrParent = user?.role === "student" || user?.role === "parent";
  const sub = useFetched<SubscriptionSummaryDto | null>(
    isStudentOrParent ? `sub:${user?.id}:${pathname}` : "none",
    () => (isStudentOrParent ? billingApi.subscription() : Promise.resolve(null)),
  );
  const hasFamily = Boolean(sub.data?.family) || (sub.data?.cycles ?? []).some((c) => c.kind === "family");

  // Hạn mức chung hiện ở đầu menu avatar: chỉ tải khi mở menu (mỗi lần mở là một lần tải mới), không tải theo từng trang
  const showQuota = isStudentOrParent && !acting;
  const [menuOpens, setMenuOpens] = useState(0);
  const quota = useFetched<QuotaOverviewDto | null>(
    showQuota && menuOpens > 0 ? `quota:${user?.id}:${menuOpens}` : "none",
    () => (showQuota ? billingApi.quotaOverview() : Promise.resolve(null)),
  );

  // 1f: phụ huynh đã liên kết không có hồ sơ riêng, các mục hồ sơ riêng nhường chỗ cho "Con của tôi"
  const { linked: isLinkedParent } = useIsLinkedParent();
  // Chỉ hiện khi đã biết chắc là không liên kết (=== false), để lúc đang tải phụ huynh không thấy mục hồ sơ riêng nháy lên.
  // 1.5d, 1.5e: phụ huynh không có hồ sơ riêng; đang làm thay con thì các mục hồ sơ là của con đó
  // (/me vẫn trả role parent khi làm thay, nên xét acting chứ không xét role)
  const hasOwnProfile = acting !== null || (user?.role === "student" && isLinkedParent === false);
  // Trang Tài khoản khi làm thay vẫn là của phụ huynh, nên hồ sơ của con mở ở trang nhập hồ sơ
  const academicHref = acting ? "/profile/academic" : "/account#academic";
  // Phụ huynh chưa có con nào, không làm thay: nút "Thêm con" thay cho "Phân tích hồ sơ của tôi"
  const isParentWithoutChild = user?.role === "parent" && !acting && isLinkedParent === false;

  const leftLinks = [
    { href: "/", label: "Trang chủ", icon: HomeIcon },
    { href: "/universities", label: "Tìm trường", icon: GraduationCapIcon },
    { href: "/scholarships", label: "Học bổng", icon: AwardIcon },
    { href: "/centers", label: "Trung tâm tư vấn", icon: Building2Icon },
    { href: "/community", label: "Cộng đồng", icon: MessagesSquareIcon },
    ...(!user ? [{ href: "/plans", label: "Gói dịch vụ", icon: CrownIcon }] : []),
  ];

  const rightLinks = [
    ...(hasOwnProfile
      ? [
          { href: "/dashboard", label: "Tổng quan", icon: LayoutDashboardIcon },
          { href: "/target-schools", label: "Mục tiêu", icon: BookmarkCheckIcon },
        ]
      : []),
    // 2h-1: đang làm thay con thì ẩn "Quản lý con", muốn quay lại thì bấm thoát làm thay trên băng ActingAsBanner
    ...(user?.role === "parent" && !acting ? [{ href: "/parent/children", label: "Quản lý con", icon: UsersIcon }] : []),
    ...(user?.role === "center" ? [{ href: "/center/leads", label: "Yêu cầu liên hệ", icon: InboxIcon }] : []),
  ];

  const isCostActive = pathname.startsWith("/costs");

  const isLinkActive = (href: string) => {
    if (href === "/") return pathname === href;
    if (href === "/universities") {
      return !isCostActive && (pathname.startsWith("/universities") || pathname.startsWith("/university"));
    }
    return pathname.startsWith(href);
  };

  // Trang có ảnh lớn ở đầu (trang chủ, phương pháp, chi tiết trường): thanh đầu trong suốt nằm trên ảnh cho tới khi cuộn
  const overHero = pathname === "/" || pathname === "/methodology" || pathname.startsWith("/university/");
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
      <div className="@container mx-auto max-w-[1560px] px-4 @2xl:px-8 @5xl:px-12">
        {/* Hàng trên */}
        <div className="flex h-13 items-center gap-3">
          <Link
            href={canOpenAdmin ? "/admin" : "/"}
            aria-label="USDSS"
            className="flex shrink-0 items-center transition-opacity hover:opacity-90"
          >
            <span className="flex size-9 shrink-0 items-center justify-center overflow-hidden rounded-lg bg-white p-0.5">
              <Image
                src="/icons/USDSS.png"
                alt=""
                width={32}
                height={32}
                priority
                className="size-8 object-contain"
              />
            </span>
          </Link>

          <form role="search" onSubmit={submitSearch} className="relative hidden min-w-0 max-w-2xl flex-1 @xl:block">
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
              className="h-9 w-full rounded-lg border border-transparent bg-surface pr-11 pl-3.5 text-sm text-ink outline-none transition-colors placeholder:text-ink-3 focus-visible:ring-3 focus-visible:ring-white/40"
            />
            <Button
              type="submit"
              variant="ghost"
              size="icon"
              aria-label="Tìm"
              className="absolute top-1/2 right-1 -translate-y-1/2 text-ink-2 hover:bg-muted hover:text-ink dark:hover:bg-muted"
            >
              <SearchIcon className="size-4" />
            </Button>
          </form>

          <Link
            href="/universities"
            aria-label="Tìm trường"
            className={buttonVariants({ variant: "ghost", size: "icon", className: "ml-auto @xl:hidden !text-white hover:!bg-white/10" })}
          >
            <SearchIcon className="size-5" aria-hidden />
          </Link>
          <div className="flex shrink-0 items-center gap-2 @xl:ml-auto @2xl:gap-3">
            {isLoading ? (
              <Skeleton className="h-9 w-24" />
            ) : user ? (
              <>
                {isStudentOrParent && sub.data?.tier === "free" && (
                  <Link
                    href="/plans"
                    aria-label="Nâng cấp gói"
                    className={buttonVariants({
                      variant: "ghost",
                      size: "sm",
                      className: "h-8 rounded-lg !bg-gold-wash px-3 text-xs font-semibold !text-brand-ink hover:!bg-white",
                    })}
                  >
                    <CrownIcon className="size-3.5" aria-hidden />
                    <span className="hidden @xl:inline">Nâng cấp</span>
                  </Link>
                )}
                {/* [UC-110] Chuông thông báo */}
                <NotificationBell />
                <DropdownMenu onOpenChange={(open) => open && setMenuOpens((n) => n + 1)}>
                <DropdownMenuTrigger
                  aria-label={`Tài khoản của ${user.fullName}`}
                  className="group flex h-9 cursor-pointer items-center gap-1.5 rounded-lg px-2.5 text-white outline-none transition-colors hover:bg-white/10 focus-visible:ring-3 focus-visible:ring-white/50 data-popup-open:bg-white/10"
                >
                  <UserRoleIcon role={user.role} className="size-5 shrink-0 @xl:hidden" />
                  <span className="hidden max-w-36 truncate text-sm font-medium @xl:inline">{user.fullName}</span>
                  <ChevronDownIcon aria-hidden="true" className="size-3.5 shrink-0 transition-transform group-data-popup-open:rotate-180" />
                </DropdownMenuTrigger>
                <DropdownMenuContent align="end" sideOffset={8} className="w-[22rem] p-0">
                  {/* Đầu menu: biểu tượng vai trò, tên, gói; học sinh / phụ huynh có thêm hạn mức chung còn lại */}
                  <div className="space-y-5 bg-brand-wash px-5 py-5">
                    <div className="flex items-center gap-4">
                      <span
                        aria-hidden="true"
                        className="flex size-12 shrink-0 items-center justify-center rounded-xl bg-brand text-white"
                      >
                        <UserRoleIcon role={user.role} className="size-7" />
                      </span>
                      <span className="flex min-w-0 flex-1 flex-col gap-0.5">
                        <span className="truncate text-base font-semibold text-ink">{user.fullName}</span>
                        {isStudentOrParent ? (
                          sub.data ? (
                            <span className="truncate text-sm text-ink-2">{formatSubscriptionLabel(sub.data)}</span>
                          ) : (
                            <Skeleton className="h-3.5 w-20" />
                          )
                        ) : (
                          <span className="truncate text-sm text-ink-2">{formatUserRole(user.role)}</span>
                        )}
                      </span>
                      <DropdownMenuItem
                        variant="destructive"
                        onClick={handleLogout}
                        aria-label="Đăng xuất"
                        title="Đăng xuất"
                        className="size-10 shrink-0 cursor-pointer justify-center rounded-lg p-0 text-risk hover:bg-risk-wash focus:bg-risk-wash [&_svg:not([class*='size-'])]:size-5"
                      >
                        <LogOutIcon />
                      </DropdownMenuItem>
                    </div>

                    {showQuota && (
                      <Link href="/account#quota" className="block space-y-2 rounded-lg outline-none focus-visible:ring-3 focus-visible:ring-ring/50">
                        <div className="flex items-baseline justify-between gap-3 text-sm">
                          <span className="text-ink-2">Hạn mức chung của bạn còn lại</span>
                          {quota.data ? (
                            <span className="font-semibold text-ink tabular-nums">{quota.data.remainingPercent}%</span>
                          ) : (
                            <Skeleton className="h-3.5 w-8" />
                          )}
                        </div>
                        <div
                          role="progressbar"
                          aria-label="Hạn mức chung còn lại"
                          aria-valuemin={0}
                          aria-valuemax={100}
                          aria-valuenow={quota.data?.remainingPercent ?? 0}
                          className="h-3 w-full overflow-hidden rounded-full bg-surface ring-1 ring-brand/15"
                        >
                          <div
                            className={cn("h-full rounded-full transition-[width] duration-500", quota.data?.lowQuota ? "bg-risk" : "bg-brand")}
                            style={{ width: `${quota.data?.remainingPercent ?? 0}%` }}
                          />
                        </div>
                      </Link>
                    )}
                  </div>

                  <div className="px-2 pt-1 pb-2">
                  {/* Tài khoản */}
                  <DropdownMenuGroup>
                    <DropdownMenuLabel className={MENU_LABEL}>Tài khoản của bạn</DropdownMenuLabel>
                    <DropdownMenuItem className={MENU_ITEM} render={<Link href="/account#personal" />}>
                      <UserIcon />
                      Thông tin cá nhân
                    </DropdownMenuItem>
                    <DropdownMenuItem className={MENU_ITEM} render={<Link href="/account#password" />}>
                      <KeyRoundIcon />
                      Bảo mật tài khoản
                    </DropdownMenuItem>
                  </DropdownMenuGroup>

                  {/* Gói & Thanh toán */}
                  {isStudentOrParent && !acting && (
                    <DropdownMenuGroup>
                      <DropdownMenuSeparator className="mt-2 mb-0" />
                      <DropdownMenuLabel className={MENU_LABEL}>Gói &amp; Thanh toán</DropdownMenuLabel>
                      <DropdownMenuItem className={MENU_ITEM} render={<Link href="/account#subscription" />}>
                        <CreditCardIcon />
                        Gói dịch vụ
                      </DropdownMenuItem>
                      <DropdownMenuItem className={MENU_ITEM} render={<Link href="/account#quota" />}>
                        <GaugeIcon />
                        Hạn mức
                      </DropdownMenuItem>
                      {hasFamily && (
                        <DropdownMenuItem className={MENU_ITEM} render={<Link href="/account#family" />}>
                          <HeartHandshakeIcon />
                          Chia sẻ cho gia đình
                        </DropdownMenuItem>
                      )}
                    </DropdownMenuGroup>
                  )}

                  {/* Hồ sơ & Kết nối */}
                  {(hasOwnProfile || (isStudentOrParent && !acting)) && (
                    <DropdownMenuGroup>
                      <DropdownMenuSeparator className="mt-2 mb-0" />
                      <DropdownMenuLabel className={MENU_LABEL}>Hồ sơ &amp; Kết nối</DropdownMenuLabel>
                      {hasOwnProfile && (
                        <DropdownMenuItem className={MENU_ITEM} render={<Link href={academicHref} />}>
                          <BookOpenIcon />
                          Học bạ
                        </DropdownMenuItem>
                      )}
                      {isStudentOrParent && !acting && (
                        <>
                          <DropdownMenuItem className={MENU_ITEM} render={<Link href="/account#parent-links" />}>
                            <UsersIcon />
                            {user.role === "student" ? "Liên kết phụ huynh" : "Con đã liên kết"}
                          </DropdownMenuItem>
                          <DropdownMenuItem className={MENU_ITEM} render={<Link href="/account#sent-leads" />}>
                            <SendIcon />
                            Yêu cầu liên hệ đã gửi
                          </DropdownMenuItem>
                        </>
                      )}
                      {hasOwnProfile && (
                        <DropdownMenuItem className={MENU_ITEM} render={<Link href="/target-schools" />}>
                          <BookmarkCheckIcon />
                          Trường mục tiêu
                        </DropdownMenuItem>
                      )}
                    </DropdownMenuGroup>
                  )}

                  {/* Khu vực theo vai trò (trung tâm, quản trị) */}
                  {(user.role === "center" || canOpenAdmin) && (
                    <DropdownMenuGroup>
                      <DropdownMenuSeparator className="mt-2 mb-0" />
                      <DropdownMenuLabel className={MENU_LABEL}>{canOpenAdmin ? "Quản trị" : "Trung tâm"}</DropdownMenuLabel>
                      {canOpenAdmin && (
                        <DropdownMenuItem className={MENU_ITEM} render={<Link href="/admin" />}>
                          <LayoutDashboardIcon />
                          {isModerator ? "Khu vực kiểm duyệt" : "Trang quản trị"}
                        </DropdownMenuItem>
                      )}
                      <DropdownMenuItem className={MENU_ITEM} render={<Link href="/center/leads" />}>
                        <InboxIcon />
                        Yêu cầu liên hệ nhận được
                      </DropdownMenuItem>
                      {isModerator && (
                        <DropdownMenuItem className={MENU_ITEM} render={<Link href="/admin/centers" />}>
                          <Building2Icon />
                          Quản lý trung tâm
                        </DropdownMenuItem>
                      )}
                      {isAdmin && (
                        <DropdownMenuItem className={MENU_ITEM} render={<Link href="/admin/users" />}>
                          <ShieldIcon />
                          Quản trị người dùng
                        </DropdownMenuItem>
                      )}
                    </DropdownMenuGroup>
                  )}
                  </div>
                </DropdownMenuContent>
                </DropdownMenu>
              </>
            ) : (
              <>
                <p className="hidden text-sm text-white/85 @5xl:block">
                  Đã có tài khoản?{" "}
                  <Link href="/login" className="font-semibold text-white underline-offset-4 hover:underline">
                    Đăng nhập
                  </Link>
                </p>
                <Link
                  href="/login"
                  className={buttonVariants({ variant: "ghost", className: "@5xl:hidden !text-white hover:!bg-white/10" })}
                >
                  Đăng nhập
                </Link>
                <Link
                  href="/register"
                  className={buttonVariants({
                    variant: "outline",
                    size: "lg",
                    className: "!border-white/60 !bg-transparent !text-white hover:!bg-white/10",
                  })}
                >
                  Đăng ký ngay
                </Link>
              </>
            )}
          </div>
        </div>

        {/* Hàng dưới */}
        <nav aria-label="Điều hướng chính" className="flex h-11 items-center justify-between gap-2 overflow-x-auto [scrollbar-width:none] @2xl:gap-4">
          <ul className="-ml-3 flex shrink-0 items-center">
            {leftLinks.map((l) => {
              const active = isLinkActive(l.href);
              const Icon = l.icon;
              return (
                <li key={l.href}>
                  <Link
                    href={l.href}
                    aria-label={l.label}
                    title={l.label}
                    aria-current={active ? "page" : undefined}
                    className={cn(
                      "relative inline-flex h-11 items-center gap-1.5 whitespace-nowrap px-3 text-sm font-medium transition-colors",
                      "after:absolute after:inset-x-3 after:bottom-0 after:h-0.5 after:origin-left after:scale-x-0 after:rounded-full after:transition-transform after:duration-300 hover:after:scale-x-100 motion-reduce:after:transition-none",
                      "after:bg-white",
                      active ? "font-semibold text-white after:scale-x-100" : "text-white/80 hover:text-white",
                    )}
                  >
                    <Icon className="size-4 shrink-0" aria-hidden />
                    <span className={cn(!active && "hidden @6xl:inline")}>{l.label}</span>
                  </Link>
                </li>
              );
            })}
            <li>
              <DropdownMenu>
                <DropdownMenuTrigger
                  aria-label="Menu Chi phí"
                  className={cn(
                    "relative inline-flex h-11 cursor-pointer items-center gap-1.5 whitespace-nowrap px-3 text-sm font-medium outline-none transition-colors",
                    "after:absolute after:inset-x-3 after:bottom-0 after:h-0.5 after:origin-left after:scale-x-0 after:rounded-full after:transition-transform after:duration-300 hover:after:scale-x-100 motion-reduce:after:transition-none",
                    "after:bg-white",
                    isCostActive ? "font-semibold text-white after:scale-x-100" : "text-white/80 hover:text-white",
                  )}
                >
                  <CalculatorIcon className="size-4 shrink-0" aria-hidden />
                  <span className={cn(!isCostActive && "hidden @6xl:inline")}>Chi phí</span>
                  <ChevronDownIcon className="size-3.5 shrink-0 opacity-80" aria-hidden />
                </DropdownMenuTrigger>
                <DropdownMenuContent align="start" sideOffset={4} className="w-60 p-1.5">
                  <DropdownMenuItem className={MENU_ITEM} render={<Link href="/costs" />}>
                    <CalculatorIcon />
                    Tra cứu chi phí theo trường
                  </DropdownMenuItem>
                  <DropdownMenuItem className={MENU_ITEM} render={<Link href="/costs?mode=state" />}>
                    <MapPinIcon />
                    Tra cứu chi phí theo bang
                  </DropdownMenuItem>
                </DropdownMenuContent>
              </DropdownMenu>
            </li>
          </ul>
          <div className="flex shrink-0 items-center gap-1 @2xl:gap-2">
            {rightLinks.map((l) => {
              const active = isLinkActive(l.href);
              const Icon = l.icon;
              return (
                <Link
                  key={l.href}
                  href={l.href}
                  aria-label={l.label}
                  title={l.label}
                  aria-current={active ? "page" : undefined}
                  className={cn(
                    "relative inline-flex h-11 items-center gap-1.5 whitespace-nowrap px-3 text-sm font-medium transition-colors",
                    "after:absolute after:inset-x-3 after:bottom-0 after:h-0.5 after:origin-left after:scale-x-0 after:rounded-full after:transition-transform after:duration-300 hover:after:scale-x-100 motion-reduce:after:transition-none",
                    "after:bg-white",
                    active ? "font-semibold text-white after:scale-x-100" : "text-white/80 hover:text-white",
                  )}
                >
                  <Icon className="size-4 shrink-0" aria-hidden />
                  <span className={cn(!active && "hidden @6xl:inline")}>{l.label}</span>
                </Link>
              );
            })}
            {/* Phụ huynh đã liên kết đi qua mục menu "Quản lý con", không có nút riêng; chưa có con thì nút "Thêm con" (1.5e) */}
            {isParentWithoutChild && (
              <Link
                href="/parent/children/add"
                className={buttonVariants({
                  variant: "ghost",
                  size: "sm",
                  className: "ml-1 h-8 shrink-0 px-3 text-xs font-semibold !bg-white !text-brand-ink hover:!bg-white/90",
                })}
              >
                <PlusIcon className="size-3.5" aria-hidden />
                <span className="hidden @3xl:inline">Thêm con</span>
              </Link>
            )}
            {/* Trung tâm và quản trị không có hồ sơ để phân tích */}
            {isStudentOrParent && (acting !== null || (user?.role !== "parent" && isLinkedParent === false)) && (
              <Link
                href="/analysis"
                className={buttonVariants({
                  variant: "ghost",
                  size: "sm",
                  className: "ml-1 h-8 shrink-0 px-3 text-xs font-semibold !bg-white !text-brand-ink hover:!bg-white/90",
                })}
              >
                <span className="@7xl:hidden">Phân tích</span>
                <span className="hidden @7xl:inline">Phân tích hồ sơ của tôi</span>
              </Link>
            )}
          </div>
        </nav>
      </div>
    </header>
  );
}
