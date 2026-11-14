"use client";

import { AlertTriangleIcon } from "lucide-react";
import { cn } from "@/lib/utils";
import {
  getCertDisplayName,
  getCertSummaryParts,
  getExpiry,
  isPlanOverdue,
  overduePlanMessage,
  validateCertification,
} from "@/lib/certification-utils";
import type { CertificationItem, CertificationTypeOption } from "@/types/profile-flow";

interface CertificationCardProps {
  cert: CertificationItem;
  type: CertificationTypeOption | undefined;
  onEdit: () => void;
  onRemove: () => void;
  /** Dự định quá hạn → "Đã thi rồi": mở form nhập điểm thật */
  onTaken?: () => void;
  /** Dự định quá hạn → "Đổi tháng": mở form dự định để chọn tháng mới */
  onChangeMonth?: () => void;
  disabled?: boolean;
}

/** Thẻ một chứng chỉ: tên, huy hiệu hạn dùng, dòng điểm, Sửa / Xóa */
export function CertificationCard({ cert, type, onEdit, onRemove, onTaken, onChangeMonth, disabled }: CertificationCardProps) {
  const expiry = getExpiry(cert.testDate, type);
  const parts = getCertSummaryParts(cert, type);
  // Dự định đã qua tháng dự kiến: hệ thống không biết đã thi chưa nên hỏi học sinh, không tự đổi (giống BE)
  const overdue = isPlanOverdue(cert);
  // Thẻ thiếu thông tin (VD AP chuyển từ dữ liệu cũ chưa có tên môn) → nhắc sửa trước khi gửi
  const incomplete = !overdue && Object.keys(validateCertification(cert, type)).length > 0;

  return (
    <div
      className={cn(
        "flex items-start justify-between gap-4 rounded-xl border bg-surface px-4 py-3.5",
        overdue ? "border-risk/50" : incomplete ? "border-warn/50" : "border-line"
      )}
    >
      <div className="min-w-0 space-y-1">
        <div className="flex flex-wrap items-center gap-2">
          <span className="font-semibold text-foreground">{getCertDisplayName(cert, type)}</span>
          {cert.isTarget ? (
            <span className="rounded-full bg-indigo-50 text-indigo-700 dark:bg-indigo-950/60 dark:text-indigo-300 px-2 py-0.5 text-xs font-medium border border-indigo-200 dark:border-indigo-800">
              🎯 Dự định thi
            </span>
          ) : (
            expiry && (
              <span
                className={cn(
                  "rounded-full px-2 py-0.5 text-xs font-medium",
                  expiry.expired ? "bg-risk-wash text-risk" : "bg-ok-wash text-ok"
                )}
              >
                {expiry.label}
              </span>
            )
          )}
          {overdue && (
            <span className="rounded-full bg-risk-wash px-2 py-0.5 text-xs font-medium text-risk">
              Đã qua tháng dự kiến
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
        {overdue && (
          <div className="space-y-2 pt-1">
            <p className="text-xs text-risk">{overduePlanMessage(cert, type)}</p>
            <div className="flex flex-wrap gap-2">
              {[
                { label: "Đã thi rồi", onClick: onTaken },
                { label: "Đổi tháng", onClick: onChangeMonth },
                { label: "Xóa dự định", onClick: onRemove },
              ].map((b) => (
                <button
                  key={b.label}
                  type="button"
                  onClick={b.onClick}
                  disabled={disabled}
                  className="cursor-pointer rounded-lg border border-line px-3 py-1 text-xs font-medium text-foreground hover:border-brand/50 hover:bg-brand-wash/40 disabled:cursor-not-allowed disabled:opacity-50"
                >
                  {b.label}
                </button>
              ))}
            </div>
          </div>
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
