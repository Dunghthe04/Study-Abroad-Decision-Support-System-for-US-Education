"use client";

import React from "react";
import Link from "next/link";
import type { AcademicProfileResponse } from "@/types/api";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { buttonVariants } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Separator } from "@/components/ui/separator";
import { Spinner } from "@/components/ui/spinner";
import { ArrowRightIcon, BookOpenIcon, CheckIcon, PencilIcon, RocketIcon, TriangleAlertIcon } from "lucide-react";

// Nhãn bậc học
const targetLevelLabels: Record<string, string> = {
  community_college: "Cao đẳng cộng đồng 2+2 (Community College)",
  undergraduate: "Đại học 4 năm (Undergraduate)",
  master: "Thạc sĩ (Master)",
  phd: "Tiến sĩ (PhD)",
};

export function hasCompleteAcademicProfile(profile: AcademicProfileResponse | null): boolean {
  return Boolean(
    profile &&
      profile.currentSchool &&
      profile.currentSchool.trim() !== "" &&
      profile.terms &&
      profile.terms.length > 0
  );
}

interface AcademicSummaryCardProps {
  profile: AcademicProfileResponse | null;
  isLoading: boolean;
}

// Tóm tắt bảng điểm & hồ sơ học thuật cho học sinh / phụ huynh
export function AcademicSummaryCard({ profile, isLoading }: AcademicSummaryCardProps) {
  if (isLoading) {
    return (
      <Card>
        <CardContent className="flex items-center justify-center gap-2 text-muted-foreground">
          <Spinner />
          Đang tải thông tin học thuật...
        </CardContent>
      </Card>
    );
  }

  if (!hasCompleteAcademicProfile(profile)) {
    /* BANNER CẢNH BÁO NẾU CHƯA CÓ BẢNG ĐIỂM */
    return (
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <BookOpenIcon className="size-5" aria-hidden />
            <h2>Hồ sơ học thuật</h2>
          </CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
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
        </CardContent>
      </Card>
    );
  }

  // Danh sách chứng chỉ có điểm
  const activeCertificates: { name: string; score: string | number }[] = [];
  if (profile?.ielts != null) activeCertificates.push({ name: "IELTS", score: profile.ielts });
  if (profile?.toefl != null) activeCertificates.push({ name: "TOEFL iBT", score: profile.toefl });
  if (profile?.duolingo != null) activeCertificates.push({ name: "Duolingo", score: profile.duolingo });
  if (profile?.sat != null) activeCertificates.push({ name: "SAT", score: profile.sat });
  if (profile?.act != null) activeCertificates.push({ name: "ACT", score: profile.act });
  if (profile?.gre != null) activeCertificates.push({ name: "GRE", score: profile.gre });
  if (profile?.gmat != null) activeCertificates.push({ name: "GMAT", score: profile.gmat });

  return (
    <Card>
      {/* Header Thẻ Bảng Điểm */}
      <CardHeader className="border-b">
        <CardTitle className="flex flex-wrap items-center gap-2">
          <BookOpenIcon className="size-5" aria-hidden />
          <h2>Bảng điểm & Hồ sơ học thuật</h2>
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
              <span className="mt-0.5 block truncate font-semibold">{profile?.currentSchool}</span>
              <span className="text-body-s">TN: {profile?.graduationYear || "Chưa rõ"}</span>
            </CardContent>
          </Card>

          <Card size="sm">
            <CardContent>
              <span className="block text-label">Khối lớp / Bậc học</span>
              <span className="mt-0.5 block truncate font-semibold">{profile?.currentGrade || "Chưa nhập"}</span>
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
          <span className="mb-2 block text-label">Chứng chỉ chuẩn hóa & Ngoại ngữ:</span>
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
  );
}
