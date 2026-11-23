"use client";

import React, { useState } from "react";
import { FileTextIcon, UploadCloudIcon, XIcon } from "lucide-react";
import { centerPortalApi, ApiError } from "@/lib/api";
import type { StudyCenter, VerificationDocumentUploadDto } from "@/types/api";
import {
  AlertDialog,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog";
import { Button } from "@/components/ui/button";
import { toast } from "@/components/ui/toast";

const DOC_TYPES = [
  { type: "business_registration", label: "Giấy chứng nhận đăng ký doanh nghiệp (ĐKKD)", required: true },
  { type: "power_of_attorney", label: "Giấy ủy quyền người thực hiện thủ tục", required: false },
  { type: "partnership_contract", label: "Hợp đồng / bằng chứng đối tác", required: false },
  { type: "consulting_license", label: "Giấy phép tư vấn du học", required: false },
];

/**
 * [UC-92] Trung tâm đã xác minh thay tài liệu pháp lý ngay trong hồ sơ công bố.
 * Gửi lại cùng thông tin pháp lý hiện có kèm tài liệu mới: hồ sơ về Chờ duyệt và dấu xác minh tạm ẩn cho tới khi Moderator duyệt.
 */
export function CenterLegalDocumentsReplace({ center, onSubmitted }: { center: StudyCenter; onSubmitted: () => void }) {
  const [docs, setDocs] = useState<VerificationDocumentUploadDto[]>([]);
  const [uploadingType, setUploadingType] = useState<string | null>(null);
  const [confirmOpen, setConfirmOpen] = useState(false);
  const [submitting, setSubmitting] = useState(false);

  const upload = async (e: React.ChangeEvent<HTMLInputElement>, type: string) => {
    const file = e.target.files?.[0];
    if (!file) return;
    setUploadingType(type);
    try {
      const doc = await centerPortalApi.uploadDocument(file, type);
      setDocs((prev) => [...prev.filter((d) => d.documentType !== type), doc]);
    } catch (err) {
      toast.add({
        type: "error",
        title: "Không tải được tệp",
        description:
          err instanceof ApiError ? err.message : "Kiểm tra lại định dạng (.pdf, .jpg, .png) và dung lượng tối đa 10MB.",
      });
    } finally {
      setUploadingType(null);
      e.target.value = "";
    }
  };

  const submit = async () => {
    setConfirmOpen(false);
    setSubmitting(true);
    try {
      await centerPortalApi.submitVerification({
        legalName: (center.name ?? "").trim(),
        taxCode: (center.taxCode ?? "").trim(),
        address: (center.address ?? "").trim(),
        legalRepresentative: (center.legalRepresentative ?? "").trim(),
        website: center.website?.trim() || undefined,
        documents: docs,
      });
      toast.add({
        type: "success",
        title: "Đã gửi tài liệu mới",
        description: "Hồ sơ chuyển về Chờ duyệt, Moderator sẽ đối chiếu lại.",
      });
      setDocs([]);
      onSubmitted();
    } catch (err) {
      toast.add({
        type: "error",
        title: "Gửi tài liệu không thành công",
        description: err instanceof ApiError ? err.message : "Vui lòng thử lại.",
      });
    } finally {
      setSubmitting(false);
    }
  };

  const hasRequired = docs.some((d) => d.documentType === "business_registration");

  return (
    <div className="space-y-3 border-t border-line pt-4">
      <div>
        <h4 className="text-sm font-semibold text-ink">Thay tài liệu pháp lý</h4>
        <p className="text-xs text-ink-3">
          Tải tài liệu mới (.pdf, .jpg, .png, tối đa 10MB mỗi tệp). Gửi đi thì hồ sơ về Chờ duyệt và dấu xác minh tạm ẩn cho tới khi Moderator duyệt lại.
        </p>
      </div>

      {DOC_TYPES.map(({ type, label, required }) => {
        const doc = docs.find((d) => d.documentType === type);
        return (
          <div key={type} className="flex flex-col gap-2 rounded-lg border border-line bg-surface p-3 sm:flex-row sm:items-center sm:justify-between">
            <span className="flex items-center gap-2 text-sm text-ink">
              <FileTextIcon className="h-4 w-4 text-ink-2" />
              {label}
              {required && <span className="text-xs text-brand">(bắt buộc)</span>}
            </span>
            {doc ? (
              <span className="flex items-center gap-2 text-xs font-medium text-emerald-600">
                <span className="max-w-[180px] truncate">✓ {doc.fileName}</span>
                <button
                  type="button"
                  aria-label="Bỏ tệp"
                  onClick={() => setDocs((prev) => prev.filter((d) => d.documentType !== type))}
                  className="rounded p-1 hover:bg-muted"
                >
                  <XIcon className="h-3.5 w-3.5" />
                </button>
              </span>
            ) : (
              <label
                className={`inline-flex cursor-pointer items-center gap-1.5 rounded-lg border border-line bg-surface px-3 py-1.5 text-xs font-medium text-ink transition-colors hover:bg-surface-3 ${
                  uploadingType === type ? "pointer-events-none opacity-50" : ""
                }`}
              >
                <UploadCloudIcon className="h-4 w-4 text-ink-2" />
                {uploadingType === type ? "Đang tải..." : "Tải tệp lên"}
                <input type="file" accept=".pdf,.jpg,.jpeg,.png" className="hidden" onChange={(e) => void upload(e, type)} />
              </label>
            )}
          </div>
        );
      })}

      <Button
        type="button"
        variant="outline"
        size="sm"
        loading={submitting}
        disabled={docs.length === 0 || !hasRequired}
        onClick={() => setConfirmOpen(true)}
      >
        Gửi tài liệu mới
      </Button>
      {docs.length > 0 && !hasRequired && (
        <p className="text-xs text-ink-3">Cần tải lên Giấy chứng nhận ĐKDN để gửi.</p>
      )}

      <AlertDialog open={confirmOpen} onOpenChange={setConfirmOpen}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Gửi tài liệu pháp lý mới?</AlertDialogTitle>
            <AlertDialogDescription>
              Hồ sơ sẽ chuyển về Chờ duyệt và dấu xác minh tạm ẩn cho tới khi Moderator duyệt lại.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Hủy</AlertDialogCancel>
            <Button type="button" onClick={() => void submit()}>
              Gửi tài liệu
            </Button>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}
