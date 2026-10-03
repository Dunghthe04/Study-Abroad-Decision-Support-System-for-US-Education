import { ApiError } from "@/lib/api";
import type { AuthResponse, LoginRequest, RegisterRequest } from "@/types/api";

// Cờ bật/tắt mock API. Khi backend Người A xong task A3, chỉ cần đổi thành false để dùng API thật.
export const USE_MOCK_AUTH = true;

const delay = (ms: number) => new Promise((resolve) => setTimeout(resolve, ms));

/**
 * Giả lập API Đăng nhập POST /api/v1/auth/login theo đúng contract:
 * - 200: Đăng nhập thành công -> { id, email, fullName, role }
 * - 401: Sai email hoặc mật khẩu (thử password: "wrong")
 * - 423: Tài khoản bị khóa (thử email chứa "locked" hoặc "khoa")
 */
export async function mockLogin(data: LoginRequest): Promise<AuthResponse> {
  await delay(600);

  const emailLower = data.email.toLowerCase();

  // Test case 423: Tài khoản bị khóa
  if (emailLower.includes("locked") || emailLower.includes("khoa")) {
    throw new ApiError(423, "Tài khoản của bạn đã bị tạm khóa 15 phút do nhập sai quá 5 lần.");
  }

  // Test case 401: Sai mật khẩu
  if (data.password === "wrong") {
    throw new ApiError(401, "Email hoặc mật khẩu không chính xác.");
  }

  // Giả lập role theo email (để tiện test các vai trò khác nhau)
  let role: AuthResponse["role"] = "student";
  if (emailLower.includes("parent")) role = "parent";
  if (emailLower.includes("center")) role = "center";

  const user: AuthResponse = {
    id: "usr_mock_123",
    email: data.email,
    fullName: emailLower.startsWith("admin") ? "Quản trị viên Demo" : data.email.split("@")[0].toUpperCase(),
    role,
    status: "Active",
  };

  if (typeof window !== "undefined") {
    localStorage.setItem("usas_mock_session", JSON.stringify(user));
  }

  return user;
}

/**
 * Giả lập API Đăng ký POST /api/v1/auth/register theo đúng contract:
 * - 201: Đăng ký thành công
 * - 400: Lỗi validation
 * - 409: Email đã tồn tại (thử email: "exists@gmail.com")
 */
export async function mockRegister(data: RegisterRequest): Promise<AuthResponse> {
  await delay(600);

  if (data.email.toLowerCase() === "exists@gmail.com") {
    throw new ApiError(409, "Email này đã được sử dụng. Vui lòng chọn email khác.");
  }

  return {
    id: `usr_mock_${Math.random().toString(36).substring(2, 9)}`,
    email: data.email,
    fullName: data.fullName,
    role: data.role,
    status: data.role === "center" ? "PendingApproval" : "Active",
  };
}

/**
 * Giả lập API Lấy thông tin phiên GET /api/v1/auth/me:
 * - 200: Đã đăng nhập -> trả về thông tin User
 * - 401: Chưa đăng nhập -> ném lỗi ApiError(401)
 */
export async function mockGetMe(): Promise<AuthResponse | null> {
  await delay(200);

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
 * Giả lập API Đăng xuất POST /api/v1/auth/logout:
 * - 204: Thành công -> xóa session
 */
export async function mockLogout(): Promise<void> {
  await delay(300);
  if (typeof window !== "undefined") {
    localStorage.removeItem("usas_mock_session");
  }
}

