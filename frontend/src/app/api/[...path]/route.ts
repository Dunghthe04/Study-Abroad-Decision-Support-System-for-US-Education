import { NextRequest, NextResponse } from "next/server";

const targetBase = process.env.API_INTERNAL_URL ?? "http://localhost:5080";

async function proxy(request: NextRequest, { params }: { params: Promise<{ path: string[] }> }) {
  const { path } = await params;
  const targetUrl = new URL(`/api/${path.join("/")}`, targetBase);
  targetUrl.search = request.nextUrl.search;

  const headers = new Headers(request.headers);
  headers.set("host", new URL(targetBase).host);

  const init: RequestInit = {
    method: request.method,
    headers,
    redirect: "manual",
  };

  if (request.method !== "GET" && request.method !== "HEAD" && request.body) {
    init.body = request.body;
    // @ts-expect-error duplex is required in Node.js runtime for stream bodies
    init.duplex = "half";
  }

  const response = await fetch(targetUrl.toString(), init);

  const resHeaders = new Headers(response.headers);
  return new NextResponse(response.body, {
    status: response.status,
    statusText: response.statusText,
    headers: resHeaders,
  });
}

export const GET = proxy;
export const POST = proxy;
export const PUT = proxy;
export const PATCH = proxy;
export const DELETE = proxy;
export const HEAD = proxy;
