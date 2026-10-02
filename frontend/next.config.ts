import type { NextConfig } from "next";

// In development the browser calls /api/* on the Next.js dev server, which forwards to the .NET API.
// In production Nginx routes /api/* straight to the API container, so no rewrite is needed.
const devApiTarget = process.env.API_PROXY_TARGET;

const nextConfig: NextConfig = {
  output: "standalone",
  poweredByHeader: false,
  async rewrites() {
    return devApiTarget ? [{ source: "/api/:path*", destination: `${devApiTarget}/api/:path*` }] : [];
  },
};

export default nextConfig;
