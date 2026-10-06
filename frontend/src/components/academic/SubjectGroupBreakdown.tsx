import type { SubjectGroupScore } from "@/types/academic";

interface SubjectGroupBreakdownProps {
  groups: SubjectGroupScore[];
}

/**
 * [USAS-365] Phân tích điểm trung bình theo từng nhóm môn học:
 * - Điều chỉnh tên chính xác theo dữ liệu: "Điểm Trung Bình Theo Nhóm Môn".
 * - Tối ưu kích cỡ chữ và độ tương phản dễ đọc cho học sinh và phụ huynh.
 */
export function SubjectGroupBreakdown({ groups }: SubjectGroupBreakdownProps) {
  if (groups.length === 0) {
    return null;
  }

  return (
    <div className="rounded-xl border border-slate-200/90 bg-white p-5 sm:p-6 shadow-xs h-full flex flex-col justify-between min-w-0 w-full">
      <div>
        <div className="border-b border-slate-100 pb-3">
          <h3 className="text-base font-bold text-slate-900">
            Điểm Trung Bình Theo Nhóm Môn
          </h3>
          <p className="text-xs text-slate-500 mt-0.5">
            Thống kê điểm số và số lượng môn học theo từng nhóm lĩnh vực
          </p>
        </div>

        <div className="mt-4 space-y-3.5">
          {groups.map((group) => {
            const percent = Math.min(100, Math.round((group.gpa4 / 4.0) * 100));

            return (
              <div
                key={group.groupKey}
                className="rounded-lg border border-slate-100 bg-slate-50/70 p-3.5 space-y-2.5"
              >
                <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-1.5 sm:gap-2">
                  <div className="flex flex-wrap items-center gap-1.5 min-w-0">
                    <span className="text-xs sm:text-sm font-bold text-slate-900 break-words">
                      {group.groupName}
                    </span>
                    <span className="text-xs font-medium text-slate-500 shrink-0">
                      ({group.subjectsCount} môn)
                    </span>
                  </div>
                  <div className="text-left sm:text-right flex items-baseline gap-1 shrink-0">
                    <span className="text-sm sm:text-base font-extrabold text-slate-900">
                      {group.gpa4.toFixed(2)}
                    </span>
                    <span className="text-xs text-slate-500 font-medium">/ 4.00</span>
                    <span className="text-xs text-slate-500 ml-1">
                      ({group.rawAverage.toFixed(1)}/10)
                    </span>
                  </div>
                </div>

                {/* Thanh đo tỷ lệ điểm */}
                <div className="h-1.5 w-full overflow-hidden rounded-full bg-slate-200">
                  <div
                    className="h-full rounded-full bg-slate-800 transition-all duration-500"
                    style={{ width: `${percent}%` }}
                  />
                </div>

                {/* Danh sách tên môn */}
                <div className="flex flex-wrap gap-1.5 pt-0.5">
                  {group.subjects.map((sub) => (
                    <span
                      key={sub}
                      className="rounded bg-white px-2 py-0.5 text-xs font-medium text-slate-700 border border-slate-200"
                    >
                      {sub}
                    </span>
                  ))}
                </div>
              </div>
            );
          })}
        </div>
      </div>
    </div>
  );
}
