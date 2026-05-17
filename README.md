# ExpenseTracker.Api

Backend for the Expense Tracker application, built with ASP.NET Core 8 and EF Core.

This solution is now split into three projects so the web API, business logic, and shared contracts are separated more cleanly.

## Solution Structure

```text
ExpenseTracker.Api.sln
|
+-- ExpenseTracker.Api                 # Web host / API shell
|   +-- Program.cs
|   +-- Controllers/
|   +-- Middleware/
|   +-- Extensions/
|   +-- appsettings.json
|   +-- appsettings.Development.json  # local-only secrets and machine config
|
+-- Modules/
|   +-- Service/
|   |   +-- ExpenseTracker.Services.csproj
|   |   +-- GlobalUsings.cs
|   |
|   +-- Shared/
|       +-- ExpenseTracker.Shared.csproj
|       +-- Constants/
|           +-- ApplicationText.cs
|
+-- Data/                             # compiled into ExpenseTracker.Services
+-- Services/                         # compiled into ExpenseTracker.Services
+-- Dtos/                             # compiled into ExpenseTracker.Shared
+-- Models/                           # compiled into ExpenseTracker.Shared
+-- Configuration/                    # shared settings types
+-- Security/                         # shared auth constants + API auth attributes
+-- Tests/
    +-- ExpenseTracker.Api.Tests/
```

## Project Responsibilities

### `ExpenseTracker.Api`

Owns the HTTP layer only:

- `Controllers/`
- `Middleware/`
- API-specific `Extensions/`
- `Program.cs`
- web startup and dependency injection wiring

### `ExpenseTracker.Services`

Owns application logic and persistence:

- `Services/`
- `Data/`
- repository and unit-of-work implementations
- EF Core `DbContext`
- background services

### `ExpenseTracker.Shared`

Owns contracts and reusable shared types:

- `Dtos/`
- `Models/`
- shared constants in `Modules/Shared/Constants/ApplicationText.cs`
- shared settings types such as `Configuration/JwtSettings.cs`
- shared helper types such as role constants and parsing helpers

## Important Note About File Layout

The source code is still physically organized in top-level folders like `Services/`, `Data/`, `Dtos/`, and `Models/`, but those folders are compiled by different projects through project-file includes.

That means:

- files under `Controllers/`, `Middleware/`, and API web extensions belong to `ExpenseTracker.Api`
- files under `Services/` and `Data/` belong to `ExpenseTracker.Services`
- files under `Dtos/`, `Models/`, and selected shared helpers/settings belong to `ExpenseTracker.Shared`

## Request Flow

```text
HTTP Request
-> Controller (ExpenseTracker.Api)
-> Service / Domain Logic (ExpenseTracker.Services)
-> Repository / DbContext (ExpenseTracker.Services)
-> SQL Server
```

## Configuration

Keep real secrets in configuration files or user secrets, not in source code constants.

### Committed config

- `appsettings.json`
  - safe defaults only
  - no real secrets

### Local-only config

- `appsettings.Development.json`
  - connection string
  - JWT secret
  - SMTP credentials
  - OpenAI or Groq-compatible API key

### Active configuration sections

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "..."
  },
  "JwtSettings": {
    "Secret": "...",
    "Issuer": "ExpenseTracker",
    "Audience": "ExpenseTrackerUsers",
    "ExpiryMinutes": 120
  },
  "Email": {
    "SmtpHost": "...",
    "SmtpPort": 587,
    "Username": "...",
    "Password": "...",
    "FromAddress": "...",
    "FromName": "AI Expense Tracker",
    "EnableSsl": true
  },
  "OpenAI": {
    "ApiKey": "...",
    "Model": "openai/gpt-oss-20b",
    "ResponsesEndpoint": "https://api.groq.com/openai/v1/responses"
  },
  "AzureAI": {
    "Endpoint": "...",
    "Key": "..."
  },
  "Storage": {
    "RootPath": "storage",
    "AvatarsFolder": "avatars",
    "ReceiptsFolder": "receipts"
  }
}
```

### Security rule

- No real secrets should be stored in `ApplicationText.cs` or any `.cs` file.
- `ApplicationText.cs` should only contain labels, route names, section names, safe defaults, and non-sensitive messages.

## Main API Areas

- `AuthController` for registration, login, OTP, password reset, and admin bootstrap
- `ReceiptsController` for upload, quick add, paging, bulk operations, and file access
- `BudgetsController` and budget status endpoints for budgets, advisor data, and health snapshots
- `AIController` for receipt parsing, insights, summaries, anomalies, forecasts, vendor analysis, and duplicate checks
- `CategoriesController` for categories and vendor rules
- `NotificationsController` for notification feeds and digest delivery status
- `ProfileController` for profile and avatar management
- `AdminController` for overview, users, role updates, and user deletion
- `ExportController` for Excel and AI report export

## Run Locally

```bash
dotnet restore
dotnet build ExpenseTracker.Api.csproj
dotnet run --project ExpenseTracker.Api.csproj
```

## Tests

```bash
dotnet test Tests/ExpenseTracker.Api.Tests/ExpenseTracker.Api.Tests.csproj
```

## Current Status

The solution currently builds and tests successfully with the split architecture:

- `ExpenseTracker.Api`
- `ExpenseTracker.Services`
- `ExpenseTracker.Shared`
