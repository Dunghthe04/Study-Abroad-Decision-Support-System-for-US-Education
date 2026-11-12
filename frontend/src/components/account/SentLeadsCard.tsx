"use client";

import React, { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { ArrowRightIcon, MessageSquareIcon, SendIcon } from "lucide-react";
import { useAuth } from "@/contexts/AuthContext";
import { AGE_FEATURES, useAgeBlock } from "@/lib/age-features";
import { getLeadRuleError, leadApi } from "@/lib/lead-api";
import { studyLevelLabel } from "@/lib/study-levels";
import { cn } from "@/lib/utils";
import { LEAD_SHARED_FIELD_INFO, leadStatusLabel, type SentLeadDto } from "@/types/lead";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button, buttonVariants } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Spinner } from "@/components/ui/spinner";
import { SentLeadMessagesDialog, type SentLeadThreadChange } from "@/components/account/SentLeadMessagesDialog";

const PAGE_SIZE = 10;

// Màu giống badge trạng thái trong bảng lead phía trung tâm (UC-73, CenterLeadsListView).
const STATUS_BADGE_CLASS: Record<string, string> = {
  new: "bg-blue-600 text-white",
  contacted: "bg-amber-600 text-white",
  closed: "text-emerald-700 border-emerald-300 bg-emerald-50 dark:bg-emerald-950/40",
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

  if (!user || (user.role !== "student" && user.role !== "parent")) return null;

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <SendIcon className="size-5" aria-hidden />
          <h2>Yêu cầu liên hệ đã gửi</h2>
        </CardTitle>
        <CardDescription>
          {isParent
            ? "Yêu cầu bạn gửi thay con và yêu cầu con đã liên kết tự gửi tới trung tâm du học."
            : "Yêu cầu bạn gửi tới trung tâm du học, kể cả yêu cầu phụ huynh gửi thay bạn."}
        </CardDescription>
      </CardHeader>

      <CardContent className="space-y-4">
        {ageBlocked ? (
          <Alert>
            <AlertDescription>
              Học sinh dưới 16 tuổi không xem được yêu cầu liên hệ với trung tâm. Phụ huynh đã liên kết có thể xem bằng tài khoản phụ huynh.
            </AlertDescription>
          </Alert>
        ) : (
          <>
            {error && (
              <Alert variant="destructive">
                <AlertDescription>{error}</AlertDescription>
              </Alert>
            )}

            {isLoading ? (
              <div className="flex items-center gap-2 text-sm text-muted-foreground">
                <Spinner />
                Đang tải...
              </div>
            ) : leads.length === 0 ? (
              !error && (
                <div className="space-y-3">
                  <p className="text-sm italic text-muted-foreground">
                    {isParent ? "Bạn và con chưa gửi yêu cầu liên hệ nào." : "Bạn chưa gửi yêu cầu liên hệ nào."}
                  </p>
                  <Link href="/centers" className={buttonVariants({ variant: "outline", size: "sm" })}>
                    <span>Tìm trung tâm du học</span>
                    <ArrowRightIcon aria-hidden />
                  </Link>
                </div>
              )
            ) : (
              <>
                <ul className="space-y-3">
                  {leads.map((lead) => (
                    <li key={lead.id}>
                      <SentLeadItem lead={lead} isParent={isParent} onThreadChange={updateLead} />
                    </li>
                  ))}
                </ul>

                {page < totalPages && (
                  <Button type="button" variant="outline" size="sm" disabled={isLoadingMore} onClick={loadMore}>
                    {isLoadingMore && <Spinner data-icon="inline-start" />}
                    Xem thêm
                  </Button>
                )}
              </>
            )}
          </>
        )}
      </CardContent>
    </Card>
  );
}

function SentLeadItem({
  lead,
  isParent,
  onThreadChange,
}: {
  lead: SentLeadDto;
  isParent: boolean;
  onThreadChange: (leadId: string, change: SentLeadThreadChange) => void;
}) {
  const [messagesOpen, setMessagesOpen] = useState(false);
  const handleThreadChange = useCallback(
    (change: SentLeadThreadChange) => onThreadChange(lead.id, change),
    [lead.id, onThreadChange],
  );
  const statusClass = STATUS_BADGE_CLASS[lead.status];
  const sentBy = isParent
    ? lead.submittedByParent
      ? "Phụ huynh gửi thay"
      : "Con tự gửi"
    : lead.submittedByParent
      ? "Phụ huynh gửi thay bạn"
      : null;

  return (
    <Card size="sm">
      <CardContent className="space-y-2">
        <div className="flex flex-wrap items-start justify-between gap-2">
          <div className="min-w-0">
            <Link href={`/centers/${lead.studyCenterId}`} className="font-semibold hover:underline">
              {lead.studyCenterName}
            </Link>
            <div className="text-body-s">
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

        <div className="text-sm">
          <span className="text-muted-foreground">Đã chia sẻ: </span>
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
          <div className="text-sm text-muted-foreground">
            {lead.studyLevel && <>Bậc học: {studyLevelLabel(lead.studyLevel)}</>}
            {lead.studyLevel && lead.contactPhone && " · "}
            {lead.contactPhone && <>SĐT: {lead.contactPhone}</>}
          </div>
        )}

        {lead.message && (
          <p className="line-clamp-2 text-sm italic text-muted-foreground" title={lead.message}>
            “{lead.message}”
          </p>
        )}

        <div className="flex flex-wrap items-center justify-between gap-2">
          <div className="flex items-center gap-1.5 text-body-s">
            <MessageSquareIcon className="size-3.5" aria-hidden />
            {lead.centerMessageCount > 0
              ? `Trung tâm đã nhắn ${lead.centerMessageCount} tin`
              : "Trung tâm chưa nhắn tin"}
          </div>
          <Button type="button" variant="outline" size="sm" onClick={() => setMessagesOpen(true)}>
            <MessageSquareIcon data-icon="inline-start" aria-hidden />
            Tin nhắn
          </Button>
        </div>
      </CardContent>

      <SentLeadMessagesDialog
        lead={lead}
        open={messagesOpen}
        onOpenChange={setMessagesOpen}
        onThreadChange={handleThreadChange}
      />
    </Card>
  );
}
