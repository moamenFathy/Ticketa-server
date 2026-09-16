# 🏗️ Architecture & System Design

> **Ticketa** is a cinema booking and management platform inspired by real-world cinema systems (like VOX Cinemas and AMC). It is designed as a clean, layered ASP.NET Core solution combined with a modern React SPA client.

---

## 📐 Layered Solution Architecture

The backend is built around Clean Architecture and Separation of Concerns principles, partitioned into distinct projects sharing common core models and infrastructure without duplication:

```text
TicketaSol.sln
├── Ticketa.Core           → Domain Entities, Enums, Interfaces, DTOs, Helpers
├── Ticketa.Infrastructure → EF Core, DbContext, Repositories, UoW, Services, MailKit, QRCoder
├── Ticketa.Web            → Admin MVC Portal (Controllers, ViewModels, Views, Razor Layouts)
├── Ticketa.Api            → Customer-Facing REST API (JWT Auth, Booking, Payments, Movies)
└── Ticketa.Tests          → xUnit, Moq, Stryker Mutation Testing, TestBuilders
```

```
┌─────────────────────────────────────────────────────────────┐
│                       Client Layer                          │
│   React 19 + TypeScript + Vite (Customer Web App)          │
│   DaisyUI + Tailwind MVC Views (Admin Back-Office)          │
└──────────────────────────┬──────────────────────────────────┘
                           │
             ┌─────────────┴─────────────┐
             ▼                           ▼
┌─────────────────────────┐ ┌─────────────────────────┐
│       Ticketa.Api       │ │       Ticketa.Web       │
│ (RESTful Controllers)   │ │  (MVC Admin Controllers)│
└────────────┬────────────┘ └────────────┬────────────┘
             │                           │
             └─────────────┬─────────────┘
                           ▼
┌─────────────────────────────────────────────────────────────┐
│                 Ticketa.Infrastructure                      │
│  - EF Core AppDbContext & SQL Server                        │
│  - Generic & Specialized Repositories + Unit of Work        │
│  - Business Services (Booking, Payment, Showtime, etc.)     │
│  - External Integrations (Stripe, MailKit, QRCoder, TMDB)   │
│  - Background Hosted Services (ShowtimeCompletionService)   │
└──────────────────────────┬──────────────────────────────────┘
                           ▼
┌─────────────────────────────────────────────────────────────┐
│                      Ticketa.Core                           │
│  - Domain Entities (Movie, Showtime, Hall, Booking, etc.)   │
│  - Domain Enums & Helper Logic (HallTypeHelper, Permissions)│
│  - Specifications & Expression Evaluators                   │
│  - Data Transfer Objects (DTOs) & Service Contracts         │
└─────────────────────────────────────────────────────────────┘
```

---

## 🧩 Project Breakdown

### 1. `Ticketa.Core` (The Domain Core)
* **Zero External Dependencies**: Has no dependencies on EF Core, ASP.NET Core MVC, or database drivers.
* **Entities**: `Movie`, `Showtime`, `Hall`, `Booking`, `BookedSeat`, `Payment`, `AppUser`, `AppRole`, `Genre`.
* **Enums**: `HallType` (`Standard`, `IMAX`, `Gold`), `SeatCategory` (`Regular`, `VIP`, `Premium`), `BookingStatus`, `ShowtimeStatus`, `PaymentStatus`, `MovieStatus`.
* **Domain Logic & Helpers**: `HallTypeHelper` (generates virtual seat templates on demand), `Permissions` (nested static constants for fine-grained authorization), `EmailTemplates`.
* **Contracts**: Interface definitions for repositories (`IBookingRepository`, `IShowtimeRepository`, `IUnitOfWork`) and services (`IBookingService`, `IPaymentService`, `IProfileService`, `ITokenService`).
* **Specifications**: Encapsulates query logic (`BaseSpecification<T>`, `ISpecification<T>`) for sorting, filtering, and eager loading.

### 2. `Ticketa.Infrastructure` (Data & External Services)
* **Data Access**: `AppDbContext`, Fluent API configurations, migrations, and database connection handling for SQL Server.
* **Repository & Unit of Work**: Generic repository implementation with specification evaluator (`SpecificationEvaluator.cs`) + custom repositories for specialized bulk operations (`BookedSeatRepository`, `ShowtimeRepository`).
* **Service Implementations**: `BookingService`, `PaymentService`, `ShowtimeService`, `MoviesService`, `ProfileService`, `AuthApiService`, `DashboardService`, `EmailService`, `QrCodeService`.
* **Background Services**: `ShowtimeCompletionService` (`IHostedService`) periodically marks expired showtimes as `Completed` and soft-archives them.

### 3. `Ticketa.Web` (Admin MVC Management)
* **Back-Office UI**: Server-rendered ASP.NET Core MVC application styled with Tailwind CSS and DaisyUI.
* **Features**:
  * Dashboard with key metrics and revenue trend charts.
  * Interactive Gantt Chart / Timeline view for visual showtime scheduling and conflict management.
  * Server-side DataTables.js integration for Movies, Showtimes, Payments, Users, and Roles.
  * Role & Permission management with fine-grained claim assignments.
  * TMDB Bulk Movie Importer.

### 4. `Ticketa.Api` (Customer-Facing REST API)
* **Public Endpoints**: Consumed by the React client.
* **Features**:
  * JWT Authentication & Token Lifecycle (`/api/auth`).
  * Movie discovery & details (`/api/movies`).
  * Showtime sessions and virtual seat layout (`/api/showtimes`).
  * Booking reservation & QR ticket validation (`/api/bookings`).
  * Stripe payment intent creation & confirmation (`/api/payments`).
  * Customer profile & infinite-scroll booking history (`/api/profile`).

---

## 🏛️ Core Design Patterns

### 1. Specification Pattern
Instead of writing repetitive LINQ queries across services, queries are encapsulated into reusable, composable specification classes:

```csharp
public class BookingHistorySpecification : BaseSpecification<Booking>
{
    public BookingHistorySpecification(string userId, int page, int pageSize)
    {
        AddCriteria(b => b.UserId == userId);
        AddInclude(b => b.Showtime.Movie);
        AddInclude(b => b.Showtime.Hall);
        AddInclude(b => b.BookedSeats);
        ApplyOrderByDescending(b => b.BookedAt);
        ApplyPaging((page - 1) * pageSize, pageSize);
    }
}
```

The `SpecificationEvaluator<T>` applies criteria, includes, ordering, and pagination dynamically to any `IQueryable<T>`.

### 2. Unit of Work & Repository Pattern
* `IUnitOfWork` coordinates transactional boundaries across multiple repositories.
* Calls `SaveChangesAsync()` within atomic transactions, ensuring state changes to bookings, seats, and payments commit together.

### 3. Virtual Seat Architecture (Zero Static Seat Rows)
* **No `Seat` table in the database**.
* Instead, `HallTypeHelper` dynamically calculates coordinates, categories, and spacing formulas from the hall's `HallType`.
* `BookedSeat` rows record reserved positions (`ShowtimeId`, `Row`, `SeatNumber`). The seat map UI blends the template with existing `BookedSeat` records on-the-fly.

---

## 💻 Frontend Client Architecture (React 19 + TypeScript)

The customer frontend (`Ticketa.Client`) enforces a strict **3-Layer Separation Pattern**:

```text
┌────────────────────────────────────────────────────────┐
│                   UI Presentation                      │
│   Pages (Home, ShowtimeSeats, Checkout, Profile)       │
│   Components (SeatGrid, HeroSection, OrderSummary)     │
└───────────────────────────┬────────────────────────────┘
                            │
┌───────────────────────────▼────────────────────────────┐
│                  Custom Hook Layer                     │
│   useAuth, useMovies, useShowtimes, usePayment         │
│   (TanStack React Query v5 useQuery / useMutation)     │
└───────────────────────────┬────────────────────────────┘
                            │
┌───────────────────────────▼────────────────────────────┐
│                 API & Key Factory                      │
│   queryKeys.ts (centralized typed cache keys)          │
│   api/*.api.ts (pure Axios requests + normalizers)     │
│   client.ts (interceptor queue + silent refresh)       │
└────────────────────────────────────────────────────────┘
```

### Key Frontend Capabilities
1. **In-Memory JWT & httpOnly Refresh**: Access tokens reside strictly in memory; refresh tokens are stored in secure `httpOnly` cookies.
2. **401 Interceptor Queue**: Automatically intercepts expired requests, silently executes `/api/auth/refresh`, and replays failed API calls.
3. **Stripe PaymentElement Flow**: Integrated with `@stripe/react-stripe-js` inside a checkout modal/page supporting credit cards and mobile wallets (Apple Pay / Google Pay).
4. **Infinite Scroll Pagination**: TanStack `useInfiniteQuery` coupled with `IntersectionObserver` for smooth browsing of booking history.

---

## ⚙️ Infrastructure & Hosting Constraints

### Shared Hosting (MonsterASP / IIS) Considerations
* **IIS Worker Process Recycling**: In shared hosting environments, application pools can recycle periodically due to idle timeouts or memory limits.
* **SignalR vs. Polling Decision**:
  * *Rejected*: Persistent WebSocket / SignalR connections drop simultaneously upon worker process recycling, forcing complex reconnection queues and reconnect storms.
  * *Chosen*: Lightweight HTTP Polling (`setInterval(..., 15000)` on Admin Dashboard; `30000ms` on Notification Center). Each poll is stateless and resilient across app restarts.
* **Background Jobs**: Handled in-process via `IHostedService` with safe try/catch loops and scoped `IServiceScopeFactory` execution.
