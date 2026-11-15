export type ImageUploadPurpose = "school-logo" | "school-cover";

export interface UploadedImage {
  secureUrl: string;
  publicId: string;
  contentType: string;
  sizeBytes: number;
  width: number;
  height: number;
}

export const IMAGE_UPLOAD_LIMITS: Record<ImageUploadPurpose, number> = {
  "school-logo": 2 * 1024 * 1024,
  "school-cover": 10 * 1024 * 1024,
};

export async function uploadImage(file: File, purpose: ImageUploadPurpose, signal?: AbortSignal): Promise<UploadedImage> {
  const limit = IMAGE_UPLOAD_LIMITS[purpose];
  if (file.size === 0) throw new Error("Ảnh không được rỗng.");
  if (file.size > limit) throw new Error(`Ảnh tối đa ${limit / 1024 / 1024} MB.`);
  if (!/\.(jpe?g|png|webp)$/i.test(file.name)) throw new Error("Chỉ hỗ trợ ảnh JPEG, PNG hoặc WebP.");

  const body = new FormData();
  body.append("file", file);
  body.append("purpose", purpose);
  const response = await fetch("/api/v1/media/images", {
    method: "POST",
    credentials: "include",
    body,
    signal,
  });
  if (!response.ok) {
    const error = await response.json().catch(() => null);
    const fallback: Record<number, string> = {
      401: "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.",
      403: "Bạn không có quyền tải ảnh lên.",
      413: "Ảnh vượt quá giới hạn dung lượng cho phép.",
      429: "Bạn tải ảnh quá nhiều lần. Vui lòng thử lại sau.",
      502: "Không thể tải ảnh lên Cloudinary. Vui lòng thử lại.",
      503: "Backend chưa cấu hình Cloudinary. Vui lòng liên hệ quản trị viên.",
    };
    throw new Error(error?.detail ?? error?.error ?? error?.message ?? fallback[response.status] ?? "Không thể tải ảnh lên.");
  }
  return await response.json() as UploadedImage;
}
