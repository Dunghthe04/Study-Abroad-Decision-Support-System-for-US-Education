import Link from "next/link";
import { ArrowRightIcon, FileTextIcon, LightbulbIcon } from "lucide-react";
import { ProtectedRoute } from "@/components/auth/ProtectedRoute";
import { ChatBox } from "@/components/advisor/ChatBox";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { buttonVariants } from "@/components/ui/button";
import { cn } from "@/lib/utils";

export const metadata = { title: "Tư vấn AI – USAS" };

export default function AdvisorPage() {
  return (
    <ProtectedRoute>
      <section className="space-y-4">
        <div className="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <h1 className="text-h1">Tư vấn du học Mỹ bằng AI</h1>
            <p className="text-sm text-muted-foreground">
              Hỗ trợ mọi bậc: THCS/THPT, cao đẳng, đại học, thạc sĩ, tiến sĩ. Chọn bậc học để câu trả lời sát hơn.
            </p>
          </div>
          <Link
            href="/profile/academic"
            className={cn(buttonVariants({ variant: "outline", size: "sm" }), "self-start sm:self-auto")}
          >
            <FileTextIcon aria-hidden="true" />
            Điền bảng điểm & chứng chỉ
            <ArrowRightIcon aria-hidden="true" />
          </Link>
        </div>

        {/* Banner gợi ý kết nối với Hồ sơ học thuật */}
        <Alert variant="warning">
          <LightbulbIcon />
          <AlertDescription>
            <span>
              <strong>Mẹo nhỏ:</strong> Để AI gợi ý nhóm trường Reach / Match / Safety và tính toán khả năng trúng tuyển chuẩn xác nhất, bạn hãy cập nhật đầy đủ điểm 3 năm gần nhất tại{" "}
              <Link href="/profile/academic" className="underline">
                Hồ sơ học thuật
              </Link>.
            </span>
          </AlertDescription>
        </Alert>

        <ChatBox />
      </section>
    </ProtectedRoute>
  );
}
