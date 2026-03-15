import { createBrowserRouter, Navigate } from "react-router-dom";
import { AccountsPage } from "../../features/accounts/pages/AccountsPage";
import { LoginPage } from "../../features/auth/pages/LoginPage";
import { SignupPage } from "../../features/auth/pages/SignupPage";
import { AuthGuard } from "../../features/auth/components/AuthGuard";
import { BudgetsPage } from "../../features/budgets/pages/BudgetsPage";
import { DashboardPage } from "../../features/dashboard/pages/DashboardPage";
import { GoalsPage } from "../../features/goals/pages/GoalsPage";
import { ReportsPage } from "../../features/reports/pages/ReportsPage";
import { RecurringTransactionsPage } from "../../features/recurring/pages/RecurringTransactionsPage";
import { TransactionsPage } from "../../features/transactions/pages/TransactionsPage";
import { AppShell } from "../../layout/AppShell";
import { PlaceholderPage } from "../../pages/PlaceholderPage";

export const router = createBrowserRouter([
  { path: "/", element: <Navigate to="/dashboard" replace /> },
  { path: "/login", element: <LoginPage /> },
  { path: "/signup", element: <SignupPage /> },
  {
    element: <AuthGuard />,
    children: [
      {
        element: <AppShell />,
        children: [
          { path: "/dashboard", element: <DashboardPage /> },
          { path: "/transactions", element: <TransactionsPage /> },
          { path: "/accounts", element: <AccountsPage /> },
          { path: "/budgets", element: <BudgetsPage /> },
          { path: "/goals", element: <GoalsPage /> },
          { path: "/reports", element: <ReportsPage /> },
          { path: "/recurring", element: <RecurringTransactionsPage /> },
          { path: "/settings", element: <PlaceholderPage title="Settings" description="Settings remains a placeholder while core finance modules are built out." /> },
        ],
      },
    ],
  },
]);