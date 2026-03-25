import { useEffect, useMemo, useState } from "react";
import { zodResolver } from "@hookform/resolvers/zod";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { useAuth } from "../../../app/providers/AuthProvider";
import { useWorkspaceScope } from "../../../app/providers/WorkspaceScopeProvider";
import { categoriesApi, type CategoryDto } from "../../categories/api/categoriesApi";
import { budgetsApi, type BudgetDto } from "../api/budgetsApi";
import { Alert } from "../../../shared/components/Alert";
import { Button } from "../../../shared/components/Button";
import { EmptyState } from "../../../shared/components/EmptyState";
import { Field } from "../../../shared/components/Field";
import { FilterRow } from "../../../shared/components/FilterRow";
import { PageLoader } from "../../../shared/components/PageLoader";
import { ProgressBar } from "../../../shared/components/ProgressBar";
import { SectionHeader } from "../../../shared/components/SectionHeader";
import { SelectField } from "../../../shared/components/SelectField";
import { StatCard } from "../../../shared/components/StatCard";
import { ApiError } from "../../../shared/lib/api/client";
import { formatCurrency } from "../../../shared/lib/format";

const budgetSchema = z.object({
  categoryId: z.string().min(1, "Category is required."),
  amount: z.coerce.number().positive("Budget amount must be greater than zero."),
  alertThresholdPercent: z.coerce.number().min(1, "Threshold must be at least 1.").max(100, "Threshold cannot exceed 100."),
});

type BudgetFormValues = z.infer<typeof budgetSchema>;

const monthNames = ["January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December"];

function summarizeBudgets(budgets: BudgetDto[]) {
  return {
    totalBudgeted: budgets.reduce((sum, budget) => sum + budget.amount, 0),
    totalSpent: budgets.reduce((sum, budget) => sum + budget.actualSpent, 0),
    totalRemaining: budgets.reduce((sum, budget) => sum + budget.remaining, 0),
    overBudgetCount: budgets.filter((budget) => budget.isOverBudget).length,
    thresholdReachedCount: budgets.filter((budget) => budget.isThresholdReached).length,
  };
}

export function BudgetsPage() {
  const { accessToken } = useAuth();
  const { sharedAccessView } = useWorkspaceScope();
  const today = new Date();
  const [selectedYear, setSelectedYear] = useState(today.getFullYear());
  const [selectedMonth, setSelectedMonth] = useState(today.getMonth() + 1);
  const [categories, setCategories] = useState<CategoryDto[]>([]);
  const [budgets, setBudgets] = useState<BudgetDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [editing, setEditing] = useState<BudgetDto | null>(null);
  const [errorVariant, setErrorVariant] = useState<"error" | "info">("error");

  const { register, handleSubmit, reset, formState: { errors, isSubmitting } } = useForm<BudgetFormValues>({
    resolver: zodResolver(budgetSchema),
    defaultValues: { categoryId: "", amount: 0, alertThresholdPercent: 80 },
  });

  useEffect(() => { void bootstrap(); }, [accessToken]);
  useEffect(() => { void loadBudgets(); }, [accessToken, selectedYear, selectedMonth]);

  async function bootstrap() {
    if (!accessToken) return;
    setLoading(true);
    try {
      const categoryResponse = await categoriesApi.list(accessToken);
      const expenseCategories = categoryResponse.filter((item) => item.type === "Expense" && !item.isArchived);
      setCategories(expenseCategories);
      reset({ categoryId: expenseCategories[0]?.id ?? "", amount: 0, alertThresholdPercent: 80 });
      setErrorVariant("error");
      setErrorMessage(null);
    } catch (error) {
      setErrorVariant("error");
      setErrorMessage(error instanceof ApiError ? error.message : "Unable to load budget categories.");
    } finally {
      setLoading(false);
    }
  }

  async function loadBudgets() {
    if (!accessToken) return;
    setLoading(true);
    try {
      const budgetResponse = await budgetsApi.list(accessToken, selectedYear, selectedMonth);
      setBudgets(budgetResponse);
      setErrorVariant("error");
      setErrorMessage(null);
    } catch (error) {
      if (error instanceof ApiError && error.status === 429) {
        setErrorVariant("info");
        setErrorMessage("Budgets are refreshing too quickly. Please wait a moment and try again.");
      } else {
        setErrorVariant("error");
        setErrorMessage(error instanceof ApiError ? error.message : "Unable to load budgets.");
      }
    } finally {
      setLoading(false);
    }
  }

  function resetForm() {
    setEditing(null);
    reset({ categoryId: categories[0]?.id ?? "", amount: 0, alertThresholdPercent: 80 });
  }

  function editBudget(budget: BudgetDto) {
    if (!budget.canManage) {
      setErrorVariant("info");
      setErrorMessage(`This budget belongs to ${budget.ownerDisplayName}. Switch to All or Shared with me to review it, but only the owner can edit it.`);
      return;
    }

    setEditing(budget);
    reset({ categoryId: budget.categoryId, amount: budget.amount, alertThresholdPercent: budget.alertThresholdPercent });
  }

  async function onSubmit(values: BudgetFormValues) {
    if (!accessToken) return;

    try {
      if (editing) {
        await budgetsApi.update(accessToken, editing.id, {
          amount: values.amount,
          alertThresholdPercent: values.alertThresholdPercent,
        });
      } else {
        await budgetsApi.create(accessToken, {
          categoryId: values.categoryId,
          year: selectedYear,
          month: selectedMonth,
          amount: values.amount,
          alertThresholdPercent: values.alertThresholdPercent,
        });
      }

      resetForm();
      await loadBudgets();
    } catch (error) {
      setErrorVariant("error");
      setErrorMessage(error instanceof ApiError ? error.message : "Unable to save budget.");
    }
  }

  async function deleteBudget(budgetId: string) {
    if (!accessToken || !window.confirm("Delete this budget? Actual transaction history will remain unchanged.")) return;

    try {
      await budgetsApi.remove(accessToken, budgetId);
      if (editing?.id === budgetId) {
        resetForm();
      }
      await loadBudgets();
    } catch (error) {
      setErrorVariant("error");
      setErrorMessage(error instanceof ApiError ? error.message : "Unable to delete budget.");
    }
  }

  async function copyPreviousMonth() {
    if (!accessToken) return;

    try {
      await budgetsApi.copyPreviousMonth(accessToken, { year: selectedYear, month: selectedMonth, overwriteExisting: false });
      await loadBudgets();
    } catch (error) {
      setErrorVariant("error");
      setErrorMessage(error instanceof ApiError ? error.message : "Unable to copy budgets from the previous month.");
    }
  }

  const yearOptions = [today.getFullYear() - 1, today.getFullYear(), today.getFullYear() + 1];
  const visibleBudgets = useMemo(() => {
    if (sharedAccessView === "mine") {
      return budgets.filter((budget) => budget.canManage);
    }

    if (sharedAccessView === "shared") {
      return budgets.filter((budget) => !budget.canManage);
    }

    return budgets;
  }, [budgets, sharedAccessView]);
  const sharedReadOnlyBudgets = useMemo(() => visibleBudgets.filter((budget) => !budget.canManage), [visibleBudgets]);
  const budgetSummary = useMemo(() => summarizeBudgets(visibleBudgets), [visibleBudgets]);
  const hasSharedReadOnlyBudgets = useMemo(() => budgets.some((budget) => !budget.canManage), [budgets]);
  const canManageBudgets = useMemo(() => sharedAccessView !== "shared" && (categories.length > 0 || visibleBudgets.some((budget) => budget.canManage)), [categories.length, sharedAccessView, visibleBudgets]);
  const scopeMessage = useMemo(() => {
    if (sharedAccessView === "mine" && hasSharedReadOnlyBudgets) {
      return "Budget view is focused on budgets you manage. Shared read-only budgets are hidden in this scope.";
    }

    if (sharedAccessView === "shared" && hasSharedReadOnlyBudgets) {
      return "Budget view is focused on read-only shared budget summaries only. Owner-managed budgets are hidden in this scope.";
    }

    if (sharedReadOnlyBudgets.length > 0) {
      return `You can review ${sharedReadOnlyBudgets.length} shared budget ${sharedReadOnlyBudgets.length === 1 ? "summary" : "summaries"} here when the account owner has activity in that category this month, but only the owner can change them.`;
    }

    return null;
  }, [hasSharedReadOnlyBudgets, sharedAccessView, sharedReadOnlyBudgets.length]);

  if (loading && budgets.length === 0 && categories.length === 0) {
    return <PageLoader label="Loading budgets" />;
  }

  return (
    <div className="page-stack">
      <SectionHeader
        title="Budgets"
        description="Plan monthly category limits and compare them against actual expense activity. Shared-account budget visibility is read-only in this phase."
        action={canManageBudgets ? <Button type="button" onClick={resetForm}>New budget</Button> : undefined}
      />
      {errorMessage ? <Alert message={errorMessage} variant={errorVariant} /> : null}
      {scopeMessage ? (
        <div className="page-context-row">
          <div className="rules-preview"><strong>Budget scope</strong><p>{scopeMessage}</p></div>
        </div>
      ) : null}
      <FilterRow action={canManageBudgets ? <Button type="button" onClick={copyPreviousMonth}>Copy previous month</Button> : undefined}>
        <Field label="Month">
          <SelectField value={String(selectedMonth)} onChange={(event) => setSelectedMonth(Number(event.target.value))}>
            {monthNames.map((name, index) => <option key={name} value={index + 1}>{name}</option>)}
          </SelectField>
        </Field>
        <Field label="Year">
          <SelectField value={String(selectedYear)} onChange={(event) => setSelectedYear(Number(event.target.value))}>
            {yearOptions.map((year) => <option key={year} value={year}>{year}</option>)}
          </SelectField>
        </Field>
      </FilterRow>
      <div className="stats-grid stats-grid--four">
        <StatCard label="Budgeted" value={formatCurrency(budgetSummary.totalBudgeted)} hint="Total planned expense in this view." />
        <StatCard label="Spent" value={formatCurrency(budgetSummary.totalSpent)} hint="Visible expense transactions matched to budgets in this view." tone="negative" />
        <StatCard label="Remaining" value={formatCurrency(budgetSummary.totalRemaining)} hint="Budgeted minus visible actual spent in this view." tone={budgetSummary.totalRemaining < 0 ? "negative" : "positive"} />
        <StatCard label="Risk flags" value={`${budgetSummary.overBudgetCount} over / ${budgetSummary.thresholdReachedCount} warning`} hint="Visible categories beyond threshold or budget limit in this view." />
      </div>
      <div className="budget-layout">
        <section className="panel-card panel-card--form">
          <div className="panel-card__header">
            <h3>{editing ? "Edit budget" : `Create budget for ${monthNames[selectedMonth - 1]} ${selectedYear}`}</h3>
            <p>{editing ? "Adjust planned amount or alert threshold." : "Each expense category can only have one budget per month."}</p>
            {!canManageBudgets ? <small>Budget creation and edits are hidden in this scope.</small> : null}
          </div>
          {canManageBudgets ? (
            <form className="form-stack" onSubmit={handleSubmit(onSubmit)} noValidate>
              <Field label="Category" error={errors.categoryId?.message}>
                <SelectField {...register("categoryId")} disabled={Boolean(editing)}>
                  <option value="">Select category</option>
                  {categories.map((category) => <option key={category.id} value={category.id}>{category.name}</option>)}
                </SelectField>
              </Field>
              <div className="field-grid">
                <Field label="Budget amount" error={errors.amount?.message}><input {...register("amount")} type="number" step="0.01" /></Field>
                <Field label="Alert threshold %" error={errors.alertThresholdPercent?.message}><input {...register("alertThresholdPercent")} type="number" min={1} max={100} /></Field>
              </div>
              <Button type="submit" loading={isSubmitting}>{editing ? "Save budget" : "Create budget"}</Button>
            </form>
          ) : (
            <div className="rules-preview">
              <strong>Budget editing is hidden in this scope.</strong>
              <p>{sharedAccessView === "shared" ? "Switch to Mine or All to manage your own budgets." : "No owner-managed budgets are visible in the current scope."}</p>
            </div>
          )}
        </section>

        <section className="panel-card">
          <div className="panel-card__header">
            <h3>{monthNames[selectedMonth - 1]} budgets</h3>
            <p>{visibleBudgets.length} visible category budget{visibleBudgets.length === 1 ? "" : "s"} for this month.</p>
          </div>
          {visibleBudgets.length === 0 ? (
            <EmptyState title="No budgets in this view" description="Switch workspace scope or create a new budget to see monthly budget performance." />
          ) : (
            <div className="budget-list">
              {visibleBudgets.map((budget) => {
                const tone = budget.isOverBudget ? "danger" : budget.isThresholdReached ? "warning" : "default";
                return (
                  <article key={budget.id} className="budget-card">
                    <div className="budget-card__header">
                      <div>
                        <strong>{budget.categoryName}</strong>
                        <p>{formatCurrency(budget.actualSpent)} of {formatCurrency(budget.amount)} used</p>
                      </div>
                      <div className="budget-card__aside">
                        <span className={`status-badge status-badge--${tone}`}>{budget.isOverBudget ? "Over budget" : budget.isThresholdReached ? "Threshold reached" : "Healthy"}</span>
                        {!budget.canManage ? <small>Shared from {budget.ownerDisplayName}</small> : budget.categoryIsArchived ? <small>Archived category</small> : null}
                      </div>
                    </div>
                    <ProgressBar value={budget.percentageUsed} tone={tone} />
                    <div className="budget-card__metrics">
                      <span>{budget.percentageUsed.toFixed(2)}% used</span>
                      <span>{formatCurrency(budget.remaining)} remaining</span>
                      <span>Alert at {budget.alertThresholdPercent}%</span>
                    </div>
                    {budget.canManage ? (
                      <div className="inline-actions">
                        <button type="button" className="ghost-button ghost-button--small" onClick={() => editBudget(budget)}>Edit</button>
                        <button type="button" className="ghost-button ghost-button--small" onClick={() => deleteBudget(budget.id)}>Delete</button>
                      </div>
                    ) : (
                      <div className="budget-card__metrics">
                        <span>Read-only shared budget from {budget.ownerDisplayName}</span>
                      </div>
                    )}
                  </article>
                );
              })}
            </div>
          )}
        </section>
      </div>
    </div>
  );
}


