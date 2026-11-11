import { ErrorFallback } from "@/components/ui/error-fallback";

export default function UniversityNotFound() {
  return (
    <ErrorFallback
      title="Không tìm thấy trường"
      description="Mã trường không tồn tại hoặc trường hiện không được công khai. Vui lòng kiểm tra lại đường dẫn."
      action={{
        label: "Về trang chủ",
        href: "/",
      }}
      secondaryAction={{
        label: "Tìm trường khác",
        href: "/universities",
      }}
    />
  );
}
