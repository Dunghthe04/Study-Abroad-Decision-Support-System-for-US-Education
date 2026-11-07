"use client";

import { Suspense, useEffect } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import Link from "next/link";
import { ArrowRightIcon, ClipboardListIcon, MessageCircleIcon, ShieldAlertIcon, ExternalLinkIcon } from "lucide-react";
import { buttonVariants } from "@/components/ui/button";
import { OpenAssistantButton } from "@/components/assistant/OpenAssistantButton";
import { useAuth } from "@/contexts/AuthContext";
import { Spinner } from "@/components/ui/spinner";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";

function HomeView() {
  const { user, isLoading } = useAuth();
  const router = useRouter();
  const searchParams = useSearchParams();
  const isPublicPreview = searchParams.get("view") === "public";

  const isAdmin = user?.role === "admin";
  const isModerator = user?.role === "moderator";
  const isStaff = isAdmin || isModerator;

  useEffect(() => {
    // Tài khoản admin/moderator thì vào thẳng trang quản trị/kiểm duyệt luôn
    if (!isLoading && !isPublicPreview) {
      if (isAdmin) {
        router.replace("/admin");
      } else if (isModerator) {
        router.replace("/moderator/centers");
      }
    }
  }, [isAdmin, isModerator, isLoading, isPublicPreview, router]);

  // Nếu là admin/moderator và không yêu cầu preview công khai, hiển thị loader ngắn gọn trước khi router.replace hoàn tất
  if (!isLoading && isStaff && !isPublicPreview) {
    return (
      <div className="flex min-h-[40vh] flex-col items-center justify-center gap-3 text-sm text-muted-foreground">
        <Spinner />
        <p>{isAdmin ? "Đang chuyển hướng thẳng đến trang Quản trị..." : "Đang chuyển hướng thẳng đến khu vực Moderator..."}</p>
      </div>
    );
  }

  return (
    <section className="mx-auto max-w-[1080px] space-y-6">
      {isStaff && isPublicPreview && (
        <Alert className="border-amber-500/50 bg-amber-500/10 text-amber-900 dark:text-amber-200">
          <ShieldAlertIcon className="size-4 text-amber-600" />
          <AlertTitle className="font-semibold">Chế độ xem trước công khai ({isAdmin ? "Admin" : "Moderator"})</AlertTitle>
          <AlertDescription className="flex items-center justify-between gap-2 mt-1">
            <span>Bạn đang xem giao diện trang chủ của học sinh / phụ huynh với tư cách {isAdmin ? "Quản trị viên" : "Moderator"}.</span>
            <Link
              href={isAdmin ? "/admin" : "/moderator/centers"}
              className={buttonVariants({ variant: "outline", size: "sm", className: "h-7 text-xs gap-1 shrink-0 bg-background" })}
            >
              {isAdmin ? "Vào trang Quản trị" : "Vào khu vực Moderator"}
              <ExternalLinkIcon className="size-3" />
            </Link>
          </AlertDescription>
        </Alert>
      )}

      <div className="space-y-2.5 pt-5">
        <div className="text-[11.5px] font-bold tracking-[0.07em] text-brand uppercase">
          Tư vấn du học Mỹ · bậc Đại học
        </div>
        <h1 className="text-[clamp(30px,4vw,42px)] leading-[1.15] font-semibold text-foreground">
          Biết hồ sơ của bạn đang ở đâu so với từng trường
        </h1>
        <p className="max-w-[62ch] text-base text-muted-foreground">
          Điền hồ sơ một lần, nhận danh sách trường chia theo Khó vào, Vừa sức, An toàn. Hoặc hỏi trợ lý bất cứ điều
          gì về du học Mỹ.
        </p>
      </div>

      <div className="grid grid-cols-[repeat(auto-fit,minmax(min(300px,100%),1fr))] gap-5">
        <article className="flex flex-col gap-4 rounded-xl border border-line bg-surface p-6">
          <ClipboardListIcon className="size-6 text-foreground" />
          <div className="space-y-1.5">
            <h2 className="text-2xl font-semibold">Phân tích tư vấn</h2>
            <p className="text-muted-foreground">
              Form 6 bước, khoảng 4 phút. Cuối cùng là trang đề xuất trường chia theo 4 nhóm, kèm lý do cho từng
              trường.
            </p>
          </div>
          <Link href="/analysis" className={buttonVariants({ size: "lg", className: "h-11 self-start px-5" })}>
            Bắt đầu phân tích
            <ArrowRightIcon className="ml-1.5 size-4" />
          </Link>
        </article>

        <article className="flex flex-col gap-4 rounded-xl border border-line bg-surface p-6">
          <MessageCircleIcon className="size-6 text-foreground" />
          <div className="space-y-1.5">
            <h2 className="text-2xl font-semibold">Trợ lý USAS</h2>
            <p className="text-muted-foreground">
              Hỏi về visa, I-20, chi phí, chứng chỉ hay hồ sơ của bạn. Ở trang nào cũng có nút “Hỏi trợ lý” góc dưới
              bên phải: trợ lý mở bên cạnh và biết bạn đang xem gì.
            </p>
          </div>
          <OpenAssistantButton className="h-11 self-start px-5" />
        </article>
      </div>

      <p className="text-sm text-muted-foreground">
        Muốn biết hệ thống xếp trường thế nào?{" "}
        <Link href="/methodology" className="font-medium text-brand underline-offset-4 hover:underline">
          Xem giới thiệu phương pháp
        </Link>
      </p>
    </section>
  );
}

export default function HomePage() {
  return (
    <Suspense
      fallback={
        <div className="flex min-h-[40vh] items-center justify-center gap-2 text-sm text-muted-foreground">
          <Spinner />
          <span>Đang tải...</span>
        </div>
      }
    >
      <HomeView />
    </Suspense>
  );
}
