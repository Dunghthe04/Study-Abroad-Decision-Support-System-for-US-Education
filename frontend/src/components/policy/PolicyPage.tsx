import Link from "next/link";
import { ArrowLeftIcon, TriangleAlertIcon } from "lucide-react";
import { PolicyContent } from "@/components/policy/PolicyContent";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { buttonVariants } from "@/components/ui/button";
import { formatPolicyDate, getCurrentPolicy } from "@/lib/policy-api";
import type { PolicyKind, PublicPolicy } from "@/types/policy";

interface PolicyPageProps {
  kind: PolicyKind;
  /** Tiêu đề hiện khi chưa tải được văn bản. */
  fallbackTitle: string;
}

/**
 * Trang công khai của một văn bản chính sách (UC-103): đọc bản ĐANG HIỆU LỰC từ backend, không còn chữ viết cứng.
 * Admin công bố bản mới thì trang này hiện ngay bản mới. Chưa có bản nào hoặc không tải được thì báo rõ, không dựng văn bản thay thế.
 */
export async function PolicyPage({ kind, fallbackTitle }: PolicyPageProps) {
  let policy: PublicPolicy | null = null;
  let failed = false;
  try {
    policy = await getCurrentPolicy(kind);
  } catch {
    failed = true;
  }

  return (
    <main className="min-h-screen bg-surface px-4 py-12 sm:px-6 lg:px-8">
      <div className="mx-auto max-w-3xl space-y-8">
        <Link href="/register" className={buttonVariants({ variant: "ghost", size: "sm" })}>
          <ArrowLeftIcon className="mr-1.5 size-4" /> Quay lại đăng ký
        </Link>

        <div className="space-y-3">
          <h1 className="font-heading text-3xl font-bold tracking-tight text-ink">{policy?.title ?? fallbackTitle}</h1>
          {policy ? (
            <p className="text-sm text-ink-3">
              Phiên bản {policy.version} · Có hiệu lực từ {formatPolicyDate(policy.publishedAt)}
            </p>
          ) : null}
        </div>

        {policy ? (
          <PolicyContent content={policy.content} />
        ) : (
          <Alert variant={failed ? "destructive" : "warning"}>
            <TriangleAlertIcon />
            <AlertDescription>
              {failed
                ? "Chưa tải được nội dung. Bạn tải lại trang sau ít phút nhé."
                : "Văn bản này chưa được công bố. Bạn vui lòng quay lại sau."}
            </AlertDescription>
          </Alert>
        )}
      </div>
    </main>
  );
}
