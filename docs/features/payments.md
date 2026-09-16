# 💳 Feature: Payments & Ticket Dispatch

> The **Payment Module** manages secure credit card and mobile wallet processing via **Stripe**, automated conflict refunds, and email confirmation dispatch with server-rendered QR codes.

---

## 🔄 End-to-End Payment Flow

```text
┌──────────────┐         ┌──────────────┐         ┌──────────────┐         ┌──────────────┐
│ React Client │         │ Ticketa.Api  │         │  Stripe API  │         │ MailKit SMTP │
└──────┬───────┘         └──────┬───────┘         └──────┬───────┘         └──────┬───────┘
       │                        │                        │                        │
       │ 1. POST /create-intent │                        │                        │
       ├───────────────────────>│                        │                        │
       │                        │ 2. Create PaymentIntent│                        │
       │                        ├───────────────────────>│                        │
       │                        │<───────────────────────┤                        │
       │ 3. { clientSecret }    │                        │                        │
       │<───────────────────────┤                        │                        │
       │                        │                        │                        │
       │ 4. Confirm Payment (Card / Apple Pay / GPay)    │                        │
       ├────────────────────────────────────────────────>│                        │
       │<────────────────────────────────────────────────┤ (Status: Succeeded)    │
       │                        │                        │                        │
       │ 5. POST /confirm-payment                        │                        │
       ├───────────────────────>│                        │                        │
       │                        │ 6. Atomic DB Commit    │                        │
       │                        │    Insert Booking+Seats│                        │
       │                        │    Generate QR (PNG)   │                        │
       │                        │ 7. Send Ticket Email   │                        │
       │                        ├────────────────────────────────────────────────>│
       │ 8. Booking Confirmation│                        │                        │
       │<───────────────────────┤                        │                        │
```

---

## ⚡ Stripe Integration Details

### 1. Client-Side PaymentElement (`CheckoutForm.tsx`)
* **Module-Level Stripe Promise**: Initialized once outside the React tree:
  ```typescript
  import { loadStripe } from '@stripe/stripe-js';
  export const stripePromise = loadStripe(import.meta.env.VITE_STRIPE_PUBLIC_KEY);
  ```
* **Inner Component Constraint**: `CheckoutForm` must be rendered as an immediate child of `<Elements options={{ clientSecret }}>` to satisfy Stripe's `useStripe()` and `useElements()` hook context requirements.
* **Wallets**: Digital wallets (Apple Pay, Google Pay) are enabled automatically.

### 2. Idempotency Key Generation
To prevent accidental duplicate charges from rapid button clicks or network retries, `PaymentService` generates deterministic idempotency keys:

```csharp
var sortedSeats = string.Join("-", request.Seats.OrderBy(s => s.Row).ThenBy(s => s.SeatNumber));
var idempotencyKey = $"intent-{userId}-{request.ShowtimeId}-{sortedSeats}";
```

---

## 🔄 Automated Conflict Refund Logic

If two customers initiate checkout for the same seat simultaneously:

```csharp
var result = await _bookingService.CreateAsync(bookingCreateDto, userId, ct);

if (!result.Succeeded && result.IsConflict)
{
    // 1. Immediately refund the Stripe PaymentIntent
    var refundOptions = new RefundCreateOptions
    {
        PaymentIntent = paymentIntentId,
        Reason = RefundReasons.Duplicate
    };
    await _refundService.CreateAsync(refundOptions, ct);

    // 2. Return 409 Conflict with details
    return Conflict(new { message = "Selected seats were booked by another user. Your payment has been automatically refunded.", conflictSeats = result.ConflictSeats });
}
```

---

## ✉️ Server-Side QR Generation & Email Dispatch

```text
┌────────────────────────────────────────────────────────┐
│             Email Ticket Assembly Flow                 │
│                                                        │
│  1. Generate Raw Scan URL:                             │
│     https://ticketa.com/scan/TKT-9F2B7A1C              │
│                                                        │
│  2. Render PNG Byte Array via QRCoder:                 │
│     byte[] qrBytes = _qrCodeService.GeneratePng(url)   │
│                                                        │
│  3. Attach via MailKit CID (Content-ID):               │
│     var image = builder.LinkedResources.Add(           │
│         "ticket-qr.png", qrBytes, "image/png");        │
│     image.ContentId = "ticket-qr";                     │
│                                                        │
│  4. HTML Body Reference:                               │
│     <img src="cid:ticket-qr" alt="Ticket QR" />        │
└────────────────────────────────────────────────────────┘
```

### Why MailKit CID over Base64?
* **Problem**: Desktop mail clients (Outlook Desktop, corporate filters) routinely strip `data:image/png;base64,...` inline image strings.
* **Solution**: MailKit's MIME `LinkedResources` embeds the image as a multipart MIME attachment referenced by `cid:ticket-qr`, guaranteeing display across all email clients.

### Non-Blocking Resilience Rule
Email delivery is wrapped in a dedicated `try/catch` block that logs failures without interrupting the confirmed booking:
> *A successful booking with a delayed email is recoverable; a successful booking rolled back due to a temporary SMTP hiccup is a disastrous customer experience.*

---

## 🛡️ Admin Payment Management

In `Ticketa.Web` (`/Admin/Payments`), staff with the isolated `payments:refund` permission can review transaction history and trigger manual refunds for cancelled showtimes:

* **Auditability**: Records payment provider reference, amount, currency, timestamp, and status.
* **Security Isolation**: `payments:refund` is strictly isolated from standard viewing and editing permissions.
