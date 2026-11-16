import type { AlgorithmParameter, MethodologyParameters } from "@/types/methodology";

// Backend trả số thập phân bằng dấu chấm; trang tiếng Việt hiển thị dấu phẩy (0.25 thành 0,25)
function formatValue(value: string): string {
  return value.replace(/(\d)\.(\d)/g, "$1,$2");
}

// Giữ nguyên thứ tự backend trả về, gom các tham số cùng bước thành một nhóm
function groupByStep(parameters: AlgorithmParameter[]): { step: string; items: AlgorithmParameter[] }[] {
  const groups = new Map<string, AlgorithmParameter[]>();
  for (const p of parameters) {
    const items = groups.get(p.step);
    if (items) items.push(p);
    else groups.set(p.step, [p]);
  }
  return Array.from(groups, ([step, items]) => ({ step, items }));
}

function stepTitle(step: string): string {
  return /^B\d+$/.test(step) ? `Bước ${step}` : step;
}

// Bảng tham số đang chạy của thuật toán (UC-89). Không có thao tác sửa: muốn đổi thì gửi yêu cầu cho nhóm phát triển.
export function ParameterTable({ data }: { data: MethodologyParameters | null }) {
  if (!data) {
    return (
      <p className="text-sm text-muted-foreground">
        Chưa tải được bảng tham số. Bạn tải lại trang sau ít phút nhé.
      </p>
    );
  }

  return (
    <div className="space-y-6">
      <p className="text-sm text-muted-foreground">
        Phiên bản thuật toán: <span className="text-numeric font-semibold text-foreground">{data.algorithmVersion}</span>. Các giá trị dưới đây
        lấy trực tiếp từ hệ thống đang chạy. Mỗi lần đổi một giá trị, phiên bản thuật toán được tăng và đi qua kiểm thử; bảng này chỉ để xem, không sửa tại đây.
      </p>
      {groupByStep(data.parameters).map((group) => (
        <div key={group.step}>
          <h3 className="text-sm font-semibold text-foreground">{stepTitle(group.step)}</h3>
          <div className="mt-2 overflow-x-auto">
            <table className="w-full min-w-[520px] text-sm">
              <thead>
                <tr className="border-b border-line text-left text-xs text-muted-foreground">
                  <th className="py-2.5 pr-3 font-semibold">Tham số</th>
                  <th className="py-2.5 pr-3 font-semibold">Giá trị</th>
                  <th className="py-2.5 font-semibold">Ý nghĩa</th>
                </tr>
              </thead>
              <tbody>
                {group.items.map((p) => (
                  <tr key={p.key} className="border-b border-line last:border-0 align-top">
                    <td className="py-3 pr-3 text-foreground">{p.name}</td>
                    <td className="text-numeric py-3 pr-3 font-medium text-foreground">{formatValue(p.value)}</td>
                    <td className="py-3 text-muted-foreground">{p.meaning}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      ))}
    </div>
  );
}
