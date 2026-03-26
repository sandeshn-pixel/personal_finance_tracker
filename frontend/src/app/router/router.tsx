import { createBrowserRouter, Navigate } from "react-router-dom";
import { AccountDetailsPage } from "../../features/accounts/pages/AccountDetailsPage";
import { AccountInviteAcceptPage } from "../../features/accounts/pages/AccountInviteAcceptPage";
import { AccountsPage } from "../../features/accounts/pages/AccountsPage";
import { AuthGuard } from "../../features/auth/components/AuthGuard";
import { ForgotPasswordPage } from "../../features/auth/pages/ForgotPasswordPage";
import { LoginPage } from "../../features/auth/pages/LoginPage";
import { ResetPasswordPage } from "../../features/auth/pages/ResetPasswordPage";
import { SignupPage } from "../../features/auth/pages/SignupPage";
import { BudgetsPage } from "../../features/budgets/pages/BudgetsPage";
import { CategoriesPage } from "../../features/categories/pages/CategoriesPage";
import { DashboardPage } from "../../features/dashboard/pages/DashboardPage";
import { GoalsPage } from "../../features/goals/pages/GoalsPage";
import { HealthScorePage } from "../../features/insights/pages/HealthScorePage";
import { InsightsPage } from "../../features/insights/pages/InsightsPage";
import { NotificationsPage } from "../../features/notifications/pages/NotificationsPage";
import { ReportsPage } from "../../features/reports/pages/ReportsPage";
import { RecurringTransactionsPage } from "../../features/recurring/pages/RecurringTransactionsPage";
import { RulesPage } from "../../features/rules/pages/RulesPage";
import { SettingsPage } from "../../features/settings/pages/SettingsPage";
import { TransactionsPage } from "../../features/transactions/pages/TransactionsPage";
import { AppShell } from "../../layout/AppShell";

export const router = createBrowserRouter([
  { path: "/", element: <Navigate to="/dashboard" replace /> },
  { path: "/login", element: <LoginPage /> },
  { path: "/signup", element: <SignupPage /> },
  { path: "/forgot-password", element: <ForgotPasswordPage /> },
  { path: "/reset-password", element: <ResetPasswordPage /> },
  { path: "/account-invites/accept", element: <AccountInviteAcceptPage /> },
  {
    element: <AuthGuard />,
    children: [
      {
        element: <AppShell />,
        children: [
          { path: "/dashboard", element: <DashboardPage /> },
          { path: "/transactions", element: <TransactionsPage /> },
          { path: "/accounts", element: <AccountsPage /> },
          { path: "/accounts/:accountId", element: <AccountDetailsPage /> },
          { path: "/categories", element: <CategoriesPage /> },
          { path: "/budgets", element: <BudgetsPage /> },
          { path: "/goals", element: <GoalsPage /> },
          { path: "/insights", element: <InsightsPage /> },
          { path: "/insights/health-score", element: <HealthScorePage /> },
          { path: "/rules", element: <RulesPage /> },
          { path: "/reports", element: <ReportsPage /> },
          { path: "/recurring", element: <RecurringTransactionsPage /> },
          { path: "/notifications", element: <NotificationsPage /> },
          { path: "/settings", element: <SettingsPage /> },
        ],
      },
    ],
  },
]);
