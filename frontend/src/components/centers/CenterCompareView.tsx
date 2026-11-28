"use client";

import { SafeImage } from "@/components/shared/safe-image";
import { useId, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import {
  ArrowLeftIcon,
  BadgeCheckIcon,
  Building2Icon,
  CheckIcon,
  ClockIcon,
  FileCheckIcon,
  FileTextIcon,
  MinusIcon,
  SearchIcon,
  StarIcon,
  XIcon,
} from "lucide-react";
import type { StudyCenter } from "@/types/api";
import { getEnrichedCenterProfile } from "@/lib/center-details";
import { CENTER_SERVICES, centerServiceLabel } from "@/lib/center-services";
import { COMPARE_MAX, COMPARE_MIN, compareHref } from "@/lib/center-compare";
import { formatRating } from "@/lib/rating";
import { Cell, CompareTable, CornerCell, HeadCell, RowHead } from "@/components/costs/CompareTable";
import { Button, buttonVariants } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from "@/components/ui/tooltip";
import { cn } from "@/lib/utils";

interface Props {
  selected: StudyCenter[];
  all: StudyCenter[];
}

const MISSING = "Chưa cập nhật";

interface InfoRow {
  label: string;
  sub?: string;
  /** Chuỗi để so sánh giữa các trung tâm (lọc "chỉ điểm khác nhau") */
  text: string;
  node: React.ReactNode;
}

function formatMedianHours(hours: number): string {
  return hours < 1 ? "< 1 giờ" : hours < 24 ? `${hours.toFixed(1)} giờ` : `${(hours / 24).toFixed(1)} ngày`;
}

function centerTypeLabel(centerType?: string | null) {
  if (centerType === "hybrid") return "Toàn diện";
  if (centerType === "english_training" || centerType === "test_prep") return "Luyện thi Chứng chỉ";
  return "Tư vấn Du học";
}

function TrustIcon({ label, lines, children }: { label: string; lines?: string[]; children: React.ReactNode }) {
  return (
    <TooltipProvider>
      <Tooltip>
        <TooltipTrigger delay={150} render={<span />} className="cursor-help">
          {children}
          <span className="sr-only">{label}</span>
        </TooltipTrigger>
        <TooltipContent side="top" sideOffset={8}>
          {lines ? lines.map((line) => <div key={line}>{line}</div>) : label}
        </TooltipContent>
      </Tooltip>
    </TooltipProvider>
  );
}

// [UC-142] Chỉ các thông tin trang danh sách và trang chi tiết đã công khai; không có phí, tỷ lệ đỗ Visa, SĐT, email
function infoRows(center: StudyCenter): InfoRow[] {
  const profile = getEnrichedCenterProfile(center);
  const place = [center.district, center.city].filter(Boolean).join(", ") || MISSING;
  const hasReviews = (center.reviewCount ?? 0) > 0;
  const accreditations = profile.accreditations.length ? profile.accreditations.join(", ") : MISSING;
  const isVerified = center.verificationStatus === "verified";
  const verifiedDate = center.verifiedAt ? new Date(center.verifiedAt).toLocaleDateString("vi-VN") : null;
  const licenseOrTax = center.licenseNumber || center.taxCode;
  const paperTitle = profile.legalComplianceMode === "licensed" ? "Cấp phép Sở GD&ĐT" : "Thông báo hoạt động Sở GD";
  const paperLines = [paperTitle, licenseOrTax ? `Giấy phép / MST: ${licenseOrTax}` : null].filter(
    (line): line is string => Boolean(line),
  );
  const trustText = `${isVerified ? "Đã xác minh" : "Chờ xác minh"} · ${paperLines.join(" · ")}`;

  const hasResponseSample = (center.responseSampleCount ?? 0) >= 5;
  const medianText =
    hasResponseSample && center.medianResponseHours != null
      ? formatMedianHours(center.medianResponseHours)
      : "Chưa đủ dữ liệu";
  const rateText =
    hasResponseSample && center.responseRatePercent != null ? `${center.responseRatePercent}%` : "Chưa đủ dữ liệu";

  return [
    { label: "Khu vực", text: place, node: <span>{place}</span> },
    {
      label: "Xác minh & Pháp lý",
      text: trustText,
      node: (
        <div className="space-y-1.5">
          <div className="flex items-center gap-1.5">
            <TrustIcon label={isVerified ? "Đã xác minh" : "Chờ xác minh"}>
              {isVerified ? (
                <BadgeCheckIcon className="size-4 text-ok" aria-hidden />
              ) : (
                <ClockIcon className="size-4 text-warn" aria-hidden />
              )}
            </TrustIcon>
            <span className={cn("font-medium", isVerified ? "text-ok" : "text-ink-2")}>
              {isVerified ? "Đã xác minh" : "Chờ xác minh"}
            </span>
            {isVerified && verifiedDate && <span className="text-xs text-ink-3">· {verifiedDate}</span>}
          </div>
          <div className="flex items-center gap-1.5 text-xs text-ink-2">
            <TrustIcon label={paperLines.join(". ")} lines={paperLines}>
              {profile.legalComplianceMode === "licensed" ? (
                <FileCheckIcon className="size-4 text-ok" aria-hidden />
              ) : (
                <FileTextIcon className="size-4 text-ink-3" aria-hidden />
              )}
            </TrustIcon>
            <span>{paperTitle}</span>
          </div>
        </div>
      ),
    },
    {
      label: "Thời gian phản hồi",
      sub: "trung vị",
      text: medianText,
      node:
        hasResponseSample && center.medianResponseHours != null ? (
          <span className="font-medium text-ink">{medianText}</span>
        ) : (
          <span className="text-ink-3">Chưa đủ dữ liệu</span>
        ),
    },
    {
      label: "Tỷ lệ phản hồi",
      text: rateText,
      node:
        hasResponseSample && center.responseRatePercent != null ? (
          <span className="font-semibold text-ok">{rateText}</span>
        ) : (
          <span className="text-ink-3">Chưa đủ dữ liệu</span>
        ),
    },
    {
      label: "Đánh giá",
      text: hasReviews ? `${center.averageRating}/${center.reviewCount}` : "",
      node: hasReviews ? (
        <span className="inline-flex items-center gap-1.5">
          <StarIcon className="size-4 fill-current text-gold" aria-hidden />
          <span className="font-semibold text-ink">{formatRating(center.averageRating)}</span>
          <span className="text-xs text-ink-2">({center.reviewCount} lượt)</span>
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
          <span>{center.staffCount} chuyên viên</span>
        ) : (
          <span className="text-ink-3">{MISSING}</span>
        ),
    },
    {
      label: "Năm thành lập",
      text: String(profile.establishedYear ?? ""),
      node:
        profile.establishedYear != null ? (
          <span>{profile.establishedYear}</span>
        ) : (
          <span className="text-ink-3">{MISSING}</span>
        ),
    },
    {
      label: "Chứng nhận",
      text: accreditations,
      node: <span className={accreditations === MISSING ? "text-ink-3" : undefined}>{accreditations}</span>,
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

/** Ô tìm trung tâm theo mẫu SchoolSearch trên /costs: bấm vào là hiện gợi ý, gõ để lọc, Enter/click để thêm. */
function CenterSearch({
  options,
  pickedIds,
  onPick,
  placeholder,
  disabled = false,
  action,
}: {
  options: StudyCenter[];
  pickedIds: string[];
  onPick: (id: string) => void;
  placeholder: string;
  disabled?: boolean;
  action?: React.ReactNode;
}) {
  const listId = useId();
  const [query, setQuery] = useState("");
  const [open, setOpen] = useState(false);
  const [active, setActive] = useState(0);

  const q = fold(query.trim());
  const items = (
    q
      ? options.filter((c) => fold(`${c.name} ${c.city ?? ""} ${c.district ?? ""} ${c.code}`).includes(q))
      : options
  ).slice(0, 8);
  const showList = open && !disabled && (q.length > 0 || items.length > 0);

  const pick = (c: StudyCenter) => {
    if (pickedIds.includes(c.id)) return;
    onPick(c.id);
    setQuery("");
    setActive(0);
    setOpen(false);
  };

  return (
    <div className="relative w-full">
      <label className="sr-only" htmlFor={`${listId}-input`}>
        Tìm trung tâm để so sánh
      </label>
      <SearchIcon aria-hidden className="pointer-events-none absolute top-1/2 left-4 size-5 -translate-y-1/2 text-ink-3" />
      <input
        id={`${listId}-input`}
        role="combobox"
        aria-expanded={showList}
        aria-controls={listId}
        aria-autocomplete="list"
        autoComplete="off"
        disabled={disabled}
        value={query}
        placeholder={placeholder}
        onChange={(e) => {
          setQuery(e.target.value);
          setActive(0);
          setOpen(true);
        }}
        onFocus={() => setOpen(true)}
        onClick={() => setOpen(true)}
        onBlur={() => setTimeout(() => setOpen(false), 120)}
        onKeyDown={(e) => {
          if (e.key === "Escape") {
            setOpen(false);
            return;
          }
          if (!showList || items.length === 0) return;
          if (e.key === "ArrowDown") {
            e.preventDefault();
            setActive((a) => (a + 1) % items.length);
          } else if (e.key === "ArrowUp") {
            e.preventDefault();
            setActive((a) => (a - 1 + items.length) % items.length);
          } else if (e.key === "Enter") {
            e.preventDefault();
            pick(items[active]);
          }
        }}
        className={cn(
          "h-14 w-full rounded-2xl border border-control-border bg-surface pl-12 text-base text-ink shadow-[var(--elev-1)] transition-[border-color,box-shadow] outline-none placeholder:text-ink-2 focus-visible:border-brand focus-visible:ring-3 focus-visible:ring-brand/20 disabled:cursor-not-allowed disabled:bg-muted disabled:text-ink-3",
          action ? "pr-36" : "pr-12",
        )}
      />
      {action && <div className="absolute top-1/2 right-2.5 -translate-y-1/2">{action}</div>}
      {showList && (
        <div className="absolute z-20 mt-2 w-full overflow-hidden rounded-2xl border border-line bg-surface text-left shadow-lg">
          {!q && <p className="px-4 pt-3 pb-1 text-xs font-medium text-ink-2">Gợi ý</p>}
          <ul id={listId} role="listbox" className="max-h-96 overflow-auto p-1.5">
            {q && items.length === 0 && (
              <li className="px-3 py-3 text-sm text-ink-2">Không tìm thấy trung tâm nào khớp «{query.trim()}».</li>
            )}
            {items.map((c, i) => {
              const profile = getEnrichedCenterProfile(c);
              const picked = pickedIds.includes(c.id);
              const place = [c.district, c.city].filter(Boolean).join(", ");
              const hasReviews = (c.reviewCount ?? 0) > 0;
              return (
                <li
                  key={c.id}
                  role="option"
                  aria-selected={i === active}
                  aria-disabled={picked}
                  onMouseDown={(e) => e.preventDefault()}
                  onMouseEnter={() => setActive(i)}
                  onClick={() => pick(c)}
                  className={cn(
                    "flex cursor-pointer items-center gap-3.5 rounded-xl p-2 transition-colors",
                    i === active && "bg-brand-wash",
                    picked && "cursor-default opacity-50",
                  )}
                >
                  <span className="relative flex h-14 w-[4.5rem] shrink-0 items-center justify-center overflow-hidden rounded-lg bg-muted">
                    <SafeImage
                      src={profile.coverImage ?? profile.logoImage}
                      alt=""
                      fallback={<Building2Icon aria-hidden className="size-5 text-ink-3" />}
                    />
                  </span>
                  <span className="min-w-0 flex-1">
                    <span className="block truncate text-sm font-semibold text-ink">{c.name}</span>
                    <span className="block truncate text-xs text-ink-2">
                      {[place, centerTypeLabel(c.centerType)].filter(Boolean).join(" · ") || c.code}
                    </span>
                    {(hasReviews || c.staffCount != null) && (
                      <span className="mt-0.5 block truncate text-xs text-ink-2 tabular-nums">
                        {hasReviews && (
                          <>
                            Đánh giá <span className="font-medium text-ink">{formatRating(c.averageRating)}★</span> (
                            {c.reviewCount})
                          </>
                        )}
                        {hasReviews && c.staffCount != null && " · "}
                        {c.staffCount != null && <>{c.staffCount} chuyên viên</>}
                      </span>
                    )}
                  </span>
                  {picked && <span className="shrink-0 text-xs text-ink-2">Đã chọn</span>}
                </li>
              );
            })}
          </ul>
        </div>
      )}
    </div>
  );
}

export function CenterCompareView({ selected, all }: Props) {
  const router = useRouter();
  const [onlyDiff, setOnlyDiff] = useState(false);
  const ids = selected.map((c) => c.id);

  function go(next: string[]) {
    router.replace(compareHref(next), { scroll: false });
  }

  const remove = (id: string) => go(ids.filter((x) => x !== id));
  const add = (newId: string) => {
    if (ids.includes(newId) || ids.length >= COMPARE_MAX) return;
    go([...ids, newId]);
  };

  const full = selected.length >= COMPARE_MAX;
  const canCompare = selected.length >= COMPARE_MIN;
  const info = selected.map(infoRows);
  const infoLabels = info[0]?.map((r) => ({ label: r.label, sub: r.sub })) ?? [];
  const differs = (i: number) => new Set(info.map((rows) => rows[i].text)).size > 1;
  const services = serviceUnion(selected);
  const serviceDiffers = (code: string) => new Set(selected.map((c) => c.services.includes(code))).size > 1;
  const visibleInfo = infoLabels.map((_, i) => i).filter((i) => !onlyDiff || differs(i));
  const visibleServices = services.filter((code) => !onlyDiff || serviceDiffers(code));

  const showResults = () => {
    const reduce = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
    document.getElementById("center-compare-results")?.scrollIntoView({
      behavior: reduce ? "auto" : "smooth",
      block: "start",
    });
  };

  return (
    <section className="@container mx-auto flex w-full max-w-6xl flex-col gap-10 px-4 py-10 pb-20 sm:px-6 has-data-empty:min-h-[calc(100dvh-var(--header-h,6.5rem))] has-data-empty:justify-center has-data-empty:pb-[12vh]">
      <header className="flex flex-col items-center gap-4 text-center">
        <Link
          href="/centers"
          className="inline-flex items-center gap-1.5 text-sm text-ink-2 transition-colors hover:text-ink"
        >
          <ArrowLeftIcon aria-hidden className="size-4" /> Quay lại danh sách trung tâm
        </Link>
        <h1 className="font-heading text-3xl font-semibold tracking-tight text-ink sm:text-4xl">So sánh trung tâm</h1>
        <p className="mx-auto max-w-2xl text-base text-pretty text-ink-2">
          Chỉ so sánh thông tin trong hồ sơ các trung tâm đã xác minh. USDSS không xếp hạng hay khuyên chọn, bạn tự
          chọn nơi hợp với mình.
        </p>
      </header>

      <div className="space-y-8" data-empty={selected.length === 0 ? "" : undefined}>
        <div className="mx-auto w-full max-w-3xl space-y-4">
          <CenterSearch
            options={all}
            pickedIds={ids}
            onPick={add}
            disabled={full}
            placeholder={
              full
                ? `Đã đủ ${COMPARE_MAX} trung tâm, bỏ bớt để thêm trung tâm khác`
                : selected.length === 0
                  ? "Nhập tên, thành phố hoặc quận để chọn trung tâm so sánh"
                  : `Thêm trung tâm để so sánh (${selected.length}/${COMPARE_MAX})`
            }
            action={
              canCompare ? (
                <Button type="button" onClick={showResults} className="h-10 rounded-xl px-5 text-sm font-semibold">
                  So sánh
                </Button>
              ) : undefined
            }
          />

          {selected.length > 0 && (
            <ul className="grid grid-cols-[repeat(auto-fit,minmax(11rem,1fr))] gap-3" aria-label="Trung tâm đang so sánh">
              {selected.map((center) => (
                <li
                  key={center.id}
                  className="flex min-h-16 items-start justify-between gap-2 rounded-xl border border-line bg-surface p-4 shadow-[var(--elev-1)]"
                >
                  <div className="min-w-0">
                    <Link
                      href={`/centers/${center.id}`}
                      className="line-clamp-2 text-sm font-semibold text-ink hover:text-brand hover:underline"
                    >
                      {center.name}
                    </Link>
                    <p className="mt-0.5 truncate text-xs text-ink-2">
                      {[center.district, center.city].filter(Boolean).join(", ") || center.code}
                    </p>
                  </div>
                  <button
                    type="button"
                    onClick={() => remove(center.id)}
                    aria-label={`Bỏ ${center.name}`}
                    className="-mt-1 -mr-1 shrink-0 rounded-md p-1.5 text-ink-3 transition-colors hover:bg-risk-wash hover:text-risk"
                  >
                    <XIcon aria-hidden className="size-4" />
                  </button>
                </li>
              ))}
            </ul>
          )}

          {selected.length === 1 && (
            <p className="text-center text-sm text-ink-2">
              Chọn thêm ít nhất 1 trung tâm nữa (tối đa {COMPARE_MAX}) để so sánh.
            </p>
          )}
        </div>

        {selected.length > 0 && (
          <div id="center-compare-results" className={cn("scroll-mt-24 space-y-4", selected.length === 1 && "mx-auto w-full max-w-4xl")}>
            <div className="flex flex-wrap items-center justify-between gap-3">
              <h2 className="font-heading text-xl font-semibold text-ink sm:text-2xl">Chi tiết so sánh</h2>
              {canCompare && (
                <label className="flex cursor-pointer items-center gap-2 text-sm text-ink-2">
                  <Checkbox checked={onlyDiff} onCheckedChange={(v) => setOnlyDiff(v === true)} />
                  Chỉ hiện điểm khác nhau
                </label>
              )}
            </div>

            <CompareTable minWidth={canCompare ? 600 : 420}>
              <thead>
                <tr>
                  <CornerCell>Tiêu chí</CornerCell>
                  {selected.map((center) => (
                    <HeadCell key={center.id}>
                      <div className="flex items-start justify-between gap-2">
                        <div className="min-w-0">
                          <Link href={`/centers/${center.id}`} className="hover:text-brand hover:underline">
                            {center.name}
                          </Link>
                          <span className="mt-0.5 block text-sm font-normal text-ink-2">
                            {centerTypeLabel(center.centerType)}
                          </span>
                        </div>
                        <button
                          type="button"
                          onClick={() => remove(center.id)}
                          aria-label={`Bỏ ${center.name} khỏi so sánh`}
                          className="-mt-1 -mr-1 shrink-0 rounded-md p-1 text-ink-3 transition-colors hover:bg-risk-wash hover:text-risk"
                        >
                          <XIcon aria-hidden className="size-4" />
                        </button>
                      </div>
                    </HeadCell>
                  ))}
                </tr>
              </thead>
              <tbody>
                {visibleInfo.map((i) => (
                  <tr key={infoLabels[i].label}>
                    <RowHead title={infoLabels[i].label} sub={infoLabels[i].sub} />
                    {info.map((rows, col) => (
                      <Cell key={selected[col].id}>{rows[i].node}</Cell>
                    ))}
                  </tr>
                ))}
                {visibleInfo.length === 0 && (
                  <tr>
                    <td colSpan={selected.length + 1} className="border-r border-b border-ink/70 px-4 py-4 text-sm text-ink-2">
                      Các thông tin chung của những trung tâm này giống nhau.
                    </td>
                  </tr>
                )}

                {visibleServices.length > 0 && (
                  <>
                    <tr>
                      <th
                        colSpan={selected.length + 1}
                        scope="colgroup"
                        className="border-r border-b border-ink/70 bg-muted/40 px-4 py-2.5 text-left text-xs font-semibold tracking-wide text-ink-2 uppercase"
                      >
                        Dịch vụ hỗ trợ
                      </th>
                    </tr>
                    {visibleServices.map((code) => (
                      <tr key={code}>
                        <RowHead title={centerServiceLabel(code)} />
                        {selected.map((center) => (
                          <Cell key={center.id}>
                            {center.services.includes(code) ? (
                              <span className="inline-flex items-center gap-1.5 font-medium text-ok">
                                <CheckIcon aria-hidden className="size-4" /> Có
                              </span>
                            ) : (
                              <span className="inline-flex items-center gap-1.5 text-ink-3">
                                <MinusIcon aria-hidden className="size-4" /> Chưa có
                              </span>
                            )}
                          </Cell>
                        ))}
                      </tr>
                    ))}
                  </>
                )}

                <tr>
                  <RowHead title="Hồ sơ trung tâm" />
                  {selected.map((center) => (
                    <Cell key={center.id}>
                      <Link
                        href={`/centers/${center.id}`}
                        className={cn(buttonVariants({ variant: "outline", size: "sm" }), "w-full sm:w-auto")}
                      >
                        Chi tiết và liên hệ
                      </Link>
                    </Cell>
                  ))}
                </tr>
              </tbody>
            </CompareTable>
          </div>
        )}
      </div>
    </section>
  );
}
