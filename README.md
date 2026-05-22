# ⚡ Wazzuf Jobs — AI-Powered Job Platform

<div align="center">

![Wazzuf Jobs](https://img.shields.io/badge/Wazzuf-Jobs-00d4ff?style=for-the-badge&logo=lightning&logoColor=white)
![.NET](https://img.shields.io/badge/.NET_9-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)
![Angular](https://img.shields.io/badge/Angular_18-DD0031?style=for-the-badge&logo=angular&logoColor=white)
![Three.js](https://img.shields.io/badge/Three.js-000000?style=for-the-badge&logo=three.js&logoColor=white)
![SQL Server](https://img.shields.io/badge/SQL_Server-CC2927?style=for-the-badge&logo=microsoft-sql-server&logoColor=white)

**A full-stack AI-powered job platform where users upload their CV, apply for jobs, and receive an instant AI match score powered by Groq LLaMA 3.**

[🌐 Live Demo](https://wazzuf-jobs.vercel.app)  • [📖 API Docs](https://wazzufjobs.runasp.net/index.html)

</div>

---

## ✨ Features

### 👤 For Job Seekers
- 🔐 Register, confirm email, login with JWT authentication
- 📄 Upload CV (PDF) — automatically validated and text extracted
- 🔍 Browse and search jobs by title, keyword, location, category, type
- ⚡ Apply for jobs with one click
- 🤖 **AI Match Scoring** — Groq LLaMA 3 scores your CV against the job description instantly in the background
- 📊 Track all your applications and AI scores in your dashboard
- 🔔 Real-time SignalR notification when your score is ready
- 📧 Email notification with your match score and feedback
- 🔖 Save jobs to apply later
- 🎯 Onboarding — set career level, preferred job types, salary, and more

### 🛠️ For Admins
- 📋 Full job management (create, edit, delete, toggle status)
- 🗂️ Job category management with icon upload to Cloudinary
- 👥 View all applicants per job with their AI scores
- 🔑 Role and permission management
- 👤 User management (create, update, toggle status, unlock)
- 📊 Hangfire dashboard for background job monitoring

---

## 🏗️ Architecture

```
┌─────────────────────────────────────────────────────────┐
│                    Angular 18 Frontend                   │
│              Three.js 3D • SCSS • SignalR               │
└────────────────────────┬────────────────────────────────┘
                         │ HTTPS / REST / WebSocket
┌────────────────────────▼────────────────────────────────┐
│                  ASP.NET Core 9 API                      │
│         CQRS + MediatR • JWT • Permissions              │
├──────────────┬──────────────────┬───────────────────────┤
│  Application │  Infrastructure  │       Domain          │
│   (BLL)      │     (DAL)        │    (Entities)         │
└──────┬───────┴────────┬─────────┴───────────────────────┘
       │                │
       ▼                ▼
  ┌─────────┐    ┌────────────┐    ┌──────────────┐
  │  Groq   │    │ SQL Server │    │  Cloudinary  │
  │ LLaMA 3 │    │  (EF Core) │    │  (CV + imgs) │
  └─────────┘    └────────────┘    └──────────────┘
       │
  ┌────▼─────┐    ┌──────────┐
  │ Hangfire │    │ SignalR  │
  │ (bg jobs)│    │ (notify) │
  └──────────┘    └──────────┘
```

---

## 🛠️ Tech Stack

### Backend
| Technology | Purpose |
|---|---|
| ASP.NET Core 9 | Web API |
| Entity Framework Core 9 | ORM |
| SQL Server | Database |
| ASP.NET Identity | Authentication & user management |
| JWT Bearer | Token-based auth |
| CQRS + MediatR | Command/Query separation |
| Hangfire | Background job processing |
| SignalR | Real-time notifications |
| Cloudinary | File & image storage |
| Groq API (LLaMA 3) | AI CV matching & scoring |
| FluentValidation | Request validation |
| Mapster | Object mapping |
| PdfPig | PDF text extraction |
| Swagger | API documentation |

### Frontend
| Technology | Purpose |
|---|---|
| Angular 18 | SPA Framework |
| Three.js | 3D graphics & particles |
| GSAP | Animations |
| SCSS | Styling with CSS variables |
| SignalR Client | Real-time score notifications |
| Vercel | Hosting |

---

## 📁 Project Structure

```
WazzufJobs/
├── WazzufJobs.API/                  # Presentation layer
│   ├── Controllers/                 # API endpoints
│   ├── Services/                    # CurrentUserService, EmailService
│   ├── Filters/                     # HangfireAuthorizationFilter
│   └── DependencyInjection.cs       # Service registration
│
├── WazzufJobs.BLL/                  # Business logic layer
│   ├── Features/                    # CQRS handlers by feature
│   │   ├── Auth/
│   │   ├── Jobs/
│   │   ├── Categories/
│   │   ├── Applications/
│   │   ├── UserCV/
│   │   ├── SavedJobs/
│   │   └── Onboarding/
│   ├── Abstractions/                # Result pattern, Error
│   ├── Authentication/              # JWT, Permissions
│   ├── Services/                    # Cloudinary, AI scoring, CV extraction
│   ├── Helpers/                     # Email helpers
│   ├── Hubs/                        # SignalR hub
│   └── Settings/                    # Configuration classes
│
├── WazzufJobs.DAL/                  # Data access layer
│   ├── Entities/                    # Domain entities
│   ├── Enums/                       # CareerLevel, JobType, etc.
│   ├── IRepository/                 # Repository interfaces
│   ├── Repository/                  # Repository implementations
│   ├── Persistence/
│   │   ├── ApplicationDBContext.cs
│   │   ├── Configurations/          # Fluent API entity configs
│   │   └── Seeders/                 # Role, permission, admin seeders
│   └── Migrations/
│
└── wazzuf-jobs-frontend/            # Angular 18 frontend
    ├── src/app/
    │   ├── core/                    # Services, guards, interceptors, models
    │   ├── features/                # Pages (landing, auth, jobs, admin...)
    │   └── shared/                  # Reusable components
    └── src/environments/
```

---

## 🚀 Getting Started

### Prerequisites
- .NET 9 SDK
- SQL Server
- Node.js 18+
- Angular CLI 18

### Backend Setup

```bash
# Clone the repo
git clone https://github.com/your-username/wazzuf-jobs.git
cd wazzuf-jobs

# Set up user secrets
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "your-connection-string" --project WazzufJobs.API
dotnet user-secrets set "Jwt:key" "your-jwt-secret-min-32-chars" --project WazzufJobs.API
dotnet user-secrets set "CloudinarySettings:ApiKey" "your-key" --project WazzufJobs.API
dotnet user-secrets set "CloudinarySettings:ApiSecret" "your-secret" --project WazzufJobs.API
dotnet user-secrets set "AISettings:ApiKey" "your-groq-key" --project WazzufJobs.API
dotnet user-secrets set "MailSettings:Password" "your-gmail-app-password" --project WazzufJobs.API

# Run migrations
dotnet ef database update --project WazzufJobs.DAL --startup-project WazzufJobs.API

# Run the API
dotnet run --project WazzufJobs.API
```

API runs at `https://localhost:7000`
Swagger at `https://localhost:7000/swagger`
Hangfire at `https://localhost:7000/hangfire`

### Frontend Setup

```bash
cd wazzuf-jobs-frontend

# Install dependencies
npm install

# Run development server
ng serve
```

Frontend runs at `http://localhost:4200`

---

## 🔑 Default Admin Credentials

```
Email:    admin@wazzuf.com
Password: Admin@123456
```

> ⚠️ Change these immediately in production via the `AdminSeeder.cs` file.

---

## 🌐 API Endpoints

### Auth
| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/auth/register` | Register new user |
| POST | `/api/auth/login` | Login + get JWT |
| GET | `/api/auth/confirm-email` | Confirm email |
| POST | `/api/auth/forget-password` | Send reset email |
| POST | `/api/auth/reset-password` | Reset password |
| POST | `/api/auth/refresh-token` | Refresh JWT |
| POST | `/api/auth/revoke-token` | Logout |

### Jobs
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/jobs` | List jobs (paginated + filtered) |
| GET | `/api/jobs/{id}` | Get job detail |
| POST | `/api/jobs` | Create job (Admin) |
| PUT | `/api/jobs/{id}` | Update job (Admin) |
| DELETE | `/api/jobs/{id}` | Delete job (Admin) |
| PUT | `/api/jobs/{id}/toggle-status` | Toggle active/closed (Admin) |

### Applications
| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/applications/job/{jobId}/apply` | Apply for job |
| GET | `/api/applications/my-applications` | My applications + AI scores |
| GET | `/api/applications/job/{jobId}` | Job applicants (Admin) |
| GET | `/api/applications/{id}` | Application detail (Admin) |
| PUT | `/api/applications/{id}/status` | Update status (Admin) |

### CV
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/cv` | Get my CV |
| POST | `/api/cv` | Upload CV (PDF) |
| DELETE | `/api/cv` | Delete CV |

### Categories
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/categories` | List all categories |
| POST | `/api/categories` | Create category (Admin) |
| PUT | `/api/categories/{id}` | Update category (Admin) |
| DELETE | `/api/categories/{id}` | Delete category (Admin) |

### Saved Jobs
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/saved-jobs` | My saved jobs |
| POST | `/api/saved-jobs/{jobId}` | Save a job |
| DELETE | `/api/saved-jobs/{jobId}` | Remove saved job |

### Onboarding
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/onboarding/status` | Get onboarding status |
| POST | `/api/onboarding/complete` | Complete onboarding |

---

## 🤖 AI Scoring Flow

```
1. User applies for a job
2. Application saved → Status: Pending
3. Hangfire enqueues background job
4. PdfPig extracts text from stored CV
5. Groq LLaMA 3 receives CV text + job description
6. AI returns SCORE (0-100) + FEEDBACK
7. Application updated with score
8. SignalR notifies user in real-time
9. Email sent with score details
```

**Scoring criteria:**
- Skills match — 40%
- Experience relevance — 30%
- Education fit — 20%
- Location/work type compatibility — 10%

---

## 🔐 Permission System

| Permission | Admin | User |
|---|---|---|
| `jobs:read` | ✅ | ✅ |
| `jobs:create/update/delete` | ✅ | ❌ |
| `applications:read` | ✅ | ✅ (own only) |
| `applications:create` | ❌ | ✅ |
| `applications:update` | ✅ | ❌ |
| `categories:read` | ✅ | ✅ |
| `categories:create/update/delete` | ✅ | ❌ |
| `users:read/update/delete` | ✅ | ❌ |
| `roles:*` | ✅ | ❌ |
| `cv:upload/delete` | ❌ | ✅ |
| `savedjobs:*` | ❌ | ✅ |

---

## ⚙️ Configuration

### `appsettings.json` keys (use user secrets or environment variables for sensitive values)

```json
{
  "ConnectionStrings": {
    "DefaultConnection": ""
  },
  "Jwt": {
    "key": "",
    "Issuer": "WazzufJobsAPI",
    "Audience": "WazzufJobsClient",
    "ExpiryMinutes": 30
  },
  "MailSettings": {
    "Host": "smtp.gmail.com",
    "Port": 587,
    "UserName": "",
    "Password": "",
    "FromEmail": "",
    "DisplayName": "Wazzuf Jobs"
  },
  "CloudinarySettings": {
    "CloudName": "",
    "ApiKey": "",
    "ApiSecret": ""
  },
  "AISettings": {
    "ApiKey": "",
    "Model": "llama3-8b-8192",
    "BaseUrl": "https://api.groq.com/openai/v1/chat/completions"
  },
  "AppURL": {
    "BaseUrl": "https://your-frontend-url.com"
  }
}
```

---

## 🎓 About

This project was built as a **graduation project** showcasing a modern full-stack architecture with:

- Architecture (3-tier)
- CQRS pattern with MediatR
- Repository pattern
- Result pattern for error handling
- Permission-based authorization
- Real-time features with SignalR
- AI integration with Groq
- 3D frontend with Three.js

---

## 👨‍💻 Author

**Abdullah Rezk**

[![LinkedIn](https://img.shields.io/badge/LinkedIn-0077B5?style=for-the-badge&logo=linkedin&logoColor=white)](https://linkedin.com/in/your-profile)
[![GitHub](https://img.shields.io/badge/GitHub-100000?style=for-the-badge&logo=github&logoColor=white)](https://github.com/your-username)

---

## 📄 License

This project is licensed under the MIT License.

---

<div align="center">
  <strong>Built with ❤️ for the next generation of job seekers</strong>
</div>