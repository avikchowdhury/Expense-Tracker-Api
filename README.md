# ExpenseTracker.Api

A production-ready REST API for the AI-powered Expense Tracker application. Built with ASP.NET Core 8, Entity Framework Core, and Groq AI — it handles receipt parsing, budget management, financial forecasting, anomaly detection, and natural-language expense entry.

---

## Table of Contents

1. [Tech Stack](#tech-stack)
2. [Architecture](#architecture)
3. [Modules & Endpoints](#modules--endpoints)
4. [Configuration](#configuration)
5. [How to Run](#how-to-run)
6. [Database](#database)
7. [AI Integration](#ai-integration)

---

## Tech Stack

| Layer | Technology |
|---|---|
| Framework | ASP.NET Core 8 (`net8.0`) |
| Language | C# 12 (nullable enabled, implicit usings) |
| ORM | Entity Framework Core 9 (Code-First) |
| Database | SQL Server 2019+ |
| Auth | JWT Bearer — HS256, configurable expiry |
| AI | Groq via OpenAI Responses API (`/v1/responses`) |
| File Storage | Azure Blob Storage (`Azure.Storage.Blobs 12.27`) |
| Excel Export | EPPlus 7 |
| JSON | Newtonsoft.Json 13 |
| API Docs | Swashbuckle / Swagger UI (`/swagger`) |
| Secrets | .NET User Secrets (`UserSecretsId: expense-tracker-api-otp-mailer`) |

---

## Architecture

```
ExpenseTracker.Api/
├── Controllers/          # 12 REST controllers
├── Services/             # Business logic (interface + implementation)
│   ├── IAIService.cs / AIService.cs
│   ├── IEmailService.cs / EmailService.cs
│   └── ...
├── Data/
│   ├── AppDbContext.cs          # EF Core DbContext
│   ├── IUnitOfWork.cs           # Unit-of-Work interface
│   ├── UnitOfWork.cs            # Wraps all repositories
│   └── Repositories/            # One repository per entity
├── Models/               # EF entity classes
├── DTOs/                 # Request/Response transfer objects
├── Migrations/           # EF Core migration history
└── Program.cs            # DI registration, middleware pipeline
```

**Patterns used:**
- **Repository + Unit of Work** — all data access goes through `IUnitOfWork`, no raw DbContext calls in controllers
- **Interface-driven services** — every service has a matching `I*Service` interface registered in DI
- **JWT middleware** — all non-auth routes require `[Authorize]`; admin routes additionally require the `Admin` role
- **AI fallback** — if the model returns no valid JSON, every AI method returns a safe default DTO rather than throwing

---

## Modules & Endpoints

### Auth — `/api/auth`
| Method | Route | Description |
|--------|-------|-------------|
| POST | `/api/auth/register` | Create account (returns OTP) |
| POST | `/api/auth/verify-otp` | Confirm email OTP, receive JWT |
| POST | `/api/auth/login` | Username + password login, receive JWT |
| POST | `/api/auth/resend-otp` | Resend verification OTP |

### Receipts — `/api/receipts`
| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/receipts` | List receipts (paged, filterable) |
| POST | `/api/receipts` | Upload receipt file + AI parse |
| GET | `/api/receipts/{id}` | Get single receipt |
| PUT | `/api/receipts/{id}` | Update receipt fields |
| DELETE | `/api/receipts/{id}` | Delete receipt |

### Budgets — `/api/budgets`
| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/budgets` | List all budgets for user |
| POST | `/api/budgets` | Create monthly category budget |
| PUT | `/api/budgets/{id}` | Update budget amount |
| DELETE | `/api/budgets/{id}` | Delete budget |
| GET | `/api/budgets/status` | Current month spend vs all budgets |

### Categories — `/api/categories`
| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/categories` | List user categories |
| POST | `/api/categories` | Create category |
| PUT | `/api/categories/{id}` | Rename / update category |
| DELETE | `/api/categories/{id}` | Delete category |
| GET | `/api/categories/{id}/vendor-rules` | List vendor rules for category |
| POST | `/api/categories/{id}/vendor-rules` | Add vendor rule pattern |
| DELETE | `/api/categories/{id}/vendor-rules/{ruleId}` | Remove vendor rule |

### Analytics — `/api/analytics`
| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/analytics/monthly-summary` | Month-over-month totals |
| GET | `/api/analytics/category-breakdown` | Spend per category (current month) |
| GET | `/api/analytics/recurring` | Detected recurring/subscription expenses |

### AI — `/api/ai`
| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/ai/insights` | AI overview: narrative + top risks |
| GET | `/api/ai/budget-advice` | Per-budget pace advice |
| GET | `/api/ai/subscriptions` | Subscription list with AI risk score |
| GET | `/api/ai/anomalies` | Spending anomalies vs peer averages |
| GET | `/api/ai/forecast` | Month-end projection + daily bar data |
| GET | `/api/ai/vendor-analysis` | Per-vendor breakdown + AI observation |
| GET | `/api/ai/budget-coach` | Personalised coach message |
| POST | `/api/ai/chat` | Free-form copilot chat (markdown response) |
| POST | `/api/ai/parse-text` | Extract receipt fields from natural language |
| POST | `/api/ai/check-duplicate` | Detect duplicate within 7-day window |
| POST | `/api/ai/tag-receipt` | Suggest category + tags for a receipt |

### Notifications — `/api/notifications`
| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/notifications` | Budget alerts + anomalies + upcoming charges |
| POST | `/api/notifications/mark-read` | Mark notification(s) as read |

### Export — `/api/export`
| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/export/excel` | Download all receipts as `.xlsx` |
| GET | `/api/export/report` | Download 3-sheet AI report (Summary, Receipts, Insights) |

### Profile — `/api/profile`
| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/profile` | Get profile details |
| PUT | `/api/profile` | Update name, bio, avatar URL |
| PUT | `/api/profile/password` | Change password |
| POST | `/api/profile/avatar` | Upload avatar image |

### Admin — `/api/admin` _(role: Admin only)_
| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/admin/users` | List all users with stats |
| PUT | `/api/admin/users/{id}/role` | Assign / revoke Admin role |
| GET | `/api/admin/stats` | Workspace-wide aggregates |

---

## Configuration

Create `appsettings.Development.json` (never commit to source control):

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost,1433;Database=ExpenseTrackerDb;User Id=sa;Password=YOUR_SA_PASSWORD;TrustServerCertificate=True;"
  },
  "Jwt": {
    "Secret": "a-long-random-secret-at-least-32-characters",
    "Issuer": "ExpenseTrackerApi",
    "Audience": "ExpenseTrackerClient",
    "ExpiryMinutes": 1440
  },
  "OpenAI": {
    "ApiKey": "gsk_YOUR_GROQ_API_KEY",
    "Model": "openai/gpt-oss-20b",
    "ResponsesEndpoint": "https://api.groq.com/openai/v1/responses"
  },
  "AzureBlob": {
    "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=...;",
    "ContainerName": "receipts"
  },
  "Email": {
    "SmtpHost": "smtp.gmail.com",
    "SmtpPort": 587,
    "SenderEmail": "you@gmail.com",
    "SenderPassword": "app-password"
  }
}
```

> **Tip:** Use .NET User Secrets during local development:
> ```bash
> dotnet user-secrets set "Jwt:Secret" "your-secret"
> dotnet user-secrets set "OpenAI:ApiKey" "gsk_..."
> ```

---

## How to Run

### Prerequisites
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8)
- SQL Server 2019+ (Docker or local install)
- (Optional) Azure Storage account for receipt file uploads

### Steps

```bash
# 1. Clone the repo
git clone https://github.com/your-org/expense-tracker.git
cd expense-tracker/ExpenseTracker.Api

# 2. Create appsettings.Development.json (see Configuration above)

# 3. Apply EF Core migrations to create the database
dotnet ef database update

# 4. Run the API (default: https://localhost:5001)
dotnet run

# 5. Open Swagger UI
# https://localhost:5001/swagger
```

### Docker (optional)

A `Dockerfile` is included at the repo root. Build and run:

```bash
docker build -t expense-tracker-api .
docker run -p 5001:5001 \
  -e "ConnectionStrings__DefaultConnection=Server=host.docker.internal,1433;..." \
  -e "Jwt__Secret=your-secret" \
  -e "OpenAI__ApiKey=gsk_..." \
  expense-tracker-api
```

---

## Database

Tables created by EF Core migrations:

| Table | Description |
|---|---|
| `Users` | Accounts and OTP verification state |
| `Receipts` | Primary expense records (vendor, amount, date, category, file URL) |
| `Categories` | User-defined spend categories |
| `VendorRules` | Regex/substring patterns that auto-assign a category |
| `Budgets` | Monthly category spending limits |
| `Profiles` | Extended user profile info and avatar |

Run `dotnet ef migrations list` to see all applied migrations.

---

## AI Integration

All AI calls go through `AIService` → `TryGenerateModelReplyAsync()` which POSTs to the Groq `/v1/responses` endpoint.

**Capabilities:**

| Feature | Method | What it does |
|---|---|---|
| Financial Insights | `GetInsightsAsync` | Narrative overview + top risk areas |
| Budget Advice | `GetBudgetAdviceAsync` | Per-budget pace + projected overspend |
| Subscription Detection | `GetSubscriptionsAsync` | Groups recurring receipts, adds AI risk score |
| Anomaly Detection | `GetSpendingAnomaliesAsync` | Flags categories with unusual spend |
| Spending Forecast | `GetSpendingForecastAsync` | Month-end projection + daily bar chart data |
| Vendor Analysis | `GetVendorAnalysisAsync` | Per-vendor totals + MoM trend + narrative |
| Notifications | `GetNotificationsAsync` | Combines budget alerts + anomalies + subscriptions |
| NL Expense Parse | `ParseTextExpenseAsync` | Extracts vendor/amount/date/category from free text |
| Duplicate Check | `CheckDuplicateReceiptAsync` | Fuzzy match within 7-day rolling window |
| Receipt Tagging | `TagReceiptAsync` | Suggests category + tags for uploaded receipt |
| Budget Coach | `GetBudgetCoachMessageAsync` | Personalised coaching message |
| Chat | `GetChatResponseAsync` | Multi-turn copilot over user's real data |

If the model returns invalid JSON or times out, each method returns a safe default DTO so the rest of the app continues working normally.
