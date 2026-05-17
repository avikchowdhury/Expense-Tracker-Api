# ExpenseTracker.Api

This is the backend for the Expense Tracker app.

It runs on ASP.NET Core 8, uses EF Core for data access, and handles auth, receipts, budgets, AI endpoints, notifications, exports, and admin APIs.

## How the backend is split

The solution is no longer just one API project.

- `ExpenseTracker.Api` is the actual web app. It contains `Program.cs`, controllers, middleware, and the API startup wiring.
- `ExpenseTracker.Services` is the business/application layer. It holds the higher-level services that coordinate auth, receipts, budgets, AI features, profile workflows, and category workflows.
- `ExpenseTracker.Infrastructure` is the low-level implementation layer. It owns EF Core, repositories, DbContext, JWT generation, email delivery, avatar file storage, and the AI HTTP client integrations.
- `ExpenseTracker.Shared` is for shared types. That project builds the code under `Dtos/`, `Models/`, and the shared constants/helpers we want available across the backend.
- `Tests/ExpenseTracker.Api.Tests` is the test project.

## One thing that can be confusing at first

The folders are still at the repo root, but they are compiled by different projects.

So for example:

- `Controllers/` belongs to the API project
- `Services/` is split between application services and infrastructure implementations depending on the project file that includes the code
- `Data/` belongs to the infrastructure project
- `Dtos/` and `Models/` belong to the shared project

That setup lets us separate responsibilities without physically moving every folder into a new nested project structure.

## Main folders

- `Controllers/` HTTP endpoints
- `Middleware/` request pipeline behavior
- `Extensions/` API registration and startup helpers
- `Services/` application services plus a few infrastructure implementations compiled by different projects
- `Data/` EF Core context, repositories, and unit of work implementations
- `Dtos/` request and response models
- `Models/` entity models
- `Security/` roles, auth helpers, and authorization-related types
- `Configuration/` settings classes
- `Modules/Shared/Constants/` shared non-sensitive constants
- `Tests/ExpenseTracker.Api.Tests/` automated tests

## Configuration

Real secrets should stay in configuration, not in source files.

Use:

- `appsettings.json` for safe committed defaults
- `appsettings.Development.json` for local machine settings and secrets
- user secrets or environment variables if you want to avoid keeping secrets in a local JSON file

The backend currently expects these config sections:

- `ConnectionStrings`
- `JwtSettings`
- `Email`
- `OpenAI`
- `AzureAI`
- `Storage`

Important: `ApplicationText.cs` is only for normal shared text and safe constant values. It should not hold connection strings, API keys, SMTP passwords, JWT secrets, or anything similar.

## Running locally

```bash
dotnet restore
dotnet build ExpenseTracker.Api.csproj
dotnet run --project ExpenseTracker.Api.csproj
```

## Running tests

```bash
dotnet test Tests/ExpenseTracker.Api.Tests/ExpenseTracker.Api.Tests.csproj
```

## Current state

The backend builds and the test project passes with the current split:

- `ExpenseTracker.Api`
- `ExpenseTracker.Services`
- `ExpenseTracker.Infrastructure`
- `ExpenseTracker.Shared`
