"use client";

import { useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import {
  ArrowLeftIcon,
  Building2Icon,
  CheckIcon,
  MinusIcon,
  PlusIcon,
  RepeatIcon,
  SearchIcon,
  StarIcon,
  XIcon,
} from "lucide-react";
import type { StudyCenter } from "@/types/api";
import { getEnrichedCenterProfile } from "@/lib/center-details";
import { CENTER_SERVICES, centerServiceLabel } from "@/lib/center-services";
import { COMPARE_MAX, COMPARE_MIN, compareHref } from "@/lib/center-compare";
import { formatRating } from "@/lib/rating";
import { Button, buttonVariants } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Input } from "@/components/ui/input";
import { Popover, PopoverContent, PopoverTrigger } from "@/components/ui/popover";
import { cn } from "@/lib/utils";
import { CenterTypeBadge } from "./CenterBadges";

interface Props {
  selected: StudyCenter[];
  all: StudyCenter[];
}

const MISSING = "Chưa cập nhật";

interface InfoRow {
  label: string;
  /** Chuỗi để so sánh giữa các trung tâm (lọc "chỉ điểm khác nhau") */
  text: string;
  node: React.ReactNode;
}

// Chỉ các thông tin trang danh sách và trang chi tiết đã công khai; không có phí, số điện thoại, email
function infoRows(center: StudyCenter): InfoRow[] {
  const profile = getEnrichedCenterProfile(center);
  const place = [center.district, center.city].filter(Boolean).join(", ") || MISSING;
  const hasReviews = (center.reviewCount ?? 0) > 0;
  const accreditations = profile.accreditations.length ? profile.accreditations.join(", ") : MISSING;
  return [
    { label: "Khu vực", text: place, node: <span className="text-base">{place}</span> },
    {
      label: "Tỷ lệ đỗ Visa",
      text: String(center.visaSuccessRate ?? ""),
      node:
        center.visaSuccessRate != null ? (
          <span className="font-heading text-2xl font-bold text-ok">{center.visaSuccessRate}%</span>
        ) : (
          <span className="text-ink-3">{MISSING}</span>
        ),
    },
    {
      label: "Đánh giá",
      text: hasReviews ? `${center.averageRating}/${center.reviewCount}` : "",
      node: hasReviews ? (
        <span className="inline-flex items-center gap-1.5">
          <StarIcon className="size-5 fill-current text-gold" />
          <span className="font-heading text-xl font-bold">{formatRating(center.averageRating)}</span>
          <span className="text-sm text-ink-3">({center.reviewCount} lượt)</span>
        </span>
      ) : (
        <span className="text-ink-3">Chưa có đánh giá</span>
      ),
    },
    {
      label: "Nhân sự",
      text: String(center.staffCount ?? ""),
      node:
        center.staffCount != null ? (
          <span className="text-base">{center.staffCount} chuyên viên</span>
        ) : (
          <span className="text-ink-3">{MISSING}</span>
        ),
    },
    {
      label: "Năm thành lập",
      text: String(profile.establishedYear ?? ""),
      node:
        profile.establishedYear != null ? (
          <span className="text-base">{profile.establishedYear}</span>
        ) : (
          <span className="text-ink-3">{MISSING}</span>
        ),
    },
    {
      label: "Chứng nhận",
      text: accreditations,
      node: <span className="text-base">{accreditations}</span>,
    },
  ];
}

/** Hợp các dịch vụ của các trung tâm đang so sánh: dịch vụ chuẩn trước (theo thứ tự danh mục), mã lạ sau. */
function serviceUnion(centers: StudyCenter[]): string[] {
  const all = new Set(centers.flatMap((c) => c.services));
  const known = Object.keys(CENTER_SERVICES).filter((code) => all.has(code));
  const other = [...all].filter((code) => !(code in CENTER_SERVICES)).sort();
  return [...known, ...other];
}

/** Bỏ dấu và hạ chữ thường để tìm "ha noi" ra "Hà Nội". */
function fold(text: string): string {
  return text
    .normalize("NFD")
    .replace(/[̀-ͯ]/g, "")
    .replace(/đ/g, "d")
    .replace(/Đ/g, "D")
    .toLowerCase();
}

/** Nút mở danh sách trung tâm còn lại để chọn (dùng cho cả "Thêm" và "Đổi"): có ô tìm và lọc theo thành phố. */
function CenterPicker({
  options,
  onPick,
  trigger,
  title,
}: {
  options: StudyCenter[];
  onPick: (id: string) => void;
  trigger: React.ReactElement;
  title: string;
}) {
  const [open, setOpen] = useState(false);
  const [query, setQuery] = useState("");
  const [city, setCity] = useState("");

  const cities = [...new Set(options.map((c) => c.city).filter((c): c is string => !!c))].sort();
  const q = fold(query.trim());
  const shown = options.filter(
    (c) =>
      (!city || c.city === city) &&
      (!q || fold(`${c.name} ${c.city ?? ""} ${c.district ?? ""} ${c.code}`).includes(q)),
  );

  function handleOpenChange(next: boolean) {
    setOpen(next);
    if (!next) {
      setQuery("");
      setCity("");
    }
  }

  return (
    <Popover open={open} onOpenChange={handleOpenChange}>
      <PopoverTrigger render={trigger} />
      <PopoverContent align="start" className="w-80 gap-2 p-2.5">
        <p className="text-xs font-medium text-ink-3">{title}</p>
        <div className="relative">
          <SearchIcon className="pointer-events-none absolute top-1/2 left-2.5 size-4 -translate-y-1/2 text-ink-3" />
          <Input
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            placeholder="Tìm theo tên, thành phố, quận…"
            className="pl-8"
            autoFocus
          />
        </div>
        {cities.length > 1 && (
          <div className="flex flex-wrap gap-1.5">
            {["", ...cities].map((name) => (
              <button
                key={name || "all"}
                type="button"
                onClick={() => setCity(name)}
                className={cn(
                  "cursor-pointer rounded-full border px-2.5 py-0.5 text-xs font-medium outline-none transition-colors focus-visible:ring-3 focus-visible:ring-ring/50",
                  city === name
                    ? "border-brand bg-brand text-white"
                    : "border-line text-ink-2 hover:border-brand/50",
                )}
              >
                {name || "Tất cả"}
              </button>
            ))}
          </div>
        )}
        <ul className="max-h-60 overflow-y-auto">
          {shown.map((c) => (
            <li key={c.id}>
              <button
                type="button"
                onClick={() => {
                  handleOpenChange(false);
                  onPick(c.id);
                }}
                className="flex w-full cursor-pointer flex-col rounded-md px-2 py-1.5 text-left outline-none hover:bg-muted focus-visible:ring-3 focus-visible:ring-ring/50"
              >
                <span className="text-sm font-medium text-ink">{c.name}</span>
                <span className="text-xs text-ink-3">
                  {[c.district, c.city].filter(Boolean).join(", ") || MISSING}
                </span>
              </button>
            </li>
          ))}
          {shown.length === 0 && (
            <li className="px-2 py-3 text-center text-sm text-ink-3">Không tìm thấy trung tâm phù hợp</li>
          )}
        </ul>
      </PopoverContent>
    </Popover>
  );
}

/** Ảnh có thể lỗi (link hỏng): khi lỗi thì bỏ ảnh để hiện nền thay thế, không hiện biểu tượng ảnh vỡ. */
function SafeImg({ src, alt, className, fallback }: { src: string | null; alt: string; className: string; fallback: React.ReactNode }) {
  const [failed, setFailed] = useState(false);
  if (!src || failed) return <>{fallback}</>;
  // eslint-disable-next-line @next/next/no-img-element
  return <img src={src} alt={alt} className={className} onError={() => setFailed(true)} />;
}

/** Đầu cột: ảnh bìa lớn, logo, tên, loại, nút Đổi/Bỏ. */
function CenterHeader({
  center,
  canSwap,
  options,
  onSwap,
  onRemove,
}: {
  center: StudyCenter;
  canSwap: boolean;
  options: StudyCenter[];
  onSwap: (id: string) => void;
  onRemove: () => void;
}) {
  const profile = getEnrichedCenterProfile(center);
  return (
    <div className="overflow-hidden rounded-xl border bg-card">
      <div className="relative h-36 bg-brand-wash">
        <SafeImg
          src={profile.coverImage}
          alt=""
          className="h-full w-full object-cover"
          fallback={
            <div className="flex h-full w-full items-center justify-center">
              <Building2Icon className="size-12 text-brand/30" />
            </div>
          }
        />
        <Button
          type="button"
          variant="ghost"
          size="icon"
          onClick={onRemove}
          className="absolute top-2 right-2 rounded-full bg-black/55 text-white hover:bg-black/75 hover:text-white dark:hover:bg-black/75"
          aria-label={`Bỏ ${center.name} khỏi so sánh`}
          title="Bỏ khỏi so sánh"
        >
          <XIcon className="size-4" />
        </Button>
        <div className="absolute -bottom-6 left-4 flex size-14 items-center justify-center overflow-hidden rounded-xl border-2 border-card bg-white shadow">
          <SafeImg
            src={profile.logoImage}
            alt={`Logo ${center.name}`}
            className="h-full w-full object-contain p-1"
            fallback={<Building2Icon className="size-6 text-brand" />}
          />
        </div>
      </div>
      <div className="space-y-2 px-4 pt-9 pb-4">
        <Link
          href={`/centers/${center.id}`}
          className="line-clamp-2 block font-heading text-lg font-bold leading-snug text-ink hover:underline"
        >
          {center.name}
        </Link>
        <CenterTypeBadge centerType={center.centerType} compact />
        {canSwap && (
          <div className="pt-1">
            <CenterPicker
              options={options}
              onPick={onSwap}
              title="Đổi sang trung tâm"
              trigger={
                <Button variant="outline" size="sm">
                  <RepeatIcon className="size-3.5" /> Đổi trung tâm khác
                </Button>
              }
            />
          </div>
        )}
      </div>
    </div>
  );
}

const LABEL_CELL = "pr-4 text-left align-middle text-sm font-medium text-ink-2";
const VALUE_CELL = "text-left align-middle text-ink";
const ROW = "border-t";

export function CenterCompareView({ selected, all }: Props) {
  const router = useRouter();
  const [onlyDiff, setOnlyDiff] = useState(false);
  const ids = selected.map((c) => c.id);

  function go(next: string[]) {
    router.replace(compareHref(next));
  }

  const remove = (id: string) => go(ids.filter((x) => x !== id));
  const replace = (oldId: string, newId: string) => go(ids.map((x) => (x === oldId ? newId : x)));
  const add = (newId: string) => go([...ids, newId]);

  const free = all.filter((c) => !ids.includes(c.id));
  const canAdd = selected.length < COMPARE_MAX && free.length > 0;
  const info = selected.map(infoRows);
  const infoLabels = info[0]?.map((r) => r.label) ?? [];
  const differs = (i: number) => new Set(info.map((rows) => rows[i].text)).size > 1;
  const services = serviceUnion(selected);
  const serviceDiffers = (code: string) => new Set(selected.map((c) => c.services.includes(code))).size > 1;
  const visibleInfo = infoLabels.map((_, i) => i).filter((i) => !onlyDiff || differs(i));
  const visibleServices = services.filter((code) => !onlyDiff || serviceDiffers(code));
  const cols = selected.length + (canAdd ? 1 : 0);
  const gridStyle = { gridTemplateColumns: `minmax(150px, 190px) repeat(${cols}, minmax(0, 1fr))` };

  return (
    <div className="space-y-6">
      <div className="space-y-2">
        <Link href="/centers" className="inline-flex items-center gap-1 text-sm text-ink-3 hover:text-ink">
          <ArrowLeftIcon className="size-4" /> Quay lại danh sách trung tâm
        </Link>
        <h1 className="font-heading text-3xl font-bold text-ink">So sánh trung tâm</h1>
        <p className="text-sm text-ink-3">
          Chỉ so sánh thông tin trong hồ sơ các trung tâm đã xác minh. USDSS không xếp hạng hay khuyên chọn,
          bạn tự chọn nơi hợp với mình.
        </p>
      </div>

      {selected.length === 0 ? (
        <div className="rounded-xl border bg-card p-10 text-center text-ink-2">
          <p>Chọn {COMPARE_MIN} đến {COMPARE_MAX} trung tâm để so sánh.</p>
          {free.length > 0 && (
            <div className="mt-4 flex justify-center">
              <CenterPicker
                options={free}
                onPick={add}
                title="Chọn trung tâm"
                trigger={
                  <Button variant="outline">
                    <PlusIcon className="size-4" /> Chọn trung tâm
                  </Button>
                }
              />
            </div>
          )}
        </div>
      ) : (
        <>
          {selected.length >= COMPARE_MIN && (
            <label className="flex w-fit cursor-pointer items-center gap-2 text-sm text-ink-2">
              <Checkbox checked={onlyDiff} onCheckedChange={(v) => setOnlyDiff(v === true)} />
              Chỉ hiện điểm khác nhau
            </label>
          )}

          <div className="overflow-x-auto pb-2">
            <div className="min-w-[820px] space-y-0">
              {/* Đầu cột */}
              <div className="grid items-start gap-4 px-5 pb-4" style={gridStyle}>
                <div />
                {selected.map((center) => (
                  <CenterHeader
                    key={center.id}
                    center={center}
                    canSwap={free.length > 0}
                    options={free}
                    onSwap={(id) => replace(center.id, id)}
                    onRemove={() => remove(center.id)}
                  />
                ))}
                {canAdd && (
                  <CenterPicker
                    options={free}
                    onPick={add}
                    title="Thêm trung tâm"
                    trigger={
                      <button
                        type="button"
                        className="flex min-h-64 w-full cursor-pointer flex-col items-center justify-center gap-2 rounded-xl border-2 border-dashed text-ink-3 outline-none transition-colors hover:border-brand hover:bg-brand-wash hover:text-brand focus-visible:ring-3 focus-visible:ring-ring/50"
                      >
                        <span className="flex size-12 items-center justify-center rounded-full bg-muted">
                          <PlusIcon className="size-6" />
                        </span>
                        <span className="font-medium">Thêm trung tâm</span>
                        <span className="text-xs">So sánh tối đa {COMPARE_MAX} nơi</span>
                      </button>
                    }
                  />
                )}
              </div>

              {/* Thông tin */}
              <div className="rounded-xl border bg-card px-5">
                {visibleInfo.map((i, n) => (
                  <div key={infoLabels[i]} className={cn("grid gap-4 py-4", n > 0 && ROW)} style={gridStyle}>
                    <div className={LABEL_CELL}>{infoLabels[i]}</div>
                    {info.map((rows, col) => (
                      <div key={selected[col].id} className={VALUE_CELL}>{rows[i].node}</div>
                    ))}
                    {canAdd && <div />}
                  </div>
                ))}
                {visibleInfo.length === 0 && (
                  <p className="py-4 text-sm text-ink-3">Các thông tin chung của những trung tâm này giống nhau.</p>
                )}
              </div>

              {/* Dịch vụ */}
              {visibleServices.length > 0 && (
                <div className="mt-4 rounded-xl border bg-card px-5">
                  <p className="pt-4 pb-1 font-heading text-sm font-bold text-ink">Dịch vụ</p>
                  {visibleServices.map((code, n) => (
                    <div key={code} className={cn("grid items-center gap-4 py-3", n > 0 && ROW)} style={gridStyle}>
                      <div className={LABEL_CELL}>{centerServiceLabel(code)}</div>
                      {selected.map((center) => (
                        <div key={center.id} className={VALUE_CELL}>
                          {center.services.includes(code) ? (
                            <span className="inline-flex items-center gap-1.5 rounded-full bg-ok/10 px-3 py-1 text-sm font-medium text-ok">
                              <CheckIcon className="size-4" /> Có
                            </span>
                          ) : (
                            <span className="inline-flex items-center gap-1.5 text-sm text-ink-3">
                              <MinusIcon className="size-4" /> Chưa có
                            </span>
                          )}
                        </div>
                      ))}
                      {canAdd && <div />}
                    </div>
                  ))}
                </div>
              )}

              {/* Hành động */}
              <div className="grid gap-4 px-5 pt-4" style={gridStyle}>
                <div />
                {selected.map((center) => (
                  <Link
                    key={center.id}
                    href={`/centers/${center.id}`}
                    className={cn(buttonVariants({ variant: "default", size: "lg" }), "w-full")}
                  >
                    Chi tiết và liên hệ
                  </Link>
                ))}
                {canAdd && <div />}
              </div>
            </div>
          </div>
        </>
      )}

      {selected.length === 1 && (
        <p className="text-sm text-ink-3">Chọn thêm ít nhất 1 trung tâm nữa để so sánh.</p>
      )}
    </div>
  );
}
