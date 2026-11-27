"use client";

import { useEffect, useRef, useState } from "react";
import Link from "next/link";
import {
  AlertCircleIcon, CalendarIcon, CheckCircle2Icon, CircleIcon, FlagIcon, MapPinIcon, RouteIcon, StarIcon,
} from "lucide-react";
import { ContactRequestDialog } from "@/components/centers/ContactRequestDialog";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { NativeSelect, NativeSelectOption } from "@/components/ui/native-select";
import { Progress } from "@/components/ui/progress";
import { Spinner } from "@/components/ui/spinner";
import { useProvinces } from "@/hooks/use-provinces";
import { ApiError, dashboardApi } from "@/lib/api";
import { cn } from "@/lib/utils";
import type {
  Roadmap, RoadmapCenter, RoadmapCriterion, RoadmapMilestone, RoadmapSchool, RoadmapStatus, RoadmapTask,
} from "@/types/dashboard";

/** Neo để chat và các trang khác trỏ thẳng tới lộ trình: /dashboard#lo-trinh */
export const ROADMAP_ANCHOR = "lo-trinh";

const STATUS: Record<RoadmapStatus, { text: string; variant: "ok" | "warn" | "risk" | "neutral" }> = {
  met: { text: "Đạt", variant: "ok" },
  near: { text: "Gần đạt", variant: "warn" },
  below: { text: "Chưa đạt", variant: "risk" },
  unknown: { text: "Chưa đủ dữ liệu", variant: "neutral" },
};

const ENGLISH_NAMES: Record<string, string> = { ielts: "IELTS", toefl: "TOEFL iBT", duolingo: "Duolingo" };
const CATEGORY_NAMES: Record<string, string> = { reach: "Khó vào", match: "Vừa sức", safety: "An toàn hơn" };

const num = (n: number) => n.toLocaleString("en-US", { maximumFractionDigits: 2 });
const usd = (n: number) => `$${Math.round(n).toLocaleString("en-US")}`;

// "2027-01-15" -> 15/01/2027, đọc theo ngày lịch (không lệch múi giờ)
function parseDate(text: string) {
  const [y, m, d] = text.split("-").map(Number);
  return new Date(y, (m ?? 1) - 1, d ?? 1);
}
const dateText = (text: string) => parseDate(text).toLocaleDateString("vi-VN", { day: "2-digit", month: "2-digit", year: "numeric" });

function criterionText(c: RoadmapCriterion) {
  const show = (v: number | null | undefined, f: (n: number) => string = num) => (v != null ? f(v) : "?");
  switch (c.key) {
    case "gpa":
      return `GPA ${show(c.mine)} / mốc ${show(c.mark)}`;
    case "english":
      return `${ENGLISH_NAMES[c.type ?? ""] ?? "Tiếng Anh"} ${show(c.mine)} / mốc ${show(c.mark)}`;
    case "sat":
      return `SAT ${show(c.mine)} / mốc ${show(c.mark)}`;
    case "budget":
      return `Ngân sách ${show(c.mine, usd)}/năm · chi phí ${show(c.mark, usd)}/năm`;
  }
}

function SchoolCard({ school }: { school: RoadmapSchool }) {
  return (
    <div className="space-y-3 rounded-lg border border-line p-4">
      <div className="flex flex-wrap items-start justify-between gap-2">
        <div className="min-w-0">
          <p className="truncate font-semibold text-foreground">{school.name}</p>
          {school.category && CATEGORY_NAMES[school.category] && (
            <p className="text-xs text-muted-foreground">{CATEGORY_NAMES[school.category]}</p>
          )}
        </div>
        <span className="text-numeric text-sm text-muted-foreground">
          Đạt <span className="font-bold text-foreground">{school.metCount}</span>/{school.knownCount} tiêu chí có dữ liệu
        </span>
      </div>
      <ul className="space-y-1.5">
        {school.criteria.map((c) => (
          <li key={c.key} className="flex items-center justify-between gap-2 text-sm">
            <span className="text-numeric text-foreground">{criterionText(c)}</span>
            <Badge variant={STATUS[c.status].variant}>{STATUS[c.status].text}</Badge>
          </li>
        ))}
      </ul>
      {school.scholarships.length > 0 && (
        <div className="space-y-1 border-t border-line pt-2">
          <span className="block text-label">Học bổng</span>
          {school.scholarships.map((s) => (
            <div key={s.id} className="flex items-center justify-between gap-2 text-sm">
              <span className="min-w-0 truncate text-foreground">
                {s.name}
                {s.deadline && <span className="text-muted-foreground"> · hạn {dateText(s.deadline)}</span>}
              </span>
              <Badge variant={s.eligible ? "ok" : "neutral"}>{s.eligible ? "Đủ điều kiện" : "Chưa đủ"}</Badge>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

function Timeline({ milestones, today }: { milestones: RoadmapMilestone[]; today: Date }) {
  const sorted = [...milestones].sort((a, b) => a.date.localeCompare(b.date));
  // Vạch "Hôm nay" chèn trước mốc đầu tiên còn ở tương lai
  const firstFuture = sorted.findIndex((m) => parseDate(m.date) >= today);

  return (
    <ol className="relative space-y-3 border-l border-line pl-5">
      {sorted.map((m, i) => {
        const past = parseDate(m.date) < today;
        return (
          <li key={`${m.kind}-${m.date}-${i}`} className="relative">
            {i === firstFuture && (
              <div className="mb-3 -ml-5 flex items-center gap-2 text-xs font-semibold text-brand">
                <span className="h-px w-4 bg-brand" /> Hôm nay
              </div>
            )}
            <span
              className={cn(
                "absolute top-1.5 -left-[25px] size-2.5 rounded-full border-2",
                m.isEstimate ? "border-dashed border-muted-foreground bg-surface" : "border-brand bg-brand",
                past && "opacity-50",
              )}
              aria-hidden
            />
            <div className={cn("text-sm", past && "text-muted-foreground")}>
              <span className="text-numeric font-medium">{dateText(m.date)}</span>{" "}
              <span className={cn(m.isEstimate && "italic")}>{m.label}</span>
              {m.isEstimate && <span className="ml-1 text-xs text-muted-foreground">(tham khảo)</span>}
            </div>
          </li>
        );
      })}
      {firstFuture === -1 && (
        <li className="-ml-5 flex items-center gap-2 text-xs font-semibold text-brand">
          <span className="h-px w-4 bg-brand" /> Hôm nay
        </li>
      )}
    </ol>
  );
}

function CenterRow({ center, task }: { center: RoadmapCenter; task: RoadmapTask }) {
  return (
    <li className="flex flex-wrap items-center justify-between gap-2 rounded-md bg-ground px-3 py-2">
      <div className="min-w-0 space-y-0.5">
        <p className="truncate text-sm font-medium text-foreground">{center.name}</p>
        <p className="flex flex-wrap items-center gap-x-3 text-xs text-muted-foreground">
          {center.city && <span className="inline-flex items-center gap-1"><MapPinIcon className="size-3" aria-hidden />{center.city}</span>}
          {center.reviewCount > 0 ? (
            <span className="inline-flex items-center gap-1">
              <StarIcon className="size-3 fill-current text-warn" aria-hidden />
              {num(center.rating)} ({center.reviewCount} đánh giá)
            </span>
          ) : (
            <span>Chưa có đánh giá</span>
          )}
          {center.feeText && <span>Học phí tham khảo: {center.feeText}</span>}
        </p>
      </div>
      <div className="flex shrink-0 items-center gap-2">
        <Button variant="ghost" size="sm" nativeButton={false} render={<Link href={`/centers/${encodeURIComponent(center.id)}`} />}>
          Xem trung tâm
        </Button>
        <ContactRequestDialog
          centerId={center.id}
          centerName={center.name}
          isVerified={center.verified}
          compact
          defaultNote={`Mình cần hỗ trợ: ${task.label}.`}
        />
      </div>
    </li>
  );
}

function TaskItem({ task, provinceKnown }: { task: RoadmapTask; provinceKnown: boolean }) {
  const icon = !task.checkable
    ? <CalendarIcon className="size-4 text-muted-foreground" aria-label="Mốc, không tự tích" />
    : task.status === "done"
      ? <CheckCircle2Icon className="size-4 text-ok" aria-label="Đã xong" />
      : task.status === "overdue"
        ? <AlertCircleIcon className="size-4 text-risk" aria-label="Quá hạn" />
        : <CircleIcon className="size-4 text-muted-foreground" aria-label="Chưa xong" />;
  const noLocalCenter = provinceKnown && task.centers.length > 0 && !task.centers.some((c) => c.sameProvince);

  return (
    <li className="space-y-2 py-3">
      <div className="flex items-start gap-3">
        <span className="mt-0.5 shrink-0">{icon}</span>
        <div className="min-w-0 flex-1">
          <p className={cn("text-sm text-foreground", task.status === "done" && "text-muted-foreground line-through")}>
            {task.label}
          </p>
          <p className="text-xs text-muted-foreground">
            {task.due ? `Hạn ${dateText(task.due)}` : "Chưa có hạn"}
            {task.status === "overdue" && <span className="font-semibold text-risk"> · quá hạn</span>}
            {!task.checkable && " · chỉ là mốc, không tính vào tiến độ"}
          </p>
        </div>
      </div>
      {task.centers.length > 0 && (
        <div className="ml-7 space-y-1.5">
          <span className="block text-label">Trung tâm có thể giúp</span>
          {noLocalCenter && (
            <p className="text-xs text-muted-foreground">Chưa có trung tâm ở tỉnh bạn, đây là các trung tâm ở nơi khác.</p>
          )}
          <ul className="space-y-1.5">
            {task.centers.map((c) => <CenterRow key={c.id} center={c} task={task} />)}
          </ul>
        </div>
      )}
    </li>
  );
}

/**
 * Ô chọn tỉnh/thành để gợi ý trung tâm gần. Chưa có tỉnh: hỏi, có Bỏ qua (chỉ ẩn trong lần xem này).
 * Đã có tỉnh: mở từ nút Đổi, chọn sẵn tỉnh cũ, nút Hủy quay lại dòng hiện tỉnh.
 */
function ProvincePrompt({ current, onSaved, onSkip }: { current?: string | null; onSaved: () => void; onSkip: () => void }) {
  const provinces = useProvinces();
  const [value, setValue] = useState(current ?? "");
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const save = async () => {
    setSaving(true);
    setError(null);
    try {
      await dashboardApi.setProvince(value);
      onSaved();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Không lưu được, thử lại sau.");
      setSaving(false);
    }
  };

  return (
    <div className="flex flex-wrap items-center gap-2 rounded-lg border border-dashed border-line bg-ground p-3 text-sm">
      <MapPinIcon className="size-4 text-brand" aria-hidden />
      <label htmlFor="roadmap-province" className="text-foreground">
        {current ? "Đổi tỉnh/thành để gợi ý trung tâm gần bạn." : "Bạn ở tỉnh nào? Để gợi ý trung tâm gần bạn."}
      </label>
      <NativeSelect id="roadmap-province" size="sm" value={value} onChange={(e) => setValue(e.target.value)} disabled={saving}>
        <NativeSelectOption value="">Chọn tỉnh/thành</NativeSelectOption>
        {provinces.map((p) => <NativeSelectOption key={p} value={p}>{p}</NativeSelectOption>)}
      </NativeSelect>
      <Button type="button" size="sm" disabled={!value || value === current || saving} onClick={() => void save()}>
        {saving && <Spinner className="size-4" />} Lưu
      </Button>
      <Button type="button" size="sm" variant="ghost" disabled={saving} onClick={onSkip}>{current ? "Hủy" : "Bỏ qua"}</Button>
      {error && <p className="w-full text-xs text-risk">{error}</p>}
    </div>
  );
}

interface RoadmapSectionProps {
  /** Phụ huynh xem con: chỉ xem, lấy qua children/{id}/roadmap, không chọn tỉnh thay con */
  studentId?: string;
  /** Link tới danh sách trường mục tiêu (phụ huynh trỏ sang tab của con) */
  targetSchoolsHref?: string;
}

/** Lộ trình theo trường mục tiêu ở Tổng quan (docs/KE_HOACH_LO_TRINH_THEO_TRUONG_MUC_TIEU.md, bước B1–B2). */
export function RoadmapSection({ studentId, targetSchoolsHref = "/target-schools" }: RoadmapSectionProps) {
  const readOnly = !!studentId;
  const key = studentId ?? "me";
  const [state, setState] = useState<{ key: string; data?: Roadmap | null; error?: string } | null>(null);
  const [reload, setReload] = useState(0);
  const [skipProvince, setSkipProvince] = useState(false);
  const [editProvince, setEditProvince] = useState(false);
  const ref = useRef<HTMLElement>(null);

  useEffect(() => {
    let alive = true;
    (studentId ? dashboardApi.getChildRoadmap(studentId) : dashboardApi.getMyRoadmap())
      .then((data) => { if (alive) setState({ key, data }); })
      .catch((err: unknown) => {
        if (alive) setState({ key, error: err instanceof Error ? err.message : "Chưa tải được lộ trình." });
      });
    return () => { alive = false; };
  }, [studentId, key, reload]);

  const isLoading = state?.key !== key;
  const data = isLoading ? null : state?.data ?? null;
  const error = isLoading ? null : state?.error ?? null;

  // Nội dung tải sau nên trình duyệt không tự cuộn tới #lo-trinh: cuộn khi đã có lộ trình
  useEffect(() => {
    if (data && window.location.hash === `#${ROADMAP_ANCHOR}`) ref.current?.scrollIntoView({ behavior: "smooth", block: "start" });
  }, [data]);

  if (isLoading) {
    return (
      <div className="flex min-h-[10vh] items-center justify-center">
        <Spinner className="size-5 text-brand" />
      </div>
    );
  }
  if (error) {
    return (
      <div className="rounded-2xl border border-line bg-surface p-6 text-center text-sm text-muted-foreground">
        Chưa tải được lộ trình. {error}
      </div>
    );
  }
  // Chưa có hồ sơ: thẻ Tiến độ hồ sơ phía trên đã nhắc nhập hồ sơ
  if (!data) return null;

  const now = new Date();
  const today = new Date(now.getFullYear(), now.getMonth(), now.getDate());
  const pct = data.progress.total > 0 ? Math.round((data.progress.done / data.progress.total) * 100) : 0;

  return (
    <section id={ROADMAP_ANCHOR} ref={ref} className="scroll-mt-20 space-y-4">
      <div className="space-y-1">
        <h2 className="flex items-center gap-2 text-h3 text-foreground">
          <RouteIcon className="size-5 text-brand" aria-hidden />
          Lộ trình đến kỳ nhập học {data.intake}
          {data.intakeIsEstimate && <Badge variant="neutral">ước tính</Badge>}
        </h2>
        <p className="text-sm text-muted-foreground">{data.summary}</p>
        {data.mode === "average" && (
          <p className="text-sm text-muted-foreground">
            {readOnly ? "Con chưa chọn trường mục tiêu" : "Bạn chưa chọn trường mục tiêu"}, nên đang so với trung bình các trường và mốc thời gian chỉ để tham khảo.{" "}
            <Link href={targetSchoolsHref} className="font-semibold text-brand underline-offset-4 hover:underline">
              {readOnly ? "Xem trường mục tiêu" : "Chọn trường mục tiêu"}
            </Link>
          </p>
        )}
      </div>

      {!readOnly && data.provinceMissing && !skipProvince && (
        <ProvincePrompt onSaved={() => setReload((k) => k + 1)} onSkip={() => setSkipProvince(true)} />
      )}
      {!readOnly && !data.provinceMissing && (editProvince ? (
        <ProvincePrompt
          current={data.province}
          onSaved={() => { setEditProvince(false); setReload((k) => k + 1); }}
          onSkip={() => setEditProvince(false)}
        />
      ) : (
        <p className="flex items-center gap-2 text-sm text-muted-foreground">
          <MapPinIcon className="size-4 text-brand" aria-hidden />
          Gợi ý trung tâm theo tỉnh/thành: <span className="font-semibold text-foreground">{data.province}</span>
          <Button type="button" size="sm" variant="link" className="h-auto p-0" onClick={() => setEditProvince(true)}>Đổi</Button>
        </p>
      ))}

      <Card>
        <CardHeader className="border-b">
          <CardTitle className="flex items-center gap-2 text-base">
            <FlagIcon className="size-5 text-muted-foreground" aria-hidden /> Mức sẵn sàng theo từng trường
          </CardTitle>
          <CardDescription>
            So điểm thật trong hồ sơ với mốc của từng trường. Tiêu chí thiếu dữ liệu không tính là đạt hay chưa đạt.
          </CardDescription>
        </CardHeader>
        <CardContent className="grid grid-cols-1 gap-3 md:grid-cols-2">
          {data.schools.map((s) => <SchoolCard key={s.universityId ?? s.name} school={s} />)}
        </CardContent>
      </Card>

      <div className="grid grid-cols-1 gap-4 lg:grid-cols-5">
        <Card className="lg:col-span-2">
          <CardHeader className="border-b">
            <CardTitle className="flex items-center gap-2 text-base">
              <CalendarIcon className="size-5 text-muted-foreground" aria-hidden /> Trục thời gian
            </CardTitle>
            <CardDescription>Mốc chấm rỗng, chữ nghiêng là mốc ước tính để tham khảo.</CardDescription>
          </CardHeader>
          <CardContent>
            {data.milestones.length > 0
              ? <Timeline milestones={data.milestones} today={today} />
              : <p className="text-sm text-muted-foreground">Chưa có mốc nào.</p>}
          </CardContent>
        </Card>

        <Card className="lg:col-span-3">
          <CardHeader className="border-b">
            <CardTitle className="flex items-center gap-2 text-base">
              <CheckCircle2Icon className="size-5 text-muted-foreground" aria-hidden /> Nhiệm vụ
            </CardTitle>
            <CardDescription className="space-y-2">
              <span className="block">
                Tự tích khi hồ sơ có dữ liệu thật (điểm thi, ngày thi, trạng thái nộp). Liên hệ trung tâm không làm nhiệm vụ được tích.
              </span>
              <Progress value={pct}>
                <span className="text-sm text-foreground">
                  Tiến độ: <span className="text-numeric font-semibold">{data.progress.done}/{data.progress.total}</span> nhiệm vụ
                </span>
              </Progress>
            </CardDescription>
          </CardHeader>
          <CardContent>
            {data.tasks.length > 0 ? (
              <ul className="divide-y divide-line">
                {data.tasks.map((t) => <TaskItem key={t.id} task={t} provinceKnown={!data.provinceMissing} />)}
              </ul>
            ) : (
              <p className="text-sm text-muted-foreground">Chưa có nhiệm vụ nào.</p>
            )}
            {data.tasks.some((t) => t.centers.length > 0) && (
              <p className="mt-3 text-xs text-muted-foreground">
                Gợi ý theo dịch vụ và khu vực, không phải quảng cáo. Học phí chỉ để tham khảo, hỏi lại trung tâm trước khi đăng ký.
              </p>
            )}
          </CardContent>
        </Card>
      </div>
    </section>
  );
}
