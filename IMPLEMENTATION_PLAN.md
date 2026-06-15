# Implementation Plan — Feature by Feature

Each phase is **independently shippable**. Tick boxes as we go. Tests are written **alongside** each feature, not deferred.

---

## Phase 0 — Foundation
- [ ] .NET 10 solution + 5 projects (Api, Application, Domain, Infrastructure, Shared)
- [ ] 4 test projects (Domain, Application, Api.Integration, Architecture)
- [ ] Shared: `Result<T>`, `Error`, `PagedResult<T>`, `PaginationParams`
- [ ] Solution-wide `Directory.Build.props`: nullable enable, treat warnings as errors, LangVersion latest

## Phase 1 — Domain
- [ ] Value Objects: `Money`, `Address`, `Slug`
- [ ] Aggregates: `Franchise`, `Product`, `ProductVariant`, `Cart`, `CartItem`, `Order`, `OrderItem`, `Coupon`, `Review`
- [ ] Enums: `ProductType`, `OrderStatus`, `CouponType`
- [ ] Domain errors (`DomainErrors` static class — codes + descriptions)
- [ ] **Tests:** Money arithmetic, currency mismatch guard, Cart total calc, Order state transitions, Coupon validity

## Phase 2 — Infrastructure
- [ ] `AppDbContext` + EF configurations (one file per aggregate)
- [ ] Migrations + apply on startup in dev
- [ ] Seed: 10 IPL franchises (MI, CSK, RCB, KKR, SRH, DC, RR, PBKS, GT, LSG) + ~40 sample products across types
- [ ] `IUnitOfWork` (already covered by `DbContext.SaveChangesAsync`)
- [ ] Identity setup (`ApplicationUser`, roles seeded: `Customer`, `Admin`; default admin user)
- [ ] `JwtTokenService`
- [ ] `MemoryCacheService` implementing `ICacheService`
- [ ] `ConsoleEmailSender` implementing `IEmailSender`
- [ ] `MockPaymentGateway` implementing `IPaymentGateway`

## Phase 3 — Application Scaffolding
- [ ] MediatR registration
- [ ] Pipeline behaviors: `ValidationBehavior`, `LoggingBehavior`, `CachingBehavior`
- [ ] FluentValidation auto-registration
- [ ] DTOs + mapping extension methods
- [ ] `ICurrentUser` interface (read from `HttpContext.User`)

## Phase 4 — API Scaffolding
- [ ] `Program.cs` composition
- [ ] Serilog config
- [ ] JWT auth + Swagger JWT button
- [ ] Global exception → ProblemDetails middleware
- [ ] Correlation ID middleware
- [ ] Rate limiting policies
- [ ] CORS for `http://localhost:5173`
- [ ] Health checks: `/health/live`, `/health/ready`
- [ ] API versioning + `/api/v1` route prefix

---

## Phase 5 — Features

### F1 — Auth
- `POST /api/v1/auth/register` (Customer role auto-assigned)
- `POST /api/v1/auth/login` → `{ accessToken, refreshToken, expiresIn }`
- `POST /api/v1/auth/refresh`
- `POST /api/v1/auth/logout` (revoke refresh)
- `GET  /api/v1/auth/me`
- **Tests:** register validation, login wrong password, refresh rotation, me requires token

### F2 — Product Catalog
- `GET    /api/v1/products?page=1&pageSize=20&sortBy=price&sortDir=asc` — paginated list
- `GET    /api/v1/products/{slug}` — details
- `POST   /api/v1/products` (Admin) — create
- `PUT    /api/v1/products/{id}` (Admin) — update
- `DELETE /api/v1/products/{id}` (Admin) — soft delete
- `GET    /api/v1/products/{id}/variants` — variants
- Caching on the list + details
- **Tests:** pagination math, only Admin can mutate, soft-deleted items hidden, cache-hit path

### F3 — Search
- `GET /api/v1/products/search?q=jersey&franchise=MI&type=Jersey&minPrice=500&maxPrice=3000&inStockOnly=true&page=1`
- Returns paged products + **facets** (`franchises[]`, `types[]`, `priceBuckets[]` with counts)
- DB-side search: `WHERE Name LIKE @q OR Description LIKE @q` with indexed columns; ordered by relevance heuristic (exact name match > starts-with > contains)
- **Tests:** filter combinations, facet counts correctness, empty query returns all

### F4 — Cart
- `GET    /api/v1/cart`
- `POST   /api/v1/cart/items` (Idempotency-Key required) — add or merge quantity
- `PATCH  /api/v1/cart/items/{itemId}` — update qty (0 → remove)
- `DELETE /api/v1/cart/items/{itemId}`
- `DELETE /api/v1/cart` — empty cart
- Validates: variant exists, in stock, qty ≤ stock, qty ≤ 10 per line
- **Tests:** add merges existing line, qty > stock rejected, idempotency replay returns same response

### F5 — Checkout & Orders
- `POST /api/v1/orders` (Idempotency-Key required) — body: `{ shippingAddress, couponCode?, paymentMethod }`
  - In a single transaction:
    1. Re-validate cart against current prices & stock
    2. Apply coupon if any
    3. Decrement variant stock (optimistic concurrency)
    4. Create Order + OrderItems (snapshot prices)
    5. Call `IPaymentGateway` (mock)
    6. Empty cart, increment coupon usage
    7. Publish `OrderPlacedEvent` (in-process for now)
  - On stock contention → 409 with helpful body
- `POST /api/v1/orders/{id}/cancel` — only if status = Pending|Confirmed; restores stock
- **Tests:** happy path, insufficient stock, invalid coupon, idempotency replay, cancel restores stock, cannot cancel shipped order

### F6 — Order History
- `GET /api/v1/orders?page=1&pageSize=10&status=` — current user only
- `GET /api/v1/orders/{orderNumber}` — own order, or any if Admin
- **Tests:** user cannot see another user's order, pagination, status filter

---

## Phase 6 — Bonus Features (the "wow" delta)

### B1 — Coupons (admin-managed)
- `POST   /api/v1/admin/coupons` (Admin)
- `GET    /api/v1/admin/coupons`
- `POST   /api/v1/coupons/validate` — `{ code, cartTotal }` → discount preview
- **Tests:** expired, usage-limit, min-order-value

### B2 — Reviews & Ratings
- `POST   /api/v1/products/{id}/reviews` (Customer; must have purchased — verified buyer)
- `GET    /api/v1/products/{id}/reviews?page=1`
- `DELETE /api/v1/products/{id}/reviews/{reviewId}` (own or Admin)
- Domain event recomputes `AverageRating` + `ReviewCount` on Product
- **Tests:** non-buyer rejected, second review by same user rejected, average recomputes

### B3 — Wishlist
- `GET    /api/v1/wishlist`
- `POST   /api/v1/wishlist/{productId}`
- `DELETE /api/v1/wishlist/{productId}`

---

## Phase 7 — Testing & Architecture Guardrails
- Unit tests target ≥ 80% coverage on Domain + Application
- Integration tests cover **the happy path of every endpoint** + key failure paths
- Architecture tests:
  - Domain references nothing
  - Application references only Domain + Shared
  - Controllers do not reference EF Core
  - All handlers end with `Handler`
  - All validators end with `Validator`

## Phase 8 — Client (minimal React)
- Vite + TS + React Router + minimal CSS
- Pages: Login, Register, ProductList, ProductDetails, Cart, Checkout, OrderHistory, OrderDetails
- One `api.ts` with fetch wrapper that handles token refresh
- One typed contract file kept in sync with API (manually)

## Phase 9 — Polish
- `README.md` with: prereqs, run steps (3 commands), test steps, default admin login, architecture diagram link, design decisions TL;DR
- `docs/api-examples.http` covering every endpoint with sample bodies
- Verify `dotnet test` is green
- Verify `npm run build` clean
