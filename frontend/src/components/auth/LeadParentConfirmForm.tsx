"use client";

import React, { useCallback, useEffect, useState } from "react";
import { useSearchParams } from "next/navigation";
import { CheckIcon, SendIcon, TriangleAlertIcon, XIcon } from "lucide-react";
import { ApiError } from "@/lib/api";
import { leadParentConfirmApi } from "@/lib/lead-api";
import { LEAD_SHARED_FIELD_INFO, type LeadParentConfirmInfo } from "@/types/lead";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Separator } from "@/components/ui/separator";
import { Spinner } from "@/components/ui/spinner";

const EXPIRED_MESSAGE = "Link đã hết hạn (quá 7 ngày) nên trung tâm không nhận yêu cầu này. Con có thể gửi lại yêu cầu liên hệ.";
const INVALID_MESSAGE = "Link xác nhận không hợp lệ.";

function formatDate(value: string | null) {
  return value ? new Date(value).toLocaleString("vi-VN") : "";
}

// [UC-116] Phụ huynh mở từ link trong mail: con dưới 16 tuổi gửi yêu cầu liên hệ trung tâm, phụ huynh Đồng ý / Từ chối.
// Không cần tài khoản; mở link không đổi trạng thái, phải bấm nút. Con có nhiều phụ huynh: người bấm trước quyết định.
export function LeadParentConfirmForm() {
  const searchParams = useSearchParams();
  const token = searchParams.get("token") ?? "";
  // Phụ huynh bấm Từ chối trong mail: vẫn phải bấm lại ở đây
  const choseReject = searchParams.get("choice") === "reject";

  const [info, setInfo] = useState<LeadParentConfirmInfo | null>(null);
  const [loadError, setLoadError] = useState<string | null>(token ? null : INVALID_MESSAGE);
  const [isLoading, setIsLoading] = useState(Boolean(token));
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState<"accept" | "reject" | null>(null);

  const load = useCallback(
    () =>
      leadParentConfirmApi
        .lookup(token)
        .then((res) => setInfo(res.data))
        .catch((err) => {
          if (err instanceof ApiError && err.status === 410) {
            setLoadError(EXPIRED_MESSAGE);
          } else if (err instanceof ApiError) {
            setLoadError(INVALID_MESSAGE);
          } else {
            setLoadError("Có lỗi kết nối máy chủ. Vui lòng thử lại sau.");
          }
        })
        .finally(() => setIsLoading(false)),
    [token],
  );

  useEffect(() => {
    if (token) load();
  }, [token, load]);

  const decide = async (choice: "accept" | "reject") => {
    setError(null);
    setSubmitting(choice);
    try {
      const res = choice === "accept" ? await leadParentConfirmApi.accept(token) : await leadParentConfirmApi.reject(token);
      setInfo(res.data);
    } catch (err) {
      if (err instanceof ApiError && err.status === 409) {
        // Phụ huynh khác (hoặc tab khác) đã trả lời trước: tải lại để hiện kết quả đã ghi nhận
        await load();
      } else if (err instanceof ApiError && err.status === 410) {
        setLoadError(EXPIRED_MESSAGE);
      } else if (err instanceof ApiError) {
        setError(err.message || "Không thể ghi nhận lúc này. Vui lòng thử lại.");
      } else {
        setError("Có lỗi kết nối máy chủ. Vui lòng thử lại sau.");
      }
    } finally {
      setSubmitting(null);
    }
  };

  if (isLoading) {
    return (
      <div className="flex items-center gap-2 text-sm text-ink-2">
        <Spinner />
        Đang tải yêu cầu...
      </div>
    );
  }

  const decidedBy = info?.decidedByYou ? "Bạn đã" : "Một phụ huynh khác của con đã";

  return (
    <div className="space-y-6">
      <div className="space-y-1">
        <h1 className="font-heading text-2xl font-semibold tracking-tight text-ink sm:text-3xl">
          Xác nhận yêu cầu liên hệ trung tâm
        </h1>
        <p className="text-sm text-ink-2">
          Con dưới 16 tuổi muốn gửi thông tin cho trung tâm du học. Trung tâm chỉ nhận khi phụ huynh đồng ý. Bạn không cần tài khoản để
          trả lời.
        </p>
      </div>

      {loadError || !info ? (
        <Alert variant="destructive">
          <TriangleAlertIcon aria-hidden />
          <AlertDescription>{loadError ?? INVALID_MESSAGE}</AlertDescription>
        </Alert>
      ) : info.state === "accepted" ? (
        <Alert variant="success" role="status">
          <CheckIcon aria-hidden />
          <AlertTitle>Đã ghi nhận đồng ý</AlertTitle>
          <AlertDescription>
            {decidedBy} đồng ý lúc {formatDate(info.decidedAt)}. {info.centerName} đã nhận yêu cầu của {info.studentName} và sẽ liên hệ
            lại. Hệ thống đã gửi email báo kết quả cho con.
          </AlertDescription>
        </Alert>
      ) : info.state === "rejected" ? (
        <Alert variant="warning" role="status">
          <XIcon aria-hidden />
          <AlertTitle>Đã ghi nhận từ chối</AlertTitle>
          <AlertDescription>
            {decidedBy} từ chối lúc {formatDate(info.decidedAt)}. {info.centerName} không nhận thông tin của {info.studentName}. Nếu bấm
            nhầm, con có thể gửi lại yêu cầu.
          </AlertDescription>
        </Alert>
      ) : (
        <>
          <div className="space-y-1.5 text-sm text-ink">
            <div>
              <span className="text-label">Học sinh: </span>
              <span className="font-semibold">{info.studentName}</span>
              {info.studentAge != null && <span className="text-ink-2"> ({info.studentAge} tuổi)</span>}
            </div>
            <div>
              <span className="text-label">Trung tâm: </span>
              <span className="font-semibold">{info.centerName}</span>
            </div>
            <div>
              <span className="text-label">Email phụ huynh: </span>
              <span className="font-semibold">{info.parentEmail}</span>
            </div>
            {info.expiresAt && <div className="text-body-s text-ink-3">Link hết hạn lúc {formatDate(info.expiresAt)}.</div>}
          </div>

          <Separator />

          <div className="space-y-3 text-sm">
            <h2 className="flex items-center gap-2 font-semibold text-ink">
              <SendIcon className="size-4 text-brand" aria-hidden />
              Thông tin trung tâm sẽ nhận
            </h2>
            <ul className="list-disc space-y-0.5 pl-5 text-ink-2">
              {info.sharedFields.map((field) => {
                const fieldInfo = LEAD_SHARED_FIELD_INFO[field];
                return <li key={field}>{fieldInfo ? `${fieldInfo.label}: ${fieldInfo.description}` : field}</li>;
              })}
              {info.contactPhone && <li>Số điện thoại: {info.contactPhone}</li>}
            </ul>
            {info.message && (
              <div className="space-y-1">
                <div className="font-medium text-ink">Lời nhắn của con</div>
                <p className="whitespace-pre-line italic text-ink-2">“{info.message}”</p>
              </div>
            )}
            <p className="text-body-s text-ink-3">
              Căn cứ: Luật Bảo vệ dữ liệu cá nhân 91/2025/QH15 (Điều 24). Hệ thống lưu thời điểm, email và địa chỉ IP khi bạn trả lời.
            </p>
          </div>

          <Separator />

          {error && (
            <Alert variant="destructive">
              <AlertDescription>{error}</AlertDescription>
            </Alert>
          )}

          {choseReject && (
            <p className="text-sm text-ink-2">
              Bạn đã chọn <strong className="text-ink">Từ chối</strong> trong email. Bấm nút Từ chối bên dưới để xác nhận.
            </p>
          )}

          <div className="space-y-4">
            <div className="flex flex-col gap-2 sm:flex-row">
              <Button type="button" size="xl" className="flex-1" disabled={submitting !== null} onClick={() => decide("accept")} loading={submitting === "accept"}>
                {submitting === "accept" ? "Đang ghi nhận..." : "Đồng ý gửi cho trung tâm"}
              </Button>
              <Button
                type="button"
                size="xl"
                variant="outline"
                className="flex-1"
                disabled={submitting !== null}
                onClick={() => decide("reject")}
                loading={submitting === "reject"}
              >
                {submitting === "reject" ? "Đang ghi nhận..." : "Từ chối"}
              </Button>
            </div>
            <p className="text-body-s text-ink-3">
              Nếu bạn không phải phụ huynh của {info.studentName}, hãy đóng trang này; trung tâm không nhận thông tin khi chưa có phụ huynh
              đồng ý.
            </p>
          </div>
        </>
      )}
    </div>
  );
}
