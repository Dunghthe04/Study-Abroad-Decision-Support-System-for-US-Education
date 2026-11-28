import type { NextConfig } from "next";

// In development the browser calls /api/* on the Next.js dev server, which forwards to the .NET API.
// In production Nginx routes /api/* straight to the API container, so no rewrite is needed.
const devApiTarget = process.env.API_PROXY_TARGET;

// Header bảo mật cho trang web. Bản deploy chính thức (Cloudflare → Azure Container Apps) không đi qua Nginx,
// nên Next.js tự gắn; nội dung CSP chuyển nguyên từ deploy/nginx/snippets/csp.conf (đã thử trên Docker local 27/11).
// script-src/style-src cần 'unsafe-inline' vì Next.js App Router chèn script inline và chart.tsx chèn thẻ <style>.
// img-src mở https: vì ảnh trong chat tư vấn lấy từ link bất kỳ trong kho tri thức; blob: cho ảnh xem trước; data: cho icon emoji-picker.
// frame-src blob: cho minh chứng ngày sinh dạng PDF admin xem trong iframe. connect-src 'self': trình duyệt chỉ gọi /api cùng tên miền.
const contentSecurityPolicy = [
  "default-src 'self'",
  "script-src 'self' 'unsafe-inline'",
  "style-src 'self' 'unsafe-inline'",
  "img-src 'self' data: blob: https:",
  "frame-src 'self' blob:",
  "font-src 'self'",
  "connect-src 'self'",
  "frame-ancestors 'self'",
  "object-src 'none'",
  "base-uri 'self'",
  "form-action 'self'",
].join("; ");

const securityHeaders = [
  { key: "X-Content-Type-Options", value: "nosniff" },
  { key: "X-Frame-Options", value: "SAMEORIGIN" },
  { key: "Referrer-Policy", value: "strict-origin-when-cross-origin" },
  { key: "Permissions-Policy", value: "camera=(), microphone=(), geolocation=(), payment=(), usb=()" },
  // next dev cần eval cho Fast Refresh và chạy http, nên CSP + HSTS chỉ bật ở bản build production
  // CSP đang ở chế độ Report-Only (chỉ báo vào Console, không chặn) cho lần deploy Azure đầu tiên.
  // Thử hết các trang trên web thật, Console không có cảnh báo CSP thì đổi key thành "Content-Security-Policy" để chặn thật.
  ...(process.env.NODE_ENV === "production"
    ? [
        { key: "Content-Security-Policy-Report-Only", value: contentSecurityPolicy },
        { key: "Strict-Transport-Security", value: "max-age=31536000" },
      ]
    : []),
];

const nextConfig: NextConfig = {
  output: "standalone",
  poweredByHeader: false,
  // Cho phép mở dev server từ máy khác trong mạng LAN (chỉ có tác dụng khi chạy next dev)
  allowedDevOrigins: ["192.168.*.*"],
  images: {
    remotePatterns: [
      {
        protocol: "https",
        hostname: "res.cloudinary.com",
      },
    ],
  },
  async headers() {
    return [{ source: "/(.*)", headers: securityHeaders }];
  },
  async rewrites() {
    return devApiTarget ? [{ source: "/api/:path*", destination: `${devApiTarget}/api/:path*` }] : [];
  },
  // Dev proxy mặc định cắt request sau 30 giây; gợi ý trường chờ AI viết giải thích, máy chỉ có CPU mất tới khoảng 9 phút
  experimental: { proxyTimeout: 600_000, proxyClientMaxBodySize: "21mb" },
};

export default nextConfig;
