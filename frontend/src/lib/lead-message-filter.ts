/**
 * [USAS-2053 / UC-74] Tiện ích kiểm tra bộ lọc nội dung phía client (đồng bộ với LeadMessageContentFilter.cs).
 * Chỉ dùng để cảnh báo sớm cho người dùng trước khi gửi; kiểm duyệt chính thức được thực hiện ở backend.
 */
export const MAX_LEAD_MESSAGE_LENGTH = 4000;

const INVISIBLE_FORMAT_REGEX = /[\u200B-\u200D\u2060\uFEFF\u00AD]/g;

const VIETNAM_PHONE_REGEX =
  /(?<!\d)(?:\(?(?:\+|00)?84\)?[\s.\-_*]*|\(?0[\s.\-_*]*)(?:2|[35789])[\s.\-_*]*\d\)?(?:[\s.\-_*]*\d){7,8}(?!\d)/;

const HOTLINE_PHONE_REGEX = /(?<!\d)(?:1800|1900)[\s.\-]?\d{4,6}(?!\d)/;

const INTERNATIONAL_PHONE_REGEX =
  /(?<!\d)\+(?!84\b)\d{1,3}[\s.\-]?\(?\d{3}\)?[\s.\-]?\d{3}[\s.\-]?\d{4}(?!\d)/;

const KEYWORD_PHONE_REGEX =
  /\b(?:sdt|so dien thoai|dien\s*thoai|tel|hotline|zalo|whatsapp|viber|telegram|phone|call)\b\s*[:\-]?\s*(?:\+?\d[\d\s.\-*]{7,15}\d)/gi;

const DIGIT_WORD_PATTERN =
  "khong|linh|mot|hai|ba|bon|nam|lam|sau|bay|tam|chin|zero|one|two|three|four|five|six|seven|eight|nine";
const DIGIT_TOKEN = `(?:\\d|(?<!\\p{L})(?:${DIGIT_WORD_PATTERN})(?!\\p{L}))`;
const SPELLED_DIGIT_RUN_REGEX = new RegExp(
  `${DIGIT_TOKEN}(?:[\\s.\\-_*,;|]*${DIGIT_TOKEN}){8,}`,
  "gu"
);

const DIGIT_WORDS: Record<string, string> = {
  khong: "0", linh: "0", zero: "0",
  mot: "1", one: "1",
  hai: "2", two: "2",
  ba: "3", three: "3",
  bon: "4", four: "4",
  nam: "5", lam: "5", five: "5",
  sau: "6", six: "6",
  bay: "7", seven: "7",
  tam: "8", eight: "8",
  chin: "9", nine: "9",
};

function collapseSpelledDigits(run: string): string {
  return (run.match(/\d|\p{L}+/gu) ?? [])
    .map((tok) => (/^\d$/.test(tok) ? tok : (DIGIT_WORDS[tok] ?? "")))
    .join("");
}

const EXTERNAL_URL_REGEX =
  /(?:https?:\/\/\S+|www\.\S+|\b(?:bit\.ly|tinyurl\.com|goo\.gl|linktr\.ee|forms\.gle|zalo\.me|zaloapp\.com|t\.me|wa\.me|m\.me|fb\.com|facebook\.com|instagram\.com|tiktok\.com|youtube\.com|youtu\.be|shopee\.vn|lazada\.vn)\/\S*|\bdocs\.google\.com\/forms\S*)/i;

const NORMALIZED_AD_PHRASES = [
  "khuyen mai",
  "khuyen mai dac biet",
  "khuyen mai hoc phi",
  "giam gia",
  "giam gia hoc phi",
  "uu dai dac biet",
  "uu dai doc quyen",
  "ma giam gia",
  "voucher",
  "tang ngay",
  "mien phi 100%",
  "cam ket dau",
  "bao dau",
  "chi hom nay",
  "nhanh tay dang ky",
  "dang ky ngay hom nay",
  "ket ban zalo",
  "lien he zalo",
  "lien he qua zalo",
  "nhan tin qua zalo",
  "nhan qua zalo",
  "add zalo",
  "ib zalo",
  "inbox zalo",
  "inbox ngay",
  "ib ngay",
  "nhan tin rieng",
  "lien he ngoai he thong",
  "giao dich ngoai nen tang",
  "chuyen khoan",
  "dat coc",
  "truy cap link",
  "click vao link",
  "ban sim",
  "cho vay",
  "vay von",
  "ca do",
  "casino",
  "xo so",
  "kiem tien online",
  "special offer",
  "limited time offer",
  "promo code",
  "discount code",
  "guaranteed admission",
  "click here",
] as const;

export interface LeadMessageDraftInspection {
  isFlagged: boolean;
  reasons: string[];
}

export function stripInvisibleChars(input: string): string {
  return input.replace(INVISIBLE_FORMAT_REGEX, "");
}

export function normalizeVietnameseForFilter(input: string): string {
  return input
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .replace(/[đĐ]/g, "d")
    .toLowerCase()
    .normalize("NFC");
}

function containsWholePhrase(normalizedText: string, phrase: string): boolean {
  let idx = normalizedText.indexOf(phrase);
  while (idx >= 0) {
    const leftOk = idx === 0 || !/[a-z0-9]/i.test(normalizedText[idx - 1]);
    const end = idx + phrase.length;
    const rightOk = end >= normalizedText.length || !/[a-z0-9]/i.test(normalizedText[end]);
    if (leftOk && rightOk) return true;
    idx = normalizedText.indexOf(phrase, idx + 1);
  }
  return false;
}

function countDigits(value: string): number {
  let count = 0;
  for (const ch of value) {
    if (ch >= "0" && ch <= "9") count += 1;
  }
  return count;
}

function containsPhoneNumber(cleanText: string, normalizedText: string): boolean {
  if (
    VIETNAM_PHONE_REGEX.test(cleanText) ||
    HOTLINE_PHONE_REGEX.test(cleanText) ||
    INTERNATIONAL_PHONE_REGEX.test(cleanText)
  ) {
    return true;
  }

  for (const run of normalizedText.matchAll(SPELLED_DIGIT_RUN_REGEX)) {
    const digits = collapseSpelledDigits(run[0]);
    if (VIETNAM_PHONE_REGEX.test(digits) || HOTLINE_PHONE_REGEX.test(digits)) return true;
  }

  KEYWORD_PHONE_REGEX.lastIndex = 0;
  let match: RegExpExecArray | null = KEYWORD_PHONE_REGEX.exec(normalizedText);
  while (match !== null) {
    if (countDigits(match[0]) >= 9) return true;
    match = KEYWORD_PHONE_REGEX.exec(normalizedText);
  }
  return false;
}

export function inspectLeadMessageDraft(content: string): LeadMessageDraftInspection {
  const sanitized = stripInvisibleChars(content).trim();
  if (!sanitized) {
    return { isFlagged: false, reasons: [] };
  }

  const normalized = normalizeVietnameseForFilter(sanitized);
  const reasons: string[] = [];

  if (containsPhoneNumber(sanitized, normalized)) {
    reasons.push("Chứa số điện thoại liên hệ");
  }

  const hasAdPhrase = NORMALIZED_AD_PHRASES.some((phrase) =>
    containsWholePhrase(normalized, phrase)
  );
  if (EXTERNAL_URL_REGEX.test(sanitized) || hasAdPhrase) {
    reasons.push("Chứa nội dung quảng cáo hoặc liên kết ngoài");
  }

  return {
    isFlagged: reasons.length > 0,
    reasons,
  };
}

export function formatLeadMessageRoleLabel(role: string): string {
  switch (role.toLowerCase()) {
    case "center":
      return "Trung tâm";
    case "moderator":
      return "Kiểm duyệt viên";
    case "admin":
      return "Quản trị viên";
    case "parent":
      return "Phụ huynh";
    case "student":
      return "Học sinh";
    default:
      return "Người dùng";
  }
}
