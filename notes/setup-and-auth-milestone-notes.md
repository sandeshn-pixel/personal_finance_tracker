# Personal Finance Tracker Notes

## Scope Completed

This document summarizes what was implemented and debugged for the first milestone:

- Backend auth foundation
- Frontend auth pages and app shell
- Environment setup
- PostgreSQL connection and migration setup
- Local development issues resolved along the way

## Project Structure Created

Monorepo-style structure:

```text
Personal_Fincance_Tracker/
  backend/
    src/
      FinanceTracker.Api/
      FinanceTracker.Application/
      FinanceTracker.Domain/
      FinanceTracker.Infrastructure/
  frontend/
  notes/
```

## Backend Work Completed

### Architecture

The backend was organized into clear layers:

- `FinanceTracker.Domain`
  - entities and shared domain types
- `FinanceTracker.Application`
  - DTOs, interfaces, validators, auth contracts
- `FinanceTracker.Infrastructure`
  - EF Core, auth services, token generation, migrations
- `FinanceTracker.Api`
  - controllers, middleware, configuration, startup

### Auth Features Implemented

Implemented:

- user registration
- user login
- JWT access token issuance
- refresh token persistence
- refresh token rotation
- logout with refresh token revocation
- user identity resolution from JWT
- protected `me` endpoint

### Security Decisions Implemented

- password hashing uses ASP.NET Core `PasswordHasher`
- refresh tokens are stored hashed, not plaintext
- refresh token cookie is `HttpOnly`
- access token stays on the frontend in memory
- request validation uses FluentValidation
- entities are not returned directly from the API
- JWT settings come from environment/config

### Database Schema Implemented

Tables intended:

- `users`
- `refresh_tokens`
- `__EFMigrationsHistory`

Schema characteristics:

- unique index on email
- UTC timestamps
- foreign key from refresh tokens to users
- revocation/expiry/session metadata for refresh tokens

## Frontend Work Completed

### Features Implemented

- React + TypeScript app structure
- login page
- signup page
- auth provider
- protected route guard
- silent session restore attempt on app startup
- logout flow
- app shell with placeholder pages

### Placeholder Navigation Added

- Dashboard
- Transactions
- Budgets
- Goals
- Reports
- Recurring
- Accounts
- Settings

### Frontend Libraries Used

- React Router
- React Hook Form
- Zod
- Vite

### UI Direction Used

The UI was built as:

- light
- minimal
- calm
- warm brown / neutral palette
- responsive

## Environment Setup Completed

Created:

- [backend/.env](/d:/Sandesh/Projects/Personal_Fincance_Tracker/backend/.env)
- [frontend/.env](/d:/Sandesh/Projects/Personal_Fincance_Tracker/frontend/.env)

Examples were kept in:

- [backend/.env.example](/d:/Sandesh/Projects/Personal_Fincance_Tracker/backend/.env.example)
- [frontend/.env.example](/d:/Sandesh/Projects/Personal_Fincance_Tracker/frontend/.env.example)

### Backend `.env`

Includes:

- ASP.NET Core environment
- PostgreSQL connection string
- JWT settings
- frontend allowed origins

### Frontend `.env`

Includes:

- `VITE_API_BASE_URL`

## Development Issues Resolved

### 1. Empty Workspace

The workspace started essentially empty, so the full monorepo structure was created from scratch.

### 2. .NET CLI First-Run Permission Issues

The .NET CLI initially failed due to sandbox/profile path issues. Local repo-scoped CLI and package paths were used to keep tooling working inside the project workspace.

### 3. SDK Version Mismatch

The backend was originally pinned to a .NET 8 SDK in `global.json`, but the machine only had .NET 10 installed.

Temporary action taken:

- `backend/global.json` was updated so commands could run with the installed SDK

Important note:

- the project still targets `net8.0`
- long term, the clean setup is to install .NET 8 and pin `global.json` to the installed 8.x SDK

### 4. HTTPS Certificate Trust

The frontend initially failed with:

```text
net::ERR_CERT_AUTHORITY_INVALID
```

The local development certificate was trusted with:

```powershell
dotnet dev-certs https --trust
```

### 5. PostgreSQL Authentication Failure

The app initially failed to connect because the actual PostgreSQL password for the `postgres` user did not match the connection string.

Resolved by:

- identifying the real DB user as `postgres`
- resetting/verifying the password in PostgreSQL
- using the correct password in the connection string

### 6. EF Design-Time Connection Difference

`dotnet ef` succeeded only after temporarily setting the PowerShell environment variable:

```powershell
$env:ConnectionStrings__DefaultConnection="Host=localhost;Port=5432;Database=finance_tracker;Username=postgres;Password=1234"
```

Reason:

- EF design-time context creation was using config/environment values differently than the running API

### 7. Migration Discovery Failure

A major issue was found:

- EF could connect to the database
- but it reported that no migrations were found

Root cause:

- the manually created migration file was missing EF migration metadata attributes

Resolved by updating the migration file with:

- `[DbContext(typeof(ApplicationDbContext))]`
- `[Migration("20260314120000_InitialAuthSchema")]`

## What Migration Commands Were Doing

Command used:

```powershell
dotnet ef database update --project src/FinanceTracker.Infrastructure --startup-project src/FinanceTracker.Api
```

Purpose:

- build the projects
- locate the EF `DbContext`
- connect to PostgreSQL
- compare the database state with EF migrations
- apply any missing schema changes

Why migrations are used:

- schema changes are source-controlled
- DB setup is repeatable
- avoids hand-writing tables manually
- safer for real applications than ad hoc SQL setup

## Current Expected Database Tables

After migration is correctly applied, `public` should contain:

- `__EFMigrationsHistory`
- `users`
- `refresh_tokens`

## Current Run Flow

### Backend

From `backend`:

```powershell
dotnet run --project src/FinanceTracker.Api
```

Expected endpoints:

- `https://localhost:7054`
- `http://localhost:5054`

### Frontend

From `frontend`:

```powershell
npm run dev
```

Expected frontend URL:

- `http://localhost:5173`

## Important Notes Going Forward

### Password Rules

Registration password validation currently requires:

- minimum 12 characters
- one uppercase
- one lowercase
- one number
- one special character

### Auth Refresh Behavior

`401` responses from `/api/auth/refresh` before login are expected. The frontend tries to restore a session on startup. If no refresh cookie exists yet, the API correctly returns unauthorized.

### Production vs Local Development

The current setup is appropriate for local development and milestone work, but future milestones should improve:

- automatic design-time `.env` loading for EF tools
- dev-only migration convenience if desired
- stronger operational setup and secrets handling
- CSRF hardening considerations if cookie auth evolves further

## Suggested Next Cleanup

Recommended near-term follow-up items:

1. Make EF design-time config read `backend/.env` automatically.
2. Re-pin `backend/global.json` to a real installed .NET 8 SDK after .NET 8 is installed locally.
3. Verify the migration is fully applied and `users` / `refresh_tokens` exist.
4. Continue with the next functional milestone only after auth flow is fully stable end to end.

## Phase 2: Accounts, Categories, Transactions, Dashboard

This section summarizes the second implementation phase that was added after the auth foundation was completed.

### Scope Completed

Implemented in this phase:

- accounts module
- category foundation with user defaults
- transactions CRUD
- transfer handling
- balance-safe account updates
- basic dashboard APIs and UI

### Backend Additions

New financial domain models were added:

- `Account`
- `Category`
- `Transaction`
- `TransactionTag`

Supporting enums were added for:

- account type
- category type
- transaction type

Application-layer additions included:

- DTOs for accounts, categories, transactions, and dashboard summaries
- validators for create/update/filter requests
- service interfaces for accounts, categories, transactions, and dashboard
- paged result support
- application exceptions for validation, conflict, and not-found flows

Infrastructure-layer additions included:

- EF Core entity configurations for accounts, categories, transactions, and transaction tags
- financial services for accounts, categories, transactions, and dashboard queries
- category seeding service for default user categories
- migration `20260314094520_AddFinancialFoundation`

API additions included:

- `AccountsController`
- `CategoriesController`
- `TransactionsController`
- `DashboardController`

### Financial Modeling Decisions

The following design choices were used:

- money uses `decimal` / PostgreSQL `numeric(18,2)`
- `Account` stores both `OpeningBalance` and `CurrentBalance`
- balances are only mutated server-side
- transaction create/update/delete operations reverse and reapply balance impact when needed
- transfers are modeled as a single transaction with `AccountId` and `TransferAccountId`
- accounts and categories use archival instead of unsafe hard-delete

Balance rules:

- income adds to the source account
- expense subtracts from the source account
- transfer subtracts from the source account and adds to the destination account
- deleting a transaction reverses its financial effect
- updating a transaction first reverses the original effect, then applies the new effect

### Category Strategy

Default categories are created per user.

Expense defaults:

- Food
- Rent
- Utilities
- Transport
- Entertainment
- Shopping
- Health
- Education
- Travel
- Subscriptions
- Miscellaneous

Income defaults:

- Salary
- Freelance
- Bonus
- Investment
- Gift
- Refund
- Other

Rules enforced:

- income transactions must use income categories
- expense transactions must use expense categories
- transfer transactions cannot use categories
- categories are archived instead of deleted when history could be affected

### Transaction Listing Support

Implemented transaction list features:

- filter by date range
- filter by category
- filter by type
- filter by account
- search by merchant or note
- pagination

### Dashboard Support

The dashboard now includes server-driven values for:

- current month income
- current month expense
- net balance across active accounts
- recent transactions
- spending by category

Important note:

- dashboard totals are calculated by the API, not by the frontend

### Frontend Additions

New pages added:

- accounts page
- transactions page
- dashboard page

New frontend support included:

- account create/edit/archive flows
- transaction create/edit/delete flows
- dashboard cards and recent activity list
- spending-by-category visualization
- shared empty states
- section headers
- select input component
- formatting helpers for currency and date

Routing was extended so the shell now points real pages to:

- `/dashboard`
- `/transactions`
- `/accounts`

The remaining shell sections are still placeholders by design.

### Security and Ownership Rules

All financial endpoints remain protected by authentication.

Ownership rules enforced:

- all account, category, and transaction data is scoped by authenticated `userId`
- client input is never trusted for ownership
- user-scoped lookups are required before update/delete/archive actions
- entities are not exposed directly from API responses

### Migrations and Database Updates

Added migration:

- `20260314094520_AddFinancialFoundation`

The financial schema now includes:

- `accounts`
- `categories`
- `transactions`
- `transaction_tags`

Additional schema characteristics:

- indexes for common user/date/type/category/account queries
- foreign keys for account/category/user ownership
- numeric precision for money values
- archive flags for safe record lifecycle handling

### Development Issues Resolved In Phase 2

Notable issues encountered and fixed:

1. The dashboard summary endpoint initially returned `500`.
2. Recent transaction projection was simplified to materialize first and map DTOs in memory.
3. Spending-by-category aggregation was simplified to avoid fragile grouped navigation translation.
4. Refresh cookies originally used `SameSite=Strict`, which caused logout on browser refresh when frontend and backend ran on different local origins.
5. Development refresh-cookie handling was updated so local session persistence works correctly.

### Current Expected Tables After Phase 2

The `public` schema should now contain at least:

- `__EFMigrationsHistory`
- `users`
- `refresh_tokens`
- `accounts`
- `categories`
- `transactions`
- `transaction_tags`

### Current Expected App Behavior

After login, the app should now support:

- creating accounts
- listing and updating accounts
- seeding and listing categories
- creating income, expense, and transfer transactions
- viewing paginated transaction history
- seeing dashboard totals and recent activity

### Recommended Next Step

After this phase, the next strong milestone would be:

- budgets
- recurring transaction management
- category management UI refinement
- richer reporting

## Phase 3: Budgets and Reports Basics

This phase added the first budgeting and reporting layer on top of the existing accounts, categories, transactions, and dashboard foundation.

### Scope Completed

Implemented:

- monthly budgets by expense category
- one-budget-per-user-category-month-year enforcement
- budget actual vs planned calculations
- threshold and over-budget states
- copy previous month budgets flow
- reporting overview with date-range filtering
- category spend reporting
- income vs expense trend reporting
- account balance trend reporting
- dashboard budget health summary

### Backend Additions

Added backend model and service support for:

- `Budget` entity
- budget DTOs, validators, queries, and service interface
- report DTOs, query validation, and reporting service interface
- `BudgetService`
- `ReportService`
- `BudgetsController`
- `ReportsController`

Budget/report persistence updates:

- `budgets` table
- unique constraint on `userId + categoryId + year + month`
- indexes for month-based budget lookup

Migration added:

- `20260314174757_AddBudgetingAndReporting`

### Budget Rules Implemented

- budgets are monthly
- budgets are user-scoped
- budgets can only be created for expense categories
- archived categories cannot receive new budgets
- historical budget records remain visible even if a category is later archived
- actual spend uses only non-deleted expense transactions
- income and transfer transactions do not consume budgets
- month/year matching is based on transaction `DateUtc`

Calculated budget values:

- actual spent
- remaining amount
- percentage used
- threshold reached state
- over-budget state

Deletion choice:

- budgets are safe to hard-delete because they are planning artifacts, not source-of-truth ledger entries

### Reporting Rules Implemented

Reports are derived only from persisted server-side financial data.

Implemented reporting outputs:

- category spend report
- income vs expense trend
- account balance trend
- summary totals for selected range

Reporting correctness rules:

- category spend represents expense activity only
- income vs expense trend excludes transfers
- account balance trend includes the financial effect of transfers
- archived accounts/categories remain safe for historical reporting
- frontend does not perform authoritative report calculations

### Frontend Additions

Added:

- budgets page
- reports page
- dashboard budget summary cards and indicators
- reusable progress bar
- reusable chart card wrapper
- reusable filter row
- shared select-field ref forwarding fix for form reliability

Budget page features:

- month/year selection
- create budget form
- edit budget form
- delete flow
- copy previous month action
- progress/status display
- budget summary cards

Reports page features:

- date range filters
- optional account filter
- server-driven summary cards
- category spend visualization
- income vs expense trend visualization
- account balance trend visualization

### Integration Fixes Finalized In Phase 3

Additional stability fixes were completed after the main milestone implementation:

1. API enum serialization was configured to use string values instead of numeric enum values.
2. This ensured frontend filters correctly recognize values such as `Income`, `Expense`, account types, and related enum-backed responses.
3. Transaction-category dropdown behavior was corrected by aligning backend enum JSON output with frontend expectations.
4. Shared `SelectField` handling was improved by forwarding the native select ref so React Hook Form tracks selection state correctly.
5. This fix also improved select behavior across accounts, transactions, budgets, and reports forms.

### Validation and Performance Notes

Validation added for:

- budget create/update
- budget month query
- copy previous month request
- report date range query

Performance and safety choices:

- report range is capped to 366 days
- reports are aggregated server-side
- budget uniqueness is enforced both in application logic and database schema
- dashboard and reporting queries were simplified where needed to avoid fragile ORM translation paths

### Recommended Next Step

After this phase, the next strong milestone would be:

- recurring transactions
- goals
- richer reports and export capabilities
- notification or budget alert workflows
