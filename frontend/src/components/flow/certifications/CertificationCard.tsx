"use client";

import { AlertTriangleIcon } from "lucide-react";
import { cn } from "@/lib/utils";
import {
  getCertDisplayName,
  getCertSummaryParts,
  getExpiry,
  validateCertification,
} from "@/lib/certification-utils";
import type { CertificationItem, CertificationTypeOption } from "@/types/profile-flow";

interface CertificationCardProps {
  cert: CertificationItem;
  type: CertificationTypeOption | undefined;
  onEdit: () => void;
  onRemove: () => void;
  disabled?: boolean;
}

/** Thẻ một chứng chỉ: tên, huy hiệu hạn dùng, dòng điểm, Sửa / Xóa */
export function CertificationCard({ cert, type, onEdit, onRemove, disabled }: CertificationCardProps) {
  const expiry = getExpiry(cert.testDate, type);
  const parts = getCertSummaryParts(cert, type);
  // Thẻ thiếu thông tin (VD AP chuyển từ dữ liệu cũ chưa có tên môn) → nhắc sửa trước khi gửi
  const incomplete = Object.keys(validateCertification(cert, type)).length > 0;

  return (
    <div
      className={cn(
        "flex items-start justify-between gap-4 rounded-xl border bg-surface px-4 py-3.5",
        incomplete ? "border-warn/50" : "border-line"
      )}
    >
      <div className="min-w-0 space-y-1">
        <div className="flex flex-wrap items-center gap-2">
          <span className="font-semibold text-foreground">{getCertDisplayName(cert, type)}</span>
          {expiry && (
            <span
              className={cn(
                "rounded-full px-2 py-0.5 text-xs font-medium",
                expiry.expired ? "bg-risk-wash text-risk" : "bg-ok-wash text-ok"
              )}
            >
              {expiry.label}
            </span>
          )}
          {cert.type === "other" && (
            <span className="rounded-full bg-warn-wash px-2 py-0.5 text-xs font-medium text-warn">
              Có thể không được các trường xét
            </span>
          )}
        </div>
        {parts.length > 0 && (
          <p className="text-sm text-muted-foreground">
            {parts.map((p, i) => (
              <span key={i}>
                {i > 0 && " · "}
                {p.label && `${p.label} `}
                {p.strong ? <strong className="font-semibold text-foreground">{p.value}</strong> : p.value}
                {p.suffix && ` ${p.suffix}`}
              </span>
            ))}
          </p>
        )}
        {incomplete && (
          <p className="flex items-center gap-1 text-xs text-warn">
            <AlertTriangleIcon className="size-3.5" />
            Còn thiếu thông tin, bấm Sửa để bổ sung
          </p>
        )}
      </div>
      <div className="flex shrink-0 items-center gap-3 text-sm font-medium">
        <button
          type="button"
          onClick={onEdit}
          disabled={disabled}
          className="cursor-pointer text-brand hover:underline disabled:cursor-not-allowed disabled:opacity-50"
        >
          Sửa
        </button>
        <button
          type="button"
          onClick={onRemove}
          disabled={disabled}
          className="cursor-pointer text-brand hover:underline disabled:cursor-not-allowed disabled:opacity-50"
        >
          Xóa
        </button>
      </div>
    </div>
  );
}
