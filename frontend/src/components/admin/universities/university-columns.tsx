import { Badge } from "@/components/ui/badge";
import type { DataTableColumn } from "@/components/admin/shared/DataTable";
import { controlLabel, formatAcceptanceRate, formatRetrievedAt } from "@/lib/admin-university-display";
import { studyLevelLabel } from "@/lib/study-levels";
import type { AdminUniversityListItem } from "@/types/admin-university";
import type { UsStateOption } from "@/types/profile-flow";

export function universityColumns(
  states: UsStateOption[],
  onSelect: (university: AdminUniversityListItem) => void,
): DataTableColumn<AdminUniversityListItem>[] {
  const stateNames = new Map(states.map((state) => [state.code.toUpperCase(), state.name]));

  return [
    {
      id: "logo",
      label: "Logo",
      required: true,
      headerClassName: "w-20 min-w-20 max-w-20 text-center",
      cellClassName: "w-20 min-w-20 max-w-20 p-1 text-center",
      render: (university) => university.logoUrl ? (
        <img
          key={university.logoUrl}
          src={university.logoUrl}
          alt={`Logo ${university.name}`}
          className="mx-auto h-8 w-14 object-contain"
          onError={(event) => { event.currentTarget.style.visibility = "hidden"; }}
        />
      ) : <span className="text-xs text-muted-foreground">—</span>,
    },
    {
      id: "code",
      label: "Mã",
      required: true,
      headerClassName: "w-16",
      cellClassName: "font-mono text-xs",
      render: (university) => university.code,
    },
    {
      id: "name",
      label: "Tên trường",
      required: true,
      render: (university) => (
        <>
          <button
            type="button"
            className="text-left font-medium text-brand underline-offset-2 hover:underline focus-visible:outline-2 focus-visible:outline-ring"
            onClick={(event) => {
              event.stopPropagation();
              onSelect(university);
            }}
          >
            {university.name}
          </button>
          {university.city && <div className="text-xs text-muted-foreground">{university.city}</div>}
        </>
      ),
    },
    {
      id: "state",
      label: "Bang",
      render: (university) => {
        if (!university.state) {
          return "—";
        }
        const state = university.state.trim();
        const fullName = stateNames.get(state.toUpperCase());
        if (fullName) {
          return fullName;
        }
        return state.length > 2 ? state : "—";
      },
    },
    {
      id: "control",
      label: "Loại",
      render: (university) => controlLabel(university.control),
    },
    {
      id: "acceptanceRate",
      label: "Tỷ lệ nhận",
      headerClassName: "text-right",
      cellClassName: "text-right",
      render: (university) => formatAcceptanceRate(university.acceptanceRate),
    },
    {
      id: "studyLevels",
      label: "Bậc học",
      render: (university) => (
        <div className="flex flex-wrap gap-1">
          {university.studyLevels.length === 0 && <span className="text-muted-foreground">—</span>}
          {university.studyLevels.map((level) => (
            <Badge key={level} variant="outline">{studyLevelLabel(level)}</Badge>
          ))}
        </div>
      ),
    },
    {
      id: "isActive",
      label: "Hiển thị",
      render: (university) => (
        <Badge variant={university.isActive ? "brand" : "outline"}>
          {university.isActive ? "Đang hiển thị" : "Đang ẩn"}
        </Badge>
      ),
    },
    {
      id: "latestRetrievedAt",
      label: "Dữ liệu cập nhật",
      render: (university) => formatRetrievedAt(university.latestRetrievedAt),
    },
  ];
}
