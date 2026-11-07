"use client";

// Dòng nói hệ thống đang nhớ gì để học sinh hỏi tiếp ("còn học bổng?", "còn Yale?") mà không phải nhắc lại. Chỉ hiện ở lượt cuối.
// Nội dung lấy từ chính dữ liệu của thẻ (tên trường đã khớp, mục đang xem), nên học sinh thấy hệ thống hiểu đúng hay chưa.

export function RememberedLine({
  about,
  hint,
}: {
  about: string;
  hint: string;
}) {
  return (
    <p className="text-[11px] text-ink-3">
      Đang nói về: <b className="font-semibold text-ink-2">{about}</b>. {hint}
    </p>
  );
}
