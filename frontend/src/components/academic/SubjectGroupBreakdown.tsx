import type { SubjectGroupScore } from "@/types/academic";

interface SubjectGroupBreakdownProps {
  groups: SubjectGroupScore[];
}

/**
 * [USAS-365] Phân tích điểm theo từng nhóm môn học:
 * - Khoa học Tự nhiên (STEM)
 * - Khoa học Xã hội & Nhân văn
 * - Ngoại ngữ
 */
export function SubjectGroupBreakdown({ groups }: SubjectGroupBreakdownProps) {
  if (groups.length === 0) {
    return null;
  }

  const getGroupBadgeColor = (groupKey: string) => {
    switch (groupKey) {
      case "natural_sciences":
        return {
          bar: "bg-blue-600",
          badge: "bg-blue-100 text-blue-800",
          icon: "🔬",
        };
      case "languages":
        return {
          bar: "bg-emerald-600",
          badge: "bg-emerald-100 text-emerald-800",
          icon: "🌐",
        };
      case "social_sciences":
        return {
          bar: "bg-purple-600",
          badge: "bg-purple-100 text-purple-800",
          icon: "📚",
        };
      default:
        return {
          bar: "bg-slate-600",
          badge: "bg-slate-100 text-slate-800",
          icon: "📌",
        };
    }
  };

  return (
    <div className="rounded-xl border border-slate-200 bg-white p-6 shadow-sm">
      <div className="border-b border-slate-100 pb-4">
        <h3 className="text-lg font-bold text-slate-900">
          Phân Tích Năng Lực Theo Nhóm Môn Học
        </h3>
        <p className="text-xs text-slate-500 mt-1">
          Hội đồng tuyển sinh Mỹ đặc biệt quan tâm tới nhóm môn phù hợp với ngành học dự định (Major Fit).
        </p>
      </div>

      <div className="mt-5 grid gap-4 md:grid-cols-3">
        {groups.map((group) => {
          const style = getGroupBadgeColor(group.groupKey);
          const percent = Math.min(100, Math.round((group.gpa4 / 4.0) * 100));

          return (
            <div
              key={group.groupKey}
              className="flex flex-col justify-between rounded-lg border border-slate-200 bg-slate-50/50 p-4 transition-all hover:border-slate-300 hover:shadow-xs"
            >
              <div>
                <div className="flex items-center justify-between">
                  <span className="text-sm font-bold text-slate-800 flex items-center gap-1.5">
                    <span>{style.icon}</span>
                    <span>{group.groupName}</span>
                  </span>
                  <span
                    className={`rounded-full px-2 py-0.5 text-[11px] font-semibold ${style.badge}`}
                  >
                    {group.subjectsCount} môn
                  </span>
                </div>

                <div className="mt-4 flex items-baseline justify-between">
                  <div>
                    <span className="text-xs text-slate-500">GPA Quy Đổi:</span>
                    <div className="text-2xl font-black text-slate-900">
                      {group.gpa4.toFixed(2)}
                      <span className="text-xs text-slate-400 font-normal"> / 4.0</span>
                    </div>
                  </div>
                  <div className="text-right">
                    <span className="text-xs text-slate-500">Điểm TB Hệ 10:</span>
                    <div className="text-base font-bold text-slate-700">
                      {group.rawAverage.toFixed(2)}
                    </div>
                  </div>
                </div>

                {/* Progress bar */}
                <div className="mt-3">
                  <div className="h-2 w-full overflow-hidden rounded-full bg-slate-200">
                    <div
                      className={`h-full rounded-full transition-all duration-500 ${style.bar}`}
                      style={{ width: `${percent}%` }}
                    />
                  </div>
                  <div className="mt-1 flex justify-between text-[11px] text-slate-400">
                    <span>0.0</span>
                    <span>{percent}% chuẩn tối đa</span>
                    <span>4.0</span>
                  </div>
                </div>
              </div>

              {/* Danh sách môn */}
              <div className="mt-4 border-t border-slate-200/60 pt-3">
                <span className="text-[11px] font-medium text-slate-500">
                  Môn học đã ghi nhận:
                </span>
                <div className="mt-1 flex flex-wrap gap-1">
                  {group.subjects.map((sub) => (
                    <span
                      key={sub}
                      className="rounded bg-white px-1.5 py-0.5 text-[10px] text-slate-600 border border-slate-200"
                    >
                      {sub}
                    </span>
                  ))}
                </div>
              </div>
            </div>
          );
        })}
      </div>
    </div>
  );
}
