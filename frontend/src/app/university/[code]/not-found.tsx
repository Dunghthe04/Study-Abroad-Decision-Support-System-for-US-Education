import Link from "next/link";
import { Building2Icon } from "lucide-react";
import { buttonVariants } from "@/components/ui/button";

export default function UniversityNotFound() {
  return (
    <section className="mx-auto max-w-lg space-y-4 py-20 text-center">
      <Building2Icon className="mx-auto size-12 text-muted-foreground" />
      <h1 className="text-2xl font-semibold">Không tìm thấy trường</h1>
      <p className="text-sm leading-relaxed text-muted-foreground">Mã trường không tồn tại hoặc trường hiện không được công khai. Vui lòng kiểm tra lại đường dẫn.</p>
      <Link href="/" className={buttonVariants({ variant: "outline" })}>Về trang chủ</Link>
    </section>
  );
}
