"use client";

import React, { useCallback, useState, useEffect, type ComponentType } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useAuth } from "@/contexts/AuthContext";
import { profileApi } from "@/lib/api";
import { cn } from "@/lib/utils";
import type { AcademicProfileResponse, ParentProfileOverviewDto } from "@/types/api";
import { Badge } from "@/components/ui/badge";
import { Button, buttonVariants } from "@/components/ui/button";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { AcademicSummaryCard, hasCompleteAcademicProfile } from "@/components/account/AcademicSummaryCard";
import { AccountSectionHeader } from "@/components/account/AccountSectionHeader";
import {
  BookOpenFilledIcon,
  CreditCardFilledIcon,
  HeartHandshakeFilledIcon,
  KeyRoundFilledIcon,
  ReceiptTextFilledIcon,
  SendFilledIcon,
  UserFilledIcon,
  UsersFilledIcon,
} from "@/components/account/AccountNavIcons";
import { ChangePasswordCard } from "@/components/account/ChangePasswordCard";
import { DeleteAccountCard } from "@/components/account/DeleteAccountCard";
import { ParentLinksCard } from "@/components/account/ParentLinksCard";
import { ParentAcademicSection, StudentParentOffers } from "@/components/account/ParentProfileShareCards";
import { PersonalInfoCard } from "@/components/account/PersonalInfoCard";
import { SentLeadsCard } from "@/components/account/SentLeadsCard";
import { SubscriptionCard } from "@/components/account/SubscriptionCard";
import { FamilyCard } from "@/components/account/FamilyCard";
import { OrdersHistoryView } from "@/components/billing/OrdersHistoryView";
import { billingApi } from "@/lib/billing-api";
import { useFetched } from "@/lib/use-fetched";
import type { SubscriptionSummaryDto } from "@/types/billing";
import {
  ArrowRightIcon,
  ArrowUpRightIcon,
  BookmarkCheckIcon,
  BookOpenIcon,
  Building2Icon,
  CreditCardIcon,
  GraduationCapIcon,
  HeartHandshakeIcon,
  KeyRoundIcon,
  LogOutIcon,
  ReceiptTextIcon,
  SendIcon,
  ShieldIcon,
  TriangleAlertIcon,
  UserIcon,
  UsersIcon,
  XIcon,
  type LucideIcon,
} from "lucide-react";

type SectionId = "personal" | "subscription" | "family" | "orders" | "academic" | "parent-links" | "sent-leads" | "password";

type NavGroupId = "account" | "billing" | "profile";

interface SectionItem {
  id: SectionId;
  label: string;
  icon: LucideIcon;
  filledIcon: ComponentType<{ className?: string }>;
  group: NavGroupId;
}

interface NavGroupMeta {
  id: NavGroupId;
  title: string;
}

const NAV_GROUPS: NavGroupMeta[] = [
  { id: "account", title: "Tài khoản" },
  { id: "billing", title: "Gói & Thanh toán" },
  { id: "profile", title: "Hồ sơ & Kết nối" },
];

// Mục đang mở lưu trên hash (#password...) để tải lại trang hoặc gửi link vẫn mở đúng mục.
// Giữ nguyên hash được yêu cầu: mục "family" chỉ có sau khi tải GET subscription, nên mục đang mở được suy ra khi render.
function readHash(): string {
  return typeof window === "undefined" ? "" : window.location.hash.slice(1);
}

export function AccountView() {
  const { user, logout } = useAuth();
  const router = useRouter();
  const [isLoggingOut, setIsLoggingOut] = useState(false);

  const isStudentOrParent = Boolean(user && (user.role === "student" || user.role === "parent"));

  // Mục Gói Gia đình chỉ hiện khi GET subscription có family (chủ hoặc thành viên của gói còn kỳ).
  // Đổi gì trong gói Gia đình thì tăng billingVersion để tải lại cả subscription lẫn family.
  const [billingVersion, setBillingVersion] = useState(0);
  const reloadBilling = useCallback(() => setBillingVersion((v) => v + 1), []);
  const sub = useFetched<SubscriptionSummaryDto | null>(
    isStudentOrParent ? `sub:${user?.id}:${billingVersion}` : "none",
    () => (isStudentOrParent ? billingApi.subscription() : Promise.resolve(null)),
  );
  const hasFamily = Boolean(sub.data?.family);

  const sections: SectionItem[] = [
    { id: "personal", label: "Thông tin cá nhân", icon: UserIcon, filledIcon: UserFilledIcon, group: "account" },
    { id: "password", label: "Bảo mật tài khoản", icon: KeyRoundIcon, filledIcon: KeyRoundFilledIcon, group: "account" },
    ...(isStudentOrParent
      ? [
          { id: "subscription" as const, label: "Gói dịch vụ", icon: CreditCardIcon, filledIcon: CreditCardFilledIcon, group: "billing" as const },
          ...(hasFamily
            ? [{ id: "family" as const, label: "Gói Gia đình", icon: HeartHandshakeIcon, filledIcon: HeartHandshakeFilledIcon, group: "billing" as const }]
            : []),
          { id: "orders" as const, label: "Lịch sử giao dịch", icon: ReceiptTextIcon, filledIcon: ReceiptTextFilledIcon, group: "billing" as const },
          { id: "academic" as const, label: "Hồ sơ học thuật", icon: BookOpenIcon, filledIcon: BookOpenFilledIcon, group: "profile" as const },
          {
            id: "parent-links" as const,
            label: user?.role === "student" ? "Liên kết phụ huynh" : "Con đã liên kết",
            icon: UsersIcon,
            filledIcon: UsersFilledIcon,
            group: "profile" as const,
          },
          // [UC-44] Yêu cầu liên hệ đã gửi
          { id: "sent-leads" as const, label: "Yêu cầu liên hệ đã gửi", icon: SendIcon, filledIcon: SendFilledIcon, group: "profile" as const },
        ]
      : []),
  ];

  const groupedSections = NAV_GROUPS.map((group) => ({
    ...group,
    items: sections.filter((s) => s.group === group.id),
  })).filter((group) => group.items.length > 0);

  const [requestedSection, setActiveSection] = useState<string>(readHash);
  const activeSection: SectionId = sections.find((s) => s.id === requestedSection)?.id ?? "personal";

  // Menu tài khoản ở thanh đầu trang trỏ tới /account#academic, #password...: đang ở trang này thì chỉ đổi hash, không tải lại
  useEffect(() => {
    const onHash = () => setActiveSection(readHash());
    window.addEventListener("hashchange", onHash);
    return () => window.removeEventListener("hashchange", onHash);
  }, []);

  // Trạng thái Hồ sơ học thuật
  const [profile, setProfile] = useState<AcademicProfileResponse | null>(null);
  const [isLoadingProfile, setIsLoadingProfile] = useState(isStudentOrParent);
  const [showWarningModal, setShowWarningModal] = useState(false);
  // Phụ huynh: hồ sơ các con đã liên kết. Có con liên kết thì xem hồ sơ con (chỉ đọc), không cảnh báo hồ sơ của mình.
  const [parentOverview, setParentOverview] = useState<ParentProfileOverviewDto | null>(null);
  const [profileVersion, setProfileVersion] = useState(0);
  const reloadProfile = useCallback(() => setProfileVersion((v) => v + 1), []);

  // Tải thông tin học thuật khi là học sinh hoặc phụ huynh.
  // Chỉ phụ thuộc id + role: lưu thông tin cá nhân gọi refreshUser() tạo object user mới,
  // nếu phụ thuộc cả user thì popup cảnh báo bảng điểm sẽ bật lại sau mỗi lần lưu.
  const userId = user?.id;
  const userRole = user?.role;
  useEffect(() => {
    if (!userId) return;
    if (userRole === "student" || userRole === "parent") {
      const overviewRequest =
        userRole === "parent"
          ? profileApi
              .getChildrenProfiles()
              .then((res) => res.data)
              .catch(() => null)
          : Promise.resolve(null);
      Promise.all([profileApi.getAcademicProfile().catch(() => null), overviewRequest])
        .then(([res, overview]) => {
          setProfile(res);
          setParentOverview(overview);
          // 1.5d: phụ huynh không có hồ sơ riêng nên không cảnh báo thiếu bảng điểm
          const isParent = userRole === "parent";
          // Chỉ bật popup ở lần tải đầu, không bật lại sau khi chọn chuyển hồ sơ
          if (profileVersion === 0 && !isParent && !hasCompleteAcademicProfile(res)) {
            setShowWarningModal(true);
          }
        })
        .finally(() => {
          setIsLoadingProfile(false);
        });
    }
  }, [userId, userRole, profileVersion]);

  if (!user) return null;

  const selectSection = (id: SectionId) => {
    setActiveSection(id);
    window.history.replaceState(null, "", `#${id}`);
  };

  const handleLogout = async () => {
    setIsLoggingOut(true);
    await logout();
    router.push("/login");
  };

  const RoleIcon =
    user.role === "student"
      ? GraduationCapIcon
      : user.role === "parent"
        ? UsersIcon
        : user.role === "center"
          ? Building2Icon
          : ShieldIcon;

  return (
    <>
      {/* POPUP MODAL CẢNH BÁO KHI CHƯA CÓ BẢNG ĐIỂM */}
      <Dialog
        open={showWarningModal && !hasCompleteAcademicProfile(profile) && !isLoadingProfile}
        onOpenChange={(open) => {
          if (!open) setShowWarningModal(false);
        }}
      >
        <DialogContent showCloseButton={false}>
          <Button
            variant="ghost"
            size="icon-sm"
            onClick={() => setShowWarningModal(false)}
            className="absolute top-2 right-2"
            title="Đóng thông báo"
          >
            <XIcon aria-hidden />
          </Button>

          <DialogHeader>
            <DialogTitle>Yêu cầu hoàn thiện bảng điểm</DialogTitle>
            <Badge variant="warn">
              <TriangleAlertIcon aria-hidden />
              Hồ sơ học thuật chưa được thiết lập
            </Badge>
          </DialogHeader>

          <DialogDescription>
            Tài khoản của bạn hiện <strong>chưa có thông tin bảng điểm</strong> và các chứng chỉ học thuật. Vui lòng cập nhật bảng điểm để hệ thống hỗ trợ bạn tốt nhất.
          </DialogDescription>

          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => setShowWarningModal(false)}>
              Để sau
            </Button>
            <Link href="/analysis" className={buttonVariants()}>
              <span>Điền bảng điểm ngay</span>
              <ArrowRightIcon aria-hidden />
            </Link>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <div className="grid grid-cols-1 gap-8 md:grid-cols-[260px_minmax(0,1fr)] lg:grid-cols-[276px_minmax(0,1fr)] lg:gap-12">
        {/* THANH BÊN TRÁI: nền trong suốt, danh tính gọn + điều hướng phân nhóm giống AdminShell */}
        <aside className="md:sticky md:top-28 md:self-start">
          {/* Thông tin người dùng: icon vai trò bo góc nhẹ giống AdminUserMenu, không nền thẻ, không đường kẻ */}
          <div className="flex items-center gap-3 px-1 pb-3">
            <span
              aria-hidden="true"
              className="flex size-9 shrink-0 items-center justify-center rounded-lg border border-border bg-surface text-muted-foreground"
            >
              <RoleIcon className="size-4" />
            </span>
            <div className="min-w-0 flex-1">
              <div className="truncate text-sm font-semibold text-ink" title={user.fullName}>
                {user.fullName}
              </div>
              <div className="truncate text-xs text-ink-2" title={user.email}>
                {user.email}
              </div>
            </div>
          </div>

          {/* Menu điều hướng phân nhóm */}
          <nav
            aria-label="Mục tài khoản"
            className="no-scrollbar mt-2 flex gap-1.5 overflow-x-auto pb-1 [scrollbar-width:none] md:flex-col md:gap-0 md:overflow-visible md:pb-0"
          >
            {groupedSections.map((group, groupIdx) => (
              <div key={group.id} className="flex shrink-0 gap-1.5 md:flex-col md:gap-1">
                <div
                  className={cn(
                    "hidden px-3.5 pb-1 text-[13px] leading-4 font-medium text-ink-3/85 md:block",
                    groupIdx === 0 ? "pt-1" : "pt-4",
                  )}
                >
                  {group.title}
                </div>
                <ul className="flex gap-1.5 md:flex-col md:gap-1">
                  {group.items.map((item) => {
                    const active = activeSection === item.id;
                    const Icon = active ? item.filledIcon : item.icon;
                    return (
                      <li key={item.id} className="shrink-0">
                        <button
                          type="button"
                          onClick={() => selectSection(item.id)}
                          aria-current={active ? "page" : undefined}
                          className={cn(
                            "flex min-h-10 w-full shrink-0 cursor-pointer items-center gap-3 rounded-xl px-3.5 py-2.5 text-left text-sm font-medium whitespace-nowrap transition-colors",
                            "focus-visible:ring-3 focus-visible:ring-ring/50 focus-visible:outline-none",
                            active
                              ? "bg-brand-wash font-semibold text-brand"
                              : "text-ink-2 hover:bg-foreground/[0.05] hover:text-ink",
                          )}
                        >
                          <Icon className="size-[18px] shrink-0" aria-hidden />
                          <span>{item.label}</span>
                        </button>
                      </li>
                    );
                  })}

                  {group.id === "profile" && isStudentOrParent && (
                    <li className="shrink-0">
                      <Link
                        href="/target-schools"
                        className={cn(
                          "flex min-h-10 w-full shrink-0 items-center gap-3 rounded-xl px-3.5 py-2.5 text-left text-sm font-medium whitespace-nowrap text-ink-2 transition-colors",
                          "hover:bg-foreground/[0.05] hover:text-ink focus-visible:ring-3 focus-visible:ring-ring/50 focus-visible:outline-none",
                        )}
                      >
                        <BookmarkCheckIcon className="size-[18px] shrink-0" aria-hidden />
                        <span>Trường mục tiêu</span>
                        <ArrowUpRightIcon className="ml-auto hidden size-3.5 text-ink-3 md:block" aria-hidden />
                      </Link>
                    </li>
                  )}
                </ul>
              </div>
            ))}

            <div className="flex shrink-0 md:mt-3 md:border-t md:border-line md:pt-3">
              <button
                type="button"
                onClick={handleLogout}
                disabled={isLoggingOut}
                className={cn(
                  "flex min-h-10 w-full shrink-0 cursor-pointer items-center gap-3 rounded-xl px-3.5 py-2.5 text-left text-sm font-medium whitespace-nowrap text-risk transition-colors disabled:cursor-default",
                  "hover:bg-risk-wash/75 focus-visible:ring-3 focus-visible:ring-ring/50 focus-visible:outline-none disabled:opacity-50",
                )}
              >
                <LogOutIcon className="size-[18px] shrink-0" aria-hidden />
                <span>{isLoggingOut ? "Đang đăng xuất..." : "Đăng xuất"}</span>
              </button>
            </div>
          </nav>
        </aside>

        {/* NỘI DUNG MỤC ĐANG CHỌN: nền trong suốt, tiêu đề ngoài thẻ, căn thẳng mép phải với thanh đầu trang */}
        <section className="min-w-0">
          {activeSection === "personal" && <PersonalInfoCard />}
          {activeSection === "subscription" && <SubscriptionCard />}
          {activeSection === "family" && <FamilyCard version={billingVersion} onChanged={reloadBilling} />}
          {activeSection === "orders" && <OrdersHistoryView inAccount />}
          {activeSection === "academic" &&
            (parentOverview ? (
              <ParentAcademicSection overview={parentOverview} onChanged={reloadProfile} />
            ) : (
              <div className="space-y-6">
                {user.role === "student" && <StudentParentOffers onChanged={reloadProfile} />}
                <AcademicSummaryCard profile={profile} isLoading={isLoadingProfile} />
              </div>
            ))}
          {activeSection === "parent-links" && <ParentLinksCard />}
          {activeSection === "sent-leads" && <SentLeadsCard />}
          {activeSection === "password" && (
            <div>
              <AccountSectionHeader
                title="Bảo mật tài khoản"
                description="Đổi mật khẩu định kỳ để giữ an toàn cho tài khoản và dữ liệu hồ sơ của bạn."
              />
              <div className="space-y-6">
                <ChangePasswordCard />
                {/* [UC-80] Chỉ học sinh, phụ huynh được tự xóa tài khoản */}
                {isStudentOrParent && <DeleteAccountCard />}
              </div>
            </div>
          )}
        </section>
      </div>
    </>
  );
}
