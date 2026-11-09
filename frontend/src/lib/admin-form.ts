/** Giá trị số/chuỗi có thể null -> chuỗi để đặt vào ô nhập. */
export function toInputValue(value: number | string | null | undefined): string {
  if (value === null || value === undefined) {
    return "";
  }
  return String(value);
}

/** Ô nhập -> chuỗi đã cắt khoảng trắng, rỗng thì null. */
export function textOrNull(text: string): string | null {
  const trimmed = text.trim();
  return trimmed === "" ? null : trimmed;
}

/**
 * Ô nhập -> số, rỗng thì null. Chấp nhận dấu phẩy thập phân (6,5).
 * Trả về NaN nếu không phải số, để form báo lỗi trước khi gửi.
 */
export function numberOrNull(text: string): number | null {
  const trimmed = text.trim();
  if (trimmed === "") {
    return null;
  }
  return Number(trimmed.replace(",", "."));
}

/** Tên các ô có giá trị không phải số. */
export function findNotNumbers(values: Record<string, number | null>): string[] {
  const invalid: string[] = [];
  for (const [field, value] of Object.entries(values)) {
    if (value !== null && Number.isNaN(value)) {
      invalid.push(field);
    }
  }
  return invalid;
}
