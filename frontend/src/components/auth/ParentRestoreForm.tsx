"use client";

import React, { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { useSearchParams } from "next/navigation";
import { CheckIcon, ShieldCheckIcon, TriangleAlertIcon } from "lucide-react";
import { parentLinksApi, ApiError } from "@/lib/api";
import { getCurrentPolicy } from "@/lib/policy-api";
import type { ParentRestoreInfo } from "@/types/api";
import type { PublicPolicy } from "@/types/policy";
import { PolicyContent } from "@/components/policy/PolicyContent";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Button, buttonVariants } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Label } from "@/components/ui/label";
import { Separator } from "@/components/ui/separator";
import { Spinner } from "@/components/ui/spinner";

const INVALID_LINK = "Liên kết khôi phục không hợp lệ, đã được sử dụng hoặc đã hết hạn.";
const NETWORK_ERROR = "Có lỗi kết nối máy chủ. Vui lòng thử lại sau.";

function formatDate(value: string) {
  return new Date(value).toLocaleDateString("vi-VN");
}

// [UC-75 4b] Người đại diện mở từ hai nút trong mail xác nhận yêu cầu xóa tài khoản của con, không cần đăng nhập.
// "Hủy xóa và đồng ý lại cho con": đọc nội dung đồng ý và tick là cha, mẹ, người giám hộ như khi đồng ý qua link mời.
// "Chỉ hủy xóa": khôi phục tài khoản, đồng ý vẫn đã rút. Dùng một nút thì nút còn lại hết tác dụng.
export function ParentRestoreForm() {
  const searchParams = useSearchParams();
  const token = searchParams.get("token") ?? "";

  const [info, setInfo] = useState<ParentRestoreInfo | null>(null);
  const [loadError, setLoadError] = useState<string | null>(token ? null : INVALID_LINK);
  const [isLoadingInfo, setIsLoadingInfo] = useState(Boolean(token));
  const [isGuardian, setIsGuardian] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [doneMessage, setDoneMessage] = useState<string | null>(null);
  // [UC-103] Văn bản đồng ý đang hiệu lực. undefined: đang tải; null: chưa có bản nào
  const [policy, setPolicy] = useState<PublicPolicy | null | undefined>(undefined);
  const [policyFailed, setPolicyFailed] = useState(false);

  useEffect(() => {
    if (!token) return;
    parentLinksApi
      .lookupRestore(token)
      .then((res) => setInfo(res.data))
      .catch((err) => setLoadError(err instanceof ApiError ? INVALID_LINK : NETWORK_ERROR))
      .finally(() => setIsLoadingInfo(false));
  }, [token]);

  const reconsent = info?.mode === "reconsent" && info.canReconsent;

  const loadPolicy = useCallback(
    () =>
      getCurrentPolicy("child_data_consent")
        .then((p) => {
          setPolicy(p);
          setPolicyFailed(false);
        })
        .catch(() => setPolicyFailed(true)),
    [],
  );

  useEffect(() => {
    if (reconsent) loadPolicy();
  }, [reconsent, loadPolicy]);

  const handleRestore = async () => {
    setError(null);
    setIsSubmitting(true);
    try {
      const res = reconsent
        ? await parentLinksApi.restore(token, isGuardian, policy?.version ?? info?.policyVersion)
        : await parentLinksApi.restore(token, false);
      setDoneMessage(res.message || "Đã hủy yêu cầu xóa.");
    } catch (err) {
      if (err instanceof ApiError && err.status === 404) {
        setLoadError(INVALID_LINK);
      } else if (err instanceof ApiError) {
        // Admin vừa công bố bản mới trong lúc đang đọc: tải lại văn bản để đọc lại
        if (reconsent && err.status === 400) await loadPolicy();
        setError(err.message || "Không thể khôi phục lúc này. Vui lòng thử lại.");
      } else {
        setError(NETWORK_ERROR);
      }
    } finally {
      setIsSubmitting(false);
    }
  };

  if (isLoadingInfo) {
    return (
      <div className="flex items-center gap-2 text-sm text-ink-2">
        <Spinner />
        Đang tải...
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div className="space-y-1">
        <h1 className="font-heading text-2xl font-semibold tracking-tight text-ink sm:text-3xl">Hủy yêu cầu xóa tài khoản của con</h1>
        <p className="text-sm text-ink-2">Khôi phục tài khoản của con trước ngày xóa vĩnh viễn. Bạn không cần đăng nhập.</p>
      </div>

      {loadError || !info ? (
        <Alert variant="destructive">
          <TriangleAlertIcon aria-hidden />
          <AlertDescription>{loadError ?? INVALID_LINK}</AlertDescription>
        </Alert>
      ) : doneMessage ? (
        <div className="space-y-6">
          <Alert variant="success" role="status">
            <CheckIcon aria-hidden />
            <AlertTitle>Đã khôi phục tài khoản</AlertTitle>
            <AlertDescription>
              {doneMessage}{" "}
              {reconsent
                ? "Chúng tôi đã gửi email xác nhận có link Rút đồng ý mới cho bạn và báo cho con."
                : "Chúng tôi đã báo cho con qua email."}
            </AlertDescription>
          </Alert>
          <Link href="/" className={buttonVariants({ size: "xl", variant: "outline", className: "w-full" })}>
            Về trang chủ
          </Link>
        </div>
      ) : (
        <>
          <div className="space-y-1.5 text-sm text-ink">
            <div>
              <span className="text-label">Học sinh: </span>
              <span className="font-semibold">{info.studentName}</span>
            </div>
            <div>
              <span className="text-label">Ngày xóa vĩnh viễn: </span>
              <span className="font-semibold">{formatDate(info.deletionDate)}</span>
            </div>
            <div>
              <span className="text-label">Bạn đã chọn: </span>
              <span className="font-semibold">
                {info.mode === "reconsent" ? "Hủy xóa và đồng ý lại cho con" : "Chỉ hủy xóa"}
              </span>
            </div>
          </div>

          {info.mode === "reconsent" && !info.canReconsent && (
            <Alert variant="warning">
              <TriangleAlertIcon aria-hidden />
              <AlertDescription>
                {info.studentName} đã đủ 16 tuổi và tự quyết định việc đồng ý xử lý dữ liệu của mình. Bạn chỉ hủy được yêu cầu xóa bằng nút
                &quot;Chỉ hủy xóa&quot; trong email.
              </AlertDescription>
            </Alert>
          )}

          {info.mode === "restore_only" && (
            <ul className="list-disc space-y-1 pl-5 text-sm text-ink-2">
              <li>Tài khoản của con được mở lại, con đăng nhập được như trước.</li>
              <li>Đồng ý của bạn vẫn đã rút: con tạm không dùng được các chức năng cần hồ sơ cho tới khi có người đại diện đồng ý.</li>
              <li>Nút &quot;Hủy xóa và đồng ý lại cho con&quot; trong email sẽ không dùng được nữa.</li>
            </ul>
          )}

          {reconsent && (
            <>
              <Separator />
              {/* Nội dung đồng ý: bản đang hiệu lực do admin công bố (UC-103); phiên bản được lưu kèm bản ghi đồng ý */}
              <div className="space-y-3 text-sm">
                <h2 className="flex items-center gap-2 font-semibold text-ink">
                  <ShieldCheckIcon className="size-4 text-brand" aria-hidden />
                  Nội dung đồng ý (phiên bản {policy?.version ?? info.policyVersion})
                </h2>
                {policyFailed && policy === undefined ? (
                  <Alert variant="destructive">
                    <TriangleAlertIcon aria-hidden />
                    <AlertDescription>
                      Không tải được nội dung đồng ý.{" "}
                      <button type="button" className="underline" onClick={() => loadPolicy()}>
                        Thử lại
                      </button>
                    </AlertDescription>
                  </Alert>
                ) : policy === undefined ? (
                  <div className="flex items-center gap-2 text-ink-2">
                    <Spinner />
                    Đang tải nội dung...
                  </div>
                ) : policy ? (
                  <PolicyContent content={policy.content} />
                ) : (
                  <p className="text-ink-2">
                    Cho phép hệ thống lưu hồ sơ, phân tích năng lực và gợi ý trường phù hợp cho việc du học Mỹ của con. Bạn rút lại được bất
                    cứ lúc nào bằng link trong email xác nhận gửi sau khi đồng ý.
                  </p>
                )}
                <p className="text-body-s text-ink-3">
                  Căn cứ: Luật Bảo vệ dữ liệu cá nhân 91/2025/QH15 (Điều 24) và Nghị định 356/2025/NĐ-CP. Hệ thống lưu thời điểm, email và
                  địa chỉ IP khi bạn đồng ý.
                </p>
              </div>
              <Separator />
              <div className="flex items-start gap-2">
                <Checkbox id="parent-restore-guardian" checked={isGuardian} onCheckedChange={(checked) => setIsGuardian(Boolean(checked))} />
                <Label htmlFor="parent-restore-guardian" className="leading-snug text-ink">
                  Tôi là cha, mẹ hoặc người giám hộ hợp pháp của {info.studentName}, đã đọc nội dung trên.
                </Label>
              </div>
            </>
          )}

          {error && (
            <Alert variant="destructive">
              <TriangleAlertIcon aria-hidden />
              <AlertDescription>{error}</AlertDescription>
            </Alert>
          )}

          {(info.mode === "restore_only" || reconsent) && (
            <Button
              type="button"
              size="xl"
              className="w-full"
              onClick={handleRestore}
              disabled={isSubmitting || (reconsent && (!isGuardian || policy === undefined))}
              loading={isSubmitting}
            >
              {isSubmitting ? "Đang khôi phục..." : reconsent ? "Hủy xóa và đồng ý lại" : "Chỉ hủy xóa"}
            </Button>
          )}
        </>
      )}
    </div>
  );
}
