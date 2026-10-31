# Phase 02 S1: automatic request validation

Status: **implemented locally on `phase/02-domain-identity` (2026-10-31). Not deployed.**
Decision: D-VALIDATION (`docs/decisions/p02-decision-log.md`).

## Why
Eighteen FluentValidation validators were registered (`AddValidatorsFromAssembly`), but **none ever ran**. There was no MediatR pipeline behavior, and the `ValidationException` branch of `GlobalExceptionsHandler` was unreachable. Malformed input reached handlers and failed later and inconsistently:
- Identity errors (400, `errors: [code, message]`);
- `401`;
- database errors (500, for example names over the column length);
- or silent acceptance.

## Design
| Part | Implementation |
|---|---|
| Pipeline | `BLL/Behaviors/ValidationBehavior<TRequest,TResponse>`, registered with `AddOpenBehavior` in `API/DependencyInjection.cs` (`AddMediatRServices`) |
| Execution | Every validator for the request runs **sequentially**, each with a **fresh `ValidationContext`**; a shared context accumulates failures and would duplicate them. All failures are collected; if there are any, a `ValidationException` is thrown and the handler is **not** called. Requests without validators pass through unchanged. |
| Response | `GlobalExceptionsHandler` returns RFC 9457 `application/problem+json`, **400**, as `ValidationProblemDetails` (below) |
| Field names | `BLL/Errors/ValidationErrors.ToClientKey`: the command's wrapper segment is removed and the rest camel-cased, so the keys match the client's JSON fields (`requestDTO.Email` → `email`, `Passrequest.NewPassword` → `newPassword`, `userId` → `userId`) |
| Logging | Validation failures are logged at **Information** with the request path and field **names** only. Other exceptions are logged at Error, as before. |
| Order | Authentication, authorization (permission policies) and the C0 rate limiter all run in the ASP.NET Core pipeline **before** MediatR. Unauthenticated or unauthorized callers therefore get 401 or 403 and never see validation details of endpoints they can't use (tested). |

**Response format:**
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "detail": "Email is required.",
  "errors": { "email": ["Email is required."], "password": ["Password cannot be empty."] },
  "code": "Validation.Failed",
  "traceId": "00-…"
}
```
- **`detail` carries the first message.** The current Angular app reads `error.detail` (or `errors[1]` for the existing `{ errors: [code, message] }` shape), so it shows the specific message **without any frontend change**.
- The shape matches ASP.NET Core's built-in model-binding `ValidationProblemDetails`, which the API already returns for malformed JSON.
- The existing `Result` → `ToProblem()` failure shape (`errors: [code, message]`) is **unchanged** for every non-validation error.

**Sensitive information:**
- Responses and logs contain only field names and the validators' fixed messages. **Attempted values are never returned or logged**; tests use sentinel passwords and emails.
- No internal command structure appears (`requestDTO`, exception text).
- The 400 for a malformed email on `forget-Password` / `Resend-Confirm-Email` depends only on the format, never on account existence, so enumeration protection is unchanged.

## Compatibility review (before enabling)
The rule was that a validator may reject only input that:
- already failed (Identity error, 401 or 500);
- exceeds a database column limit;
- is explicitly declared invalid by the product UI;
- or is documented below as an intended change.

Every validator was compared against the Angular forms, Identity's options and the EF column limits.

| Validator | Adjustment | Reason |
|---|---|---|
| `RegexPatterns.Password` (Register, ResetPassword, ChangePassword, CreateUser) | The special-character **list** is replaced with Identity's rule (any non-ASCII-alphanumeric character); `[0-9]` replaces `\d`; newlines are allowed | The old list rejected passwords Identity accepts (e.g. `.` `/` space). Test `Password_rule_accepts_exactly_what_identity_accepts` proves parity. |
| `LoginCommandValidator` | `MinimumLength(6)` removed | A wrong password must remain an authentication failure (401), not a 400 |
| `CreateJobCommandValidator` | Location and skills optional (location max 200 kept); salary `>= 0`; max salary `>=` min | The admin form treats location and skills as optional and may send 0 |
| `CompleteOnboardingCommandValidator` | Interested categories and job titles optional; min salary `>= 0` | The onboarding form requires only job types and workplace types |
| `CreateUser`, `UpdateUser`, `UpdateUserProfile` validators | Names: `Length(3,100)` → required, max 100 | Registration accepts 1–2 character names (e.g. "Al"), so those users could no longer be edited |
| `CreateUser`, `UpdateUser` | Roles: `NotEmpty` → `NotNull` | An empty list removes all roles. This is a C0-tested contract (`AdminAccountSafetyTests`), which caught the conflict. |
| `CreateRole`, `UpdateRole` | Name `Length(3,200)` → required, max 256 (Identity column); permissions `NotEmpty` → `NotNull` | Previously accepted names and empty permission lists |
| ConfirmEmail, Register (other rules), ResetPassword, Refresh/Revoke, ResendConfirmation, SendResetPassword, CreateCategory, ChangePassword (other rules) | Unchanged | Compatible with all existing clients |

Validators were **not** added to commands that have none (e.g. UpdateJob, UpdateCategory, Google sign-in). S1 only enables the existing ones.

## API behaviour differences (intended)
Successful requests from the current UI are unaffected; this is covered by the 58 C0 tests, the compatibility tests and the full suite. Differences apply only to malformed input:

| Endpoint(s) | Before | Now |
|---|---|---|
| Any endpoint with a validator, invalid input | Mixed: Identity 400 `errors:[code,message]`, 401, 404, 500 or accepted | **400 validation ProblemDetails** (format above) |
| `POST /api/Auth/login` with an empty email or password, or a malformed email | 401 | 400. Well-formed wrong credentials are still **401**, and the C0 rate limit still applies (it runs first). |
| Register / ResetPassword / ChangePassword with a weak password | 400 with the Identity error shape | 400 validation shape (same status) |
| `PUT /api/Account/change-password` with new = current | 204 (Identity allowed it) | 400. This is an existing explicit rule; the admin UI already enforced it. |
| Names or titles over the column length, category name over 100 | 500 (database error) | 400 |
| `POST /api/Users` without a password | 500 (null password) | 400 |
| `forget-Password` / `Resend-Confirm-Email` with a malformed email | 200 (no account) | 400 (format only; no enumeration) |
| Register via API with empty names | Accepted (empty names stored) | 400 (the UI always requires names) |
| Onboarding with experience outside 0–50 or empty job/workplace types; job with negative or inverted salary range; duplicate roles or permissions | Accepted | 400 (ranges the UI declares; data integrity) |

No route, request DTO or success response changed. No schema change. No frontend change.

## Tests
36 new; **95/95 passing** with the 58 C0 tests unchanged.

| File | Coverage |
|---|---|
| `ValidationBehaviorTests` (unit) | No validators → pass-through; valid → handler runs; invalid → handler not called, failures from all validators aggregated without duplicates; multiple messages per field; field-name mapping (8 cases); attempted values never in the error map |
| `RequestValidationTests` (integration) | RFC 9457 shape, content type, `code`, `detail`, `traceId`; every invalid field reported; no echo of sentinel password, email or oversized values, and no internal names; valid registration unaffected; malformed login → 400 but wrong credentials → 401; endpoint without a validator unaffected; authorization before validation (anonymous → 401, candidate on admin endpoints → 403); same-password change → 400 without echo |
| `ValidationCompatibilityTests` (SQL Server) | 2-character names on profile update; onboarding without categories or titles; admin job without skills or location and with zero salary; registration with `.` as the special character; **password-rule parity with Identity** (11 cases) |

## Verification
- `dotnet test`: 95/95.
- `dotnet-ef migrations has-pending-model-changes`: no changes.
- `dotnet list package --vulnerable --include-transitive`: 0.
- Release rebuild: no new warnings (existing NU1701 and CS8618 only).
- Secret scan of the diff: clean.
