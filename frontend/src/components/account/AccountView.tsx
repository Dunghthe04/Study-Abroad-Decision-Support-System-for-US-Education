"use client";

import React, { useState, useEffect } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useAuth } from "@/contexts/AuthContext";
import { profileApi } from "@/lib/api";
import type { AcademicProfileResponse } from "@/types/api";
import { Badge } from "@/components/ui/badge";
import { Button, buttonVariants } from "@/components/ui/button";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Card, CardAction, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Separator } from "@/components/ui/separator";
import { Spinner } from "@/components/ui/spinner";
import {
  ArrowRightIcon,
  BookOpenIcon,
  Building2Icon,
  CheckIcon,
  GraduationCapIcon,
  HourglassIcon,
  LockIcon,
  MailIcon,
  PencilIcon,
  RocketIcon,
  ShieldIcon,
  TriangleAlertIcon,
  UsersIcon,
  XIcon,
} from "lucide-react";

export function AccountView() {
  const { user, logout } = useAuth();
  const router = useRouter();
  const [isLoggingOut, setIsLoggingOut] = useState(false);

  // Trạng thái Hồ sơ học thuật
  const [profile, setProfile] = useState<AcademicProfileResponse | null>(null);
  const [isLoadingProfile, setIsLoadingProfile] = useState(
    () => Boolean(user && (user.role === "student" || user.role === "parent"))
  );
  const [showWarningModal, setShowWarningModal] = useState(false);

  // Tải thông tin học thuật khi là học sinh hoặc phụ huynh
  useEffect(() => {
    if (!user) return;
    if (user.role === "student" || user.role === "parent") {
      profileApi
        .getAcademicProfile()
        .then((res) => {
          setProfile(res);
          const isComplete = Boolean(
            res &&
              res.currentSchool &&
              res.currentSchool.trim() !== "" &&
              res.terms &&
              res.terms.length > 0
          );
          if (!isComplete) {
            setShowWarningModal(true);
          }
        })
        .catch(() => {
          setShowWarningModal(true);
        })
        .finally(() => {
          setIsLoadingProfile(false);
        });
    }
  }, [user]);

  if (!user) return null;

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

  const hasCompleteAcademicProfile = Boolean(
    profile &&
      profile.currentSchool &&
      profile.currentSchool.trim() !== "" &&
      profile.terms &&
      profile.terms.length > 0
  );

  // Nhãn bậc học
  const targetLevelLabels: Record<string, string> = {
    middle_school: "Trung học cơ sở / Cấp 2 (Middle School)",
    secondary: "Trung học phổ thông / Cấp 3 (High School)",
    community_college: "Cao đẳng cộng đồng 2+2 (Community College)",
    undergraduate: "Đại học 4 năm (Undergraduate)",
    master: "Thạc sĩ (Master)",
    phd: "Tiến sĩ (PhD)",
  };

  // Danh sách chứng chỉ có điểm
  const activeCertificates = (() => {
    if (!profile) return [];
    const list: { name: string; score: string | number }[] = [];
    if (profile.ielts != null) list.push({ name: "IELTS", score: profile.ielts });
    if (profile.toefl != null) list.push({ name: "TOEFL iBT", score: profile.toefl });
    if (profile.duolingo != null) list.push({ name: "Duolingo", score: profile.duolingo });
    if (profile.sat != null) list.push({ name: "SAT", score: profile.sat });
    if (profile.act != null) list.push({ name: "ACT", score: profile.act });
    if (profile.gre != null) list.push({ name: "GRE", score: profile.gre });
    if (profile.gmat != null) list.push({ name: "GMAT", score: profile.gmat });
    return list;
  })();

  return (
    <>
      {/* POPUP MODAL CẢNH BÁO KHI CHƯA CÓ BẢNG ĐIỂM */}
      <Dialog
        open={showWarningModal && !hasCompleteAcademicProfile && !isLoadingProfile}
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
            <Link href="/profile/academic" className={buttonVariants()}>
              <span>Điền bảng điểm ngay</span>
              <ArrowRightIcon aria-hidden />
            </Link>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Card className="mx-auto w-full max-w-2xl">
        {/* TIÊU ĐỀ & ĐĂNG XUẤT */}
        <CardHeader className="border-b">
          <CardTitle>
            <h1 className="text-h1">Hồ sơ tài khoản</h1>
          </CardTitle>
          <CardDescription>
            Thông tin cá nhân được bảo vệ và quản lý theo phiên làm việc an toàn
          </CardDescription>
          <CardAction>
            <Button variant="destructive" size="lg" onClick={handleLogout} disabled={isLoggingOut}>
              {isLoggingOut ? "Đang đăng xuất..." : "Đăng xuất"}
            </Button>
          </CardAction>
        </CardHeader>

        <CardContent className="space-y-5">
          {/* VAI TRÒ & TRẠNG THÁI */}
          <Card size="sm">
            <CardContent className="flex items-center justify-between">
              <div>
                <div className="text-label">Vai trò trong hệ thống</div>
                <div className="mt-1 flex items-center gap-2 font-semibold">
                  <RoleIcon className="size-4" aria-hidden />
                  {roleLabel}
                </div>
              </div>
              {user.status === "pending" ? (
                <Badge variant="warn"><HourglassIcon aria-hidden />Chờ xét duyệt</Badge>
              ) : user.status === "locked" ? (
                <Badge variant="risk"><LockIcon aria-hidden />Khóa bởi Quản trị viên</Badge>
              ) : user.status === "temp_locked" ? (
                <Badge variant="warn"><TriangleAlertIcon aria-hidden />Tạm khóa (Sai 5 lần)</Badge>
              ) : user.status === "unverified" ? (
                <Badge variant="warn"><MailIcon aria-hidden />Chưa xác thực Email</Badge>
              ) : (
                <Badge variant="ok"><CheckIcon aria-hidden />Đang hoạt động</Badge>
              )}
            </CardContent>
          </Card>

          {/* THÔNG TIN CÁ NHÂN */}
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <Card size="sm">
              <CardContent>
                <span className="block text-label">Họ và tên</span>
                <span className="mt-1 block font-semibold">{user.fullName}</span>
              </CardContent>
            </Card>

            <Card size="sm">
              <CardContent>
                <span className="block text-label">Email đăng nhập</span>
                <span className="mt-1 block font-semibold">{user.email}</span>
              </CardContent>
            </Card>
          </div>

          <Card size="sm">
            <CardContent>
              <span className="block text-label">Số điện thoại liên hệ</span>
              <span className="mt-1 block text-numeric">
                {user.phone ? user.phone : <span className="italic text-muted-foreground">Chưa cung cấp</span>}
              </span>
              <p className="mt-2 text-body-s">
                Số điện thoại được sử dụng để chuyên viên tư vấn liên hệ hỗ trợ hồ sơ du học.
              </p>
            </CardContent>
          </Card>

          {/* THÔNG TIN BẢNG ĐIỂM & HỌC THUẬT CHO HỌC SINH / PHỤ HUYNH */}
          {(user.role === "student" || user.role === "parent") && (
            <div className="space-y-4">
              {isLoadingProfile ? (
                <div className="flex items-center justify-center gap-2 text-muted-foreground">
                  <Spinner />
                  Đang tải thông tin học thuật...
                </div>
              ) : !hasCompleteAcademicProfile ? (
                /* BANNER CẢNH BÁO NẾU CHƯA CÓ BẢNG ĐIỂM */
                <>
                  <Alert variant="warning">
                    <TriangleAlertIcon aria-hidden />
                    <AlertTitle className="flex items-center gap-2">
                      <h3>Bảng điểm chưa được điền</h3>
                      <Badge variant="warn">Yêu cầu nhập</Badge>
                    </AlertTitle>
                    <AlertDescription>
                      Bạn chưa nhập bảng điểm môn học theo từng học kỳ. Vui lòng hoàn thành để hệ thống lưu trữ và hỗ trợ tư vấn học thuật.
                    </AlertDescription>
                  </Alert>
                  <Link href="/profile/academic" className={buttonVariants({ size: "lg" })}>
                    <span>Điền bảng điểm ngay</span>
                    <ArrowRightIcon aria-hidden />
                  </Link>
                </>
              ) : (
                /* THẺ HIỂN THỊ TÓM TẮT BẢNG ĐIỂM KHI ĐÃ CÓ DỮ LIỆU */
                <Card>
                  {/* Header Thẻ Bảng Điểm */}
                  <CardHeader className="border-b">
                    <CardTitle className="flex flex-wrap items-center gap-2">
                      <BookOpenIcon className="size-5" aria-hidden />
                      <h3>Bảng điểm & Hồ sơ học thuật</h3>
                      <Badge variant="ok">
                        <CheckIcon aria-hidden />
                        Đã cập nhật
                      </Badge>
                    </CardTitle>
                    <CardDescription>
                      {profile?.targetLevel ? targetLevelLabels[profile.targetLevel] || profile.targetLevel : "Đại học"} • {profile?.currentGrade || "Lớp 11"}
                    </CardDescription>
                    <div className="flex flex-wrap items-center gap-2">
                      <Link href="/profile/academic" className={buttonVariants({ variant: "outline" })}>
                        <PencilIcon aria-hidden />
                        Sửa bảng điểm
                      </Link>
                      <Link href="/profile/academic/analysis" className={buttonVariants()}>
                        <RocketIcon aria-hidden />
                        <span>Phân tích năng lực</span>
                      </Link>
                    </div>
                  </CardHeader>

                  <CardContent className="space-y-4">
                    {/* Chi tiết học vấn & GPA theo thang trường */}
                    <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
                      <Card size="sm">
                        <CardContent>
                          <span className="block text-label">Trường học</span>
                          <span className="mt-0.5 block truncate font-semibold">
                            {profile?.currentSchool}
                          </span>
                          <span className="text-body-s">TN: {profile?.graduationYear || "Chưa rõ"}</span>
                        </CardContent>
                      </Card>

                      <Card size="sm">
                        <CardContent>
                          <span className="block text-label">Khối lớp / Bậc học</span>
                          <span className="mt-0.5 block truncate font-semibold">
                            {profile?.currentGrade || "Chưa nhập"}
                          </span>
                          <span className="text-body-s">
                            {profile?.targetLevel ? targetLevelLabels[profile.targetLevel] || profile.targetLevel : ""}
                          </span>
                        </CardContent>
                      </Card>

                      <Card size="sm">
                        <CardContent>
                          <span className="block text-label">Điểm trung bình (GPA)</span>
                          <span className="mt-0.5 block text-lg text-numeric">
                            {profile?.overallGpa?.toFixed(2) || "0.00"}{" "}
                            <span className="text-xs font-normal text-muted-foreground">(Thang {profile?.gradeScale})</span>
                          </span>
                        </CardContent>
                      </Card>
                    </div>

                    {/* Tiến độ các học kỳ */}
                    <div>
                      <span className="mb-2 block text-label">
                        Tiến độ bảng điểm ({profile?.terms?.length || 0} kỳ đã nhập):
                      </span>
                      <div className="flex flex-wrap gap-2">
                        {profile?.terms?.map((term, idx) => (
                          <Badge key={idx} variant="secondary">
                            <span>{term.termName}</span>
                            <span>({term.scores.length} môn)</span>
                          </Badge>
                        ))}
                      </div>
                    </div>

                    {/* Danh sách chứng chỉ đã thi */}
                    <Separator />
                    <div>
                      <span className="mb-2 block text-label">
                        Chứng chỉ chuẩn hóa & Ngoại ngữ:
                      </span>
                      {activeCertificates.length > 0 ? (
                        <div className="flex flex-wrap gap-2">
                          {activeCertificates.map((c, idx) => (
                            <Badge key={idx} variant="brand">
                              <span>{c.name}:</span>
                              <span className="text-numeric">{c.score}</span>
                            </Badge>
                          ))}
                        </div>
                      ) : (
                        <p className="text-xs italic text-muted-foreground">
                          Chưa nhập chứng chỉ nào (IELTS, TOEFL, SAT, GRE...). Bạn có thể cập nhật thêm khi có điểm.
                        </p>
                      )}
                    </div>
                  </CardContent>
                </Card>
              )}
            </div>
          )}
        </CardContent>
      </Card>
    </>
  );
}
