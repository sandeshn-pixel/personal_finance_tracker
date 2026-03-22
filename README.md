# Personal Finance Tracker

Production-style personal finance tracker built with:
- Frontend: React + Vite + TypeScript
- Backend: ASP.NET Core Web API (.NET 8)
- Database: PostgreSQL
- ORM: Entity Framework Core

## Prerequisites

- .NET 8 SDK
- Node.js 20+
- PostgreSQL
- Podman and `podman compose` for containerized runs

The repository is pinned to the .NET 8 SDK with [global.json](/d:/Sandesh/Projects/Personal_Fincance_Tracker/global.json) so local CLI commands use the same toolchain consistently.

## Environment Setup

Backend config lives in:
- [backend/.env.example](/d:/Sandesh/Projects/Personal_Fincance_Tracker/backend/.env.example)
- [backend/.env](/d:/Sandesh/Projects/Personal_Fincance_Tracker/backend/.env)

At minimum, set:
- `ConnectionStrings__DefaultConnection`
- `Jwt__Issuer`
- `Jwt__Audience`
- `Jwt__SigningKey`
- `Frontend__AllowedOrigins__0`

For forgot-password email, also set:
- `Email__Enabled`
- `Email__FromAddress`
- `Email__FromName`
- `Email__SmtpHost`
- `Email__Port`
- `Email__Username`
- `Email__Password`
- `Email__UseSsl`

## Build

Backend:

```powershell
cd backend
dotnet build src/FinanceTracker.Api/FinanceTracker.Api.csproj -c Release
```

Frontend:

```powershell
cd frontend
npm install
npm run build
```

## Test

Backend:

```powershell
cd backend
dotnet test tests/FinanceTracker.Backend.Tests/FinanceTracker.Backend.Tests.csproj --no-restore
```

Frontend:

```powershell
cd frontend
npm test
```

## Database Migration

Run EF Core migrations explicitly before first use or after schema changes:

```powershell
cd backend
$env:ConnectionStrings__DefaultConnection="Host=localhost;Port=5432;Database=finance_tracker;Username=postgres;Password=1234"
dotnet ef database update --project src/FinanceTracker.Infrastructure --startup-project src/FinanceTracker.Api
```

## Run Locally

Backend:

```powershell
cd backend
dotnet run --project src/FinanceTracker.Api
```

Frontend:

```powershell
cd frontend
npm run dev
```

Typical local URLs:
- frontend: `http://localhost:5173`
- backend: `https://localhost:7054` or `http://localhost:5147`

## Seed Local Sample Data

After registering at least one local user, you can seed realistic sample accounts, transactions, budgets, goals, and recurring rules:

```powershell
dotnet run --project backend/tools/SeedSampleData/SeedSampleData.csproj
```

The seed data is idempotent for the same user and is intended to light up dashboard, forecast, and health-score views with meaningful local data.

## Deploy With Podman

Build and start the full stack:

```powershell
cd d:\Sandesh\Projects\Personal_Fincance_Tracker
podman compose up --build
```

Stop the stack:

```powershell
podman compose down
```

Container files:
- [compose.yaml](/d:/Sandesh/Projects/Personal_Fincance_Tracker/compose.yaml)
- [backend/Containerfile](/d:/Sandesh/Projects/Personal_Fincance_Tracker/backend/Containerfile)
- [frontend/Containerfile](/d:/Sandesh/Projects/Personal_Fincance_Tracker/frontend/Containerfile)

Podman notes:
- [podman-deployment.md](/d:/Sandesh/Projects/Personal_Fincance_Tracker/notes/podman-deployment.md)

## Recommended Deployment Sequence

1. Configure real environment values.
2. Make sure PostgreSQL is reachable.
3. Apply EF Core migrations.
4. Build and start backend.
5. Build and start frontend.
6. Verify `/health`.
7. Verify auth, exports, recurring automation, and forgot-password flow.

## Notes

- Migrations are intentionally explicit and do not auto-run on container startup.
- Forgot-password email delivery needs valid SMTP settings.
- For local debugging, running frontend and backend directly is usually faster than containers.
