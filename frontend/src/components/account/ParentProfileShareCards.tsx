"use client";

import React, { useCallback, useEffect, useState } from "react";
import { ArrowRightLeftIcon, UsersIcon } from "lucide-react";
import { profileApi, ApiError } from "@/lib/api";
import type { ParentProfileOfferDto, ParentProfileOverviewDto } from "@/types/api";
import { AcademicSummaryCard } from "@/components/account/AcademicSummaryCard";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Spinner } from "@/components/ui/spinner";

function errorMessage(err: unknown, fallback: string) {
  if (err instanceof ApiError) return err.message || fallback;
  return "Có lỗi kết nối máy chủ. Vui lòng thử lại sau.";
}

interface ParentAcademicSectionProps {
  overview: ParentProfileOverviewDto;
  onChanged: () => void;
}

// Phụ huynh đã liên kết con: xem hồ sơ từng con (chỉ đọc). Hồ sơ phụ huynh tự nhập mà có từ 2 con
// thì chọn con nhận hồ sơ (hoặc không chuyển). Con đã có hồ sơ riêng thì con tự chọn, không ghi đè.
export function ParentAcademicSection({ overview, onChanged }: ParentAcademicSectionProps) {
  const [submitting, setSubmitting] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  const share = async (linkId: string | null) => {
    setSubmitting(linkId ?? "skip");
    setError(null);
    try {
      const res = await profileApi.shareToChild(linkId);
      setNotice(res.message);
      onChanged();
    } catch (err) {
      setError(errorMessage(err, "Không chuyển được hồ sơ."));
    } finally {
      setSubmitting(null);
    }
  };

  return (
    <div className="space-y-4">
      {notice && (
        <Alert role="status">
          <AlertDescription>{notice}</AlertDescription>
        </Alert>
      )}

      {overview.hasPendingOwnProfile && (
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <ArrowRightLeftIcon className="size-5" aria-hidden />
              <h2>Chuyển hồ sơ bạn đã nhập cho con nào?</h2>
            </CardTitle>
            <CardDescription>
              Bạn đã nhập hồ sơ trước khi liên kết. Chọn một con để nhận hồ sơ này. Nếu con đã có hồ sơ riêng, con sẽ tự
              chọn dùng bản của bạn hay giữ bản của mình.
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-3">
            {error && (
              <Alert variant="destructive">
                <AlertDescription>{error}</AlertDescription>
              </Alert>
            )}
            <div className="flex flex-wrap gap-2">
              {overview.children.map((child) => (
                <Button key={child.linkId} disabled={submitting !== null} onClick={() => share(child.linkId)}>
                  {submitting === child.linkId && <Spinner />}
                  Chuyển cho {child.studentName}
                </Button>
              ))}
              <Button variant="outline" disabled={submitting !== null} onClick={() => share(null)}>
                {submitting === "skip" && <Spinner />}
                Không chuyển
              </Button>
            </div>
          </CardContent>
        </Card>
      )}

      {overview.children.map((child) => (
        <div key={child.linkId} className="space-y-2">
          {child.profileTransferStatus === "offered" && (
            <Alert role="status">
              <AlertDescription>
                Đã gửi hồ sơ của bạn cho {child.studentName}. Con đã có hồ sơ riêng nên đang chờ con chọn bản nào.
              </AlertDescription>
            </Alert>
          )}
          <AcademicSummaryCard profile={child.profile} isLoading={false} readOnly title={`Hồ sơ của ${child.studentName}`} />
        </div>
      ))}
    </div>
  );
}

interface StudentParentOffersProps {
  onChanged: () => void;
}

// Học sinh đã có hồ sơ riêng mà phụ huynh liên kết cũng có hồ sơ: chọn dùng bản của phụ huynh hay giữ bản của mình
export function StudentParentOffers({ onChanged }: StudentParentOffersProps) {
  const [offers, setOffers] = useState<ParentProfileOfferDto[]>([]);
  const [submitting, setSubmitting] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(() => {
    profileApi
      .getParentOffers()
      .then((res) => setOffers(res.data ?? []))
      .catch(() => setOffers([]));
  }, []);

  useEffect(() => {
    load();
  }, [load]);

  const decide = async (linkId: string, accept: boolean) => {
    setSubmitting(`${linkId}:${accept}`);
    setError(null);
    try {
      await profileApi.decideParentOffer(linkId, accept);
      load();
      onChanged();
    } catch (err) {
      setError(errorMessage(err, "Không ghi nhận được lựa chọn."));
    } finally {
      setSubmitting(null);
    }
  };

  if (offers.length === 0) return null;

  return (
    <div className="space-y-4">
      {offers.map((offer) => (
        <Card key={offer.linkId}>
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <UsersIcon className="size-5" aria-hidden />
              <h2>{offer.parentName} đã nhập hồ sơ cho bạn</h2>
            </CardTitle>
            <CardDescription>
              Bạn đã có hồ sơ riêng nên hệ thống không tự ghi đè. Chọn dùng bản của phụ huynh (thay hồ sơ hiện tại) hoặc
              giữ bản của mình.
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-3">
            {error && (
              <Alert variant="destructive">
                <AlertDescription>{error}</AlertDescription>
              </Alert>
            )}
            <AcademicSummaryCard
              profile={offer.parentProfile}
              isLoading={false}
              readOnly
              title={`Bản của ${offer.parentName}`}
            />
            <div className="flex flex-col gap-2 sm:flex-row">
              <Button disabled={submitting !== null} onClick={() => decide(offer.linkId, true)}>
                {submitting === `${offer.linkId}:true` && <Spinner />}
                Dùng bản của phụ huynh
              </Button>
              <Button variant="outline" disabled={submitting !== null} onClick={() => decide(offer.linkId, false)}>
                {submitting === `${offer.linkId}:false` && <Spinner />}
                Giữ bản của mình
              </Button>
            </div>
          </CardContent>
        </Card>
      ))}
    </div>
  );
}
