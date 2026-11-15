import { AlertCircleIcon } from "lucide-react";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import type { EnglishNotice } from "@/types/recommendation";

// Điểm IELTS viết theo kiểu Việt: 6.5 → "6,5"
const ielts = (n: number) => n.toFixed(1).replace(".", ",");

interface EnglishNoticeBannerProps {
  notice: EnglishNotice;
  empty: boolean; // danh sách rỗng: không còn trường nào để hiện
}

// Vì sao một số trường không có trong danh sách (mức tiếng Anh tối thiểu của trường cao hơn điểm của học sinh từ 1 band,
// hoặc chưa có chứng chỉ) và cần đạt bao nhiêu để mở thêm trường. Số liệu do backend tính; ở đây chỉ dựng câu.
export function EnglishNoticeBanner({ notice, empty }: EnglishNoticeBannerProps) {
  const { hidden, noCertificate, ladder } = notice;
  const ladderText = ladder.map((step) => `${ielts(step.ielts)} → ${step.eligible} trường`).join(" · ");

  // Không còn trường nào, hoặc chưa có chứng chỉ nào: đây là điều chặn cả danh sách nên giữ khối cảnh báo rõ ràng
  if (empty || noCertificate) {
    const title = noCertificate ? "Bạn chưa có chứng chỉ tiếng Anh" : "Điểm tiếng Anh hiện tại chưa đủ để nộp vào trường nào";
    const text = noCertificate
      ? `Phần lớn trường yêu cầu IELTS, TOEFL hoặc Duolingo, bạn cần thi trước khi nộp. Có ${hidden} trường chưa hiển thị vì thiếu chứng chỉ này.`
      : "Điểm tiếng Anh hiện tại của bạn chưa đủ để nộp vào trường nào theo tiêu chí bạn chọn. Bạn cần cải thiện tiếng Anh.";
    return (
      <Alert variant="warning">
        <AlertCircleIcon className="size-4" />
        <AlertTitle>{title}</AlertTitle>
        <AlertDescription className="space-y-1 text-xs">
          <p>{text}</p>
          {ladder.length > 0 && <p>Số trường đủ điều kiện nếu bạn đạt IELTS: {ladderText}.</p>}
        </AlertDescription>
      </Alert>
    );
  }

  // Chỉ một số trường bị ẩn: một dòng nhỏ, không phải một nhóm của danh sách. Bấm mới hiện cách mở thêm trường
  return (
    <details className="rounded-lg border border-line bg-surface px-3 py-2 text-xs text-ink-2">
      <summary className="cursor-pointer">
        {hidden} trường chưa hiển thị vì tiếng Anh (mức tối thiểu của trường cao hơn điểm của bạn từ 1 band).{" "}
        {ladder.length > 0 && <span className="font-semibold text-brand">Xem cách mở thêm</span>}
      </summary>
      {ladder.length > 0 && <p className="mt-1.5">Số trường đủ điều kiện nếu bạn đạt IELTS: {ladderText}.</p>}
    </details>
  );
}
