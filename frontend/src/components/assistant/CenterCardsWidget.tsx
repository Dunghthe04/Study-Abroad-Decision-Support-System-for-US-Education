"use client";

// Thẻ trung tâm trong chat: mỗi trung tâm một thẻ, hai nút nhỏ ngay dưới thẻ: Chi tiết (trang trung tâm) và Liên hệ (form gửi yêu cầu).
// Hiện firstPage trung tâm đầu, nút "Xem thêm" mở phần còn lại ngay trong thẻ (dữ liệu đã có sẵn, không gọi lại).
// Thanh gõ "Tìm ở thành phố khác" chỉ liệt kê thành phố đang có trung tâm cho dịch vụ này, nên chọn xong luôn có kết quả.
// Mỗi thẻ có ô tick "So sánh" (tối đa 3); từ 2 thẻ trở lên hiện nút mở trang /centers/compare. Hệ thống không tự chấm hay xếp hạng.
// Dữ liệu do advisor dựng (chỉ trung tâm đã xác minh), phần chữ "vì sao hợp" do AI viết và code đã kiểm.

import { useState } from "react";
import Link from "next/link";
import { SearchIcon } from "lucide-react";
import { Button, buttonVariants } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { COMPARE_MAX, COMPARE_MIN, compareHref } from "@/lib/center-compare";
import { ContactRequestDialog } from "@/components/centers/ContactRequestDialog";
import { normalizeSearch } from "@/lib/certification-utils";
import { cn } from "@/lib/utils";

interface CenterCard {
  id: string | null;
  name: string;
  city: string | null;
  website: string | null;
  reason: string;
}

interface CityOption {
  city: string;
  count: number;
  ask: string; // câu gửi lại khi chọn thành phố này (đã nêu rõ dịch vụ)
}

const DEFAULT_FIRST_PAGE = 3;
const MAX_CITY_BUTTONS = 8;

function parseCenters(data: Record<string, unknown> | null | undefined): CenterCard[] {
  const raw = data?.centers;
  if (!Array.isArray(raw)) return [];
  return raw.filter((c): c is CenterCard => typeof c === "object" && c !== null && typeof (c as CenterCard).name === "string");
}

function parseCities(data: Record<string, unknown> | null | undefined): CityOption[] {
  const raw = data?.cities;
  if (!Array.isArray(raw)) return [];
  return raw.filter(
    (c): c is CityOption =>
      typeof c === "object" && c !== null && typeof (c as CityOption).city === "string" && typeof (c as CityOption).ask === "string",
  );
}

function CitySearch({ cities, onPick, hasCenters }: { cities: CityOption[]; onPick: (text: string) => void; hasCenters: boolean }) {
  const [text, setText] = useState("");
  const typed = normalizeSearch(text);
  const matches = typed ? cities.filter((c) => normalizeSearch(c.city).includes(typed)) : cities;
  const shown = typed ? matches : matches.slice(0, MAX_CITY_BUTTONS);
  return (
    <div className="mt-3 space-y-1.5">
      <p className="text-xs font-semibold text-ink-2">{hasCenters ? "Tìm ở thành phố khác" : "Chọn thành phố đang có trung tâm"}</p>
      <div className="relative">
        <SearchIcon className="pointer-events-none absolute left-2.5 top-1/2 size-3.5 -translate-y-1/2 text-ink-3" />
        <input
          type="text"
          value={text}
          onChange={(e) => setText(e.target.value)}
          placeholder="Gõ tên tỉnh, thành phố..."
          aria-label="Gõ tên tỉnh, thành phố"
          className="h-8 w-full rounded-lg border border-line bg-surface pl-8 pr-2 text-sm outline-none focus-visible:border-brand focus-visible:ring-1 focus-visible:ring-brand"
        />
      </div>
      <div className="flex flex-wrap gap-1.5">
        {shown.map((c) => (
          <Button key={c.city} type="button" variant="outline" size="sm" onClick={() => onPick(c.ask)}>
            {c.city} ({c.count})
          </Button>
        ))}
        {typed && matches.length === 0 && (
          <p className="text-xs text-ink-3">Chưa có trung tâm ở nơi bạn gõ. Hệ thống chỉ liệt kê các thành phố đang có trung tâm.</p>
        )}
      </div>
    </div>
  );
}

export function CenterCardsWidget({
  data,
  onPick,
}: {
  data: Record<string, unknown> | null | undefined;
  onPick?: (text: string) => void;
}) {
  const centers = parseCenters(data);
  const cities = parseCities(data);
  const firstPage = typeof data?.firstPage === "number" ? data.firstPage : DEFAULT_FIRST_PAGE;
  const [expanded, setExpanded] = useState(false);
  const [compareIds, setCompareIds] = useState<string[]>([]);
  const toggleCompare = (id: string) =>
    setCompareIds((prev) => (prev.includes(id) ? prev.filter((x) => x !== id) : prev.length >= COMPARE_MAX ? prev : [...prev, id]));
  if (centers.length === 0 && cities.length === 0) return null;
  const shown = expanded ? centers : centers.slice(0, firstPage);
  const hidden = centers.length - shown.length;
  return (
    <div>
      {shown.length > 0 && (
        <ul className="mt-2 space-y-2">
          {shown.map((center) => (
            <li key={center.id ?? center.name} className="rounded-xl border border-line bg-surface px-3 py-2.5">
              <p className="text-[13px] font-bold text-foreground">
                {center.name}
                {center.city && <span className="font-normal text-ink-2"> · {center.city}</span>}
              </p>
              <p className="mt-0.5 text-xs text-ink-2">{center.reason}</p>
              {center.website && (
                <a
                  href={center.website}
                  target="_blank"
                  rel="noreferrer"
                  className="mt-0.5 inline-block text-xs text-brand underline-offset-2 hover:underline"
                >
                  {center.website.replace(/^https?:\/\//, "")}
                </a>
              )}
              {center.id && (
                <div className="mt-2 flex flex-wrap items-center gap-2">
                  <Link
                    href={`/centers/${center.id}`}
                    className={buttonVariants({ variant: "outline", size: "sm" })}
                  >
                    Chi tiết
                  </Link>
                  {/* Advisor chỉ trả trung tâm đã xác minh nên cho phép gửi yêu cầu; server vẫn kiểm lại */}
                  <ContactRequestDialog centerId={center.id} centerName={center.name} isVerified compact />
                  <label
                    className={cn(
                      "ml-auto flex items-center gap-1.5 text-xs text-ink-2",
                      !compareIds.includes(center.id) && compareIds.length >= COMPARE_MAX
                        ? "cursor-not-allowed opacity-50"
                        : "cursor-pointer",
                    )}
                  >
                    <Checkbox
                      checked={compareIds.includes(center.id)}
                      disabled={!compareIds.includes(center.id) && compareIds.length >= COMPARE_MAX}
                      onCheckedChange={() => toggleCompare(center.id!)}
                    />
                    So sánh
                  </label>
                </div>
              )}
            </li>
          ))}
        </ul>
      )}
      {compareIds.length > 0 && (
        <div className="mt-2 flex flex-wrap items-center gap-2 rounded-xl border border-brand/30 bg-brand-wash px-3 py-2">
          {compareIds.length >= COMPARE_MIN ? (
            <Link href={compareHref(compareIds)} className={cn(buttonVariants({ variant: "default", size: "sm" }))}>
              So sánh {compareIds.length} nơi
            </Link>
          ) : (
            <span className="text-xs text-ink-2">Chọn thêm ít nhất 1 trung tâm để so sánh</span>
          )}
          <Button type="button" variant="ghost" size="sm" onClick={() => setCompareIds([])}>
            Bỏ chọn
          </Button>
        </div>
      )}
      {hidden > 0 && (
        <Button type="button" variant="outline" size="sm" className="mt-2" onClick={() => setExpanded(true)}>
          Xem thêm {hidden} trung tâm
        </Button>
      )}
      {onPick && cities.length > 0 && <CitySearch cities={cities} onPick={onPick} hasCenters={centers.length > 0} />}
    </div>
  );
}
