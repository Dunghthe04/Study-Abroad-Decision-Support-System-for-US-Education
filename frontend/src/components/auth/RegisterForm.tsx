"use client";

import { useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { ApiError, apiFetch } from "@/lib/api";
import { USE_MOCK_AUTH, mockRegister } from "@/lib/mock-auth";
import type { AuthResponse, RegisterRequest, UserRole } from "@/types/api";

const ROLES: { id: UserRole; title: string; desc: string; icon: string }[] = [
  {
    id: "student",
    title: "Học sinh / Sinh viên",
    desc: "Tìm kiếm học bổng, lộ trình du học",
    icon: "🎓",
  },
  {
    id: "parent",
    title: "Phụ huynh",
    desc: "Đồng hành và định hướng cho con",
    icon: "👨‍👩‍👧",
  },
  {
    id: "center",
    title: "Trung tâm tư vấn",
    desc: "Cung cấp dịch vụ tư vấn du học",
    icon: "🏢",
  },
];

export function RegisterForm() {
  const router = useRouter();

  // Form states
  const [role, setRole] = useState<UserRole>("student");
  const [fullName, setFullName] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [showPassword, setShowPassword] = useState(false);
  const [parentAcknowledged, setParentAcknowledged] = useState(false);

  // Client validation error states
  const [fullNameError, setFullNameError] = useState("");
  const [emailError, setEmailError] = useState("");
  const [passwordError, setPasswordError] = useState("");
  const [parentAcknowledgedError, setParentAcknowledgedError] = useState("");

  // API & submission states
  const [isLoading, setIsLoading] = useState(false);
  const [apiError, setApiError] = useState<{ message: string; status?: number } | null>(null);
  const [registerSuccess, setRegisterSuccess] = useState<AuthResponse | null>(null);

  // Client-side validation function
  const validate = (): boolean => {
    let isValid = true;
    setFullNameError("");
    setEmailError("");
    setPasswordError("");
    setParentAcknowledgedError("");
    setApiError(null);

    // 1. Kiểm tra Họ và tên
    if (!fullName.trim()) {
      setFullNameError("Vui lòng nhập họ và tên của bạn.");
      isValid = false;
    }

    // 2. Kiểm tra Email
    const trimmedEmail = email.trim();
    if (!trimmedEmail) {
      setEmailError("Vui lòng nhập địa chỉ email.");
      isValid = false;
    } else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(trimmedEmail)) {
      setEmailError("Địa chỉ email không đúng định dạng (vd: name@example.com).");
      isValid = false;
    }

    // 3. Kiểm tra Mật khẩu (Quy tắc nhóm: >= 8 ký tự)
    if (!password) {
      setPasswordError("Vui lòng nhập mật khẩu.");
      isValid = false;
    } else if (password.length < 8) {
      setPasswordError("Mật khẩu phải có tối thiểu 8 ký tự.");
      isValid = false;
    }

    // 4. Kiểm tra điều kiện Học sinh (bắt buộc tick Phụ huynh đã biết)
    if (role === "student" && !parentAcknowledged) {
      setParentAcknowledgedError(
        "Học sinh bắt buộc phải có sự xác nhận đồng ý của phụ huynh để đăng ký.",
      );
      isValid = false;
    }

    return isValid;
  };

  const handleSubmit = async (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    if (!validate()) return;

    setIsLoading(true);
    setApiError(null);

    const payload: RegisterRequest = {
      fullName: fullName.trim(),
      email: email.trim(),
      password,
      role,
      parentAcknowledged: role === "student" ? parentAcknowledged : undefined,
    };

    try {
      let result: AuthResponse;

      if (USE_MOCK_AUTH) {
        result = await mockRegister(payload);
      } else {
        result = await apiFetch<AuthResponse>("/api/v1/auth/register", {
          method: "POST",
          body: JSON.stringify(payload),
          credentials: "include",
        });
      }

      setRegisterSuccess(result);

      // Chuyển sang trang đăng nhập sau 1.5 giây
      setTimeout(() => {
        router.push("/login");
      }, 1500);
    } catch (err: unknown) {
      if (err instanceof ApiError) {
        setApiError({ message: err.message, status: err.status });
      } else {
        setApiError({
          message: "Không thể kết nối tới máy chủ. Vui lòng thử lại sau.",
        });
      }
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className="w-full max-w-lg rounded-2xl border border-slate-200 bg-white p-8 shadow-sm">
      <div className="mb-6 text-center">
        <h1 className="text-2xl font-bold tracking-tight text-slate-900">
          Tạo tài khoản mới
        </h1>
        <p className="mt-1 text-sm text-slate-500">
          Đăng ký để nhận hỗ trợ tư vấn du học Mỹ tốt nhất
        </p>
      </div>

      {/* Thông báo lỗi từ API (vd: 409 Email đã tồn tại) */}
      {apiError && (
        <div
          role="alert"
          className="mb-5 flex items-start gap-3 rounded-lg border border-red-200 bg-red-50 p-4 text-sm text-red-900"
        >
          <span className="text-lg">⚠️</span>
          <div className="flex-1">
            <p className="font-semibold">
              {apiError.status === 409
                ? "Email đã tồn tại (Mã 409)"
                : apiError.status === 400
                  ? "Dữ liệu không hợp lệ (Mã 400)"
                  : "Đăng ký không thành công"}
            </p>
            <p className="mt-0.5">{apiError.message}</p>
          </div>
        </div>
      )}

      {/* Thông báo đăng ký thành công */}
      {registerSuccess && (
        <div className="mb-5 rounded-lg border border-emerald-200 bg-emerald-50 p-4 text-sm text-emerald-900">
          <p className="font-semibold">Đăng ký tài khoản thành công!</p>
          <p className="mt-1">
            {registerSuccess.role === "center" ? (
              <span>
                Tài khoản trung tâm <strong>{registerSuccess.fullName}</strong> đang ở trạng thái{" "}
                <span className="font-semibold text-amber-700">Chờ Admin duyệt</span>. Đang chuyển sang trang đăng nhập...
              </span>
            ) : (
              <span>
                Chào mừng <strong>{registerSuccess.fullName}</strong>. Đang chuyển hướng sang trang đăng nhập...
              </span>
            )}
          </p>
        </div>
      )}

      <form onSubmit={handleSubmit} noValidate className="space-y-5">
        {/* Bước 1: Chọn vai trò (3 lựa chọn) */}
        <div>
          <label className="block text-xs font-semibold uppercase tracking-wider text-slate-700">
            Bạn tham gia với vai trò nào?
          </label>
          <div className="mt-2 grid grid-cols-1 gap-2.5 sm:grid-cols-3">
            {ROLES.map((r) => {
              const isSelected = role === r.id;
              return (
                <button
                  key={r.id}
                  type="button"
                  onClick={() => {
                    setRole(r.id);
                    setParentAcknowledgedError("");
                  }}
                  disabled={isLoading || !!registerSuccess}
                  className={`flex flex-col items-center rounded-xl border p-3 text-center transition ${
                    isSelected
                      ? "border-slate-900 bg-slate-50 ring-2 ring-slate-900"
                      : "border-slate-200 bg-white hover:border-slate-300"
                  }`}
                >
                  <span className="text-2xl">{r.icon}</span>
                  <span className="mt-1.5 text-xs font-semibold text-slate-900">
                    {r.title}
                  </span>
                  <span className="mt-0.5 text-[11px] leading-tight text-slate-500">
                    {r.desc}
                  </span>
                </button>
              );
            })}
          </div>
        </div>

        {/* Thông báo đặc biệt khi chọn Trung tâm (Task B2 requirement) */}
        {role === "center" && (
          <div className="flex items-start gap-2.5 rounded-lg border border-blue-200 bg-blue-50/80 p-3 text-xs text-blue-900">
            <span className="text-base">ℹ️</span>
            <div>
              <p className="font-semibold">Lưu ý cho Trung tâm tư vấn:</p>
              <p className="mt-0.5 text-blue-800">
                Tài khoản trung tâm sau khi đăng ký sẽ ở trạng thái <strong>Chờ Admin duyệt (PendingApproval)</strong> để xác thực thông tin khảo sát trước khi chính thức hoạt động trên hệ thống.
              </p>
            </div>
          </div>
        )}

        {/* Họ và tên */}
        <div>
          <label
            htmlFor="fullName"
            className="block text-xs font-semibold uppercase tracking-wider text-slate-700"
          >
            {role === "center" ? "Tên trung tâm tư vấn" : "Họ và tên"}
          </label>
          <input
            id="fullName"
            name="fullName"
            type="text"
            autoComplete="name"
            value={fullName}
            onChange={(e) => {
              setFullName(e.target.value);
              if (fullNameError) setFullNameError("");
            }}
            disabled={isLoading || !!registerSuccess}
            placeholder={role === "center" ? "Công ty Tư vấn Du học ABC" : "Nguyễn Văn A"}
            className={`mt-1 w-full rounded-lg border px-3.5 py-2.5 text-sm outline-none transition ${
              fullNameError
                ? "border-red-300 bg-red-50/30 focus:border-red-500 focus:ring-2 focus:ring-red-200"
                : "border-slate-300 bg-white hover:border-slate-400 focus:border-slate-900 focus:ring-2 focus:ring-slate-200"
            }`}
          />
          {fullNameError && (
            <p className="mt-1 text-xs text-red-600">{fullNameError}</p>
          )}
        </div>

        {/* Email */}
        <div>
          <label
            htmlFor="email"
            className="block text-xs font-semibold uppercase tracking-wider text-slate-700"
          >
            Email
          </label>
          <input
            id="email"
            name="email"
            type="email"
            autoComplete="email"
            value={email}
            onChange={(e) => {
              setEmail(e.target.value);
              if (emailError) setEmailError("");
            }}
            disabled={isLoading || !!registerSuccess}
            placeholder="tenban@gmail.com"
            className={`mt-1 w-full rounded-lg border px-3.5 py-2.5 text-sm outline-none transition ${
              emailError
                ? "border-red-300 bg-red-50/30 focus:border-red-500 focus:ring-2 focus:ring-red-200"
                : "border-slate-300 bg-white hover:border-slate-400 focus:border-slate-900 focus:ring-2 focus:ring-slate-200"
            }`}
          />
          {emailError && (
            <p className="mt-1 text-xs text-red-600">{emailError}</p>
          )}
        </div>

        {/* Mật khẩu (yêu cầu >= 8 ký tự) */}
        <div>
          <label
            htmlFor="password"
            className="block text-xs font-semibold uppercase tracking-wider text-slate-700"
          >
            Mật khẩu (Tối thiểu 8 ký tự)
          </label>
          <div className="relative mt-1">
            <input
              id="password"
              name="password"
              type={showPassword ? "text" : "password"}
              autoComplete="new-password"
              value={password}
              onChange={(e) => {
                setPassword(e.target.value);
                if (passwordError) setPasswordError("");
              }}
              disabled={isLoading || !!registerSuccess}
              placeholder="Tối thiểu 8 ký tự"
              className={`w-full rounded-lg border px-3.5 py-2.5 pr-12 text-sm outline-none transition ${
                passwordError
                  ? "border-red-300 bg-red-50/30 focus:border-red-500 focus:ring-2 focus:ring-red-200"
                  : "border-slate-300 bg-white hover:border-slate-400 focus:border-slate-900 focus:ring-2 focus:ring-slate-200"
              }`}
            />
            <button
              type="button"
              tabIndex={-1}
              onClick={() => setShowPassword(!showPassword)}
              className="absolute right-3 top-1/2 -translate-y-1/2 text-xs font-medium text-slate-500 hover:text-slate-800"
            >
              {showPassword ? "Ẩn" : "Hiện"}
            </button>
          </div>
          {passwordError && (
            <p className="mt-1 text-xs text-red-600">{passwordError}</p>
          )}
        </div>

        {/* Ô tick bắt buộc khi vai trò là Học sinh (Task B2 requirement) */}
        {role === "student" && (
          <div className="rounded-lg border border-slate-200 bg-slate-50/60 p-3.5">
            <label className="flex items-start gap-3 cursor-pointer">
              <input
                type="checkbox"
                checked={parentAcknowledged}
                onChange={(e) => {
                  setParentAcknowledged(e.target.checked);
                  if (parentAcknowledgedError) setParentAcknowledgedError("");
                }}
                disabled={isLoading || !!registerSuccess}
                className="mt-0.5 h-4 w-4 rounded border-slate-300 text-slate-900 focus:ring-slate-900"
              />
              <span className="text-xs leading-relaxed text-slate-700">
                <strong className="text-slate-900">Phụ huynh/Người giám hộ đã biết:</strong>{" "}
                Tôi xác nhận rằng bố mẹ hoặc người giám hộ đã được thông báo và đồng ý cho tôi tìm hiểu thông tin du học trên nền tảng USAS.
              </span>
            </label>
            {parentAcknowledgedError && (
              <p className="mt-2 text-xs font-medium text-red-600">
                {parentAcknowledgedError}
              </p>
            )}
          </div>
        )}

        {/* Nút Đăng ký */}
        <button
          type="submit"
          disabled={isLoading || !!registerSuccess}
          className="mt-3 flex w-full items-center justify-center rounded-lg bg-slate-900 px-4 py-2.5 text-sm font-semibold text-white shadow transition hover:bg-slate-800 focus:outline-none focus:ring-2 focus:ring-slate-900 focus:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-60"
        >
          {isLoading ? (
            <span className="flex items-center gap-2">
              <span className="inline-block h-4 w-4 animate-spin rounded-full border-2 border-white border-t-transparent" />
              Đang tạo tài khoản...
            </span>
          ) : (
            "Đăng ký tài khoản"
          )}
        </button>
      </form>

      {/* Điều hướng về Đăng nhập */}
      <p className="mt-6 text-center text-sm text-slate-600">
        Đã có tài khoản?{" "}
        <Link
          href="/login"
          className="font-semibold text-slate-900 underline hover:text-slate-700"
        >
          Đăng nhập ngay
        </Link>
      </p>

      {/* Hộp gợi ý test mock Task B2 */}
      {USE_MOCK_AUTH && (
        <details className="mt-6 rounded-lg border border-dashed border-slate-200 bg-slate-50/70 p-3 text-xs text-slate-600">
          <summary className="cursor-pointer font-medium text-slate-700 hover:text-slate-900">
            🧪 Gợi ý kiểm thử Mock API (Task B2)
          </summary>
          <ul className="mt-2 list-disc space-y-1 pl-4 text-slate-600">
            <li>
              <strong>Đăng ký bình thường (201):</strong> Điền đầy đủ thông tin hợp lệ (mật khẩu $\ge 8$ ký tự).
            </li>
            <li>
              <strong>Kiểm tra Học sinh:</strong> Bắt buộc tick checkbox <em>"Phụ huynh đã biết"</em> mới cho tạo.
            </li>
            <li>
              <strong>Kiểm tra Trung tâm:</strong> Hiện thông báo và trả về trạng thái <code>PendingApproval</code> (chờ duyệt).
            </li>
            <li>
              <strong>Lỗi trùng Email (409):</strong> Thử nhập email là <code>exists@gmail.com</code>.
            </li>
          </ul>
        </details>
      )}
    </div>
  );
}
