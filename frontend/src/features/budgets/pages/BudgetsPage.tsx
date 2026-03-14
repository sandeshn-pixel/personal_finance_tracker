import { useEffect, useState } from "react";
import { zodResolver } from "@hookform/resolvers/zod";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { useAuth } from "../../../app/providers/AuthProvider";
import { categoriesApi, type CategoryDto } from "../../categories/api/categoriesApi";
import { budgetsApi, type BudgetDto, type BudgetSummaryDto } from "../api/budgetsApi";
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

export function BudgetsPage() {
  const { accessToken } = useAuth();
  const today = new Date();
  const [selectedYear, setSelectedYear] = useState(today.getFullYear());
  const [selectedMonth, setSelectedMonth] = useState(today.getMonth() + 1);
  const [categories, setCategories] = useState<CategoryDto[]>([]);
  const [budgets, setBudgets] = useState<BudgetDto[]>([]);
  const [summary, setSummary] = useState<BudgetSummaryDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [editing, setEditing] = useState<BudgetDto | null>(null);

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
      setErrorMessage(null);
    } catch (error) {
      setErrorMessage(error instanceof ApiError ? error.message : "Unable to load budget categories.");
    } finally {
      setLoading(false);
    }
  }

  async function loadBudgets() {
    if (!accessToken) return;
    setLoading(true);
    try {
      const [budgetResponse, summaryResponse] = await Promise.all([
        budgetsApi.list(accessToken, selectedYear, selectedMonth),
        budgetsApi.summary(accessToken, selectedYear, selectedMonth),
      ]);
      setBudgets(budgetResponse);
      setSummary(summaryResponse);
      setErrorMessage(null);
    } catch (error) {
      setErrorMessage(error instanceof ApiError ? error.message : "Unable to load budgets.");
    } finally {
      setLoading(false);
    }
  }

  function resetForm() {
    setEditing(null);
    reset({ categoryId: categories[0]?.id ?? "", amount: 0, alertThresholdPercent: 80 });
  }

  function editBudget(budget: BudgetDto) {
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
      setErrorMessage(error instanceof ApiError ? error.message : "Unable to delete budget.");
    }
  }

  async function copyPreviousMonth() {
    if (!accessToken) return;

    try {
      await budgetsApi.copyPreviousMonth(accessToken, { year: selectedYear, month: selectedMonth, overwriteExisting: false });
      await loadBudgets();
    } catch (error) {
      setErrorMessage(error instanceof ApiError ? error.message : "Unable to copy budgets from the previous month.");
    }
  }

  const yearOptions = [today.getFullYear() - 1, today.getFullYear(), today.getFullYear() + 1];

  if (loading && !summary && budgets.length === 0 && categories.length === 0) {
    return <PageLoader label="Loading budgets" />;
  }

  return (
    <div className="page-stack">
      <SectionHeader
        title="Budgets"
        description="Plan monthly category limits and compare them against actual expense activity."
        action={<Button type="button" onClick={resetForm}>New budget</Button>}
      />
      {errorMessage ? <Alert message={errorMessage} /> : null}
      <FilterRow action={<Button type="button" onClick={copyPreviousMonth}>Copy previous month</Button>}>
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
        <StatCard label="Budgeted" value={formatCurrency(summary?.totalBudgeted ?? 0)} hint="Total planned expense for the month." />
        <StatCard label="Spent" value={formatCurrency(summary?.totalSpent ?? 0)} hint="Actual expense transactions matched to budgeted categories." tone="negative" />
        <StatCard label="Remaining" value={formatCurrency(summary?.totalRemaining ?? 0)} hint="Budgeted minus actual spent." tone={(summary?.totalRemaining ?? 0) < 0 ? "negative" : "positive"} />
        <StatCard label="Risk flags" value={`${summary?.overBudgetCount ?? 0} over / ${summary?.thresholdReachedCount ?? 0} warning`} hint="Categories beyond threshold or budget limit." />
      </div>
      <div className="budget-layout">
        <section className="panel-card panel-card--form">
          <div className="panel-card__header">
            <h3>{editing ? "Edit budget" : `Create budget for ${monthNames[selectedMonth - 1]} ${selectedYear}`}</h3>
            <p>{editing ? "Adjust planned amount or alert threshold." : "Each expense category can only have one budget per month."}</p>
          </div>
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
        </section>

        <section className="panel-card">
          <div className="panel-card__header">
            <h3>{monthNames[selectedMonth - 1]} budgets</h3>
            <p>{budgets.length} category budgets configured for this month.</p>
          </div>
          {budgets.length === 0 ? (
            <EmptyState title="No budgets yet" description="Create a category budget or copy the previous month to start tracking spending discipline." />
          ) : (
            <div className="budget-list">
              {budgets.map((budget) => {
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
                        {budget.categoryIsArchived ? <small>Archived category</small> : null}
                      </div>
                    </div>
                    <ProgressBar value={budget.percentageUsed} tone={tone} />
                    <div className="budget-card__metrics">
                      <span>{budget.percentageUsed.toFixed(2)}% used</span>
                      <span>{formatCurrency(budget.remaining)} remaining</span>
                      <span>Alert at {budget.alertThresholdPercent}%</span>
                    </div>
                    <div className="inline-actions">
                      <button type="button" className="ghost-button ghost-button--small" onClick={() => editBudget(budget)}>Edit</button>
                      <button type="button" className="ghost-button ghost-button--small" onClick={() => deleteBudget(budget.id)}>Delete</button>
                    </div>
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
