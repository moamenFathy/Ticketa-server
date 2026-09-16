# 🔐 Authentication, Authorization & Security

> **Ticketa** features a robust dual-layer security architecture: a **stateless Dual-Token JWT flow** for the customer React SPA, coupled with a **granular Claims-Based Permission System** for back-office administration.

---

## 🛡️ Identity Model & AppUser

Ticketa builds on **ASP.NET Core Identity** with custom domain extensions:

```csharp
public class AppUser : IdentityUser
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateOnly DateOfBirth { get; set; }
    public string Theme { get; set; } = "light";
    public string? ConfirmationCode { get; set; }
    public DateTime? ConfirmationCodeExpiry { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime? RefreshTokenExpiry { get; set; }
    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
}
```

---

## 🔑 Dual-Token Authentication Architecture (Client SPA)

```text
┌──────────────┐                                 ┌──────────────┐
│ React Client │                                 │ Ticketa.Api  │
└──────┬───────┘                                 └──────┬───────┘
       │                                                │
       │ 1. POST /api/auth/login {email, password}      │
       ├───────────────────────────────────────────────>│
       │                                                │ Validate credentials
       │ 2. Response: { accessToken }                   │ Generate JWT (15 min)
       │    Set-Cookie: refreshToken (httpOnly, 7 days) │ Generate Refresh Token
       │<───────────────────────────────────────────────┤
       │                                                │
       │ 3. Memory storage of accessToken               │
       │    Subsequent requests include Bearer token    │
       ├───────────────────────────────────────────────>│
       │                                                │
       │ 4. 401 Unauthorized (Access Token expired)    │
       │<───────────────────────────────────────────────┤
       │                                                │
       │ 5. POST /api/auth/refresh (Auto-intercepted)   │
       │    (Browser automatically sends Cookie)        │
       ├───────────────────────────────────────────────>│
       │                                                │ Validate cookie token
       │ 6. Response: { new accessToken }               │ Rotate Refresh Token
       │<───────────────────────────────────────────────┤
       │                                                │
       │ 7. Replay queued original request              │
       ├───────────────────────────────────────────────>│
```

### 1. In-Memory Access Tokens (XSS Protection)
* Access tokens are **never written to `localStorage` or `sessionStorage`**.
* The token resides exclusively in React memory (`AuthProvider.tsx`).
* An Axios request interceptor injects the header: `Authorization: Bearer <token>`.

### 2. Secure httpOnly Refresh Cookie (CSRF & Theft Protection)
* Refresh tokens are delivered via an `httpOnly`, `Secure`, `SameSite=Strict` cookie.
* JavaScript running on the page cannot access or inspect the refresh token.

### 3. Silent Refresh & 401 Queue Interceptor
* **On App Mount**: `AuthProvider` invokes `/api/auth/refresh` silently to restore the session if a valid refresh cookie exists.
* **On Expiration**: When any API request receives a `401 Unauthorized`, an Axios interceptor suspends outgoing requests, requests a fresh access token, updates the client token ref, and replays all queued calls transparently.

---

## ✉️ Registration & Email Verification Lifecycle

```text
┌─────────────────┐       ┌─────────────────┐       ┌─────────────────┐
│  Step 1: Input  │  ──>  │  Step 2: OTP    │  ──>  │ Step 3: Success │
│ Name, DOB, Pass │       │  6-Digit Code   │       │ Auto-Login & JWT│
└─────────────────┘       └─────────────────┘       └─────────────────┘
```

1. **Submission**: User provides registration details via `POST /api/auth/register`.
2. **OTP Generation**: Server generates a cryptographically random 6-digit verification code with a 15-minute expiration (`ConfirmationCodeExpiry`).
3. **Dispatch**: Formatted HTML email is sent via `MailKit` and Google SMTP using `EmailTemplates.EmailConfirmation(code)`.
4. **Verification**: User enters the code into the client's `input-otp` component, triggering `POST /api/auth/confirm-email`.
5. **Issuance**: On successful confirmation, the server activates the account, issues access/refresh tokens, and logs the user in immediately.

---

## 🛡️ Granular Permissions System (RBAC)

### The Anti-`Manage` Principle
Ticketa explicitly rejects coarse `Manage` permissions (e.g., `ManageMovies`). A single `Manage` permission collapses viewing, editing, creating, and deleting into one toggle, eliminating the ability to create specialized cinema staff roles (such as a Content Editor who can update movie metadata but cannot import or delete).

### 1. Code-Only Permission Constants (`Permissions.cs`)
Permissions are static code constants categorized by domain module:

```csharp
public static class Permissions
{
    public static class Movies
    {
        public const string View   = "movies:view";
        public const string Import = "movies:import";  // TMDB bulk import
        public const string Edit   = "movies:edit";
        public const string Delete = "movies:delete";
    }

    public static class Showtimes
    {
        public const string View   = "showtimes:view";
        public const string Create = "showtimes:create";
        public const string Edit   = "showtimes:edit";
        public const string Delete = "showtimes:delete";
    }

    public static class Payments
    {
        public const string View   = "payments:view";
        public const string Refund = "payments:refund";  // High-stakes financial action
    }

    public static class Bookings
    {
        public const string View = "bookings:view";
        public const string Scan = "bookings:scan";    // Entrance QR ticket verification
    }

    public static class Users
    {
        public const string View   = "users:view";
        public const string Create = "users:create";
        public const string Edit   = "users:edit";
        public const string Delete = "users:delete";
    }

    public static class Roles
    {
        public const string View   = "roles:view";
        public const string Create = "roles:create";
        public const string Edit   = "roles:edit";
        public const string Delete = "roles:delete";
    }

    public static class Dashboard
    {
        public const string View = "dashboard:view";
    }

    public static IEnumerable<string> GetAll() =>
        typeof(Permissions)
            .GetNestedTypes()
            .SelectMany(t => t.GetFields(BindingFlags.Public | BindingFlags.Static))
            .Select(f => (string)f.GetValue(null)!);
}
```

### 2. Reflection-Driven Discovery
`Permissions.GetAll()` uses reflection to discover all declared constants dynamically. When a developer adds a new permission constant to `Permissions.cs`, it automatically appears in the Admin Role creation/editing UI without modifying manual registration lists.

### 3. Permissions in Claims & Tokens
When a user logs in, their assigned role claims are loaded and embedded into the token:

```csharp
var claims = await _roleManager.GetClaimsAsync(appRole);
var permissions = claims.Where(c => c.Type == "permission").Select(c => c.Value);

var token = _jwtService.GenerateAccessToken(user, roles, permissions.Distinct().ToList());
```

In the JWT payload, each permission is emitted as:
```json
{
  "permission": "movies:import",
  "permission": "showtimes:create",
  "permission": "dashboard:view"
}
```

### 4. Admin Controller Protection
In `Ticketa.Web`, controllers and action methods are guarded using the custom `[RequirePermission]` attribute:

```csharp
[RequirePermission(Permissions.Movies.Import)]
public async Task<IActionResult> Import(MovieImportVM model) { ... }

[RequirePermission(Permissions.Payments.Refund)]
public async Task<IActionResult> Refund(int paymentId) { ... }
```

---

## 👥 Realistic Cinema Role Presets

| Permission Constant | Super Admin | Content Manager | Scheduler | Box Office Staff |
| :--- | :---: | :---: | :---: | :---: |
| `dashboard:view` | ✅ | ❌ | ❌ | ❌ |
| `movies:view` | ✅ | ✅ | ✅ | ❌ |
| `movies:import` | ✅ | ✅ | ❌ | ❌ |
| `movies:edit` | ✅ | ✅ | ❌ | ❌ |
| `movies:delete` | ✅ | ❌ | ❌ | ❌ |
| `showtimes:view` | ✅ | ✅ | ✅ | ✅ |
| `showtimes:create` | ✅ | ❌ | ✅ | ❌ |
| `showtimes:edit` | ✅ | ❌ | ✅ | ❌ |
| `showtimes:delete` | ✅ | ❌ | ❌ | ❌ |
| `payments:view` | ✅ | ❌ | ❌ | ❌ |
| `payments:refund` | ✅ | ❌ | ❌ | ❌ |
| `bookings:view` | ✅ | ❌ | ❌ | ❌ |
| `bookings:scan` | ✅ | ❌ | ❌ | ✅ |
| `users:*` | ✅ | ❌ | ❌ | ❌ |
| `roles:*` | ✅ | ❌ | ❌ | ❌ |
