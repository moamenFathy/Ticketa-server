# 🎟️ Feature: Cinema Booking & Seat Selection

> The **Booking Module** handles the customer journey from interactive hall seat selection and real-time pricing to reservation confirmation, QR ticket verification, and ticket history management.

---

## 🗺️ Customer Booking Journey

```text
┌────────────────────────────────────────────────────────┐
│ 1. Showtime Discovery (/showtimes)                    │
│    Two-panel layout: movie list + grouped sessions     │
└───────────────────────────┬────────────────────────────┘
                            │
┌───────────────────────────▼────────────────────────────┐
│ 2. Seat Selection (/showtimes/:id)                     │
│    Interactive grid, category legend, max 10 seats     │
│    Real-time price calculation in Order Summary        │
└───────────────────────────┬────────────────────────────┘
                            │
┌───────────────────────────▼────────────────────────────┐
│ 3. Auth Gate & Payment Intent Generation               │
│    If unauthenticated: redirect /login?returnUrl=...   │
│    POST /api/payments/create-intent                    │
└───────────────────────────┬────────────────────────────┘
                            │
┌───────────────────────────▼────────────────────────────┐
│ 4. Stripe Checkout & Payment Confirmation              │
│    Stripe PaymentElement -> POST /confirm-payment      │
└───────────────────────────┬────────────────────────────┘
                            │
┌───────────────────────────▼────────────────────────────┐
│ 5. Confirmation & Digital Ticket                       │
│    Display Confirmation Card + QR Code Ticket          │
│    Accessible anytime via My Tickets (/profile)        │
└────────────────────────────────────────────────────────┘
```

---

## 🪑 Seat Grid Component Architecture (`SeatGrid.tsx`)

### 1. Visual State Hierarchy
The client assigns styles based on a strict visual priority matrix:

```typescript
// State priority: Conflict > Selected > Booked > Available (by Category)
isConflict && 'bg-red-500 animate-pulse text-white'
isSelected && 'bg-orange-400 scale-110 shadow-lg text-white'
isBooked   && 'bg-gray-700 cursor-not-allowed opacity-50'
!isBooked && !isSelected && !isConflict && categoryColors[category]
```

### 2. Category Color Tokens
```typescript
const categoryColors: Record<SeatCategory, string> = {
  Regular:      'bg-slate-400 hover:bg-slate-300',
  VIP:          'bg-blue-500 hover:bg-blue-400',
  Premium:      'bg-purple-500 hover:bg-purple-400',
  GoldLounge:   'bg-yellow-500 hover:bg-yellow-400',
  GoldRecliner: 'bg-amber-400 hover:bg-amber-300',
};
```

### 3. Coordinate System Alignment
* **Backend Database & DTOs**: Use **1-based** row and seat numbering (`Row: 1..10`, `SeatNumber: 1..12`).
* **Frontend UI**: Renders 0-indexed grids and converts row numbers to alphabetical cinema labels:
  ```typescript
  export const rowLabel = (rowIndex: number) => String.fromCharCode(65 + rowIndex); // 0 -> 'A', 1 -> 'B'
  ```

---

## ⚡ Concurrency & Conflict Handling (409 Conflict)

If another user completes checkout for any of the selected seats before the current transaction completes:

1. **Storage Constraint**: SQL Server rejects the insertion with a unique constraint violation on `IX_BookedSeats_ShowtimeId_Row_SeatNumber`.
2. **Auto-Refund**: The backend triggers an automated refund for the current user's Stripe `PaymentIntent`.
3. **Client Notification**: The API responds with `409 Conflict` containing the list of conflicting seat identifiers.
4. **Interactive Recovery**: The client removes the conflicted seats from the user's cart, flashes the conflicted seats in red (`animate-pulse`), and keeps non-conflicting seats selected for quick re-checkout.

---

## 📱 Ticket Validation & QR Scanning

### 1. Digital Ticket Display (`/bookings/:reference` & `/scan/:reference`)
Each completed booking generates a unique alphanumeric reference string (e.g. `TKT-9F2B7A1C`).

* **Client QR Generator**: Rendered client-side via `qrcode.react` targeting `{ClientBaseUrl}/scan/{bookingReference}`.
* **Server Verification Endpoint**: `GET /api/bookings/{reference}` returns the complete `BookingDetailsDto`:
  * Movie title, poster, runtime, and age rating.
  * Hall name, hall type, and showtime start time.
  * Seat breakdown (row letter, seat number, category, price).
  * Total amount paid and customer name.

---

## 📂 "My Tickets" (Inventory & History View)

Accessible from `/profile`, the ticket inventory view replaces traditional flat logs with a clear **Upcoming vs. Past** division:

```text
┌────────────────────────────────────────────────────────┐
│                      My Tickets                        │
│   [ 🟢 Upcoming Tickets (2) ]    [ ⚪ Past History (8) ]│
└────────────────────────────────────────────────────────┘
```

* **Upcoming Tab**: Shows future showtimes (`Showtime.StartTime > UtcNow`). Displays QR ticket front-and-center for door scanning.
* **Past Tab**: Shows historical showtimes (`Showtime.StartTime <= UtcNow`). Reference-only summary with links to review details.
* **Infinite Scroll**: Utilizes TanStack `useInfiniteQuery` coupled with `IntersectionObserver` sentinel elements to stream history pages smoothly.
