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

  const title = noCertificate
    ? "Bạn chưa có chứng chỉ tiếng Anh"
    : empty
      ? "Điểm tiếng Anh hiện tại chưa đủ để nộp vào trường nào"
      : "Một số trường chưa hiển thị vì tiếng Anh";

  const body = noCertificate
    ? `Phần lớn trường yêu cầu IELTS, TOEFL hoặc Duolingo, bạn cần thi trước khi nộp. Có ${hidden} trường chưa hiển thị vì thiếu chứng chỉ này.`
    : empty
      ? "Điểm tiếng Anh hiện tại của bạn chưa đủ để nộp vào trường nào theo tiêu chí bạn chọn. Bạn cần cải thiện tiếng Anh."
      : `Có ${hidden} trường chưa hiển thị vì mức tiếng Anh tối thiểu của trường cao hơn điểm của bạn từ 1 band trở lên.`;

  return (
    <Alert variant="warning">
      <AlertCircleIcon className="size-4" />
      <AlertTitle>{title}</AlertTitle>
      <AlertDescription className="space-y-1 text-xs">
        <p>{body}</p>
        {ladder.length > 0 && (
          <p>
            Số trường đủ điều kiện nếu bạn đạt IELTS:{" "}
            {ladder.map((step) => `${ielts(step.ielts)} → ${step.eligible} trường`).join(" · ")}.
          </p>
        )}
      </AlertDescription>
    </Alert>
  );
}
