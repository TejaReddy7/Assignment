# 🏏 IPL Franchise Store

A production-grade ecommerce backend for selling IPL franchise merchandise — jerseys, caps, flags, autographed photos and more — built for a senior backend engineering assessment.

The emphasis is on **backend depth**: Clean Architecture, CQRS, domain-driven design, transactional integrity, testability, and security. The React frontend is a deliberately thin shell to prove the API end-to-end.

---

## ✅ Requirements coverage

| # | Requirement | Where |
| --- | --- | --- |
| 1 | Product **list** page with prices | `GET /api/v1/products` (paged, sortable) |
| 2 | Product **details** page | `GET /api/v1/products/{slug}` |
| 3 | **Search** by name, type, franchise | `GET /api/v1/products/search` (+ facet counts) |
| 4 | **Cart** | `GET/POST/PATCH/DELETE /api/v1/cart` |
| 5 | **Order history** | `GET /api/v1/orders`, `GET /api/v1/orders/{orderNumber}` |

**Bonus features** (the differentiators): JWT auth with roles, idempotent checkout, coupons, verified-buyer reviews & ratings, wishlist, inventory reservation with optimistic concurrency, response caching, rate limiting, RFC 7807 error responses, correlation IDs, health checks, and 64 automated tests.

---

## 🧱 Architecture at a glance

Clean Architecture with the dependency rule pointing **inward**:

```
API  ──►  Application  ──►  Domain  ──►  Shared
 └──────►  Infrastructure ──┘   (implements Application interfaces)
```

- **Domain** — entities, value objects (`Money`, `Address`, `Slug`), domain events, business rules. Zero framework dependencies.
- **Application** — CQRS use cases (MediatR), DTOs, FluentValidation, pipeline behaviors (validation, logging, caching). Depends only on Domain + Shared.
- **Infrastructure** — EF Core, ASP.NET Identity, JWT, cache, mock payment gateway, seeders.
- **API** — controllers, middleware, DI composition, OpenAPI.

See **[ARCHITECTURE.md](ARCHITECTURE.md)** for the full design and **[IMPLEMENTATION_PLAN.md](IMPLEMENTATION_PLAN.md)** for the feature-by-feature build log.

### Why these choices (interview talking points)

- **Result pattern over exceptions** — business failures (`coupon expired`, `out of stock`) are normal outcomes, returned as `Result<T>` and mapped centrally to HTTP status codes. Exceptions are reserved for the truly exceptional.
- **CQRS** — reads dominate ecommerce; queries use `AsNoTracking` + projection and are cacheable, while commands stay transactional. Each use case is one request + one handler → trivially testable.
- **Idempotent checkout** — `Idempotency-Key` header guarantees a double-tapped "Place order" creates exactly one order and charges once.
- **Inventory integrity** — stock is decremented in the **same transaction** as the order insert, guarded by an optimistic-concurrency token, so we never oversell.
- **Money value object** — eliminates the "decimal vs int" class of bugs and makes currency explicit.
- **Hand-rolled mapping** — no AutoMapper; every mapping is explicit, compile-checked, and reflection-free.

---

## 🛠️ Tech stack

- **.NET 10** · ASP.NET Core Web API · **EF Core 10**
- **SQLite** by default (zero install), **SQL Server**-ready via one config switch
- **MediatR** (CQRS) · **FluentValidation** · **Serilog** · **ASP.NET Identity + JWT**
- **xUnit · FluentAssertions · Moq · NetArchTest** (64 tests)
- **React 18 + TypeScript + Vite** (minimal UI)
- OpenAPI document + **Scalar** UI at `/scalar/v1`

---

## 🚀 Run it (two terminals)

### Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 18+](https://nodejs.org) (only for the optional React UI)

> No database to install — SQLite is created and seeded automatically on first run.

### 1) Backend API

```bash
cd src/IplStore.Api
dotnet run
```

- API: `http://localhost:5080`
- Interactive API docs (Scalar): `http://localhost:5080/scalar/v1`
- Health: `http://localhost:5080/health/ready`

On startup the app applies migrations and seeds **10 IPL franchises**, ~**50 products** with variants, **2 coupons**, and two users.

### 2) React client (optional)

```bash
cd client
npm install
npm run dev
```

Open `http://localhost:5173`. The Vite dev server proxies `/api` to the backend, so no CORS setup is needed.

### Seeded logins

| Role | Email | Password |
| --- | --- | --- |
| Admin | `admin@iplstore.local` | `Admin#12345` |
| Customer | `fan@iplstore.local` | `Fan#12345` |

---

## 🧪 Tests

```bash
dotnet test
```

**64 tests, all green**, organized by layer:

| Project | Count | Focus |
| --- | --- | --- |
| `IplStore.Domain.Tests` | 39 | Pure business rules — Money math, cart totals, coupon validity, order state machine, stock |
| `IplStore.Application.Tests` | 10 | Handler behavior on a real in-memory SQLite DB — incl. payment-rollback & idempotency |
| `IplStore.Api.IntegrationTests` | 8 | Full HTTP pipeline via `WebApplicationFactory` — register → cart → order → history, cross-user authorization |
| `IplStore.Architecture.Tests` | 7 | NetArchTest guardrails — layer boundaries + naming conventions |

The architecture tests **fail the build** if someone points a dependency the wrong way — the Clean Architecture rule is enforced automatically, not just documented.

---

## 📡 Try the API

Open **[docs/api-examples.http](docs/api-examples.http)** in VS Code (with the REST Client extension) — it has ready-to-run samples for every endpoint, including login token capture, idempotent checkout, search facets, and admin operations.

A 30-second tour with `curl`:

```bash
# Browse
curl "http://localhost:5080/api/v1/products?pageSize=5"

# Search with facets
curl "http://localhost:5080/api/v1/products/search?q=jersey&franchise=MI"

# Login (capture the accessToken from the response)
curl -X POST http://localhost:5080/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"fan@iplstore.local","password":"Fan#12345"}'
```

---

## 🔐 Security

- Passwords hashed by ASP.NET Identity (PBKDF2); JWT access tokens (15 min) + rotating refresh tokens (7 days).
- Role-based authorization (`Customer` / `Admin`) **and** resource-ownership checks (you can only see your own orders/cart).
- All inputs validated by FluentValidation; EF Core parameterizes every query (no SQL injection).
- Per-IP rate limiting; CORS allow-list; structured logs with no PII.
- Frontend dependencies audited clean (`npm audit` → 0 vulnerabilities).

---

## 🗄️ Switching to SQL Server

No code changes — edit `src/IplStore.Api/appsettings.json`:

```json
{
  "Database": { "Provider": "SqlServer" },
  "ConnectionStrings": { "Default": "Server=localhost;Database=IplStore;Trusted_Connection=True;TrustServerCertificate=True" }
}
```

EF Core abstracts the dialect; the same migrations and code run against either provider.

---

## 📂 Project layout

```
src/
  IplStore.Api/             ASP.NET Core entry, controllers, middleware, DI
  IplStore.Application/     CQRS commands/queries, DTOs, validators, behaviors
  IplStore.Domain/          Entities, value objects, domain events, business rules
  IplStore.Infrastructure/  EF Core, Identity, JWT, cache, seeders
  IplStore.Shared/          Result<T>, Error, PagedResult
tests/
  IplStore.Domain.Tests/        IplStore.Application.Tests/
  IplStore.Api.IntegrationTests/ IplStore.Architecture.Tests/
client/                     React + Vite + TS minimal UI
docs/api-examples.http      Sample requests for every endpoint
ARCHITECTURE.md             Full design write-up
IMPLEMENTATION_PLAN.md      Feature-by-feature build log
```

---

## 📈 Scaling beyond the assessment

Each abstraction is one DI registration away from production scale:

- `ICacheService` (in-memory) → Redis `IDistributedCache`
- `IPaymentGateway` (mock) → Stripe / Razorpay
- Search → project to Elasticsearch / Azure Cognitive Search
- Order spikes (IPL final!) → stateless API + queue-based async order placement; idempotency keys already make retries safe
- Inventory contention → reservation TTL + outbox/saga (documented in `ARCHITECTURE.md`)
