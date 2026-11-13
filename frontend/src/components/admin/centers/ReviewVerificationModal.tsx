"use client";

import React, { useEffect, useState } from "react";
import {
  ShieldCheckIcon,
  CheckCircle2Icon,
  XCircleIcon,
  FileTextIcon,
  ExternalLinkIcon,
  CheckSquareIcon,
  XIcon
} from "lucide-react";
import { adminCenterVerificationsApi, ApiError } from "@/lib/api";
import type { CenterVerificationDto } from "@/types/api";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Textarea } from "@/components/ui/textarea";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Spinner } from "@/components/ui/spinner";

interface Props {
  requestId: string | null;
  isOpen: boolean;
  onClose: () => void;
  onSuccess: () => void;
}

export function ReviewVerificationModal({
  requestId,
  isOpen,
  onClose,
  onSuccess,
}: Props) {
  const [data, setData] = useState<CenterVerificationDto | null>(null);
  const [loading, setLoading] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // 3 Moderator Checklist items (BR-C3)
  const [isNationalRegistryMatched, setIsNationalRegistryMatched] = useState(false);
  const [isBusinessOperating, setIsBusinessOperating] = useState(false);
  const [isRepresentativeValid, setIsRepresentativeValid] = useState(false);

  const [reviewNotes, setReviewNotes] = useState("");
  const [rejectionReason, setRejectionReason] = useState("");

  useEffect(() => {
    let active = true;
    queueMicrotask(() => {
      if (!active) return;
      if (!isOpen || !requestId) {
        setData(null);
        setError(null);
        return;
      }

      setLoading(true);
      void (async () => {
        try {
          const item = await adminCenterVerificationsApi.getById(requestId);
          if (active) {
            setData(item);
            setIsNationalRegistryMatched(item.isNationalRegistryMatched || false);
            setIsBusinessOperating(item.isBusinessOperating || false);
            setIsRepresentativeValid(item.isRepresentativeValid || false);
            setReviewNotes(item.reviewNotes || "");
            setRejectionReason(item.rejectionReason || "");
          }
        } catch (err) {
          if (active) {
            setError(err instanceof ApiError ? err.message : "Không thể tải chi tiết hồ sơ xác minh.");
          }
        } finally {
          if (active) setLoading(false);
        }
      })();
    });

    return () => {
      active = false;
    };
  }, [isOpen, requestId]);

  if (!isOpen) return null;

  const handleAction = async (action: "approve" | "reject" | "changes_requested" | "revoke") => {
    if (!requestId) return;
    setError(null);

    if (action === "approve") {
      if (!isNationalRegistryMatched || !isBusinessOperating || !isRepresentativeValid) {
        setError("Để cấp dấu xác minh (BR-C3), bạn phải tích xác nhận cả 3 tiêu chí đối chiếu trên Cổng ĐKDN.");
        return;
      }
    } else {
      if (!rejectionReason.trim()) {
        setError("Vui lòng nhập lý do từ chối / nội dung yêu cầu bổ sung.");
        return;
      }
    }

    setSubmitting(true);
    try {
      await adminCenterVerificationsApi.review(requestId, {
        action,
        isNationalRegistryMatched,
        isBusinessOperating,
        isRepresentativeValid,
        reviewNotes: reviewNotes.trim() || null,
        rejectionReason: rejectionReason.trim() || null,
      });

      onSuccess();
      onClose();
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.message);
      } else {
        setError("Thao tác kiểm duyệt thất bại. Vui lòng thử lại.");
      }
    } finally {
      setSubmitting(false);
    }
  };

  const isEmailVerified = !!data?.emailVerifiedAt;
  const allConditionsMet =
    isEmailVerified &&
    isNationalRegistryMatched &&
    isBusinessOperating &&
    isRepresentativeValid;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/50 backdrop-blur-sm animate-in fade-in duration-200">
      <div className="relative w-full max-w-3xl max-h-[90vh] flex flex-col rounded-xl border border-line bg-surface shadow-2xl overflow-hidden">
        {/* Header */}
        <div className="flex items-center justify-between border-b border-line px-6 py-4 bg-surface-2">
          <div className="flex items-center gap-2.5">
            <ShieldCheckIcon className="h-5 w-5 text-brand" />
            <h2 className="text-lg font-bold text-ink">
              Kiểm duyệt hồ sơ xác minh trung tâm (UC-59)
            </h2>
          </div>
          <button
            type="button"
            onClick={onClose}
            className="p-1 rounded-lg text-ink-3 hover:bg-surface-3 hover:text-ink transition-colors"
          >
            <XIcon className="h-5 w-5" />
          </button>
        </div>

        {/* Content Body */}
        <div className="flex-1 overflow-y-auto p-6 space-y-6 text-sm">
          {loading ? (
            <div className="flex h-48 items-center justify-center gap-2 text-ink-2">
              <Spinner />
              <span>Đang tải thông tin hồ sơ...</span>
            </div>
          ) : error && !data ? (
            <Alert variant="destructive">
              <AlertDescription>{error}</AlertDescription>
            </Alert>
          ) : data ? (
            <>
              {error && (
                <Alert variant="destructive">
                  <AlertDescription>{error}</AlertDescription>
                </Alert>
              )}

              {/* Center Overview */}
              <div className="rounded-lg border border-line bg-surface-2/60 p-4 space-y-3">
                <div className="flex items-start justify-between">
                  <div>
                    <h3 className="font-bold text-base text-ink">{data.centerName || data.legalName}</h3>
                    <p className="text-xs text-ink-3 font-mono">Mã: {data.centerCode || "CTR_---"}</p>
                  </div>
                  <Badge variant="outline" className="capitalize">
                    Trạng thái: {data.status}
                  </Badge>
                </div>

                <div className="grid grid-cols-1 sm:grid-cols-2 gap-3 text-xs">
                  <div>
                    <span className="text-ink-3 block">Email tài khoản:</span>
                    <span className="font-medium text-ink flex items-center gap-1.5 mt-0.5">
                      {data.centerEmail || "—"}
                      {isEmailVerified ? (
                        <CheckCircle2Icon className="h-3.5 w-3.5 text-emerald-600" />
                      ) : (
                        <XCircleIcon className="h-3.5 w-3.5 text-rose-500" />
                      )}
                    </span>
                  </div>
                  <div>
                    <span className="text-ink-3 block">Số điện thoại liên hệ:</span>
                    <span className="font-medium text-ink flex items-center gap-1.5 mt-0.5">
                      {data.centerPhone || "—"}
                    </span>
                  </div>
                </div>
              </div>

              {/* Submitted Legal Details */}
              <div className="space-y-3">
                <div className="flex items-center justify-between border-b border-line pb-2">
                  <h4 className="font-semibold text-ink">Thông tin khai báo theo ĐKKD</h4>
                  <a
                    href="https://dangkykinhdoanh.gov.vn"
                    target="_blank"
                    rel="noreferrer"
                    className="inline-flex items-center gap-1 text-xs font-semibold text-brand hover:underline"
                  >
                    Tra cứu Cổng ĐKDN Quốc gia <ExternalLinkIcon className="h-3 w-3" />
                  </a>
                </div>

                <div className="grid grid-cols-1 sm:grid-cols-2 gap-4 bg-surface-2/40 p-4 rounded-lg border border-line">
                  <div>
                    <span className="text-xs text-ink-3 block">Tên pháp lý:</span>
                    <span className="font-medium text-ink">{data.legalName}</span>
                  </div>
                  <div>
                    <span className="text-xs text-ink-3 block">Mã số thuế / MST:</span>
                    <span className="font-mono font-bold text-brand">{data.taxCode}</span>
                  </div>
                  <div>
                    <span className="text-xs text-ink-3 block">Người đại diện theo pháp luật:</span>
                    <span className="font-medium text-ink">{data.legalRepresentative}</span>
                  </div>
                  <div>
                    <span className="text-xs text-ink-3 block">Địa chỉ trụ sở chính:</span>
                    <span className="font-medium text-ink">{data.address}</span>
                  </div>
                </div>
              </div>

              {/* Uploaded Documents List */}
              <div className="space-y-3">
                <h4 className="font-semibold text-ink">Tài liệu minh chứng đính kèm ({data.documents.length})</h4>
                <div className="divide-y divide-line rounded-lg border border-line bg-surface-2/40">
                  {data.documents.map((doc) => (
                    <div key={doc.id} className="p-3 flex items-center justify-between text-xs">
                      <div className="flex items-center gap-2.5">
                        <FileTextIcon className="h-4 w-4 text-brand shrink-0" />
                        <div>
                          <p className="font-medium text-ink truncate max-w-[280px] sm:max-w-md">
                            {doc.fileName}
                          </p>
                          <span className="text-ink-3 text-[11px]">
                            Loại: {doc.documentType} • {(doc.fileSize / 1024).toFixed(0)} KB
                          </span>
                        </div>
                      </div>

                      <a
                        href={`/api/v1/admin/center-verifications/${data.id}/documents/${doc.id}/file`}
                        target="_blank"
                        rel="noreferrer"
                        className="inline-flex items-center gap-1 px-2.5 py-1 rounded bg-brand/10 text-brand font-medium hover:bg-brand/20 transition-colors"
                      >
                        Mở xem <ExternalLinkIcon className="h-3 w-3" />
                      </a>
                    </div>
                  ))}
                </div>
                <p className="text-[11px] text-ink-3">
                  * Theo quy tắc BR-C5: Mỗi lần mở xem tài liệu đều được hệ thống ghi nhật ký kiểm toán bất biến.
                </p>
              </div>

              {/* 5-Item Checklist (BR-C3) */}
              <div className="space-y-3 rounded-lg border border-line bg-surface p-4">
                <h4 className="font-semibold text-ink flex items-center gap-2">
                  <CheckSquareIcon className="h-4 w-4 text-brand" />
                  Đối chiếu 5 điều kiện bắt buộc cấp tick xanh (BR-C3)
                </h4>

                <div className="space-y-2.5 text-xs">
                  {/* System Condition 1 */}
                  <div className="flex items-center justify-between p-2 rounded bg-surface-2">
                    <span className="text-ink">1. Email liên hệ của trung tâm đã xác thực OTP</span>
                    {isEmailVerified ? (
                      <Badge variant="outline" className="border-emerald-500/30 text-emerald-600 bg-emerald-500/5 text-[10px]">
                        ✓ Đạt (Hệ thống)
                      </Badge>
                    ) : (
                      <Badge variant="outline" className="border-rose-500/30 text-rose-600 bg-rose-500/5 text-[10px]">
                        ✗ Chưa đạt
                      </Badge>
                    )}
                  </div>

                  {/* Moderator Condition 2 */}
                  <label className="flex items-center gap-2.5 p-2 rounded bg-surface-2 cursor-pointer hover:bg-surface-3 transition-colors">
                    <input
                      type="checkbox"
                      checked={isNationalRegistryMatched}
                      onChange={(e) => setIsNationalRegistryMatched(e.target.checked)}
                      className="h-4 w-4 rounded border-line text-brand focus:ring-brand"
                    />
                    <span className="text-ink font-medium">
                      2. Tên pháp lý và MST hoàn toàn trùng khớp với Cổng ĐKDN Quốc gia (dangkykinhdoanh.gov.vn)
                    </span>
                  </label>

                  {/* Moderator Condition 3 */}
                  <label className="flex items-center gap-2.5 p-2 rounded bg-surface-2 cursor-pointer hover:bg-surface-3 transition-colors">
                    <input
                      type="checkbox"
                      checked={isBusinessOperating}
                      onChange={(e) => setIsBusinessOperating(e.target.checked)}
                      className="h-4 w-4 rounded border-line text-brand focus:ring-brand"
                    />
                    <span className="text-ink font-medium">
                      3. Doanh nghiệp đang trong trạng thái &quot;Đang hoạt động&quot; trên Cổng ĐKDN
                    </span>
                  </label>

                  {/* Moderator Condition 4 */}
                  <label className="flex items-center gap-2.5 p-2 rounded bg-surface-2 cursor-pointer hover:bg-surface-3 transition-colors">
                    <input
                      type="checkbox"
                      checked={isRepresentativeValid}
                      onChange={(e) => setIsRepresentativeValid(e.target.checked)}
                      className="h-4 w-4 rounded border-line text-brand focus:ring-brand"
                    />
                    <span className="text-ink font-medium">
                      4. Người đăng ký là người đại diện pháp luật hoặc có Giấy ủy quyền hợp lệ
                    </span>
                  </label>
                </div>
              </div>

              {/* Review notes and rejection reason */}
              <div className="space-y-4">
                <div>
                  <label className="text-xs font-semibold text-ink block mb-1">
                    Ghi chú kiểm duyệt nội bộ
                  </label>
                  <Textarea
                    rows={2}
                    placeholder="Ghi chú đối chiếu của Moderator (lưu trữ audit)..."
                    value={reviewNotes}
                    onChange={(e) => setReviewNotes(e.target.value)}
                  />
                </div>

                <div>
                  <label className="text-xs font-semibold text-ink block mb-1">
                    Lý do từ chối / Nội dung yêu cầu bổ sung (Bắt buộc nếu Từ chối hoặc Yêu cầu bổ sung)
                  </label>
                  <Textarea
                    rows={2}
                    placeholder="Nêu rõ lý do để gửi thông báo cho trung tâm cập nhật..."
                    value={rejectionReason}
                    onChange={(e) => setRejectionReason(e.target.value)}
                  />
                </div>
              </div>
            </>
          ) : null}
        </div>

        {/* Footer Actions */}
        <div className="flex items-center justify-between border-t border-line px-6 py-4 bg-surface-2">
          <Button variant="ghost" size="sm" onClick={onClose} disabled={submitting}>
            Đóng
          </Button>

          <div className="flex items-center gap-2">
            <Button
              type="button"
              variant="outline"
              size="sm"
              disabled={submitting}
              onClick={() => handleAction("changes_requested")}
              className="text-amber-600 border-amber-500/30 hover:bg-amber-500/10"
            >
              Yêu cầu bổ sung
            </Button>

            <Button
              type="button"
              variant="outline"
              size="sm"
              disabled={submitting}
              onClick={() => handleAction("reject")}
              className="text-rose-600 border-rose-500/30 hover:bg-rose-500/10"
            >
              Từ chối hồ sơ
            </Button>

            <Button
              type="button"
              size="sm"
              disabled={submitting || !allConditionsMet}
              onClick={() => handleAction("approve")}
              className="bg-emerald-600 hover:bg-emerald-700 text-white"
            >
              {submitting && <Spinner data-icon="inline-start" />}
              Duyệt & Cấp tick xanh
            </Button>
          </div>
        </div>
      </div>
    </div>
  );
}
