// Liveness probe for Docker / Nginx. Kept outside /api because Nginx routes /api/* to the .NET API.
export function GET() {
  return Response.json({ status: "ok" });
}
