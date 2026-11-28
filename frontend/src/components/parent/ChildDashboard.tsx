"use client";

// Chức năng 1 "Con của tôi": phụ huynh xem hồ sơ, gợi ý, trường mục tiêu, lịch sử và kịch bản của con. Chỉ đọc.

import { useEffect, useState, type ReactNode } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { Trash2Icon, UnlinkIcon } from "lucide-react";
import { RevokeAndDeleteChildDialog, RevokeConsentDialog } from "@/components/account/ParentConsentDialogs";
import { AcademicSummaryCard } from "@/components/account/AcademicSummaryCard";
import { RecommendationHistory, RunGroups } from "@/components/recommendation/RecommendationHistory";
import { scenarioOverrides, snapshotText, useSavedScenarios } from "@/components/recommendation/SavedScenarios";
import { ActAsChildButton, AddManagedChildButton } from "@/components/parent/ManagedChildActions";
import { SuggestTargetSchool } from "@/components/parent/SuggestTargetSchool";
import { ParentPreferencePanel } from "@/components/parent/ParentPreferencePanel";
import { ParentDashboardKpiSection } from "@/components/parent/ParentDashboardKpiSection";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Spinner } from "@/components/ui/spinner";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { toast } from "@/components/ui/toast";
import { ApiError, parentLinksApi } from "@/lib/api";
import { parentChildrenApi } from "@/lib/parent-children-api";
import { recommendationHistoryApi } from "@/lib/recommendation-history-api";
import { describeSimulation } from "@/lib/simulation-link";
import { useFetched } from "@/lib/use-fetched";
import type { ParentChildProfileDto, TargetSchoolDto } from "@/types/api";
import type { ChildTab } from "@/lib/child-tabs";

const STATUS_LABEL: Record<string, string> = {
  considering: "Đang cân nhắc",
  applying: "Đang nộp",
  submitted: "Đã nộp",
  admitted: "Đỗ",
  rejected: "Trượt",
  parent_suggested: "Chờ con xem",
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

// Link sang trang một con, giữ thẻ đang cần (vd. đi từ /target-schools thì mở thẻ Trường mục tiêu)
const childHref = (studentId: string, tab: ChildTab) =>
  `/parent/children/${encodeURIComponent(studentId)}${tab === "overview" ? "" : `?tab=${tab}`}`;

/** /parent/children: 1 con thì mở thẳng con đó, từ 2 con thì cho chọn con, chưa liên kết thì chỉ sang Tài khoản.
 *  Giữ ?tab= khi được chuyển từ trang hồ sơ riêng. */
export function ParentChildrenHome({ tab = "overview" }: { tab?: ChildTab }) {
  const router = useRouter();
  const { data: children, error, loading } = useFetched("parent-children", parentChildrenApi.listChildren);
  const onlyId = children?.length === 1 ? children[0].studentUserId : undefined;

  useEffect(() => {
    if (onlyId) router.replace(childHref(onlyId, tab));
  }, [onlyId, router, tab]);

  if (loading || onlyId) return <Loading />;
  return (
    <Page>
      {error ? (
        <Message>Chưa tải được danh sách con. {error}</Message>
      ) : children && children.length > 1 ? (
        <ChildPicker items={children} tab={tab} />
      ) : (
        <Message>
          Bạn chưa liên kết với con nào. Con mời email của bạn ở mục Tài khoản của con, bạn chấp nhận lời mời là xem
          được. <Link href="/account" className={LINK}>Mở Tài khoản</Link>
          <span className="mt-4 block">Con chưa có tài khoản? Bạn thêm con và nhập hồ sơ thay con.</span>
          <span className="mt-3 flex justify-center">
            <AddManagedChildButton variant="default" />
          </span>
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
          <p className="text-sm text-muted-foreground">
            {child.isManaged
              ? `${child.studentName} chưa có tài khoản. Bạn nhập và sửa hồ sơ thay con.`
              : "Chỉ xem. Hồ sơ và trường mục tiêu do con tự nhập và sửa."}
          </p>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          {child.isManaged && <ActAsChildButton childId={child.studentUserId} childName={child.studentName} />}
          {!child.isManaged && <ChildConsentActions studentId={child.studentUserId} childName={child.studentName} />}
          <AddManagedChildButton />
          {list.length > 1 && (
            <Select
              value={studentId}
              onValueChange={(v) => {
                if (v !== null) router.push(`/parent/children/${encodeURIComponent(v)}`);
              }}
              items={list.map((c) => ({ value: c.studentUserId, label: c.studentName }))}
            >
              <SelectTrigger className="w-56 bg-surface" aria-label="Chọn con">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {list.map((c) => (
                  <SelectItem key={c.studentUserId} value={c.studentUserId}>
                    {c.studentName}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          )}
        </div>
      </div>

      {/* key: đổi con thì về lại tab đầu, không giữ dữ liệu của con trước;
          link ?tab=... trong cùng trang (vd. thẻ Tổng quan UC-94) cũng phải mở đúng tab */}
      <Tabs key={`${studentId}:${initialTab}`} defaultValue={initialTab} className="space-y-4">
        <TabsList className="h-auto flex-wrap">
          <TabsTrigger value="overview">Tổng quan</TabsTrigger>
          <TabsTrigger value="recommend">Trường gợi ý</TabsTrigger>
          <TabsTrigger value="targets">Trường mục tiêu</TabsTrigger>
          <TabsTrigger value="history">Lịch sử và kịch bản</TabsTrigger>
          {!child.isManaged && <TabsTrigger value="preference">So sánh ưu tiên</TabsTrigger>}
        </TabsList>
        <TabsContent value="overview" className="space-y-6">
          <ParentDashboardKpiSection studentId={studentId} childName={child.studentName} />
          <AcademicSummaryCard profile={child.profile} isLoading={false} readOnly title={`Hồ sơ của ${child.studentName}`} />
        </TabsContent>
        <TabsContent value="recommend">
          <LatestRecommendation studentId={studentId} />
        </TabsContent>
        <TabsContent value="targets">
          <TargetSchoolList studentId={studentId} childName={child.studentName} />
        </TabsContent>
        <TabsContent value="history" className="space-y-8">
          <RecommendationHistory studentId={studentId} />
          <ScenarioList studentId={studentId} />
        </TabsContent>
        {!child.isManaged && (
          <TabsContent value="preference">
            <ParentPreferencePanel studentId={studentId} />
          </TabsContent>
        )}
      </Tabs>
    </Page>
  );
}

// [UC-111 1a, UC-75 1a] Phụ huynh rút đồng ý ngay tại Con của tôi (giống Tài khoản > Liên kết) khi con dưới 16 và liên kết
// đang hoạt động. Rút xong con không còn trong danh sách nên quay về Con của tôi.
function ChildConsentActions({ studentId, childName }: { studentId: string; childName: string }) {
  const router = useRouter();
  const { data: links } = useFetched(`parent-links-${studentId}`, async () => (await parentLinksApi.getMine()).data);
  const [dialog, setDialog] = useState<"revoke" | "delete" | null>(null);
  const [busy, setBusy] = useState(false);

  const link = links?.find((l) => l.studentUserId === studentId && l.status === "active");
  if (!link || (link.studentAge != null && link.studentAge >= 16)) return null;

  const run = async (action: () => Promise<{ message: string | null }>, fallback: string) => {
    setDialog(null);
    setBusy(true);
    try {
      const res = await action();
      toast.add({ type: "success", title: res.message || "Đã cập nhật." });
      router.push("/parent/children");
    } catch (err) {
      toast.add({ type: "error", title: fallback, description: err instanceof ApiError ? err.message : "Vui lòng thử lại." });
      setBusy(false);
    }
  };

  return (
    <>
      {link.canRevoke && (
        <Button
          type="button"
          variant="ghost"
          className="text-risk hover:bg-risk-wash hover:text-risk"
          disabled={busy}
          onClick={() => setDialog("revoke")}
        >
          <UnlinkIcon data-icon="inline-start" aria-hidden />
          Rút đồng ý
        </Button>
      )}
      <Button
        type="button"
        variant="ghost"
        className="text-risk hover:bg-risk-wash hover:text-risk"
        disabled={busy}
        onClick={() => setDialog("delete")}
      >
        <Trash2Icon data-icon="inline-start" aria-hidden />
        Rút đồng ý và xóa tài khoản
      </Button>
      <RevokeConsentDialog
        open={dialog === "revoke"}
        onCancel={() => setDialog(null)}
        onConfirm={() => run(() => parentLinksApi.revoke(link.id), "Không thể rút đồng ý.")}
      />
      <RevokeAndDeleteChildDialog
        open={dialog === "delete"}
        childName={childName}
        onCancel={() => setDialog(null)}
        onConfirm={() => run(() => parentLinksApi.revokeAndDelete(link.id), "Không thể xóa tài khoản của con.")}
      />
    </>
  );
}

// Phụ huynh có từ 2 con: chọn con muốn xem
function ChildPicker({ items, tab }: { items: ParentChildProfileDto[]; tab: ChildTab }) {
  return (
    <>
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div className="space-y-1">
          <h1 className="text-h1 text-foreground">Con của tôi</h1>
          <p className="text-sm text-muted-foreground">Bạn muốn xem con nào?</p>
        </div>
        <AddManagedChildButton />
      </div>
      <ul className="grid gap-4 sm:grid-cols-2">
        {items.map((c) => (
          <li key={c.studentUserId} className="space-y-2">
            <Link
              href={childHref(c.studentUserId, tab)}
              className="block space-y-2 rounded-2xl border border-line bg-surface p-5 transition-colors hover:border-brand"
            >
              <p className="text-h3 text-foreground">{c.studentName}</p>
              <Badge variant={c.profile.isComplete ? "ok" : "neutral"}>
                {c.profile.isComplete ? "Đã gửi hồ sơ" : "Chưa gửi hồ sơ"}
              </Badge>
              {c.isManaged && <p className="text-xs text-muted-foreground">Chưa có tài khoản, bạn nhập hồ sơ thay con</p>}
              {c.profile.intendedMajor && (
                <p className="text-xs text-muted-foreground">Ngành dự định: {c.profile.intendedMajor}</p>
              )}
            </Link>
            {c.isManaged && <ActAsChildButton childId={c.studentUserId} childName={c.studentName} className="w-full" />}
          </li>
        ))}
      </ul>
    </>
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

// Trường mục tiêu của con + gợi ý của phụ huynh. Dòng parent_suggested là gợi ý con chưa xem, phụ huynh rút lại được.
function TargetSchoolList({ studentId, childName }: { studentId: string; childName: string }) {
  // Tăng version để tải lại sau khi gợi ý / rút lại
  const [version, setVersion] = useState(0);
  const reload = () => setVersion((v) => v + 1);
  const { data, error, loading } = useFetched(`parent-targets-${studentId}-${version}`, () =>
    parentChildrenApi.targetSchools(studentId),
  );
  const [withdrawing, setWithdrawing] = useState<string | null>(null);

  const withdraw = async (t: TargetSchoolDto) => {
    setWithdrawing(t.id);
    try {
      await parentChildrenApi.withdrawSuggestion(studentId, t.id);
      toast.add({ type: "success", title: "Đã rút lại gợi ý", description: t.universityName });
    } catch (err) {
      toast.add({
        type: "error",
        title: "Chưa rút lại được",
        description: err instanceof ApiError ? err.message : "Vui lòng thử lại.",
      });
    } finally {
      setWithdrawing(null);
      reload();
    }
  };

  const suggestions = (data ?? []).filter((t) => t.status === "parent_suggested");
  const targets = (data ?? []).filter((t) => t.status !== "parent_suggested");

  return (
    <div className="space-y-6">
      <SuggestTargetSchool studentId={studentId} childName={childName} onSuggested={reload} />

      {/* Chỉ hiện "đang tải" lần đầu; tải lại thì giữ danh sách cũ để khung không nhảy */}
      {loading && !data ? (
        <Loading />
      ) : error && !data ? (
        <Message>Chưa tải được trường mục tiêu của con. {error}</Message>
      ) : (
        <>
          {suggestions.length > 0 && (
            <section className="space-y-2">
              <h3 className="text-sm font-semibold text-foreground">Bạn đã gợi ý, đang chờ con xem</h3>
              <ul className="divide-y divide-line overflow-hidden rounded-2xl border border-line bg-surface">
                {suggestions.map((t) => (
                  <TargetRow key={t.id} target={t}>
                    <Button
                      variant="outline"
                      size="sm"
                      disabled={withdrawing === t.id}
                      onClick={() => withdraw(t)}
                      loading={withdrawing === t.id}
                    >
                      Rút lại
                    </Button>
                  </TargetRow>
                ))}
              </ul>
            </section>
          )}
          <section className="space-y-2">
            <h3 className="text-sm font-semibold text-foreground">Danh sách của con</h3>
            {targets.length === 0 ? (
              <Message>Con chưa thêm trường mục tiêu.</Message>
            ) : (
              <ul className="divide-y divide-line overflow-hidden rounded-2xl border border-line bg-surface">
                {targets.map((t) => (
                  <TargetRow key={t.id} target={t} />
                ))}
              </ul>
            )}
          </section>
        </>
      )}
    </div>
  );
}

function TargetRow({ target: t, children }: { target: TargetSchoolDto; children?: ReactNode }) {
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
      {/* Gợi ý: Notes là lời nhắn của phụ huynh */}
      {t.status === "parent_suggested" && t.notes && (
        <p className="text-xs text-ink-2">Lời nhắn: {t.notes}</p>
      )}
      {children && <div className="pt-1">{children}</div>}
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
