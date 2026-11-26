"use client";

import React, { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import {
  Building2Icon,
  MessageSquareIcon,
  SearchIcon,
  UsersIcon,
} from "lucide-react";
import { useAuth } from "@/contexts/AuthContext";
import { AGE_FEATURES, useAgeBlock } from "@/lib/age-features";
import { getLeadRuleError, leadApi } from "@/lib/lead-api";
import { studyLevelLabel } from "@/lib/study-levels";
import { cn } from "@/lib/utils";
import { isLeadAwaitingCenter, LEAD_SHARED_FIELD_INFO, leadStatusLabel, type SentLeadDto } from "@/types/lead";
import { AccountSectionHeader } from "@/components/account/AccountSectionHeader";
import type { SentLeadThreadChange } from "@/components/account/SentLeadMessagesDialog";
import { SENT_THREAD_CHANGED_EVENT, useChatDock, type SentThreadChangedDetail } from "@/components/messages/ChatDock";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button, buttonVariants } from "@/components/ui/button";
import {
  Empty,
  EmptyContent,
  EmptyDescription,
  EmptyHeader,
  EmptyTitle,
} from "@/components/ui/empty";
import { Skeleton } from "@/components/ui/skeleton";

const PAGE_SIZE = 10;

// Màu giống badge trạng thái trong bảng lead phía trung tâm (UC-73, CenterLeadsListView).
const STATUS_BADGE_CLASS: Record<string, string> = {
  new: "bg-blue-600 text-white",
  accepted: "bg-amber-600 text-white", // [UC-73]
  rejected: "text-muted-foreground border-border bg-muted", // [UC-73]
  contacted: "bg-amber-600 text-white",
  closed: "text-emerald-700 border-emerald-300 bg-emerald-50 dark:bg-emerald-950/40",
  withdrawn: "text-muted-foreground border-border bg-muted", // [UC-80]
  // [UC-116]
  pending_parent: "text-amber-700 border-amber-300 bg-amber-50 dark:bg-amber-950/40",
  cancelled: "text-muted-foreground border-border bg-muted",
  expired: "text-muted-foreground border-border bg-muted",
  center_inactive: "text-muted-foreground border-border bg-muted",
};

// [UC-116] Trung tâm chưa nhận yêu cầu: giải thích thay cho dòng số tin nhắn
const AWAITING_CENTER_NOTE: Record<string, string> = {
  pending_parent: "Đã gửi email cho phụ huynh. Trung tâm chỉ nhận thông tin sau khi phụ huynh đồng ý.",
  cancelled: "Phụ huynh đã từ chối nên trung tâm không nhận thông tin. Bạn có thể gửi lại sau khi trao đổi với phụ huynh.",
  expired: "Yêu cầu đã hết hạn (quá thời hạn xác nhận hoặc trung tâm không phản hồi trong 7 ngày). Bạn có thể gửi lại.",
};

function errorMessage(err: unknown) {
  return getLeadRuleError(err)?.message ?? "Không tải được danh sách yêu cầu. Vui lòng thử lại sau.";
}

// [UC-44] Yêu cầu liên hệ đã gửi.
// Học sinh (từ 16 tuổi): yêu cầu của mình, kể cả yêu cầu phụ huynh gửi thay.
// Phụ huynh: yêu cầu mình gửi thay con và yêu cầu con đang liên kết tự gửi.
export function SentLeadsCard() {
  const { user } = useAuth();
  const isParent = user?.role === "parent";
  // Server chặn thật (403 age_restricted); ở đây chỉ để không gọi API và giải thích lý do.
  const ageBlocked = useAgeBlock(AGE_FEATURES.shareWithCenter) !== null && !isParent;

  const [leads, setLeads] = useState<SentLeadDto[]>([]);
  const [page, setPage] = useState(1);
  const [totalPages, setTotalPages] = useState(0);
  const [isLoading, setIsLoading] = useState(!ageBlocked);
  const [isLoadingMore, setIsLoadingMore] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const loadFirstPage = useCallback(
    () =>
      leadApi
        .listSent({ page: 1, pageSize: PAGE_SIZE })
        .then((res) => {
          setLeads(res.items);
          setPage(res.page);
          setTotalPages(res.totalPages);
        })
        .catch((err) => setError(errorMessage(err)))
        .finally(() => setIsLoading(false)),
    [],
  );

  useEffect(() => {
    if (!ageBlocked) loadFirstPage();
  }, [ageBlocked, loadFirstPage]);

  const loadMore = async () => {
    setError(null);
    setIsLoadingMore(true);
    try {
      const res = await leadApi.listSent({ page: page + 1, pageSize: PAGE_SIZE });
      // Bỏ trùng phòng khi có yêu cầu mới gửi làm lệch trang
      setLeads((prev) => [...prev, ...res.items.filter((item) => !prev.some((p) => p.id === item.id))]);
      setPage(res.page);
      setTotalPages(res.totalPages);
    } catch (err) {
      setError(errorMessage(err));
    } finally {
      setIsLoadingMore(false);
    }
  };

  // [Nhắn tin phía người gửi] Khung tin nhắn trả về trạng thái và số tin trung tâm mới nhất
  const updateLead = useCallback((leadId: string, change: SentLeadThreadChange) => {
    setLeads((prev) =>
      prev.map((l) =>
        l.id === leadId && (l.status !== change.status || l.centerMessageCount !== change.centerMessageCount)
          ? { ...l, status: change.status, centerMessageCount: change.centerMessageCount }
          : l,
      ),
    );
  }, []);

  // Khung chat nổi (hộp thư tin nhắn) báo trạng thái và số tin trung tâm mới nhất
  useEffect(() => {
    const onChange = (e: Event) => {
      const { leadId, ...change } = (e as CustomEvent<SentThreadChangedDetail>).detail;
      updateLead(leadId, change);
    };
    window.addEventListener(SENT_THREAD_CHANGED_EVENT, onChange);
    return () => window.removeEventListener(SENT_THREAD_CHANGED_EVENT, onChange);
  }, [updateLead]);

  if (!user || (user.role !== "student" && user.role !== "parent")) return null;

  return (
    <div>
      <AccountSectionHeader
        title="Yêu cầu liên hệ đã gửi"
        description={
          isParent
            ? "Yêu cầu bạn gửi thay con và yêu cầu con đã liên kết tự gửi tới trung tâm du học."
            : "Yêu cầu bạn gửi tới trung tâm du học, kể cả yêu cầu phụ huynh gửi thay bạn."
        }
        actions={
          !ageBlocked && leads.length > 0 ? (
            <Link href="/centers" className={buttonVariants({ variant: "outline", size: "lg", className: "bg-surface" })}>
              <Building2Icon data-icon="inline-start" aria-hidden />
              Tìm trung tâm
            </Link>
          ) : undefined
        }
      />

      <div className="space-y-4">
        {ageBlocked ? (
          <Empty className="py-16">
            <EmptyHeader>
              <EmptyTitle className="text-base font-semibold text-ink">
                Cần phụ huynh xác nhận đồng ý
              </EmptyTitle>
              <EmptyDescription>
                Học sinh dưới 16 tuổi cần phụ huynh đồng ý trước mới xem và gửi được yêu cầu liên hệ với trung tâm. Phụ huynh đã liên kết có thể xem bằng tài khoản phụ huynh.
              </EmptyDescription>
            </EmptyHeader>
            <EmptyContent>
              <Link href="#parent-links" className={buttonVariants({ variant: "outline", size: "lg", className: "bg-surface" })}>
                <UsersIcon data-icon="inline-start" aria-hidden />
                Xem liên kết phụ huynh
              </Link>
            </EmptyContent>
          </Empty>
        ) : (
          <>
            {error && (
              <Alert variant="destructive">
                <AlertDescription>{error}</AlertDescription>
              </Alert>
            )}

            {isLoading ? (
              <div className="space-y-3">
                <Skeleton className="h-28 w-full rounded-xl" />
                <Skeleton className="h-28 w-full rounded-xl" />
              </div>
            ) : leads.length === 0 ? (
              !error && (
                <Empty className="py-16">
                  <EmptyHeader>
                    <EmptyTitle className="text-base font-semibold text-ink">
                      Chưa có yêu cầu liên hệ nào
                    </EmptyTitle>
                    <EmptyDescription>
                      {isParent
                        ? "Bạn và con chưa gửi yêu cầu liên hệ nào tới trung tâm tư vấn du học."
                        : "Bạn chưa gửi yêu cầu liên hệ nào tới trung tâm tư vấn du học."}
                    </EmptyDescription>
                  </EmptyHeader>
                  <EmptyContent>
                    <Link href="/centers" className={buttonVariants({ size: "lg" })}>
                      <SearchIcon data-icon="inline-start" aria-hidden />
                      Tìm trung tâm du học
                    </Link>
                  </EmptyContent>
                </Empty>
              )
            ) : (
              <>
                <ul className="divide-y divide-line overflow-hidden rounded-xl border border-line bg-surface">
                  {leads.map((lead) => (
                    <li key={lead.id}>
                      <SentLeadItem lead={lead} isParent={isParent} />
                    </li>
                  ))}
                </ul>

                {page < totalPages && (
                  <div className="flex justify-center pt-2">
                    <Button
                      type="button"
                      variant="outline"
                      size="lg"
                      disabled={isLoadingMore}
                      onClick={loadMore}
                      loading={isLoadingMore}
                    >
                      Xem thêm
                    </Button>
                  </div>
                )}
              </>
            )}
          </>
        )}
      </div>
    </div>
  );
}

function SentLeadItem({
  lead,
  isParent,
}: {
  lead: SentLeadDto;
  isParent: boolean;
}) {
  const { openChat } = useChatDock();
  // [Hộp thư tin nhắn] Mở khung chat nổi ở góc dưới thay cho hộp thoại
  const openMessages = () =>
    openChat({
      leadId: lead.id,
      title: lead.studyCenterName,
      subtitle: isParent ? `Học sinh: ${lead.studentName}` : null,
      side: "sent",
      leadStatus: lead.status,
    });
  const statusClass = STATUS_BADGE_CLASS[lead.status];
  const awaitingCenter = isLeadAwaitingCenter(lead.status);
  const sentBy = isParent
    ? lead.submittedByParent
      ? "Phụ huynh gửi thay"
      : "Con tự gửi"
    : lead.submittedByParent
      ? "Phụ huynh gửi thay bạn"
      : null;

  return (
    <div className="space-y-3 px-5 py-5 sm:px-6">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="min-w-0 space-y-0.5">
          <Link
            href={`/centers/${lead.studyCenterId}`}
            className="text-base font-semibold text-ink transition-colors hover:text-brand hover:underline"
          >
            {lead.studyCenterName}
          </Link>
          <div className="text-xs text-ink-2">
            Gửi ngày {new Date(lead.createdAt).toLocaleDateString("vi-VN")}
            {isParent && <> · Con: {lead.studentName}</>}
            {sentBy && <> · {sentBy}</>}
          </div>
        </div>
        <Badge
          variant={lead.status === "new" || lead.status === "contacted" ? "default" : "outline"}
          className={cn("shrink-0 font-medium", statusClass)}
        >
          {leadStatusLabel(lead.status)}
        </Badge>
      </div>

      <div className="space-y-1 text-sm text-ink">
        <div>
          <span className="text-ink-2">Đã chia sẻ: </span>
          {lead.sharedFields.map((field, i) => {
            const info = LEAD_SHARED_FIELD_INFO[field];
            return (
              <React.Fragment key={field}>
                {i > 0 && " · "}
                {info ? `${info.label} (${info.description})` : field}
              </React.Fragment>
            );
          })}
        </div>

        {(lead.studyLevel || lead.contactPhone) && (
          <div className="text-xs text-ink-2">
            {lead.studyLevel && <>Bậc học: {studyLevelLabel(lead.studyLevel)}</>}
            {lead.studyLevel && lead.contactPhone && " · "}
            {lead.contactPhone && <>SĐT: {lead.contactPhone}</>}
          </div>
        )}
      </div>

      {lead.message && (
        <p
          className="line-clamp-2 rounded-lg bg-ground px-3.5 py-2.5 text-sm text-ink-2"
          title={lead.message}
        >
          “{lead.message}”
        </p>
      )}

      {awaitingCenter ? (
        <p className="text-xs text-ink-2">{AWAITING_CENTER_NOTE[lead.status]}</p>
      ) : (
        <div className="flex flex-wrap items-center justify-between gap-2 pt-1">
          <div className="flex items-center gap-1.5 text-xs text-ink-2">
            <MessageSquareIcon className="size-3.5 text-ink-3" aria-hidden />
            {lead.centerMessageCount > 0
              ? `Trung tâm đã nhắn ${lead.centerMessageCount} tin`
              : "Trung tâm chưa nhắn tin"}
          </div>
          <Button type="button" variant="outline" size="sm" onClick={openMessages}>
            <MessageSquareIcon data-icon="inline-start" aria-hidden />
            Tin nhắn
          </Button>
        </div>
      )}
    </div>
  );
}
