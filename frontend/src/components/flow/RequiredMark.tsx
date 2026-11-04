/** Sao đỏ sau nhãn ô bắt buộc. Trình đọc màn hình đọc "(bắt buộc)" thay vì "dấu sao" */
export function RequiredMark() {
  return (
    <>
      <span aria-hidden className="text-destructive font-bold">
        *
      </span>
      <span className="sr-only">(bắt buộc)</span>
    </>
  );
}
