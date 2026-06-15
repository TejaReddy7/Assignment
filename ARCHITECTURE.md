# IPL Franchise Store — Architecture

> Senior Backend Engineer assessment. Brief: a single-stop ecommerce site for IPL franchise merchandise (jerseys, caps, flags, autographed photos, etc.).
> Evaluation axes: **Design, Testability, Accuracy, Flexibility, Readability**.

---

## 1. Goals & Non-Goals

### Goals
- Cover all five mandated features (product list, product details, search, cart, order history) end-to-end.
- Demonstrate **senior-level architecture**: Clean Architecture, DDD-lite, CQRS, Result-based error handling.
- Production-grade cross-cutting: authn/authz, validation, caching, observability, rate limiting, idempotency.
- **Reviewer can clone & run in two commands** — zero external infra required.
- High test coverage on the parts that matter (domain rules, command handlers, HTTP contract).

### Non-Goals
- Pixel-perfect UI. Frontend is a thin React shell to prove the API is real.
- Real payment integration — mocked at the boundary behind an interface.
- Multi-region deployment / cloud-native auto-scale. We design *for* it but don't deploy it.

---

## 2. Tech Stack

| Layer | Choice | Rationale |
| --- | --- | --- |
| Runtime | **.NET 10 (LTS)** | Current LTS, top-tier perf, minimal APIs + native AOT-ready. |
| Web API | **ASP.NET Core** controllers | Versionable, OpenAPI out of the box. |
| ORM | **EF Core 10** | Strong LINQ + migrations; provider-agnostic. |
| Database | **SQLite** dev default, **SQL Server** prod | Reviewer runs with zero install; one config line swaps to SQL Server. |
| Mediator | **MediatR** | Enables CQRS + pipeline behaviors (validation, logging, caching). |
| Validation | **FluentValidation** | Declarative, testable, automatically wired via pipeline behavior. |
| Mapping | Hand-rolled extension methods | Avoids AutoMapper magic; trivially testable. |
| Auth | **ASP.NET Core Identity + JWT** | Standard, role-ready (Customer / Admin). |
| Logging | **Serilog** (console + rolling file) | Structured logs, easy correlation IDs. |
| Caching | `IMemoryCache` behind `ICacheService` | Distributed-cache-ready interface (Redis swap = one DI line). |
| Rate limiting | Built-in `AddRateLimiter` (fixed window) | Demonstrates DDoS awareness without extra deps. |
| API Docs | **Swashbuckle (Swagger UI)** | Live, JWT-aware. |
| Frontend | **React 18 + TypeScript + Vite** | Minimum UI to exercise the API. |
| Testing | **xUnit + FluentAssertions + Moq + WebApplicationFactory** | Industry standard. |
| Arch tests | **NetArchTest** | Enforces layer boundaries automatically in CI. |

---

## 3. Architectural Style — Clean Architecture + CQRS

```
┌──────────────────────────────────────────────────────────┐
│                         API (Web)                        │  ← Controllers, Middleware, DI composition
│                    IplStore.Api                          │
└────────────┬─────────────────────────────────┬───────────┘
             │                                 │
             ▼                                 ▼
┌──────────────────────────┐      ┌──────────────────────────┐
│       Application        │      │      Infrastructure      │
│   IplStore.Application   │◄────►│  IplStore.Infrastructure │  ← EF Core, Identity, JWT, Cache impl
│  (CQRS, DTOs, Validators)│      │                          │
└────────────┬─────────────┘      └────────────┬─────────────┘
             │                                 │
             ▼                                 ▼
┌──────────────────────────────────────────────────────────┐
│                         Domain                            │ ← Entities, Value Objects, Errors, Events
│                    IplStore.Domain                        │   NO dependencies. Pure C#.
└──────────────────────────────────────────────────────────┘
                              │
                              ▼
┌──────────────────────────────────────────────────────────┐
│                         Shared                            │ ← Result<T>, PagedResult, Error
│                    IplStore.Shared                        │
└──────────────────────────────────────────────────────────┘
```

**Dependency rule:** arrows point *inward only*. Domain knows nothing about EF/HTTP/Identity. Infrastructure depends on Application interfaces (Dependency Inversion).

### Why CQRS here?
- Reads dominate ecommerce (browse:checkout ≈ 100:1). Splitting them lets us cache reads aggressively and keep writes transactional.
- Each use case is one `IRequest` + one `Handler` → 1:1 mapping with assessment criteria, easy to test, easy to grow.

---

## 4. Solution Layout

```
IplStore/
├── src/
│   ├── IplStore.Api/                  → ASP.NET Core entry, controllers, DI, middleware
│   ├── IplStore.Application/          → CQRS commands/queries, DTOs, validators, behaviors
│   ├── IplStore.Domain/               → Entities, value objects, domain errors, events
│   ├── IplStore.Infrastructure/       → EF Core, Identity, JWT, cache, seed
│   └── IplStore.Shared/               → Result<T>, Error, PagedResult, pagination params
├── tests/
│   ├── IplStore.Domain.Tests/         → Pure domain rule tests
│   ├── IplStore.Application.Tests/    → Handler tests with in-memory deps
│   ├── IplStore.Api.IntegrationTests/ → WebApplicationFactory + SQLite in-memory
│   └── IplStore.Architecture.Tests/   → NetArchTest layer rules
├── client/                            → React + Vite + TS
├── docs/
│   └── api-examples.http              → REST Client / IntelliJ HTTP sample requests
├── README.md
├── ARCHITECTURE.md  (this file)
├── IMPLEMENTATION_PLAN.md
└── IplStore.sln
```

---

## 5. Domain Model (DDD-lite)

### Bounded contexts (informal — single service for assessment)
- **Catalog** — Franchise, Product, ProductVariant, Category, Review
- **Cart** — Cart, CartItem
- **Ordering** — Order, OrderItem, Coupon, Payment (mock)
- **Identity** — ApplicationUser, Role

### Core aggregates

```
Franchise (aggregate root)
  ├─ Id, Name, ShortCode (e.g. "MI", "CSK"), City, PrimaryColor, FoundedYear
  └─ Products: 1..*

Product (aggregate root)
  ├─ Id, Name, Slug, Description, Type (Jersey|Cap|Flag|Autograph|Accessory)
  ├─ FranchiseId, BasePrice (Money VO), ImageUrl, IsActive
  ├─ Variants: 0..*  (size / color combinations with their own SKU & stock)
  ├─ AverageRating, ReviewCount (denormalized for read perf, updated via domain events)
  └─ RowVersion (concurrency token)

ProductVariant
  ├─ Id, ProductId, Sku, Size?, Color?, PriceOverride? (Money), StockQuantity
  └─ RowVersion (optimistic concurrency on stock writes)

Cart (aggregate root, one per customer)
  ├─ Id, CustomerId, Items: 0..*
  ├─ Subtotal, Discount, Total (computed)
  └─ ETag for client-side concurrency

Order (aggregate root, immutable after placement)
  ├─ Id, OrderNumber (human-readable: ORD-2026-000123)
  ├─ CustomerId, Items: 1..*, ShippingAddress (VO), Status, PlacedAtUtc
  ├─ Subtotal, DiscountAmount, ShippingFee, Total
  ├─ CouponCode?, IdempotencyKey
  └─ State machine: Pending → Confirmed → Shipped → Delivered  (or Cancelled)

Coupon
  ├─ Code (unique), Type (Percentage|FixedAmount), Value
  ├─ MinOrderValue?, MaxDiscount?, ExpiresAtUtc, UsageLimit, UsedCount
  └─ Validity rules enforced in domain method

Review
  ├─ Id, ProductId, CustomerId, Rating (1..5), Title, Body, CreatedAtUtc
  └─ One review per (Product, Customer) — DB unique index
```

### Value Objects
- `Money(Amount, Currency)` — arithmetic operators, currency check, immutable.
- `Address(Line1, Line2?, City, State, PostalCode, Country)` — equality by components.
- `Slug` — normalized URL-friendly string with validation.

### Domain errors (no exceptions for control flow)
Every domain method that can fail returns `Result<T>` or `Result`. Errors carry a `Code` + `Description`. The API layer maps codes → HTTP status via a small switch.

---

## 6. Cross-cutting Concerns

| Concern | Implementation |
| --- | --- |
| **Validation** | `ValidationBehavior<TReq,TRes>` runs FluentValidation before handler; returns `ValidationError` result. |
| **Logging** | `LoggingBehavior` logs request name + elapsed ms. Serilog enrichers add CorrelationId, UserId. |
| **Caching** | `CachingBehavior` keys on `ICacheableQuery.CacheKey`; default TTL 60s for catalog reads. |
| **Idempotency** | `[Idempotent]` filter on cart/order POSTs reads `Idempotency-Key` header, stores result hash in `IdempotencyRecord` table for 24h. |
| **Concurrency** | `RowVersion` on Product/Variant; EF Core throws → handler returns `ConcurrencyConflict` error → API returns 409. |
| **AuthN** | JWT bearer; access token 15 min, refresh token 7 days (rotation on use). |
| **AuthZ** | Role policies (`Customer`, `Admin`) + resource-based check for "is this *your* order/cart". |
| **Rate limiting** | Per-IP fixed window: 100 req/min on read endpoints, 20 req/min on write. |
| **Pagination** | `PagedResult<T>` with `Page`, `PageSize`, `TotalCount`. Default page size 20, hard max 100. |
| **Exception handling** | `ExceptionHandlingMiddleware` converts unhandled exceptions → RFC 7807 `ProblemDetails`. |
| **API versioning** | `Asp.Versioning` — URL segment `/api/v1/...` from day one. |
| **Soft delete** | `ISoftDeletable` + EF Core global query filter. |
| **Auditing** | `IAuditable` (`CreatedAtUtc`, `UpdatedAtUtc`, `CreatedBy`, `UpdatedBy`) populated in `SaveChangesAsync` override. |

---

## 7. Key Design Decisions (with the *why*)

1. **Result pattern over exceptions** — Exceptions are for *exceptional* things; "coupon expired" is a normal business outcome. Result keeps handler code linear and HTTP mapping centralized.
2. **CQRS without separate read models** — Same DbContext for reads and writes, but reads use `AsNoTracking()` + projection to DTO. Avoids over-engineering while reaping 80% of the benefit.
3. **Idempotency on cart/order writes** — Network retries on mobile are real. Without this, a double-tap on "Place order" charges twice.
4. **Inventory check inside the order transaction** — Stock is decremented in the same DB transaction as the order insert, with optimistic concurrency. Prevents oversell. Documented limitation: under heavy concurrent load you'd move to reservation + outbox/saga.
5. **Money as a value object** — Stops the "I added a decimal to an int" class of bugs and makes the currency story explicit.
6. **No AutoMapper** — Reviewers see exactly what maps to what; no reflection surprises.
7. **SQLite default** — One less thing for the reviewer to install. EF Core abstracts the dialect. Swap to SQL Server via `appsettings.json`.
8. **Tests organized by layer** — Mirrors `src/` layout. Reviewer can open `*.Tests` and immediately see what's covered.

---

## 8. Scalability Story (interview talking points)

| Bottleneck | Mitigation in code | Production next step |
| --- | --- | --- |
| Hot product reads | `CachingBehavior` + 60s TTL | Swap `IMemoryCache` for Redis (`IDistributedCache`), already abstracted. |
| Write throughput | CQRS split | Read replicas; route queries to replica via `DbContextFactory`. |
| Search at scale | Indexed columns + EF Core full-text-ready | Project events to Elasticsearch / Azure Cognitive Search. |
| Order spikes (IPL final!) | Stateless API → horizontal scale, idempotency keys | Add API gateway, queue order placement (RabbitMQ/Service Bus), confirm async. |
| Inventory contention | Optimistic concurrency + retry | Move to reservation TTL + saga. |
| Reporting load | n/a in MVP | CDC → data warehouse (no impact on OLTP). |

---

## 9. Security Checklist

- [x] Passwords hashed by Identity (PBKDF2).
- [x] JWT signed (HS256), short-lived access + rotating refresh.
- [x] HTTPS-only cookies for refresh token.
- [x] Authorization checked on every mutating endpoint + resource ownership.
- [x] FluentValidation guards all inputs (defense against malformed payloads).
- [x] EF Core parameterized queries everywhere → SQL injection neutralized.
- [x] CORS allow-list (only the React origin in dev).
- [x] Rate limiting (DoS soft guard).
- [x] No PII in logs (Serilog destructuring rules).
- [x] Secrets via `dotnet user-secrets` in dev, env vars in prod.

---

## 10. Observability

- **Structured logs:** Serilog → console + `logs/log-.txt` (rolling, 7 days).
- **Correlation:** middleware assigns `X-Correlation-Id` per request; flows through logs.
- **Health checks:** `/health/live` and `/health/ready` (DB ping).
- **Swagger UI:** `/swagger` with JWT auth flow built in.

---

## 11. Out of Scope (deliberately) — but designed for

- Email/SMS (interface present, console implementation only).
- Real payment gateway (`IPaymentGateway` mocked; success/failure switchable in config).
- Multi-currency / FX.
- Recommendation engine (ML).
- Full-text search engine.
- CI/CD pipeline (chosen "local-only" runtime per requirements).

Each is one DI registration away — that's the point of the abstractions.
