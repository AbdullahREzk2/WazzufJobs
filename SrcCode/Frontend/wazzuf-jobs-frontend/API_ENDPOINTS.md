# Wazzuf Jobs — useful API routes (SPA base: `environment.apiUrl`)

All paths below are relative to the API root (e.g. `https://localhost:7xxx/api`).

## Account (JWT required)

| Method | Path | Body / notes |
|--------|------|----------------|
| GET | `/account/userInfo` | Returns profile: `email`, `userName`, `firstName`, `lastName`, `profilePhotoUrl` |
| PUT | `/account/update-User-Info` | JSON `{ "firstName", "lastName" }` |
| PUT | `/account/change-password` | JSON `{ "currentPassword", "newPassword" }` |
| POST | `/account/profile-image` | `multipart/form-data` field name **`image`** (file) |

## Saved jobs (JWT + permission)

| Method | Path | Notes |
|--------|------|--------|
| GET | `/SavedJobs` | List saved jobs (PascalCase controller name) |
| POST | `/SavedJobs/{jobId}` | Save a job |
| DELETE | `/SavedJobs/{jobId}` | Unsave |

## Admin — users (permission-gated)

| Method | Path |
|--------|------|
| GET | `/users` |
| GET | `/users/{userId}` |
| PUT | `/users/{userId}` |
| PUT | `/users/{userId}/toggle-status` |
| PUT | `/users/{userId}/unlock` |

## Admin — roles (permission-gated)

| Method | Path |
|--------|------|
| GET | `/roles` |
| GET | `/roles/{id}` |
| POST | `/roles` |
| PUT | `/roles/{roleId}` |
| PUT | `/roles/{roleId}/toggle-status` |
