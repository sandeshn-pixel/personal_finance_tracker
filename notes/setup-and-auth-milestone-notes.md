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

## Phase 4: Goals and Recurring Transactions

This phase added audited savings-goal tracking and recurring transaction scheduling on top of the existing accounts, budgets, and reporting foundation.

### Scope Completed

Implemented:

- savings goal creation and editing
- goal contribution and withdrawal flows
- optional linked-account support for goals
- goal completion and archive flows
- dedicated goal audit trail entries
- recurring transaction rule creation and editing
- pause, resume, and safe delete behavior for recurring rules
- due-occurrence processing endpoint for local execution
- duplicate-prevention execution tracking
- frontend goals page
- frontend recurring transactions page

### Backend Additions

New domain models added:

- `Goal`
- `GoalEntry`
- `RecurringTransactionRule`
- `RecurringTransactionExecution`

New enums added:

- `GoalStatus`
- `GoalEntryType`
- `RecurringFrequency`
- `RecurringRuleStatus`
- `RecurringExecutionStatus`

New backend services and controllers:

- `GoalService`
- `RecurringTransactionService`
- `GoalsController`
- `RecurringTransactionsController`

New EF configurations:

- `GoalConfiguration`
- `GoalEntryConfiguration`
- `RecurringTransactionRuleConfiguration`
- `RecurringTransactionExecutionConfiguration`

Migration added and applied:

- `20260315060307_AddGoalsAndRecurringTransactions`

### Goal Modeling Decisions

Goals use a dedicated audited ledger instead of normal transactions.

Reasoning:

- goal contributions and withdrawals must be traceable
- goal movements should not distort income, expense, budget, or report calculations
- linked-account balance changes still need to happen safely when a goal is connected to an account

Implemented behavior:

- each contribution or withdrawal creates a `goal_entries` record
- each entry stores amount, type, occurred date, optional note, linked account reference, and resulting goal balance
- if the goal is linked to an account, the linked account balance is adjusted in the same database transaction
- goal current amount can never go negative
- completed goals automatically return to active if a later withdrawal drops them below target
- archived goals block new contributions and withdrawals
- manual completion requires the target amount to be reached first

### Recurring Scheduling Decisions

Recurring rules generate real transactions, but scheduling is separated from the rule definition.

Idempotency strategy:

- each scheduled occurrence is tracked in `recurring_transaction_executions`
- there is a unique constraint on `rule + scheduled date`
- this prevents duplicate generation of the same occurrence
- recovery logic checks for a previously generated matching transaction before retrying an occurrence

Local execution strategy:

- a safe authenticated endpoint processes due recurring rules on demand
- this gives a production-sensible abstraction now without pretending a background scheduler already exists
- the same design can later be driven by Hangfire, Quartz, or another job host

Generation behavior:

- generated transactions use the same transaction service and therefore the same balance rules as manual transactions
- generated transactions are tagged with `RecurringTransactionId`
- paused rules do not generate
- completed or deleted rules do not generate
- transfer rules still obey source/destination account validation
- category validation still applies for recurring income and expense rules

### Frontend Additions

Added real pages for:

- `/goals`
- `/recurring`

Goals UI includes:

- goal list with active and completed sections
- create/edit goal form
- contribution flow
- withdrawal flow
- progress display
- recent goal entry history

Recurring UI includes:

- recurring rule list
- create/edit rule form
- pause/resume actions
- delete flow
- next due date display
- process-due-now action for local execution

### Security and Financial Integrity

Rules enforced in this phase:

- all goal and recurring endpoints are authenticated
- all records are scoped by authenticated `userId`
- linked accounts must belong to the authenticated user and be active
- recurring rule references are validated against user-owned accounts and categories
- generated recurring transactions obey the same source/destination/category invariants as manual transactions
- money remains `decimal` / `numeric(18,2)`
- UTC timestamps are used consistently
- duplicate occurrence generation is guarded by both persisted execution records and recovery checks

### Current Expected Tables After Phase 4

The `public` schema should now include at least:

- `goals`
- `goal_entries`
- `recurring_transaction_rules`
- `recurring_transaction_executions`

### Validation Status

Verified for this phase:

- `dotnet build src/FinanceTracker.Application/FinanceTracker.Application.csproj`
- `dotnet build src/FinanceTracker.Infrastructure/FinanceTracker.Infrastructure.csproj`
- `dotnet build src/FinanceTracker.Api/FinanceTracker.Api.csproj -c Release`
- `npm run build`
- `dotnet ef database update --project src/FinanceTracker.Infrastructure --startup-project src/FinanceTracker.Api`

### Known Limitations In Phase 4

- recurring execution is currently manual/on-demand rather than driven by a background worker
- recurring-generated transactions currently use the rule title as the transaction note
- goals use an internal audited ledger rather than separate transfer transactions, by design

### Recommended Next Step

After this phase, the next strong milestone would be:

- notifications and reminder workflows
- export capabilities
- richer analytics and forecasting
- scheduler infrastructure hardening for unattended recurring execution
## Phase 5 - Export, Responsive Polish, Testing, and Deployment Hardening

### Scope Delivered

This phase focused on release readiness rather than adding another financial module. The work covered:

- CSV exports for transactions, reports overview, and monthly budget summaries
- responsive and accessibility polish on the frontend shell and filter/export flows
- backend and frontend test coverage for critical business behavior
- health checks, startup configuration validation, safer request/error handling, and deployment artifacts

### Export Design

Implemented backend export endpoints:

- `GET /api/exports/transactions.csv`
- `GET /api/exports/reports/overview.csv`
- `GET /api/exports/budgets/month.csv`

Export rules:

- all export endpoints require authentication
- all export data is scoped by authenticated `userId`
- transaction export reuses the same server-side transaction filters used by the transactions page
- report export reuses the same server-side report aggregation used by the reports page
- budget export uses trusted monthly budget summary/detail calculations already in the backend
- CSV is the only export format added in this phase; PDF was intentionally skipped to avoid a low-value rendering dependency and keep the phase production-sensible

File generation decisions:

- responses use `text/csv; charset=utf-8`
- filenames are deterministic and time-stamped where appropriate
- transfer handling remains consistent with the existing reporting rules, so exports do not distort income/expense reporting

### Backend Hardening

Operational changes added:

- request logging middleware for method/path/status/duration logging
- health endpoints:
  - `/health/live`
  - `/health/ready`
- startup validation for JWT configuration
- startup validation that frontend allowed origins exist outside development
- CORS fallback only for local development origins when explicit origins are not configured
- global exception middleware now returns cleaner problem titles for known application errors

Production-safety decisions:

- migrations are still explicit/manual rather than auto-applied on startup
- category seeding remains user-triggered through auth/category flows and is not run globally at app startup
- no production data seeding was added automatically

### Frontend Additions

Transactions page:

- added `Export CSV` action
- export uses the active transaction filters
- export button is disabled while running
- success feedback is shown after export completes

Reports page:

- added `Export CSV` action
- export uses the current date/account report filters
- success feedback is shown after export completes

Responsive polish:

- mobile/tablet shell now uses a drawer-style sidebar toggle instead of always-expanded navigation
- drawer closes on route change and backdrop click
- filter/action layouts are more resilient on narrower screens
- topbar and shell spacing were tightened for smaller screens

Accessibility cleanup:

- stronger focus-visible behavior for interactive elements
- explicit aria labels on export, logout, and nav toggle controls
- status feedback uses `aria-live`

### Testing Added

#### Backend tests

Created `backend/tests/FinanceTracker.Backend.Tests` with focused service-level coverage for:

- auth register/login/refresh flow
- transaction balance reversal correctness
- transfer balance safety
- budget calculation correctness
- report aggregation correctness
- goal contribution/withdrawal correctness
- recurring idempotent due processing
- transaction export filter correctness

Important note:

- backend tests use SQLite in-memory for fast correctness coverage
- one provider-compatibility improvement was made for testability and portability:
  - transaction text search was made provider-safe
  - budget actual aggregation now has a SQLite-safe path while keeping PostgreSQL server aggregation in production

#### Frontend tests

Added Vitest + Testing Library coverage for:

- route protection (`AuthGuard`)
- auth persistence/logout (`AuthProvider`)
- login validation
- transactions filtering/export trigger
- reports export trigger and smoke render
- dashboard smoke render

### Deployment Artifacts

Added:

- `backend/Dockerfile`
- `backend/.dockerignore`
- `frontend/Dockerfile`
- `frontend/.dockerignore`
- `frontend/nginx.conf`
- root `docker-compose.yml`

Docker decisions:

- backend runs on ASP.NET Core runtime image
- frontend builds with Node and serves static assets through Nginx
- Nginx proxies `/api` and `/health` to the backend container
- docker-compose includes PostgreSQL, backend, and frontend for local full-stack execution
- compose uses explicit environment variables and keeps migration execution separate/documented

### Validation Status

Verified for this phase:

- `dotnet build src/FinanceTracker.Api/FinanceTracker.Api.csproj -c Release`
- `dotnet test backend/tests/FinanceTracker.Backend.Tests/FinanceTracker.Backend.Tests.csproj --no-restore`
- `npm test`
- `npm run build`

### Known Limitations In Phase 5

- exports are CSV-only in this phase; PDF is intentionally deferred
- transaction export currently uses the existing transaction filters, which do not yet expose every possible report-style aggregation filter in the UI
- request logging is structured and useful, but not yet integrated with an external log sink or correlation system
- docker-compose does not auto-run database migrations; migration execution remains an explicit operational step

### Recommended Next Step

The strongest next cleanup or phase after this would be one of:

- notification and reminder infrastructure for budgets/goals/recurring rules
- scheduler hosting for unattended recurring execution
- richer report/export formats and analytics
- CI pipeline wiring for automated build/test/migration checks
