"use client";

// Chức năng 1 "Con của tôi": phụ huynh xem hồ sơ, gợi ý, trường mục tiêu, lịch sử và kịch bản của con. Chỉ đọc.

import { useEffect, type ReactNode } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { AcademicSummaryCard } from "@/components/account/AcademicSummaryCard";
import { RecommendationHistory, RunGroups } from "@/components/recommendation/RecommendationHistory";
import { scenarioOverrides, snapshotText, useSavedScenarios } from "@/components/recommendation/SavedScenarios";
import { Badge } from "@/components/ui/badge";
import { NativeSelect, NativeSelectOption } from "@/components/ui/native-select";
import { Spinner } from "@/components/ui/spinner";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { parentChildrenApi } from "@/lib/parent-children-api";
import { recommendationHistoryApi } from "@/lib/recommendation-history-api";
import { describeSimulation } from "@/lib/simulation-link";
import { useFetched } from "@/lib/use-fetched";
import type { TargetSchoolDto } from "@/types/api";
import type { ChildTab } from "@/lib/child-tabs";

const STATUS_LABEL: Record<string, string> = {
  considering: "Đang cân nhắc",
  applying: "Đang nộp",
  submitted: "Đã nộp",
  admitted: "Đỗ",
  rejected: "Trượt",
};

// Cùng tên nhóm với trang Gợi ý trường
const CATEGORY: Record<string, { label: string; variant: "reach" | "warn" | "ok" }> = {
  reach: { label: "Khó vào", variant: "reach" },
  match: { label: "Vừa sức", variant: "warn" },
  safety: { label: "An toàn", variant: "ok" },
};

const formatTime = (iso: string) =>
  new Date(iso).toLocaleString("vi-VN", { dateStyle: "short", timeStyle: "short" });

const LINK = "font-semibold text-brand underline-offset-4 hover:underline";

/** /parent/children: có con thì mở con đầu tiên (giữ ?tab= khi được chuyển từ trang hồ sơ riêng), chưa liên kết thì chỉ sang Tài khoản. */
export function ParentChildrenHome({ tab = "overview" }: { tab?: ChildTab }) {
  const router = useRouter();
  const { data: children, error, loading } = useFetched("parent-children", parentChildrenApi.listChildren);
  const firstId = children?.[0]?.studentUserId;

  useEffect(() => {
    if (firstId) {
      const query = tab === "overview" ? "" : `?tab=${tab}`;
      router.replace(`/parent/children/${encodeURIComponent(firstId)}${query}`);
    }
  }, [firstId, router, tab]);

  if (loading || firstId) return <Loading />;
  return (
    <Page>
      {error ? (
        <Message>Chưa tải được danh sách con. {error}</Message>
      ) : (
        <Message>
          Bạn chưa liên kết với con nào. Con mời email của bạn ở mục Tài khoản của con, bạn chấp nhận lời mời là xem
          được. <Link href="/account" className={LINK}>Mở Tài khoản</Link>
        </Message>
      )}
    </Page>
  );
}

/** /parent/children/[studentId]: 4 tab dữ liệu của một con. */
export function ChildDashboard({ studentId, initialTab = "overview" }: { studentId: string; initialTab?: ChildTab }) {
  const router = useRouter();
  const { data: children, error, loading } = useFetched("parent-children", parentChildrenApi.listChildren);

  if (loading) return <Loading />;
  if (error) {
    return (
      <Page>
        <Message>Chưa tải được danh sách con. {error}</Message>
      </Page>
    );
  }

  const list = children ?? [];
  const child = list.find((c) => c.studentUserId === studentId);
  if (!child) {
    return (
      <Page>
        <Message>
          Không tìm thấy con này, hoặc liên kết đã bị hủy.{" "}
          <Link href="/parent/children" className={LINK}>Về Con của tôi</Link>
        </Message>
      </Page>
    );
  }

  return (
    <Page>
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div className="space-y-1">
          <h1 className="text-h1 text-foreground">Con của tôi</h1>
          <p className="text-sm text-muted-foreground">Chỉ xem. Hồ sơ và trường mục tiêu do con tự nhập và sửa.</p>
        </div>
        {list.length > 1 && (
          <NativeSelect
            className="w-56"
            value={studentId}
            aria-label="Chọn con"
            onChange={(e) => router.push(`/parent/children/${encodeURIComponent(e.target.value)}`)}
          >
            {list.map((c) => (
              <NativeSelectOption key={c.studentUserId} value={c.studentUserId}>
                {c.studentName}
              </NativeSelectOption>
            ))}
          </NativeSelect>
        )}
      </div>

      {/* key: đổi con thì về lại tab đầu, không giữ dữ liệu của con trước */}
      <Tabs key={studentId} defaultValue={initialTab} className="space-y-4">
        <TabsList className="h-auto flex-wrap">
          <TabsTrigger value="overview">Tổng quan</TabsTrigger>
          <TabsTrigger value="recommend">Trường gợi ý</TabsTrigger>
          <TabsTrigger value="targets">Trường mục tiêu</TabsTrigger>
          <TabsTrigger value="history">Lịch sử và kịch bản</TabsTrigger>
        </TabsList>
        <TabsContent value="overview">
          <AcademicSummaryCard profile={child.profile} isLoading={false} readOnly title={`Hồ sơ của ${child.studentName}`} />
        </TabsContent>
        <TabsContent value="recommend">
          <LatestRecommendation studentId={studentId} />
        </TabsContent>
        <TabsContent value="targets">
          <TargetSchoolList studentId={studentId} />
        </TabsContent>
        <TabsContent value="history" className="space-y-8">
          <RecommendationHistory studentId={studentId} />
          <ScenarioList studentId={studentId} />
        </TabsContent>
      </Tabs>
    </Page>
  );
}

// Lần gợi ý mới nhất của con. Các lần cũ hơn xem ở tab Lịch sử.
function LatestRecommendation({ studentId }: { studentId: string }) {
  const { data: rec, error, loading } = useFetched(`parent-latest-rec-${studentId}`, async () => {
    const runs = await recommendationHistoryApi.list(studentId);
    return runs.length > 0 ? recommendationHistoryApi.get(runs[0].id) : null;
  });

  if (loading) return <Loading />;
  if (error) return <Message>Chưa tải được gợi ý của con. {error}</Message>;
  if (!rec) return <Message>Con chưa nhận gợi ý trường.</Message>;
  return (
    <div className="space-y-4">
      <p className="text-sm text-muted-foreground">Lần gợi ý mới nhất, lúc {formatTime(rec.createdAt)}.</p>
      <RunGroups rec={rec} />
    </div>
  );
}

function TargetSchoolList({ studentId }: { studentId: string }) {
  const { data, error, loading } = useFetched(`parent-targets-${studentId}`, () =>
    parentChildrenApi.targetSchools(studentId),
  );

  if (loading) return <Loading />;
  if (error) return <Message>Chưa tải được trường mục tiêu của con. {error}</Message>;
  if (!data || data.length === 0) return <Message>Con chưa thêm trường mục tiêu.</Message>;
  return (
    <ul className="divide-y divide-line overflow-hidden rounded-2xl border border-line bg-surface">
      {data.map((t) => (
        <TargetRow key={t.id} target={t} />
      ))}
    </ul>
  );
}

function TargetRow({ target: t }: { target: TargetSchoolDto }) {
  const category = t.category ? CATEGORY[t.category] : undefined;
  const place = [t.city, t.state].filter(Boolean).join(", ");
  return (
    <li className="space-y-1 p-4">
      <div className="flex flex-wrap items-center gap-2">
        <span className="text-sm font-semibold text-foreground">{t.universityName}</span>
        {category && <Badge variant={category.variant}>{category.label}</Badge>}
        <Badge variant="neutral">{STATUS_LABEL[t.status] ?? t.status}</Badge>
      </div>
      <p className="text-xs text-muted-foreground">
        {place || "Chưa rõ địa điểm"}
        {t.deadline && ` · Hạn nộp ${new Date(t.deadline).toLocaleDateString("vi-VN")}`}
      </p>
    </li>
  );
}

// Kịch bản con đã lưu, chỉ xem. Không có nút Mở vì Mở tính lại theo hồ sơ của người đang đăng nhập.
function ScenarioList({ studentId }: { studentId: string }) {
  const { items, error } = useSavedScenarios(true, studentId);

  return (
    <section className="space-y-3">
      <h2 className="text-h3 text-foreground">Kịch bản con đã lưu</h2>
      {error ? (
        <Message>{error}</Message>
      ) : items.length === 0 ? (
        <Message>Con chưa lưu kịch bản nào.</Message>
      ) : (
        <ul className="divide-y divide-line overflow-hidden rounded-2xl border border-line bg-surface">
          {items.map((s) => {
            const snapshot = snapshotText(s.baseline, s.simulated);
            return (
              <li key={s.id} className="space-y-1 p-4 text-sm">
                <p className="font-semibold text-foreground">{s.title}</p>
                <p className="text-xs text-muted-foreground">Lưu lúc {formatTime(s.createdAt)}</p>
                <p className="text-xs text-foreground">{describeSimulation(scenarioOverrides(s))}</p>
                {snapshot && <p className="text-xs text-muted-foreground">{snapshot}</p>}
              </li>
            );
          })}
        </ul>
      )}
    </section>
  );
}

function Page({ children }: { children: ReactNode }) {
  return <div className="mx-auto max-w-5xl space-y-6 px-4 py-6 lg:px-8">{children}</div>;
}

function Loading() {
  return (
    <div className="flex min-h-[30vh] items-center justify-center">
      <Spinner className="size-6 text-brand" />
    </div>
  );
}

function Message({ children }: { children: ReactNode }) {
  return (
    <div className="rounded-2xl border border-line bg-surface p-8 text-center text-sm text-muted-foreground">
      {children}
    </div>
  );
}
