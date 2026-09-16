# 🗄️ Database Architecture & Schema Design

> **Ticketa** utilizes **Microsoft SQL Server** orchestrated through **Entity Framework Core (EF Core)**. The schema is optimized for data integrity, financial auditability, high-concurrency seat reservations, and query performance under scale.

---

## 🏛️ Entity Relationship Model (ERD)

```text
┌─────────────────┐       1:N       ┌────────────────────────┐
│      Movie      ├─────────────────┤        Showtime        │
│─────────────────│                 │────────────────────────│
│ Id (PK)         │                 │ Id (PK)                │
│ Title           │                 │ MovieId (FK)           │
│ RuntimeMinutes  │                 │ HallId (FK)            │
│ Status          │                 │ StartsAt, EndsAt       │
│ IsArchived      │                 │ BasePrice              │
│ ArchivedAt      │                 │ Status, IsArchived     │
└────────┬────────┘                 └───────────┬────────────┘
         │                                      │
         │ M:N                                  │ 1:N
┌────────┴────────┐                 ┌───────────┴────────────┐
│      Genre      │                 │        Booking         │
│─────────────────│                 │────────────────────────│
│ Id (PK)         │                 │ Id (PK)                │
│ Name            │                 │ Reference (Unique)     │
└─────────────────┘                 │ ShowtimeId (FK)        │
                                    │ UserId (FK)            │
┌─────────────────┐                 │ TotalAmount, Status    │
│     AppUser     ├────────┐        │ BookedAt               │
│─────────────────│        │        └───────────┬────────────┘
│ Id (PK)         │        │                    │
│ Email, FullName │        │ 1:N                │ 1:N
│ DateOfBirth     │        ▼                    ▼
│ Theme           │ ┌──────────────┐   ┌─────────────────────┐
└─────────────────┘ │   Payment    │   │     BookedSeat      │
                    │──────────────│   │─────────────────────│
                    │ Id (PK)      │   │ Id (PK)             │
                    │ BookingId(FK)│   │ BookingId (FK)      │
                    │ StripeIntent │   │ ShowtimeId (FK)     │
                    │ Amount,Status│   │ Row, SeatNumber     │
                    │ CreatedAt    │   │ Category, Price     │
                    └──────────────┘   └─────────────────────┘
```

---

## 🪑 The Virtual Seat Strategy (Zero-Table Seat Design)

### The Problem with a Traditional `Seat` Table
A typical cinema booking database might store a row for every individual physical chair in every hall. For a theater complex with 10 halls, 150 seats each, that represents 1,500 static rows. Across multiple multiplex locations, this scales to hundreds of thousands of static records that never change structural coordinates.

### The Ticketa Solution
* **Zero `Seat` Table**: The static `Seat` table was eliminated from the database.
* **On-Demand Template Generation**: The layout is purely mathematical and determined by the hall's `HallType` via `HallTypeHelper.GetTemplate(hall.Type)`:
  * **Standard Hall**: 10 rows × 12 seats (8 Regular rows, 2 VIP rows).
  * **IMAX Hall**: 14 rows × 16 seats (10 Regular rows, 4 Premium rows).
  * **Gold Hall**: 6 rows × 8 seats (3 GoldLounge rows, 3 GoldRecliner rows).
* **Booking Storage**: When a user purchases tickets, records are inserted into `BookedSeat` with `(ShowtimeId, Row, SeatNumber, Category, Price)`.
* **State Synthesis**: The API combines the static mathematical template with the list of active `BookedSeat` records for that showtime, instantly deriving available, reserved, and conflict states.

---

## ⚡ Indexing & Performance Optimization

```text
┌────────────────────────────────────────────────────────┐
│               Filtered Index Architecture              │
│                                                        │
│  Entire Showtime Table (History grows infinitely)      │
│  ┌──────────────────────────────────────────────────┐  │
│  │ ⚪ Archived Showtimes (IsArchived = 1)          │  │
│  │    - Excluded from Filtered Index                │  │
│  │    - Retained for financial / audit integrity   │  │
│  ├──────────────────────────────────────────────────┤  │
│  │ 🟢 Active Showtimes (IsArchived = 0)             │  │
│  │    - Stored in Filtered Index Leaf Nodes         │  │
│  │    - Point lookups remain fast indefinitely     │  │
│  └──────────────────────────────────────────────────┘  │
└────────────────────────────────────────────────────────┘
```

### 1. Filtered Indexes (Hot Path Preservation)
Cinema systems accumulate historical showtime and movie data continuously. To prevent table scan degradation over years of operation:

```csharp
// ShowtimeConfiguration.cs
builder.HasIndex(s => s.IsArchived)
       .HasFilter("[IsArchived] = 0");

// MovieConfiguration.cs
builder.HasIndex(m => m.IsArchived)
       .HasFilter("[IsArchived] = 0");
```

* **Advantage**: The filtered B-tree index contains only active records. Its memory footprint tracks the *active* schedule, not lifetime history.

### 2. Concurrency Unique Constraint (Race-Condition Hardening)
To guarantee at the storage engine level that two concurrent transactions can never book the exact same seat in the same showtime:

```csharp
// BookedSeatConfiguration.cs
builder.HasIndex(bs => new { bs.ShowtimeId, bs.Row, bs.SeatNumber })
       .IsUnique();
```

If two concurrent checkout requests attempt to insert identical `(ShowtimeId, Row, SeatNumber)` tuples simultaneously, SQL Server will raise a unique constraint violation (`2601` / `2627`), allowing EF Core to catch the exception and trigger an automated refund/rollback.

### 3. Composite Indexes for Booking History & Ordering
```csharp
// BookingConfiguration.cs
builder.HasIndex(b => new { b.UserId, b.BookedAt });
```
Optimizes the customer profile endpoint (`GET /api/profile/bookings`), allowing index seeks filtered by `UserId` and pre-sorted in descending order by `BookedAt`.

---

## 🛡️ Referential Integrity & Soft-Delete Lifecycle

### Foreign Key Constraints (`OnDelete: Restrict`)
All foreign keys linking `Booking -> Showtime` and `BookedSeat -> Showtime` are configured with `DeleteBehavior.Restrict`. 

> **Why?** Once money has transferred, deleting a showtime or movie must never cascade-delete associated bookings or payment records. Doing so would destroy the financial audit trail, break QR ticket validation (`/scan/:reference`), and corrupt customer booking history.

### Archiving Precondition Matrix

Because cascading deletes are forbidden, entities transition through an **independent soft-archive lifecycle**:

| Entity | Has Bookings? | Entity Status | Allowed Action | Result |
| :--- | :--- | :--- | :--- | :--- |
| **Showtime** | No | Any | Admin Delete | **Hard Delete** (Record removed) |
| **Showtime** | Yes | `Completed` | Admin Delete / Background Job | **Soft Archive** (`IsArchived = true`, `ArchivedAt = UtcNow`) |
| **Showtime** | Yes | `Scheduled` / `SoldOut` | Admin Delete | **Blocked** (Error: Showtime is currently live) |
| **Movie** | No | Any | Admin Delete | **Hard Delete** (Cascades to unbooked showtimes) |
| **Movie** | Yes | `Archived` | Admin Delete | **Soft Archive** (`IsArchived = true`, `ArchivedAt = UtcNow`) |
| **Movie** | Yes | `Active` / `ComingSoon` | Admin Delete | **Blocked** (Error: Admin must change status to Archived first) |

---

## 🔄 Concurrency Control & Transactions

### EF Core Execution Behavior
* **Single Operations**: EF Core automatically wraps single `SaveChangesAsync()` invocations inside an implicit transaction.
* **Multi-Step Workflows**: In booking and payment workflows (e.g. creating `Booking`, inserting multiple `BookedSeat` items, updating `Showtime.Status`, and recording `Payment`), an explicit transaction boundary ensures atomic all-or-nothing execution:

```csharp
using var transaction = await _uow.BeginTransactionAsync(ct);
try
{
    await _uow.Bookings.CreateAsync(booking, ct);
    await _uow.BookedSeats.AddRangeAsync(bookedSeats, ct);
    await _uow.SaveAsync(ct);
    
    await transaction.CommitAsync(ct);
}
catch
{
    await transaction.RollbackAsync(ct);
    throw;
}
```

### UTC Datetime Consistency
All database timestamps (`StartsAt`, `EndsAt`, `BookedAt`, `ArchivedAt`, `CreatedAt`) are stored strictly in **UTC**. The application utilizes EF Core `ValueConverter` configurations to enforce `DateTimeKind.Utc` on deserialization, preventing timezone calculation anomalies between client browsers and server workers.
