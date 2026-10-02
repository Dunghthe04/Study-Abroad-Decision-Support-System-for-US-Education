# Frontend (Next.js + TypeScript)

```bash
cp .env.example .env.local
npm install
npm run dev        # http://localhost:3000
npm run lint
npm run build
```

- `src/app/` – routes (App Router): `/`, `/advisor`, `/centers`, `/healthz`
- `src/components/` – UI components
- `src/lib/api.ts` – fetch wrapper for the .NET API
- `src/types/api.ts` – DTO types mirroring the API
