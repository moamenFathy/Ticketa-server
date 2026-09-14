# 🧪 Testing Architecture & Execution Plan

> **Ticketa** treats testing as an engineering discipline to prove that business-critical operations—especially high-concurrency seat selection, financial transactions, and RBAC rules—behave strictly according to specifications.

---

## 🏗️ The Testing Pyramid

```text
          /\
         /E2E\          Minimal Playwright golden path (Browse -> Seat -> Pay -> Ticket)
        /------\
       /Integration\     Real SQL Server via Testcontainers (Concurrency, EF Specs, UTC)
      /------------\
     /  Unit Tests  \    Comprehensive business logic coverage (xUnit + Moq, fast & isolated)
    /----------------\
```

1. **Unit Tests (Core & Services)**: Fast, memory-only, Moq-backed execution isolating business logic from databases and networks.
2. **Integration Tests (Testcontainers + SQL Server)**: Real SQL Server containers proving concurrency constraints, EF Core query translation, and unique index collision behavior.
3. **End-to-End Tests (Playwright)**: Minimal user journey tests validating the full customer checkout path.

---

## 🎯 Testing Philosophy: Genuine vs. Vacuous Tests

### ❌ The Trap: Tautological / Vacuous Testing
A test is **vacuous** if its assertions are merely written to match whatever the code currently produces. Such tests provide a false sense of security, passing even when the underlying business calculation is fundamentally wrong.

### ✅ The Standard: Requirement-Derived Assertions
* Expected values must be derived by hand from the domain requirement **before inspecting code output** (e.g. `3 VIP Seats × $100 base × 1.5 multiplier = $450`).
* **Manual Mutation Check**: Intentionally alter the production logic (e.g. change multiplier `1.5m` to `1.4m`). The test **must** fail immediately.
* **Automated Mutation Testing (Stryker.NET)**: Automatically mutates binaries and verifies that tests kill every mutant mutant (`Mutant Killed` vs `Mutant Survived`).

---

## 📋 Naming & Structural Conventions

| Item | Standard Convention | Example |
| :--- | :--- | :--- |
| **Project** | `Ticketa.Tests` (created via `dotnet new xunit`) | — |
| **Directory** | Mirrors production source tree | `Ticketa.Tests/Infrastructure/Services/BookingServiceTests.cs` |
| **Class Name** | `<TargetClass>Tests` | `BookingServiceTests.cs` |
| **Method Name** | `<MethodUnderTest>_<Scenario>_<ExpectedResult>` | `CreateAsync_WithConflictingSeat_ReturnsConflictResult` |
| **Structure** | Strict **Arrange-Act-Assert (AAA)** pattern | — |
| **Test Fixtures** | Fluent Test Data Builders | `ShowtimeBuilder`, `BookingBuilder`, `BookedSeatBuilder` |
| **Theory vs Fact** | `[Theory]` + `[InlineData]` for parameter variations; `[Fact]` for distinct mock graphs | — |

---

## 🧱 The Test Data Builder Pattern

### 🛑 Why We Avoid Manual Entity Instantiation
Complex domain models like `Showtime`, `Booking`, and `BookedSeat` have deep object graphs (e.g. `Hall`, `Movie`, navigation collections, pricing, UTC timestamps, and enum statuses).

Manually initializing these objects directly within test methods creates severe architectural problems:
1. **Noisy Setup**: 15–20 lines of repetitive setup code per test obscures what the test is actually verifying.
2. **Brittle Test Suites**: Adding a new required property or relation to an entity breaks dozens of test files across the solution.
3. **Loss of Test Intent**: Readers cannot easily distinguish between required domain data and irrelevant boilerplate.

### 💡 The Solution: Fluent Builders (`Ticketa.Tests/TestBuilders/`)
Test Data Builders provide complete, sensible default instances out-of-the-box, exposing expressive fluent methods for only the properties relevant to the specific test scenario:

```csharp
// Fluent and expressive — overrides only what matters for the test scenario
var showtime = new ShowtimeBuilder()
    .WithId(1)
    .WithHallType(HallType.Gold)
    .WithPrice(100m)
    .WithStatus(ShowtimeStatus.SoldOut)
    .Build();

var seat = new BookedSeatBuilder()
    .WithShowtimeId(1)
    .WithSeat(row: 1, seatNumber: 5)
    .WithCategory(SeatCategory.VIP)
    .Build();
```

* **`ShowtimeBuilder`**: Preconfigures valid default `Hall` (Standard, 182 visible seats), `Movie` (Active), and UTC timeframes.
* **`BookedSeatBuilder`**: Sets up valid row/seat coordinates, seat categories, and unit pricing.
* **`BookingBuilder`**: Bundles booked seat collections, computes `TotalAmount`, and sets confirmed status.

---

## 📊 Code Coverage & Risk Analysis (ReportGenerator & Stryker)

### Running Coverage Locally
The repository includes an automated batch script (`run-coverage.bat`) for generating and visualizing code coverage reports:

```cmd
@echo off
dotnet test --collect:"XPlat Code Coverage"
reportgenerator -reports:"**/coverage.cobertura.xml" -targetdir:"coveragereport" -reporttypes:Html
start coveragereport/index.html
```

### Risk Hotspot Identification (Crap Score)
ReportGenerator tracks **Crap Score** ($\text{Complexity} \times \text{Uncovered Paths}$) to direct testing priorities towards the most vulnerable components:

* `PaymentManagementSpecification.ApplyOrdering` — High Complexity / Ordering permutations
* `MovieSpecification.ApplyOrdering` / `ApplyFilters`
* `BookingService.CreateAsync` — High Concurrency critical section

### Running Mutation Testing (Stryker)
```bash
dotnet tool install -g dotnet-stryker
dotnet stryker
```

---

## 🗺️ 10-Phase Risk-Ordered Testing Roadmap

```text
Phase 0 [Done ✅] ──> Phase 1 [Done ✅] ──> Phase 2 [Done ✅] ──> Phase 3 [Done ✅]
        │
        └───> Phase 4 [Done ✅] ──> Phase 5 [Done ✅] ──> Phase 6 [Done ✅]
                │
                └───> Phase 7 [Done ✅] ──> Phase 8 [Done ✅] ──> Phase 9 [Integration ⏳]
                        │
                        └───> Phase 10 [E2E Playwright]
```

* **Test Suite Status**: **199 / 199 pure unit tests passing** across `Ticketa.Tests` (`dotnet test`).
* **Cross-Platform**: 100% platform-independent, executing cleanly on both Windows and Linux CI/CD environments.
* **Core Domain Coverage**: **81.0%** line coverage on `Ticketa.Core`.
* **Infrastructure Services Coverage**: **68.2%** line coverage on `Ticketa.Infrastructure` (with `DashboardService` at 99%, `NotificationService` at 97%, `PaymentService` at 100%, `ProfileService` at 100%, `AdminManagementService` at 92%, and `TokenService` at 100%).
* **Overall Method Coverage**: **78.4%**.

### Phase 0 — Core Helpers & Math ✅
* `HallTypeHelper.GetPriceMultiplier`: Verified `VIP (1.5x)`, `Premium (1.2x)`, `Regular (1.0x)`.
* `HallTemplate.VisibleSeatCount` & `InvisibleSeatCount`: Bowl-shape skip math calculations verified across Standard (110 seats), IMAX (214 seats), and Gold (38 seats).

### Phase 1 — Booking Core Logic ✅ (Implemented in `BookingServiceTests.cs`)

The `BookingService` test suite covers **13 comprehensive scenarios** across 5 categories of business guarantees:

#### 1. 🛡️ Safety & Zero-Dirty-Write Guarantees
* `CreateAsync_WhenShowtimeNotFound_ReturnsConflictWithEmptySeatsAndNeverSaves`
  * Verifies missing showtime returns `Succeeded = false`.
  * **Moq Assertion**: Verifies `Bookings.CreateAsync` and `SaveAsync` were **never called** (`Times.Never`), guaranteeing no partial records.
* `CreateAsync_WhenSeatsAlreadyBooked_ReturnsConflictWithConflictingSeatsAndNeverCreatesBooking`
  * When requested seats collide with existing bookings, returns `Succeeded = false` with conflicting seat coordinates.
  * **Moq Assertion**: Verifies `Bookings.CreateAsync` and `SaveAsync` were **never called**.

#### 2. 💰 Financial Correctness & Pricing Multipliers
* `CreateAsync_WithValidSeats_CalculatesPriceMultipliersAndTotalCorrectly`
  * Standard Hall Base Price = $100.
  * User selects Row 1 (Regular, $1.0\times = \$100$) + Row 10 (VIP, $1.5\times = \$150$).
  * **Assertions**: Verifies per-seat pricing, category assignments, and that `TotalAmount` on the created booking equals exactly **$250.00**.

#### 3. 🎟️ Capacity State Machine & Automated Status Transitions
* `CreateAsync_WhenBookingFillsCapacity_TransitionsShowtimeStatusToSoldOutAndUpdates`
  * Capacity = 38 visible seats, existing booked = 36. User books 2 seats ($36 + 2 = 38 \ge 38$).
  * **Assertions**: Verifies `Showtime.Status` transitions to `ShowtimeStatus.SoldOut` and `Showtimes.UpdateAsync` is called.
* `CreateAsync_WhenBookingDoesNotFillCapacity_ShowtimeStatusRemainsScheduled`
  * Capacity = 38 visible seats, existing booked = 10. User books 2 seats ($10 + 2 = 12 < 38$).
  * **Assertions**: Verifies `Showtime.Status` remains `ShowtimeStatus.Scheduled` with zero redundant update queries.

#### 4. ⚡ Storage Concurrency & Exception Handling
* `CreateAsync_WhenDbUpdateExceptionOccurs_CatchesExceptionAndReturnsLateConflict`
  * Simulates a database-level unique constraint collision (`DbUpdateException` on `IX_BookedSeats_ShowtimeId_Row_SeatNumber`).
  * **Assertions**: Verifies the exception is caught gracefully and re-queries to return the colliding seat coordinates rather than failing with an unhandled 500 error.

#### 5. 🔄 Refund & Cancellation Integrity
* `CancelBookingsForPaymentAsync_WhenShowtimeNotFound_ReturnsFailureWithMessage`
* `CancelBookingsForPaymentAsync_WhenPaymentSeatsEmpty_ReturnsFailureWithMessage`
* `CancelBookingsForPaymentAsync_WhenNoMatchingBookedSeatsFound_ReturnsFailureWithMessage`
* `CancelBookingsForPaymentAsync_WhenPartialSeatsCancelled_DeletesMatchedSeatsAndPreservesBookingStatus`
  * When 1 of 2 seats is refunded, only the targeted `BookedSeat` is deleted; the remaining seat and `Booking.Status = Confirmed` are preserved.
* `CancelBookingsForPaymentAsync_WhenAllSeatsForBookingCancelled_UpdatesBookingStatusToCancelled`
  * When all seats for a booking are refunded, transitions `Booking.Status` to `Cancelled`.
* `CancelBookingsForPaymentAsync_WhenSoldOutShowtimeHasCapacityFreed_RevertsStatusToScheduled`
  * If a `SoldOut` showtime drops below capacity after a refund, automatically re-opens the session by setting `Showtime.Status = Scheduled`.
* `CancelBookingsForPaymentAsync_WhenSoldOutShowtimeStillAtOrAboveCapacity_StatusRemainsSoldOut`
  * If remaining seats still meet capacity, preserves `ShowtimeStatus.SoldOut`.

---

### Phase 2 — Payments (Stripe SDK Boundary) ✅ (Implemented in `PaymentServiceTests.cs`)

The `PaymentService` test suite covers **9 comprehensive scenarios** validating Stripe interaction, idempotency, security boundaries, and auto-refunds:

#### 1. ⚡ Intent Creation & Deduplication
* `CreateIntentAsync_WhenShowtimeNotFound_ReturnsNullAndNeverCallsStripeOrSaves`
  * Missing showtime returns `null`; verifies Stripe `CreateAsync` and `UoW.SaveAsync` are **never called**.
* `CreateIntentAsync_WhenDuplicatePendingPaymentExists_ReturnsCachedIntentWithoutCallingStripe`
  * If a pending payment for `(userId, showtimeId, seatHash)` already exists in the database, returns cached client secret and intent ID without calling Stripe.
* `CreateIntentAsync_WithValidSeats_CalculatesAmountInMinorUnitsAndCreatesPendingPayment`
  * Standard Hall Base ($100): Row 1 Regular ($100) + Row 10 VIP ($150) = $250.
  * **Assertions**: Verifies Stripe `Amount` is converted to **25,000 minor units (cents/piastres)**, sets metadata (`userId`, `showtimeId`), computes sorted `SeatHash` (`"1:5,10:5"`), and records `Payment` with `Status = PaymentStatus.Pending`.

#### 2. 🛡️ Security & Idempotency in Confirmation
* `ConfirmAsync_WhenPaymentIntentStatusNotSucceeded_ReturnsFailureAndNeverCreatesBooking`
  * If Stripe intent status is incomplete (e.g. `requires_payment_method`), returns failure (`"Payment has not been completed."`); verifies `BookingService.CreateAsync` is **never called**.
* `ConfirmAsync_WhenUserIdDoesNotMatchIntentMetadata_ReturnsUnauthorizedFailure`
  * **Security Check**: When the caller's `userId` does not match `paymentIntent.Metadata["userId"]`, returns failure (`"Unauthorized access."`) and blocks booking creation.
* `ConfirmAsync_WhenPaymentAlreadyCompletedInDb_ReturnsCachedSuccessAndNeverCreatesBooking`
  * **Idempotency**: If payment in DB is already `PaymentStatus.Completed`, returns existing booking reference without creating duplicate bookings.

#### 3. 💳 Happy Path & Conflict Auto-Refund
* `ConfirmAsync_WhenPaymentSucceeded_CompletesPaymentGeneratesQrAndSendsEmail`
  * Valid payment $\rightarrow$ creates booking, updates `Payment.Status = PaymentStatus.Completed`, sets `CompletedAt`, generates QR code via `IQrCodeService`, and dispatches confirmation email via `IEmailService`.
* `ConfirmAsync_WhenBookingReturnsSeatConflict_TriggersStripeRefundAndMarksPaymentRefunded`
  * **Auto-Refund on Collision**: If seats were taken concurrently by another user, catches conflict, invokes `RefundService.CreateAsync(PaymentIntent = paymentIntentId)`, and sets `Payment.Status = PaymentStatus.Refunded`.
* `ConfirmAsync_WhenEmailSendingThrows_BookingStillSucceeds`
  * **Fault Tolerance**: If `IEmailService` throws an SMTP exception, verifies the confirmed booking is still returned successfully without failing the customer transaction.

### Phase 3 — Authentication & Security ✅ (Implemented in `AuthApiServiceTests.cs`)

The `AuthApiService` test suite covers **23 comprehensive scenarios** validating anti-enumeration, OTP verification, login gates, dual-token rotation, and password recovery:

#### 1. 📝 Registration & Anti-Enumeration
* `RegisterAsync_WhenNewUser_CreatesUserGeneratesOtpAndSendsEmail`
  * New user registration creates `AppUser`, generates random 6-digit OTP, sets 15-minute expiration, and dispatches verification email.
* `RegisterAsync_WhenExistingUserAlreadyConfirmed_ReturnsFailureAndNeverSendsOtp`
  * Prevents duplicate registration for confirmed accounts and suppresses email dispatch.
* `RegisterAsync_WhenExistingUserUnconfirmed_ResendsOtpAndReturnsSuccess`
  * **Anti-Enumeration & Recovery**: Refreshes OTP code on unconfirmed accounts and dispatches new email without leaking account existence errors.
* `RegisterAsync_WhenUserManagerCreateFails_ReturnsFailureWithIdentityErrors`
  * Surfaces identity validation errors (e.g., weak password rules) and blocks email dispatch.

#### 2. ✉️ Email Verification & Activation
* `ConfirmEmailAsync_WhenUserNotFound_ReturnsFailure`
* `ConfirmEmailAsync_WhenCodeMismatch_ReturnsFailureAndLeavesEmailUnconfirmed`
  * Wrong OTP code leaves `EmailConfirmed = false` and rejects token issuance.
* `ConfirmEmailAsync_WhenCodeExpired_ReturnsFailure`
  * Code with `VerificationCodeExpiry < UtcNow` is rejected.
* `ConfirmEmailAsync_WhenCodeValid_ActivatesAccountAndIssuesTokens`
  * Valid OTP sets `EmailConfirmed = true`, clears OTP fields, persists new refresh token, and issues JWT access token.

#### 3. 🔑 Authentication & Login Gating
* `LoginAsync_WhenUserNotFoundOrPasswordInvalid_ReturnsFailure`
* `LoginAsync_WhenEmailNotConfirmed_ReturnsEmailNotConfirmedFailure`
  * Valid credentials on unconfirmed account are blocked with message `"Email not confirmed. Please check your inbox."`
* `LoginAsync_WhenCredentialsValidAndConfirmed_ReturnsSuccessWithTokens`
  * Successful login generates access token with embedded permissions and sets refresh token with 7-day expiration.

#### 4. 🔄 Refresh Token Lifecycle & Revocation
* `RefreshTokenAsync_WhenRefreshTokenNotFound_ReturnsFailure`
* `RefreshTokenAsync_WhenRefreshTokenExpired_ReturnsFailure`
* `RefreshTokenAsync_WhenRefreshTokenValid_RotatesRefreshTokenAndReturnsSuccess`
  * **Token Rotation**: Generates a new access token and simultaneously rotates to a brand-new refresh token in the database.
* `LogoutAsync_WhenRefreshTokenValid_ClearsRefreshTokenAndExpiry`
  * Session revocation sets `user.RefreshToken = null` and `user.RefreshTokenExpiry = null`.
* `LogoutAsync_WhenRefreshTokenNullOrWhitespace_ReturnsSilently`

#### 5. 🔐 Resend & Password Recovery
* `ResendEmailConfirmationAsync_WhenUserExistsAndUnconfirmed_SendsNewOtp`
* `ResendEmailConfirmationAsync_WhenUserNotFoundOrAlreadyConfirmed_ReturnsSilently`
* `ForgetPasswordAsync_WhenUserExistsAndConfirmed_GeneratesResetLinkAndSendsEmail`
  * Generates reset token via `UserManager`, encodes token using Base64URL, and sends formatted reset email.
* `ForgetPasswordAsync_WhenUserNotFoundOrUnconfirmed_ReturnsSilently` (Anti-enumeration)
* `ResetPasswordAsync_WhenUserNotFound_ReturnsFailure`
* `ResetPasswordAsync_WhenTokenValid_ResetsPasswordAndReturnsSuccess`
* `ResetPasswordAsync_WhenIdentityResetFails_ReturnsFailureWithDescription`

---

### Phase 4 — Showtime Scheduling & 15-Minute Buffer ✅ (Implemented in `ShowtimeServiceTests.cs`)

The `ShowtimeService` test suite covers **19 comprehensive scenarios** validating advance scheduling rules, turnaround buffers, immutability gates, deletion safety, and Gantt chart timeline batch persistence:

#### 1. 🕐 Showtime Creation & 15-Minute Turnaround Buffer
* `CreateAsync_WhenStartTimeInPast_ReturnsPastError`
  * Rejects past start times (`"A showtime cannot be scheduled in the past."`).
* `CreateAsync_WhenStartTimeLessThan5HoursFromNow_Returns5HourAdvanceError`
  * Enforces the 5-hour advance scheduling window (`"A showtime must be scheduled at least 5 hours from now."`).
* `CreateAsync_WhenMovieNotFound_ReturnsMovieNotFoundError` & `CreateAsync_WhenHallNotFound_ReturnsHallNotFoundError`
* `CreateAsync_WhenTurnaroundBufferHasConflict_ReturnsConflictError`
  * Rejects overlapping sessions in the same hall.
* `CreateAsync_WhenValid_CalculatesEndTimeWith15MinBufferAndCreatesScheduledShowtime`
  * Validates that `EndTime` equals `StartTime + RuntimeMinutes + 15 minutes buffer` (e.g. 120m movie + 15m = 135m duration) and persists `ShowtimeStatus.Scheduled`.

#### 2. 🔄 Showtime Rescheduling & Immutability Gates
* `UpdateAsync_WhenStartTimeInPast_ReturnsPastError` & `UpdateAsync_WhenStartTimeLessThan5HoursFromNow_Returns5HourAdvanceError`
* `UpdateAsync_WhenShowtimeNotFound_ReturnsShowtimeNotFoundError`
* `UpdateAsync_WhenExistingShowtimeStartsInLessThan5Hours_ReturnsCannotEditError`
  * **Immutability Rule**: Editing a session whose current start time is $< 5$ hours away is blocked.
* `UpdateAsync_WhenShowtimeAlreadyCompleted_ReturnsCompletedError`
  * **Completed Lock**: Modifying completed historical sessions is strictly forbidden.
* `UpdateAsync_WhenValid_UpdatesStartTimeEndTimePriceAndSaves`
  * Recalculates end time with the 15-minute buffer and persists updated price.

#### 3. 🛡️ Showtime Deletion & Booking Integrity
* `DeleteAsync_WhenShowtimeNotFound_ReturnsShowtimeNotFoundError`
* `DeleteAsync_WhenShowtimeHasBookings_ReturnsCannotRemoveError`
  * **Financial Audit Protection**: Prevents hard deleting showtimes that have booking or payment history (`"Can't remove this showtime — it has bookings or payments."`).
* `DeleteAsync_WhenUnbooked_DeletesShowtimeAndSaves`
  * Deletes unbooked sessions cleanly.

#### 4. 🪑 Seat Map Synthesis & Timeline Batching
* `GetSeatMapAsync_WhenShowtimeNotFound_ReturnsNull`
* `GetSeatMapAsync_WhenShowtimeExists_SynthesizesVirtualLayoutWithPricingAndBookedSeats`
  * Synthesizes mathematical rows/seats, category mappings, multipliers, and active reservations.
* `SaveBatchAsync_WithValidBatchChanges_PerformsCreateUpdateDeleteAndCommits`
  * Executes multi-item Gantt chart actions (create, update, delete) in a single atomic transaction.
* `SaveBatchAsync_WhenBatchItemsHaveErrors_PopulatesErrorsAndCalculatesSuccess`
  * Collects per-item validation errors while allowing valid changes to commit.

---

### Phase 5 — Specifications & Query Builders ✅ (Implemented across 4 Test Classes)

Eliminates the highest-risk complexity hotspots identified in the code coverage report (**Crap Score 380** on `PaymentManagementSpecification`, **182 / 72** on `MovieSpecification`):

#### 1. 💳 `PaymentManagementSpecificationTests.cs` (19 tests)
* **Search Filters**: Tests keyword searching across `User.FirstName + LastName`, `User.Email`, `Showtime.Movie.Title`, and `BookingReference`.
* **Column Ordering Matrix**: Validates ascending and descending ordering for all 6 sortable DataTables columns:
  * Column 0: `User.FirstName` (`asc` / `desc`)
  * Column 1: `User.Email` (`asc` / `desc`)
  * Column 2: `Showtime.Movie.Title` (`asc` / `desc`)
  * Column 3: `TotalAmount` (`asc` / `desc`)
  * Column 4: `Status` (`asc` / `desc`)
  * Column 5: `CreatedAt` (`asc` / `desc`)
  * Fallback / Default: `CreatedAt DESC`
* **Navigation Includes & Paging**: Verifies eager loading of `User`, `Showtime`, `PaymentSeats`, `"Showtime.Movie"`, and calculates `Skip`/`Take`.

#### 2. 🎬 `MovieSpecificationTests.cs` (16 tests)
* **Archiving Partition**: Asserts `archivedOnly = false` excludes archived movies and `archivedOnly = true` selects only archived movies.
* **Filter Combinations**: Tests combining `MovieStatus` filters with search query terms.
* **Sort Permutations**: Verifies ordering by `VoteAverage`, `ReleaseDate`, `ImportedAt`, `RuntimeMinutes`, and `Status`.
* **Deep Includes**: Verifies eager loading of `Genres` and `Cast`.

#### 3. 🛡️ `PaymentSpecificationTests.cs` (6 tests)
* **Deduplication Specification**: Validates the compound filter `UserId + ShowtimeId + SeatHash + PaymentStatus.Pending`.
* **Filtered Queries**: Tests optional filtering by `ShowtimeId`, `UserId`, and `PaymentStatus`.
* **Eager Loading**: Verifies inclusion of `PaymentSeats`, `User`, `Showtime`, `"Showtime.Movie"`, and `"Showtime.Hall"`.

#### 4. 📂 `BookingHistorySpecificationTests.cs` (6 tests)
* **Temporal Partitioning**:
  * `Upcoming`: Filters `Showtime.StartTime >= UtcNow` and sorts by `StartTime ASC` (soonest first).
  * `Past`: Filters `Showtime.StartTime < UtcNow` and sorts by `StartTime DESC` (most recent first).
  * `All`: Retrieves all user bookings sorted by `BookedAt DESC`.
* **Paging & Count Accuracy**: Validates mathematical page offset calculation `((page - 1) * pageSize, pageSize)` and proves `BookingHistoryCountSpecification` matches criteria.

---

### Phase 6 — Permissions & RBAC ✅ (Implemented across 3 Test Classes)

Validates authorization handlers, reflection discovery, and role claim synchronization:

#### 1. 🛡️ `PermissionAuthorizationHandlerTests.cs` (4 tests)
* `HandleRequirementAsync_WhenUserHasMatchingPermissionClaim_SucceedsRequirement`
  * Matches `Claim("permission", "movies:import")` and marks authorization context as succeeded.
* `HandleRequirementAsync_WhenUserHasDifferentPermissionClaim_FailsExplicitly`
  * Missing exact claim fails authorization explicitly.
* `HandleRequirementAsync_WhenUserHasNoPermissionClaims_FailsExplicitly` & `HandleRequirementAsync_WhenUserIsUnauthenticated_FailsExplicitly`

#### 2. 🔐 `RoleServiceTests.cs` (12 tests)
* **Creation & Claim Assignment**:
  * Blocks duplicate role names (`RoleExistsAsync`).
  * Creates `AppRole` and attaches all selected permissions as `Claim("permission", val)` entries.
* **Update & Delta Synchronization**:
  * Validates updating role details and synchronizing permission claims (strips removed claims, attaches newly checked claims).
* **Deletion & User Assignment Guard**:
  * `DeleteAsync` checks `UserRoles.CountAsync(ur => ur.RoleId == role.Id)`.
  * If active users are assigned (e.g. 2 users), blocks deletion with error `"Cannot delete role 'BoxOffice' — 2 user(s) are assigned to it."` and prevents `DeleteAsync`.
  * Deletes unassigned roles cleanly.

#### 3. 🧩 `PermissionsTests.cs` (2 tests)
* **Reflection Engine**: Verifies `Permissions.GetAll()` dynamically discovers all nested constants across all domain modules without duplicates.
* **Naming Conventions**: Proves all discovered permissions strictly follow the `module:action` lowercase standard.

---

### Phase 7 — User Profile & History ✅ (Implemented in `ProfileServiceTests.cs`)

Validates profile management, password updates with session preservation, and infinite-scroll pagination:

#### 1. 👤 Profile Data Lifecycle
* `GetProfileAsync_WhenUserNotFound_ReturnsNull` & `GetProfileAsync_WhenUserExists_ReturnsMappedProfileDto`
  * Verifies mapping of `FirstName`, `LastName`, `Email`, `DateOfBirth`, and `Theme`.
* `UpdateProfileAsync_WhenValid_UpdatesFieldsAndReturnsSuccess`
  * Updates editable fields on `AppUser` while keeping email locked.
* `UpdateProfileAsync_WhenIdentityFails_ReturnsFailureWithErrors`

#### 2. 🔐 Password Update & Active Session Invariance
* `ChangePasswordAsync_WhenUserNotFound_ReturnsFailure` & `ChangePasswordAsync_WhenIdentityFails_ReturnsFailureWithErrors`
  * Validates Identity error propagation for incorrect current passwords or policy failures.
* `ChangePasswordAsync_WhenValid_SucceedsAndPreservesActiveRefreshTokens`
  * **Core Security Invariant**: Verifies changing password succeeds while leaving `user.RefreshToken` and `user.RefreshTokenExpiry` **100% untouched** so active mobile/web sessions stay alive.

#### 3. 📂 Booking History Pagination & Projection
* `GetBookingHistoryAsync_WhenPageSizeExceedsLimit_ClampsPageSizeTo25`
  * **Security Clamping**: Requests $> 25$ items (e.g. 100) are automatically clamped to `PageSize = 25`.
* `GetBookingHistoryAsync_WhenHasMorePages_ReturnsHasMoreTrue` & `GetBookingHistoryAsync_WhenOnLastPage_ReturnsHasMoreFalse`
  * Verifies mathematical `HasMore` calculation `(page * pageSize) < totalCount`.
* `GetBookingHistoryAsync_ProjectsBookingFieldsAndSeatCountCorrectly`
  * Asserts `SeatCount = b.BookedSeats.Count`, UTC timestamps, movie title, poster, and total amount.

---

### Phase 8 — Background Services & Archiving ✅ (Implemented across 3 Test Classes)

Validates background task cycles, session completion specifications, and soft-delete invariants:

#### 1. ⚙️ `ShowtimeCompletionSpecificationTests.cs` (7 tests)
* **`ShowtimeCloseBookingsSpecification` (4 tests)**:
  * Matches sessions starting within 10 minutes (`StartTime <= UtcNow + 10m`) in `Scheduled` or `SoldOut` status to close bookings.
  * Excludes future sessions ($> 10$ mins) and already archived sessions.
* **`ShowtimeCompletionSpecification` (3 tests)**:
  * Matches expired sessions (`Status == Completed && !IsArchived && EndTime < UtcNow`).
  * Excludes currently running sessions (`EndTime >= UtcNow`) and already archived sessions.

#### 2. 🔄 `ShowtimeCompletionServiceTests.cs` (3 tests)
* **Automated Cycle Execution**:
  * Closes bookable sessions when starting time threshold is reached $\rightarrow$ updates `Status = Completed` and commits `SaveAsync()`.
  * Archives expired sessions $\rightarrow$ updates `IsArchived = true`, assigns UTC timestamp to `ArchivedAt`, and commits `SaveAsync()`.
  * Suppresses redundant database writes when zero sessions require updates.

#### 3. 🎬 `MoviesServiceTests.cs` (6 tests)
* **Status Archiving Lifecycle**:
  * Setting status to `MovieStatus.Archived` sets `IsArchived = true` and records `ArchivedAt = UtcNow`.
  * Reactivating status to `MovieStatus.Active` resets `IsArchived = false` and clears `ArchivedAt = null`.
* **Deletion Safety Guard**:
  * Prevents deleting movies that still have linked showtime sessions (`"Can't remove this movie — it still has showtimes."`).
  * Deletes unlinked movies cleanly.

---

### Phase 9 — Integration: The Concurrency Test ⭐ (Planned for Multi-Container CI)
* Spawns a real disposable SQL Server via **Testcontainers.MsSql** or Docker Compose.
* Fires parallel `CreateAsync` requests for the **exact same seat** at the same instant.
* Proves that the `(ShowtimeId, Row, SeatNumber)` unique constraint blocks the second request, triggering the automated conflict and refund path.

---

### Phase 10 — Minimal E2E Golden Path (Playwright)
* Executes the complete user journey: Browse Movie $\rightarrow$ Pick Showtime $\rightarrow$ Select Seat $\rightarrow$ Stripe Checkout $\rightarrow$ View QR Ticket.

---

## 📌 Definition of Done for Any Test

1. Assertions test specific semantic values rather than generic `NotNull` or `DoesNotThrow`.
2. Expected test values are calculated independently of implementation code.
3. Mutations applied to the production code cause the test to fail.
4. Happy path and failure/edge conditions are both verified.
5. Tests are fully isolated with zero shared mutable state or execution-order dependency.
