"use client";

import React, { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { ArrowRightLeftIcon, CheckIcon, UserPlusIcon, UsersIcon, XIcon } from "lucide-react";
import { profileApi, ApiError } from "@/lib/api";
import type { ParentProfileOfferDto, ParentProfileOverviewDto } from "@/types/api";
import { AcademicSummaryCard } from "@/components/account/AcademicSummaryCard";
import { AccountSectionHeader } from "@/components/account/AccountSectionHeader";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button, buttonVariants } from "@/components/ui/button";
import {
  Empty,
  EmptyContent,
  EmptyDescription,
  EmptyHeader,
  EmptyMedia,
  EmptyTitle,
} from "@/components/ui/empty";

function errorMessage(err: unknown, fallback: string) {
  if (err instanceof ApiError) return err.message || fallback;
  return "Có lỗi kết nối máy chủ. Vui lòng thử lại sau.";
}

// "YYYY-MM-DD" → "dd/mm/yyyy"
function formatDob(dob: string | null) {
  if (!dob) return null;
  const [y, m, d] = dob.split("-");
  return `${d}/${m}/${y}`;
}

// Lệch quá 1 năm thì nhiều khả năng chọn nhầm con; chưa có ngày sinh thì không so được
function isDobFarApart(a: string | null, b: string | null) {
  if (!a || !b) return false;
  const days = Math.abs(Date.parse(a) - Date.parse(b)) / 86_400_000;
  return days > 366;
}

function nameWithDob(name: string, dob: string | null) {
  const formatted = formatDob(dob);
  return formatted ? `${name} (sinh ${formatted})` : `${name} (chưa có ngày sinh)`;
}

interface ParentAcademicSectionProps {
  overview: ParentProfileOverviewDto;
  onChanged: () => void;
}

// 1.5d: phụ huynh không có hồ sơ riêng, chỉ xem hồ sơ từng con (chỉ đọc). Con thật vừa liên kết mà phụ huynh
// đang có con managed thì hỏi "Đây có phải con bạn đã nhập?". Con thật đã có hồ sơ riêng thì con tự chọn, không ghi đè.
export function ParentAcademicSection({ overview, onChanged }: ParentAcademicSectionProps) {
  const [submitting, setSubmitting] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const managedChildren = overview.children.filter((c) => c.isManaged);

  const match = async (linkId: string, managedChildId: string | null) => {
    setSubmitting(`${linkId}:${managedChildId ?? "none"}`);
    setError(null);
    try {
      const res = await profileApi.matchManagedChild(linkId, managedChildId);
      setNotice(res.message);
      onChanged();
    } catch (err) {
      setError(errorMessage(err, "Không ghi nhận được lựa chọn."));
    } finally {
      setSubmitting(null);
    }
  };

  return (
    <div>
      <AccountSectionHeader
        title="Hồ sơ học thuật của con"
        description="Tài khoản phụ huynh không có hồ sơ riêng; bạn xem tổng quan bảng điểm và chứng chỉ của các con đã liên kết tại đây."
        actions={
          <Link href="/parent/children" className={buttonVariants({ variant: "outline", size: "lg", className: "bg-surface" })}>
            <UsersIcon data-icon="inline-start" aria-hidden />
            Quản lý Con của tôi
          </Link>
        }
      />

      {overview.children.length === 0 ? (
        <Empty className="border border-line bg-surface py-12">
          <EmptyHeader>
            <EmptyMedia variant="icon" className="size-12 rounded-2xl bg-brand-wash text-brand">
              <UsersIcon className="size-6" aria-hidden />
            </EmptyMedia>
            <EmptyTitle className="text-base font-semibold text-ink">
              Chưa có hồ sơ con để hiển thị
            </EmptyTitle>
            <EmptyDescription>
              Thêm con hoặc liên kết với tài khoản của con ở mục Con của tôi để nhập và theo dõi hồ sơ học thuật.
            </EmptyDescription>
          </EmptyHeader>
          <EmptyContent>
            <Link href="/parent/children/add" className={buttonVariants({ size: "lg" })}>
              <UserPlusIcon data-icon="inline-start" aria-hidden />
              Thêm con
            </Link>
          </EmptyContent>
        </Empty>
      ) : (
        <div className="space-y-6">
          {notice && (
            <Alert role="status">
              <AlertDescription>{notice}</AlertDescription>
            </Alert>
          )}

          {error && (
            <Alert variant="destructive">
              <AlertDescription>{error}</AlertDescription>
            </Alert>
          )}

          {overview.pendingMatches.map((pending) => (
            <div key={pending.linkId} className="space-y-4 rounded-xl border border-brand/25 bg-surface p-5 sm:p-6">
              <div className="space-y-1">
                <h2 className="flex items-center gap-2 font-heading text-lg font-semibold text-ink">
                  <ArrowRightLeftIcon className="size-5 text-brand" aria-hidden />
                  {nameWithDob(pending.studentName, pending.dateOfBirth)} có phải là con bạn đã nhập hồ sơ?
                </h2>
                <p className="text-sm text-ink-2">
                  Chọn đúng con bạn đã nhập để chuyển hồ sơ, trường mục tiêu và kịch bản sang tài khoản mới liên kết. Nếu
                  con đã có hồ sơ riêng, con sẽ tự chọn dùng bản của bạn hay giữ bản của mình.
                </p>
              </div>

              {managedChildren.some((m) => isDobFarApart(m.dateOfBirth, pending.dateOfBirth)) && (
                <Alert role="status">
                  <AlertDescription>
                    Ngày sinh {pending.studentName} tự khai lệch hơn 1 năm so với ngày sinh bạn đã nhập cho{" "}
                    {managedChildren
                      .filter((m) => isDobFarApart(m.dateOfBirth, pending.dateOfBirth))
                      .map((m) => m.studentName)
                      .join(", ")}
                    . Kiểm tra lại trước khi chuyển. Sau khi chuyển, tài khoản của con giữ ngày sinh con tự khai.
                  </AlertDescription>
                </Alert>
              )}

              <div className="flex flex-wrap gap-2">
                {managedChildren.map((managed) => (
                  <Button
                    key={managed.studentUserId}
                    disabled={submitting !== null}
                    onClick={() => match(pending.linkId, managed.studentUserId)}
                    loading={submitting === `${pending.linkId}:${managed.studentUserId}`}
                  >
                    <CheckIcon data-icon="inline-start" aria-hidden />
                    Là {nameWithDob(managed.studentName, managed.dateOfBirth)}
                  </Button>
                ))}
                <Button
                  variant="outline"
                  disabled={submitting !== null}
                  onClick={() => match(pending.linkId, null)}
                  loading={submitting === `${pending.linkId}:none`}
                >
                  <XIcon data-icon="inline-start" aria-hidden />
                  Không phải ai trong số này
                </Button>
              </div>
            </div>
          ))}

          {overview.children.map((child) => (
            <div key={child.linkId} className="space-y-3">
              {child.profileTransferStatus === "offered" && (
                <Alert role="status">
                  <AlertDescription>
                    Đã gửi hồ sơ bạn nhập cho {child.studentName}. Con đã có hồ sơ riêng nên đang chờ con chọn bản nào.
                  </AlertDescription>
                </Alert>
              )}
              <AcademicSummaryCard
                profile={child.profile}
                isLoading={false}
                readOnly
                title={`Hồ sơ của ${child.studentName}`}
              />
            </div>
          ))}
        </div>
      )}
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
        <div key={offer.linkId} className="space-y-4 rounded-xl border border-brand/25 bg-surface p-5 sm:p-6">
          <div className="space-y-1">
            <h2 className="flex items-center gap-2 font-heading text-lg font-semibold text-ink">
              <UsersIcon className="size-5 text-brand" aria-hidden />
              {offer.parentName} đã nhập hồ sơ cho bạn
            </h2>
            <p className="text-sm text-ink-2">
              Bạn đã có hồ sơ riêng nên hệ thống không tự ghi đè. Chọn dùng bản của phụ huynh (thay hồ sơ hiện tại) hoặc
              giữ bản của mình.
            </p>
          </div>

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
            <Button
              disabled={submitting !== null}
              onClick={() => decide(offer.linkId, true)}
              loading={submitting === `${offer.linkId}:true`}
            >
              <CheckIcon data-icon="inline-start" aria-hidden />
              Dùng bản của phụ huynh
            </Button>
            <Button
              variant="outline"
              disabled={submitting !== null}
              onClick={() => decide(offer.linkId, false)}
              loading={submitting === `${offer.linkId}:false`}
            >
              Giữ bản của mình
            </Button>
          </div>
        </div>
      ))}
    </div>
  );
}
