"use client";

import { useEffect, useRef, useState } from "react";
import { ImagePlusIcon, XIcon } from "lucide-react";
import { Button } from "@/components/ui/button";
import { IMAGE_UPLOAD_LIMITS, uploadImage } from "@/lib/media-api";

/** Khớp StudyCenterService.MaxGalleryImages ở backend. */
export const MAX_GALLERY_IMAGES = 10;

interface CenterGalleryFieldProps {
  value: string[];
  onChange: (urls: string[]) => void;
  disabled?: boolean;
  onUploadingChange?: (uploading: boolean) => void;
}

/** Thư viện ảnh của trung tâm: tải nhiều ảnh lên, xóa từng ảnh. Ảnh chỉ được lưu khi bấm lưu hồ sơ. */
export function CenterGalleryField({ value, onChange, disabled, onUploadingChange }: CenterGalleryFieldProps) {
  const [isUploading, setIsUploading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const inputRef = useRef<HTMLInputElement>(null);
  const requestRef = useRef<AbortController | null>(null);
  useEffect(() => () => requestRef.current?.abort(), []);

  const remaining = MAX_GALLERY_IMAGES - value.length;
  const busy = disabled || isUploading;

  const handleSelect = async (event: React.ChangeEvent<HTMLInputElement>) => {
    const files = Array.from(event.target.files ?? []).slice(0, Math.max(remaining, 0));
    event.target.value = "";
    if (files.length === 0 || busy) return;
    const controller = new AbortController();
    requestRef.current = controller;
    setError(null);
    setIsUploading(true);
    onUploadingChange?.(true);
    const added: string[] = [];
    try {
      for (const file of files) {
        const uploaded = await uploadImage(file, "center-gallery", controller.signal);
        added.push(uploaded.secureUrl);
      }
    } catch (err) {
      if (!controller.signal.aborted) setError(err instanceof Error ? err.message : "Không thể tải ảnh lên.");
    } finally {
      if (!controller.signal.aborted) {
        // Ảnh đã tải xong trước khi gặp lỗi vẫn được giữ lại
        if (added.length > 0) onChange([...value, ...added]);
        setIsUploading(false);
        onUploadingChange?.(false);
      }
    }
  };

  return (
    <div className="space-y-2">
      {value.length > 0 && (
        <ul className="grid grid-cols-2 gap-2 sm:grid-cols-4 lg:grid-cols-5">
          {value.map((url) => (
            <li key={url} className="relative aspect-video overflow-hidden rounded-md border border-line bg-surface-2">
              {/* eslint-disable-next-line @next/next/no-img-element */}
              <img src={url} alt="Ảnh trung tâm" className="h-full w-full object-cover" />
              <button
                type="button"
                aria-label="Xóa ảnh khỏi thư viện"
                disabled={busy}
                onClick={() => onChange(value.filter((u) => u !== url))}
                className="absolute right-1 top-1 rounded-full bg-black/60 p-1 text-white hover:bg-black/80 disabled:opacity-50"
              >
                <XIcon className="h-3.5 w-3.5" />
              </button>
            </li>
          ))}
        </ul>
      )}
      <div className="flex flex-wrap items-center gap-2">
        <input
          ref={inputRef}
          type="file"
          multiple
          accept="image/jpeg,image/png,image/webp"
          className="sr-only"
          tabIndex={-1}
          aria-label="Chọn ảnh thư viện"
          disabled={busy || remaining <= 0}
          onChange={handleSelect}
        />
        <Button type="button" variant="outline" size="sm" disabled={disabled || remaining <= 0} loading={isUploading} onClick={() => inputRef.current?.click()}>
          <ImagePlusIcon />
          Thêm ảnh
        </Button>
        <span className="text-xs text-muted-foreground">
          {value.length}/{MAX_GALLERY_IMAGES} ảnh · JPEG, PNG, WebP · tối đa {IMAGE_UPLOAD_LIMITS["center-gallery"] / 1024 / 1024} MB mỗi ảnh
        </span>
      </div>
      {error && <p role="alert" className="text-xs text-destructive">{error}</p>}
    </div>
  );
}
