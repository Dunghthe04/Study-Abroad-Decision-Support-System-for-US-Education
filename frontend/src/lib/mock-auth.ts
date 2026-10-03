import { ApiError } from "@/lib/api";
import type { AuthResponse, LoginRequest, RegisterRequest, UserRole, UserStatus } from "@/types/api";

// Cờ bật/tắt mock API. Khi backend Người A xong task A3, chỉ cần đổi thành false để dùng API thật.
export const USE_MOCK_AUTH = true;

const delay = (ms: number) => new Promise((resolve) => setTimeout(resolve, ms));

interface StoredUser {
  id: string;
  email: string;
  password: string;
  fullName: string;
  role: UserRole;
  status: UserStatus;
}

interface AttemptRecord {
  count: number;
  lockedUntil: number | null; // timestamp (ms)
}

// 1. Danh sách tài khoản mẫu ban đầu (Có sẵn mật khẩu để đăng nhập ngay)
export const DEFAULT_MOCK_ACCOUNTS: StoredUser[] = [
  {
    id: "usr_student_01",
    email: "student@gmail.com",
    password: "Password123",
    fullName: "Nguyễn Văn An (Học sinh)",
    role: "student",
    status: "Active",
  },
  {
    id: "usr_parent_01",
    email: "parent@gmail.com",
    password: "Password123",
    fullName: "Trần Thị Mai (Phụ huynh)",
    role: "parent",
    status: "Active",
  },
  {
    id: "usr_center_01",
    email: "center@gmail.com",
    password: "Password123",
    fullName: "Tư Vấn Du Học Á Âu",
    role: "center",
    status: "PendingApproval", // Trạng thái chờ Admin duyệt
  },
  {
    id: "usr_center_02",
    email: "approved_center@gmail.com",
    password: "Password123",
    fullName: "Tổ Chức Giáo Dục IvyPath",
    role: "center",
    status: "Active",
  },
];

// Helper: Lấy danh sách tài khoản từ Mock Database (localStorage)
function getMockDatabase(): StoredUser[] {
  if (typeof window === "undefined") return DEFAULT_MOCK_ACCOUNTS;

  const raw = localStorage.getItem("usas_mock_db_users");
  if (!raw) {
    localStorage.setItem("usas_mock_db_users", JSON.stringify(DEFAULT_MOCK_ACCOUNTS));
    return DEFAULT_MOCK_ACCOUNTS;
  }

  try {
    return JSON.parse(raw) as StoredUser[];
  } catch {
    localStorage.setItem("usas_mock_db_users", JSON.stringify(DEFAULT_MOCK_ACCOUNTS));
    return DEFAULT_MOCK_ACCOUNTS;
  }
}

// Helper: Lưu danh sách tài khoản
function saveMockDatabase(users: StoredUser[]) {
  if (typeof window !== "undefined") {
    localStorage.setItem("usas_mock_db_users", JSON.stringify(users));
  }
}

// Helper: Đọc bản ghi đếm số lần nhập sai của từng email
function getAttemptRecord(email: string): AttemptRecord {
  if (typeof window === "undefined") return { count: 0, lockedUntil: null };

  const raw = localStorage.getItem(`usas_lock_${email.toLowerCase()}`);
  if (!raw) return { count: 0, lockedUntil: null };

  try {
    return JSON.parse(raw) as AttemptRecord;
  } catch {
    return { count: 0, lockedUntil: null };
  }
}

// Helper: Lưu bản ghi số lần nhập sai
function saveAttemptRecord(email: string, record: AttemptRecord) {
  if (typeof window !== "undefined") {
    localStorage.setItem(`usas_lock_${email.toLowerCase()}`, JSON.stringify(record));
  }
}

/**
 * Giả lập API Đăng nhập POST /api/v1/auth/login theo đúng logic thật:
 * 1. Kiểm tra tài khoản có đang bị khóa 15 phút hay không (mã 423)
 * 2. Tìm tài khoản trong Database
 * 3. Nếu sai mật khẩu: Tăng biến đếm (1/5, 2/5, ...). Đủ 5 lần -> KHÓA THẬT 15 phút
 * 4. Nếu đúng: Reset biến đếm về 0, lưu session và trả về 200
 */
export async function mockLogin(data: LoginRequest): Promise<AuthResponse> {
  await delay(500); // Giả lập độ trễ mạng

  const email = data.email.trim().toLowerCase();
  const attempt = getAttemptRecord(email);
  const now = Date.now();

  // BƯỚC 1: Kiểm tra xem tài khoản có đang trong thời gian bị khóa hay không
  if (attempt.lockedUntil && now < attempt.lockedUntil) {
    const remainingMs = attempt.lockedUntil - now;
    const remainingMins = Math.ceil(remainingMs / 60000);
    throw new ApiError(
      423,
      `Tài khoản đã bị tạm khóa 15 phút do nhập sai mật khẩu 5 lần liên tiếp. Vui lòng thử lại sau ${remainingMins} phút.`,
    );
  }

  // Nếu đã hết hạn khóa 15 phút -> Tự động mở khóa và reset lượt thử
  if (attempt.lockedUntil && now >= attempt.lockedUntil) {
    attempt.count = 0;
    attempt.lockedUntil = null;
    saveAttemptRecord(email, attempt);
  }

  // BƯỚC 2: Tìm tài khoản trong CSDL mẫu
  const users = getMockDatabase();
  const user = users.find((u) => u.email.toLowerCase() === email);

  // BƯỚC 3: Kiểm tra thông tin đăng nhập
  const isMatch = user && user.password === data.password;

  if (!isMatch) {
    // Tăng số lần thử sai
    attempt.count = (attempt.count || 0) + 1;

    // Đạt mốc 5 lần sai -> KHÓA THẬT 15 PHÚT
    if (attempt.count >= 5) {
      attempt.lockedUntil = now + 15 * 60 * 1000; // 15 phút tính bằng miligiây
      saveAttemptRecord(email, attempt);
      throw new ApiError(
        423,
        "Bạn đã nhập sai mật khẩu quá 5 lần liên tiếp. Tài khoản đã bị tạm khóa trong vòng 15 phút.",
      );
    }

    // Chưa đủ 5 lần -> Báo lỗi 401 kèm số lần thử còn lại
    saveAttemptRecord(email, attempt);
    const remainingTries = 5 - attempt.count;
    throw new ApiError(
      401,
      `Email hoặc mật khẩu không chính xác. Bạn còn ${remainingTries} lần thử trước khi tài khoản bị khóa 15 phút.`,
    );
  }

  // BƯỚC 4: Đăng nhập thành công -> Reset số lần đếm sai về 0
  saveAttemptRecord(email, { count: 0, lockedUntil: null });

  const sessionUser: AuthResponse = {
    id: user.id,
    email: user.email,
    fullName: user.fullName,
    role: user.role,
    status: user.status,
  };

  if (typeof window !== "undefined") {
    localStorage.setItem("usas_mock_session", JSON.stringify(sessionUser));
  }

  return sessionUser;
}

/**
 * Giả lập API Đăng ký POST /api/v1/auth/register:
 * - Lưu người dùng mới vào Mock Database (localStorage)
 * - Khi đăng ký xong tài khoản mới, bạn có thể dùng chính email/mật khẩu vừa tạo để đăng nhập thật!
 */
export async function mockRegister(data: RegisterRequest): Promise<AuthResponse> {
  await delay(600);

  const users = getMockDatabase();
  const emailLower = data.email.trim().toLowerCase();

  // Kiểm tra trùng email (Mã 409)
  const isExisting = users.some((u) => u.email.toLowerCase() === emailLower);
  if (isExisting) {
    throw new ApiError(409, "Email này đã được sử dụng. Vui lòng chọn email khác.");
  }

  // Tạo người dùng mới và lưu vào cơ sở dữ liệu mẫu
  const newUser: StoredUser = {
    id: `usr_${Math.random().toString(36).substring(2, 9)}`,
    email: data.email.trim(),
    password: data.password,
    fullName: data.fullName.trim(),
    role: data.role,
    status: data.role === "center" ? "PendingApproval" : "Active",
  };

  users.push(newUser);
  saveMockDatabase(users);

  return {
    id: newUser.id,
    email: newUser.email,
    fullName: newUser.fullName,
    role: newUser.role,
    status: newUser.status,
  };
}

/**
 * Giả lập API Lấy thông tin phiên GET /api/v1/auth/me
 */
export async function mockGetMe(): Promise<AuthResponse | null> {
  await delay(150);

  if (typeof window === "undefined") return null;

  const saved = localStorage.getItem("usas_mock_session");
  if (!saved) {
    throw new ApiError(401, "Chưa đăng nhập.");
  }

  try {
    return JSON.parse(saved) as AuthResponse;
  } catch {
    localStorage.removeItem("usas_mock_session");
    throw new ApiError(401, "Phiên đăng nhập không hợp lệ.");
  }
}

/**
 * Giả lập API Đăng xuất POST /api/v1/auth/logout
 */
export async function mockLogout(): Promise<void> {
  await delay(200);
  if (typeof window !== "undefined") {
    localStorage.removeItem("usas_mock_session");
  }
}

/**
 * Hàm tiện ích: Reset toàn bộ Database mẫu và xóa tất cả trạng thái khóa (nếu cần bắt đầu lại)
 */
export function resetMockDatabase() {
  if (typeof window !== "undefined") {
    localStorage.removeItem("usas_mock_db_users");
    localStorage.removeItem("usas_mock_session");
    // Xóa tất cả các khóa attempts
    Object.keys(localStorage)
      .filter((k) => k.startsWith("usas_lock_"))
      .forEach((k) => localStorage.removeItem(k));
  }
}
