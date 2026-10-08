"use client";

import React, { useState, useEffect } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useAuth } from "@/contexts/AuthContext";
import { profileApi } from "@/lib/api";
import type { AcademicProfileResponse } from "@/types/api";

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
      ? "🎓 Học sinh"
      : user.role === "parent"
        ? "👨‍👩‍👧 Phụ huynh"
        : user.role === "center"
          ? "🏢 Trung tâm du học"
          : "🛡️ Quản trị viên";

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
    const list: { name: string; score: string | number; color: string }[] = [];
    if (profile.ielts != null) list.push({ name: "IELTS", score: profile.ielts, color: "bg-red-50 text-red-700 border-red-200" });
    if (profile.toefl != null) list.push({ name: "TOEFL iBT", score: profile.toefl, color: "bg-blue-50 text-blue-700 border-blue-200" });
    if (profile.duolingo != null) list.push({ name: "Duolingo", score: profile.duolingo, color: "bg-emerald-50 text-emerald-700 border-emerald-200" });
    if (profile.sat != null) list.push({ name: "SAT", score: profile.sat, color: "bg-purple-50 text-purple-700 border-purple-200" });
    if (profile.act != null) list.push({ name: "ACT", score: profile.act, color: "bg-indigo-50 text-indigo-700 border-indigo-200" });
    if (profile.gre != null) list.push({ name: "GRE", score: profile.gre, color: "bg-teal-50 text-teal-700 border-teal-200" });
    if (profile.gmat != null) list.push({ name: "GMAT", score: profile.gmat, color: "bg-cyan-50 text-cyan-700 border-cyan-200" });
    return list;
  })();

  return (
    <>
      {/* POPUP MODAL CẢNH BÁO KHI CHƯA CÓ BẢNG ĐIỂM */}
      {showWarningModal && !hasCompleteAcademicProfile && !isLoadingProfile && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/60 p-4 backdrop-blur-sm animate-in fade-in duration-200">
          <div className="relative w-full max-w-md rounded-2xl bg-white p-6 shadow-2xl border border-slate-100">
            {/* Nút đóng */}
            <button
              onClick={() => setShowWarningModal(false)}
              className="absolute right-4 top-4 text-slate-400 hover:text-slate-600 focus:outline-none"
              title="Đóng thông báo"
            >
              <svg className="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
              </svg>
            </button>

            <div className="flex items-center gap-3">
              <div className="flex h-12 w-12 shrink-0 items-center justify-center rounded-2xl bg-amber-100 text-amber-600">
                <svg className="h-6 w-6" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" />
                </svg>
              </div>
              <div>
                <h3 className="text-base font-bold text-slate-900">Yêu cầu hoàn thiện bảng điểm</h3>
                <p className="text-xs text-amber-700 font-medium">Hồ sơ học thuật chưa được thiết lập</p>
              </div>
            </div>

            <p className="mt-4 text-sm text-slate-600 leading-relaxed">
              Tài khoản của bạn hiện <strong>chưa có thông tin bảng điểm</strong> và các chứng chỉ học thuật. Vui lòng cập nhật bảng điểm để hệ thống hỗ trợ bạn tốt nhất.
            </p>

            <div className="mt-6 flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-end">
              <button
                type="button"
                onClick={() => setShowWarningModal(false)}
                className="rounded-xl border border-slate-200 bg-white px-4 py-2.5 text-xs font-semibold text-slate-600 hover:bg-slate-50 transition"
              >
                Để sau
              </button>
              <Link
                href="/profile/academic"
                className="inline-flex items-center justify-center gap-1.5 rounded-xl bg-blue-600 px-5 py-2.5 text-xs font-bold text-white shadow-md hover:bg-blue-700 transition"
              >
                <span>Điền bảng điểm ngay</span>
                <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M14 5l7 7m0 0l-7 7m7-7H3" />
                </svg>
              </Link>
            </div>
          </div>
        </div>
      )}

      <div className="mx-auto w-full max-w-2xl rounded-2xl border border-slate-200 bg-white p-8 shadow-sm">
        {/* TIÊU ĐỀ & ĐĂNG XUẤT */}
        <div className="flex items-center justify-between border-b border-slate-100 pb-6">
          <div>
            <h1 className="text-2xl font-bold tracking-tight text-slate-900">Hồ sơ tài khoản</h1>
            <p className="mt-1 text-sm text-slate-500">
              Thông tin cá nhân được bảo vệ và quản lý theo phiên làm việc an toàn
            </p>
          </div>
          <button
            onClick={handleLogout}
            disabled={isLoggingOut}
            className="rounded-lg border border-red-200 bg-white px-4 py-2 text-sm font-medium text-red-600 shadow-sm transition hover:bg-red-50 focus:outline-none focus:ring-2 focus:ring-red-500 disabled:opacity-50"
          >
            {isLoggingOut ? "Đang đăng xuất..." : "Đăng xuất"}
          </button>
        </div>

        <div className="mt-6 space-y-5">
          {/* VAI TRÒ & TRẠNG THÁI */}
          <div className="flex items-center justify-between rounded-xl bg-slate-50 p-4">
            <div>
              <div className="text-xs font-medium text-slate-500">Vai trò trong hệ thống</div>
              <div className="mt-1 text-base font-semibold text-slate-900">{roleLabel}</div>
            </div>
            {user.status === "pending" ? (
              <span className="inline-flex items-center rounded-full bg-amber-100 px-3 py-1 text-xs font-semibold text-amber-800">
                ⏳ Chờ xét duyệt
              </span>
            ) : user.status === "locked" ? (
              <span className="inline-flex items-center rounded-full bg-red-100 px-3 py-1 text-xs font-semibold text-red-800">
                🔒 Khóa bởi Quản trị viên
              </span>
            ) : user.status === "temp_locked" ? (
              <span className="inline-flex items-center rounded-full bg-orange-100 px-3 py-1 text-xs font-semibold text-orange-800">
                ⚠️ Tạm khóa (Sai 5 lần)
              </span>
            ) : user.status === "unverified" ? (
              <span className="inline-flex items-center rounded-full bg-yellow-100 px-3 py-1 text-xs font-semibold text-yellow-800">
                ✉️ Chưa xác thực Email
              </span>
            ) : (
              <span className="inline-flex items-center rounded-full bg-emerald-100 px-3 py-1 text-xs font-semibold text-emerald-800">
                ✓ Đang hoạt động
              </span>
            )}
          </div>

          {/* THÔNG TIN CÁ NHÂN */}
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <div className="rounded-xl border border-slate-200 p-4">
              <span className="block text-xs font-medium text-slate-500">Họ và tên</span>
              <span className="mt-1 block text-sm font-semibold text-slate-900">{user.fullName}</span>
            </div>

            <div className="rounded-xl border border-slate-200 p-4">
              <span className="block text-xs font-medium text-slate-500">Email đăng nhập</span>
              <span className="mt-1 block text-sm font-semibold text-slate-900">{user.email}</span>
            </div>
          </div>

          <div className="rounded-xl border border-slate-200 p-4">
            <span className="block text-xs font-medium text-slate-500">Số điện thoại liên hệ</span>
            <span className="mt-1 block text-sm font-semibold text-slate-900">
              {user.phone ? user.phone : <span className="italic text-slate-400">Chưa cung cấp</span>}
            </span>
            <p className="mt-2 text-xs text-slate-500">
              Số điện thoại được sử dụng để chuyên viên tư vấn liên hệ hỗ trợ hồ sơ du học.
            </p>
          </div>

          {/* THÔNG TIN BẢNG ĐIỂM & HỌC THUẬT CHO HỌC SINH / PHỤ HUYNH */}
          {(user.role === "student" || user.role === "parent") && (
            <div className="space-y-4">
              {isLoadingProfile ? (
                <div className="rounded-xl border border-slate-200 bg-slate-50 p-6 text-center text-sm text-slate-500">
                  Đang tải thông tin học thuật...
                </div>
              ) : !hasCompleteAcademicProfile ? (
                /* BANNER CẢNH BÁO NẾU CHƯA CÓ BẢNG ĐIỂM */
                <div className="rounded-2xl border-2 border-dashed border-amber-300 bg-gradient-to-r from-amber-50 to-orange-50 p-6 shadow-sm">
                  <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
                    <div className="flex items-start gap-3.5">
                      <div className="flex h-11 w-11 shrink-0 items-center justify-center rounded-xl bg-amber-500 text-white shadow-sm">
                        <svg className="h-6 w-6" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" />
                        </svg>
                      </div>
                      <div>
                        <div className="flex items-center gap-2">
                          <h3 className="text-base font-bold text-amber-900">Bảng điểm chưa được điền</h3>
                          <span className="rounded-full bg-amber-200 px-2 py-0.5 text-[10px] font-bold text-amber-900">
                            Yêu cầu nhập
                          </span>
                        </div>
                        <p className="mt-1 text-xs text-amber-800 leading-relaxed">
                          Bạn chưa nhập bảng điểm môn học theo từng học kỳ. Vui lòng hoàn thành để hệ thống lưu trữ và hỗ trợ tư vấn học thuật.
                        </p>
                      </div>
                    </div>
                    <Link
                      href="/profile/academic"
                      className="inline-flex items-center justify-center gap-2 rounded-xl bg-gradient-to-r from-amber-600 to-orange-600 px-5 py-3 text-xs font-bold text-white shadow-md hover:from-amber-700 hover:to-orange-700 transition whitespace-nowrap"
                    >
                      <span>Điền bảng điểm ngay</span>
                      <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M14 5l7 7m0 0l-7 7m7-7H3" />
                      </svg>
                    </Link>
                  </div>
                </div>
              ) : (
                /* THẺ HIỂN THỊ TÓM TẮT BẢNG ĐIỂM KHI ĐÃ CÓ DỮ LIỆU */
                <div className="rounded-2xl border border-blue-200 bg-white p-6 shadow-sm">
                  {/* Header Thẻ Bảng Điểm */}
                  <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between border-b border-slate-100 pb-4">
                    <div className="flex items-center gap-3">
                      <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-blue-50 text-blue-600">
                        <svg className="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 6.253v13m0-13C10.832 5.477 9.246 5 7.5 5S4.168 5.477 3 6.253v13C4.168 18.477 5.754 18 7.5 18s3.332.477 4.5 1.253m0-13C13.168 5.477 14.754 5 16.5 5c1.747 0 3.332.477 4.5 1.253v13C19.832 18.477 18.247 18 16.5 18c-1.746 0-3.332.477-4.5 1.253" />
                        </svg>
                      </div>
                      <div>
                        <div className="flex items-center gap-2">
                          <h3 className="text-base font-bold text-slate-900">Bảng điểm & Hồ sơ học thuật</h3>
                          <span className="rounded-full bg-emerald-100 px-2 py-0.5 text-[11px] font-bold text-emerald-800 border border-emerald-200">
                            ✓ Đã cập nhật
                          </span>
                        </div>
                        <p className="text-xs text-slate-500">
                          {profile?.targetLevel ? targetLevelLabels[profile.targetLevel] || profile.targetLevel : "Đại học"} • {profile?.currentGrade || "Lớp 11"}
                        </p>
                      </div>
                    </div>

                    <div className="flex items-center gap-2">
                      <Link
                        href="/profile/academic"
                        className="inline-flex items-center gap-1.5 rounded-lg border border-slate-300 bg-white px-3.5 py-2 text-xs font-bold text-slate-700 shadow-sm hover:bg-slate-50 transition"
                      >
                        <svg className="h-3.5 w-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15.232 5.232l3.536 3.536m-2.036-5.036a2.5 2.5 0 113.536 3.536L6.5 21.036H3v-3.572L16.732 3.732z" />
                        </svg>
                        Sửa bảng điểm
                      </Link>
                      <Link
                        href="/profile/academic/analysis"
                        className="inline-flex items-center gap-1.5 rounded-lg bg-indigo-600 px-3.5 py-2 text-xs font-bold text-white shadow-sm hover:bg-indigo-700 transition"
                      >
                        <span>🚀 Phân tích năng lực</span>
                      </Link>
                    </div>
                  </div>

                  {/* Chi tiết học vấn & GPA theo thang trường */}
                  <div className="mt-4 grid grid-cols-1 gap-3 sm:grid-cols-3">
                    <div className="rounded-xl bg-slate-50 p-3.5 border border-slate-100">
                      <span className="block text-[11px] font-medium text-slate-500 uppercase">Trường học</span>
                      <span className="mt-0.5 block text-sm font-bold text-slate-900 truncate">
                        {profile?.currentSchool}
                      </span>
                      <span className="text-[11px] text-slate-500">TN: {profile?.graduationYear || "Chưa rõ"}</span>
                    </div>

                    <div className="rounded-xl bg-slate-50 p-3.5 border border-slate-100">
                      <span className="block text-[11px] font-medium text-slate-500 uppercase">Khối lớp / Bậc học</span>
                      <span className="mt-0.5 block text-sm font-bold text-slate-900 truncate">
                        {profile?.currentGrade || "Chưa nhập"}
                      </span>
                      <span className="text-[11px] text-slate-500 truncate">
                        {profile?.targetLevel ? targetLevelLabels[profile.targetLevel] || profile.targetLevel : ""}
                      </span>
                    </div>

                    <div className="rounded-xl bg-blue-50/60 p-3.5 border border-blue-100">
                      <span className="block text-[11px] font-medium text-blue-700 uppercase">Điểm trung bình (GPA)</span>
                      <span className="mt-0.5 block text-lg font-extrabold text-blue-900">
                        {profile?.overallGpa?.toFixed(2) || "0.00"}{" "}
                        <span className="text-xs font-normal text-blue-600">(Thang {profile?.gradeScale})</span>
                      </span>
                    </div>
                  </div>

                  {/* Tiến độ các học kỳ */}
                  <div className="mt-4">
                    <span className="block text-xs font-semibold uppercase tracking-wider text-slate-600 mb-2">
                      Tiến độ bảng điểm ({profile?.terms?.length || 0} kỳ đã nhập):
                    </span>
                    <div className="flex flex-wrap gap-2">
                      {profile?.terms?.map((term, idx) => (
                        <div
                          key={idx}
                          className="flex items-center gap-1.5 rounded-lg bg-slate-100 px-3 py-1.5 text-xs font-medium text-slate-700 border border-slate-200"
                        >
                          <span className="h-1.5 w-1.5 rounded-full bg-blue-600"></span>
                          <span>{term.termName}</span>
                          <span className="text-[10px] text-slate-500">({term.scores.length} môn)</span>
                        </div>
                      ))}
                    </div>
                  </div>

                  {/* Danh sách chứng chỉ đã thi */}
                  <div className="mt-4 border-t border-slate-100 pt-3">
                    <span className="block text-xs font-semibold uppercase tracking-wider text-slate-600 mb-2">
                      Chứng chỉ chuẩn hóa & Ngoại ngữ:
                    </span>
                    {activeCertificates.length > 0 ? (
                      <div className="flex flex-wrap gap-2">
                        {activeCertificates.map((c, idx) => (
                          <span
                            key={idx}
                            className={`inline-flex items-center gap-1.5 rounded-lg border px-3 py-1 text-xs font-bold ${c.color}`}
                          >
                            <span>{c.name}:</span>
                            <span className="underline">{c.score}</span>
                          </span>
                        ))}
                      </div>
                    ) : (
                      <p className="text-xs italic text-slate-400">
                        Chưa nhập chứng chỉ nào (IELTS, TOEFL, SAT, GRE...). Bạn có thể cập nhật thêm khi có điểm.
                      </p>
                    )}
                  </div>
                </div>
              )}
            </div>
          )}
        </div>
      </div>
    </>
  );
}
