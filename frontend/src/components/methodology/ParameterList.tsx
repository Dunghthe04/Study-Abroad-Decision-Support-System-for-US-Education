import { Badge } from "@/components/ui/badge";
import type { AlgorithmParameter, MethodologyParameters } from "@/types/methodology";

// Các phần của bảng, theo thứ tự hệ thống xử lý một trường. Backend gắn mỗi tham số vào một mã bước (steps);
// tham số có mã bước chưa khai báo ở đây vẫn được hiện, ở phần "Khác" cuối bảng, để không bao giờ làm mất tham số.
const SECTIONS: { id: string; title: string; description: string; steps: string[] }[] = [
  {
    id: "filter",
    title: "Bước 1 · Lọc trường",
    description: "Trường không đạt các điều kiện này sẽ không xuất hiện trong gợi ý của bạn.",
    steps: ["B4", "B5"],
  },
  {
    id: "position",
    title: "Bước 2a · Hồ sơ của bạn đứng ở đâu",
    description:
      "Hệ thống so GPA và điểm thi của bạn với sinh viên đã nhập học của trường, rồi cho một vị trí từ 0% đến 100%.",
    steps: ["B6", "B7", "B8"],
  },
  {
    id: "activities",
    title: "Bước 2b · Cách tính bậc ngoại khóa",
    description: "Hoạt động và giải thưởng bạn khai được tính thành một bậc từ 1 đến 5, rồi nhích vị trí của bạn lên hoặc xuống một chút.",
    steps: ["Bậc ngoại khóa"],
  },
  {
    id: "group",
    title: "Bước 2c · Xếp vào nhóm Khó vào, Vừa sức, An toàn hơn",
    description: "Các ngưỡng quyết định một trường nằm ở nhóm nào so với vị trí của bạn.",
    steps: ["B9"],
  },
  {
    id: "rank",
    title: "Bước 3 · Sắp thứ tự và chọn trường",
    description: "Trong mỗi nhóm, trường hợp với bạn hơn đứng trước.",
    steps: ["B10", "B11"],
  },
  {
    id: "goal",
    title: "Thêm · Cần gì để vào một trường",
    description: "Cách hệ thống tìm mức điểm tối thiểu bạn cần đạt để một trường lên nhóm cao hơn.",
    steps: ["Cần gì để vào"],
  },
];

// Backend trả số thập phân bằng dấu chấm; trang tiếng Việt hiển thị dấu phẩy (0.25 thành 0,25)
function formatValue(value: string): string {
  return value.replace(/(\d)\.(\d)/g, "$1,$2");
}

// Giá trị gồm nhiều ý (ngăn cách bằng " · " hoặc "; ") thì tách thành từng thẻ nhỏ cho dễ nhìn
function splitValue(value: string): string[] {
  const parts = formatValue(value)
    .split(/ · |; /)
    .map((part) => part.trim())
    .filter(Boolean);
  return parts.length > 1 ? parts : [];
}

function ValueCell({ value }: { value: string }) {
  const chips = splitValue(value);
  if (chips.length === 0) {
    return <div className="text-numeric text-sm font-semibold text-foreground">{formatValue(value)}</div>;
  }
  return (
    <ul className="flex flex-wrap gap-1.5">
      {chips.map((chip) => (
        <li key={chip} className="text-numeric rounded-md border border-line bg-ground px-2 py-1 text-xs font-medium text-foreground">
          {chip}
        </li>
      ))}
    </ul>
  );
}

function ParameterRow({ parameter }: { parameter: AlgorithmParameter }) {
  return (
    <div className="grid gap-2 px-4 py-3.5 sm:grid-cols-[minmax(0,1fr)_minmax(0,1fr)] sm:gap-6">
      <div className="min-w-0">
        <div className="text-sm font-semibold text-foreground">{parameter.name}</div>
        <p className="mt-0.5 text-sm text-muted-foreground">{parameter.meaning}</p>
      </div>
      <div className="min-w-0 sm:pt-0.5">
        <ValueCell value={parameter.value} />
      </div>
    </div>
  );
}

// Gom tham số vào các phần theo mã bước; trong một phần giữ thứ tự mã bước đã khai báo, rồi thứ tự backend trả về
function buildSections(parameters: AlgorithmParameter[]) {
  const known = new Set(SECTIONS.flatMap((s) => s.steps));
  const sections = SECTIONS.map((section) => ({
    ...section,
    items: section.steps.flatMap((step) => parameters.filter((p) => p.step === step)),
  }));
  const others = parameters.filter((p) => !known.has(p.step));
  if (others.length > 0) {
    sections.push({ id: "other", title: "Khác", description: "", steps: [], items: others });
  }
  return sections.filter((section) => section.items.length > 0);
}

// Danh sách tham số đang chạy của thuật toán (UC-89). Chỉ xem, không có thao tác sửa.
export function ParameterList({ data }: { data: MethodologyParameters | null }) {
  if (!data) {
    return <p className="text-sm text-muted-foreground">Chưa tải được bảng tham số. Bạn tải lại trang sau ít phút nhé.</p>;
  }

  return (
    <div className="space-y-8">
      <div className="space-y-2">
        <div className="flex flex-wrap items-center gap-2 text-sm text-foreground">
          Phiên bản thuật toán
          <Badge variant="outline" className="text-numeric">
            {data.algorithmVersion}
          </Badge>
        </div>
        <p className="text-sm text-muted-foreground">
          Mỗi dòng gồm tên tham số, ý nghĩa bằng lời và giá trị đang dùng. Bạn không cần nhớ các con số này: chúng giúp bạn kiểm tra vì sao một trường nằm ở nhóm đó.
          Các giá trị do hệ thống đọc trực tiếp từ bản đang chạy; mỗi lần đổi một giá trị, phiên bản thuật toán được tăng và đi qua kiểm thử. Bảng này chỉ để xem.
        </p>
      </div>

      {buildSections(data.parameters).map((section) => (
        <section key={section.id} aria-labelledby={`param-${section.id}`}>
          <h3 id={`param-${section.id}`} className="text-base font-semibold text-foreground">
            {section.title}
          </h3>
          {section.description && <p className="mt-0.5 text-sm text-muted-foreground">{section.description}</p>}
          <div className="mt-3 divide-y divide-line overflow-hidden rounded-lg border border-line">
            {section.items.map((parameter) => (
              <ParameterRow key={parameter.key} parameter={parameter} />
            ))}
          </div>
        </section>
      ))}
    </div>
  );
}
