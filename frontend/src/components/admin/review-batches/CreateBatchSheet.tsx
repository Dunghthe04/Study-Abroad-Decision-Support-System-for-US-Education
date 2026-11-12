"use client";

import { useState } from "react";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Sheet, SheetContent, SheetDescription, SheetHeader, SheetTitle } from "@/components/ui/sheet";
import { ApiError } from "@/lib/api";
import { formatInt } from "@/lib/ai-usage-format";
import { adminReviewBatchApi } from "@/lib/review-batch-api";
import { stratumLabel } from "@/lib/review-batch-format";
import type { ReviewBatchCreated } from "@/types/review-batch";

const DEFAULT_SIZE = 20;
const MAX_SIZE = 100;   // khớp ReviewBatchService.MaxSizePerStratum

interface Props {
  open: boolean;
  onClose: () => void;
  /** Gọi sau khi tạo thành công (để tải lại danh sách). */
  onCreated: () => void;
}

export function CreateBatchSheet({ open, onClose, onCreated }: Props) {
  return (
    <Sheet open={open} onOpenChange={(next) => { if (!next) onClose(); }}>
      <SheetContent side="right" className="w-full gap-0 overflow-y-auto sm:max-w-lg">
        <div className="flex flex-col gap-4 p-4">
          {/* key theo open để mỗi lần mở là một biểu mẫu mới, không giữ chữ của lần trước */}
          {open ? <Form onClose={onClose} onCreated={onCreated} /> : null}
        </div>
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

  const sizeNumber = Number(size);
  const problem =
    name.trim() === "" ? "Đặt tên cho đợt."
    : !Number.isInteger(sizeNumber) || sizeNumber < 1 || sizeNumber > MAX_SIZE ? `Số câu mỗi nhóm phải là số nguyên từ 1 đến ${MAX_SIZE}.`
    : from && to && from > to ? "Ngày bắt đầu phải trước hoặc bằng ngày kết thúc."
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
    <>
      <SheetHeader className="p-0">
        <SheetTitle>Tạo đợt duyệt mẫu</SheetTitle>
        <SheetDescription>
          Hệ thống bốc ngẫu nhiên tối đa số câu bạn chọn cho mỗi nhóm skill, chỉ từ các câu đã trả lời được. Bạn sẽ là người duyệt.
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

      <div className="flex gap-2">
        <Button type="button" disabled={pending || problem !== null} onClick={() => void submit()}>
          {pending ? "Đang bốc mẫu..." : "Tạo đợt"}
        </Button>
        <Button type="button" variant="outline" disabled={pending} onClick={onClose}>Hủy</Button>
      </div>
    </>
  );
}

function Done({ created, onClose }: { created: ReviewBatchCreated; onClose: () => void }) {
  return (
    <>
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
      <Button type="button" onClick={onClose}>Đóng</Button>
    </>
  );
}
