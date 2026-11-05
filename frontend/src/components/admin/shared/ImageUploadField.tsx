"use client";

import { useEffect, useRef, useState } from "react";
import { UploadIcon } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Spinner } from "@/components/ui/spinner";
import { IMAGE_UPLOAD_LIMITS, uploadImage, type ImageUploadPurpose } from "@/lib/media-api";

interface ImageUploadFieldProps {
  id: string;
  value: string;
  onChange: (url: string) => void;
  purpose: ImageUploadPurpose;
  disabled?: boolean;
  invalid?: boolean;
  autoFocus?: boolean;
  onUploadingChange?: (uploading: boolean) => void;
}

export function ImageUploadField({ id, value, onChange, purpose, disabled, invalid, autoFocus, onUploadingChange }: ImageUploadFieldProps) {
  const [isUploading, setIsUploading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const inputRef = useRef<HTMLInputElement>(null);
  const requestRef = useRef<AbortController | null>(null);
  useEffect(() => () => requestRef.current?.abort(), []);

  const handleSelect = async (event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    event.target.value = "";
    if (!file || isUploading || disabled) return;
    const controller = new AbortController();
    requestRef.current = controller;
    setError(null);
    setIsUploading(true);
    onUploadingChange?.(true);
    try {
      const uploaded = await uploadImage(file, purpose, controller.signal);
      if (!controller.signal.aborted) onChange(uploaded.secureUrl);
    } catch (err) {
      if (!controller.signal.aborted) setError(err instanceof Error ? err.message : "Không thể tải ảnh lên.");
    } finally {
      if (!controller.signal.aborted) {
        setIsUploading(false);
        onUploadingChange?.(false);
      }
    }
  };

  const busy = disabled || isUploading;
  return (
    <div className="space-y-2">
      <Input id={id} value={value} onChange={(event) => { setError(null); onChange(event.target.value); }}
        aria-invalid={invalid || Boolean(error) || undefined} placeholder="https://res.cloudinary.com/..." disabled={busy} autoFocus={autoFocus} />
      <div className="flex flex-wrap items-center gap-2">
        <input ref={inputRef} type="file" accept="image/jpeg,image/png,image/webp" className="sr-only" tabIndex={-1}
          aria-label={`Chọn ảnh ${purpose === "school-logo" ? "logo" : "bìa"}`} disabled={busy} onChange={handleSelect} />
        <Button type="button" variant="outline" size="sm" disabled={busy} onClick={() => inputRef.current?.click()}>
          {isUploading ? <Spinner /> : <UploadIcon />}
          {isUploading ? "Đang tải..." : "Tải ảnh lên"}
        </Button>
        <span className="text-xs text-muted-foreground">JPEG, PNG, WebP · tối đa {IMAGE_UPLOAD_LIMITS[purpose] / 1024 / 1024} MB</span>
      </div>
      {error && <p role="alert" className="text-xs text-destructive">{error}</p>}
    </div>
  );
}
