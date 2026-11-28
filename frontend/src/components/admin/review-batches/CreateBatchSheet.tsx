"use client";

import { useState } from "react";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Input } from "@/components/ui/input";
import { Sheet, SheetContent, SheetDescription, SheetFooter, SheetHeader, SheetTitle } from "@/components/ui/sheet";
import { ApiError } from "@/lib/api";
import { formatInt } from "@/lib/ai-usage-format";
import { useAuth } from "@/contexts/AuthContext";
import { adminReviewBatchApi } from "@/lib/review-batch-api";
import { stratumLabel } from "@/lib/review-batch-format";
import { useFetched } from "@/lib/use-fetched";
import type { ReviewBatchCreated } from "@/types/review-batch";

const DEFAULT_SIZE = 20;
const MAX_SIZE = 100;   // khớp ReviewBatchService.MaxSizePerStratum
const MAX_REVIEWERS = 5;   // khớp ReviewBatchService.MaxReviewers
const ROLE_LABEL: Record<string, string> = { admin: "Admin", moderator: "Moderator" };

interface Props {
  open: boolean;
  onClose: () => void;
  /** Gọi sau khi tạo thành công (để tải lại danh sách). */
  onCreated: () => void;
}

export function CreateBatchSheet({ open, onClose, onCreated }: Props) {
  return (
    <Sheet open={open} onOpenChange={(next) => { if (!next) onClose(); }}>
      <SheetContent side="right" className="w-full gap-0 overflow-hidden sm:max-w-lg">
        {/* key theo open để mỗi lần mở là một biểu mẫu mới, không giữ chữ của lần trước */}
        {open ? <Form onClose={onClose} onCreated={onCreated} /> : null}
      </SheetContent>
    </Sheet>
  );
}

function Form({ onClose, onCreated }: Omit<Props, "open">) {
  const [name, setName] = useState("");
  const [size, setSize] = useState(String(DEFAULT_SIZE));
  const [from, setFrom] = useState("");
  const [to, setTo] = useState("");
  const [seed, setSeed] = useState("");
  const [pending, setPending] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [created, setCreated] = useState<ReviewBatchCreated | null>(null);

  // Người duyệt: mặc định là chính người tạo; chọn lại thì lấy đúng danh sách đã chọn
  const { user } = useAuth();
  const reviewerOptions = useFetched("review-batch-reviewers", () => adminReviewBatchApi.reviewers());
  const [picked, setPicked] = useState<string[] | null>(null);
  const reviewerIds = picked ?? (user?.id ? [user.id] : []);
  const toggleReviewer = (id: string) =>
    setPicked(reviewerIds.includes(id) ? reviewerIds.filter((current) => current !== id) : [...reviewerIds, id]);

  const sizeNumber = Number(size);
  const problem =
    name.trim() === "" ? "Đặt tên cho đợt."
    : !Number.isInteger(sizeNumber) || sizeNumber < 1 || sizeNumber > MAX_SIZE ? `Số câu mỗi nhóm phải là số nguyên từ 1 đến ${MAX_SIZE}.`
    : from && to && from > to ? "Ngày bắt đầu phải trước hoặc bằng ngày kết thúc."
    : reviewerIds.length < 1 ? "Chọn ít nhất một người duyệt."
    : reviewerIds.length > MAX_REVIEWERS ? `Chọn tối đa ${MAX_REVIEWERS} người duyệt.`
    : null;

  const submit = async () => {
    if (problem) return;
    setPending(true);
    setError(null);
    try {
      const result = await adminReviewBatchApi.create({
        name: name.trim(),
        sizePerStratum: sizeNumber,
        from: from || null,
        to: to || null,
        seed: seed.trim() || null,
        reviewerIds,
      });
      setCreated(result);
      onCreated();
    } catch (cause) {
      setError(cause instanceof ApiError || cause instanceof Error ? cause.message : "Không tạo được đợt.");
    } finally {
      setPending(false);
    }
  };

  if (created) return <Done created={created} onClose={onClose} />;

  return (
    <div className="flex min-h-0 flex-1 flex-col">
      <div className="min-h-0 flex-1 space-y-4 overflow-y-auto p-4">
      <SheetHeader className="p-0">
        <SheetTitle>Tạo đợt duyệt mẫu</SheetTitle>
        <SheetDescription>
          Hệ thống bốc ngẫu nhiên tối đa số câu bạn chọn cho mỗi nhóm skill, chỉ từ các câu đã trả lời được. Những người được chọn
          bên dưới sẽ đọc và chấm các câu trong đợt.
        </SheetDescription>
      </SheetHeader>

      <label className="flex flex-col gap-1 text-sm text-ink">
        Tên đợt
        <Input value={name} maxLength={120} placeholder="Ví dụ: Đợt tuần 3 tháng 11" onChange={(event) => setName(event.target.value)} />
      </label>

      <label className="flex flex-col gap-1 text-sm text-ink">
        Số câu mỗi nhóm
        <Input type="number" min={1} max={MAX_SIZE} value={size} onChange={(event) => setSize(event.target.value)} className="w-32" />
        <span className="text-xs text-ink-3">
          Gợi ý 20 đến 30. Nhóm nào ít câu hơn thì lấy hết. Tổng số câu = số nhóm × số này (tối đa).
        </span>
      </label>

      <div className="flex flex-wrap gap-3">
        <label className="flex flex-col gap-1 text-sm text-ink">
          Từ ngày (UTC)
          <Input type="date" value={from} max={to || undefined} onChange={(event) => setFrom(event.target.value)} className="w-40" />
        </label>
        <label className="flex flex-col gap-1 text-sm text-ink">
          Đến ngày (UTC)
          <Input type="date" value={to} min={from || undefined} onChange={(event) => setTo(event.target.value)} className="w-40" />
        </label>
      </div>
      <p className="-mt-2 text-xs text-ink-3">Bỏ trống ngày là lấy toàn bộ thời gian.</p>

      <fieldset className="flex flex-col gap-2">
        <legend className="text-sm text-ink">Người duyệt ({reviewerIds.length}/{MAX_REVIEWERS})</legend>
        {reviewerOptions.error ? (
          <Alert variant="destructive"><AlertDescription>{reviewerOptions.error}</AlertDescription></Alert>
        ) : null}
        {!reviewerOptions.data && !reviewerOptions.error ? <p className="text-xs text-ink-3">Đang tải danh sách...</p> : null}
        <ul className="flex flex-col gap-1">
          {reviewerOptions.data?.map((option) => {
            const checked = reviewerIds.includes(option.id);
            return (
              <li key={option.id}>
                <label className="flex cursor-pointer items-start gap-2 rounded-md px-2 py-1.5 text-sm hover:bg-ground">
                  <Checkbox
                    className="mt-1"
                    checked={checked}
                    disabled={!checked && reviewerIds.length >= MAX_REVIEWERS}
                    onCheckedChange={() => toggleReviewer(option.id)}
                  />
                  <span className="flex flex-col">
                    <span className="text-ink">
                      {option.fullName}
                      {option.id === user?.id ? " (bạn)" : ""} · {ROLE_LABEL[option.role] ?? option.role}
                    </span>
                    <span className="text-xs text-ink-3">{option.email}</span>
                  </span>
                </label>
              </li>
            );
          })}
        </ul>
        <p className="text-xs text-ink-3">
          Chỉ người được chọn đọc được nội dung câu. Bạn tạo đợt mà không chọn chính mình thì chỉ xem được số đo.
        </p>
      </fieldset>

      <details className="rounded-md border border-line">
        <summary className="cursor-pointer px-3 py-2 text-sm font-medium text-ink">Nâng cao: seed</summary>
        <div className="flex flex-col gap-1 border-t border-line p-3">
          <Input value={seed} maxLength={64} placeholder="Bỏ trống để hệ thống tự sinh" onChange={(event) => setSeed(event.target.value)} />
          <span className="text-xs text-ink-3">
            Seed là chuỗi giúp bốc lại ra đúng mẫu đó. Đặt một lần, trước khi xem kết quả; đừng thử nhiều seed rồi chọn cái đẹp, vì như vậy mẫu không còn ngẫu nhiên.
          </span>
        </div>
      </details>

      {problem && (name !== "" || size !== String(DEFAULT_SIZE)) ? <p className="text-xs text-risk">{problem}</p> : null}
      {error ? <Alert variant="destructive"><AlertDescription>{error}</AlertDescription></Alert> : null}
      </div>

      <SheetFooter className="shrink-0 flex-row justify-end gap-2 border-t border-border bg-card px-4 py-3">
        <Button type="button" variant="outline" disabled={pending} onClick={onClose}>Hủy</Button>
        <Button type="button" disabled={pending || problem !== null} onClick={() => void submit()}>
          {pending ? "Đang bốc mẫu..." : "Tạo đợt"}
        </Button>
      </SheetFooter>
    </div>
  );
}

function Done({ created, onClose }: { created: ReviewBatchCreated; onClose: () => void }) {
  return (
    <div className="flex min-h-0 flex-1 flex-col">
      <div className="min-h-0 flex-1 space-y-4 overflow-y-auto p-4">
        <SheetHeader className="p-0">
          <SheetTitle>Đã tạo đợt</SheetTitle>
          <SheetDescription>
            {created.name}: {formatInt(created.totalItems)} câu. Seed <code className="rounded bg-ground px-1 text-[13px]">{created.seed}</code> đã được lưu.
          </SheetDescription>
        </SheetHeader>
        <ul className="flex flex-col gap-1 rounded-md bg-ground p-3 text-sm">
          {created.strata.map((stratum) => (
            <li key={stratum.stratum} className="flex justify-between gap-2">
              <span className="text-ink">{stratumLabel(stratum.stratum)}</span>
              <span className="tabular text-ink-2">{formatInt(stratum.picked)} / {formatInt(stratum.population)}</span>
            </li>
          ))}
        </ul>
        <p className="text-xs text-ink-3">Mỗi dòng: số câu được chọn / số câu có trong nhóm lúc bốc.</p>
      </div>
      <SheetFooter className="shrink-0 flex-row justify-end gap-2 border-t border-border bg-card px-4 py-3">
        <Button type="button" onClick={onClose}>Đóng</Button>
      </SheetFooter>
    </div>
  );
}
