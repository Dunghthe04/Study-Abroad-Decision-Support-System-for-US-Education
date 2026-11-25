"use client";

import { useEffect, useState } from "react";
import { Building2Icon, FlagIcon, MessageSquareReplyIcon, PencilIcon, StarIcon } from "lucide-react";
import { CenterNav } from "@/components/center/CenterNav";
import { CENTER_REVIEW_REPORT_REASON_OPTIONS } from "@/components/centers/CenterReviewList";
import { Alert, AlertDescription } from "@/components/ui/alert";
import {
  AlertDialog,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Label } from "@/components/ui/label";
import { RadioGroup, RadioGroupItem } from "@/components/ui/radio-group";
import { Spinner } from "@/components/ui/spinner";
import { Textarea } from "@/components/ui/textarea";
import { toast } from "@/components/ui/toast";
import { ApiError, centerPortalApi, studyCentersApi } from "@/lib/api";
import { formatRating } from "@/lib/rating";
import type { CenterReviewDto, CenterReviewReportReason, CenterReviewsDto, StudyCenter } from "@/types/api";

const MAX_REPLY_LENGTH = 1000;
const EDIT_WINDOW_MS = 24 * 60 * 60 * 1000;

function remainingEditHours(createdAt: string): number {
  const diff = new Date(createdAt).getTime() + EDIT_WINDOW_MS - Date.now();
  return diff > 0 ? Math.ceil(diff / (60 * 60 * 1000)) : 0;
}

/**
 * [UC-152 & UC-151 1a] Trang quản lý đánh giá của trung tâm tư vấn:
 * - Xem các đánh giá đang hiển thị về trung tâm
 * - Đăng tối đa 1 phản hồi công khai / đánh giá (<= 1.000 ký tự, không SĐT / email / liên kết)
 * - Chỉnh sửa phản hồi trong vòng 24 giờ kể từ lúc đăng (Flow 2a)
 * - Báo cáo đánh giá vi phạm để Moderator xem xét (UC-151 1a)
 */
export function CenterReviewsPortalView() {
  const [center, setCenter] = useState<StudyCenter | null>(null);
  const [data, setData] = useState<CenterReviewsDto | null>(null);
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Form viết / sửa phản hồi (mở tại chỗ trên từng đánh giá)
  const [activeReplyId, setActiveReplyId] = useState<string | null>(null);
  const [isEditingReply, setIsEditingReply] = useState(false);
  const [replyDraft, setReplyDraft] = useState("");
  const [replyError, setReplyError] = useState<string | null>(null);
  const [isSavingReply, setIsSavingReply] = useState(false);

  // Dialog báo cáo đánh giá (UC-151 1a)
  const [reportTarget, setReportTarget] = useState<CenterReviewDto | null>(null);
  const [reportReason, setReportReason] = useState<CenterReviewReportReason>("not_real_experience");
  const [reportDetail, setReportDetail] = useState("");
  const [isReporting, setIsReporting] = useState(false);
  const [reportedIds, setReportedIds] = useState<Set<string>>(() => new Set());

  useEffect(() => {
    let ignore = false;
    Promise.all([centerPortalApi.getProfile(), centerPortalApi.getReviews(page, 20)])
      .then(([profile, reviews]) => {
        if (!ignore) {
          setCenter(profile);
          setData(reviews);
          setError(null);
        }
      })
      .catch((err: unknown) => {
        if (!ignore) {
          setError(err instanceof ApiError ? err.message : "Không thể tải danh sách đánh giá.");
        }
      })
      .finally(() => {
        if (!ignore) setLoading(false);
      });
    return () => {
      ignore = true;
    };
  }, [page]);

  const goToPage = (nextPage: number) => {
    setLoading(true);
    setPage(nextPage);
  };

  const isCenterEligibleToReply = center?.isActive !== false && center?.verificationStatus === "verified";

  const openReplyForm = (review: CenterReviewDto, edit: boolean) => {
    setActiveReplyId(review.id);
    setIsEditingReply(edit);
    setReplyDraft(edit ? review.centerReply?.content ?? "" : "");
    setReplyError(null);
  };

  const closeReplyForm = () => {
    if (isSavingReply) return;
    setActiveReplyId(null);
    setIsEditingReply(false);
    setReplyDraft("");
    setReplyError(null);
  };

  const submitReply = async (reviewId: string) => {
    const trimmed = replyDraft.trim();
    if (!trimmed) {
      setReplyError("Nội dung phản hồi không được để trống.");
      return;
    }
    if (trimmed.length > MAX_REPLY_LENGTH) {
      setReplyError(`Nội dung phản hồi tối đa ${MAX_REPLY_LENGTH} ký tự.`);
      return;
    }

    setIsSavingReply(true);
    setReplyError(null);
    try {
      const updated = isEditingReply
        ? await centerPortalApi.editReviewReply(reviewId, trimmed)
        : await centerPortalApi.createReviewReply(reviewId, trimmed);

      setData((prev) =>
        prev
          ? { ...prev, items: prev.items.map((item) => (item.id === reviewId ? updated : item)) }
          : prev,
      );
      toast.add({
        type: "success",
        title: isEditingReply ? "Đã cập nhật phản hồi" : "Đã đăng phản hồi công khai",
        description: isEditingReply
          ? "Phản hồi đã được cập nhật và gắn nhãn Đã chỉnh sửa."
          : "Người viết đánh giá đã nhận được thông báo về phản hồi của bạn.",
      });
      closeReplyForm();
    } catch (err: unknown) {
      const msg = err instanceof ApiError ? err.message : "Không thể lưu phản hồi. Vui lòng thử lại.";
      setReplyError(msg);
      toast.add({ type: "error", title: "Lưu phản hồi thất bại", description: msg });
    } finally {
      setIsSavingReply(false);
    }
  };

  const confirmReport = async () => {
    if (!reportTarget || !center || isReporting) return;
    setIsReporting(true);
    try {
      await studyCentersApi.reportReview(center.id, reportTarget.id, reportReason, reportDetail);
      setReportedIds((prev) => new Set(prev).add(reportTarget.id));
      toast.add({
        type: "success",
        title: "Đã gửi báo cáo đánh giá",
        description: "Kiểm duyệt viên sẽ xem xét khách quan. Lưu ý hệ thống đánh dấu báo cáo do trung tâm gửi.",
      });
      setReportTarget(null);
      setReportReason("not_real_experience");
      setReportDetail("");
    } catch (err: unknown) {
      toast.add({
        type: "error",
        title: "Báo cáo thất bại",
        description: err instanceof ApiError ? err.message : "Không thể gửi báo cáo. Vui lòng thử lại.",
      });
    } finally {
      setIsReporting(false);
    }
  };

  const totalPages = data ? Math.max(1, Math.ceil(data.totalCount / 20)) : 1;

  return (
    <div className="min-h-screen bg-ground">
      <CenterNav />
      <div className="container mx-auto max-w-4xl px-4 py-6 sm:px-6 space-y-6">
        <div className="flex flex-wrap items-start justify-between gap-4">
          <div className="space-y-1">
            <h1 className="font-heading text-2xl font-bold text-ink">Đánh giá từ học sinh &amp; phụ huynh</h1>
            <p className="text-sm text-ink-2">
              Mỗi đánh giá được phản hồi công khai tối đa 1 lần (tối đa 1.000 ký tự, không chứa số điện thoại, email hay liên kết).
              Phản hồi có thể chỉnh sửa trong 24 giờ đầu kể từ lúc đăng.
            </p>
          </div>
          {data && data.totalCount > 0 && data.averageRating != null && (
            <div className="rounded-xl border border-line bg-surface px-4 py-2.5 text-right">
              <div className="flex items-center justify-end gap-1 font-heading text-xl font-bold text-ink tabular-nums">
                <StarIcon className="size-5 fill-current text-gold" />
                {formatRating(data.averageRating)} / 5.0
              </div>
              <p className="text-xs text-ink-3">{data.totalCount} đánh giá công khai</p>
            </div>
          )}
        </div>

        {!loading && center && !isCenterEligibleToReply && (
          <Alert>
            <AlertDescription>
              Trung tâm hiện đang bị ẩn hoặc chưa/bị thu hồi dấu xác minh, vì vậy chưa thể đăng hoặc chỉnh sửa phản hồi đánh giá.
            </AlertDescription>
          </Alert>
        )}

        {error && (
          <Alert variant="destructive">
            <AlertDescription>{error}</AlertDescription>
          </Alert>
        )}

        {loading ? (
          <div className="flex items-center justify-center gap-2 py-12 text-sm text-ink-2">
            <Spinner />
            Đang tải danh sách đánh giá…
          </div>
        ) : data && data.items.length === 0 ? (
          <Card>
            <CardHeader>
              <CardTitle className="text-base">Chưa có đánh giá nào</CardTitle>
              <CardDescription>
                Khi học sinh hoặc phụ huynh đã liên hệ để lại nhận xét về trung tâm, các đánh giá sẽ hiển thị tại đây.
              </CardDescription>
            </CardHeader>
          </Card>
        ) : (
          <div className="space-y-4">
            {data?.items.map((rev) => {
              const hoursLeft = rev.centerReply ? remainingEditHours(rev.centerReply.createdAt) : 0;
              const isFormOpen = activeReplyId === rev.id;

              return (
                <Card key={rev.id}>
                  <CardHeader className="pb-2">
                    <div className="flex flex-wrap items-start justify-between gap-2">
                      <div className="flex flex-wrap items-center gap-2">
                        <span className="font-semibold text-sm text-ink">
                          {rev.reviewerName} ({rev.reviewerRole === "parent" ? "Phụ huynh" : "Học sinh"})
                        </span>
                        {rev.isVerified && (
                          <Badge variant="ok" className="text-xs py-0">
                            ✓ Đã sử dụng dịch vụ
                          </Badge>
                        )}
                        <span className="text-xs text-ink-3">
                          {new Date(rev.createdAt).toLocaleDateString("vi-VN")}
                        </span>
                      </div>
                      <div className="flex items-center gap-0.5" aria-label={`${rev.rating} trên 5 sao`}>
                        {Array.from({ length: 5 }, (_, i) => (
                          <StarIcon
                            key={i}
                            className={i < rev.rating ? "size-4 fill-current text-gold" : "size-4 text-ink-3 opacity-35"}
                          />
                        ))}
                      </div>
                    </div>
                  </CardHeader>

                  <CardContent className="space-y-3">
                    {rev.comment ? (
                      <p className="text-sm text-ink-2 leading-relaxed whitespace-pre-line">{rev.comment}</p>
                    ) : (
                      <p className="text-xs italic text-ink-3">(Không có nhận xét bằng chữ)</p>
                    )}

                    {/* Phản hồi hiện tại của trung tâm */}
                    {rev.centerReply && !isFormOpen && (
                      <div className="rounded-lg border border-brand/20 bg-brand-wash/30 px-3.5 py-2.5 space-y-1.5">
                        <div className="flex flex-wrap items-center justify-between gap-2 text-xs">
                          <span className="inline-flex items-center gap-1.5 font-semibold text-brand-ink">
                            <Building2Icon className="size-3.5 text-brand" />
                            Phản hồi của trung tâm
                          </span>
                          <span className="text-[11px] text-ink-3">
                            {new Date(rev.centerReply.createdAt).toLocaleDateString("vi-VN")}
                            {rev.centerReply.editedAt ? " · Đã chỉnh sửa" : ""}
                          </span>
                        </div>
                        <p className="text-sm text-ink-2 leading-relaxed whitespace-pre-line">
                          {rev.centerReply.content}
                        </p>
                      </div>
                    )}

                    {/* Form viết / sửa phản hồi */}
                    {isFormOpen && (
                      <div className="space-y-2 rounded-lg border border-line bg-muted/20 p-3">
                        <Textarea
                          value={replyDraft}
                          onChange={(e) => {
                            setReplyDraft(e.target.value);
                            if (replyError) setReplyError(null);
                          }}
                          maxLength={MAX_REPLY_LENGTH}
                          rows={4}
                          disabled={isSavingReply}
                          placeholder="Nhập phản hồi lịch sự, khách quan (không chứa số điện thoại, email hay liên kết website)..."
                          aria-label="Nội dung phản hồi đánh giá"
                          className="text-sm resize-y bg-surface"
                        />
                        {replyError && <p className="text-xs font-medium text-destructive">{replyError}</p>}
                        <div className="flex flex-wrap items-center justify-between gap-2">
                          <span className="text-xs text-ink-3">
                            {replyDraft.trim().length} / {MAX_REPLY_LENGTH} ký tự
                          </span>
                          <div className="flex items-center gap-2">
                            <Button type="button" variant="ghost" size="sm" disabled={isSavingReply} onClick={closeReplyForm}>
                              Hủy
                            </Button>
                            <Button
                              type="button"
                              size="sm"
                              loading={isSavingReply}
                              disabled={isSavingReply || replyDraft.trim().length === 0}
                              onClick={() => void submitReply(rev.id)}
                            >
                              {isEditingReply ? "Lưu chỉnh sửa" : "Đăng phản hồi"}
                            </Button>
                          </div>
                        </div>
                      </div>
                    )}

                    {/* Thanh hành động */}
                    <div className="flex flex-wrap items-center justify-between gap-2 pt-1 border-t border-line/60">
                      <div className="flex flex-wrap items-center gap-2">
                        {!rev.centerReply && !isFormOpen && (
                          <Button
                            type="button"
                            variant="outline"
                            size="sm"
                            disabled={!isCenterEligibleToReply}
                            onClick={() => openReplyForm(rev, false)}
                            className="gap-1.5"
                          >
                            <MessageSquareReplyIcon className="size-3.5" />
                            Viết phản hồi
                          </Button>
                        )}
                        {rev.centerReply && !isFormOpen && hoursLeft > 0 && (
                          <Button
                            type="button"
                            variant="outline"
                            size="sm"
                            disabled={!isCenterEligibleToReply}
                            onClick={() => openReplyForm(rev, true)}
                            className="gap-1.5"
                          >
                            <PencilIcon className="size-3.5" />
                            Chỉnh sửa (còn ~{hoursLeft} giờ)
                          </Button>
                        )}
                        {rev.centerReply && hoursLeft === 0 && (
                          <span className="text-xs text-ink-3">Đã hết thời hạn 24 giờ chỉnh sửa phản hồi</span>
                        )}
                      </div>

                      {reportedIds.has(rev.id) ? (
                        <span className="text-xs text-ink-3">Đã gửi báo cáo đánh giá này</span>
                      ) : (
                        <Button
                          type="button"
                          variant="ghost"
                          size="sm"
                          onClick={() => setReportTarget(rev)}
                          className="gap-1.5 text-xs text-ink-3 hover:text-risk"
                        >
                          <FlagIcon className="size-3.5" />
                          Báo cáo đánh giá
                        </Button>
                      )}
                    </div>
                  </CardContent>
                </Card>
              );
            })}

            {totalPages > 1 && (
              <div className="flex items-center justify-center gap-3 pt-2">
                <Button type="button" variant="outline" size="sm" disabled={page <= 1} onClick={() => goToPage(page - 1)}>
                  Trang trước
                </Button>
                <span className="text-xs text-ink-2">
                  Trang {page} / {totalPages}
                </span>
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  disabled={page >= totalPages}
                  onClick={() => goToPage(page + 1)}
                >
                  Trang sau
                </Button>
              </div>
            )}
          </div>
        )}
      </div>

      {/* Dialog báo cáo đánh giá (UC-151 1a) */}
      <AlertDialog
        open={reportTarget !== null}
        onOpenChange={(open) => {
          if (!open && !isReporting) {
            setReportTarget(null);
            setReportDetail("");
          }
        }}
      >
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Báo cáo đánh giá về trung tâm</AlertDialogTitle>
            <AlertDialogDescription>
              Kiểm duyệt viên sẽ xem xét khách quan theo quy chuẩn cộng đồng. Báo cáo do trung tâm gửi sẽ được đánh dấu rõ để đảm bảo minh bạch.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <RadioGroup
            value={reportReason}
            onValueChange={(v) => setReportReason(v as CenterReviewReportReason)}
            className="gap-2"
          >
            {CENTER_REVIEW_REPORT_REASON_OPTIONS.map((opt) => (
              <div key={opt.value} className="flex items-center gap-2.5">
                <RadioGroupItem id={`portal-report-${opt.value}`} value={opt.value} disabled={isReporting} />
                <Label htmlFor={`portal-report-${opt.value}`} className="cursor-pointer text-sm font-normal">
                  {opt.label}
                </Label>
              </div>
            ))}
          </RadioGroup>
          <Textarea
            value={reportDetail}
            onChange={(e) => setReportDetail(e.target.value)}
            maxLength={1000}
            rows={3}
            disabled={isReporting}
            placeholder="Chi tiết bổ sung (tùy chọn, tối đa 1.000 ký tự)"
            aria-label="Chi tiết báo cáo đánh giá"
            className="text-sm resize-y"
          />
          <AlertDialogFooter>
            <AlertDialogCancel disabled={isReporting}>Hủy</AlertDialogCancel>
            <Button type="button" onClick={() => void confirmReport()} disabled={isReporting} loading={isReporting} className="gap-1.5">
              <FlagIcon className="size-3.5" />
              Gửi báo cáo
            </Button>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}
