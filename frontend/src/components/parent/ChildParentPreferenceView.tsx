"use client";

// UC-76 (2e): con xem danh sách theo ưu tiên cha mẹ, chỉ đọc. Có 2 phụ huynh thì chọn Bố / Mẹ.
// ?parentId= trong link thông báo chọn sẵn phụ huynh vừa lưu.

import Link from "next/link";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { ArrowLeftIcon } from "lucide-react";
import { Spinner } from "@/components/ui/spinner";
import { ToggleGroup, ToggleGroupItem } from "@/components/ui/toggle-group";
import { parentPreferenceApi } from "@/lib/parent-preference-api";
import { useFetched } from "@/lib/use-fetched";
import { PreferenceComparison } from "@/components/parent/PreferenceComparison";

export function ChildParentPreferenceView() {
  const router = useRouter();
  const pathname = usePathname();
  const parentId = useSearchParams().get("parentId") ?? undefined;
  const view = useFetched(`child-pp-${parentId ?? ""}`, () => parentPreferenceApi.forChild(parentId));

  const choose = (id: string) => router.replace(`${pathname}?parentId=${encodeURIComponent(id)}`, { scroll: false });

  const data = view.data;
  const selected = data?.parents.find((p) => p.id === data.selectedParentId) ?? null;

  return (
    <div className="mx-auto max-w-5xl space-y-6 px-4 py-6 lg:px-8">
      <div className="space-y-2">
        <Link
          href="/recommendations"
          className="inline-flex items-center gap-1 text-xs font-semibold text-brand underline-offset-4 hover:underline"
        >
          <ArrowLeftIcon className="size-3.5" />
          Về trang Gợi ý trường
        </Link>
        <h1 className="text-h1 text-foreground">So với cha mẹ</h1>
        <p className="text-sm text-muted-foreground">
          Danh sách trường nếu chọn theo ưu tiên của cha mẹ, đặt cạnh danh sách của bạn. Trang này chỉ để xem, không đổi
          gợi ý của bạn.
        </p>
      </div>

      {view.loading && data == null ? (
        <div className="flex min-h-[30vh] items-center justify-center">
          <Spinner className="size-6 text-brand" />
        </div>
      ) : view.error ? (
        <Box>{view.error}</Box>
      ) : !data || data.parents.length === 0 ? (
        <Box>Bạn chưa liên kết với phụ huynh nào nên chưa có gì để so.</Box>
      ) : (
        <div className="space-y-4">
          {data.parents.length > 1 && (
            <ToggleGroup
              value={data.selectedParentId ? [data.selectedParentId] : []}
              onValueChange={(v) => {
                const next = Array.isArray(v) ? v[0] : v;
                if (next) choose(next);
              }}
              variant="outline"
              aria-label="Chọn phụ huynh"
            >
              {data.parents.map((p) => (
                <ToggleGroupItem key={p.id} value={p.id}>
                  {p.name}
                  {!p.hasPref && <span className="text-xs text-muted-foreground"> (chưa chọn)</span>}
                </ToggleGroupItem>
              ))}
            </ToggleGroup>
          )}

          {!data.compare || !data.compare.parentHasPref ? (
            <Box>{`${selected?.name ?? "Phụ huynh"} chưa chọn ưu tiên cho bạn.`}</Box>
          ) : (
            <div className={view.loading ? "opacity-60 transition-opacity" : "transition-opacity"}>
              <PreferenceComparison data={data.compare} parentName={selected?.name ?? "Cha mẹ"} forChild />
            </div>
          )}
        </div>
      )}
    </div>
  );
}

function Box({ children }: { children: React.ReactNode }) {
  return (
    <div className="rounded-2xl border border-line bg-surface p-8 text-center text-sm text-muted-foreground">{children}</div>
  );
}
