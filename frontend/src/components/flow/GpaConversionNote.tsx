import { InfoIcon } from "lucide-react";
import { cn } from "@/lib/utils";

/**
 * Lời nhắc dưới chỗ hiện GPA thang 4 quy đổi từ thang 10: số này chỉ là mốc tham khảo (EducationUSA),
 * trường Mỹ tự đánh giá học bạ theo cách riêng.
 */
export function GpaConversionNote({ className }: { className?: string }) {
  return (
    <p className={cn("flex items-start gap-1.5 text-xs text-muted-foreground", className)}>
      <InfoIcon className="mt-0.5 size-3.5 shrink-0" aria-hidden />
      <span>
        Điểm thang 4 ở đây là quy đổi tham khảo theo mốc của EducationUSA, giúp bạn hình dung mặt bằng của mình. Mỗi
        trường Mỹ tự đánh giá học bạ theo cách riêng, nên con số thực tế có thể chênh một chút.
      </span>
    </p>
  );
}
