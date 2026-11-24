"use client";

// Ảnh minh họa kèm câu trả lời (ví dụ ảnh mẫu form I-20). Trình duyệt tải ảnh thẳng từ link do người thu thập dán vào kho tri thức:
// hệ thống không lưu hay xử lý ảnh. Link có thể hỏng hoặc bị trang nguồn chặn nhúng ảnh, nên ảnh lỗi thì chuyển sang đường link bấm được.
// Chỉ vẽ link https; câu trả lời chữ không phụ thuộc vào ảnh.

import { useState } from "react";
import { ExternalLinkIcon, ImageOffIcon } from "lucide-react";
import type { ChatMedia } from "@/types/api";

/** Chỉ nhận ảnh có link https hợp lệ (backend đã kiểm, kiểm lại ở đây vì đây là chỗ đưa link vào trang). */
export function safeImages(media: ChatMedia[] | null | undefined): ChatMedia[] {
  return (media ?? []).filter((item) => {
    if (item.kind !== "image") return false;
    try {
      return new URL(item.url).protocol === "https:";
    } catch {
      return false;
    }
  });
}

export function ChatImages({ media }: { media: ChatMedia[] | null | undefined }) {
  const [failed, setFailed] = useState<ReadonlySet<string>>(new Set());
  const images = safeImages(media);
  if (images.length === 0) return null;

  const markFailed = (url: string) => setFailed((current) => new Set(current).add(url));

  return (
    <div className="mt-2.5 space-y-2">
      {images.map((image) =>
        failed.has(image.url) ? (
          // Ảnh không tải được: link bấm được thay cho ảnh
          <a
            key={image.url}
            href={image.url}
            target="_blank"
            rel="noopener noreferrer"
            className="flex items-center gap-2 rounded-lg border border-line bg-surface px-3 py-2 text-xs text-muted-foreground underline hover:text-brand"
          >
            <ImageOffIcon className="size-4 shrink-0" aria-hidden />
            <span className="min-w-0 flex-1">Không hiện được ảnh. Xem ảnh: {image.alt}</span>
            <ExternalLinkIcon className="size-3.5 shrink-0" aria-hidden />
          </a>
        ) : (
          <figure key={image.url} className="overflow-hidden rounded-lg border border-line bg-surface">
            {/* Thẻ img thường, không dùng next/image: ảnh nằm ở nhiều trang khác nhau, không khai báo trước được tên miền */}
            {/* no-referrer: giảm bị trang nguồn chặn nhúng ảnh */}
            <img
              src={image.url}
              alt={image.alt}
              loading="lazy"
              decoding="async"
              referrerPolicy="no-referrer"
              onError={() => markFailed(image.url)}
              className="max-h-80 w-full object-contain"
            />
            <figcaption className="border-t border-line px-3 py-1.5 text-xs text-muted-foreground">{image.alt}</figcaption>
          </figure>
        ),
      )}
    </div>
  );
}
