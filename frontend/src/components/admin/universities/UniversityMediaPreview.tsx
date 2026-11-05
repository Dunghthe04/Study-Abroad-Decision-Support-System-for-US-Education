"use client";

import { useState } from "react";
import { ImageIcon } from "lucide-react";

interface PreviewImageProps {
  url?: string | null;
  alt: string;
  className: string;
  emptyText: string;
}

function PreviewImage({ url, alt, className, emptyText }: PreviewImageProps) {
  const [failedUrl, setFailedUrl] = useState<string | null>(null);
  if (!url || failedUrl === url) {
    return (
      <div className={`${className} flex items-center justify-center gap-1 text-xs text-muted-foreground`}>
        <ImageIcon className="size-4 shrink-0" />
        <span>{failedUrl === url && url ? "Không tải được ảnh" : emptyText}</span>
      </div>
    );
  }
  return <img src={url} alt={alt} className={className} onError={() => setFailedUrl(url)} />;
}

export function UniversityMediaPreview({ logoUrl, coverUrl, name }: {
  logoUrl?: string | null;
  coverUrl?: string | null;
  name: string;
}) {
  return (
    <div className="relative h-32 w-full border-b border-border bg-muted/40">
      <PreviewImage url={coverUrl} alt={`Ảnh bìa ${name}`} className="h-full w-full object-cover" emptyText="Chưa có ảnh bìa" />
      <div className="absolute bottom-2 left-3">
        <PreviewImage url={logoUrl} alt={`Logo ${name}`} className="size-14 bg-white object-contain p-1" emptyText="Logo" />
      </div>
    </div>
  );
}
