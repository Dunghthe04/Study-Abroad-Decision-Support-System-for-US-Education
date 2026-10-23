import Link from "next/link";
import { ArrowRightIcon, ClipboardListIcon, MessageCircleIcon } from "lucide-react";
import { buttonVariants } from "@/components/ui/button";
import { OpenAssistantButton } from "@/components/assistant/OpenAssistantButton";

export default function HomePage() {
  return (
    <section className="mx-auto max-w-[1080px] space-y-6">
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
