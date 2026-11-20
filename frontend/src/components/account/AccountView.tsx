"use client";

import React, { useCallback, useState, useEffect } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useAuth } from "@/contexts/AuthContext";
import { profileApi } from "@/lib/api";
import { cn } from "@/lib/utils";
import type { AcademicProfileResponse, ParentProfileOverviewDto } from "@/types/api";
import { Badge } from "@/components/ui/badge";
import { Button, buttonVariants } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { AcademicSummaryCard, hasCompleteAcademicProfile } from "@/components/account/AcademicSummaryCard";
import { ChangePasswordCard } from "@/components/account/ChangePasswordCard";
import { DeleteAccountCard } from "@/components/account/DeleteAccountCard";
import { ParentLinksCard } from "@/components/account/ParentLinksCard";
import { ParentAcademicSection, StudentParentOffers } from "@/components/account/ParentProfileShareCards";
import { PersonalInfoCard } from "@/components/account/PersonalInfoCard";
import { SentLeadsCard } from "@/components/account/SentLeadsCard";
import { SubscriptionCard } from "@/components/account/SubscriptionCard";
import {
  ArrowRightIcon,
  BookmarkCheckIcon,
  BookOpenIcon,
  Building2Icon,
  CrownIcon,
  GraduationCapIcon,
  KeyRoundIcon,
  LogOutIcon,
  SendIcon,
  ShieldIcon,
  TriangleAlertIcon,
  UserIcon,
  UsersIcon,
  XIcon,
  type LucideIcon,
} from "lucide-react";

type SectionId = "personal" | "subscription" | "academic" | "parent-links" | "sent-leads" | "password";

interface SectionItem {
  id: SectionId;
  label: string;
  icon: LucideIcon;
}

// Mục đang mở lưu trên hash (#password...) để tải lại trang hoặc gửi link vẫn mở đúng mục
function readSectionFromHash(available: SectionItem[]): SectionId {
  const hash = typeof window === "undefined" ? "" : window.location.hash.slice(1);
  return available.find((s) => s.id === hash)?.id ?? "personal";
}

export function AccountView() {
  const { user, logout } = useAuth();
  const router = useRouter();
  const [isLoggingOut, setIsLoggingOut] = useState(false);

  const isStudentOrParent = Boolean(user && (user.role === "student" || user.role === "parent"));

  const sections: SectionItem[] = [
    { id: "personal", label: "Thông tin cá nhân", icon: UserIcon },
    ...(isStudentOrParent
      ? [
          { id: "subscription" as const, label: "Gói dịch vụ", icon: CrownIcon },
          { id: "academic" as const, label: "Hồ sơ học thuật", icon: BookOpenIcon },
          {
            id: "parent-links" as const,
            label: user?.role === "student" ? "Liên kết phụ huynh" : "Con đã liên kết",
            icon: UsersIcon,
          },
          // [UC-44] Yêu cầu liên hệ đã gửi
          { id: "sent-leads" as const, label: "Yêu cầu liên hệ đã gửi", icon: SendIcon },
        ]
      : []),
    { id: "password", label: "Bảo mật tài khoản", icon: KeyRoundIcon },
  ];

  const [activeSection, setActiveSection] = useState<SectionId>(() => readSectionFromHash(sections));

  // Menu tài khoản ở thanh đầu trang trỏ tới /account#academic, #password...: đang ở trang này thì chỉ đổi hash, không tải lại
  const sectionIds = sections.map((s) => s.id).join(",");
  useEffect(() => {
    const onHash = () => {
      const id = window.location.hash.slice(1);
      if (sectionIds.split(",").includes(id)) setActiveSection(id as SectionId);
    };
    window.addEventListener("hashchange", onHash);
    return () => window.removeEventListener("hashchange", onHash);
  }, [sectionIds]);

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

  const roleLabel =
    user.role === "student"
      ? "Học sinh"
      : user.role === "parent"
        ? "Phụ huynh"
        : user.role === "center"
          ? "Trung tâm du học"
          : "Quản trị viên";

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
          {/* Nút đóng */}
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

      <div className="mx-auto w-full max-w-5xl">
        <h1 className="mb-4 text-h1">Tài khoản của tôi</h1>

        <div className="grid grid-cols-1 gap-6 md:grid-cols-[240px_minmax(0,1fr)]">
          {/* THANH BÊN TRÁI: người dùng + danh sách mục */}
          <aside className="space-y-3 md:sticky md:top-20 md:self-start">
            <Card size="sm">
              <CardContent className="space-y-1">
                <div className="truncate font-semibold" title={user.fullName}>{user.fullName}</div>
                <div className="truncate text-body-s" title={user.email}>{user.email}</div>
                <div className="flex items-center gap-1.5 text-body-s">
                  <RoleIcon className="size-3.5" aria-hidden />
                  {roleLabel}
                </div>
              </CardContent>
            </Card>

            <nav aria-label="Mục tài khoản" className="flex gap-1 overflow-x-auto pb-1 md:flex-col md:overflow-visible md:pb-0">
              {sections.map(({ id, label, icon: Icon }) => (
                <button
                  key={id}
                  type="button"
                  onClick={() => selectSection(id)}
                  aria-current={activeSection === id ? "page" : undefined}
                  className={cn(
                    "flex shrink-0 items-center gap-2 rounded-md px-3 py-2 text-left text-sm whitespace-nowrap transition-colors",
                    "hover:bg-muted focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none",
                    activeSection === id ? "bg-muted font-semibold text-foreground" : "text-muted-foreground"
                  )}
                >
                  <Icon className="size-4" aria-hidden />
                  {label}
                </button>
              ))}

              {isStudentOrParent && (
                <Link
                  href="/target-schools"
                  className={cn(
                    "flex shrink-0 items-center gap-2 rounded-md px-3 py-2 text-left text-sm whitespace-nowrap transition-colors",
                    "text-muted-foreground hover:bg-muted hover:text-foreground focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none"
                  )}
                >
                  <BookmarkCheckIcon className="size-4" aria-hidden />
                  Trường mục tiêu
                </Link>
              )}

              <button
                type="button"
                onClick={handleLogout}
                disabled={isLoggingOut}
                className={cn(
                  "flex shrink-0 items-center gap-2 rounded-md px-3 py-2 text-left text-sm whitespace-nowrap text-destructive transition-colors",
                  "hover:bg-destructive/10 focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none disabled:opacity-50",
                  "md:mt-2 md:border-t md:pt-3"
                )}
              >
                <LogOutIcon className="size-4" aria-hidden />
                {isLoggingOut ? "Đang đăng xuất..." : "Đăng xuất"}
              </button>
            </nav>
          </aside>

          {/* NỘI DUNG MỤC ĐANG CHỌN */}
          <section className="min-w-0">
            {activeSection === "personal" && <PersonalInfoCard />}
            {activeSection === "subscription" && <SubscriptionCard />}
            {activeSection === "academic" &&
              (parentOverview ? (
                <ParentAcademicSection overview={parentOverview} onChanged={reloadProfile} />
              ) : (
                <div className="space-y-4">
                  {user.role === "student" && <StudentParentOffers onChanged={reloadProfile} />}
                  <AcademicSummaryCard profile={profile} isLoading={isLoadingProfile} />
                </div>
              ))}
            {activeSection === "parent-links" && <ParentLinksCard />}
            {activeSection === "sent-leads" && <SentLeadsCard />}
            {activeSection === "password" && (
              <div className="space-y-6">
                <ChangePasswordCard />
                {/* [UC-80] Chỉ học sinh, phụ huynh được tự xóa tài khoản */}
                {isStudentOrParent && <DeleteAccountCard />}
              </div>
            )}
          </section>
        </div>
      </div>
    </>
  );
}
