"use client";

// Thẻ so sánh từ hai trường trong chat: bảng số liệu thật từ cơ sở dữ liệu (code dựng) và, tách riêng, nhận xét do AI viết.
// Hai phần có nhãn khác nhau để học sinh biết phần nào hệ thống tra cứu, phần nào AI diễn đạt.

import { SparklesIcon } from "lucide-react";
import type { SchoolCompareData } from "@/types/recommendation";
import { RememberedLine } from "./RememberedLine";

const NO_DATA = "chưa có số liệu";

export function SchoolCompareCard({
  data,
  remember = false,
}: {
  data: SchoolCompareData;
  remember?: boolean;
}) {
  return (
    <div className="mt-2.5 space-y-3 rounded-xl border border-line bg-surface p-3 text-sm">
      <p className="text-[13px] font-bold text-brand">
        So sánh {data.schools.map((s) => s.name).join(" và ")}
      </p>

      {/* Cuộn ngang khi khung hẹp, cột đầu (tên mục) giữ cố định để vẫn đọc được số */}
      <div className="overflow-x-auto">
        <table className="w-full min-w-[320px] border-collapse text-left text-xs">
          <thead>
            <tr className="border-b border-line align-bottom">
              <th className="w-24 py-1.5 pr-2 text-xs font-medium text-ink-2">
                Mục
              </th>
              {data.schools.map((school) => (
                <th
                  key={school.code}
                  className="px-2 py-1.5 text-[12px] font-bold text-foreground"
                >
                  {school.name}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {data.rows.map((row) => (
              <RowCells
                key={row.field}
                row={row}
                columns={data.schools.length}
              />
            ))}
          </tbody>
        </table>
      </div>

      {data.aiNote && (
        <div className="space-y-1.5 rounded-lg bg-brand-wash/50 p-2.5">
          <p className="inline-flex items-center gap-1 text-xs font-semibold text-brand">
            <SparklesIcon className="size-3.5" />
            AI nhận xét
          </p>
          <p className="text-xs leading-relaxed whitespace-pre-wrap text-foreground">
            {data.aiNote}
          </p>
          <p className="text-xs text-ink-3">
            Nhận xét do AI viết từ số liệu ở bảng trên, chỉ để tham khảo, không
            thay lời khuyên theo hồ sơ của bạn.
          </p>
        </div>
      )}

      {remember && (
        <RememberedLine
          about={`${data.schools.map((s) => s.name).join(" và ")} · ${data.rows.map((r) => r.label.toLowerCase()).join(", ")}`}
          hint="Bạn hỏi tiếp, ví dụ “còn học bổng thì sao?” hoặc “cái nào rẻ hơn?”."
        />
      )}
      {data.verify && (
        <p className="text-xs text-ink-3">
          Chi phí và hạn nộp có thể thay đổi, bạn nên kiểm tra lại trên trang
          chính thức của trường.
        </p>
      )}
    </div>
  );
}

function RowCells({
  row,
  columns,
}: {
  row: SchoolCompareData["rows"][number];
  columns: number;
}) {
  return (
    <>
      <tr className="border-b border-line/60 align-top">
        <th
          scope="row"
          className="py-2 pr-2 text-xs font-medium text-ink-2"
        >
          {row.label}
        </th>
        {row.values.map((value, i) => (
          <td
            key={i}
            className={
              value === NO_DATA
                ? "px-2 py-2 text-ink-3"
                : "px-2 py-2 font-semibold text-foreground"
            }
          >
            {value}
          </td>
        ))}
      </tr>
      {row.gap && (
        <tr className="border-b border-line/60">
          <td colSpan={columns + 1} className="pb-2 text-xs text-ink-2">
            {row.gap}
          </td>
        </tr>
      )}
    </>
  );
}
