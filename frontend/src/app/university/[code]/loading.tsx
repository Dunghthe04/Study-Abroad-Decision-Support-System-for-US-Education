import { Skeleton } from "@/components/ui/skeleton";

export default function UniversityLoading() {
  return (
    <div role="status" aria-label="Đang tải thông tin trường" className="space-y-6">
      <Skeleton className="h-64 w-full rounded-lg" />
      <Skeleton className="h-9 w-3/4" />
      <Skeleton className="h-5 w-1/3" />
      <div className="grid gap-6 lg:grid-cols-[minmax(0,1fr)_280px]">
        <div className="space-y-5"><Skeleton className="h-64 w-full" /><Skeleton className="h-96 w-full" /></div>
        <Skeleton className="h-72 w-full" />
      </div>
    </div>
  );
}
