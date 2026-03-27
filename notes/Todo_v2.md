Got it — you don’t need explanations, just a **clear V2 execution TODO** in the same structured, production-style way.

Here’s your **clean, actionable V2 TODO roadmap (for .NET 8 backend + React frontend)** 👇

---

notification
top bar better for small screen
Icons

# PRODUCTION DEPLOYMENT / MIGRATION RULES

## Database migrations

* [ ] Keep `Database.Migrate()` on startup only as a temporary early-stage convenience
* [ ] Plan a future CI/CD migration step that runs before App Service startup
* [ ] Run production migrations only from an Azure-connected environment that can reach the private PostgreSQL server
* [ ] Treat migration failure as deployment failure
* [ ] Avoid renaming already-applied migrations unless migration history is reconciled carefully
* [ ] Prefer backward-compatible schema changes for safer deploys

---

## Auth / session production rules

* [ ] Keep refresh-token cookie as `Secure + HttpOnly + SameSite=None`
* [ ] Preserve credentialed CORS between frontend and backend origins
* [ ] Do not use `SameSite=Strict` for refresh-token cookie in production cross-origin flow
* [ ] Re-test login, refresh, logout, and page reload behavior after auth changes

---

## Azure deployment guardrails

* [ ] Verify required backend env vars before deploy: connection string, JWT settings, frontend allowed origins, email settings if enabled
* [ ] Keep production DB name aligned with Azure config: `ledgernest-database`
* [ ] Verify `/health/live` and `/health/ready` after deploy
* [ ] Prefer single controlled migration path instead of relying on ad hoc manual DB changes

---

# ✅ V2 IMPLEMENTATION TODO (PHASE-WISE)

---

# 🔵 PHASE 4 — CASH FLOW FORECASTING

## Backend (.NET 8)

### 🧱 Setup

* [ ] Create `ForecastService`
* [ ] Create `IForecastService` interface
* [ ] Add `ForecastController`

---

### 📊 Data Aggregation

* [ ] Query last 3–6 months transactions
* [ ] Calculate:

  * [ ] avg daily expense
  * [ ] avg daily income
* [ ] Fetch:

  * [ ] current account balances
  * [ ] upcoming recurring transactions

---

### 🧠 Logic

* [ ] Compute:

  * [ ] projected end-of-month balance
  * [ ] daily balance projection
  * [ ] safe-to-spend amount
* [ ] Add risk detection:

  * [ ] LOW / MEDIUM / HIGH
  * [ ] negative balance warning

---

### 🔌 APIs

* [ ] `GET /api/forecast/month`
* [ ] `GET /api/forecast/daily`

---

### 📦 DTOs

* [ ] `ForecastSummaryDto`
* [ ] `DailyForecastDto`

---

### ⚠️ Edge Handling

* [ ] Handle sparse data users
* [ ] Ignore future dates beyond month
* [ ] Ensure timezone consistency (UTC)

---

## Frontend

* [ ] Add “Projected Balance” card (Dashboard)
* [ ] Add line chart (today → end of month)
* [ ] Add “Safe to Spend” indicator
* [ ] Show risk warning (if needed)

---

# 🟢 PHASE 5 — FINANCIAL HEALTH SCORE

## Backend

### 🧱 Setup

* [ ] Create `InsightsService`
* [ ] Add `HealthScoreCalculator`

---

### 📊 Metrics

* [ ] Savings rate
* [ ] Expense stability
* [ ] Budget adherence
* [ ] Cash buffer

---

### 🧠 Scoring

* [ ] Define weights for each factor
* [ ] Normalize to score (0–100)

---

### 🔌 API

* [ ] `GET /api/insights/health-score`

---

### 📦 DTO

* [ ] `HealthScoreDto`

  * score
  * breakdown
  * suggestions

---

## Frontend

* [ ] Add score card to dashboard
* [ ] Add drill-down page
* [ ] Show factor breakdown
* [ ] Add suggestions UI

---

# 🟡 PHASE 6 — RULES ENGINE

## Backend

### 🧱 Setup

* [ ] Create `RulesEngineService`
* [ ] Create `RuleEvaluator`

---

### 🗄️ Database

* [ ] Create `rules` table

  * id
  * user_id
  * condition_json
  * action_json
  * is_active

---

### ⚙️ Execution

* [ ] Trigger rules:

  * [ ] on transaction create
  * [ ] on import (future-proof)
* [ ] Implement:

  * [ ] condition parser
  * [ ] action executor
* [ ] Support:

  * [ ] equals / contains / greater_than

---

### 🔌 APIs

* [ ] GET `/api/rules`
* [ ] POST `/api/rules`
* [ ] PUT `/api/rules/{id}`
* [ ] DELETE `/api/rules/{id}`

---

## Frontend

* [ ] Rules list page
* [ ] Rule builder UI (form-based)
* [ ] Enable/disable toggle
* [ ] Validation for rule inputs

---

# 🔴 PHASE 7 — SHARED ACCOUNTS

## Backend

### 🗄️ Database

* [ ] Create `account_members` table

  * account_id
  * user_id
  * role (Owner / Editor / Viewer)

---

### 🧱 Setup

* [ ] Create `AccountMembershipService`
* [ ] Add access control middleware

---

### 🔐 Permissions

* [ ] Owner → full access
* [ ] Editor → CRUD transactions
* [ ] Viewer → read-only

---

### 🔌 APIs

* [ ] POST `/api/accounts/{id}/invite`
* [ ] GET `/api/accounts/{id}/members`
* [ ] PUT `/api/accounts/{id}/members/{userId}`

---

### ⚠️ Critical

* [ ] Modify ALL queries to support shared access
* [ ] Replace `userId` filter with:

  * owned OR shared

---

## Frontend

* [ ] “Shared with” section in account page
* [ ] Invite user modal
* [ ] Role selector UI
* [ ] Member list UI

---

# 🟣 PHASE 8 — ADVANCED REPORTING & INSIGHTS

## Backend

### 🧱 Setup

* [ ] Extend `ReportsService`
* [ ] Extend `InsightsService`

---

### 📊 Reports

* [ ] Category trends (time series)
* [ ] Savings rate trend
* [ ] Net worth tracking
* [ ] Income vs expense multi-month

---

### 🧠 Insights

* [ ] Detect:

  * [ ] spending increase %
  * [ ] savings improvement
  * [ ] unusual spikes

---

### 🔌 APIs

* [ ] GET `/api/reports/trends`
* [ ] GET `/api/reports/net-worth`
* [ ] GET `/api/insights`

---

## Frontend

* [ ] Insights page
* [ ] Highlight cards (key findings)
* [ ] Comparison charts
* [ ] Trend visualizations

---

# ⚙️ CROSS-CUTTING TODO (IMPORTANT)

## Backend

* [ ] Add caching (for heavy reports/forecast)
* [ ] Optimize aggregation queries
* [ ] Add indexes if needed
* [ ] Add logging for V2 services
* [ ] Add unit tests for:

  * forecasting
  * scoring
  * rules

---

## Frontend

* [ ] Add loading skeletons for new widgets
* [ ] Handle empty states
* [ ] Improve chart performance
* [ ] Maintain consistent UI patterns

---

# 🧭 EXECUTION ORDER (FINAL)

Do this strictly:

1. ✅ Forecasting
2. ✅ Health Score
3. ✅ Rules Engine
4. ✅ Shared Accounts
5. ✅ Advanced Insights

---



# CURRENT REFINEMENTS - FORECAST + HEALTH SCORE

## Forecast clarity

* [ ] Rename labels that still feel too technical for normal users
* [ ] Add helper text explaining `Projected month-end balance`
* [ ] Add helper text explaining `Safe to spend this month`
* [ ] Rename `Lowest point this month` to `Lowest expected balance` if users still find it unclear
* [ ] Make recurring-impact wording simpler and more human-readable

---

## Health score clarity

* [ ] Add clearer explanation that score uses the last 3 completed months
* [ ] Show factor weights more clearly in the detail page
* [ ] Improve factor explanation copy so users understand what changed the score
* [ ] Add a short `why this score` summary on the dashboard card
* [ ] Keep suggestions concise, practical, and non-judgmental

---

## Sparse / empty state quality

* [ ] Improve sparse-data messaging for forecast when history is limited
* [ ] Improve sparse-data messaging for health score when budgets are missing
* [ ] Distinguish between `no data yet` and `limited data` states
* [ ] Ensure empty states tell the user exactly what action unlocks each widget

---

## Testing / correctness follow-up

* [ ] Add health-score tests for archived-account exclusion
* [ ] Add health-score tests for zero-income months
* [ ] Add health-score tests for partial budget coverage
* [ ] Add forecast tests for irregular expense months
* [ ] Add UI checks for loading, empty, sparse, and error states

---

## Product polish follow-up

* [ ] Consider showing score change vs previous period only if explanation remains simple and deterministic
* [ ] Consider adding `what improved` and `what weakened` summaries for health score
* [ ] Keep all score and forecast calculations server-side
* [ ] Re-check dashboard readability after adding multiple V2 insight cards
