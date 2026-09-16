# 🎬 Ticketa — Modern Cinema Booking & Management System

> A full-stack, enterprise-grade cinema ticketing and management platform inspired by VOX Cinemas and AMC. Built with a clean, layered ASP.NET Core backend and a modern React 19 SPA frontend.

---

## 📚 Documentation Hub

Explore the in-depth technical documentation self-hosted directly within this repository:

```text
TicketaSol/
├── docs/
│   ├── architecture.md               # Solution Architecture & System Design
│   ├── database.md                   # Database Schema, Indexing & EF Core Design
│   ├── authentication.md             # Dual-Token JWT Auth & Granular RBAC
│   ├── testing-plan.md               # Testing Pyramid, xUnit, Moq, Stryker & Coverage
│   ├── decisions/                    # Architecture Decision Records (ADRs)
│   │   ├── sql-server-vs-postgresql.md
│   │   ├── hall-template.md
│   │   └── payment-first-booking.md
│   └── features/                     # Feature Deep-Dives & Workflows
│       ├── booking.md
│       ├── payments.md
│       └── showtimes.md
└── README.md                         # Project Overview & Gateway
```

### 📑 Core System Documentation
* **[🏗️ System Architecture & Design](docs/architecture.md)** — Layered solution structure (`Core`, `Infrastructure`, `Web`, `Api`, `Tests`), design patterns, and IIS hosting resilience.
* **[🗄️ Database Architecture & Schema](docs/database.md)** — Entity relationships, virtual seat model, filtered indexing (`[IsArchived] = 0`), and referential integrity.
* **[🔐 Authentication & Security](docs/authentication.md)** — In-memory JWT access tokens, `httpOnly` refresh cookies, 401 interceptor queue, OTP verification, and reflection-based RBAC permissions.
* **[🧪 Testing Plan & Strategy](docs/testing-plan.md)** — xUnit, Moq, Testcontainers SQL Server, Stryker.NET mutation testing, and the 10-phase testing roadmap.

### 🏛️ Architecture Decision Records (ADRs)
* **[ADR 001: SQL Server vs. PostgreSQL](docs/decisions/sql-server-vs-postgresql.md)** — Rationale for choosing SQL Server, SSMS execution plan diagnostics, and filtered index support.
* **[ADR 002: Fixed Mathematical Hall Templates](docs/decisions/hall-template.md)** — Why we replaced the traditional static `Seat` table with mathematical templates (`HallTypeHelper`).
* **[ADR 003: Payment-First Booking Architecture](docs/decisions/payment-first-booking.md)** — Eliminating abandoned reservation holds, avoiding Redis overhead, and managing race conditions via auto-refunds.

### 🚀 Feature Deep-Dives
* **[🎟️ Cinema Booking & Seat Selection](docs/features/booking.md)** — Customer booking journey, interactive seat grid, coordinate normalization, and "My Tickets" inventory view.
* **[💳 Payments & Ticket Dispatch](docs/features/payments.md)** — Stripe `PaymentElement` integration, server-side PNG QR code generation with `QRCoder`, and MailKit CID inline email tickets.
* **[🕐 Showtimes & Interactive Schedule Management](docs/features/showtimes.md)** — 15-minute cleaning turnaround buffer, state machine transitions, and the back-office Gantt Chart schedule editor.

---

## 🏗️ Solution Overview

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

## ⚡ Key Engineering Highlights

* **Virtual Seat Architecture**: No static `Seat` table in the database; auditoriums are synthesized dynamically on demand via `HallTypeHelper`.
* **Zero-Orphan Booking Engine**: Payment-first architecture guarantees every database booking represents a verified, paid transaction.
* **Storage-Engine Race Protection**: `(ShowtimeId, Row, SeatNumber)` composite unique index prevents double-booking at the hardware storage layer, backed by instant automated Stripe refunds on conflict.
* **Secure Dual-Token Auth**: Access tokens are kept strictly in React memory (preventing XSS access); refresh tokens use `httpOnly` secure cookies with a transparent 401 replay queue.
* **High-Performance Filtered Indexes**: `[IsArchived] = 0` filtered indexes preserve fast point-lookup execution plans over years of operational history.
* **Reflection-Driven Permissions (RBAC)**: Fine-grained, string-constant permissions discovered via reflection, eliminating coarse `Manage` permissions and allowing nuanced staff role configuration.

---

## 🛠️ Tech Stack

### Backend
* **Runtime**: .NET 10 (C# 13)
* **Frameworks**: ASP.NET Core Web API & ASP.NET Core MVC
* **ORM & Database**: Entity Framework Core 10, Microsoft SQL Server
* **Security**: ASP.NET Core Identity, JWT Bearer Tokens, Custom Claims Authorizer
* **Integrations**: Stripe.net (Payments & Refunds), MailKit (SMTP), QRCoder (QR Ticket Generation), TMDB API (Movie Metadata)
* **Testing**: xUnit, Moq, Testcontainers.MsSql, Stryker.NET, ReportGenerator

### Frontend
* **Core**: React 19, TypeScript, Vite
* **State & Data Fetching**: TanStack React Query v5, Axios
* **UI & Styling**: Tailwind CSS, shadcn/ui (Radix UI), DaisyUI (Admin), Framer Motion
* **Forms & Validation**: React Hook Form, Zod, input-otp
* **Payment UI**: @stripe/react-stripe-js, @stripe/stripe-js

---

## 🚀 Getting Started & Local Development

### Prerequisites
* [.NET 10 SDK](https://dotnet.microsoft.com/download)
* [Node.js 20+ & npm](https://nodejs.org/)
* [SQL Server](https://www.microsoft.com/sql-server) (LocalDB, Developer Edition, or Docker container)

### 1. Clone & Configure Backend
```bash
# Clone the repository
git clone https://github.com/your-username/Ticketa.git
cd TicketaSol

# Configure User Secrets for Ticketa.Api
cd Ticketa.Api
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=(localdb)\\mssqllocaldb;Database=TicketaDb;Trusted_Connection=True;MultipleActiveResultSets=true"
dotnet user-secrets set "Stripe:SecretKey" "sk_test_..."
dotnet user-secrets set "Stripe:PublishableKey" "pk_test_..."
dotnet user-secrets set "EmailSettings:Password" "your-smtp-app-password"
```

### 2. Apply Migrations & Seed Database
```bash
dotnet ef database update --project ../Ticketa.Infrastructure --startup-project .
```

### 3. Run Backend Services
```bash
# Start the Web API (port 5000)
dotnet run --project Ticketa.Api

# Or start the Admin Web Portal (port 5001)
dotnet run --project Ticketa.Web
```

### 4. Run Frontend Client
```bash
cd ../Ticketa.Client
npm install
npm run dev
```

---

## 🧪 Testing & Code Coverage

Run all unit and integration tests:
```bash
dotnet test
```

Generate the HTML Code Coverage Report (Windows batch script):
```cmd
run-coverage.bat
```

Run Stryker Mutation Testing:
```bash
dotnet stryker
```
