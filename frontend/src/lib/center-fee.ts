// Rút gọn mức phí tham khảo của trung tâm: "20 – 90 triệu", "500 triệu – 1,2 tỷ", "Từ 10 triệu"
function vndShort(value: number): { num: string; unit: string } {
  const fmt = (n: number) => n.toLocaleString("vi-VN", { maximumFractionDigits: 1 });
  if (value >= 1e9) return { num: fmt(value / 1e9), unit: "tỷ" };
  if (value >= 1e6) return { num: fmt(value / 1e6), unit: "triệu" };
  return { num: value.toLocaleString("vi-VN"), unit: "" };
}

// "20 – 90 triệu", "500 triệu – 1,2 tỷ", "Từ 10 triệu"
export function feeHeadline(min: number | null | undefined, max: number | null | undefined): string | null {
  if (min != null && max != null) {
    const a = vndShort(min);
    const b = vndShort(max);
    return a.unit === b.unit ? `${a.num} – ${b.num} ${b.unit}`.trim() : `${a.num} ${a.unit} – ${b.num} ${b.unit}`.trim();
  }
  if (min != null) return `Từ ${vndShort(min).num} ${vndShort(min).unit}`.trim();
  if (max != null) return `Đến ${vndShort(max).num} ${vndShort(max).unit}`.trim();
  return null;
}
