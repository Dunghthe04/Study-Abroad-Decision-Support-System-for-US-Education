# Section III.2 Performance & Scalability Design (copy from here)

Paste the text below under the heading "2. Performance & Scalability Design". Subsections 2.1 and 2.2 match the two empty headings in the report; 2.3 is optional and can be dropped if the template must stay at two subsections.

Everything below is taken from the code in the repository (backend, frontend, advisor, docker-compose.prod.yml, deploy/nginx). Where a mechanism is a known limitation, it is stated as such.

## Overview paragraph

US-DSS is read-heavy: students browse universities and centers, while writes (profiles, leads, verification requests) are small and per-user. The design therefore keeps the Backend API stateless, reads reference data through short in-memory caches, pages every list, and keeps the slow work (AI chat, OCR, scoring) in a separate Advisor service that the Backend calls with timeouts. The Backend API, the Frontend and the Advisor are separate containers (docker-compose.prod.yml), so each can be resized on its own.

## 2.1 Caching Strategy

The system uses a single cache layer: ASP.NET Core `IMemoryCache` (registered once with `AddMemoryCache()` in Program.cs). It holds small reference data that is read on almost every request and changes rarely. There is no distributed cache and no HTTP output cache.

| Cached data | Class | Key | Lifetime | Invalidation |
| :---- | :---- | :---- | :---- | :---- |
| Grade-scale configuration used for GPA conversion | GradeScaleProvider | `grade_scale_10_cache` | 10 minutes | `Invalidate()` is called when an Administrator saves the setting |
| Profile-flow metadata (dropdown options for the six-step profile wizard) | ProfileMetadataService | `profile_flow_metadata_cache` | 10 minutes | `InvalidateCache()` after the metadata changes |
| VND per USD exchange rate used by the financial profile | ExchangeRateProvider | `exchange_rate_vnd_per_usd` | 1 day (`MaxAge`); on a failed fetch the last known rate is kept for 1 hour (`RetryAfter`) before retrying | Expiry only. The HTTP call to the rate source has a 5-second timeout, so a slow source cannot block a request |

Rules applied:
- Only data that is the same for every user is cached. Personal data (profiles, leads, chat) is never cached.
- Every cached item has an expiry, so a missed invalidation can only serve stale data for at most the lifetime above.
- Counters that must be exact are not cached: the review count and average rating of a study center are computed from `center_reviews` on every read (see 2.2), which is why a new review shows up immediately.

Client side:
- The frontend API client and the advisor stream call the API with `cache: "no-store"` (frontend/src/lib/api.ts, advisor-stream.ts) so a user never sees another user's response or a stale one.
- Small static lookups are memoised once per page session in module variables (the school widget options and the US state names).

Known limitation: the in-memory cache lives inside one API process. With several API replicas each replica keeps its own copy, so an Administrator's change can take up to 10 minutes to reach every replica. If the API is scaled out, move these three entries to a shared cache (for example Redis) or shorten the lifetime.

## 2.2 Database Query Optimization

Database: PostgreSQL 16 with the pgvector extension, accessed through EF Core (Npgsql).

**Indexing**
- Every foreign-key column is the first column of at least one index (checked against all FKs in the schema).
- Composite indexes follow the real filters, for example `leads (study_center_id, status)` for a center's inbox, `leads (submitted_by_user_id, created_at)` and `leads (user_id, created_at)` for "my sent requests" sorted by time, and `center_verification_requests (status)` and `(tax_code)` for the moderator queue and the duplicate-tax-code check.
- Unique indexes enforce rules at the database level, for example `study_centers.code`.
- Vector search over the advisor knowledge base uses an HNSW index with cosine distance: `ix_knowledge_documents_embedding ... USING hnsw (embedding vector_cosine_ops)`.
- Array columns such as `study_levels` and `services` are `text[]`, so a filter like "center offers service X" is a single `Contains` on one row without a join table.

**Query patterns**
- Read-only queries use `AsNoTracking()` (about 100 call sites in the repositories), which avoids change tracking and lowers memory use.
- The large lists are paged on the server with `Skip/Take`, and the page size is limited (the reviews of one center are not paged but capped at the 50 most recent): the study-center search clamps `PageSize` to 1..100 (`Math.Clamp`), the admin AI-usage and chat-review queues default to 20 with a maximum of 100, and "my sent requests" defaults to 10. A client cannot ask for the whole table.
- Aggregates are calculated in the database in one grouped query, not row by row. For example `ApplyLiveReviewStatsAsync` loads the review count and average for all centers on the current page with a single `GROUP BY study_center_id` query, so a list of 20 centers costs one extra query instead of 20 (this removes the N+1 pattern).
- Filtering and searching run in SQL (`ILIKE` for name and city, equality on type and status), and the total count for paging is a separate `COUNT` on the same filtered query.
- The moderator center list orders pending requests first with a CASE expression on `verification_status`, then newest first, so new registrations appear on page 1 without loading the whole table.
- Admin reports (AI usage, tokens per user) are served by `AiUsageReportService`, which takes an optional date range and pages the per-user ranking.

**Connection and call limits**
- HTTP calls to the Advisor use typed `HttpClient` instances with explicit timeouts (the OCR client has its own `OcrTimeoutSeconds`, the exchange-rate client 5 seconds), so a slow AI call does not hold an API thread forever.

## 2.3 Scalability and Load Protection (optional)

- **Separate, stateless services.** Frontend (Next.js), Backend API (ASP.NET Core), Advisor (FastAPI), PostgreSQL, Ollama and Nginx run as separate containers. The API keeps no session state in memory: authentication uses a JWT and the `user_sessions` table, so more API instances can be started behind Nginx.
- **AI work is isolated.** Chat answers are streamed from the Advisor (the Advisor routes `chat.py` and `chat_flow.py` stream the response), and only the Backend can reach the Advisor, so a slow model does not block normal pages.
- **Rate limiting at two layers.**
  - Nginx: `limit_req` zones for the advisor routes and the auth routes (`auth` zone 20 requests per minute per IP, `advisor` zone 10 to 30 requests per minute with a small burst), returning HTTP 429, plus `client_max_body_size` caps (10 MB, 11 MB and 21 MB on the upload routes).
  - ASP.NET Core: named rate-limit policies for authentication, media upload and transcript upload (the transcript policy is a per-user limiter built from fixed windows).
- **Resilience.** Containers restart automatically (`restart: always`), and the database and the API have health checks, so a crashed process comes back without manual work.

Not implemented (state these as future work if the report asks): response compression tuning, an HTTP output cache for the public center and university lists, a read replica for PostgreSQL, a message queue for long AI jobs, and a shared cache for multiple API replicas.

---

Checklist before you paste it into Report4:
1. The three cache durations (10 minutes, 10 minutes, 1 day / 1 hour) are read from the code, not assumed.
2. The Nginx limits differ between `deploy/nginx/local.conf` (advisor 30 requests per minute) and `deploy/nginx/templates/default.conf.template` (advisor 10 requests per minute, the production template). Mention the production value if you quote one number.
3. The statements about JWT plus `user_sessions`, health checks and streaming come from reading the code and docker-compose.prod.yml; I did not run the stack to confirm them.
4. "Checked against all FKs" repeats the earlier schema check (59 foreign keys). The two new tables add 5 more foreign keys, all indexed by the migration, but that count was not re-run against the live database.
