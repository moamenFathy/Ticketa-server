# 🕐 Feature: Showtimes & Interactive Schedule Management

> The **Showtime Module** handles cinema auditorium scheduling, conflict checking with turnaround buffers, state transitions (`Scheduled`, `SoldOut`, `Completed`), and the back-office **Gantt Chart Timeline**.

---

## 🔄 Showtime State Machine & Lifecycle

```text
               ┌───────────────────────┐
               │       Created         │
               └───────────┬───────────┘
                           │
                           ▼
               ┌───────────────────────┐
       ┌───────┤      Scheduled        │◄──────┐
       │       └───────────┬───────────┘       │
       │                   │                   │
Capacity filled            │ Auto-completed    │ Refund frees
       │                   │ after runtime     │ seats
       ▼                   │                   │
┌──────────────┐           │            ┌──────┴───────┐
│   SoldOut    ├───────────┼───────────>│   Scheduled  │
└──────┬───────┘           │            └──────────────┘
       │                   ▼
       │       ┌───────────────────────┐
       └──────>│       Completed       │
               │   (Soft-Archived)     │
               └───────────────────────┘
```

1. **`Scheduled`**: The session is active and bookable by customers on the public API.
2. **`SoldOut`**: Automatically triggered when `BookedSeats.Count` reaches `HallTemplate.VisibleSeatCount`. The customer API excludes sold-out sessions from the discovery grid.
3. **Reversion**: If a refund or cancellation reduces booked seats below capacity, the session automatically reverts to `Scheduled`.
4. **`Completed`**: A background hosted worker (`ShowtimeCompletionService`) marks sessions as `Completed` once `StartsAt + RuntimeMinutes` has elapsed, soft-archiving the record.

---

## ⏱️ Scheduling Rules & 15-Minute Turnaround Buffer

When scheduling a showtime in an auditorium:

1. **15-Minute Cleaning Buffer**: Consecutive showtimes in the exact same hall must have at least **15 minutes of clearance** between the previous movie's end time (`StartsAt + RuntimeMinutes`) and the next movie's start time:
   ```text
   [ Showtime A: 14:00 - 16:00 ] ── 15 min Cleaning Buffer ──> [ Showtime B: >= 16:15 ]
   ```
2. **5-Hour Advance Window**: New showtimes or rescheduled sessions must be scheduled at least 5 hours in advance of current UTC time.
3. **Immutable History**: Showtimes that have already started, completed, or have active booking records cannot be rescheduled or hard-deleted.

---

## 📊 Back-Office Interactive Gantt Chart Timeline

The admin portal (`Ticketa.Web` at `/Admin/Showtime`) provides a visual **Gantt Chart schedule editor**:

```text
Hall 1 (IMAX)     [ Movie A: 12:00 - 14:30 ]     [ Movie B: 15:00 - 17:30 ]
Hall 2 (Standard)          [ Movie C: 13:00 - 15:00 ]     [ Movie A: 16:00 - 18:30 ]
Hall 3 (Gold)     [ Movie D: 14:00 - 16:15 ]
                  ──────────────────────────────────────────────────────────>
                  12:00      13:00      14:00      15:00      16:00     17:00
```

### 1. Canvas-Based Visual Scheduling
* Renders all cinema halls along the Y-axis and 24-hour time slots along the X-axis.
* Admin can drag movie sessions across auditoriums or adjust time slots interactively.
* Real-time visual conflict detection highlights overlapping sessions in red.

### 2. Multi-Item Batch Persistence (`SaveBatchAsync`)
Rather than firing individual HTTP requests per drag operation, the editor stages changes locally and commits all schedule modifications atomically through a single batch endpoint:

```csharp
public async Task<BatchOperationResultDto> SaveBatchAsync(ShowtimeBatchDto batchDto, CancellationToken ct)
{
    // 1. Validate advance buffer and overlap rules across all items
    // 2. Perform inserts, updates, and soft-deletes in a single Unit of Work transaction
    // 3. Commit changes atomically
}
```

---

## 🔍 Public Showtime Discovery API

* `GET /api/showtimes`: Returns upcoming sessions grouped by date and movie.
  * Automatically filters out `IsArchived = true`, `Status = SoldOut`, and past sessions.
  * Supports eager loading of genres, posters, ratings, and hall formats.
* `GET /api/showtimes/{id}/seats`: Returns the synthesized seat map:
  * Number of rows and seats per row from `HallTypeHelper`.
  * Dynamic category map (`RowCategoryMap`).
  * Real-time list of reserved `BookedSeat` items.
