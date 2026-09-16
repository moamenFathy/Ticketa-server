# 🏛️ ADR 003: Payment-First Booking Architecture

## 📌 Status
**Accepted**

---

## 🎯 Context
In high-demand cinema ticketing systems, seat reservation locking is a fundamental architectural choice. We evaluated two models:

1. **Option A: Temporary Seat Holds (Countdown Reservation)**:
   * Selecting seats creates a temporary `Pending` booking with a 5–10 minute expiration timer.
   * Requires distributed locking (e.g. Redis Redlock) or background cleanup daemons to unlock abandoned carts.
2. **Option B: Payment-First Booking (Stripe PaymentIntent + Atomic Reservation)**:
   * Client creates a Stripe `PaymentIntent` for the selected seats.
   * Booking records are created **only after payment succeeds**.
   * If a rare concurrent race condition occurs, the unique constraint catches the collision and issues an immediate automated refund.

---

## ⚖️ Evaluation & Comparison

| Consideration | Option A: Temporary Seat Holds | Option B: Payment-First Booking (Chosen) |
| :--- | :--- | :--- |
| **Infrastructure Overhead** | Requires Redis cluster or persistent background sweeps. | **Zero additional infrastructure**; stateless HTTP flow. |
| **Database Cleanliness** | DB flooded with abandoned `Pending` / expired records. | **100% clean data**; every DB booking row is confirmed & paid. |
| **Shared-Hosting Resilience** | Background lock sweeps fail if IIS worker processes recycle. | Completely resilient across process recycling. |
| **Customer Experience** | Holds seats during checkout, but timer expiration creates frustration. | Seamless checkout; instant auto-refund on rare collision. |

---

## ✅ Decision
We selected **Option B: Payment-First Booking Architecture**.

### Workflow:
```text
ShowtimeSeats UI
  │ 1. User selects seats
  │ 2. POST /api/payments/create-intent ──> Stripe PaymentIntent (with idempotency key)
  ▼
Stripe Checkout Modal
  │ 3. User enters card / Apple Pay / Google Pay
  │ 4. Stripe processes payment (status: 'succeeded')
  ▼
Backend Confirmation (POST /api/payments/confirm-payment)
  │ 5. Atomic DB Transaction: Insert Booking + BookedSeats
  ├─► ✅ SUCCESS: Generate QR ticket & dispatch confirmation email
  └─► ❌ CONFLICT (409): Unique constraint violation caught
          └──> Auto-trigger Stripe Refund + Prompt user to reselect
```

---

## 🚀 Consequences & Concurrency Guarantees
* **Simplified Queries**: "My Tickets" and Admin reporting endpoints do not need complex filters for `Pending` or expired carts; every booking row in the database is verified revenue.
* **Storage-Engine Safety Net**: The composite unique index `(ShowtimeId, Row, SeatNumber)` on `BookedSeat` provides absolute database-level protection against double-booking.
* **Auto-Refund Safety**: In the event of a simultaneous checkout conflict, `PaymentService.ConfirmAsync` rolls back the local transaction, invokes the Stripe Refund API, and returns a `409 Conflict` with the conflicting seat keys highlighted in red on the client seat map.
