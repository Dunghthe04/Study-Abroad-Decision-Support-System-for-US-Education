// Đăng xuất thì xóa dữ liệu hồ sơ còn nằm trong trình duyệt, để người dùng sau trên máy dùng chung
// không mở DevTools → Application mà đọc được (nhận xét của thầy 26/11, KE_HOACH_SUA_THEO_NHAN_XET_THAY_28_11.md mục A2).
// Lịch sử chat trợ lý đã được AssistantProvider xóa khi đăng xuất; ở đây xóa thêm cho chắc.

// localStorage: còn lại cả khi đóng trình duyệt
const LOCAL_KEYS = [
  "usas_flow_draft", // nháp form hồ sơ (khách điền trước khi đăng nhập)
  "usas_last_committed_draft", // bản hồ sơ đã nộp gần nhất (ProfileFlowWizard)
  "usas_user", // khóa cũ, giờ không còn ghi nhưng máy cũ có thể vẫn còn
];

// sessionStorage: chỉ trong tab, nhưng tab vẫn mở sau khi đăng xuất
const SESSION_KEYS = ["usas_flow_goals"]; // nhu cầu đã chọn ở form/chat
const SESSION_PREFIXES = [
  "usas:what-if:", // điểm giả định ở trang gợi ý trường
  "usas_assistant_chat", // lịch sử chat trợ lý
  "usas_assistant_session",
];

function removeKeys(storage: Storage, keys: string[], prefixes: string[] = []) {
  keys.forEach((key) => storage.removeItem(key));
  if (prefixes.length === 0) return;
  // Duyệt ngược vì xóa làm dời chỉ số
  for (let i = storage.length - 1; i >= 0; i--) {
    const key = storage.key(i);
    if (key && prefixes.some((prefix) => key.startsWith(prefix))) storage.removeItem(key);
  }
}

export function clearUserStorage() {
  if (typeof window === "undefined") return;
  try {
    removeKeys(window.localStorage, LOCAL_KEYS);
    removeKeys(window.sessionStorage, SESSION_KEYS, SESSION_PREFIXES);
  } catch {
    // trình duyệt chặn storage → không có gì để xóa
  }
}
